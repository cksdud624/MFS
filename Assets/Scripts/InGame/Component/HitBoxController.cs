using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 이펙트를 쓰지 않는 공격의 판정 전용.
    /// 이펙트가 있는 공격은 그 이펙트가 판정을 들고 다니므로(AttackHitBox) 여기서는 보지 않는다.
    /// 판정 박스마다 사각형 트리거 콜라이더를 자식으로 하나씩 만들어두고, 켜야 하는 동안만 켠다.
    /// 콜라이더는 캐릭터 본체의 Rigidbody에 딸려가므로 트리거 콜백은 이 오브젝트로 들어온다.
    /// </summary>
    public class HitBoxController : MonoBehaviour
    {
        private ObjectContext _objectContext;
        //맞출 수 있는 대상이 있는 레이어
        private int _targetLayerMask;

        //판정 박스마다 만들어둔 콜라이더. 한 번 만들면 켜고 끄면서 계속 재사용한다
        private readonly Dictionary<AttackHitBoxData, BoxCollider2D> _colliders = new();
        //지금 켜져 있는 판정과 그 타입. 콜백이 어느 판정에서 났는지는 겹침을 다시 확인해서 가린다
        private readonly List<BoxCollider2D> _activeColliders = new();
        private readonly List<AttackType> _activeTypes = new();
        //같은 공격에서 같은 대상을 여러 번 때리지 않도록 기록한다.
        //마법과 기계는 각자 한 번씩 때려야 하므로 타입별로 따로 센다
        private readonly Dictionary<AttackType, HashSet<Collider2D>> _hitTargets = new();

        public async UniTask Init(ObjectContext objectContext)
        {
            _objectContext = objectContext;
            _objectContext.OnAttackStart += BeginAttack;
            _objectContext.OnAttackHitBoxActiveChanged += SetHitBoxActive;

            int hitBoxLayer = LayerMask.NameToLayer(LayerHitBox);
            if (hitBoxLayer < 0)
            {
                Debug.LogError($"Layer {LayerHitBox} is not defined.");
                hitBoxLayer = 0;
            }
            _targetLayerMask = 1 << hitBoxLayer;

            await UniTask.CompletedTask;
        }

        /// <summary>공격(또는 콤보 한 단계)의 시작. 중복 히트 기록을 비우고 남은 판정을 끈다</summary>
        public void BeginAttack()
        {
            //타입별 기록만 비우고 집합 자체는 다음 공격에 다시 쓴다
            foreach (var targets in _hitTargets.Values)
                targets.Clear();

            //단계가 끝나기 전에 대시로 끊기는 등, 켜둔 채로 넘어온 판정이 있으면 여기서 확실히 끈다
            for (int i = 0; i < _activeColliders.Count; i++)
                _activeColliders[i].enabled = false;
            _activeColliders.Clear();
            _activeTypes.Clear();
        }

        /// <summary>
        /// 판정을 켜고 끈다. 켜는 시점과 유지시간은 커맨드를 굴리는 쪽이 정하고,
        /// 여기서는 그 시간 동안 콜라이더를 살려두기만 한다
        /// </summary>
        private void SetHitBoxActive(AttackHitBoxData hitBox, AttackType attackType, bool active, bool ownedByEffect)
        {
            //이펙트가 맡은 판정은 이펙트 쪽에서 켜고 끈다
            if (hitBox == null || ownedByEffect) return;

            var collider = GetCollider(hitBox);
            int index = _activeColliders.IndexOf(collider);

            if (!active)
            {
                collider.enabled = false;
                if (index < 0) return;

                _activeColliders.RemoveAt(index);
                _activeTypes.RemoveAt(index);
                return;
            }

            //offset.x는 캐릭터가 보는 방향 기준이라 왼쪽을 볼 때 뒤집는다.
            //공격 중에는 방향이 잠기므로 켜는 시점에 한 번만 맞춰두면 된다
            collider.transform.localPosition = _objectContext.GetDirectionalOffset(hitBox.HitBoxOffset);
            collider.size = hitBox.HitBoxSize;
            collider.enabled = true;

            if (index >= 0)
            {
                _activeTypes[index] = attackType;
                return;
            }

            _activeColliders.Add(collider);
            _activeTypes.Add(attackType);
        }

        /// <summary>판정 박스의 콜라이더. 처음 쓰는 박스면 자식으로 만들어둔다</summary>
        private BoxCollider2D GetCollider(AttackHitBoxData hitBox)
        {
            if (_colliders.TryGetValue(hitBox, out var collider))
                return collider;

            //Rigidbody는 본체 것을 그대로 쓰므로 붙이지 않는다. 붙이면 판정이 본체와 따로 놀게 된다
            var hitBoxObject = new GameObject($"HitBox_{hitBox.Id}");
            hitBoxObject.transform.SetParent(transform, false);

            collider = hitBoxObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.enabled = false;

            _colliders[hitBox] = collider;
            return collider;
        }

        //켜지는 순간 이미 겹쳐 있던 대상은 Enter가 오지 않을 수 있으므로 Stay도 같이 받는다
        private void OnTriggerEnter2D(Collider2D other) => DetectAttack(other);
        private void OnTriggerStay2D(Collider2D other) => DetectAttack(other);

        /// <summary>
        /// 트리거에 걸린 대상을 판정으로 처리한다.
        /// 콜백은 본체 콜라이더가 받은 접촉까지 같이 들어오므로 실제로 판정 박스와 겹칠 때만 본다.
        /// 데미지 처리가 생기기 전까지는 맞은 대상만 알린다
        /// </summary>
        private void DetectAttack(Collider2D other)
        {
            if (other == null || _activeColliders.Count == 0) return;
            //맞출 수 있는 대상만 본다
            if (((1 << other.gameObject.layer) & _targetLayerMask) == 0) return;
            //자기 자신과 자기 하위 오브젝트는 제외
            if (other.transform.IsChildOf(transform)) return;

            for (int i = 0; i < _activeColliders.Count; i++)
            {
                if (!_activeColliders[i].IsTouching(other)) continue;

                var attackType = _activeTypes[i];
                if (!_hitTargets.TryGetValue(attackType, out var hitTargets))
                {
                    hitTargets = new HashSet<Collider2D>();
                    _hitTargets[attackType] = hitTargets;
                }
                //한 단계에서 같은 대상을 여러 번 때리지 않는다
                if (!hitTargets.Add(other)) continue;

                //다음 커맨드가 히트를 요구할 수 있으므로 맞았다는 것을 남겨둔다.
                //타입을 가리지 않고 하나라도 맞으면 히트로 본다
                _objectContext.ReportAttackHit();
                Debug.Log($"{name} hit {other.transform.root.name} ({attackType})");
            }
        }

        private void OnDestroy()
        {
            if (_objectContext == null) return;

            _objectContext.OnAttackStart -= BeginAttack;
            _objectContext.OnAttackHitBoxActiveChanged -= SetHitBoxActive;
        }

#if UNITY_EDITOR
        //판정 범위를 눈으로 맞추기 위한 디버그 표시. 켜져 있는 동안만 그린다
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _activeColliders.Count; i++)
            {
                var collider = _activeColliders[i];
                if (collider == null) continue;

                Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
            }
        }
#endif
    }
}
