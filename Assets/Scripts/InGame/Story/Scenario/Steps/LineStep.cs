using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Object;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Line : Dialogue 표의 대사 한 줄을 띄우고, 넘기면 다음 줄로 넘어간다. 대사 사이에 연출을 끼울 때 쓴다.
    /// 말풍선은 그 대사를 말하는 대상(Dialogue 표의 Speaker)의 머리 위에 뜬다.
    /// 닫지 않아서 뒤의 연출 동안 마지막 대사가 남아 그 대상을 따라다니고, 다음 Line이 내용과 자리를 바꾼다.
    /// 말풍선은 CloseDialogue나 Dialogue(대화 하나)가 끝날 때, 아니면 시나리오가 끝날 때 닫힌다.
    /// 플레이어는 대사를 읽는 동안만 묶어서, 대사 사이의 Move로 플레이어를 움직일 수 있다.
    /// Actor = 대화 상대 (적으면 서로 마주 본다, 비워도 된다), Param1 = 대사 Id (Dialogue 표의 Id 칸, 100101)
    /// </summary>
    public class LineStep : StoryStep
    {
        #region Fields
        private long _lineId;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator)
        {
            validator.RequirePlayer();
            validator.CheckDialogueLine(_lineId);
            validator.CheckSpeaker(_lineId);
            if (Actor.IsPlayer)
                validator.Error("Line partner cannot be Player. Leave Actor empty or write the partner.");
            validator.SetDialogueOpen(true);
        }

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            var line = context.TableManager.DialogueRecord.GetRecord(_lineId);
            //말풍선은 에셋이라 정합성 검사로는 잡을 수 없다. 프리팹을 만들지 않았으면 여기서 걸린다
            if (line == null || context.DialoguePlayer == null)
            {
                LogError("Line needs a dialogue line and a dialogue view.");
                return;
            }

            ObjectBase partner = null;
            if (!Actor.IsNone && context.TryGetActor(this, out var found))
                partner = found;
            await context.DialoguePlayer.PlayLine(context, partner, line, cancellationToken);
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
            => reader.ReadActor(false, out actor) & reader.ReadLong(1, "LineId", out _lineId);
        #endregion
    }
}
