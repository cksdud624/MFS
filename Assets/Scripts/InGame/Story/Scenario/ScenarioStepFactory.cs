using System;
using System.Collections.Generic;
using InGame.Story.Scenario.Steps;
using InGame.Story.Steps;

namespace InGame.Story.Scenario
{
    /// <summary>
    /// Scenario 표의 Command 이름으로 명령을 만든다. 시나리오는 연출만 담으므로 퀘스트 목표는 여기에 없다.
    /// 명령을 추가할 때는 StoryStep을 상속한 클래스를 Steps에 만들고 여기에 이름을 한 줄 등록한다.
    /// 아래 묶음은 읽기 쉽게 나눈 것이고, 분류는 클래스의 Kind가 정한다
    /// </summary>
    public static class ScenarioStepFactory
    {
        #region Fields
        private static readonly Dictionary<string, Func<StoryStep>> Creators = new()
        {
            //대상을 월드에 세우거나 없앤다
            { "Spawn", () => new SpawnStep() },
            { "Despawn", () => new DespawnStep() },

            //연출
            { "Move", () => new MoveStep() },
            { "Effect", () => new EffectStep() },
            { "Dialogue", () => new DialogueStep(true) },
            { "Line", () => new LineStep() },
            { "CloseDialogue", () => new CloseDialogueStep() },
            { "Wait", () => new WaitStep() },
            { "FadeOut", () => new FadeStep(true) },
            { "FadeIn", () => new FadeStep(false) },
            { "Zoom", () => new ZoomStep() },
        };
        #endregion

        #region Public
        /// <summary>명령을 만들고 칸을 해석한다. 없는 이름이거나 칸이 틀리면 에러를 남기고 false</summary>
        public static bool TryCreate(StoryRow row, out StoryStep step) => StoryStep.TryCreate(row, Creators, out step);
        #endregion
    }
}
