using System.Collections.Generic;
using Common;
using Common.Template.FSM;
using Common.Template.Interface;
using Generated.Table;
using InGame.Component.State;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    public class ObjectStateController : MonoBehaviour, IFixedUpdateable
    {
        #region Fields
        private StateMachine<FSMState> _stateMachine;
        private InputContext _inputContext;
        private ObjectContext _objectContext;

        private int _dashStack = DashMaxStack;
        private float _dashChargeTimer;

        private int _counterStack = CounterMaxStack;
        private float _counterChargeTimer;
        //카운터에 성공했을 때 나갈 반격. 없는 오브젝트는 카운터 자체를 쓸 수 없다
        private IReadOnlyList<AttackCommandData> _counterAttackCommands;
        private bool CanCounter => _counterAttackCommands is { Count: > 0 };
        //반격 중인지. 반격은 끝날 때까지 맞지 않고, 대시나 카운터로 끊기지도 않는다
        private bool IsCounterAttacking =>
            _stateMachine.CurrentKey == FSMState.Action && _objectContext.ActionType is ActionType.CounterAttack;

        //지금까지 쌓인 공격 커맨드. 공격 버튼을 누를 때마다 그 번호가 뒤에 붙는다 ("1" → "11" → "111")
        private string _attackCommand = string.Empty;
        //마지막 공격이 끝난 뒤 흐른 시간. 유예 시간을 넘기면 커맨드를 처음으로 되돌린다
        private float _attackCommandTimer;
        //공격 중에 들어온 재입력. 지금 단계가 끝날 때 다음 단계로 이어붙인다. 0이면 예약 없음
        private int _bufferedAttackButton;
        #endregion

        #region Init
        public void Init(InputContext inputContext, ObjectContext objectContext)
        {
            _inputContext = inputContext;
            _objectContext = objectContext;
            //반격은 테이블 값이라 도중에 바뀌지 않으므로 처음에 한 번만 찾아둔다
            _counterAttackCommands = Global.Instance.TableManager.AttackCommandRecord
                .GetCommands(objectContext.ObjectData, CounterAttackCommand);

            _stateMachine = new StateMachine<FSMState>();
            _stateMachine.AddState(FSMState.Ground, new GroundState(_stateMachine, inputContext, objectContext));
            _stateMachine.AddState(FSMState.Air, new AirState(_stateMachine, inputContext, objectContext));
            _stateMachine.AddState(FSMState.Action, new ActionState(_stateMachine, inputContext, objectContext));
            _stateMachine.AddState(FSMState.Damage, new DamageState(_stateMachine, inputContext, objectContext));
            _stateMachine.AddState(FSMState.Event, new EventState(_stateMachine, inputContext, objectContext));
            _stateMachine.ChangeState(objectContext.IsGrounded ? FSMState.Ground : FSMState.Air);

            //핸들러가 상태 머신을 바로 보므로 첫 상태에 들어간 뒤에 구독한다
            _objectContext.OnGroundedChanged += OnGroundedChanged;
            _objectContext.OnAttackEnd += OnAttackEnd;
            _objectContext.OnDamage += OnDamage;
            _objectContext.OnInEventChanged += OnInEventChanged;
            _inputContext.OnDash += OnDash;
            _inputContext.OnAttack += OnAttack;
            _inputContext.OnCounter += OnCounter;
            Global.Instance.BindFixedUpdate(this);
        }
        #endregion

        #region LifeCycle
        public void OnFixedUpdate()
        {
            UpdateDashCharge();
            UpdateCounterCharge();
            UpdateAttackCommand();
            _stateMachine.OnFixedUpdate();
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindFixedUpdate(this);
            if (_objectContext != null)
            {
                _objectContext.OnGroundedChanged -= OnGroundedChanged;
                _objectContext.OnAttackEnd -= OnAttackEnd;
                _objectContext.OnDamage -= OnDamage;
                _objectContext.OnInEventChanged -= OnInEventChanged;
            }
            if (_inputContext != null)
            {
                _inputContext.OnDash -= OnDash;
                _inputContext.OnAttack -= OnAttack;
                _inputContext.OnCounter -= OnCounter;
            }
        }
        #endregion

        #region Event Handlers
        private void OnGroundedChanged(bool grounded)
        {
            if (_stateMachine.CurrentKey >= FSMState.Action) return;
            _stateMachine.ChangeState(grounded ? FSMState.Ground : FSMState.Air);
        }

        private void OnDash()
        {
            if (_dashStack <= 0) return;
            //반격을 대시로 끊으면 반격 중의 무적까지 같이 풀리므로 받지 않는다
            if (IsCounterAttacking) return;
            switch (_stateMachine.CurrentKey)
            {
                case FSMState.Damage:
                case FSMState.Event:
                    return;
                case FSMState.Action:
                    //공격 중에 대시를 넣으면 대시로 캔슬된다. 콤보도 거기서 끊긴다
                    _dashStack--;
                    ResetAttackCommand();
                    _objectContext.SetActionType(ActionType.Dash);
                    _objectContext.RequestDashRestart();
                    return;
            }

            _dashStack--;
            ResetAttackCommand();
            _objectContext.SetActionType(ActionType.Dash);
            _stateMachine.ChangeState(FSMState.Action);
        }

        private void OnAttack(int attackButton)
        {
            switch (_stateMachine.CurrentKey)
            {
                case FSMState.Damage:
                case FSMState.Event:
                    return;
                case FSMState.Action:
                    //대시 중에는 받지 않는다.
                    //공격 중 재입력은 지금 단계가 끝날 때 이어붙이도록 예약만 해둔다.
                    //반격 중에 누르면 반격이 끝날 때 콤보 첫 단계로 이어진다
                    if (_objectContext.ActionType is ActionType.Attack or ActionType.CounterAttack)
                        _bufferedAttackButton = attackButton;
                    return;
            }

            TryStartAttack(attackButton);
        }

        /// <summary>
        /// 맞았다. Damage는 Ground, Air, Action보다 우선하므로 무엇을 하던 중이든 끊고 들어간다.
        /// 어떻게 받았는지는 때린 쪽이 히트로 셀지 정할 수 있도록 결과로 남긴다.
        /// 결과를 남기지 않고 빠져나가면 맞지 않은 것(Ignored)이 된다
        /// </summary>
        private void OnDamage(Direction attackerSide, bool counterable)
        {
            if (_stateMachine.CurrentKey == FSMState.Event) return;

            //이미 Damage 상태면 상태가 알아서 시간을 다시 세므로 맞았다는 것만 남긴다
            if (_stateMachine.CurrentKey == FSMState.Damage)
            {
                _objectContext.ReportDamageResult(DamageResult.Hit);
                return;
            }

            //카운터 대기 중이면 맞지 않고 받아내서 공격한 쪽으로 반격한다.
            //반격은 받아내지 못하고 그대로 맞는다. 받아내면 양쪽이 서로 반격을 주고받으며 끝나지 않는다
            if (_objectContext.IsCountering && counterable)
            {
                _objectContext.ReportDamageResult(DamageResult.Countered);
                StartCounterAttack(attackerSide);
                return;
            }

            //반격 중에는 맞지 않는다. 받아낸 공격과 같이 들어온 판정(마법+기계)이나
            //다른 적의 공격에 반격이 끊기지 않도록 반격이 끝날 때까지 받아내기만 한다
            if (IsCounterAttacking) return;

            //맞으면 콤보도 끊긴다. 예약해둔 공격이 피격이 끝난 뒤에 튀어나오지 않도록 같이 비운다
            ResetAttackCommand();
            _objectContext.ReportDamageResult(DamageResult.Hit);
            _stateMachine.ChangeState(FSMState.Damage);
        }

        /// <summary>
        /// 카운터 대기에 들어간다. 카운터는 Action 단계라 Ground, Air에서는 물론이고
        /// 같은 단계인 대시, 공격 도중에도 끊고 들어갈 수 있다. Damage, Event, 반격 중에는 받지 않는다
        /// </summary>
        private void OnCounter()
        {
            //반격이 없으면 받아내기만 하는 무적 구간이 되므로 대기에 들어가지 않는다
            if (!CanCounter || _counterStack <= 0) return;
            //반격을 카운터 대기로 끊으면 반격이 나가다 마므로 받지 않는다
            if (IsCounterAttacking) return;
            switch (_stateMachine.CurrentKey)
            {
                case FSMState.Damage:
                case FSMState.Event:
                    return;
                case FSMState.Action:
                    //대시나 공격 중이면 상태를 다시 들어가지 않고 카운터 대기로 갈아탄다. 콤보도 거기서 끊긴다
                    _counterStack--;
                    ResetAttackCommand();
                    _objectContext.SetActionType(ActionType.Counter);
                    _objectContext.RequestCounterRestart();
                    return;
            }

            _counterStack--;
            ResetAttackCommand();
            _objectContext.SetActionType(ActionType.Counter);
            _stateMachine.ChangeState(FSMState.Action);
        }

        /// <summary>
        /// 연출이 시작되면 무엇을 하던 중이든 끊고 Event로 들어가고, 끝나면 Ground나 Air로 돌아간다.
        /// Event는 가장 높은 단계라 연출 중에는 입력도 피격도 받지 않는다
        /// </summary>
        private void OnInEventChanged(bool inEvent)
        {
            if (inEvent)
            {
                //연출이 끝난 뒤에 예약해둔 공격이 튀어나오지 않도록 콤보를 비운다
                ResetAttackCommand();
                _stateMachine.ChangeState(FSMState.Event);
                return;
            }

            if (_stateMachine.CurrentKey != FSMState.Event) return;
            _stateMachine.ChangeState(_objectContext.IsGrounded ? FSMState.Ground : FSMState.Air);
        }

        /// <summary>공격 한 단계가 끝났다. 예약해둔 입력이 있으면 여기서 다음 단계로 이어붙인다</summary>
        private void OnAttackEnd()
        {
            if (_bufferedAttackButton == 0) return;

            int attackButton = _bufferedAttackButton;
            _bufferedAttackButton = 0;
            TryStartAttack(attackButton);
        }
        #endregion

        #region Private
        private void UpdateDashCharge()
        {
            if (_dashStack >= DashMaxStack) return;

            _dashChargeTimer += Time.fixedDeltaTime;
            if (_dashChargeTimer < DashChargeInterval) return;

            Debug.Log($"스택 충전 : {_dashChargeTimer}");
            _dashChargeTimer = 0f;
            _dashStack++;
        }

        private void UpdateCounterCharge()
        {
            if (_counterStack >= CounterMaxStack) return;

            _counterChargeTimer += Time.fixedDeltaTime;
            if (_counterChargeTimer < CounterChargeInterval) return;

            _counterChargeTimer = 0f;
            _counterStack++;
        }

        /// <summary>공격이 끝난 뒤 한동안 다음 입력이 없으면 쌓인 커맨드를 처음으로 되돌린다</summary>
        private void UpdateAttackCommand()
        {
            if (_attackCommand.Length == 0) return;

            //공격이 이어지는 동안에는 유예 시간을 세지 않는다
            if (_stateMachine.CurrentKey == FSMState.Action && _objectContext.ActionType is ActionType.Attack)
            {
                _attackCommandTimer = 0f;
                return;
            }

            _attackCommandTimer += Time.fixedDeltaTime;
            if (_attackCommandTimer < AttackCommandResetTime) return;

            ResetAttackCommand();
        }

        private void ResetAttackCommand()
        {
            _attackCommand = string.Empty;
            _attackCommandTimer = 0f;
            _bufferedAttackButton = 0;
        }

        /// <summary>
        /// 받아낸 공격에 반격한다. 공격한 쪽으로 돌아선 뒤 카운터 대기를 끊고 반격을 이어붙인다.
        /// 반격이 없는 오브젝트는 대기에 들어가지 못하므로 여기서는 반격이 있다고 본다.
        /// 반격은 콤보가 아니므로 쌓인 커맨드에는 남기지 않는다
        /// </summary>
        private void StartCounterAttack(Direction attackerSide)
        {
            //공격이 시작되면 방향이 잠기므로 반격을 걸기 전에 돌려둔다
            _objectContext.SetDirection(attackerSide);
            _objectContext.SetAttackCommands(_counterAttackCommands);
            _objectContext.SetActionType(ActionType.CounterAttack);
            _objectContext.NotifyCounterSuccess();
        }

        /// <summary>쌓인 커맨드에 누른 버튼을 이어붙여 나갈 공격을 정한다</summary>
        private void TryStartAttack(int attackButton)
        {
            var attackCommands = FindAttackCommand(attackButton);
            if (attackCommands == null)
            {
                ResetAttackCommand();
                return;
            }

            //TODO : 커맨드 확인용. 콤보가 자리잡으면 지운다
            Debug.Log($"공격 커맨드 : {_attackCommand} + {attackButton} → {attackCommands[0].Command} ({attackCommands.Count}종)");

            _attackCommand = attackCommands[0].Command;
            _attackCommandTimer = 0f;
            _objectContext.SetAttackCommands(attackCommands);
            _objectContext.SetActionType(ActionType.Attack);

            //이미 공격 중이면 상태를 다시 들어가지 않고 다음 단계로 이어붙인다
            if (_stateMachine.CurrentKey == FSMState.Action)
                _objectContext.RequestAttackRestart();
            else
                _stateMachine.ChangeState(FSMState.Action);
        }

        /// <summary>
        /// 쌓인 커맨드 뒤에 누른 버튼을 붙여서 찾는다. 마법과 기계가 한 세트로 나오므로 목록으로 받는다.
        /// 콤보가 끝까지 갔거나 없는 조합이면 그 입력은 그냥 버린다. 커맨드는 처음으로 돌아간다.
        /// </summary>
        private IReadOnlyList<AttackCommandData> FindAttackCommand(int attackButton)
        {
            var record = Global.Instance.TableManager.AttackCommandRecord;
            var next = record.GetCommands(_objectContext.ObjectData, _attackCommand + attackButton);
            if (next == null || next.Count == 0) return null;

            //이어지는 단계가 히트를 요구하면 직전 공격이 맞았는지 본다.
            //타입이 하나라도 요구하면 세트로 막는다. 첫 단계는 직전 공격이 없으므로 따지지 않는다
            if (_attackCommand.Length > 0 && !_objectContext.IsAttackHit && IsHitRequired(next))
                return null;

            return next;
        }

        private static bool IsHitRequired(IReadOnlyList<AttackCommandData> attackCommands)
        {
            foreach (var attackCommand in attackCommands)
            {
                if (attackCommand.IsHitRequired)
                    return true;
            }
            return false;
        }
        #endregion
    }
}
