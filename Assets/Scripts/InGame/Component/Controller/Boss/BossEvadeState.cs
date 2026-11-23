using Common.Template.FSM;
using InGame.Context;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 물러나는 중. 대상의 반대쪽으로 대시해서 빠진 뒤 거리 재기로 돌아간다.
    /// 패턴 사이에 맞았을 때(회피)와 경직이 풀렸을 때 쓴다. 맞은 자리에 그대로 서서 이어지는 콤보를 다 받지 않게 한다.
    /// 플레이어의 대시와 같은 대시라서 빠지는 동안에는 맞지 않고, 대시 스택이 없으면 빠지지 못한다
    /// </summary>
    public class BossEvadeState : BossStateBase
    {
        #region Fields
        //대시가 나갔는지. 스택이 없거나 대시할 수 없는 상태였으면 나가지 않는다
        private bool _isDashStarted;
        #endregion

        #region Init
        public BossEvadeState(StateMachine<BossAIState> stateMachine, ObjectContext objectContext, ControllerBossAI controller)
            : base(stateMachine, objectContext, controller)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            //대시는 이동 입력이 가리키는 쪽으로 나가므로 물러날 쪽을 먼저 누른다. 대상이 없으면 보던 쪽의 반대로 빠진다
            float awayX = Controller.TryGetTargetGap(out _, out float directionX) ? -directionX
                : ObjectContext.Direction == Direction.Right ? -1f : 1f;
            Controller.SetMoveInput(awayX);
            Controller.RequestDash();
            _isDashStarted = ObjectContext.IsDashing;
            if (_isDashStarted)
                Controller.SetPatternName(BossLabelEvade);
        }

        public override void OnExit()
        {
            Controller.SetPatternName(string.Empty);
            Controller.StopMove();
        }

        public override void OnFixedUpdate()
        {
            if (_isDashStarted && ObjectContext.IsDashing) return;
            StateMachine.ChangeState(BossAIState.Spacing);
        }
        #endregion
    }
}
