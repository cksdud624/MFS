using Common.Template.FSM;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 패턴 사이에 쉰다. 쉬는 동안 대상 쪽을 보고 서 있다가, 쉬는 시간이 지나면 패턴 상태로 넘어간다.
    /// 쉬는 시간은 바로 앞 패턴이 끝까지 갔으면 그 패턴의 RecoveryDuration, 아니면 Boss 표의 IdleDuration이다
    /// </summary>
    public class BossIdleState : BossStateBase
    {
        #region Fields
        private readonly BossState _patternState;
        //이번에 쉬는 시간(초)
        private float _duration;
        #endregion

        #region Init
        public BossIdleState(StateMachine<BossState> stateMachine, ControllerBossAI controller, BossState patternState)
            : base(stateMachine, controller)
        {
            _patternState = patternState;
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            base.OnEnter();
            _duration = Controller.ConsumeNextIdleDuration();
            Controller.StopMove();
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            Controller.FaceTarget();

            //넘어가는 것은 들어온 다음 스텝부터다. 고를 패턴이 없어도 쉬기와 패턴을 한 스텝 안에서 끝없이 오가지 않는다
            if (StateTime < _duration) return;

            StateMachine.ChangeState(_patternState);
        }
        #endregion
    }
}
