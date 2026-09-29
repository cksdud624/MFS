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
        //이번 단계가 끝나는 시각. 마법과 기계 중 늦게 끝나는 쪽에 맞춘다
        private float _attackTime;
        //이번 단계에서 켜야 하는 판정 박스와, 어느 타입의 판정인지, 지금 켜져 있는지
        private readonly List<AttackHitBoxData> _hitBoxes = new();
        private readonly List<AttackType> _hitBoxTypes = new();
        private readonly List<bool> _hitBoxActive = new();
        //이펙트를 쓰는 줄의 판정은 그 이펙트가 들고 다니고, 아니면 캐릭터가 직접 들고 있는다
        private readonly List<bool> _hitBoxByEffect = new();
        //이펙트는 타입마다 자기 첫 판정에 맞춰 한 번씩 띄운다. 아래 목록은 커맨드 줄 순서와 같다.
        //번호가 0인 줄은 이펙트를 쓰지 않는다
        private readonly List<float> _effectDelays = new();
        private readonly List<bool> _effectPlayed = new();
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
            ObjectContext.SetAttacking(false);
            CloseHitBoxes();
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

            //대시는 공격 잠금을 푸는 유일한 수단이다. 방향도 여기서 다시 입력에 맞춘다.
            //휘두르던 도중이면 켜둔 판정도 같이 거둔다
            ObjectContext.SetAttacking(false);
            CloseHitBoxes();
            SyncDirection();

            Vector2 moveDirection = InputContext.MoveDirection;
            Vector2 dashDirection = moveDirection.sqrMagnitude > 0f
                ? moveDirection.normalized
                : (ObjectContext.Direction == Direction.Right ? Vector2.right : Vector2.left);

            //전용 대시 클립이 없으므로 지상 대시는 이동 애니메이션으로 대신한다.
            //입력이 없어도(제자리 대시) 미끄러지는 모습이 되지 않게 이동으로 맞춘다.
            //공중은 이동 애니메이션을 쓰지 않으므로 지금 클립을 그대로 두고, 대시가 끝나면 Air 상태가 정리한다.
            //이미 이동 중이었거나 연속 대시면 클립을 처음부터 다시 틀지 않는다
            if (ObjectContext.IsGrounded && ObjectContext.Animation is not AnimationType.Move)
                ObjectContext.SetAnimation(AnimationType.Move);

            ObjectContext.SetDashVelocity(dashDirection * DashSpeed);
            ObjectContext.SetDashing(true);
            //대시가 유지되는 동안만 파티클을 뿜는다.
            //꼬리가 뒤로 끌리는 모양은 프리팹이 들고 있으므로 대시 방향을 그대로 넘긴다.
            //오프셋은 대각선 대시도 있으므로 좌우로 뒤집지 않고 대시 방향을 그대로 따라간다
            ObjectContext.PlayEffect(EffectType.Dash, 0, dashDirection * DashEffectDistance, dashDirection, DashDuration);
        }

        /// <summary>
        /// 제자리에서 휘두른다. 어떤 애니메이션을 얼마나, 어떤 판정 박스로 휘두를지는
        /// 컨트롤러가 정해서 넘겨준 커맨드가 들고 있다.
        /// 한 번의 커맨드에 마법과 기계가 같이 나가므로 줄마다 이펙트와 판정을 따로 굴린다.
        /// </summary>
        private void ApplyAttack()
        {
            _elapsed = 0f;

            var attackCommands = ObjectContext.AttackCommands;

            ObjectContext.SetMoveVelocity(0f);
            //휘두르는 동안은 제자리에 고정한다. 커맨드가 이어져도 계속 묶인 채로 간다
            ObjectContext.SetAttacking(true);
            //애니메이션은 타입별로 나뉘지 않으므로 번호를 적어둔 첫 줄의 것을 쓴다
            ObjectContext.SetAnimation(AnimationType.Attack, ResolveAnimation(attackCommands));
            ObjectContext.StartAttack();

            LoadHitBoxes(attackCommands);
        }

        private static int ResolveAnimation(IReadOnlyList<AttackCommandData> attackCommands)
        {
            if (attackCommands == null) return 0;

            foreach (var attackCommand in attackCommands)
            {
                if (attackCommand.Animation > 0)
                    return attackCommand.Animation;
            }
            return 0;
        }

        /// <summary>
        /// 커맨드에 적힌 판정 박스를 타입별로 미리 꺼내둔다.
        /// 켜는 시점과 유지시간은 각 박스가 들고 있고, 단계 길이는 늦게 끝나는 줄에 맞춘다
        /// </summary>
        private void LoadHitBoxes(IReadOnlyList<AttackCommandData> attackCommands)
        {
            //콤보로 이어질 때 앞 단계의 판정이 남아있지 않도록 먼저 전부 끈다
            CloseHitBoxes();

            _hitBoxes.Clear();
            _hitBoxTypes.Clear();
            _hitBoxActive.Clear();
            _hitBoxByEffect.Clear();
            _effectDelays.Clear();
            _effectPlayed.Clear();
            _attackTime = 0f;

            if (attackCommands == null) return;

            var record = Global.Instance.TableManager.AttackHitBoxRecord;
            foreach (var attackCommand in attackCommands)
            {
                //마법이 끝나도 기계 판정이 남아있을 수 있으므로 가장 늦게 끝나는 줄까지 기다린다
                _attackTime = Mathf.Max(_attackTime, attackCommand.AttackTime);

                var attackType = (AttackType)attackCommand.Type;
                //이펙트는 이 줄에서 가장 먼저 나가는 판정에 맞춘다. 판정이 없는 줄이면 시작하자마자 띄운다
                float effectDelay = 0f;
                bool hasHitBox = false;

                foreach (long hitBoxId in attackCommand.AttackHitBox)
                {
                    var hitBox = record.GetRecord(hitBoxId);
                    //유지시간이 없는 판정은 켤 구간이 없으므로 넘긴다
                    if (hitBox == null || hitBox.Duration <= 0f) continue;

                    if (!hasHitBox || hitBox.StartTime < effectDelay)
                        effectDelay = hitBox.StartTime;
                    hasHitBox = true;

                    _hitBoxes.Add(hitBox);
                    _hitBoxTypes.Add(attackType);
                    _hitBoxActive.Add(false);
                    _hitBoxByEffect.Add(attackCommand.Effect > 0);
                }

                _effectDelays.Add(effectDelay);
                _effectPlayed.Add(false);
            }
        }

        public override void OnFixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;

            //대시로 캔슬되면 ActionType이 바뀌므로 매번 지금 무엇을 하는 중인지 보고 판단한다
            if (ObjectContext.ActionType is ActionType.Attack)
            {
                UpdateAttack();

                if (_elapsed < _attackTime) return;

                //한 단계가 끝났다. 예약된 입력이 있으면 컨트롤러가 여기서 다음 단계를 이어붙인다
                _attackRestarted = false;
                ObjectContext.EndAttack();
                if (_attackRestarted) return;
            }
            else if (_elapsed < DashDuration) return;

            StateMachine.ChangeState(ObjectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }

        /// <summary>때가 된 판정 박스를 켜고 이펙트를 띄운다. 마법과 기계가 각자 자기 시점에 나간다</summary>
        private void UpdateAttack()
        {
            var attackCommands = ObjectContext.AttackCommands;
            for (int i = 0; attackCommands != null && i < _effectPlayed.Count; i++)
            {
                if (_effectPlayed[i]) continue;

                //번호가 0인 줄은 이펙트를 쓰지 않으므로 더 보지 않는다
                int effectVariant = attackCommands[i].Effect;
                if (effectVariant <= 0)
                {
                    _effectPlayed[i] = true;
                    continue;
                }

                if (_elapsed < _effectDelays[i]) continue;

                _effectPlayed[i] = true;
                //공격 중에는 방향이 잠기므로 공격을 시작한 방향 그대로 오프셋을 맞춘다.
                //진행 방향을 넘기지 않으면 캐릭터가 보는 방향(좌/우)을 그대로 쓴다.
                //길이를 적어두면 그 시간에 딱 맞게 재생되고, 0이면 프리팹 수명대로 나간다
                //이 줄의 판정은 여기서 띄운 이펙트가 들고 다닌다
                ObjectContext.PlayEffect(EffectType.Attack, effectVariant,
                    ObjectContext.GetDirectionalOffset(attackCommands[i].EffectOffset),
                    duration: attackCommands[i].EffectDuration,
                    attackType: (AttackType)attackCommands[i].Type);
            }

            for (int i = 0; i < _hitBoxes.Count; i++)
            {
                var hitBox = _hitBoxes[i];
                bool active = _elapsed >= hitBox.StartTime
                              && _elapsed < hitBox.StartTime + hitBox.Duration;
                SetHitBoxActive(i, active);
            }
        }

        /// <summary>판정 박스를 켜고 끈다. 상태가 바뀔 때만 알린다</summary>
        private void SetHitBoxActive(int index, bool active)
        {
            if (_hitBoxActive[index] == active) return;

            _hitBoxActive[index] = active;
            ObjectContext.SetAttackHitBoxActive(_hitBoxes[index], _hitBoxTypes[index], active, _hitBoxByEffect[index]);
        }

        /// <summary>켜둔 판정을 전부 끈다. 공격이 끝나거나 대시로 끊길 때 남기지 않는다</summary>
        private void CloseHitBoxes()
        {
            for (int i = 0; i < _hitBoxes.Count; i++)
                SetHitBoxActive(i, false);
        }
    }
}
