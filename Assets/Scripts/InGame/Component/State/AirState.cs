using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using FSMState = Common.GameDefine.FSMState;
using AnimationType = Common.GameDefine.AnimationType;

namespace InGame.Component.State
{
    public class AirState : ObjectStateBase
    {
        public AirState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }

        private readonly float _jumpPower = 5f;
        //테이블로 차후 관리해야하는 부분

        public override void OnEnter()
        {
            switch (StateMachine.PreviousKey)
            {
                case FSMState.Ground:
                    ObjectContext.SetJumpCount(1);
                    break;
            }

            InputContext.OnMove += OnMove;
            InputContext.OnJump += OnJump;

            if (ObjectContext.Animation is not AnimationType.Jump)
                ObjectContext.SetAnimation(AnimationType.Fly);
        }

        public override void OnExit()
        {
            InputContext.OnMove -= OnMove;
            InputContext.OnJump -= OnJump;
        }

        private void OnJump()
        {
            if (ObjectContext.SetJumpCount(ObjectContext.JumpCount - 1))
            {
                ObjectContext.SetJumpVelocity(_jumpPower);
                ObjectContext.SetAnimation(AnimationType.Jump, OnJumpAnimationEnd);
            }
        }
    }
}
