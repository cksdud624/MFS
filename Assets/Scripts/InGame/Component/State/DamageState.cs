using Common.Template.FSM;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.State
{
    /// <summary>
    /// 맞고 굳어 있는 상태. Ground &lt; Air &lt; Action &lt; Damage 순으로 우선한다.
    /// 이동, 점프, 대시, 공격 같은 일반 입력은 받지 않고, 몸은 중력과 공중 관성만 받은 채로 멈춰간다.
    /// 굳는 시간은 맞은 공격이 정한다 (ObjectContext.StaggerDuration). 그 시간이 지나면 Ground나 Air로 돌아간다
    /// </summary>
    public class DamageState : ObjectStateBase
    {
        #region Fields
        private float _elapsed;
        //이번에 굳어 있을 시간. 굳은 채로 또 맞으면 다시 정한다
        private float _duration;
        #endregion

        #region Init
        public DamageState(StateMachine<FSMState> stateMachine, InputContext inputContext, ObjectContext objectContext)
            : base(stateMachine, inputContext, objectContext)
        {
        }
        #endregion

        #region LifeCycle
        public override void OnEnter()
        {
            _elapsed = 0f;
            _duration = ObjectContext.StaggerDuration;
            //굳어 있는 중에 또 맞으면 굳는 시간을 다시 정한다
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

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;
            if (_elapsed < _duration) return;

            StateMachine.ChangeState(ObjectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 굳어 있는 중에 또 맞았다. 피격 모션을 처음부터 다시 틀고 굳는 시간을 다시 정한다.
        /// 남은 시간보다 짧게 굳히는 공격이면 남은 시간을 그대로 둔다. 약한 공격을 맞아서 일찍 풀려나면 안 된다
        /// </summary>
        private void HandleDamageRestart(Direction attackerSide, bool counterable)
        {
            //굳히지 않는 피격은 모션도 시간도 건드리지 않는다 (경직이 없는 공격이거나, 굳어 있는 도중에 경직 면역이 걸렸다)
            float stagger = ObjectContext.StaggerDuration;
            if (stagger <= 0f) return;

            _duration = Mathf.Max(_duration - _elapsed, stagger);
            _elapsed = 0f;
            ObjectContext.SetAnimation(AnimationType.Damage);
        }
        #endregion
    }
}
