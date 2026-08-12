using Common.Template.FSM;
using InGame.Context;
using AIState = Common.GameDefine.AIState;

namespace InGame.Component.Controller.AI
{
    public abstract class AIStateBase : IState
    {
        protected readonly StateMachine<AIState> StateMachine;
        protected readonly InputContext InputContext;
        protected readonly ObjectContext ObjectContext;
        protected readonly ControllerAI Controller;

        //테이블로 차후 관리해야하는 부분
        protected const float DetectRange = 5f;   //추격을 시작하는 거리
        protected const float LoseRange = 8f;     //추격을 포기하는 거리
        protected const float AttackRange = 0.5f; //멈춰서 공격하는 거리

        protected AIStateBase(StateMachine<AIState> stateMachine, InputContext inputContext, ObjectContext objectContext, ControllerAI controller)
        {
            StateMachine = stateMachine;
            InputContext = inputContext;
            ObjectContext = objectContext;
            Controller = controller;
        }

        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() { }

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
    }
}
