using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using Direction = Common.GameDefine.Direction;
using FSMState = Common.GameDefine.FSMState;

namespace InGame.Component.State
{
    public class ActionState : ObjectStateBase
    {
        public ActionState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
        
        //테이블로 차후 관리해야하는 부분
        private readonly float _dashSpeed = 5f;
        private readonly float _dashDuration = 0.2f;

        private float _elapsed;

        public override void OnEnter()
        {
            ObjectContext.OnDashRestart += HandleDashRestart;
            ApplyDash();
        }

        public override void OnExit()
        {
            ObjectContext.OnDashRestart -= HandleDashRestart;
            ObjectContext.SetDashing(false);
        }

        private void HandleDashRestart()
        {
            ApplyDash();
        }

        private void ApplyDash()
        {
            _elapsed = 0f;

            Vector2 moveDirection = InputContext.MoveDirection;
            Vector2 dashDirection = moveDirection.sqrMagnitude > 0f
                ? moveDirection.normalized
                : (ObjectContext.Direction == Direction.Right ? Vector2.right : Vector2.left);

            ObjectContext.SetDashVelocity(dashDirection * _dashSpeed);
            ObjectContext.SetDashing(true);
        }

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;
            if (_elapsed < _dashDuration) return;
            StateMachine.ChangeState(ObjectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }
    }
}
