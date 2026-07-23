using Common.Template.FSM;
using InGame.Context;
using FSMState = Common.GameDefine.FSMState;

namespace InGame.Component.State
{
    public class ActionState : ObjectStateBase
    {
        public ActionState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
    }
}
