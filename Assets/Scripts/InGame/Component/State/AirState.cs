using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.State
{
    public class AirState : ObjectStateBase
    {
        #region Init
        public AirState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
        #endregion

        #region LifeCycle
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
        #endregion

        #region Event Handlers
        private void OnJump()
        {
            if (!ObjectContext.SetJumpCount(ObjectContext.JumpCount - 1)) return;

            //공중 점프는 누르고 있는 쪽으로 밀어주면서 뛴다. 누른 쪽이 없으면 보는 쪽으로 간다
            float inputX = InputContext.MoveDirection.x;
            float directionX = inputX != 0f ? Mathf.Sign(inputX)
                : ObjectContext.Direction == Direction.Right ? 1f : -1f;
            ObjectContext.RequestAirJump(directionX);
            ObjectContext.SetAnimation(AnimationType.Jump, OnJumpAnimationEnd);
        }
        #endregion
    }
}
