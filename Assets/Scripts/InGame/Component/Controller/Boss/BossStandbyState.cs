using Common.Template.FSM;
using InGame.Context;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 전투가 시작되기 전. 제자리에 서서 기다리고 맞지 않는다.
    /// 전투는 보스가 서 있는 Zone에 플레이어가 들어와서 Zone이 잠길 때 시작되므로, 그 전에 밖에서 깎아둘 수 없게 한다
    /// </summary>
    public class BossStandbyState : BossStateBase
    {
        #region Init
        public BossStandbyState(StateMachine<BossAIState> stateMachine, ObjectContext objectContext, ControllerBossAI controller)
            : base(stateMachine, objectContext, controller)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            Controller.StopMove();
            ObjectContext.SetInvincible(true);
        }

        public override void OnExit() => ObjectContext.SetInvincible(false);
        #endregion
    }
}
