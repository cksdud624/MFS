using System;
using System.Collections.Generic;
using System.Threading;
using Common;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using InGame.Context;
using InGame.Object;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.World
{
    /// <summary>
    /// Zone 컬링. 플레이어가 있는 Zone을 기준으로 상하좌우와 대각선으로 ZoneCullDistance(2) 칸 이상 떨어진 Zone은 내리고,
    /// 그 안쪽(자기 Zone과 둘레 8칸)은 불러둔다. 플레이어가 경계를 넘어 옆 Zone으로 들어가면 기준이 바뀌어서 다시 정한다.
    /// 넘어간 쪽의 이웃은 이미 올라와 있으므로 포탈이나 로딩 없이 맵을 쭉 걸어갈 수 있다.
    /// 플레이어가 아직 없으면(도입 연출 전) 카메라가 보는 곳을 기준으로 한다.
    /// 불러온 Zone의 상하좌우 중 이웃 Zone이 없는 쪽에는 보이지 않는 벽을 세워서, 이어서 갈 곳이 없는 맵 끝을 넘어가지 못하게 한다.
    /// 내려간 Zone에 있는 캐릭터는 꺼두고, 그 Zone이 다시 올라오면 켠다. 바닥이 사라진 자리에서 떨어지지 않게 하려는 것이다.
    /// 캐릭터를 없애지 않는 이유는 이야기가 대상을 계속 추적하기 때문이다 (뒤의 퀘스트가 같은 번호로 다시 쓴다)
    /// </summary>
    public class ZoneStreamer : IUpdateable, IDisposable
    {
        #region Types
        private enum ZoneState
        {
            Loading,
            Loaded,
        }
        #endregion

        #region Fields
        private readonly ZoneLayout _layout;
        private readonly IZoneLoader _loader;
        private readonly InGameContext _inGameContext;
        //플레이어가 없을 때 기준으로 삼는 카메라. 없으면 플레이어가 생길 때까지 아무것도 불러오지 않는다
        private readonly Camera _camera;
        private readonly CancellationToken _cancellationToken;

        //지금 맵. Zone 번호가 맵을 담고 있어서 기준 Zone이 정해지면 따라 바뀐다. 카메라가 Zone 끝을 찾을 때도 쓴다
        public long MapId { get; private set; } = StartMapId;
        //기준 Zone. 플레이어가 이 Zone을 벗어나면 컬링을 다시 정한다
        private ZoneLayout.ZoneEntry _center;
        //불러와 있거나 불러오는 중인 Zone
        private readonly Dictionary<long, ZoneState> _states = new();
        //지금 올라와 있어야 하는 Zone. 불러오는 사이에 컬링 거리 밖으로 나간 Zone은 다 불러온 뒤 바로 내린다
        private readonly HashSet<long> _wanted = new();
        //프리팹이 없다고 이미 알린 Zone. 같은 경고가 Zone을 오갈 때마다 쌓이지 않게 한다
        private readonly HashSet<long> _reportedMissing = new();
        //Zone이 화면보다 작다고 이미 알린 맵
        private readonly HashSet<long> _reportedSmallMaps = new();
        //Zone에 따라 끄고 켜는 캐릭터 (월드에 세운 대상. 플레이어는 기준이라 넣지 않는다)
        private readonly List<ObjectBase> _tracked = new();
        //맵 끝을 막는 벽을 모아두는 부모. Hierarchy에서 한곳에서 찾게 한다
        private readonly Transform _wallRoot;
        //Zone 번호 → 그 Zone의 벽. Zone 내용물(프리팹)과 따로 두어서 로더를 바꿔도 벽은 그대로 세운다
        private readonly Dictionary<long, GameObject> _walls = new();

        //UpdateCulling이 쓰는 임시 목록. 매번 할당하지 않게 재사용한다
        private readonly List<ZoneLayout.ZoneEntry> _neighbors = new();
        private readonly List<long> _toUnload = new();
        #endregion

        #region Init
        public ZoneStreamer(ZoneLayout layout, IZoneLoader loader, InGameContext inGameContext, Camera camera, CancellationToken cancellationToken)
        {
            _layout = layout;
            _loader = loader;
            _inGameContext = inGameContext;
            _camera = camera;
            _cancellationToken = cancellationToken;
            _wallRoot = new GameObject("Zone Walls").transform;

            Global.Instance.BindUpdate(this);
        }
        #endregion

        #region LifeCycle
        public void OnUpdate()
        {
            if (TryFindCenter(out var center) && center != _center)
            {
                SetCenter(center);
                UpdateCulling(null);
            }
            UpdateTrackedActive();
        }
        #endregion

        #region Public
        /// <summary>
        /// 처음 올라와 있어야 할 Zone(기준 Zone과 둘레)을 다 불러올 때까지 기다린다. 이야기를 시작하기 전에 부른다.
        /// 기다리지 않으면 처음부터 서 있는 캐릭터(Resident)가 바닥이 생기기 전에 놓여서 떨어진다
        /// </summary>
        public async UniTask LoadInitial()
        {
            if (!TryFindCenter(out var center))
            {
                Debug.LogWarning($"[Zone] No zone at the start position in Map {MapId}. Zones load when the player enters one.");
                return;
            }

            SetCenter(center);
            var loads = new List<UniTask>();
            UpdateCulling(loads);
            await UniTask.WhenAll(loads);
        }

        /// <summary>Zone에 따라 끄고 켤 캐릭터로 등록한다. 파괴되면 저절로 빠진다</summary>
        public void Track(ObjectBase target)
        {
            if (target == null || target.IsPlayer) return;
            _tracked.Add(target);
            SetActiveByZone(target);
        }

        public bool IsLoaded(long zoneId) => _states.TryGetValue(zoneId, out var state) && state == ZoneState.Loaded;

        public void Dispose() => Global.Instance?.UnBindUpdate(this);
        #endregion

        #region Private - Culling
        //플레이어가 있으면 플레이어, 없으면 카메라가 보는 곳이 들어 있는 Zone
        private bool TryFindCenter(out ZoneLayout.ZoneEntry center)
        {
            center = null;
            var player = _inGameContext.Player;
            if (player != null)
                return _layout.TryFindZone(MapId, player.transform.position, out center);
            return _camera != null && _layout.TryFindZone(MapId, _camera.transform.position, out center);
        }

        private void SetCenter(ZoneLayout.ZoneEntry center)
        {
            Debug.Log($"[Zone] 기준 Zone {center.Id} {center.Cell}");
            _center = center;
            MapId = center.Map;
            WarnIfZoneSmallerThanView(center);
        }

        /// <summary>
        /// 기준 Zone에서 컬링 거리 안의 Zone을 불러오고, 밖으로 나간 Zone을 내린다.
        /// loads를 주면 새로 불러오는 작업을 담아서 돌려주고(처음에 기다릴 때), 없으면 기다리지 않고 진행한다
        /// </summary>
        private void UpdateCulling(List<UniTask> loads)
        {
            _layout.GetNeighbors(_center, ZoneCullDistance - 1, _neighbors);
            _wanted.Clear();
            foreach (var zone in _neighbors)
            {
                _wanted.Add(zone.Id);
                if (_states.ContainsKey(zone.Id)) continue;

                var load = LoadZone(zone);
                if (loads != null)
                    loads.Add(load);
                else
                    load.Forget();
            }

            //불러오는 중인 Zone은 다 불러온 뒤 LoadZone이 내린다. 지금 내리면 불러오기가 끝나고 내용물만 남는다
            _toUnload.Clear();
            foreach (var pair in _states)
            {
                if (pair.Value == ZoneState.Loaded && !_wanted.Contains(pair.Key))
                    _toUnload.Add(pair.Key);
            }
            foreach (long zoneId in _toUnload)
                UnloadZone(zoneId);
        }

        private async UniTask LoadZone(ZoneLayout.ZoneEntry zone)
        {
            _states[zone.Id] = ZoneState.Loading;
            bool hasContent = await _loader.Load(zone, _cancellationToken);
            //프리팹이 없는 Zone은 빈 Zone으로 보고 불러온 것으로 친다. 지형을 아직 씬에 직접 두고 있어도 캐릭터가 꺼지지 않게 하려는 것이다
            if (!hasContent && _reportedMissing.Add(zone.Id))
                Debug.LogWarning($"[Zone] Zone {zone.Id} has no prefab ({AssetPathZone}{zone.Id}{AssetExtensionPrefab}). It is treated as an empty zone.");

            if (!_wanted.Contains(zone.Id))
            {
                UnloadZone(zone.Id);
                return;
            }
            _states[zone.Id] = ZoneState.Loaded;
            CreateWalls(zone);
            Debug.Log($"[Zone] Zone {zone.Id} 불러옴");
        }

        private void UnloadZone(long zoneId)
        {
            Debug.Log($"[Zone] Zone {zoneId} 컬링");
            _states.Remove(zoneId);
            //씬이 내려가며 이미 파괴되었을 수 있다
            if (_walls.Remove(zoneId, out var walls) && walls != null)
                UnityEngine.Object.Destroy(walls);
            if (_layout.TryGetZone(zoneId, out var zone))
                _loader.Unload(zone);
        }

        /// <summary>
        /// 컬링 거리는 칸 수로 고정이라, Zone이 카메라 화면보다 작은 맵에서는 컬링된 Zone이 화면 가장자리에 보일 수 있다.
        /// 플레이어가 기준 Zone의 끝에 서면 화면 절반이 바로 옆 Zone을 지나 그 다음 칸(컬링된 칸)까지 닿기 때문이다. 그런 맵이면 한 번 알린다
        /// </summary>
        private void WarnIfZoneSmallerThanView(ZoneLayout.ZoneEntry center)
        {
            if (_camera == null || _reportedSmallMaps.Contains(center.Map)) return;

            float halfHeight = _camera.orthographic
                ? _camera.orthographicSize
                : Mathf.Abs(_camera.transform.position.z) * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var halfView = new Vector2(halfHeight * _camera.aspect, halfHeight);
            var size = center.Rect.size;
            if (halfView.x <= size.x && halfView.y <= size.y) return;

            _reportedSmallMaps.Add(center.Map);
            Debug.LogWarning($"[Zone] Zones of Map {center.Map} ({size.x} x {size.y}) are smaller than half the camera view ({halfView.x:0.#} x {halfView.y:0.#}). Culled zones may be visible at the screen edge.");
        }
        #endregion

        #region Private - Wall
        /// <summary>
        /// Zone의 상하좌우 중 이웃 Zone이 없는 쪽에 벽을 세운다. 벽은 Zone 바깥에 붙여서 Zone 안의 공간은 줄이지 않는다.
        /// 모서리가 비지 않게 위아래 벽은 옆벽 두께만큼 양쪽으로 더 길게 둔다.
        /// 태그를 Map으로 둬서 AI의 벽 판정에도 걸린다. 지면 판정은 위를 향한 접촉만 보므로 옆벽이 바닥으로 잡히지 않는다
        /// </summary>
        private void CreateWalls(ZoneLayout.ZoneEntry zone)
        {
            var (min, max) = _layout.GetBoundary(zone);
            var rect = zone.Rect;
            float thickness = ZoneWallThickness;
            var root = new GameObject($"Zone {zone.Id} Walls");
            root.transform.SetParent(_wallRoot, false);

            if (!float.IsInfinity(min.x))
                AddWall(root.transform, "Left", new Vector2(rect.xMin - thickness * 0.5f, rect.center.y), new Vector2(thickness, rect.height));
            if (!float.IsInfinity(max.x))
                AddWall(root.transform, "Right", new Vector2(rect.xMax + thickness * 0.5f, rect.center.y), new Vector2(thickness, rect.height));
            if (!float.IsInfinity(min.y))
                AddWall(root.transform, "Bottom", new Vector2(rect.center.x, rect.yMin - thickness * 0.5f), new Vector2(rect.width + thickness * 2f, thickness));
            if (!float.IsInfinity(max.y))
                AddWall(root.transform, "Top", new Vector2(rect.center.x, rect.yMax + thickness * 0.5f), new Vector2(rect.width + thickness * 2f, thickness));

            _walls[zone.Id] = root;
        }

        //Rigidbody 없는 콜라이더는 움직이지 않는 벽으로 다뤄져서 따로 고정할 것이 없다
        private static void AddWall(Transform parent, string side, Vector2 center, Vector2 size)
        {
            var wall = new GameObject($"{side} Wall");
            wall.tag = TagMap;
            wall.transform.SetParent(parent, false);
            wall.transform.position = center;
            wall.AddComponent<BoxCollider2D>().size = size;
        }
        #endregion

        #region Private - Actor
        //캐릭터가 움직여서 내려간 Zone으로 들어갈 수도 있으므로 매 프레임 본다. 대상은 이야기가 세운 캐릭터뿐이라 적다
        private void UpdateTrackedActive()
        {
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                var target = _tracked[i];
                if (target == null)
                {
                    _tracked.RemoveAt(i);
                    continue;
                }
                SetActiveByZone(target);
            }
        }

        //격자 밖에 있으면 끄지 않는다. Zone이 없는 곳은 내릴 바닥도 없어서 끌 이유가 없다
        private void SetActiveByZone(ObjectBase target)
        {
            bool active = !_layout.TryFindZone(MapId, target.transform.position, out var zone) || IsLoaded(zone.Id);
            if (target.gameObject.activeSelf != active)
                target.gameObject.SetActive(active);
        }
        #endregion
    }
}
