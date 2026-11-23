using System;
using System.Collections.Generic;
using Generated.Table;
using InGame.Story.Quest;
using InGame.Story.Scenario;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story
{
    /// <summary>
    /// 이야기 정합성 검사. 실행하지 않고 퀘스트를 번호 순으로, 퀘스트 안의 줄을 Id 순으로 따라가며
    /// 퀘스트가 부르는 시나리오까지 그 자리에서 펼쳐서 월드에 어떤 대상이 있는지, 화면이 어떤 상태인지 추적한다.
    /// 형식이 틀린 줄과 앞뒤가 맞지 않는 줄(세우기 전에 사용, 나간 대상 참조, 없는 표 번호, 덮인 화면으로 끝남 등)을 찾는다.
    /// 대상은 시나리오가 아니라 월드가 가지므로, 앞 퀘스트에서 세운 대상을 뒤 퀘스트가 같은 번호로 그대로 쓴다.
    /// 에디터 메뉴(Table > Validate Story)와 이야기 시작 직전에 같은 검사를 쓴다.
    /// 대상의 상태는 StoryActorTracker가 기록하고, 여기서는 규칙을 판단해 에러를 남긴다.
    /// 대상이 월드에 있는지는 명령의 분류(Kind)로 한 번에 확인하고, 명령마다 다른 확인은 각 명령의 Validate가 정한다
    /// </summary>
    public class StoryValidator
    {
        #region Types
        //시나리오와 퀘스트는 쓸 수 있는 명령이 달라서 표마다 다른 생성 함수를 넘긴다
        private delegate bool StepCreator(StoryRow row, out StoryStep step);
        #endregion

        #region Fields
        //다른 표(Object, Character, Zone)와 에셋을 확인하는 창구
        private readonly IStoryReferences _references;
        //월드에 있는 대상의 기록
        private readonly StoryActorTracker _actors = new();

        //표에서 읽은 것. 검사를 통과하면 그대로 StoryBook이 된다
        private readonly Dictionary<StoryActor, StoryBook.ActorEntry> _actorTable = new();
        private readonly Dictionary<long, IReadOnlyList<StoryStep>> _scenarios = new();
        private readonly Dictionary<long, IReadOnlyList<StoryStep>> _quests = new();
        private readonly Dictionary<StoryActor, List<StoryBook.TalkEntry>> _talks = new();
        //대사 한 줄(Dialogue 표의 Id) → 그 대사를 말하는 대상. Speaker 칸이 틀린 대사는 None으로 넣어서 없는 대사와 구분한다
        private readonly Dictionary<long, StoryActor> _speakers = new();
        //대화 번호(DialogueId) → 그 대화의 대사 Id. StoryBook에는 넣지 않고 검사할 때만 쓴다
        private readonly Dictionary<long, List<long>> _dialogueLines = new();
        //퀘스트 번호 → 화면의 퀘스트 안내에 띄우는 제목 (QuestInfo 표)
        private readonly Dictionary<long, string> _questTitles = new();
        //퀘스트가 부른 시나리오. 부르지 않는 시나리오는 실행되지 않으므로 알린다
        private readonly HashSet<long> _playedScenarios = new();

        //지금 화면이 페이드 아웃으로 가려져 있는지
        private bool _screenCovered;
        //지금 Line으로 연 말풍선이 열려 있는지. 시나리오가 끝나면 닫힌다
        public bool IsDialogueOpen { get; private set; }
        //에러 앞에 붙이는 위치 (Scenario 101020 Spawn, Actor 10001, Talk 3)
        private string _location;
        private bool _hasError;
        #endregion

        #region Init
        public StoryValidator(IStoryReferences references)
        {
            _references = references;
        }
        #endregion

        #region Public
        /// <summary>
        /// 이야기 전체를 검사하고, 통과하면 실행할 StoryBook을 돌려준다.
        /// 형식이 틀린 줄이 있으면 앞뒤 검사는 하지 않는다. Spawn 줄 하나가 틀리면 뒤의 줄이 전부 "세우기 전 사용"으로 같이 걸려서 원인이 묻힌다
        /// </summary>
        public bool Validate(StoryTables tables, out StoryBook book)
        {
            book = null;
            Reset();

            _location = nameof(PlayerObjectId);
            CheckCharacter(PlayerObjectId);
            ReadActors(tables.Actors);
            //대사의 화자는 Actor 표의 번호라서 대상을 읽은 뒤에 읽는다
            ReadDialogues(tables.Dialogues);
            ReadLines(tables.Scenarios, StoryRow.ScenarioTable, data => data.Scenario, StoryRow.From, ScenarioStepFactory.TryCreate, _scenarios);
            ReadLines(tables.Quests, StoryRow.QuestTable, data => data.Quest, StoryRow.From, QuestStepFactory.TryCreate, _quests);
            ReadTalks(tables.Talks);
            ReadQuestInfos(tables.QuestInfos);

            var questNumbers = SortedKeys(_quests);
            if (questNumbers.Count == 0)
            {
                _location = StoryRow.QuestTable;
                Error("No quest is defined. The story starts from the first quest.");
            }
            //목표를 기다리는 동안 퀘스트 안내에 제목이 같이 뜨므로 퀘스트마다 제목이 있어야 한다
            foreach (var number in questNumbers)
            {
                if (_questTitles.ContainsKey(number)) continue;
                _location = $"{StoryRow.QuestTable} {number}";
                Error("Title is not defined in QuestInfo table.");
            }

            if (!_hasError)
                FollowStory(questNumbers);

            if (_hasError)
            {
                Debug.LogError("Story is invalid. Fix the errors above.");
                return false;
            }

            var quests = new List<StoryBook.QuestEntry>(questNumbers.Count);
            foreach (var number in questNumbers)
                quests.Add(new StoryBook.QuestEntry(number, _questTitles[number], _quests[number]));
            book = new StoryBook(quests, new Dictionary<long, IReadOnlyList<StoryStep>>(_scenarios),
                new Dictionary<StoryActor, StoryBook.ActorEntry>(_actorTable), new Dictionary<StoryActor, List<StoryBook.TalkEntry>>(_talks),
                new Dictionary<long, StoryActor>(_speakers));
            return true;
        }

        /// <summary>지금 줄의 에러로 남긴다</summary>
        public void Error(string message)
        {
            _hasError = true;
            Debug.LogError($"{_location} : {message}");
        }

        /// <summary>대상의 종류. 플레이어면 Player, Actor 표에 없는 번호면 None</summary>
        public StoryActorType GetActorType(StoryActor actor)
        {
            if (actor.IsPlayer) return StoryActorType.Player;
            return _actorTable.TryGetValue(actor, out var entry) ? entry.Type : StoryActorType.None;
        }
        #endregion

        #region Public - Actor
        /// <summary>대상을 월드에 세운다. 플레이어는 하나뿐이고 없앨 수 없으므로 이야기 전체에서 한 번만 세울 수 있다</summary>
        public void Spawn(StoryActor actor)
        {
            if (actor.IsPlayer && _actors.PlayerSpawned)
            {
                Error("Player is already spawned. Player can be spawned only once in the story.");
                return;
            }
            if (_actors.IsAlive(actor))
            {
                Error($"{actor} is already in the world. Despawn it first, or use another Actor number.");
                return;
            }
            _actors.Start(actor, GetActorType(actor));
        }

        /// <summary>대상을 월드에서 없앤다. 플레이어는 없앨 수 없다</summary>
        public void Despawn(StoryActor actor)
        {
            if (actor.IsPlayer)
            {
                Error("Player cannot be despawned.");
                return;
            }
            _actors.End(actor);
        }

        /// <summary>
        /// 적이 쓰러졌다고 본다 (Clear를 통과한 뒤). 대상이 없으면 월드의 적 전부다.
        /// 쓰러진 적은 Despawn할 수 없으므로 여기서 월드에서 나간 것으로 친다
        /// </summary>
        public void Clear(StoryActor actor)
        {
            if (actor.IsNone)
            {
                if (!_actors.HasAlive(StoryActorType.Enemy))
                    Error("No Enemy is in the world at this step. Spawn Enemy before Clear, or Clear passes at once.");
                _actors.EndAll(StoryActorType.Enemy);
                return;
            }

            if (GetActorType(actor) != StoryActorType.Enemy)
            {
                Error($"{actor} is not Enemy. Clear waits for an enemy to be defeated.");
                return;
            }
            _actors.End(actor);
        }

        /// <summary>
        /// 보스가 쓰러졌다고 본다 (Boss를 통과한 뒤). 대상은 적이고 Character 표의 Boss 칸에 보스 이름이 적혀 있어야 한다.
        /// 보스가 아니면 보스전을 시작할 수 없어서 목표가 그 자리에서 넘어간다. 쓰러진 보스는 Clear와 같이 월드에서 나간 것으로 친다
        /// </summary>
        public void Boss(StoryActor actor)
        {
            if (GetActorType(actor) != StoryActorType.Enemy)
            {
                Error($"{actor} is not Enemy. Boss waits for a boss enemy to be defeated.");
                return;
            }

            long objectId = _actorTable[actor].ObjectId;
            if (!_references.IsBoss(objectId))
                Error($"{actor} (Object {objectId}) is not a boss. Write a registered boss name in the Boss column of Character table.");
            _actors.End(actor);
        }

        /// <summary>대상의 종류가 맞아야 하는 줄 (말 걸기는 Npc에게만)</summary>
        public void RequireType(StoryActor actor, StoryActorType type)
        {
            if (GetActorType(actor) != type)
                Error($"{actor} must be {type}.");
        }

        /// <summary>플레이어가 있어야 하는 줄 (대화, 퀘스트 목표)</summary>
        public void RequirePlayer()
        {
            if (!_actors.IsAlive(StoryActor.Player))
                Error("Player is not spawned before this step. Add 'Spawn Player' to an earlier scenario.");
        }
        #endregion

        #region Public - Scenario
        /// <summary>
        /// 퀘스트가 시나리오를 부른다. 시나리오의 줄을 이 자리에서 펼쳐서 검사한다.
        /// 같은 시나리오를 두 번 부르면 그때마다 월드 상태가 달라서 두 번 다 검사한다
        /// </summary>
        public void PlayScenario(long number)
        {
            if (!_scenarios.TryGetValue(number, out var steps))
            {
                Error($"Scenario {number} is not defined.");
                return;
            }
            _playedScenarios.Add(number);

            string questLocation = _location;
            foreach (var step in steps)
                Check(step);

            //시나리오가 끝나면 퀘스트 목표가 이어지므로 플레이어가 화면을 볼 수 있어야 한다. 에러는 시나리오의 마지막 줄에 붙인다
            if (_screenCovered)
                Error($"Scenario {number} ends with the screen faded out. Add FadeIn.");
            _screenCovered = false;
            //Line으로 연 말풍선은 시나리오가 끝나면 닫히므로 다음 시나리오로 이어지지 않는다
            IsDialogueOpen = false;
            _location = questLocation;
        }

        public void SetScreenCovered(bool covered) => _screenCovered = covered;

        public void SetDialogueOpen(bool open) => IsDialogueOpen = open;
        #endregion

        #region Public - Table
        public void CheckDialogue(long dialogueId)
        {
            if (!_dialogueLines.ContainsKey(dialogueId))
                Error($"Dialogue {dialogueId} is not defined.");
        }

        /// <summary>대화의 대사를 말하는 대상이 모두 이 줄 시점에 월드에 있는지</summary>
        public void CheckSpeakers(long dialogueId)
        {
            //없는 대화는 CheckDialogue가 알린다
            if (!_dialogueLines.TryGetValue(dialogueId, out var lineIds)) return;

            foreach (long lineId in lineIds)
                CheckSpeaker(lineId);
        }

        /// <summary>
        /// 대사를 말하는 대상이 이 줄 시점에 월드에 있는지. 말풍선은 그 대상의 머리 위에 뜨므로 없으면 띄울 곳이 없다.
        /// 목표를 지난 적은 쓰러졌을 수 있어서 화자로도 쓸 수 없다. 플레이어는 대사를 띄우는 명령이 따로 확인한다 (RequirePlayer)
        /// </summary>
        public void CheckSpeaker(long lineId)
        {
            //없는 대사는 CheckDialogueLine이 알리고, Speaker 칸이 틀린 대사는 표를 읽을 때 알렸다
            if (!_speakers.TryGetValue(lineId, out var speaker) || speaker.IsNone || speaker.IsPlayer) return;

            if (!_actors.IsAlive(speaker))
                Error($"Speaker {speaker} of dialogue line {lineId} is not in the world at this step.");
            else if (_actors.IsAtRisk(speaker))
                Error($"Speaker {speaker} of dialogue line {lineId} may have been defeated during an earlier goal.");
        }

        /// <summary>위치의 기준 Zone이 Zone 표에 있는지</summary>
        public void CheckZone(long zoneId)
        {
            if (!_references.HasZone(zoneId))
                Error($"Zone {zoneId} is not defined in Zone table.");
        }

        /// <summary>대사 한 줄(Dialogue 표의 Id 칸)이 있는지. 대화 번호(DialogueId)와 헷갈리기 쉬워서 따로 확인한다</summary>
        public void CheckDialogueLine(long lineId)
        {
            if (!_speakers.ContainsKey(lineId))
                Error($"Dialogue line {lineId} is not defined. Line takes the Id column (100101), not DialogueId (1001).");
        }

        /// <summary>대상이 가진 이펙트인지. 확인할 수 없는 곳(런타임)이면 건너뛴다</summary>
        public void CheckEffect(StoryActor actor, string effectName)
        {
            long objectId = actor.IsPlayer ? PlayerObjectId
                : _actorTable.TryGetValue(actor, out var entry) ? entry.ObjectId : 0;
            if (objectId == 0) return;
            if (_references.HasEffect(objectId, effectName) == false)
                Error($"Object {objectId} has no effect '{effectName}'.");
        }
        #endregion

        #region Private - Read
        private void Reset()
        {
            _actors.Clear();
            _actorTable.Clear();
            _scenarios.Clear();
            _quests.Clear();
            _talks.Clear();
            _speakers.Clear();
            _dialogueLines.Clear();
            _questTitles.Clear();
            _playedScenarios.Clear();
            _screenCovered = false;
            IsDialogueOpen = false;
            _hasError = false;
        }

        /// <summary>Actor 표를 읽는다. 대상은 모두 캐릭터이고 종류는 Npc나 Enemy다 (플레이어는 표에 두지 않는다)</summary>
        private void ReadActors(IReadOnlyList<ActorData> datas)
        {
            foreach (var data in datas)
            {
                _location = $"Actor {data.Id}";
                if (!IsValidNumber(data.Id, StoryActorChapterRange))
                {
                    Error($"Id must be chapter × {StoryActorChapterRange} + order such as 10001.");
                    continue;
                }
                var actor = StoryActor.FromId(data.Id);
                if (_actorTable.ContainsKey(actor))
                {
                    Error($"Id {actor} is duplicated.");
                    continue;
                }
                //숫자도 Enum.TryParse를 통과하므로 정의된 이름인지 따로 확인한다
                if (!Enum.TryParse(data.Type, out StoryActorType type) || !Enum.IsDefined(typeof(StoryActorType), type)
                    || (type != StoryActorType.Npc && type != StoryActorType.Enemy))
                {
                    Error($"Type must be Npc or Enemy : '{data.Type}'");
                    continue;
                }

                CheckCharacter(data.ObjectId);
                //Position은 Zone 기준 로컬 좌표라서 Zone이 없으면 어디에 세울지 정할 수 없다
                if (data.Zone == 0)
                    Error("Zone is required. Position is local to the zone (its bottom-left is 0,0).");
                else
                    CheckZone(data.Zone);
                _actorTable[actor] = new StoryBook.ActorEntry(actor, data.ObjectId, type, data.Name, data.Resident, data.Zone, data.Position);
            }
        }

        /// <summary>
        /// Dialogue 표를 읽는다. 대사마다 Speaker 칸에 그 대사를 말하는 대상을 적는다 (Player 또는 Actor 표의 번호).
        /// 대사가 월드에 있는 대상을 가리켜야 말풍선을 누구 머리 위에 띄울지, 그때 그 대상이 있는지를 표만으로 알 수 있다
        /// </summary>
        private void ReadDialogues(IReadOnlyList<DialogueData> datas)
        {
            foreach (var data in datas)
            {
                _location = $"Dialogue {data.Id}";
                if (!_dialogueLines.TryGetValue(data.DialogueId, out var lineIds))
                {
                    lineIds = new List<long>();
                    _dialogueLines[data.DialogueId] = lineIds;
                }
                lineIds.Add(data.Id);
                _speakers[data.Id] = ReadSpeaker(data.Speaker);
            }
        }

        //Speaker 칸을 읽는다. 틀리면 에러를 남기고 None을 돌려준다
        private StoryActor ReadSpeaker(string raw)
        {
            if (!StoryActor.TryParse(raw, out var speaker))
            {
                Error($"Speaker must be {StoryActor.PlayerName} or an Actor Id such as 10001 : '{raw}'");
                return StoryActor.None;
            }
            if (speaker.IsPlayer) return speaker;

            if (!_actorTable.TryGetValue(speaker, out var entry))
            {
                Error($"Speaker {speaker} is not defined in Actor table.");
                return StoryActor.None;
            }
            //말풍선에 이름을 띄우므로 말하는 대상은 이름이 있어야 한다. 말하지 않는 대상은 비워도 된다
            if (string.IsNullOrEmpty(entry.Name))
                Error($"Speaker {speaker} has no Name in Actor table. The name is shown on the speech bubble.");
            return speaker;
        }

        /// <summary>
        /// 시나리오나 퀘스트 표를 번호별로 묶어 명령으로 만든다. 줄은 표에 적힌 순서가 아니라 Id 순으로 실행한다.
        /// 중간에 줄을 끼울 때 줄을 옮기지 않고 Id만 맞춰 적어도 되게 하려는 것이다
        /// </summary>
        private void ReadLines<T>(IReadOnlyList<T> datas, string table, Func<T, long> getNumber, Func<T, StoryRow> toRow,
            StepCreator create, Dictionary<long, IReadOnlyList<StoryStep>> result)
        {
            var groups = new Dictionary<long, List<StoryStep>>();
            var ids = new HashSet<long>();
            foreach (var data in datas)
            {
                var row = toRow(data);
                _location = row.ToString();
                //생성된 Record가 Id를 키로 쓰므로 겹치면 게임에서 표를 불러올 때 멈춘다
                if (!ids.Add(row.Id))
                {
                    Error($"Id {row.Id} is duplicated.");
                    continue;
                }
                long number = getNumber(data);
                if (!IsValidNumber(number, StoryChapterRange))
                {
                    Error($"{table} must be chapter × {StoryChapterRange} + order such as 101 : {number}");
                    continue;
                }
                //번호 범위를 넘으면 다음 번호의 줄 Id와 겹친다
                if (row.Id / StoryLineIdRange != number || row.Id % StoryLineIdRange == 0)
                    Error($"Id must be between {number * StoryLineIdRange + 1} and {(number + 1) * StoryLineIdRange - 1} for {table} {number}.");
                //형식 에러는 명령이 직접 남긴다
                if (!create(row, out var step))
                {
                    _hasError = true;
                    continue;
                }

                if (!groups.TryGetValue(number, out var steps))
                {
                    steps = new List<StoryStep>();
                    groups[number] = steps;
                }
                steps.Add(step);
            }

            foreach (var pair in groups)
            {
                pair.Value.Sort((a, b) => a.Id.CompareTo(b.Id));
                result[pair.Key] = pair.Value;
            }
        }

        /// <summary>
        /// Talk 표를 읽는다. 말을 거는 대상은 Npc이고, 퀘스트 번호는 "이 퀘스트가 시작된 뒤부터"를 뜻한다.
        /// 같은 대상에게 같은 퀘스트 번호가 둘이면 어느 대화를 할지 정할 수 없다
        /// </summary>
        private void ReadTalks(IReadOnlyList<TalkData> datas)
        {
            var ids = new HashSet<long>();
            foreach (var data in datas)
            {
                _location = $"Talk {data.Id}";
                if (!ids.Add(data.Id))
                {
                    Error($"Id {data.Id} is duplicated.");
                    continue;
                }
                //표를 챕터 폴더로 나누므로 Talk도 챕터를 번호에 넣는다. 그래야 챕터마다 1번부터 매겨서 겹치는 일이 없다
                if (!IsValidNumber(data.Id, StoryChapterRange))
                {
                    Error($"Id must be chapter × {StoryChapterRange} + order such as 101 : {data.Id}");
                    continue;
                }
                var actor = StoryActor.FromId(data.Actor);
                if (GetActorType(actor) != StoryActorType.Npc)
                {
                    Error($"Actor must be an Npc Id in Actor table : {data.Actor}");
                    continue;
                }
                long quest = data.Quest;
                if (!_quests.ContainsKey(quest))
                {
                    Error($"Quest must be a quest number in Quest table : {quest}");
                    continue;
                }
                //다른 챕터의 퀘스트를 가리키면 그 줄이 엉뚱한 챕터 폴더에 들어 있다는 뜻이다
                if (data.Id / StoryChapterRange != quest / StoryChapterRange)
                {
                    Error($"Quest {quest} is not in chapter {data.Id / StoryChapterRange}.");
                    continue;
                }
                CheckDialogue(data.Dialogue);
                CheckTalkSpeakers(data.Dialogue, actor);

                if (!_talks.TryGetValue(actor, out var talks))
                {
                    talks = new List<StoryBook.TalkEntry>();
                    _talks[actor] = talks;
                }
                if (talks.Exists(talk => talk.Quest == quest))
                {
                    Error($"{actor} already has a talk from Quest {quest}.");
                    continue;
                }
                talks.Add(new StoryBook.TalkEntry(quest, data.Dialogue));
            }

            foreach (var talks in _talks.Values)
                talks.Sort((a, b) => a.Quest.CompareTo(b.Quest));
        }

        /// <summary>
        /// QuestInfo 표를 읽는다. 퀘스트마다 화면의 퀘스트 안내에 띄울 제목을 적는다.
        /// 줄을 묶는 번호만 있는 Quest 표에는 퀘스트 하나에 대한 것을 적을 자리가 없어서 표를 따로 둔다
        /// </summary>
        private void ReadQuestInfos(IReadOnlyList<QuestInfoData> datas)
        {
            foreach (var data in datas)
            {
                _location = $"QuestInfo {data.Id}";
                if (!_quests.ContainsKey(data.Id))
                {
                    Error($"Quest {data.Id} is not defined in Quest table.");
                    continue;
                }
                if (string.IsNullOrEmpty(data.Title))
                {
                    Error("Title is required. It is shown on the quest guide.");
                    continue;
                }
                _questTitles[data.Id] = data.Title;
            }
        }

        /// <summary>
        /// 일반 대화의 대사는 말을 건 Npc나 플레이어가 말해야 한다.
        /// 일반 대화는 진행 중 아무 때나 걸 수 있어서, 다른 대상이 그 자리에 있는지는 표만으로 알 수 없다
        /// </summary>
        private void CheckTalkSpeakers(long dialogueId, StoryActor partner)
        {
            if (!_dialogueLines.TryGetValue(dialogueId, out var lineIds)) return;

            foreach (long lineId in lineIds)
            {
                var speaker = _speakers[lineId];
                if (speaker.IsNone || speaker.IsPlayer || speaker.Equals(partner)) continue;
                Error($"Speaker {speaker} of dialogue line {lineId} is not {StoryActor.PlayerName} or {partner}. Only the two are in a talk.");
            }
        }

        private static List<long> SortedKeys(Dictionary<long, IReadOnlyList<StoryStep>> lines)
        {
            var keys = new List<long>(lines.Keys);
            keys.Sort();
            return keys;
        }

        //번호는 챕터 × chapterRange + 순번이고 순번은 1부터다. 100, 200처럼 순번이 0인 번호는 없다
        private static bool IsValidNumber(long number, long chapterRange) => number > chapterRange && number % chapterRange != 0;
        #endregion

        #region Private - Follow
        /// <summary>상주 대상을 세운 상태에서 퀘스트를 번호 순으로 따라간다. 게임이 이야기를 진행하는 순서와 같다</summary>
        private void FollowStory(List<long> questNumbers)
        {
            foreach (var entry in _actorTable.Values)
            {
                if (entry.Resident)
                    _actors.Start(entry.Actor, entry.Type);
            }

            foreach (var number in questNumbers)
            {
                foreach (var step in _quests[number])
                    Check(step);
            }

            foreach (var number in _scenarios.Keys)
            {
                if (!_playedScenarios.Contains(number))
                    Debug.LogWarning($"Scenario {number} is not played by any quest. It never runs.");
            }
        }

        /// <summary>한 줄을 검사한다. 대상이 월드에 있는지는 명령마다 따로 보면 빠뜨리는 명령이 생기므로 분류로 한 곳에서 본다</summary>
        private void Check(StoryStep step)
        {
            _location = step.ToString();
            var actor = step.Actor;
            if (!actor.IsNone && !actor.IsPlayer && !_actorTable.ContainsKey(actor))
            {
                //없는 번호로 뒤의 규칙을 보면 같은 원인으로 에러가 겹친다
                Error($"{actor} is not defined in Actor table.");
                return;
            }

            if (!actor.IsNone)
            {
                if (step.Kind == StoryStepKind.Content || step.Kind == StoryStepKind.End)
                    RequireUsable(actor);
                else if (step.Kind == StoryStepKind.Goal)
                    RequireAlive(actor);
            }
            if (step.Kind == StoryStepKind.Goal)
                RequirePlayer();

            step.Validate(this);

            //목표를 채우는 동안 플레이어가 적과 싸울 수 있다. 남아 있는 적은 그 뒤로 쓰러졌을 수 있다
            if (step.Kind == StoryStepKind.Goal)
                _actors.MarkAtRisk(StoryActorType.Enemy);
        }

        /// <summary>이 줄 시점에 대상이 월드에 있어야 한다</summary>
        private bool RequireAlive(StoryActor actor)
        {
            if (_actors.IsAlive(actor)) return true;

            if (_actors.IsEnded(actor))
                Error($"{actor} has already left the world (Despawn or Clear) before this step.");
            else if (actor.IsPlayer)
                Error("Player is not spawned before this step. Add 'Spawn Player' to an earlier scenario.");
            else
                Error($"{actor} is not in the world at this step. Spawn it first, or set Resident in Actor table.");
            return false;
        }

        /// <summary>
        /// 이 줄 시점에 대상이 월드에 있고 쓰러졌을 리 없어야 한다.
        /// 목표를 지난 적은 쓰러졌을 수 있어서 시나리오가 다룰 수 없고, Clear로만 기다린다
        /// </summary>
        private void RequireUsable(StoryActor actor)
        {
            if (!RequireAlive(actor)) return;
            if (_actors.IsAtRisk(actor))
                Error($"{actor} may have been defeated during an earlier goal. Point it only with Clear after a goal.");
        }

        /// <summary>대상은 모두 캐릭터다. Object 표의 IsCharacter와 Character 표를 함께 확인한다</summary>
        private void CheckCharacter(long objectId)
        {
            if (!_references.HasObject(objectId))
            {
                Error($"Object {objectId} is not defined.");
                return;
            }
            if (!_references.IsCharacter(objectId))
                Error($"Object {objectId} is not a character (IsCharacter is false).");
            //캐릭터는 생성할 때 Character 표에서 같은 Id를 찾는다. 없으면 생성은 되지만 캐릭터 데이터가 빈 채로 나온다
            if (!_references.HasCharacter(objectId))
                Error($"Character {objectId} is not defined in Character table.");
        }
        #endregion
    }
}
