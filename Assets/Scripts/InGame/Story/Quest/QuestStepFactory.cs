using System;
using System.Collections.Generic;
using InGame.Story.Quest.Steps;
using InGame.Story.Steps;

namespace InGame.Story.Quest
{
    /// <summary>
    /// Quest 표의 Command 이름으로 명령을 만든다. 퀘스트는 목표와 그에 딸린 대화, 시나리오 재생을 담고, 연출은 시나리오에 적는다.
    /// 명령을 추가할 때는 StoryStep을 상속한 클래스를 Steps에 만들고 여기에 이름을 한 줄 등록한다
    /// </summary>
    public static class QuestStepFactory
    {
        #region Fields
        private static readonly Dictionary<string, Func<StoryStep>> Creators = new()
        {
            //연출 : Scenario 표의 시나리오 하나를 재생한다
            { "Scenario", () => new PlayScenarioStep() },

            //대화 : 연출 없이 대화만 한다. 대사 사이에 연출을 끼우려면 시나리오를 쓴다
            { "Dialogue", () => new DialogueStep(false) },

            //목표 : 플레이어가 해내야 넘어간다
            { "Interact", () => new InteractStep() },
            { "Reach", () => new ReachStep() },
            { "Clear", () => new ClearStep() },
        };
        #endregion

        #region Public
        /// <summary>명령을 만들고 칸을 해석한다. 없는 이름이거나 칸이 틀리면 에러를 남기고 false</summary>
        public static bool TryCreate(StoryRow row, out StoryStep step) => StoryStep.TryCreate(row, Creators, out step);
        #endregion
    }
}
