using System.Collections.Generic;
using System.Threading;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Common.AssetKeys;

namespace InGame.World
{
    /// <summary>
    /// Zone 프리팹(Zone/(Zone 번호).prefab)을 Addressables로 불러와 Zone 영역의 왼쪽 아래에 놓는다.
    /// 프리팹은 루트를 Zone의 왼쪽 아래(로컬 0,0)로 두고 만든다. 그래야 프리팹 편집 화면의 좌표가 표에 적는 로컬 좌표와 같다.
    /// 생성은 여러 프레임에 나눠서 한다. Zone 하나의 오브젝트가 많으면 한 프레임에 몰아서 만들 때 끊김이 생긴다
    /// </summary>
    public class PrefabZoneLoader : IZoneLoader
    {
        #region Fields
        private readonly AssetManager _assetManager;
        //불러온 Zone을 모아두는 부모. Hierarchy에서 Zone을 한곳에서 찾게 한다
        private readonly Transform _root;
        //Zone 번호 → 생성한 내용물
        private readonly Dictionary<long, GameObject> _instances = new();
        #endregion

        #region Init
        public PrefabZoneLoader(AssetManager assetManager, Transform root)
        {
            _assetManager = assetManager;
            _root = root;
        }
        #endregion

        #region Public
        public async UniTask<bool> Load(ZoneLayout.ZoneEntry zone, CancellationToken cancellationToken)
        {
            if (_instances.ContainsKey(zone.Id)) return true;

            string assetName = zone.Id.ToString();
            //프리팹이 없는 Zone은 스트리머가 한 번만 알린다. 여기서도 남기면 같은 에러가 Zone을 오갈 때마다 쌓인다
            var prefab = await _assetManager.LoadAssetAsync<GameObject>(LoadTarget.Zone, assetName, false);
            if (prefab == null) return false;

            //InGame.Object 네임스페이스가 같은 이름을 가리므로 UnityEngine을 붙여 쓴다
            var operation = UnityEngine.Object.InstantiateAsync(prefab, _root, (Vector3)zone.Rect.position, Quaternion.identity);
            await operation.ToUniTask(cancellationToken: cancellationToken);
            var instance = operation.Result[0];
            instance.name = $"Zone {zone.Id}";
            _instances[zone.Id] = instance;
            return true;
        }

        public void Unload(ZoneLayout.ZoneEntry zone)
        {
            if (!_instances.Remove(zone.Id, out var instance)) return;

            //씬이 내려가며 이미 파괴되었을 수 있다
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
            _assetManager.ReleaseAsset<GameObject>(LoadTarget.Zone, zone.Id.ToString());
        }
        #endregion
    }
}
