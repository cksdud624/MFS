using Common.Template.FSM;
using InGame.Context;
using FSMState = Common.GameDefine.FSMState;
using AnimationType = Common.GameDefine.AnimationType;

namespace InGame.Component.State
{
    /// <summary>
    /// 쓰러진 상태. 가장 높은 단계라 무엇을 하던 중이든 끊고 들어오고, 다시 나가지 않는다.
    /// 이동, 점프, 대시, 공격, 카운터를 받지 않고 맞지도 않는다.
    /// 쓰러지는 모션을 한 번 틀고, 모션이 끝나면 알려서 오브젝트를 없애게 한다
    /// </summary>
    public class DeadState : ObjectStateBase
    {
        #region Init
        public DeadState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            //걷던 중에 쓰러지면 미끄러지지 않도록 세운다. 공중이면 그대로 떨어진다
            ObjectContext.SetMoveVelocity(0f);
            ObjectContext.SetAnimation(AnimationType.Death, ObjectContext.EndDeath);
        }
        #endregion
    }
}
