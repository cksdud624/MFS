using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using FSMState = Common.GameDefine.FSMState;
using AnimationType = Common.GameDefine.AnimationType;

namespace InGame.Component.State
{
    public abstract class ObjectStateBase : IState
    {
        protected readonly StateMachine<FSMState> StateMachine;
        protected readonly InputContext InputContext;
        protected readonly ObjectContext ObjectContext;

        protected ObjectStateBase(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
        {
            StateMachine = stateMachine;
            InputContext = inputContext;
            ObjectContext = objectContext;
        }

        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() { }

        protected void OnMove(Vector2 moveDirection)
        {
            float moveSpeed = ObjectContext.ObjectData.MoveSpeed;
            float velocityX = 0f;
            if (moveDirection.x > 0f)
                velocityX = moveSpeed;
            else if (moveDirection.x < 0f)
                velocityX = -moveSpeed;

            ObjectContext.SetMoveVelocity(velocityX);
        }
        
        #region Events

        protected void OnJumpAnimationEnd()
        {
            ObjectContext.SetAnimation(AnimationType.Fly);
            Debug.Log("Jump Animation End");
        }
        #endregion
    }
}
