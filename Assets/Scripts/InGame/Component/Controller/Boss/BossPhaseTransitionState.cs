using Common.Template.FSM;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 체력바 하나가 다 깎여서 다음 체력바로 넘어가는 중이다. Boss 표의 PhaseTransitionDuration 동안 멈춰 서서 맞지 않고, 지나면 다음 상태(쉬기)로 넘어간다.
    /// 페이즈 번호는 체력바가 넘어갈 때 ObjectContext가 이미 올려 두었으므로 여기서 바꾸지 않는다
    /// </summary>
    public class BossPhaseTransitionState : BossStateBase
    {
        #region Fields
        private readonly BossState _nextState;
        #endregion

        #region Init
        public BossPhaseTransitionState(StateMachine<BossState> stateMachine, ControllerBossAI controller, BossState nextState)
            : base(stateMachine, controller)
        {
            _nextState = nextState;
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            base.OnEnter();
            Controller.StopMove();
            //가득 찬 새 체력바를 전환 중에 깎지 못하게 한다. 플레이어가 숨을 돌리는 틈이다
            Controller.Context.SetInvincible(true);
            Controller.Context.SetPatternName(BossPhaseTransitionName);
        }

        public override void OnExit()
        {
            Controller.Context.SetInvincible(false);
            Controller.Context.SetPatternName(string.Empty);
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (StateTime < Controller.Definition.PhaseTransitionDuration) return;

            StateMachine.ChangeState(_nextState);
        }
        #endregion
    }
}
