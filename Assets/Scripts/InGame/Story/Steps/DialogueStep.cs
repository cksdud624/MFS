using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Object;
using static Common.GameDefine;

namespace InGame.Story.Steps
{
    /// <summary>
    /// Dialogue : Dialogue 표의 대화 하나를 끝까지 한다. 마지막 대사를 넘기면 대화창을 닫고 다음 줄로 넘어간다.
    /// 대화 중에는 플레이어를 Event 상태로 묶어서 움직이거나 맞지 않게 하고, 대화 상대를 적으면 서로 마주 본다.
    /// 시나리오와 퀘스트가 같이 쓰고, 어디서 쓰느냐에 따라 무게가 다르다.
    /// - 퀘스트(중간) : 월드는 멈추지 않고 카메라도 건드리지 않는다. 일반 대화 중이면 끝날 때까지 기다렸다가 시작한다.
    /// - 시나리오(대형) : 월드가 멈춘 연출 안이고, 배율을 적으면 대화 상대(없으면 플레이어)를 확대했다가 대화 직전 카메라로 되돌린다.
    /// 대사 사이에 연출을 끼우려면 시나리오에서 Line으로 한 줄씩 띄운다.
    /// Actor = 대화 상대 (비워도 된다), Param1 = DialogueId (Dialogue 표), Param2 = 확대 배율 (시나리오만, 비우면 카메라를 건드리지 않는다)
    /// </summary>
    public class DialogueStep : StoryStep
    {
        #region Fields
        //시나리오의 줄인지. 시나리오에서만 카메라를 쓴다. 퀘스트는 연출 없이 대화만 하는 중간 무게라서 카메라를 건드리지 않는다
        private readonly bool _inScenario;

        private long _dialogueId;
        //대화 동안의 카메라 배율. 0이면 카메라를 건드리지 않는다
        private float _zoom;
        #endregion

        #region Init
        public DialogueStep(bool inScenario)
        {
            _inScenario = inScenario;
        }
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
            //대화 하나는 끝나면 창을 닫으므로, 앞에서 Line으로 열어둔 창도 같이 닫힌다
            if (_inScenario)
                validator.SetDialogueOpen(false);
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

            //퀘스트의 대화는 월드가 움직이는 중에 나오므로, 플레이어가 다른 Npc와 일반 대화 중일 수 있다. 대화창을 같이 쓰므로 끝나기를 기다린다
            if (!_inScenario)
                await UniTask.WaitUntil(() => !context.IsInDialogue, cancellationToken: cancellationToken);

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
        {
            bool parsed = reader.ReadActor(false, out actor) & reader.ReadLong(1, "DialogueId", out _dialogueId);
            //퀘스트는 Param2를 읽지 않아서, 배율을 적으면 쓰지 않는 칸에 값이 있다는 에러가 난다
            if (_inScenario)
                parsed &= reader.ReadOptionalFloat(2, "Zoom", 0f, out _zoom);
            return parsed;
        }
        #endregion

        #region Private
        //대화 상대. 적지 않았거나 찾지 못하면 null (찾지 못한 것은 TryGetActor가 에러로 남긴다)
        private ObjectBase FindPartner(StoryContext context)
            => !Actor.IsNone && context.TryGetActor(this, out var partner) ? partner : null;
        #endregion
    }
}
