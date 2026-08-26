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
            _inputContext.OnDash += OnDash;
            _inputContext.OnAttack += OnAttack;

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
            var attackCommand = FindAttackCommand(attackButton);
            if (attackCommand == null)
            {
                ResetAttackCommand();
                return;
            }

            //TODO : 커맨드 확인용. 콤보가 자리잡으면 지운다
            Debug.Log($"공격 커맨드 : {_attackCommand} + {attackButton} → {attackCommand.Command} (Id {attackCommand.Id})");

            _attackCommand = attackCommand.Command;
            _attackCommandTimer = 0f;
            _objectContext.SetAttackCommand(attackCommand);
            _objectContext.SetActionType(ActionType.Attack);

            //이미 공격 중이면 상태를 다시 들어가지 않고 다음 단계로 이어붙인다
            if (_stateMachine.CurrentKey == FSMState.Action)
                _objectContext.RequestAttackRestart();
            else
                _stateMachine.ChangeState(FSMState.Action);
        }

        /// <summary>
        /// 쌓인 커맨드 뒤에 누른 버튼을 붙여서 찾는다.
        /// 콤보가 끝까지 갔거나 없는 조합이면 그 입력은 그냥 버린다. 커맨드는 처음으로 돌아간다.
        /// </summary>
        private AttackCommandData FindAttackCommand(int attackButton)
        {
            var record = Global.Instance.TableManager.AttackCommandRecord;
            var next = record.GetCommand(_objectContext.ObjectData, _attackCommand + attackButton);
            if (next == null) return null;

            //이어지는 단계가 히트를 요구하면 직전 공격이 맞았는지 본다.
            //첫 단계는 직전 공격이 없으므로 따지지 않는다
            if (_attackCommand.Length > 0 && next.IsHitRequired && !_objectContext.IsAttackHit)
                return null;

            return next;
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindFixedUpdate(this);
            if (_objectContext != null)
            {
                _objectContext.OnGroundedChanged -= OnGroundedChanged;
                _objectContext.OnAttackEnd -= OnAttackEnd;
            }
            if (_inputContext != null)
            {
                _inputContext.OnDash -= OnDash;
                _inputContext.OnAttack -= OnAttack;
            }
        }
    }
}
