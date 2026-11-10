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
    /// 플레이어가 있는 Zone과 카메라 화면을 덮는 이웃만 불러두고, 멀어진 Zone은 내린다. 포탈 없이 맵을 쭉 걸어가는 동안 계속 바꿔 단다.
    /// 몇 칸까지 불러둘지는 화면 크기와 그 맵의 Zone 크기로 계산한다. Zone 크기가 맵마다 달라서 칸 수를 고정할 수 없다.
    /// 플레이어가 아직 없으면(도입 연출 전) 카메라가 보는 곳을 기준으로 한다.
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

        //지금 맵. Zone 번호가 맵을 담고 있어서 기준 Zone이 정해지면 따라 바뀐다
        private long _mapId = StartMapId;
        //기준 Zone. 플레이어가 이 Zone을 벗어나면 불러둘 범위를 다시 정한다
        private ZoneLayout.ZoneEntry _center;
        //기준 Zone에서 가로/세로로 몇 칸까지 불러둘지. 카메라 배율이 바뀌면 화면 크기가 달라져서 다시 계산한다
        private Vector2Int _radius;
        //불러와 있거나 불러오는 중인 Zone
        private readonly Dictionary<long, ZoneState> _states = new();
        //지금 불러둬야 하는 Zone. 불러오는 사이에 범위에서 벗어난 Zone은 다 불러온 뒤 바로 내린다
        private readonly HashSet<long> _wanted = new();
        //프리팹이 없다고 이미 알린 Zone. 같은 경고가 Zone을 오갈 때마다 쌓이지 않게 한다
        private readonly HashSet<long> _reportedMissing = new();
        //Zone에 따라 끄고 켜는 캐릭터 (월드에 세운 대상. 플레이어는 기준이라 넣지 않는다)
        private readonly List<ObjectBase> _tracked = new();

        //Refresh가 쓰는 임시 목록. 매번 할당하지 않게 재사용한다
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

            Global.Instance.BindUpdate(this);
        }
        #endregion

        #region LifeCycle
        public void OnUpdate()
        {
            if (TryFindCenter(out var center))
            {
                var radius = GetLoadRadius(center);
                if (center != _center || radius != _radius)
                {
                    _center = center;
                    _radius = radius;
                    _mapId = center.Map;
                    Refresh(null);
                }
            }
            UpdateTrackedActive();
        }
        #endregion

        #region Public
        /// <summary>
        /// 처음 불러올 Zone(기준 Zone과 이웃)을 다 불러올 때까지 기다린다. 이야기를 시작하기 전에 부른다.
        /// 기다리지 않으면 처음부터 서 있는 캐릭터(Resident)가 바닥이 생기기 전에 놓여서 떨어진다
        /// </summary>
        public async UniTask LoadInitial()
        {
            if (!TryFindCenter(out var center))
            {
                Debug.LogWarning($"[Zone] No zone at the start position in Map {_mapId}. Zones load when the player enters one.");
                return;
            }

            _center = center;
            _radius = GetLoadRadius(center);
            _mapId = center.Map;
            var loads = new List<UniTask>();
            Refresh(loads);
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

        #region Private
        //플레이어가 있으면 플레이어, 없으면 카메라가 보는 곳이 들어 있는 Zone
        private bool TryFindCenter(out ZoneLayout.ZoneEntry center)
        {
            center = null;
            var player = _inGameContext.Player;
            if (player != null)
                return _layout.TryFindZone(_mapId, player.transform.position, out center);
            return _camera != null && _layout.TryFindZone(_mapId, _camera.transform.position, out center);
        }

        /// <summary>
        /// 기준 Zone에서 몇 칸까지 불러둘지 정한다. 플레이어가 기준 Zone의 어느 끝에 서 있어도 화면 절반이 덮이는 칸 수에 ZoneLoadMargin을 더한다.
        /// 예: 화면 가로 절반이 25이고 Zone 너비가 48이면 1칸, 너비가 10이면 3칸이다
        /// </summary>
        private Vector2Int GetLoadRadius(ZoneLayout.ZoneEntry center)
        {
            var halfView = GetHalfView();
            var size = center.Rect.size;
            return new Vector2Int(Mathf.CeilToInt(halfView.x / size.x), Mathf.CeilToInt(halfView.y / size.y))
                   + Vector2Int.one * ZoneLoadMargin;
        }

        //카메라가 게임 평면(z = 0)에서 보는 영역의 절반 크기. 카메라가 없으면 0이라 기준 Zone만 불러온다
        private Vector2 GetHalfView()
        {
            if (_camera == null) return Vector2.zero;

            float halfHeight = _camera.orthographic
                ? _camera.orthographicSize
                : Mathf.Abs(_camera.transform.position.z) * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2(halfHeight * _camera.aspect, halfHeight);
        }

        /// <summary>
        /// 기준 Zone의 이웃을 불러오고, 범위에서 벗어난 Zone을 내린다.
        /// loads를 주면 새로 불러오는 작업을 담아서 돌려주고(처음에 기다릴 때), 없으면 기다리지 않고 진행한다
        /// </summary>
        private void Refresh(List<UniTask> loads)
        {
            _layout.GetNeighbors(_center, _radius, _neighbors);
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
        }

        private void UnloadZone(long zoneId)
        {
            _states.Remove(zoneId);
            if (_layout.TryGetZone(zoneId, out var zone))
                _loader.Unload(zone);
        }

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
            bool active = !_layout.TryFindZone(_mapId, target.transform.position, out var zone) || IsLoaded(zone.Id);
            if (target.gameObject.activeSelf != active)
                target.gameObject.SetActive(active);
        }
        #endregion
    }
}
