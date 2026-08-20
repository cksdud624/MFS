using System.Collections.Generic;
using Common;
using Cysharp.Threading.Tasks;
using InGame.Context;
using UnityEngine;
using static Common.AssetKeys;
using static Common.GameDefine;

namespace InGame.Component
{
    public class EffectPlayer : MonoBehaviour
    {
        private ObjectContext _objectContext;
        private long _characterId;

        private readonly Dictionary<EffectType, GameObject> _effectPrefabs = new();
        private readonly Dictionary<EffectType, List<GameObject>> _effectInstances = new();

        public async UniTask Init(ObjectContext objectContext, long characterId)
        {
            _objectContext = objectContext;
            _characterId = characterId;

            _objectContext.OnEffectRequested += Play;

            var assetManager = Global.Instance.AssetManager;

            foreach (EffectType effectType in System.Enum.GetValues(typeof(EffectType)))
            {
                string assetName = $"{characterId}/{effectType}";

                var prefab = await assetManager.LoadAssetAsync<GameObject>(
                    LoadTarget.Effect,
                    assetName,
                    logOnMissing: false
                );

                if (prefab != null)
                    _effectPrefabs[effectType] = prefab;
            }
        }

        private void Play(EffectType effectType, Vector2 effectDirection)
        {
            if (!_effectPrefabs.TryGetValue(effectType, out var prefab))
                return;

            if (!_effectInstances.TryGetValue(effectType, out var instances))
            {
                instances = new List<GameObject>();
                _effectInstances[effectType] = instances;
            }

            GameObject instance = null;

            foreach (var effectInstance in instances)
            {
                var particleSystems =
                    effectInstance.GetComponentsInChildren<ParticleSystem>(true);

                bool isPlaying = false;

                foreach (var particleSystem in particleSystems)
                {
                    if (particleSystem.IsAlive(true))
                    {
                        isPlaying = true;
                        break;
                    }
                }

                if (!isPlaying)
                {
                    instance = effectInstance;
                    break;
                }
            }

            if (instance == null)
            {
                instance = Instantiate(prefab);
                instances.Add(instance);
            }

            instance.SetActive(false);

            instance.transform.position = transform.position;

            var directionController =
                instance.GetComponent<EffectDirection>();

            if (directionController != null)
                directionController.SetDirection(effectDirection);

            instance.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_objectContext != null)
                _objectContext.OnEffectRequested -= Play;
        }
    }
}