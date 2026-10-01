using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.State
{
    /// <summary>
    /// 맞고 있는 상태. Ground &lt; Air &lt; Action &lt; Damage 순으로 우선한다.
    /// 이동, 점프, 대시, 공격 같은 일반 입력은 받지 않고, 몸은 중력과 공중 관성만 받은 채로 멈춰간다.
    /// 정해진 시간이 지나면 Ground나 Air로 돌아간다
    /// </summary>
    public class DamageState : ObjectStateBase
    {
        public DamageState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }

        private float _elapsed;

        public override void OnEnter()
        {
            _elapsed = 0f;
            //이미 맞고 있는 중에 또 맞으면 시간을 처음부터 다시 센다
            ObjectContext.OnDamage += HandleDamageRestart;

            //이동 입력을 끊어두면 속도는 공중 관성대로 줄어든다
            ObjectContext.SetMoveVelocity(0f);
            ObjectContext.SetDamaged(true);
            ObjectContext.SetAnimation(AnimationType.Damage);
        }

        public override void OnExit()
        {
            ObjectContext.OnDamage -= HandleDamageRestart;
            ObjectContext.SetDamaged(false);
        }

        //또 맞았으면 피격 모션도 처음부터 다시 튼다
        private void HandleDamageRestart()
        {
            _elapsed = 0f;
            ObjectContext.SetAnimation(AnimationType.Damage);
        }

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;
            if (_elapsed < DamageDuration) return;

            StateMachine.ChangeState(ObjectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }
    }
}
