using Common.Template.FSM;
using InGame.Context;
using FSMState = Common.GameDefine.FSMState;
using AnimationType = Common.GameDefine.AnimationType;

namespace InGame.Component.State
{
    /// <summary>
    /// 대화 같은 연출 중인 상태. 이동, 점프, 대시, 공격, 카운터를 받지 않고 맞지도 않는다.
    /// 들어가고 나오는 것은 ObjectContext.SetInEvent로 연출을 진행하는 쪽이 정한다
    /// </summary>
    public class EventState : ObjectStateBase
    {
        #region Init
        public EventState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            //걷던 중에 들어오면 미끄러지지 않도록 세운다. 공중이면 떨어지는 모션은 그대로 둔다
            ObjectContext.SetMoveVelocity(0f);
            if (ObjectContext.IsGrounded)
                ObjectContext.SetAnimation(AnimationType.Idle);
        }
        #endregion
    }
}
