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
        //공격 판정은 한 번만 나가야 하므로 이미 나갔는지 기억해둔다
        private bool _attackHit;

        public override void OnEnter()
        {
            ObjectContext.OnDashRestart += HandleDashRestart;

            if (ObjectContext.ActionType is ActionType.Attack)
                ApplyAttack();
            else
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
            //꼬리가 뒤로 끌리는 모양은 프리팹이 들고 있으므로 대시 방향을 그대로 넘긴다
            ObjectContext.PlayEffect(EffectType.Dash, dashDirection, DashDuration);
        }

        /// <summary>제자리에서 휘두른다. 판정은 정해진 시점에 OnFixedUpdate가 날린다</summary>
        private void ApplyAttack()
        {
            _elapsed = 0f;
            _attackHit = false;

            ObjectContext.SetMoveVelocity(0f);
            ObjectContext.SetAnimation(AnimationType.Attack);
            ObjectContext.StartAttack();
        }

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;

            //대시로 캔슬되면 ActionType이 바뀌므로 매번 지금 무엇을 하는 중인지 보고 판단한다
            if (ObjectContext.ActionType is ActionType.Attack)
            {
                if (!_attackHit && _elapsed >= AttackHitDelay)
                {
                    _attackHit = true;
                    ObjectContext.RequestAttackHit();
                }

                if (_elapsed < AttackDuration) return;
            }
            else if (_elapsed < DashDuration) return;

            StateMachine.ChangeState(ObjectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }
    }
}
