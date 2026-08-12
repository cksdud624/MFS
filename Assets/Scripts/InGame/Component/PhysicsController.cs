using System.Collections.Generic;
using Common;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using InGame.Context;
using UnityEngine;
using Direction = Common.GameDefine.Direction;

namespace InGame.Component
{
    public class PhysicsController : MonoBehaviour, IFixedUpdateable
    {
        public Rigidbody2D Rigidbody { get; private set; }
        private BoxCollider2D _collider;
        private ObjectContext _objectContext;
        private float _velocityX;
        private bool _isDashing;
        private Vector2 _dashVelocity;

        private const float GroundNormalThreshold = 0.5f;
        //낭떠러지 감지용
        private const float LedgeProbeMargin = 0.02f;
        private const float LedgeProbeDepth = 0.1f;
        //벽 감지용
        private const float WallProbeDistance = 0.05f;
        //콜라이더는 방향에 따라 좌우 대칭으로 뒤집는다
        private static readonly Vector2 ColliderSize = new(0.2f, 0.35f);
        private const float ColliderOffsetX = 0.07f;
        private const float ColliderOffsetY = -0.05f;
        //공중 관성: MoveSpeed까지 붙는 시간 / 입력을 뗐을 때 멈추는 시간 / 최고 속도 초과분을 깎는 시간
        private const float AirAccelerationTime = 0.25f;
        private const float AirDecelerationTime = 1.2f;
        private const float AirOverSpeedDecelerationTime = 0.3f;
        
        private readonly HashSet<Collider2D> _groundContacts = new();

        public async UniTask Init(ObjectContext objectContext)
        {
            _objectContext = objectContext;
            _objectContext.OnMoveVelocityChanged += OnMoveVelocityChanged;
            _objectContext.OnJumpVelocityChanged += OnJumpVelocityChanged;
            _objectContext.OnDashingChanged += OnDashingChanged;
            _objectContext.OnDashVelocityChanged += OnDashVelocityChanged;
            _objectContext.OnDirectionChanged += OnDirectionChanged;

            var rb = GetComponent<Rigidbody2D>();
            Rigidbody = rb != null ? rb : gameObject.AddComponent<Rigidbody2D>();
            Rigidbody.gravityScale = 1f;
            Rigidbody.freezeRotation = true;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = GetComponent<BoxCollider2D>();
            _collider = col != null ? col : gameObject.AddComponent<BoxCollider2D>();
            _collider.size = ColliderSize;
            ApplyColliderOffset(_objectContext.Direction);

            Global.Instance.BindFixedUpdate(this);

            await UniTask.CompletedTask;
        }

        private void OnMoveVelocityChanged(float velocityX)
        {
            _velocityX = velocityX;
        }

        private void OnJumpVelocityChanged(float velocityY)
        {
            Rigidbody.linearVelocity = new Vector2(Rigidbody.linearVelocity.x, velocityY);
        }

        private void OnDashingChanged(bool dashing)
        {
            _isDashing = dashing;
            if (dashing) return;

            float velocityY = _dashVelocity.y switch
            {
                > 0f => _dashVelocity.y / 2f,
                < 0f => -_dashVelocity.y / 2f,
                _ => Rigidbody.linearVelocity.y
            };
            Rigidbody.linearVelocity = new Vector2(Rigidbody.linearVelocity.x, velocityY);
        }

        private void OnDashVelocityChanged(Vector2 velocity)
        {
            _dashVelocity = velocity;
            Rigidbody.linearVelocity = velocity;
        }

        private void OnDirectionChanged(Direction direction) => ApplyColliderOffset(direction);

        private void ApplyColliderOffset(Direction direction)
        {
            float offsetX = direction == Direction.Left ? ColliderOffsetX : -ColliderOffsetX;
            _collider.offset = new Vector2(offsetX, ColliderOffsetY);
        }

        public void OnFixedUpdate()
        {
            if (_isDashing)
            {
                var dashVelocity = _dashVelocity;
                //지상 대시 중 낭떠러지 끝에 닿으면 전진만 차단
                if (IsLedgeAhead(dashVelocity.x))
                    dashVelocity.x = 0f;
                Rigidbody.linearVelocity = dashVelocity;
            }
            else
            {
                //지상은 즉시 반응, 공중은 관성을 받는다
                float velocityX = _objectContext.IsGrounded ? _velocityX : ResolveAirVelocityX();
                Rigidbody.linearVelocity = new Vector2(velocityX, Rigidbody.linearVelocity.y);
            }
            _objectContext.SetGrounded(_groundContacts.Count > 0);
        }

        private float ResolveAirVelocityX()
        {
            float currentX = Rigidbody.linearVelocity.x;
            float moveSpeed = _objectContext.ObjectData.MoveSpeed;

            //대시 등으로 최고 속도를 넘어선 구간은 먼저 빠르게 깎는다
            if (Mathf.Abs(currentX) > moveSpeed)
            {
                //진행 방향과 반대로 입력하면 최고 속도에서 멈추지 않고 그대로 반전까지 이어간다
                float overSpeedTarget = _velocityX * currentX < 0f
                    ? _velocityX
                    : Mathf.Sign(currentX) * moveSpeed;
                float overSpeedDelta = moveSpeed / AirOverSpeedDecelerationTime * Time.fixedDeltaTime;
                return Mathf.MoveTowards(currentX, overSpeedTarget, overSpeedDelta);
            }

            float duration = _velocityX == 0f ? AirDecelerationTime : AirAccelerationTime;
            float maxDelta = moveSpeed / duration * Time.fixedDeltaTime;
            return Mathf.MoveTowards(currentX, _velocityX, maxDelta);
        }

        /// <summary>진행 방향 바로 앞이 낭떠러지인지. 지상에서만 판정한다</summary>
        public bool IsLedgeAhead(float velocityX)
        {
            if (velocityX == 0f) return false;
            if (!_objectContext.IsGrounded) return false;

            var bounds = _collider.bounds;
            float direction = Mathf.Sign(velocityX);
            //이번 프레임에 이동할 거리만큼 미리 내다봐서 끝을 넘어가는 것을 막는다
            float probeDistance = Mathf.Abs(velocityX) * Time.fixedDeltaTime + LedgeProbeMargin;
            var origin = new Vector2(
                direction > 0f ? bounds.max.x + probeDistance : bounds.min.x - probeDistance,
                bounds.min.y + LedgeProbeMargin);

            var hit = Physics2D.Raycast(origin, Vector2.down, LedgeProbeDepth + LedgeProbeMargin);
            return hit.collider == null || !hit.collider.CompareTag("Map");
        }

        /// <summary>진행 방향 바로 앞이 벽인지</summary>
        public bool IsWallAhead(float directionX)
        {
            if (directionX == 0f) return false;

            var bounds = _collider.bounds;
            float direction = Mathf.Sign(directionX);
            //자기 콜라이더에 맞지 않도록 바깥에서 쏜다
            var origin = new Vector2(
                direction > 0f ? bounds.max.x + LedgeProbeMargin : bounds.min.x - LedgeProbeMargin,
                bounds.center.y);

            var hit = Physics2D.Raycast(origin, new Vector2(direction, 0f), WallProbeDistance);
            return hit.collider != null && hit.collider.CompareTag("Map");
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsGroundContact(collision))
                _groundContacts.Add(collision.collider);
        }

        private void OnCollisionExit2D(Collision2D collision) => _groundContacts.Remove(collision.collider);

        private bool IsGroundContact(Collision2D collision)
        {
            if (!collision.collider.CompareTag("Map")) return false;

            foreach (var contact in collision.contacts)
            {
                if (contact.normal.y > GroundNormalThreshold)
                    return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindFixedUpdate(this);
            if (_objectContext != null)
            {
                _objectContext.OnMoveVelocityChanged -= OnMoveVelocityChanged;
                _objectContext.OnJumpVelocityChanged -= OnJumpVelocityChanged;
                _objectContext.OnDashingChanged -= OnDashingChanged;
                _objectContext.OnDashVelocityChanged -= OnDashVelocityChanged;
                _objectContext.OnDirectionChanged -= OnDirectionChanged;
            }
        }
    }
}
