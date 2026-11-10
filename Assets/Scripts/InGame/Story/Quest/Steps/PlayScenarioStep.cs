using System.Threading;
using Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story.Quest.Steps
{
    /// <summary>
    /// Scenario : Scenario 표의 시나리오 하나를 처음부터 끝까지 재생한다. 끝나면 다음 줄로 넘어간다.
    /// 시나리오 하나가 통째로 연출 구간이라, 재생하는 동안 월드의 조작과 AI가 모두 멈추고(도중에 등장한 대상 포함)
    /// 끝나면 카메라를 처음 상태로 되돌린 뒤 다시 켠다. 카메라가 돌아오는 동안 움직이지 않게 순서를 이렇게 둔다.
    /// Param1 = 시나리오 번호 (1-2)
    /// </summary>
    public class PlayScenarioStep : StoryStep
    {
        #region Fields
        private ChapterNumber _scenario;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.PlayScenario(_scenario);

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            var steps = context.Book.GetScenario(_scenario);

            //목표를 채운 순간 다른 Npc와 일반 대화 중일 수 있다. 대화창을 같이 쓰므로 대화가 끝난 뒤에 시작한다
            await UniTask.WaitUntil(() => !context.IsTalking, cancellationToken: cancellationToken);
            context.BeginScenario();
            foreach (var step in steps)
            {
                Debug.Log($"[시나리오] {_scenario} {step} {step.Actor}");
                await step.Run(context, cancellationToken);
            }

            //카메라가 없어도 조작은 돌려줘야 플레이어가 멈춘 채로 남지 않는다
            var camera = context.CameraDirector;
            if (camera != null && (!Mathf.Approximately(camera.CurrentZoom, 1f) || camera.CurrentFocus != null))
                await camera.Zoom(1f, null, CameraZoomDuration, cancellationToken);
            context.EndScenario();
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
        {
            actor = StoryActor.None;
            return reader.ReadNumber(1, "Scenario", out _scenario);
        }
        #endregion
    }
}
