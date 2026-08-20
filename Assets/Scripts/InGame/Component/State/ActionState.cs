using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.State
{
    public class ActionState : ObjectStateBase
    {
        public ActionState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
        
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

            ObjectContext.SetDashVelocity(dashDirection * DashSpeed);
            ObjectContext.SetDashing(true);

            ObjectContext.PlayEffect(EffectType.Dash, dashDirection);
        }

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;
            if (_elapsed < DashDuration) return;
            StateMachine.ChangeState(ObjectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }
    }
}
