using Common.Template.FSM;
using InGame.Context;
using FSMState = Common.GameDefine.FSMState;

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
            InputContext.OnMove += OnMove;
        }

        public override void OnExit()
        {
            InputContext.OnMove -= OnMove;
        }
    }
}
