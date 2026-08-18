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
            //대시가 유지되는 동안만 파티클을 뿜는다
            //이펙트는 진행 방향 뒤로 끌리는 모양이라 대시 방향을 뒤집어서 넘긴다
            ObjectContext.PlayEffect(EffectType.Dash, -dashDirection, DashDuration);
        }

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;
            if (_elapsed < DashDuration) return;
            StateMachine.ChangeState(ObjectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }
    }
}
