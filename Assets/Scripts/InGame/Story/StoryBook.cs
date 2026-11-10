using System.Collections.Generic;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story
{
    /// <summary>
    /// 검사를 통과해 실행할 수 있게 만든 이야기 전체. StoryValidator가 만들고 실행하는 동안은 바뀌지 않는다.
    /// 퀘스트는 번호 순서가 곧 진행 순서이고, 시나리오, 대상, 일반 대화는 번호로 찾는다
    /// </summary>
    public class StoryBook
    {
        #region Types
        /// <summary>Actor 표 한 줄을 실행할 때 쓰는 모양으로 읽어둔 것. 종류는 문자열이 아니라 enum으로 바꿔둔다</summary>
        public class ActorEntry
        {
            public StoryActor Actor { get; }
            public long ObjectId { get; }
            public StoryActorType Type { get; }
            //처음부터 월드에 서 있는지. 아니면 시나리오의 Spawn으로 등장한다
            public bool Resident { get; }
            //Position의 기준 Zone
            public long Zone { get; }
            //Resident가 서 있는 자리이자, Spawn이 위치를 적지 않았을 때 서는 자리. Zone의 왼쪽 아래를 0,0으로 보는 로컬 좌표다
            public Vector2 Position { get; }

            public ActorEntry(StoryActor actor, long objectId, StoryActorType type, bool resident, long zone, Vector2 position)
            {
                Actor = actor;
                ObjectId = objectId;
                Type = type;
                Resident = resident;
                Zone = zone;
                Position = position;
            }
        }

        /// <summary>퀘스트 하나. 목표와 시나리오 재생 줄을 Id 순으로 들고 있다</summary>
        public class QuestEntry
        {
            public long Number { get; }
            public IReadOnlyList<StoryStep> Steps { get; }

            public QuestEntry(long number, IReadOnlyList<StoryStep> steps)
            {
                Number = number;
                Steps = steps;
            }
        }

        /// <summary>일반 대화 한 줄. 퀘스트 Quest가 시작된 뒤부터 이 대화를 한다</summary>
        public readonly struct TalkEntry
        {
            public long Quest { get; }
            public long DialogueId { get; }

            public TalkEntry(long quest, long dialogueId)
            {
                Quest = quest;
                DialogueId = dialogueId;
            }
        }
        #endregion

        #region Fields
        //번호 순서가 곧 진행 순서다
        public IReadOnlyList<QuestEntry> Quests { get; }
        public IEnumerable<ActorEntry> Actors => _actors.Values;

        private readonly IReadOnlyDictionary<long, IReadOnlyList<StoryStep>> _scenarios;
        private readonly IReadOnlyDictionary<StoryActor, ActorEntry> _actors;
        //대상별 일반 대화. 퀘스트 번호 순으로 정렬되어 있다
        private readonly IReadOnlyDictionary<StoryActor, List<TalkEntry>> _talks;
        #endregion

        #region Init
        public StoryBook(IReadOnlyList<QuestEntry> quests, IReadOnlyDictionary<long, IReadOnlyList<StoryStep>> scenarios,
            IReadOnlyDictionary<StoryActor, ActorEntry> actors, IReadOnlyDictionary<StoryActor, List<TalkEntry>> talks)
        {
            Quests = quests;
            _scenarios = scenarios;
            _actors = actors;
            _talks = talks;
        }
        #endregion

        #region Public
        /// <summary>시나리오의 명령 목록. 퀘스트가 부르는 시나리오는 검사에서 있는지 확인했다</summary>
        public IReadOnlyList<StoryStep> GetScenario(long number)
        {
            _scenarios.TryGetValue(number, out var steps);
            return steps;
        }

        /// <summary>Actor 표의 대상. 플레이어나 없는 번호면 null</summary>
        public ActorEntry GetActor(StoryActor actor)
        {
            if (actor.IsPlayer || actor.IsNone) return null;
            _actors.TryGetValue(actor, out var entry);
            return entry;
        }

        public StoryActorType GetActorType(StoryActor actor)
            => actor.IsPlayer ? StoryActorType.Player : GetActor(actor)?.Type ?? StoryActorType.None;

        /// <summary>
        /// 지금 진행(퀘스트 progress)에서 대상이 할 일반 대화. 이미 시작된 퀘스트의 대화 중 가장 늦은 것을 고른다.
        /// 할 대화가 없으면 0
        /// </summary>
        public long FindTalk(StoryActor actor, long progress)
        {
            if (!_talks.TryGetValue(actor, out var talks)) return 0;

            long dialogueId = 0;
            for (int i = 0; i < talks.Count; i++)
            {
                if (talks[i].Quest > progress) break;
                dialogueId = talks[i].DialogueId;
            }
            return dialogueId;
        }
        #endregion
    }
}
