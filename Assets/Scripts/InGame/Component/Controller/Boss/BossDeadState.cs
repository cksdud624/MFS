using Common.Template.FSM;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 쓰러졌다. 마지막 상태라 다른 상태로 넘어가지 않는다. 쓰러지는 모션과 전투 종료는 오브젝트(Dead 상태)와 BossBattleController가 맡는다
    /// </summary>
    public class BossDeadState : BossStateBase
    {
        #region Init
        public BossDeadState(StateMachine<BossState> stateMachine, ControllerBossAI controller)
            : base(stateMachine, controller)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            base.OnEnter();
            Controller.StopMove();
        }
        #endregion
    }
}
