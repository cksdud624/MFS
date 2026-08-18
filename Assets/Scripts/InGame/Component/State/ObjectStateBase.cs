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

        /// <summary>
        /// 이동 입력을 지금 입력 상태로 다시 맞춘다.
        /// OnMove를 구독하지 않는 상태(대시 등)에 있는 동안 들어온 입력 변화는 유실되므로
        /// 이동을 처리하는 상태로 들어올 때 한 번 맞춰줘야 한다.
        /// </summary>
        protected void SyncMove() => OnMove(InputContext.MoveDirection);

        #region Events

        protected void OnJumpAnimationEnd()
        {
            ObjectContext.SetAnimation(AnimationType.Fly);
            Debug.Log("Jump Animation End");
        }
        #endregion
    }
}
