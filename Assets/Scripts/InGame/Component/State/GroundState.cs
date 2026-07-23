using Common.Template.FSM;
using InGame.Context;
using FSMState = Common.GameDefine.FSMState;

namespace InGame.Component.State
{
    public class GroundState : ObjectStateBase
    {
        private readonly float _jumpPower = 5f;

        public GroundState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }

        public override void OnEnter()
        {
            InputContext.OnMove += OnMove;
            InputContext.OnJump += OnJump;
        }

        public override void OnExit()
        {
            InputContext.OnMove -= OnMove;
            InputContext.OnJump -= OnJump;
        }

        private void OnJump()
        {
            ObjectContext.SetJumpVelocity(_jumpPower);
        }
    }
}
