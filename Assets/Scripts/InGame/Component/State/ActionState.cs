using System.Collections.Generic;
using Common;
using Common.Template.FSM;
using Generated.Table;
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
        //이번 단계에서 켜야 하는 판정 박스와, 각각 한 번이라도 판정을 냈는지
        private readonly List<AttackHitBoxData> _hitBoxes = new();
        private readonly List<bool> _hitBoxFired = new();
        //이펙트는 첫 판정에 맞춰 한 번만 띄운다. 번호가 0이면 이 커맨드는 이펙트를 쓰지 않는다
        private int _effectVariant;
        private float _effectDelay;
        private bool _effectPlayed;
        //이번 프레임에 다음 커맨드로 이어졌는지. 이어졌으면 Action 상태를 빠져나가지 않는다
        private bool _attackRestarted;

        public override void OnEnter()
        {
            ObjectContext.OnDashRestart += HandleDashRestart;
            ObjectContext.OnAttackRestart += HandleAttackRestart;

            if (ObjectContext.ActionType is ActionType.Attack)
                ApplyAttack();
            else
                ApplyDash();
        }

        public override void OnExit()
        {
            ObjectContext.OnDashRestart -= HandleDashRestart;
            ObjectContext.OnAttackRestart -= HandleAttackRestart;
            ObjectContext.SetDashing(false);
        }

        private void HandleDashRestart()
        {
            ApplyDash();
        }

        private void HandleAttackRestart()
        {
            ApplyAttack();
            _attackRestarted = true;
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

        /// <summary>
        /// 제자리에서 휘두른다. 어떤 애니메이션을 얼마나, 어떤 판정 박스로 휘두를지는
        /// 컨트롤러가 정해서 넘겨준 커맨드가 들고 있다.
        /// </summary>
        private void ApplyAttack()
        {
            _elapsed = 0f;

            var attackCommand = ObjectContext.AttackCommand;

            ObjectContext.SetMoveVelocity(0f);
            ObjectContext.SetAnimation(AnimationType.Attack, attackCommand?.Animation ?? 0);
            ObjectContext.StartAttack();

            LoadHitBoxes(attackCommand);
        }

        /// <summary>커맨드에 적힌 판정 박스를 미리 꺼내둔다. 켜는 시점과 유지시간은 각 박스가 들고 있다</summary>
        private void LoadHitBoxes(AttackCommandData attackCommand)
        {
            _hitBoxes.Clear();
            _hitBoxFired.Clear();
            _effectPlayed = false;
            _effectDelay = 0f;
            _effectVariant = attackCommand?.Effect ?? 0;

            if (attackCommand == null) return;

            var record = Global.Instance.TableManager.AttackHitBoxRecord;
            foreach (long hitBoxId in attackCommand.AttackHitBox)
            {
                var hitBox = record.GetRecord(hitBoxId);
                if (hitBox == null) continue;

                //이펙트는 가장 먼저 나가는 판정에 맞춘다. 판정이 없는 커맨드면 시작하자마자 띄운다
                if (_hitBoxes.Count == 0 || hitBox.StartTime < _effectDelay)
                    _effectDelay = hitBox.StartTime;

                _hitBoxes.Add(hitBox);
                _hitBoxFired.Add(false);
            }
        }

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;

            //대시로 캔슬되면 ActionType이 바뀌므로 매번 지금 무엇을 하는 중인지 보고 판단한다
            if (ObjectContext.ActionType is ActionType.Attack)
            {
                UpdateAttack();

                if (_elapsed < (ObjectContext.AttackCommand?.AttackTime ?? 0f)) return;

                //한 단계가 끝났다. 예약된 입력이 있으면 컨트롤러가 여기서 다음 단계를 이어붙인다
                _attackRestarted = false;
                ObjectContext.EndAttack();
                if (_attackRestarted) return;
            }
            else if (_elapsed < DashDuration) return;

            StateMachine.ChangeState(ObjectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }

        /// <summary>때가 된 판정 박스를 켜고 이펙트를 띄운다</summary>
        private void UpdateAttack()
        {
            if (!_effectPlayed && _effectVariant > 0 && _elapsed >= _effectDelay)
            {
                _effectPlayed = true;
                //방향을 넘기지 않으면 캐릭터가 보는 방향(좌/우)을 그대로 쓴다
                ObjectContext.PlayEffect(EffectType.Attack, _effectVariant);
            }

            for (int i = 0; i < _hitBoxes.Count; i++)
            {
                var hitBox = _hitBoxes[i];
                if (_elapsed < hitBox.StartTime) continue;
                //유지시간이 지났으면 그만둔다. 유지시간이 0이어도 켜지는 순간 한 번은 내보낸다
                if (_hitBoxFired[i] && _elapsed >= hitBox.StartTime + hitBox.Duration) continue;

                _hitBoxFired[i] = true;
                ObjectContext.RequestAttackHit(hitBox);
            }
        }
    }
}
