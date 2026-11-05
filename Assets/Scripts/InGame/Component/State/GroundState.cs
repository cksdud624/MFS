using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.State
{
    public class GroundState : ObjectStateBase
    {
        #region Init
        public GroundState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            ObjectContext.SetJumpCount(GroundJumpCount);
            InputContext.OnMove += OnMove;
            InputContext.OnMove += OnMoveAnimation;
            InputContext.OnJump += OnJump;
            SyncMove();
            OnMoveAnimation(InputContext.MoveDirection);
        }

        public override void OnExit()
        {
            InputContext.OnMove -= OnMove;
            InputContext.OnMove -= OnMoveAnimation;
            InputContext.OnJump -= OnJump;
        }
        #endregion

        #region Event Handlers
        private void OnMoveAnimation(Vector2 moveDirection)
        {
            ObjectContext.SetAnimation(moveDirection.x != 0f ? AnimationType.Move : AnimationType.Idle);
        }

        private void OnJump()
        {
            if (ObjectContext.SetJumpCount(ObjectContext.JumpCount - 1))
            {
                ObjectContext.SetJumpVelocity(JumpPower);
                ObjectContext.SetAnimation(AnimationType.Jump, OnJumpAnimationEnd);
            }
        }
        #endregion
    }
}
