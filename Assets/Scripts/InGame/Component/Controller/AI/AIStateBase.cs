using Common.Template.FSM;
using InGame.Context;
using static Common.GameDefine;

namespace InGame.Component.Controller.AI
{
    public abstract class AIStateBase : IState
    {
        #region Fields
        protected readonly StateMachine<AIState> StateMachine;
        protected readonly InputContext InputContext;
        protected readonly ObjectContext ObjectContext;
        protected readonly ControllerAI Controller;
        #endregion

        #region Init
        protected AIStateBase(StateMachine<AIState> stateMachine, InputContext inputContext, ObjectContext objectContext, ControllerAI controller)
        {
            StateMachine = stateMachine;
            InputContext = inputContext;
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

        #region Protected
        /// <summary>탐지 범위 안에 대상이 있으면 추격 상태로 전환</summary>
        protected bool TryChangeToChase()
        {
            if (!Controller.TryGetTargetDistance(out float distance)) return false;
            if (distance > DetectRange) return false;

            StateMachine.ChangeState(AIState.Chase);
            return true;
        }

        /// <summary>해당 방향으로 계속 이동할 수 있는지 (벽 / 낭떠러지)</summary>
        protected bool CanMove(float directionX)
        {
            return !Controller.IsWallAhead(directionX) && !Controller.IsLedgeAhead(directionX);
        }
        #endregion
    }
}
