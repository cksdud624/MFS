using System;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Common.AssetKeys;
using static Common.GameDefine;
using Object = UnityEngine.Object;

namespace Common
{
    public class AssetManager : MonoBehaviour
    {
        private readonly Dictionary<string, List<AsyncOperationHandle>> _addressableCache = new ();
        
        public async UniTask<T> LoadAssetAsync<T>(LoadTarget target, string assetName, bool logOnMissing = true) where T : Object
        {
            string key = GetAddressableKey(target, assetName);
            var locationHandle = Addressables.LoadResourceLocationsAsync(key, typeof(T));
            var locations = await locationHandle.ToUniTask();
            Addressables.Release(locationHandle);
            if (locations == null || locations.Count == 0)
            {
                if (logOnMissing) Debug.LogError($"{key} not found");
                return null;
            }

            var handle = Addressables.LoadAssetAsync<T>(key);
            T asset = await handle.ToUniTask();
            if (!_addressableCache.ContainsKey(key))
                _addressableCache[key] = new List<AsyncOperationHandle>();
            _addressableCache[key].Add(handle);
            return asset;
        }

        public void ReleaseAsset<T>(LoadTarget target, string assetName) where T : Object
        {
            string key = GetAddressableKey(target, assetName);
            if (_addressableCache.TryGetValue(key, out var list) && list.Count > 0)
            {
                var handle = list[^1];
                Addressables.Release(handle);
                list.RemoveAt(list.Count - 1);
                if(list.Count == 0)
                    _addressableCache.Remove(key);
            }
            else
                Debug.LogError($"{key} not found to release");
        }
    }

    public static class AssetKeys
    {
        public enum LoadTarget
        {
            Effect,
            Animation,
        }

        public static string GetAddressableKey(LoadTarget target, string assetName)
        {
            string key;
            switch (target)
            {
                case LoadTarget.Effect:
                    key = AssetPathEffect + assetName + AssetExtensionPrefab;
                    break;
                case LoadTarget.Animation:
                    key = AssetPathAnimation + assetName + AssetExtensionAnimation;
                    break;
                default:
                    key = string.Empty;
                    break;
            }

            return key;
        }
    }
}