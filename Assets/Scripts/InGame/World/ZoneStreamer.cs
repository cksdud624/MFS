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
    /// 캐릭터를 없애지 않는 이유는 이야기가 대상을 계속 추적하기 때문이다 (뒤의 퀘스트가 같은 번호로 다시 쓴다).
    /// 시나리오가 무대로 쓰는 Zone은 고정(Pin)해서 컬링 거리 밖이어도 올려둔다. 연출에 나오는 대상이 꺼지지 않게 하려는 것이다.
    /// 보스전이 벌어지는 Zone은 잠가서(Lock) 옆에 Zone이 있어도 사방을 벽으로 막는다. 전투가 끝날 때까지 다른 Zone으로 나가지 못한다
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
        //고정한 Zone (시나리오의 무대). 컬링 거리 밖이어도 올려둔다
        private readonly HashSet<long> _pinned = new();
        //잠근 Zone (보스전이 벌어지는 Zone). 옆에 Zone이 있어도 사방을 막는다. 잠근 Zone이 없으면 null이다.
        //카메라도 이 Zone 밖을 보여주지 않는다
        public ZoneLayout.ZoneEntry LockedZone { get; private set; }
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

        /// <summary>
        /// Zone을 고정한다. 고정한 Zone은 컬링 거리 밖이어도 올려두고, 다 올라올 때까지 기다린다. 이미 올라와 있으면 바로 끝난다.
        /// 고정은 한 묶음만 둔다 (시나리오는 겹쳐서 재생되지 않는다). 다시 부르면 앞의 고정을 새 묶음으로 바꾼다
        /// </summary>
        public async UniTask Pin(IReadOnlyCollection<long> zoneIds, CancellationToken cancellationToken)
        {
            _pinned.Clear();
            foreach (long zoneId in zoneIds)
                _pinned.Add(zoneId);

            UpdateCulling(null);
            if (!IsPinnedLoaded())
            {
                Debug.Log($"[Zone] 무대 Zone {string.Join(", ", _pinned)} 올라오기를 기다림");
                await UniTask.WaitUntil(IsPinnedLoaded, cancellationToken: cancellationToken);
            }
            //방금 올라온 Zone에 꺼져 있던 캐릭터를 다음 프레임까지 기다리지 않고 켠다. 바로 이어지는 연출의 첫 줄이 그 캐릭터를 쓸 수 있다
            UpdateTrackedActive();
        }

        /// <summary>고정을 푼다. 컬링 거리 밖에 있던 Zone은 내리고, 그 안의 캐릭터는 꺼둔다</summary>
        public void Unpin()
        {
            if (_pinned.Count == 0) return;

            _pinned.Clear();
            UpdateCulling(null);
            UpdateTrackedActive();
        }

        /// <summary>
        /// Zone을 잠근다. 옆에 Zone이 있는 쪽까지 사방에 벽을 세워서, 풀 때까지 그 Zone에서 나가지도 들어오지도 못한다.
        /// 잠그는 Zone은 플레이어가 들어와 있는 Zone이라 이미 올라와 있다. 잠근 Zone은 하나만 둔다
        /// </summary>
        public void Lock(long zoneId)
        {
            if (!_layout.TryGetZone(zoneId, out var zone))
            {
                Debug.LogError($"[Zone] Zone {zoneId} is not defined. It cannot be locked.");
                return;
            }

            var unlocked = LockedZone;
            LockedZone = zone;
            Debug.Log($"[Zone] Zone {zoneId} 잠금");
            if (unlocked != null && unlocked != zone)
                RebuildWalls(unlocked);
            RebuildWalls(zone);
        }

        /// <summary>잠근 Zone을 푼다. 옆에 Zone이 있는 쪽의 벽을 걷어서 다시 이어서 갈 수 있게 한다</summary>
        public void Unlock()
        {
            if (LockedZone == null) return;

            var zone = LockedZone;
            LockedZone = null;
            Debug.Log($"[Zone] Zone {zone.Id} 잠금 해제");
            RebuildWalls(zone);
        }

        /// <summary>
        /// 그 Zone에서 더 갈 수 없는 끝. 잠근 Zone은 사방이 끝이고, 아니면 옆에 Zone이 없는 쪽만 끝이다 (ZoneLayout.GetBoundary).
        /// 벽을 세우는 곳과 카메라가 같은 끝을 쓴다
        /// </summary>
        public (Vector2 min, Vector2 max) GetBoundary(ZoneLayout.ZoneEntry zone)
            => zone == LockedZone ? (zone.Rect.min, zone.Rect.max) : _layout.GetBoundary(zone);

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
        /// 기준 Zone에서 컬링 거리 안의 Zone과 고정한 Zone을 불러오고, 나머지를 내린다.
        /// loads를 주면 새로 불러오는 작업을 담아서 돌려주고(처음에 기다릴 때), 없으면 기다리지 않고 진행한다
        /// </summary>
        private void UpdateCulling(List<UniTask> loads)
        {
            _wanted.Clear();
            //기준 Zone이 아직 없어도(플레이어도 카메라도 Zone 밖) 고정한 Zone은 올린다
            if (_center != null)
            {
                _layout.GetNeighbors(_center, ZoneCullDistance - 1, _neighbors);
                foreach (var zone in _neighbors)
                    Want(zone, loads);
            }
            foreach (long zoneId in _pinned)
            {
                if (_layout.TryGetZone(zoneId, out var zone))
                    Want(zone, loads);
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

        //올려둘 Zone으로 넣고, 아직 불러오지 않았으면 불러온다
        private void Want(ZoneLayout.ZoneEntry zone, List<UniTask> loads)
        {
            //고정한 Zone이 컬링 거리 안에도 있으면 두 번 들어온다
            if (!_wanted.Add(zone.Id) || _states.ContainsKey(zone.Id)) return;

            var load = LoadZone(zone);
            if (loads != null)
                loads.Add(load);
            else
                load.Forget();
        }

        //고정한 Zone이 다 올라왔는지. 배치에 없는 번호는 불러올 수 없으므로 기다리지 않는다 (검사를 통과했으면 늘 있다)
        private bool IsPinnedLoaded()
        {
            foreach (long zoneId in _pinned)
            {
                if (_layout.HasZone(zoneId) && !IsLoaded(zoneId))
                    return false;
            }
            return true;
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
        /// Zone의 상하좌우 중 더 갈 수 없는 쪽에 벽을 세운다. 이웃 Zone이 없는 쪽이고, 잠근 Zone이면 사방이다.
        /// 벽은 Zone 바깥에 붙여서 Zone 안의 공간은 줄이지 않는다. 모서리가 비지 않게 위아래 벽은 옆벽 두께만큼 양쪽으로 더 길게 둔다.
        /// 태그를 Map으로 둬서 AI의 벽 판정에도 걸린다. 지면 판정은 위를 향한 접촉만 보므로 옆벽이 바닥으로 잡히지 않는다
        /// </summary>
        private void CreateWalls(ZoneLayout.ZoneEntry zone)
        {
            var (min, max) = GetBoundary(zone);
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

        //잠그거나 풀어서 막을 쪽이 달라졌으면 벽을 다시 세운다. 아직 올라오지 않은 Zone은 올라올 때 지금 상태대로 세워진다
        private void RebuildWalls(ZoneLayout.ZoneEntry zone)
        {
            if (!IsLoaded(zone.Id)) return;

            //씬이 내려가며 이미 파괴되었을 수 있다
            if (_walls.Remove(zone.Id, out var walls) && walls != null)
                UnityEngine.Object.Destroy(walls);
            CreateWalls(zone);
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
