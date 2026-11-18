using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Generated.Table;
using InGame.Story;
using InGame.World;
using UnityEngine;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 시나리오 툴이 열어 둔 이야기 표. 원본은 tsv이고, 여기서 읽어 고친 뒤 저장하면 tsv에 다시 쓴다.
    /// 이야기 표(Actor, Scenario, Quest, QuestInfo, Dialogue, Talk)는 tsv에서 읽고,
    /// 이야기 밖의 참조(Object, Map, Zone)는 게임이 읽는 .bytes에서 읽는다.
    /// ScriptableObject로 둬서 끌어 놓은 것을 Undo로 되돌릴 수 있고, 저장하지 않은 내용이 도메인 리로드를 넘어서 남는다
    /// </summary>
    public class StoryToolDocument : ScriptableObject
    {
        #region Types
        /// <summary>대사 한 줄 (Dialogue 표의 한 줄). Speaker는 표에 적힌 그대로다 (Player 또는 Actor 번호)</summary>
        public readonly struct DialogueLine
        {
            //대사의 Id와 그 대사가 속한 대화 번호
            public string Id { get; }
            public string DialogueId { get; }
            public string Speaker { get; }
            public string Text { get; }
            //문서의 표 목록에서 몇 번째 표의 몇 번째 줄인지. 대사를 고칠 때 이 자리를 고친다
            public int TableIndex { get; }
            public int Row { get; }

            public DialogueLine(TsvTable table, int tableIndex, int row)
            {
                Id = table.Get(row, ColumnId);
                DialogueId = table.Get(row, ColumnDialogueId);
                Speaker = table.Get(row, ColumnSpeaker);
                Text = table.Get(row, ColumnText);
                TableIndex = tableIndex;
                Row = row;
            }
        }
        #endregion

        #region Fields
        public const string ActorTable = "Actor";
        public const string ScenarioTable = "Scenario";
        public const string QuestTable = "Quest";
        public const string QuestInfoTable = "QuestInfo";
        public const string DialogueTable = "Dialogue";
        public const string TalkTable = "Talk";
        private const string ObjectTable = "Object";
        private const string MapTable = "Map";
        private const string ZoneTable = "Zone";
        //툴이 tsv에서 읽는 표. 나머지 표는 건드리지 않는다
        private static readonly string[] StoryTableNames = { ActorTable, ScenarioTable, QuestTable, QuestInfoTable, DialogueTable, TalkTable };

        public const string ColumnId = "Id";
        public const string ColumnCommand = "Command";
        public const string ColumnActor = "Actor";
        public const string ColumnZone = "Zone";
        public const string ColumnParam1 = "Param1";
        public const string ColumnParam2 = "Param2";
        public const string ColumnText = "Text";
        public const string ColumnNote = "Note";
        public const string ColumnObjectId = "ObjectId";
        public const string ColumnType = "Type";
        public const string ColumnName = "Name";
        public const string ColumnResident = "Resident";
        public const string ColumnPosition = "Position";
        public const string ColumnDialogueId = "DialogueId";
        public const string ColumnSpeaker = "Speaker";
        public const string ColumnTitle = "Title";
        //Talk 표의 칸. 어느 퀘스트가 시작된 뒤부터 하는 대화인지와 그 대화의 번호
        public const string ColumnQuest = "Quest";
        public const string ColumnDialogue = "Dialogue";

        //위치는 소수 둘째 자리까지 적는다. 끌어 놓은 값을 그대로 적으면 57.60000228처럼 읽기 어려운 값이 표에 남는다
        private const float PositionStep = 0.01f;
        private const string PositionFormat = "0.##";
        private static readonly char[] VectorSeparators = { ',', ';' };
        //대상을 적어야 하는 줄인데 아직 고르지 않았을 때 대본에 띄우는 글
        private const string EmptyActorName = "(대상 없음)";

        [SerializeField] private List<TsvTable> _tables = new();

        //.bytes에서 읽은 참조. 직렬화하지 않고 필요할 때 다시 읽는다 (도메인 리로드 뒤에는 비어 있다)
        private Dictionary<long, ObjectData> _objects;
        private ZoneLayout _zoneLayout;
        private readonly List<ZoneLayout.ZoneEntry> _zones = new();
        private GameCameraInfo _gameCamera;
        //도메인 리로드는 private 필드도 넘겨준다. 읽어 둔 카메라는 넘어가지 못하는데 이 값만 넘어가면 읽었다고 잘못 알게 된다
        [NonSerialized] private bool _isGameCameraLoaded;

        public IReadOnlyList<TsvTable> Tables => _tables;
        //칸을 고칠 때마다 올라간다. 화면이 계산해 둔 것을 다시 계산할지 이 값으로 안다
        public int Version { get; private set; }

        public bool IsDirty
        {
            get
            {
                foreach (var table in _tables)
                {
                    if (table.IsDirty) return true;
                }
                return false;
            }
        }

        public ZoneLayout ZoneLayout
        {
            get
            {
                EnsureReferences();
                return _zoneLayout;
            }
        }

        public IReadOnlyList<ZoneLayout.ZoneEntry> Zones
        {
            get
            {
                EnsureReferences();
                return _zones;
            }
        }

        //게임 카메라의 처음 상태. Main 씬을 잠깐 불러서 읽으므로 처음 쓸 때 한 번만 읽는다
        public GameCameraInfo GameCamera
        {
            get
            {
                if (!_isGameCameraLoaded)
                {
                    _gameCamera = GameCameraInfo.Load();
                    _isGameCameraLoaded = true;
                }
                return _gameCamera;
            }
        }
        #endregion

        #region Public - File
        /// <summary>표를 tsv에서 다시 읽는다. 고친 내용은 버려진다. 폴더나 파일이 틀렸으면 에러를 남기고 false</summary>
        public bool Load()
        {
            _tables.Clear();
            _objects = null;
            _isGameCameraLoaded = false;
            MarkChanged();

            if (!TableUpdater.TryCollectTables(out var files)) return false;

            bool loaded = true;
            foreach (var name in StoryTableNames)
            {
                if (!files.TryGetValue(name, out var paths))
                {
                    Debug.LogError($"{name}.tsv is missing.");
                    loaded = false;
                    continue;
                }
                foreach (var path in paths)
                {
                    if (TsvTable.TryLoad(name, path, out var table))
                        _tables.Add(table);
                    else
                        loaded = false;
                }
            }
            return loaded;
        }

        /// <summary>고친 표만 tsv에 다시 쓴다</summary>
        public void Save()
        {
            foreach (var table in _tables)
            {
                if (table.IsDirty)
                    table.Save();
            }
        }

        /// <summary>읽은 뒤로 툴 밖에서 바뀐 파일이 있는지. onlyDirty면 고친 표만 본다 (저장하면 밖에서 바꾼 내용을 덮어쓰게 되는 표)</summary>
        public bool HasChangedOnDisk(bool onlyDirty)
        {
            foreach (var table in _tables)
            {
                if ((!onlyDirty || table.IsDirty) && table.IsChangedOnDisk) return true;
            }
            return false;
        }

        /// <summary>표의 내용이 바뀌었다고 알린다. 칸을 고치면 알아서 불리고, Undo나 줄 추가처럼 밖에서 내용이 바뀌었을 때는 직접 부른다</summary>
        public void MarkChanged() => Version++;

        /// <summary>칸을 고친다. 값이 실제로 바뀌었으면 true. Undo에 남기는 것은 StoryEditor가 한다</summary>
        public bool SetCell(int tableIndex, int row, string column, string value)
        {
            if (!_tables[tableIndex].Set(row, column, value)) return false;

            MarkChanged();
            return true;
        }

        /// <summary>
        /// 그 챕터의 줄을 적는 표가 문서의 몇 번째 표인지. 챕터 폴더로 나눈 표는 그 챕터 폴더의 파일이고, 나누지 않은 표는 하나뿐인 그 파일이다.
        /// 챕터 폴더가 없으면 -1 (새 챕터는 폴더와 머리 두 줄짜리 파일을 먼저 만든다)
        /// </summary>
        public int FindChapterTable(string tableName, long chapter)
        {
            int single = -1;
            int count = 0;
            for (int i = 0; i < _tables.Count; i++)
            {
                if (_tables[i].Name != tableName) continue;

                count++;
                single = i;
                string folder = Path.GetFileName(Path.GetDirectoryName(_tables[i].Path));
                if (folder == TableUpdater.ChapterDirectoryPrefix + chapter) return i;
            }
            return count == 1 ? single : -1;
        }
        #endregion

        #region Public - Lookup
        /// <summary>대상의 Actor 표 줄. 플레이어는 Actor 표에 없어서 false다</summary>
        public bool TryFindActor(string actor, out StoryLine line)
        {
            line = null;
            if (string.IsNullOrEmpty(actor)) return false;

            for (int i = 0; i < _tables.Count; i++)
            {
                if (_tables[i].Name != ActorTable) continue;
                int row = _tables[i].FindRow(ColumnId, actor);
                if (row < 0) continue;

                line = new StoryLine(this, StoryLineKind.Actor, i, row);
                return true;
            }
            return false;
        }

        public static bool IsPlayer(string actor) => actor == StoryActor.PlayerName;

        /// <summary>화면에 띄우는 대상 이름. Actor 표에 이름이 없거나 없는 번호면 번호를 그대로 보여준다</summary>
        public string GetActorName(string actor)
        {
            if (string.IsNullOrEmpty(actor)) return EmptyActorName;
            if (IsPlayer(actor)) return PlayerDisplayName;
            if (!TryFindActor(actor, out var line)) return $"Actor {actor}?";

            string name = line.Get(ColumnName);
            return string.IsNullOrEmpty(name) ? $"Actor {actor}" : name;
        }

        /// <summary>대상이 쓰는 캐릭터(Object 번호). 없는 대상이면 0</summary>
        public long GetActorObjectId(string actor)
        {
            if (IsPlayer(actor)) return PlayerObjectId;
            return TryFindActor(actor, out var line) ? StoryLine.ParseLong(line.Get(ColumnObjectId)) : 0;
        }

        public StoryActorType GetActorType(string actor)
        {
            if (IsPlayer(actor)) return StoryActorType.Player;
            if (!TryFindActor(actor, out var line)) return StoryActorType.None;
            return Enum.TryParse(line.Get(ColumnType), out StoryActorType type) ? type : StoryActorType.None;
        }

        public ObjectData GetObject(long objectId)
        {
            EnsureReferences();
            return _objects.TryGetValue(objectId, out var data) ? data : null;
        }

        /// <summary>
        /// 줄이 통째로 하는 대화의 번호. 시나리오와 퀘스트의 Dialogue 줄은 Param1에, 일반 대화(Talk) 줄은 Dialogue 칸에 적혀 있다.
        /// 대본은 이런 줄 아래에 그 대화의 대사를 펼쳐 보여준다. 대화를 하는 줄이 아니면 null
        /// </summary>
        public static string GetDialogueId(StoryLine line)
        {
            if (line.Kind == StoryLineKind.Talk) return line.Get(ColumnDialogue);
            return line.Command == StoryCommandCatalog.CommandDialogue ? line.Param1 : null;
        }

        /// <summary>대화 하나의 대사를 Id 순으로 모은다</summary>
        public List<DialogueLine> GetDialogue(string dialogueId)
        {
            var lines = GetAllDialogueLines();
            lines.RemoveAll(line => line.DialogueId != dialogueId);
            return lines;
        }

        /// <summary>Dialogue 표의 모든 대사를 Id 순으로 모은다</summary>
        public List<DialogueLine> GetAllDialogueLines()
        {
            var lines = new List<DialogueLine>();
            for (int i = 0; i < _tables.Count; i++)
            {
                if (_tables[i].Name != DialogueTable) continue;
                for (int row = 0; row < _tables[i].RowCount; row++)
                    lines.Add(new DialogueLine(_tables[i], i, row));
            }
            lines.Sort((a, b) => StoryLine.ParseLong(a.Id).CompareTo(StoryLine.ParseLong(b.Id)));
            return lines;
        }

        /// <summary>대사 한 줄 (Dialogue 표의 Id)</summary>
        public bool TryGetDialogueLine(string lineId, out DialogueLine line)
        {
            line = default;
            for (int i = 0; i < _tables.Count; i++)
            {
                if (_tables[i].Name != DialogueTable) continue;
                int row = _tables[i].FindRow(ColumnId, lineId);
                if (row < 0) continue;

                line = new DialogueLine(_tables[i], i, row);
                return true;
            }
            return false;
        }

        /// <summary>캐릭터인 Object 번호 (Object 표의 IsCharacter). 대상이 쓸 캐릭터를 고르는 목록에 띄운다</summary>
        public List<long> GetCharacterObjectIds()
        {
            EnsureReferences();
            var ids = new List<long>();
            foreach (var data in _objects.Values)
            {
                if (data.IsCharacter)
                    ids.Add(data.Id);
            }
            ids.Sort();
            return ids;
        }

        /// <summary>퀘스트 제목 (QuestInfo 표). 없으면 빈 문자열</summary>
        public string GetQuestTitle(long quest)
        {
            string id = quest.ToString(CultureInfo.InvariantCulture);
            foreach (var table in _tables)
            {
                if (table.Name != QuestInfoTable) continue;
                int row = table.FindRow(ColumnId, id);
                if (row >= 0) return table.Get(row, ColumnTitle);
            }
            return string.Empty;
        }
        #endregion

        #region Public - Position
        /// <summary>줄의 위치가 적혀 있는 칸을 찾는다. 위치를 쓰지 않는 줄이면 false</summary>
        public bool TryGetPositionSlot(StoryLine line, out PositionSlot slot)
        {
            //위치를 비운 Spawn은 Actor 표의 자리에 선다. 플레이어는 Actor 표에 없어서 비울 수 없다
            if (!UsesActorPosition(line)) return TryGetOwnPositionSlot(line, out slot);

            slot = default;
            return TryFindActor(line.Actor, out var actorLine) && TryGetOwnPositionSlot(actorLine, out slot);
        }

        /// <summary>줄이 위치를 비워서 Actor 표의 자리를 쓰는 중인지 (위치를 비운 Spawn)</summary>
        public static bool UsesActorPosition(StoryLine line)
            => line.Kind != StoryLineKind.Actor
               && StoryCommandCatalog.GetPositionKind(line.Command) == PositionKind.OptionalPoint
               && string.IsNullOrEmpty(line.Param1);

        /// <summary>그 줄 자신의 위치 칸. 비어 있어도 돌려준다 (비운 Spawn의 Actor 표 자리로 넘어가지 않는다). 위치를 쓰지 않는 줄이면 false</summary>
        public bool TryGetOwnPositionSlot(StoryLine line, out PositionSlot slot)
        {
            slot = default;
            if (line.Kind == StoryLineKind.Actor)
            {
                slot = new PositionSlot(line.TableIndex, line.Row, ColumnZone, ColumnPosition, false, true);
                return true;
            }

            var kind = StoryCommandCatalog.GetPositionKind(line.Command);
            if (kind == PositionKind.None) return false;

            slot = new PositionSlot(line.TableIndex, line.Row, ColumnZone, ColumnParam1, kind == PositionKind.X, false);
            return true;
        }

        /// <summary>칸에 적힌 Zone과 그 Zone 안의 위치를 읽는다. x만 적는 칸은 y가 0이다. 형식이 틀렸거나 없는 Zone이면 false</summary>
        public bool TryReadPosition(PositionSlot slot, out long zone, out Vector2 local)
        {
            var table = _tables[slot.TableIndex];
            zone = StoryLine.ParseLong(table.Get(slot.Row, slot.ZoneColumn));
            local = Vector2.zero;
            if (!ZoneLayout.HasZone(zone)) return false;

            string raw = table.Get(slot.Row, slot.ValueColumn);
            if (slot.IsXOnly) return TryParseFloat(raw, out local.x);

            string[] items = raw.Split(VectorSeparators);
            return items.Length == 2 && TryParseFloat(items[0], out local.x) && TryParseFloat(items[1], out local.y);
        }

        /// <summary>칸에 Zone과 위치를 적는다. 값이 실제로 바뀌었으면 true. Undo에 남기는 것은 부르는 쪽이 한다</summary>
        public bool WritePosition(PositionSlot slot, long zone, Vector2 local)
        {
            var table = _tables[slot.TableIndex];
            string value = slot.IsXOnly ? FormatNumber(local.x) : $"{FormatNumber(local.x)},{FormatNumber(local.y)}";
            //둘 다 적어야 하므로 ||로 잇지 않는다. 앞이 바뀌면 뒤를 건너뛴다
            bool changed = table.Set(slot.Row, slot.ZoneColumn, zone.ToString(CultureInfo.InvariantCulture))
                           | table.Set(slot.Row, slot.ValueColumn, value);
            if (changed)
                MarkChanged();
            return changed;
        }

        /// <summary>대상의 몸통 가운데가 발밑에서 얼마나 떨어져 있는지 (Object 표의 ColliderOffset). 카메라가 대상을 볼 때 이곳을 본다</summary>
        public Vector2 GetActorCenterOffset(string actor)
        {
            var data = GetObject(GetActorObjectId(actor));
            return data != null ? data.ColliderOffset : Vector2.zero;
        }

        /// <summary>월드 위치가 들어 있는 Zone. 어느 Zone에도 들어 있지 않으면 false</summary>
        public bool TryFindZoneAt(Vector2 world, out ZoneLayout.ZoneEntry zone)
        {
            foreach (var entry in Zones)
            {
                if (!entry.Rect.Contains(world)) continue;
                zone = entry;
                return true;
            }
            zone = null;
            return false;
        }

        public static string FormatNumber(float value)
            => (Mathf.Round(value / PositionStep) * PositionStep).ToString(PositionFormat, CultureInfo.InvariantCulture);
        #endregion

        #region Private
        //표의 숫자는 항상 소수점을 .으로 적으므로 PC 지역 설정과 상관없이 읽는다
        private static bool TryParseFloat(string raw, out float value)
            => float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private void EnsureReferences()
        {
            if (_objects != null) return;

            _objects = new Dictionary<long, ObjectData>();
            _zones.Clear();
            var objects = TableBytes.Read(ObjectTable, reader => new ObjectData(reader)) ?? new List<ObjectData>();
            var maps = TableBytes.Read(MapTable, reader => new MapData(reader)) ?? new List<MapData>();
            var zones = TableBytes.Read(ZoneTable, reader => new ZoneData(reader)) ?? new List<ZoneData>();
            foreach (var data in objects)
                _objects[data.Id] = data;

            //표가 틀리면 틀린 줄만 빼고 만든다. 에러는 ZoneLayout이 남긴다
            ZoneLayout.TryBuild(maps, zones, out _zoneLayout);
            foreach (var data in zones)
            {
                if (_zoneLayout.TryGetZone(data.Id, out var entry) && !_zones.Contains(entry))
                    _zones.Add(entry);
            }
        }
        #endregion
    }
}
