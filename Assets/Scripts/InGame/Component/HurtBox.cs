using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 오브젝트가 맞는 판정. 캐릭터가 아닌 오브젝트도 가진다.
    /// 본체 콜라이더는 지형과 부딪히는 용도이므로 공격 판정은 이쪽만 본다.
    /// HitBox 레이어의 사각형 트리거 하나를 들고 있고, 콜라이더를 InGameContext에 등록해둔다.
    /// 공격 판정은 등록된 콜라이더만 맞은 것으로 보고, 거기서 주인의 진영을 확인한다.
    /// Rigidbody는 본체 것을 그대로 쓰므로 붙이지 않는다
    /// </summary>
    public class HurtBox : MonoBehaviour
    {
        private InGameContext _inGameContext;
        private ObjectContext _objectContext;
        private BoxCollider2D _collider;

        public void Init(InGameContext inGameContext, ObjectContext objectContext)
        {
            _inGameContext = inGameContext;
            _objectContext = objectContext;
            _objectContext.OnDirectionChanged += OnDirectionChanged;

            int hitBoxLayer = LayerMask.NameToLayer(LayerHitBox);
            if (hitBoxLayer < 0)
                Debug.LogError($"Layer {LayerHitBox} is not defined.");
            else
                gameObject.layer = hitBoxLayer;

            _collider = gameObject.AddComponent<BoxCollider2D>();
            _collider.isTrigger = true;
            _collider.size = _objectContext.HurtBoxSize;
            ApplyOffset();

            _inGameContext.RegisterHurtBox(_collider, _objectContext);
        }

        //오프셋은 보는 방향 기준이므로 돌아설 때마다 다시 맞춘다
        private void OnDirectionChanged(Direction direction) => ApplyOffset();
        private void ApplyOffset() => _collider.offset = _objectContext.HurtBoxOffset;

        private void OnDestroy()
        {
            _inGameContext?.UnregisterHurtBox(_collider);

            if (_objectContext == null) return;
            _objectContext.OnDirectionChanged -= OnDirectionChanged;
        }

#if UNITY_EDITOR
        //공격 판정과 겹쳐 보며 맞추기 위한 디버그 표시. 항상 그린다
        private void OnDrawGizmos()
        {
            if (_collider == null) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_collider.bounds.center, _collider.bounds.size);
        }
#endif
    }
}
