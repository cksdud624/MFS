using Common.Template.FSM;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 반격을 맞고 굳어 있다. 보스는 경직 면역이라 보통 공격에는 굳지 않고, 보스의 공격을 카운터로 받아낸 반격에만 굳는다.
    /// 굳는 시간은 맞은 공격의 Stagger가 정하고 오브젝트 상태(Damage)가 센다. 그래서 따로 세지 않고, 풀리면 다음 상태(쉬기)로 넘어간다
    /// </summary>
    public class BossGroggyState : BossStateBase
    {
        #region Fields
        private readonly BossState _nextState;
        #endregion

        #region Init
        public BossGroggyState(StateMachine<BossState> stateMachine, ControllerBossAI controller, BossState nextState)
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
        }

        //굳어 있는 동안은 판단이 멈춰서 불리지 않는다. 불렸다면 풀린 것이다
        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (Controller.IsStaggered) return;

            StateMachine.ChangeState(_nextState);
        }
        #endregion
    }
}
