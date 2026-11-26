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
/// 이야기 정합성 검사 메뉴. Play 없이 Actor, Scenario, Quest, QuestInfo, Talk, Dialogue 표와 Zone 배치(Map, Zone 표)를 함께 검사한다.
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
        private readonly Dictionary<long, CharacterData> _characters;
        //Boss 표의 이름. Character 표의 Boss 칸이 여기 있어야 보스다
        private readonly HashSet<string> _bossNames;
        private readonly ZoneLayout _zoneLayout;

        public BytesReferences(List<ObjectData> objects, List<CharacterData> characters, List<BossData> bosses, ZoneLayout zoneLayout)
        {
            _objects = objects.ToDictionary(data => data.Id);
            _characters = characters.ToDictionary(data => data.Id);
            _bossNames = bosses.Select(data => data.Id).ToHashSet();
            _zoneLayout = zoneLayout;
        }

        public bool HasObject(long objectId) => _objects.ContainsKey(objectId);
        public bool IsCharacter(long objectId) => _objects.TryGetValue(objectId, out var data) && data.IsCharacter;
        public bool HasCharacter(long characterId) => _characters.ContainsKey(characterId);
        public bool IsBoss(long characterId)
            => _characters.TryGetValue(characterId, out var data) && !string.IsNullOrEmpty(data.Boss) && _bossNames.Contains(data.Boss);
        public bool HasZone(long zoneId) => _zoneLayout.HasZone(zoneId);

        //이펙트 프리팹은 오브젝트마다 Effect/(ObjectId)/ 아래에 있다
        public bool? HasEffect(long objectId, string effectName)
            => File.Exists($"{AssetPathEffect}{objectId}/{effectName}{AssetExtensionPrefab}");
    }
    #endregion

    #region Fields
    private const string ActorTable = "Actor";
    private const string ScenarioTable = "Scenario";
    private const string QuestTable = "Quest";
    private const string QuestInfoTable = "QuestInfo";
    private const string TalkTable = "Talk";
    private const string ObjectTable = "Object";
    private const string CharacterTable = "Character";
    private const string BossTable = "Boss";
    private const string DialogueTable = "Dialogue";
    private const string MapTable = "Map";
    private const string ZoneTable = "Zone";
    #endregion

    #region Public
    [MenuItem("Table/Validate Story")]
    public static void Validate() => Run();

    /// <summary>검사하고 통과했는지 돌려준다. 틀린 곳은 콘솔에 에러로 남긴다. 메뉴와 시나리오 툴이 같이 쓴다</summary>
    public static bool Run()
    {
        //폴더가 틀렸으면 여기서도 에러가 남는다. Update Table이 멈추는 원인이므로 같이 보이게 두고 검사는 이어서 한다
        TableUpdater.TryCollectTables(out var tableFiles);
        foreach (var table in new[] { ActorTable, ScenarioTable, QuestTable, QuestInfoTable, TalkTable, ObjectTable, CharacterTable, BossTable, DialogueTable, MapTable, ZoneTable })
            WarnIfStale(table, tableFiles);

        var actors = TableBytes.Read(ActorTable, reader => new ActorData(reader));
        var scenarios = TableBytes.Read(ScenarioTable, reader => new ScenarioData(reader));
        var quests = TableBytes.Read(QuestTable, reader => new QuestData(reader));
        var questInfos = TableBytes.Read(QuestInfoTable, reader => new QuestInfoData(reader));
        var talks = TableBytes.Read(TalkTable, reader => new TalkData(reader));
        var objects = TableBytes.Read(ObjectTable, reader => new ObjectData(reader));
        var characters = TableBytes.Read(CharacterTable, reader => new CharacterData(reader));
        var bosses = TableBytes.Read(BossTable, reader => new BossData(reader));
        var dialogues = TableBytes.Read(DialogueTable, reader => new DialogueData(reader));
        var maps = TableBytes.Read(MapTable, reader => new MapData(reader));
        var zones = TableBytes.Read(ZoneTable, reader => new ZoneData(reader));
        if (actors == null || scenarios == null || quests == null || questInfos == null || talks == null
            || objects == null || characters == null || bosses == null || dialogues == null || maps == null || zones == null) return false;

        //Zone 배치가 틀려도 이야기 검사는 이어서 한다. 틀린 Zone을 가리키는 줄까지 한 번에 보이게 하려는 것이다
        bool zoneValid = ZoneLayout.TryBuild(maps, zones, out var zoneLayout);
        var validator = new StoryValidator(new BytesReferences(objects, characters, bosses, zoneLayout));
        bool storyValid = validator.Validate(new StoryTables(actors, scenarios, quests, questInfos, talks, dialogues), out var book);
        if (zoneValid && storyValid)
            Debug.Log($"[이야기 검사] Zone {zones.Count}개, 퀘스트 {book.Quests.Count}개 통과");
        else
            Debug.LogError("[이야기 검사] 실패. 위 에러를 확인한다");
        return zoneValid && storyValid;
    }
    #endregion

    #region Private
    //tsv를 고치고 Update Table을 안 했으면 고치기 전 데이터를 검사하게 되므로 알린다. 챕터 폴더로 나눈 표는 그중 하나만 고쳐도 알린다
    private static void WarnIfStale(string tableName, IReadOnlyDictionary<string, List<string>> tableFiles)
    {
        string bytesPath = TableBytes.GetPath(tableName);
        if (!tableFiles.TryGetValue(tableName, out var tsvPaths) || !File.Exists(bytesPath)) return;

        var bytesTime = File.GetLastWriteTimeUtc(bytesPath);
        if (tsvPaths.Any(path => File.GetLastWriteTimeUtc(path) > bytesTime))
            Debug.LogWarning($"{tableName}.tsv is newer than {tableName}.bytes. Run Table > Update Table first, or old data is validated.");
    }
    #endregion
}
