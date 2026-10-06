using System.Collections.Generic;
using Common;
using Common.Template.FSM;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Component.State;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component
{
    public class ObjectStateController : MonoBehaviour, IFixedUpdateable
    {
        private StateMachine<FSMState> _stateMachine;
        private InputContext _inputContext;
        private ObjectContext _objectContext;

        private int _dashStack = DashMaxStack;
        private float _dashChargeTimer;

        private int _counterStack = CounterMaxStack;
        private float _counterChargeTimer;

        //지금까지 쌓인 공격 커맨드. 공격 버튼을 누를 때마다 그 번호가 뒤에 붙는다 ("1" → "11" → "111")
        private string _attackCommand = string.Empty;
        //마지막 공격이 끝난 뒤 흐른 시간. 유예 시간을 넘기면 커맨드를 처음으로 되돌린다
        private float _attackCommandTimer;
        //공격 중에 들어온 재입력. 지금 단계가 끝날 때 다음 단계로 이어붙인다. 0이면 예약 없음
        private int _bufferedAttackButton;

        public async UniTask Init(InputContext inputContext, ObjectContext objectContext)
        {
            _inputContext = inputContext;
            _objectContext = objectContext;
            _objectContext.OnGroundedChanged += OnGroundedChanged;
            _objectContext.OnAttackEnd += OnAttackEnd;
            _objectContext.OnDamage += OnDamage;
            _inputContext.OnDash += OnDash;
            _inputContext.OnAttack += OnAttack;
            _inputContext.OnCounter += OnCounter;

            _stateMachine = new StateMachine<FSMState>();
            _stateMachine.AddState(FSMState.Ground, new GroundState(_stateMachine, inputContext, objectContext));
            _stateMachine.AddState(FSMState.Air, new AirState(_stateMachine, inputContext, objectContext));
            _stateMachine.AddState(FSMState.Action, new ActionState(_stateMachine, inputContext, objectContext));
            _stateMachine.AddState(FSMState.Damage, new DamageState(_stateMachine, inputContext, objectContext));
            _stateMachine.AddState(FSMState.Event, new EventState(_stateMachine, inputContext, objectContext));

            _stateMachine.ChangeState(objectContext.IsGrounded ? FSMState.Ground : FSMState.Air);

            Global.Instance.BindFixedUpdate(this);

            await UniTask.CompletedTask;
        }

        public void OnFixedUpdate()
        {
            UpdateDashCharge();
            UpdateCounterCharge();
            UpdateAttackCommand();
            _stateMachine.OnFixedUpdate();
        }

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

        private void OnGroundedChanged(bool grounded)
        {
            if (_stateMachine.CurrentKey >= FSMState.Action) return;
            _stateMachine.ChangeState(grounded ? FSMState.Ground : FSMState.Air);
        }

        private void OnDash()
        {
            if (_dashStack <= 0) return;
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
                    //공격 중 재입력은 지금 단계가 끝날 때 이어붙이도록 예약만 해둔다
                    if (_objectContext.ActionType is ActionType.Attack)
                        _bufferedAttackButton = attackButton;
                    return;
            }

            TryStartAttack(attackButton);
        }

        /// <summary>
        /// 맞았다. Damage는 Ground, Air, Action보다 우선하므로 무엇을 하던 중이든 끊고 들어간다.
        /// 이미 Damage 상태면 상태가 알아서 시간을 다시 세므로 여기서는 들어가기만 한다
        /// </summary>
        private void OnDamage()
        {
            if (_stateMachine.CurrentKey is FSMState.Event or FSMState.Damage) return;

            //카운터 대기 중이면 맞지 않고 받아낸다. 대기가 끝날 때까지는 몇 번을 맞아도 전부 받아낸다
            if (_objectContext.IsCountering)
            {
                _objectContext.NotifyCounterSuccess();
                return;
            }

            //맞으면 콤보도 끊긴다. 예약해둔 공격이 피격이 끝난 뒤에 튀어나오지 않도록 같이 비운다
            ResetAttackCommand();
            _stateMachine.ChangeState(FSMState.Damage);
        }

        /// <summary>
        /// 카운터 대기에 들어간다. 카운터는 Action 단계라 Ground, Air에서는 물론이고
        /// 같은 단계인 대시, 공격 도중에도 끊고 들어갈 수 있다. Damage, Event에서는 받지 않는다
        /// </summary>
        private void OnCounter()
        {
            if (_counterStack <= 0) return;
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

        /// <summary>공격 한 단계가 끝났다. 예약해둔 입력이 있으면 여기서 다음 단계로 이어붙인다</summary>
        private void OnAttackEnd()
        {
            if (_bufferedAttackButton == 0) return;

            int attackButton = _bufferedAttackButton;
            _bufferedAttackButton = 0;
            TryStartAttack(attackButton);
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

        private void OnDestroy()
        {
            Global.Instance?.UnBindFixedUpdate(this);
            if (_objectContext != null)
            {
                _objectContext.OnGroundedChanged -= OnGroundedChanged;
                _objectContext.OnAttackEnd -= OnAttackEnd;
                _objectContext.OnDamage -= OnDamage;
            }
            if (_inputContext != null)
            {
                _inputContext.OnDash -= OnDash;
                _inputContext.OnAttack -= OnAttack;
                _inputContext.OnCounter -= OnCounter;
            }
        }
    }
}
