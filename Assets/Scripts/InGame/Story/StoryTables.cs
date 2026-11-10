using System.Collections.Generic;
using Generated.Table;

namespace InGame.Story
{
    /// <summary>
    /// 이야기를 이루는 표 넷. 게임에서는 TableManager에서, 에디터 메뉴에서는 .bytes를 직접 읽어서 채운다.
    /// 검사기가 표를 어디서 읽었는지 몰라도 되게 하려고 목록만 받는다
    /// </summary>
    public class StoryTables
    {
        #region Fields
        public IReadOnlyList<ActorData> Actors { get; }
        public IReadOnlyList<ScenarioData> Scenarios { get; }
        public IReadOnlyList<QuestData> Quests { get; }
        public IReadOnlyList<TalkData> Talks { get; }
        #endregion

        #region Init
        public StoryTables(IReadOnlyList<ActorData> actors, IReadOnlyList<ScenarioData> scenarios,
            IReadOnlyList<QuestData> quests, IReadOnlyList<TalkData> talks)
        {
            Actors = actors;
            Scenarios = scenarios;
            Quests = quests;
            Talks = talks;
        }

        public StoryTables(TableManager tableManager)
            : this(tableManager.ActorRecord.GetAllRecord(), tableManager.ScenarioRecord.GetAllRecord(),
                tableManager.QuestRecord.GetAllRecord(), tableManager.TalkRecord.GetAllRecord())
        {
        }
        #endregion
    }
}
