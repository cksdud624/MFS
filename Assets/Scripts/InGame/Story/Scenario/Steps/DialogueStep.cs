using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Object;
using static Common.GameDefine;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Dialogue : 대화를 진행한다. 마지막 대사를 넘기면 다음 줄로 넘어간다.
    /// 대화 중에는 플레이어를 Event 상태로 묶어서 움직이거나 맞지 않게 한다.
    /// 배율을 적으면 대화 상대(없으면 플레이어)를 확대해서 보여주고, 대화가 끝나면 대화 직전의 카메라로 되돌린다.
    /// 진행과 상관없이 NPC에게 말을 걸어 하는 대화는 Talk 표에 적는다.
    /// Actor = 대화 상대 (적으면 서로 마주 본다, 비워도 된다), Param1 = DialogueId (Dialogue 표),
    /// Param2 = 확대 배율 (비우면 카메라를 건드리지 않는다)
    /// </summary>
    public class DialogueStep : StoryStep
    {
        #region Fields
        private long _dialogueId;
        //대화 동안의 카메라 배율. 0이면 카메라를 건드리지 않는다
        private float _zoom;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator)
        {
            validator.RequirePlayer();
            validator.CheckDialogue(_dialogueId);
            if (Actor.IsPlayer)
                validator.Error("Dialogue partner cannot be Player. Leave Actor empty or write the partner.");
            if (_zoom < 0f)
                validator.Error($"Param2 (Zoom) must be greater than 0, or empty : {_zoom}");
        }

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            var lines = context.GetDialogueLines(_dialogueId);
            var player = context.InGameContext.Player;
            //대화창은 에셋이라 정합성 검사로는 잡을 수 없다. 프리팹을 만들지 않았으면 여기서 걸린다
            if (lines == null || player == null || context.DialoguePlayer == null)
            {
                LogError("Dialogue needs dialogue lines, a dialogue view and a player.");
                return;
            }

            var partner = FindPartner(context);
            var camera = _zoom > 0f ? context.CameraDirector : null;
            if (_zoom > 0f && camera == null)
                LogError("Main camera is missing. Dialogue goes on without zoom.");

            //대화 직전의 카메라를 기억해서 되돌린다. 처음 상태로 돌리면 시나리오에서 잡아둔 화면이 풀린다
            float previousZoom = camera?.CurrentZoom ?? 1f;
            var previousFocus = camera?.CurrentFocus;
            if (camera != null)
                await camera.Zoom(_zoom, (partner != null ? partner : player).Center, CameraZoomDuration, cancellationToken);

            await context.DialoguePlayer.PlayConversation(partner, lines, cancellationToken);

            if (camera != null)
                await camera.Zoom(previousZoom, previousFocus, CameraZoomDuration, cancellationToken);
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
            => reader.ReadActor(false, out actor)
               & reader.ReadLong(1, "DialogueId", out _dialogueId)
               & reader.ReadOptionalFloat(2, "Zoom", 0f, out _zoom);
        #endregion

        #region Private
        //대화 상대. 적지 않았거나 찾지 못하면 null (찾지 못한 것은 TryGetActor가 에러로 남긴다)
        private ObjectBase FindPartner(StoryContext context)
            => !Actor.IsNone && context.TryGetActor(this, out var partner) ? partner : null;
        #endregion
    }
}
