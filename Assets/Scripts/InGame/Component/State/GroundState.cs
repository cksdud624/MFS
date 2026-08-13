using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.State
{
    public class GroundState : ObjectStateBase
    {
        public GroundState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }

        public override void OnEnter()
        {
            ObjectContext.SetJumpCount(GroundJumpCount);
            InputContext.OnMove += OnMove;
            InputContext.OnMove += OnMoveAnimation;
            InputContext.OnJump += OnJump;
            OnMoveAnimation(InputContext.MoveDirection);
        }

        public override void OnExit()
        {
            InputContext.OnMove -= OnMove;
            InputContext.OnMove -= OnMoveAnimation;
            InputContext.OnJump -= OnJump;
        }

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
    }
}
