using System;
using System.Collections.Generic;
using Common;
using Cysharp.Threading.Tasks;
using InGame.Context;
using UnityEngine;
using static Common.AssetKeys;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 오브젝트 이펙트 재생 전용.
    /// 프리팹은 오브젝트 생성 시점에 미리 로드해두고, 재생 요청이 오면 인스턴스를 만든다.
    /// 인스턴스는 재생을 시작한 자리에 남아야 하므로 자식이 아니라 월드에 띄우고,
    /// 수명이 끝나면 EffectInstance가 스스로 파괴한다.
    /// </summary>
    public class EffectPlayer : MonoBehaviour
    {
        private ObjectContext _objectContext;
        private long _objectId;
        private readonly Dictionary<EffectType, GameObject> _prefabs = new();
        //월드에 띄워둔 인스턴스. 오브젝트가 사라질 때 같이 정리하려고 들고 있는다
        private readonly List<EffectInstance> _instances = new();

        public async UniTask Init(ObjectContext objectContext, long objectId)
        {
            _objectContext = objectContext;
            _objectId = objectId;
            _objectContext.OnEffectPlay += Play;

            var assetManager = Global.Instance.AssetManager;
            var loadTasks = new List<UniTask>();
            foreach (EffectType effectType in Enum.GetValues(typeof(EffectType)))
                loadTasks.Add(LoadPrefab(assetManager, effectType));
            await UniTask.WhenAll(loadTasks);
        }

        //이펙트가 없는 오브젝트도 있으므로 없는 것은 에러로 보지 않는다
        private async UniTask LoadPrefab(AssetManager assetManager, EffectType effectType)
        {
            var prefab = await assetManager.LoadAssetAsync<GameObject>(LoadTarget.Effect, GetAssetName(effectType), logOnMissing: false);
            if (prefab == null) return;
            _prefabs[effectType] = prefab;
        }

        private string GetAssetName(EffectType effectType) => $"{_objectId}/{effectType}";

        /// <summary>direction은 이펙트가 향할 방향. 0이면 캐릭터가 보는 방향을 그대로 쓴다</summary>
        public void Play(EffectType effectType, Vector2 direction = default, float duration = 0f)
        {
            if (!_prefabs.TryGetValue(effectType, out var prefab)) return;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = _objectContext.Direction == Direction.Right ? Vector2.right : Vector2.left;

            //오브젝트 원점이 아니라 콜라이더 중점을 기준으로 잡고, 거기서 진행 방향으로 정해진 거리만큼 띄운다
            var origin = transform.position
                         + (Vector3)_objectContext.ColliderOffset
                         + (Vector3)(direction.normalized * GetDistance(effectType));
            //재생을 시작한 자리에 남아야 하므로 자식으로 붙이지 않고 월드에 띄운다
            var instance = Instantiate(prefab, origin, Quaternion.identity);
            var effectInstance = instance.AddComponent<EffectInstance>();
            effectInstance.Play(direction, duration);

            //수명이 끝난 인스턴스는 스스로 사라지므로 빈 자리만 걷어내고 새로 넣는다
            _instances.RemoveAll(played => played == null);
            _instances.Add(effectInstance);
        }

        //이펙트마다 중점에서 띄우는 거리가 다르므로 여기서 골라준다
        private static float GetDistance(EffectType effectType) => effectType switch
        {
            EffectType.Dash => DashEffectDistance,
            _ => 0f
        };

        private void OnDestroy()
        {
            if (_objectContext != null)
                _objectContext.OnEffectPlay -= Play;

            //인스턴스가 자식이 아니라 살아남으므로, 프리팹 핸들을 반납하기 전에 먼저 지운다
            foreach (var instance in _instances)
            {
                if (instance != null)
                    Destroy(instance.gameObject);
            }
            _instances.Clear();

            var assetManager = Global.Instance?.AssetManager;
            if (assetManager != null)
            {
                foreach (var effectType in _prefabs.Keys)
                    assetManager.ReleaseAsset<GameObject>(LoadTarget.Effect, GetAssetName(effectType));
            }

            _prefabs.Clear();
        }
    }
}
