using Common.Template.FSM;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>전투가 시작되기 전. 제자리에 서서 Engage를 기다리고, 불리면 다음 상태(쉬기)로 넘어간다. 등장 연출은 이야기의 시나리오가 맡는다</summary>
    public class BossIntroState : BossStateBase
    {
        #region Fields
        private readonly BossState _nextState;
        #endregion

        #region Init
        public BossIntroState(StateMachine<BossState> stateMachine, ControllerBossAI controller, BossState nextState)
            : base(stateMachine, controller)
        {
            _nextState = nextState;
        }
        #endregion

        #region LifeCycle
        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (!Controller.IsEngaged) return;

            StateMachine.ChangeState(_nextState);
        }
        #endregion
    }
}
