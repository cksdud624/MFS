using Common.Template.FSM;
using InGame.Context;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss
{
    public abstract class BossStateBase : IState
    {
        #region Fields
        protected readonly StateMachine<BossAIState> StateMachine;
        protected readonly ObjectContext ObjectContext;
        protected readonly ControllerBossAI Controller;
        #endregion

        #region Init
        protected BossStateBase(StateMachine<BossAIState> stateMachine, ObjectContext objectContext, ControllerBossAI controller)
        {
            StateMachine = stateMachine;
            ObjectContext = objectContext;
            Controller = controller;
        }
        #endregion

        #region LifeCycle
        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() { }
        #endregion
    }
}
