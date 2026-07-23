using Common.Template.FSM;
using InGame.Context;
using FSMState = Common.GameDefine.FSMState;

namespace InGame.Component.State
{
    public class EventState : ObjectStateBase
    {
        public EventState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
    }
}
