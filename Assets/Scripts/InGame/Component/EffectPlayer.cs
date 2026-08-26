using System;
using System.Collections.Generic;
using Common;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using UnityEngine;
using static Common.AssetKeys;
using static Common.GameDefine;

namespace InGame.Component
{
    /// <summary>
    /// 오브젝트 이펙트 재생 전용.
    /// 프리팹은 오브젝트 생성 시점에 미리 로드해두고, 재생 요청이 오면 인스턴스를 만든다.
    /// 한 번 터지고 마는 이펙트는 재생을 시작한 자리에 남아야 하므로 월드에 띄우고,
    /// 대시처럼 재생 내내 뿜는 이펙트는 오브젝트를 따라가야 하므로 자식으로 붙인다.
    /// 수명이 끝나면 EffectInstance가 스스로 파괴한다.
    /// </summary>
    public class EffectPlayer : MonoBehaviour
    {
        private ObjectContext _objectContext;
        private ObjectData _objectData;
        //에셋 이름(1001/Dash, 1001/Attack1 …)으로 들고 있는다. 공격은 커맨드 단계마다 프리팹이 달라진다
        private readonly Dictionary<string, GameObject> _prefabs = new();
        //띄워둔 인스턴스. 오브젝트가 사라질 때 같이 정리하려고 들고 있는다
        private readonly List<EffectInstance> _instances = new();

        public async UniTask Init(ObjectContext objectContext, ObjectData objectData)
        {
            _objectContext = objectContext;
            _objectData = objectData;
            _objectContext.OnEffectPlay += Play;

            var assetManager = Global.Instance.AssetManager;
            var loadTasks = new List<UniTask>();
            //이펙트가 없는 오브젝트도 있으므로 번호 없는 기본 프리팹은 없어도 에러로 보지 않는다
            foreach (EffectType effectType in Enum.GetValues(typeof(EffectType)))
                loadTasks.Add(LoadPrefab(assetManager, GetAssetName(effectType, 0), logOnMissing: false));

            //공격은 커맨드 단계마다 다른 이펙트를 쓰므로 테이블에 적힌 번호만큼 더 받아둔다.
            //테이블이 번호를 지정했는데 프리팹이 없으면 만들다 만 것이므로 에러로 알린다
            var effects = Global.Instance.TableManager.AttackCommandRecord.GetEffects(objectData);
            if (effects != null)
            {
                foreach (int variant in effects)
                    loadTasks.Add(LoadPrefab(assetManager, GetAssetName(EffectType.Attack, variant), logOnMissing: true));
            }

            await UniTask.WhenAll(loadTasks);
        }

        private async UniTask LoadPrefab(AssetManager assetManager, string assetName, bool logOnMissing)
        {
            var prefab = await assetManager.LoadAssetAsync<GameObject>(LoadTarget.Effect, assetName, logOnMissing);
            if (prefab == null) return;
            _prefabs[assetName] = prefab;
        }

        /// <summary>Attack1, Attack2처럼 번호가 붙은 프리팹의 에셋 이름. 번호가 0이면 번호 없는 기본 프리팹</summary>
        private string GetAssetName(EffectType effectType, int variant)
            => variant > 0 ? $"{_objectData.Id}/{effectType}{variant}" : $"{_objectData.Id}/{effectType}";

        /// <summary>
        /// offset은 콜라이더 중점에서 얼마나 띄울지. 어디에 띄울지는 부르는 쪽이 정한다.
        /// direction은 이펙트의 진행 방향. 0이면 캐릭터가 보는 방향을 그대로 쓴다.
        /// 이펙트가 진행 방향의 앞뒤 중 어느 쪽으로 그려지는지는 프리팹이 들고 있으므로
        /// 호출한 쪽에서 방향을 뒤집어 넘기지 않는다.
        /// </summary>
        public void Play(EffectType effectType, int variant, Vector2 offset, Vector2 direction = default, float duration = 0f)
        {
            string assetName = GetAssetName(effectType, variant);
            if (!_prefabs.TryGetValue(assetName, out var prefab))
            {
                //번호가 붙은 이펙트는 테이블이 쓰겠다고 지정한 것이므로 조용히 넘기지 않는다
                if (variant > 0)
                    Debug.LogError($"Effect {AssetPathEffect}{assetName}{AssetExtensionPrefab} not found");
                return;
            }

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = _objectContext.Direction == Direction.Right ? Vector2.right : Vector2.left;
            direction.Normalize();

            //오브젝트 원점(발밑)이 아니라 콜라이더 중점을 기준으로 잡고, 거기서 넘겨받은 만큼 띄운다
            var position = transform.position + (Vector3)(_objectContext.ColliderOffset + offset);

            //재생 내내 뿜는 이펙트는 오브젝트를 따라가야 하므로 자식으로 붙이고,
            //한 번 터지고 마는 이펙트는 시작한 자리에 남아야 하므로 월드에 띄운다
            bool follow = ShouldFollow(effectType);
            var instance = Instantiate(prefab, position, Quaternion.identity, follow ? transform : null);
            var effectInstance = instance.AddComponent<EffectInstance>();
            effectInstance.Play(direction, duration, follow);

            //수명이 끝난 인스턴스는 스스로 사라지므로 빈 자리만 걷어내고 새로 넣는다
            _instances.RemoveAll(played => played == null);
            _instances.Add(effectInstance);
        }

        //재생 내내 뿜는 이펙트만 오브젝트를 따라간다
        private static bool ShouldFollow(EffectType effectType) => effectType switch
        {
            EffectType.Dash => true,
            _ => false
        };

        private void OnDestroy()
        {
            if (_objectContext != null)
                _objectContext.OnEffectPlay -= Play;

            //월드에 띄운 인스턴스는 오브젝트가 사라져도 살아남으므로, 프리팹 핸들을 반납하기 전에 먼저 지운다
            foreach (var instance in _instances)
            {
                if (instance != null)
                    Destroy(instance.gameObject);
            }
            _instances.Clear();

            var assetManager = Global.Instance?.AssetManager;
            if (assetManager != null)
            {
                foreach (var assetName in _prefabs.Keys)
                    assetManager.ReleaseAsset<GameObject>(LoadTarget.Effect, assetName);
            }

            _prefabs.Clear();
        }
    }
}
