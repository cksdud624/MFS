using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// CloseDialogue : Line으로 열어둔 대화창을 닫는다. 바로 다음 줄로 넘어간다.
    /// 짧은 반응(이펙트)은 대사를 띄운 채 보여주고, 걷기 같은 긴 연출 앞에서는 이것으로 닫는다.
    /// 시나리오가 끝나면 저절로 닫히므로 마지막에는 적지 않아도 된다.
    /// 쓰는 칸 없음
    /// </summary>
    public class CloseDialogueStep : StoryStep
    {
        #region Public
        public override void Validate(StoryValidator validator)
        {
            //열린 창이 없는데 닫으면 Line을 빠뜨렸거나 순서가 틀린 것이다
            if (!validator.IsDialogueOpen)
                validator.Error("No dialogue is open. CloseDialogue closes the window opened by Line.");
            validator.SetDialogueOpen(false);
        }

        public override UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            context.DialoguePlayer?.Close();
            return UniTask.CompletedTask;
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
        {
            actor = StoryActor.None;
            return true;
        }
        #endregion
    }
}
