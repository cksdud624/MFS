using Common.Template.FSM;
using InGame.Context;
using static Common.GameDefine;

namespace InGame.Component.State
{
    public class AirState : ObjectStateBase
    {
        public AirState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }

        public override void OnEnter()
        {
            switch (StateMachine.PreviousKey)
            {
                case FSMState.Ground:
                    ObjectContext.SetJumpCount(FallJumpCount);
                    break;
            }

            InputContext.OnMove += OnMove;
            InputContext.OnJump += OnJump;
            SyncMove();

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
                ObjectContext.SetJumpVelocity(JumpPower);
                ObjectContext.SetAnimation(AnimationType.Jump, OnJumpAnimationEnd);
            }
        }
    }
}
