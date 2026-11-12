using System.Collections.Generic;
using Generated.Table;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.World
{
    /// <summary>
    /// 맵을 이루는 Zone의 배치. Zone은 맵의 한 칸이고, 상하좌우로 맞닿아 이어져 있어서 경계를 넘으면 포탈 없이 그대로 옆 Zone으로 걸어 들어간다.
    /// 칸 크기는 맵마다 정하고(Map 표), 한 맵 안에서는 같은 크기의 칸이 격자로 놓인다. Zone 번호로 어느 칸인지 찾는다.
    /// Zone 안의 위치는 그 칸의 왼쪽 아래를 0,0으로 보는 로컬 좌표다. Zone을 다른 칸으로 옮겨도 그 안의 좌표(표에 적은 위치)는 그대로 쓸 수 있다.
    /// Map, Zone 표를 읽어서 만들고, 만들 때 표가 맞는지 검사해서 틀리면 에러를 남긴다. 실행 중에는 바뀌지 않는다
    /// </summary>
    public class ZoneLayout
    {
        #region Types
        /// <summary>격자 한 칸. 월드에서 차지하는 영역(Rect)까지 계산해둔다</summary>
        public class ZoneEntry
        {
            public long Id { get; }
            public long Map { get; }
            public Vector2Int Cell { get; }
            //월드에서 차지하는 영역. position이 왼쪽 아래(로컬 0,0)다
            public Rect Rect { get; }

            public ZoneEntry(long id, long map, Vector2Int cell, Rect rect)
            {
                Id = id;
                Map = map;
                Cell = cell;
                Rect = rect;
            }
        }
        #endregion

        #region Fields
        private readonly Dictionary<long, MapData> _maps = new();
        private readonly Dictionary<long, ZoneEntry> _zones = new();
        //맵의 격자 칸 → Zone. 월드 위치에서 Zone을 찾을 때 쓴다
        private readonly Dictionary<(long map, Vector2Int cell), ZoneEntry> _zonesByCell = new();
        #endregion

        #region Public
        /// <summary>
        /// Map, Zone 표로 배치를 만든다. 표가 틀리면 에러를 남기고 false다.
        /// 틀린 줄은 빼고 만들어서 layout은 늘 돌려준다. 검사 메뉴가 Zone을 가리키는 다른 표까지 이어서 검사할 수 있게 하려는 것이다
        /// </summary>
        public static bool TryBuild(IReadOnlyList<MapData> maps, IReadOnlyList<ZoneData> zones, out ZoneLayout layout)
        {
            layout = new ZoneLayout();
            bool valid = true;

            foreach (var map in maps)
            {
                if (layout._maps.ContainsKey(map.Id))
                    valid = LogError($"Map {map.Id}", "Id is duplicated.");
                else if (map.ZoneWidth <= 0f || map.ZoneHeight <= 0f)
                    valid = LogError($"Map {map.Id}", $"ZoneWidth and ZoneHeight must be greater than 0 : {map.ZoneWidth}, {map.ZoneHeight}");
                else
                    layout._maps[map.Id] = map;
            }

            foreach (var zone in zones)
            {
                string location = $"Zone {zone.Id}";
                if (!layout._maps.TryGetValue(zone.Map, out var map))
                {
                    valid = LogError(location, $"Map {zone.Map} is not defined in Map table.");
                    continue;
                }
                //번호만 보고 어느 맵의 Zone인지 알 수 있게 맵 번호를 앞에 둔다
                if (zone.Id / ZoneMapRange != zone.Map || zone.Id % ZoneMapRange == 0)
                {
                    valid = LogError(location, $"Id must be map × {ZoneMapRange} + order such as {zone.Map * ZoneMapRange + 1}.");
                    continue;
                }
                if (zone.Column < 0 || zone.Row < 0)
                {
                    valid = LogError(location, $"Column and Row must be 0 or more : {zone.Column}, {zone.Row}");
                    continue;
                }
                if (layout._zones.ContainsKey(zone.Id))
                {
                    valid = LogError(location, "Id is duplicated.");
                    continue;
                }

                var cell = new Vector2Int(zone.Column, zone.Row);
                if (layout._zonesByCell.TryGetValue((zone.Map, cell), out var other))
                {
                    valid = LogError(location, $"Cell {cell} of Map {zone.Map} is already used by Zone {other.Id}.");
                    continue;
                }

                var size = new Vector2(map.ZoneWidth, map.ZoneHeight);
                var entry = new ZoneEntry(zone.Id, zone.Map, cell, new Rect(map.Origin + Vector2.Scale(cell, size), size));
                layout._zones[zone.Id] = entry;
                layout._zonesByCell[(zone.Map, cell)] = entry;
            }
            return valid;
        }

        public bool HasZone(long zoneId) => _zones.ContainsKey(zoneId);

        public bool TryGetZone(long zoneId, out ZoneEntry zone) => _zones.TryGetValue(zoneId, out zone);

        /// <summary>Zone 로컬 좌표를 월드 좌표로 바꾼다. 없는 Zone이면 로컬 좌표를 그대로 돌려준다 (검사를 통과했으면 늘 있다)</summary>
        public Vector2 ToWorld(long zoneId, Vector2 local)
            => _zones.TryGetValue(zoneId, out var zone) ? zone.Rect.position + local : local;

        /// <summary>월드 위치가 들어 있는 Zone을 찾는다. 격자 밖이거나 그 칸에 Zone이 없으면 false</summary>
        public bool TryFindZone(long mapId, Vector2 world, out ZoneEntry zone)
        {
            zone = null;
            if (!_maps.TryGetValue(mapId, out var map)) return false;

            var cell = new Vector2Int(Mathf.FloorToInt((world.x - map.Origin.x) / map.ZoneWidth),
                Mathf.FloorToInt((world.y - map.Origin.y) / map.ZoneHeight));
            return _zonesByCell.TryGetValue((mapId, cell), out zone);
        }

        /// <summary>
        /// 이어서 갈 수 없는 끝. 그 Zone의 왼쪽, 오른쪽, 아래, 위에 Zone이 없으면 그쪽은 Zone 끝이고,
        /// 있으면 이어서 걸어갈 수 있으므로 막지 않는다(무한대). 카메라는 화면이 이 끝을 넘지 않게, 스트리머는 이 끝에 벽을 세운다. 대각선 칸은 보지 않는다
        /// </summary>
        public (Vector2 min, Vector2 max) GetBoundary(ZoneEntry zone)
        {
            var rect = zone.Rect;
            float left = HasZone(zone, Vector2Int.left) ? float.NegativeInfinity : rect.xMin;
            float right = HasZone(zone, Vector2Int.right) ? float.PositiveInfinity : rect.xMax;
            float bottom = HasZone(zone, Vector2Int.down) ? float.NegativeInfinity : rect.yMin;
            float top = HasZone(zone, Vector2Int.up) ? float.PositiveInfinity : rect.yMax;
            return (new Vector2(left, bottom), new Vector2(right, top));
        }

        /// <summary>
        /// 그 Zone에서 상하좌우와 대각선으로 radius 칸 안의 Zone을 모은다 (자기 자신 포함). 빈 칸은 건너뛴다.
        /// 대각선도 한 칸으로 센다. radius가 1이면 자기 Zone과 둘레 8칸(3×3)이다
        /// </summary>
        public void GetNeighbors(ZoneEntry center, int radius, List<ZoneEntry> result)
        {
            result.Clear();
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    var cell = center.Cell + new Vector2Int(x, y);
                    if (_zonesByCell.TryGetValue((center.Map, cell), out var zone))
                        result.Add(zone);
                }
            }
        }
        #endregion

        #region Private
        //그 Zone에서 offset만큼 떨어진 칸에 같은 맵의 Zone이 있는지
        private bool HasZone(ZoneEntry zone, Vector2Int offset) => _zonesByCell.ContainsKey((zone.Map, zone.Cell + offset));

        //에러를 남기고 false를 돌려준다. 실패 지점에서 바로 대입할 수 있게 bool을 돌려준다
        private static bool LogError(string location, string message)
        {
            Debug.LogError($"{location} : {message}");
            return false;
        }
        #endregion
    }
}
