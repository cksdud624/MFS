using System.Collections.Generic;
using Common;
using Common.Template.Interface;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 오브젝트의 몸을 움직인다. 이동 입력, 점프, 대시, 공격의 전진을 받아서 매 물리 스텝의 속도와 중력을 정한다.
    /// 가로 속도는 입력이 원하는 속도에 바로 맞추지 않고 가속과 감속을 거쳐 다가간다.
    /// 중력은 세로 속도에 따라 배율을 바꿔서, 빠르게 뛰어올라 천천히 내려오게 한다
    /// </summary>
    public class PhysicsController : MonoBehaviour, IFixedUpdateable
    {
        #region Fields
        public Rigidbody2D Rigidbody { get; private set; }
        private BoxCollider2D _collider;
        private ObjectContext _objectContext;
        //이동 입력이 원하는 가로 속도 (입력 방향 × 걸음 속도). 실제 속도는 가속과 감속을 거쳐 여기에 다가간다
        private float _velocityX;
        //가던 방향과 반대로 누르고 있던 시간(초). MoveTurnHoldTime을 넘겨야 방향 전환으로 본다
        private float _turnHoldTimer;
        private bool _isDashing;
        private Vector2 _dashVelocity;
        private bool _isAttacking;
        //공격의 전진. 이 속도로 남은 물리 스텝 수만큼 나아간다. 스텝 수가 0이면 제자리에 묶여 있다
        private Vector2 _advanceVelocity;
        private int _advanceSteps;
        //공중 점프의 추진이 남은 시간(초). 이 동안 중력을 덜 받고 걸음 속도 초과분을 깎지 않는다
        private float _airJumpTimer;
        //공중 대시가 끝난 뒤 떠 있는 시간(초). 이 동안 중력을 덜 받는다
        private float _dashHangTimer;
        //중력 배율이 1일 때의 gravityScale. 프로젝트의 중력에 곱해서 Gravity가 되게 맞춘 값이다
        private float _gravityScale;
        //낭떠러지/벽 감지 레이캐스트가 볼 레이어.
        //레이캐스트가 트리거까지 맞추도록 설정돼 있어서 판정 박스(HitBox)를 빼두지 않으면 지형보다 먼저 걸린다
        private int _probeLayerMask;

        private readonly HashSet<Collider2D> _groundContacts = new();

        //캐릭터가 같이 쓰는 마찰 0 머티리얼. 처음 쓸 때 하나만 만들어 공유한다
        private static PhysicsMaterial2D _frictionless;
        #endregion

        #region Init
        public void Init(ObjectContext objectContext)
        {
            _objectContext = objectContext;

            if (!TryGetComponent(out Rigidbody2D body))
                body = gameObject.AddComponent<Rigidbody2D>();
            Rigidbody = body;
            //프로젝트의 중력이 0이면 나눌 수 없으므로 배율을 그대로 둔다. 그 설정에서는 어차피 떨어지지 않는다
            float projectGravity = Mathf.Abs(Physics2D.gravity.y);
            _gravityScale = projectGravity > 0f ? Gravity / projectGravity : 1f;
            Rigidbody.gravityScale = _gravityScale;
            Rigidbody.freezeRotation = true;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (!TryGetComponent(out _collider))
                _collider = gameObject.AddComponent<BoxCollider2D>();
            //콜라이더 크기와 위치는 오브젝트마다 다르므로 테이블 값을 쓴다
            _collider.size = _objectContext.ObjectData.ColliderSize;
            ApplyColliderOffset();
            //이동은 마찰이 아니라 매 물리 스텝 넣는 속도로 정한다(가속과 감속도 코드가 한다). 기본 마찰이 남아 있으면
            //공중에서 벽 쪽으로 미는 동안 벽과의 마찰이 낙하 속도를 깎아서 벽에 붙어 천천히 떨어진다
            _collider.sharedMaterial = GetFrictionlessMaterial();

            _probeLayerMask = Physics2D.DefaultRaycastLayers;
            if (TryGetLayer(LayerHitBox, out int hitBoxLayer))
                _probeLayerMask &= ~(1 << hitBoxLayer);

            //핸들러가 Rigidbody와 콜라이더를 바로 건드리므로 둘을 만든 뒤에 구독한다
            _objectContext.OnMoveVelocityChanged += OnMoveVelocityChanged;
            _objectContext.OnMoveSpeedLowered += OnMoveSpeedLowered;
            _objectContext.OnJumpVelocityChanged += OnJumpVelocityChanged;
            _objectContext.OnAirJump += OnAirJump;
            _objectContext.OnDashingChanged += OnDashingChanged;
            _objectContext.OnDashVelocityChanged += OnDashVelocityChanged;
            _objectContext.OnAttackingChanged += OnAttackingChanged;
            _objectContext.OnAttackAdvance += OnAttackAdvance;
            _objectContext.OnDirectionChanged += OnDirectionChanged;
            Global.Instance.BindFixedUpdate(this);
        }
        #endregion

        #region LifeCycle
        public void OnFixedUpdate()
        {
            if (_isAttacking)
            {
                //공격 중에는 제자리에 묶는다. 전진이 적힌 공격만 정해진 스텝 수만큼 나아간다
                Rigidbody.linearVelocity = ResolveAdvanceVelocity();
            }
            else if (_isDashing)
            {
                //지상 대시 중 낭떠러지 끝에 닿으면 전진만 차단
                float dashVelocityX = IsLedgeAhead(_dashVelocity.x) ? 0f : _dashVelocity.x;
                //세로는 대시를 시작할 때 넣은 속도에 중력이 쌓이게 둔다. 매 스텝 덮어쓰면 대시 중의 중력이 사라진다
                Rigidbody.linearVelocity = new Vector2(dashVelocityX, Rigidbody.linearVelocity.y);
            }
            else
            {
                Rigidbody.linearVelocity = new Vector2(ResolveVelocityX(), Rigidbody.linearVelocity.y);
            }

            UpdateGravity();
            _objectContext.SetGrounded(_groundContacts.Count > 0);
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindFixedUpdate(this);
            if (_objectContext != null)
            {
                _objectContext.OnMoveVelocityChanged -= OnMoveVelocityChanged;
                _objectContext.OnMoveSpeedLowered -= OnMoveSpeedLowered;
                _objectContext.OnJumpVelocityChanged -= OnJumpVelocityChanged;
                _objectContext.OnAirJump -= OnAirJump;
                _objectContext.OnDashingChanged -= OnDashingChanged;
                _objectContext.OnDashVelocityChanged -= OnDashVelocityChanged;
                _objectContext.OnAttackingChanged -= OnAttackingChanged;
                _objectContext.OnAttackAdvance -= OnAttackAdvance;
                _objectContext.OnDirectionChanged -= OnDirectionChanged;
            }
        }
        #endregion

        #region Public
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

            var hit = Physics2D.Raycast(origin, Vector2.down, LedgeProbeDepth + LedgeProbeMargin, _probeLayerMask);
            return hit.collider == null || !hit.collider.CompareTag(TagMap);
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

            var hit = Physics2D.Raycast(origin, new Vector2(direction, 0f), WallProbeDistance, _probeLayerMask);
            return hit.collider != null && hit.collider.CompareTag(TagMap);
        }

        /// <summary>
        /// 가로로 가던 속도를 지운다. 연출이 정한 자리에 세울 때 쓴다.
        /// 입력을 비우기만 하면 감속하는 동안 그 자리를 지나쳐서 멈춘다
        /// </summary>
        public void StopHorizontal()
            => Rigidbody.linearVelocity = new Vector2(0f, Rigidbody.linearVelocity.y);
        #endregion

        #region Event Handlers
        private void OnMoveVelocityChanged(float velocityX)
        {
            _velocityX = velocityX;
        }

        /// <summary>
        /// 걸음 속도가 느려지면 가던 속도도 그만큼 바로 줄인다. 걸음 속도를 넘어선 속도는 대시가 남긴 것으로 보고 서서히 풀기 때문에,
        /// 줄이지 않으면 빠르게 걷다가 느려졌을 때(테스트 키를 뗐을 때, 보스가 걷는 배율을 낮췄을 때)도 한참 미끄러진다
        /// </summary>
        private void OnMoveSpeedLowered(float moveSpeed)
        {
            //공격과 대시는 걸음 속도와 상관없이 자기 속도로 움직인다
            if (_isAttacking || _isDashing) return;

            var velocity = Rigidbody.linearVelocity;
            velocity.x = Mathf.Clamp(velocity.x, -moveSpeed, moveSpeed);
            Rigidbody.linearVelocity = velocity;
        }

        private void OnJumpVelocityChanged(float velocityY)
        {
            Rigidbody.linearVelocity = new Vector2(Rigidbody.linearVelocity.x, velocityY);
        }

        /// <summary>
        /// 공중 점프. 가던 속도에 누른 쪽의 가로 속도를 더하고, 떨어지던 속도는 지운 뒤 위로 뛴다.
        /// 걸음 속도를 넘어선 만큼은 추진 시간이 끝난 뒤부터 서서히 풀린다
        /// </summary>
        private void OnAirJump(float directionX)
        {
            var velocity = Rigidbody.linearVelocity;
            velocity.x += directionX * AirJumpHorizontalSpeed;
            velocity.y = Mathf.Max(velocity.y, 0f) + AirJumpVerticalSpeed;
            Rigidbody.linearVelocity = velocity;
            _airJumpTimer = AirJumpDuration;
        }

        private void OnDashingChanged(bool dashing)
        {
            _isDashing = dashing;
            if (dashing) return;

            //대시가 끝나면 속도를 조금만 남긴다. 가로는 걸음 속도보다 조금 빠르게 남아서 이어 달리는 느낌을 주고,
            //세로는 위로 솟구치거나 바닥으로 꽂히지 않게 자른다
            var velocity = Rigidbody.linearVelocity;
            float carrySpeed = _objectContext.MoveSpeed * DashCarryMultiplier;
            velocity.x = Mathf.Clamp(velocity.x, -carrySpeed, carrySpeed);
            velocity.y = Mathf.Clamp(velocity.y, -DashEndFallSpeed, JumpPower * DashEndUpMultiplier);
            Rigidbody.linearVelocity = velocity;

            //공중에서 끝났으면 바로 떨어지지 않고 잠깐 떠 있는다. 공중 대시 뒤에 다음 행동을 이을 틈이다
            if (!_objectContext.IsGrounded)
                _dashHangTimer = DashHangDuration;
        }

        private void OnDashVelocityChanged(Vector2 velocity)
        {
            _dashVelocity = velocity;
            Rigidbody.linearVelocity = velocity;
        }

        /// <summary>
        /// 공격 중에는 속도를 0으로 묶어둔다. 중력까지 꺼야 프레임 사이에 밀려 내려가지 않는다.
        /// 공격이 끝나면 중력을 바로 돌려놓는다. 세로 속도에 맞는 배율은 다음 물리 스텝에서 다시 정해진다.
        /// 끝날 때도 속도를 지운다. 전진이 공격과 같이 끝나는 돌진은 전진하던 속도가 남아 있어서, 지우지 않으면 끝난 뒤에도 한참 미끄러진다
        /// </summary>
        private void OnAttackingChanged(bool attacking)
        {
            _isAttacking = attacking;
            _advanceSteps = 0;
            Rigidbody.gravityScale = attacking ? 0f : _gravityScale;
            Rigidbody.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// 공격의 전진을 시작한다. 몸은 물리 스텝 단위로만 움직이므로 시간을 스텝 수로 바꾸고, 그 스텝 수에 offset을 다 가도록 속도를 맞춘다.
        /// 시간이 0이면 한 스텝에 간다 (한 걸음 내딛는 베기). 공격 단계가 시작될 때마다 불리고, 전진이 없는 단계는 0이 온다
        /// </summary>
        private void OnAttackAdvance(Vector2 offset, float duration)
        {
            _advanceSteps = 0;
            if (offset == Vector2.zero) return;

            _advanceSteps = Mathf.Max(1, Mathf.RoundToInt(duration / Time.fixedDeltaTime));
            _advanceVelocity = offset / (_advanceSteps * Time.fixedDeltaTime);
        }

        private void OnDirectionChanged(Direction direction) => ApplyColliderOffset();

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsGroundContact(collision))
                _groundContacts.Add(collision.collider);
        }

        private void OnCollisionExit2D(Collision2D collision) => _groundContacts.Remove(collision.collider);
        #endregion

        #region Private
        private void ApplyColliderOffset() => _collider.offset = _objectContext.ColliderOffset;

        /// <summary>
        /// 마찰 0 머티리얼. 섞는 방식을 Minimum으로 둬서 상대 콜라이더(벽, 바닥)의 마찰과 상관없이 늘 0이 되게 한다.
        /// 런타임에 만든 에셋은 씬이 내려가며 정리될 수 있어서 파괴되었으면 다시 만든다
        /// </summary>
        private static PhysicsMaterial2D GetFrictionlessMaterial()
        {
            if (_frictionless != null) return _frictionless;

            _frictionless = new PhysicsMaterial2D("Character Frictionless")
            {
                friction = 0f,
                frictionCombine = PhysicsMaterialCombine2D.Minimum,
            };
            return _frictionless;
        }

        private bool IsGroundContact(Collision2D collision)
        {
            if (!collision.collider.CompareTag(TagMap)) return false;

            foreach (var contact in collision.contacts)
            {
                if (contact.normal.y > GroundNormalThreshold)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 이번 스텝의 가로 속도. 지금 속도에서 입력이 원하는 속도로 가속과 감속을 거쳐 다가간다.
        /// 가속과 감속은 걸음 속도에 붙는 시간으로 정해서, 시나리오가 빠르게 걷게 하거나 테스트 키로 속도를 올려도 같은 박자로 붙고 멈춘다
        /// </summary>
        private float ResolveVelocityX()
        {
            float deltaTime = Time.fixedDeltaTime;
            float currentX = Rigidbody.linearVelocity.x;
            float moveSpeed = _objectContext.MoveSpeed;

            if (_velocityX == 0f)
            {
                //입력을 떼면 멈춰간다. 공중과 피격 중에는 덜 깎아서 가던 쪽으로 더 미끄러진다
                _turnHoldTimer = 0f;
                bool onGround = _objectContext.IsGrounded && !_objectContext.IsDamaged;
                float deceleration = moveSpeed / MoveDecelerationTime * (onGround ? 1f : AirDecelerationMultiplier);
                currentX = Mathf.MoveTowards(currentX, 0f, deceleration * deltaTime);
            }
            else if (currentX * _velocityX < 0f && Mathf.Abs(currentX) > MoveTurnSpeedThreshold)
            {
                //가던 방향과 반대로 눌렀다. 잠깐 누른 것은 보는 방향만 바꾼 것으로 보고 속도를 그대로 두고,
                //계속 누르고 있으면 방향 전환으로 보고 빠르게 돌려세운다
                _turnHoldTimer += deltaTime;
                if (_turnHoldTimer >= MoveTurnHoldTime)
                    currentX = Mathf.MoveTowards(currentX, _velocityX, moveSpeed / MoveTurnTime * deltaTime);
            }
            else
            {
                _turnHoldTimer = 0f;
                //걸음 속도를 넘어서 있으면(대시 직후) 더 밀지 않고 아래에서 깎는다
                if (Mathf.Abs(currentX) <= moveSpeed)
                    currentX = Mathf.MoveTowards(currentX, _velocityX, moveSpeed / MoveAccelerationTime * deltaTime);
            }

            //대시나 공중 점프로 걸음 속도를 넘어선 만큼은 서서히 푼다. 공중 점프가 밀어주는 동안에는 깎지 않는다
            if (Mathf.Abs(currentX) > moveSpeed && _airJumpTimer <= 0f)
            {
                float overSpeedDeceleration = moveSpeed / MoveDecelerationTime * OverSpeedDecelerationMultiplier;
                currentX = Mathf.MoveTowards(currentX, Mathf.Sign(currentX) * moveSpeed, overSpeedDeceleration * deltaTime);
            }
            return currentX;
        }

        //전진할 스텝이 남아 있으면 그 속도, 아니면 제자리. 지상에서 낭떠러지 끝에 닿으면 대시와 같이 전진만 막는다
        private Vector2 ResolveAdvanceVelocity()
        {
            if (_advanceSteps <= 0) return Vector2.zero;

            _advanceSteps--;
            var velocity = _advanceVelocity;
            if (IsLedgeAhead(velocity.x))
                velocity.x = 0f;
            return velocity;
        }

        /// <summary>
        /// 이번 스텝에 받을 중력을 정한다. 올라갈 때는 그대로, 정점 근처에서는 조금 가볍게, 떨어질 때는 훨씬 가볍게 받는다.
        /// 대시 중과 공중 점프 직후, 공중 대시 직후에는 거기에 더 가볍게 받는다. 공격 중에는 중력을 꺼둔 채로 둔다
        /// </summary>
        private void UpdateGravity()
        {
            float deltaTime = Time.fixedDeltaTime;
            if (_airJumpTimer > 0f)
                _airJumpTimer -= deltaTime;
            if (_dashHangTimer > 0f)
                _dashHangTimer -= deltaTime;
            if (_isAttacking) return;

            float velocityY = Rigidbody.linearVelocity.y;
            float multiplier;
            if (_isDashing)
            {
                multiplier = DashGravityMultiplier;
            }
            else
            {
                multiplier = velocityY > ApexSpeedBand ? 1f
                    : velocityY > -ApexSpeedBand ? ApexGravityMultiplier
                    : FallGravityMultiplier;
                if (_airJumpTimer > 0f)
                    multiplier *= AirJumpGravityMultiplier;
                else if (_dashHangTimer > 0f)
                    multiplier *= DashHangGravityMultiplier;
            }
            Rigidbody.gravityScale = _gravityScale * multiplier;

            if (velocityY < -MaxFallSpeed)
                Rigidbody.linearVelocity = new Vector2(Rigidbody.linearVelocity.x, -MaxFallSpeed);
        }
        #endregion
    }
}
