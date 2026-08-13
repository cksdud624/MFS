using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 공격 판정 전용.
    /// 트리거 콜라이더를 켜고 끄는 대신, 판정이 필요한 프레임에만 박스를 쏴서 대상을 찾는다.
    /// </summary>
    public class HitBoxController : MonoBehaviour
    {
        private ObjectContext _objectContext;
        private ContactFilter2D _filter;

        private readonly Collider2D[] _results = new Collider2D[HitBoxMaxDetectCount];
        //이번 판정에서 새로 맞은 대상만 담아서 넘긴다
        private readonly List<Collider2D> _newHits = new(HitBoxMaxDetectCount);
        //같은 공격에서 같은 대상을 여러 번 때리지 않도록 기록한다
        private readonly HashSet<Collider2D> _hitTargets = new();

        public async UniTask Init(ObjectContext objectContext)
        {
            _objectContext = objectContext;

            int hitBoxLayer = LayerMask.NameToLayer(LayerHitBox);
            if (hitBoxLayer < 0)
            {
                Debug.LogError($"Layer {LayerHitBox} is not defined.");
                hitBoxLayer = 0;
            }

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = 1 << hitBoxLayer,
                //피격 판정은 트리거 콜라이더로 두는 경우가 많으므로 같이 잡는다
                useTriggers = true
            };

            await UniTask.CompletedTask;
        }

        /// <summary>공격(또는 콤보 한 단계)의 시작. 중복 히트 기록을 비운다</summary>
        public void BeginAttack() => _hitTargets.Clear();

        /// <summary>
        /// 지금 위치에서 박스를 쏴서 이번 공격에 아직 맞지 않은 대상만 돌려준다.
        /// offset.x는 캐릭터가 보는 방향 기준이며 왼쪽을 볼 때 자동으로 뒤집힌다.
        /// 반환 리스트는 재사용되므로 호출한 쪽에서 보관하지 않는다.
        /// </summary>
        public IReadOnlyList<Collider2D> Detect(Vector2 offset, Vector2 size)
        {
            _newHits.Clear();

            Vector2 center = GetCenter(offset);
            int count = Physics2D.OverlapBox(center, size, 0f, _filter, _results);
            for (int i = 0; i < count; i++)
            {
                var target = _results[i];
                //자기 자신과 자기 하위 오브젝트는 제외
                if (target.transform.IsChildOf(transform)) continue;
                if (!_hitTargets.Add(target)) continue;

                _newHits.Add(target);
            }

#if UNITY_EDITOR
            _gizmoCenter = center;
            _gizmoSize = size;
            _gizmoTime = Time.time;
            _gizmoHit = _newHits.Count > 0;
#endif
            return _newHits;
        }

        /// <summary>판정 박스의 월드 중심. 보는 방향에 따라 좌우로 뒤집는다</summary>
        private Vector2 GetCenter(Vector2 offset)
        {
            float sign = _objectContext.Direction == Direction.Right ? 1f : -1f;
            return (Vector2)transform.position + new Vector2(offset.x * sign, offset.y);
        }

#if UNITY_EDITOR
        //판정 범위를 눈으로 맞추기 위한 디버그 표시
        private Vector2 _gizmoCenter;
        private Vector2 _gizmoSize;
        private float _gizmoTime = float.NegativeInfinity;
        private bool _gizmoHit;

        private void OnDrawGizmos()
        {
            if (Time.time - _gizmoTime > HitBoxGizmoDuration) return;

            Gizmos.color = _gizmoHit ? Color.red : Color.yellow;
            Gizmos.DrawWireCube(_gizmoCenter, _gizmoSize);
        }
#endif
    }
}
