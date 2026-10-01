using System.Collections.Generic;
using Generated.Table;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 이펙트가 들고 다니는 공격 판정.
    /// 판정 박스마다 사각형 트리거 콜라이더를 자식으로 만들어두고, 켜야 하는 동안만 켠다.
    /// 이펙트는 주인과 떨어진 별개의 오브젝트라 자기 Rigidbody가 없으면 트리거 콜백을 받지 못하므로
    /// 충돌도 중력도 없는 Kinematic Rigidbody를 하나 달아둔다.
    /// 이펙트가 수명을 다해 사라지면 판정도 같이 사라진다.
    /// </summary>
    public class AttackHitBox : MonoBehaviour
    {
        //맞은 콜라이더로 대상을 찾는다
        private InGameContext _inGameContext;
        private ObjectContext _objectContext;
        //판정을 낸 주인. 히트를 알릴 때 쓴다
        private Transform _owner;
        private AttackType _attackType;
        //판정 박스를 둘 레이어. 맞출 대상(HurtBox)도 같은 레이어에 있다
        private int _hitBoxLayer;
        private int _targetLayerMask;

        //판정 박스마다 만들어둔 콜라이더. 한 번 만들면 켜고 끄면서 계속 재사용한다
        private readonly Dictionary<AttackHitBoxData, BoxCollider2D> _colliders = new();
        //지금 켜져 있는 판정
        private readonly List<BoxCollider2D> _activeColliders = new();
        //같은 공격에서 같은 대상을 여러 번 때리지 않도록 기록한다.
        //이 인스턴스는 한 타입의 한 단계만 맡으므로 집합 하나로 충분하다
        private readonly HashSet<ObjectContext> _hitTargets = new();

        public void Init(InGameContext inGameContext, ObjectContext objectContext, Transform owner, AttackType attackType)
        {
            _inGameContext = inGameContext;
            _objectContext = objectContext;
            _owner = owner;
            _attackType = attackType;
            _objectContext.OnAttackHitBoxActiveChanged += SetHitBoxActive;
            //이펙트가 단계보다 오래 살아남을 수 있다. 다음 단계가 시작되면 그 판정까지 받지 않도록 손을 뗀다
            _objectContext.OnAttackStart += Retire;

            _hitBoxLayer = LayerMask.NameToLayer(LayerHitBox);
            if (_hitBoxLayer < 0)
            {
                Debug.LogError($"Layer {LayerHitBox} is not defined.");
                _hitBoxLayer = 0;
            }
            _targetLayerMask = 1 << _hitBoxLayer;

            //판정은 이펙트 물리와 섞이면 안 되므로 중력도 충돌도 없는 Kinematic으로 둔다.
            //움직이지 않는 대상과도 트리거를 주고받으려면 useFullKinematicContacts를 켜둬야 한다.
            //프리팹이 이미 Rigidbody를 들고 있으면 AddComponent가 null을 돌려주므로 있는 것을 덮어 쓴다
            if (!TryGetComponent<Rigidbody2D>(out var body))
                body = gameObject.AddComponent<Rigidbody2D>();
            else
                Debug.LogWarning($"Effect {name} already has Rigidbody2D. It is overridden to Kinematic for the attack hit box.");
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
        }

        /// <summary>
        /// 판정을 켜고 끈다. 켜는 시점과 유지시간은 커맨드를 굴리는 쪽이 정하고,
        /// 여기서는 그 시간 동안 콜라이더를 살려두기만 한다.
        /// 내 타입의, 이펙트가 맡기로 한 판정만 받는다
        /// </summary>
        private void SetHitBoxActive(AttackHitBoxData hitBox, AttackType attackType, bool active, bool ownedByEffect)
        {
            if (hitBox == null || !ownedByEffect || attackType != _attackType) return;

            var collider = GetCollider(hitBox);
            if (!active)
            {
                collider.enabled = false;
                _activeColliders.Remove(collider);
                return;
            }

            //판정 오브젝트는 이펙트 중점에 두고, 테이블의 오프셋은 콜라이더 offset으로 준다.
            //오프셋은 오른쪽을 보는 기준이다. 이펙트 루트가 방향에 따라 돌아가 있을 수도,
            //프리팹 안쪽만 돌아가 있을 수도 있어서 회전을 월드 기준으로 풀고 보는 방향으로 직접 뒤집는다
            collider.transform.localPosition = Vector3.zero;
            collider.transform.rotation = Quaternion.identity;
            collider.offset = _objectContext.GetDirectionalOffset(hitBox.HitBoxOffset);
            collider.size = hitBox.HitBoxSize;
            collider.enabled = true;

            if (!_activeColliders.Contains(collider))
                _activeColliders.Add(collider);
        }

        /// <summary>판정 박스의 콜라이더. 처음 쓰는 박스면 자식으로 만들어둔다</summary>
        private BoxCollider2D GetCollider(AttackHitBoxData hitBox)
        {
            if (_colliders.TryGetValue(hitBox, out var collider))
                return collider;

            var hitBoxObject = new GameObject($"HitBox_{hitBox.Id}");
            hitBoxObject.transform.SetParent(transform, false);
            hitBoxObject.layer = _hitBoxLayer;

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
        /// 콜백은 이 이펙트의 콜라이더 전부를 묶어서 들어오므로 어느 판정에 걸렸는지는 따지지 않는다.
        /// 데미지 처리가 생기기 전까지는 맞은 대상만 알린다
        /// </summary>
        private void DetectAttack(Collider2D other)
        {
            if (other == null || _activeColliders.Count == 0) return;
            //맞출 수 있는 대상만 본다
            if (((1 << other.gameObject.layer) & _targetLayerMask) == 0) return;
            //공격 판정끼리도 같은 레이어라 겹치면 콜백이 오므로 등록된 피격 판정만 본다
            if (!_inGameContext.TryGetHurtBoxOwner(other, out var target)) return;
            //같은 진영은 때리지 않는다. 자기 자신도 여기서 걸러진다
            if (target.Team == _objectContext.Team) return;
            //한 단계에서 같은 대상을 여러 번 때리지 않는다
            if (!_hitTargets.Add(target)) return;

            //다음 커맨드가 히트를 요구할 수 있으므로 맞았다는 것을 남겨둔다.
            //타입을 가리지 않고 하나라도 맞으면 히트로 본다
            _objectContext.ReportAttackHit();
            target.RequestDamage();
            Debug.Log($"[{_objectContext.Team}] {_owner.name} hit [{target.Team}] {other.transform.root.name} ({_attackType})");
        }

        /// <summary>맡은 단계가 끝났다. 판정을 거두고 더 이상 신호를 받지 않는다. 연출은 수명대로 남는다</summary>
        private void Retire()
        {
            for (int i = 0; i < _activeColliders.Count; i++)
                _activeColliders[i].enabled = false;
            _activeColliders.Clear();

            if (_objectContext == null) return;

            _objectContext.OnAttackHitBoxActiveChanged -= SetHitBoxActive;
            _objectContext.OnAttackStart -= Retire;
        }

        private void OnDestroy() => Retire();

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
