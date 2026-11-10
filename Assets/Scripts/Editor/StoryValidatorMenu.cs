using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Generated.Table;
using InGame.Story;
using InGame.World;
using UnityEditor;
using UnityEngine;
using static Common.GameDefine;

/// <summary>
/// 이야기 정합성 검사 메뉴. Play 없이 Actor, Scenario, Quest, Talk 표와 Zone 배치(Map, Zone 표)를 함께 검사한다.
/// 검사 내용은 이야기 시작 직전에 하는 것과 같고(StoryValidator), 여기서는 이펙트 프리팹이 있는지까지 본다.
/// 게임이 실제로 읽는 .bytes를 검사하므로 tsv를 고친 뒤에는 Table > Update Table을 먼저 실행한다
/// </summary>
public static class StoryValidatorMenu
{
    #region Types
    /// <summary>.bytes를 직접 읽은 표와 에셋 경로로 이야기 밖의 참조를 확인한다</summary>
    private class BytesReferences : IStoryReferences
    {
        private readonly Dictionary<long, ObjectData> _objects;
        private readonly HashSet<long> _characterIds;
        private readonly HashSet<long> _dialogueIds;
        private readonly HashSet<long> _dialogueLineIds;
        private readonly ZoneLayout _zoneLayout;

        public BytesReferences(List<ObjectData> objects, List<CharacterData> characters, List<DialogueData> dialogues, ZoneLayout zoneLayout)
        {
            _objects = objects.ToDictionary(data => data.Id);
            _characterIds = new HashSet<long>(characters.Select(data => data.Id));
            _dialogueIds = new HashSet<long>(dialogues.Select(data => data.DialogueId));
            _dialogueLineIds = new HashSet<long>(dialogues.Select(data => data.Id));
            _zoneLayout = zoneLayout;
        }

        public bool HasObject(long objectId) => _objects.ContainsKey(objectId);
        public bool IsCharacter(long objectId) => _objects.TryGetValue(objectId, out var data) && data.IsCharacter;
        public bool HasCharacter(long characterId) => _characterIds.Contains(characterId);
        public bool HasDialogue(long dialogueId) => _dialogueIds.Contains(dialogueId);
        public bool HasDialogueLine(long lineId) => _dialogueLineIds.Contains(lineId);
        public bool HasZone(long zoneId) => _zoneLayout.HasZone(zoneId);

        //이펙트 프리팹은 오브젝트마다 Effect/(ObjectId)/ 아래에 있다
        public bool? HasEffect(long objectId, string effectName)
            => File.Exists($"{AssetPathEffect}{objectId}/{effectName}{AssetExtensionPrefab}");
    }
    #endregion

    #region Fields
    private const string TableDirectory = "Assets/.Table/";
    private const string BytesDirectory = "Assets/Generated/Table/";
    private const string ActorTable = "Actor";
    private const string ScenarioTable = "Scenario";
    private const string QuestTable = "Quest";
    private const string TalkTable = "Talk";
    private const string ObjectTable = "Object";
    private const string CharacterTable = "Character";
    private const string DialogueTable = "Dialogue";
    private const string MapTable = "Map";
    private const string ZoneTable = "Zone";
    #endregion

    #region Public
    [MenuItem("Table/Validate Story")]
    public static void Validate()
    {
        foreach (var table in new[] { ActorTable, ScenarioTable, QuestTable, TalkTable, ObjectTable, CharacterTable, DialogueTable, MapTable, ZoneTable })
            WarnIfStale(table);

        var actors = ReadTable(ActorTable, reader => new ActorData(reader));
        var scenarios = ReadTable(ScenarioTable, reader => new ScenarioData(reader));
        var quests = ReadTable(QuestTable, reader => new QuestData(reader));
        var talks = ReadTable(TalkTable, reader => new TalkData(reader));
        var objects = ReadTable(ObjectTable, reader => new ObjectData(reader));
        var characters = ReadTable(CharacterTable, reader => new CharacterData(reader));
        var dialogues = ReadTable(DialogueTable, reader => new DialogueData(reader));
        var maps = ReadTable(MapTable, reader => new MapData(reader));
        var zones = ReadTable(ZoneTable, reader => new ZoneData(reader));
        if (actors == null || scenarios == null || quests == null || talks == null
            || objects == null || characters == null || dialogues == null || maps == null || zones == null) return;

        //Zone 배치가 틀려도 이야기 검사는 이어서 한다. 틀린 Zone을 가리키는 줄까지 한 번에 보이게 하려는 것이다
        bool zoneValid = ZoneLayout.TryBuild(maps, zones, out var zoneLayout);
        var validator = new StoryValidator(new BytesReferences(objects, characters, dialogues, zoneLayout));
        bool storyValid = validator.Validate(new StoryTables(actors, scenarios, quests, talks), out var book);
        if (zoneValid && storyValid)
            Debug.Log($"[이야기 검사] Zone {zones.Count}개, 퀘스트 {book.Quests.Count}개 통과");
        else
            Debug.LogError("[이야기 검사] 실패. 위 에러를 확인한다");
    }
    #endregion

    #region Private
    /// <summary>생성된 .bytes를 게임과 같은 방식(한 줄씩 BinaryReader)으로 읽는다. Addressables를 거치지 않아서 Play 없이 읽힌다</summary>
    private static List<T> ReadTable<T>(string tableName, Func<BinaryReader, T> create)
    {
        string path = BytesDirectory + tableName + ".bytes";
        if (!File.Exists(path))
        {
            Debug.LogError($"{path} is missing. Run Table > Update Table first.");
            return null;
        }

        var datas = new List<T>();
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        using var reader = new BinaryReader(stream);
        while (reader.BaseStream.Position < reader.BaseStream.Length)
            datas.Add(create(reader));
        return datas;
    }

    //tsv를 고치고 Update Table을 안 했으면 고치기 전 데이터를 검사하게 되므로 알린다
    private static void WarnIfStale(string tableName)
    {
        string tsvPath = TableDirectory + tableName + ".tsv";
        string bytesPath = BytesDirectory + tableName + ".bytes";
        if (File.Exists(tsvPath) && File.Exists(bytesPath) && File.GetLastWriteTimeUtc(tsvPath) > File.GetLastWriteTimeUtc(bytesPath))
            Debug.LogWarning($"{tableName}.tsv is newer than {tableName}.bytes. Run Table > Update Table first, or old data is validated.");
    }
    #endregion
}
