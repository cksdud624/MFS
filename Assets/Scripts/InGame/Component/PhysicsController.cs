using System.Collections.Generic;
using Common;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using InGame.Context;
using UnityEngine;

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
        private const float JumpYVelocityCorrection = 1f;
        
        private readonly HashSet<Collider2D> _groundContacts = new();

        public async UniTask Init(ObjectContext objectContext)
        {
            _objectContext = objectContext;
            _objectContext.OnMoveVelocityChanged += OnMoveVelocityChanged;
            _objectContext.OnJumpVelocityChanged += OnJumpVelocityChanged;
            _objectContext.OnDashingChanged += OnDashingChanged;
            _objectContext.OnDashVelocityChanged += OnDashVelocityChanged;

            var rb = GetComponent<Rigidbody2D>();
            Rigidbody = rb != null ? rb : gameObject.AddComponent<Rigidbody2D>();
            Rigidbody.gravityScale = 1f;
            Rigidbody.freezeRotation = true;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = GetComponent<BoxCollider2D>();
            _collider = col != null ? col : gameObject.AddComponent<BoxCollider2D>();
            _collider.size = new Vector2(0.2f, 0.35f);
            _collider.offset = new Vector2(-0.07f, -0.05f);

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

        public void OnFixedUpdate()
        {
            Rigidbody.linearVelocity = _isDashing
                ? _dashVelocity
                : new Vector2(_velocityX, Rigidbody.linearVelocity.y);
            _objectContext.SetGrounded(_groundContacts.Count > 0);
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
            }
        }
    }
}
