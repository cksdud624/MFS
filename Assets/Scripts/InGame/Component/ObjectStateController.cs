using Common;
using Common.Template.FSM;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
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

        public async UniTask Init(InputContext inputContext, ObjectContext objectContext)
        {
            _inputContext = inputContext;
            _objectContext = objectContext;
            _objectContext.OnGroundedChanged += OnGroundedChanged;
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
                    //공격 중에 대시를 넣으면 대시로 캔슬된다
                    _dashStack--;
                    _objectContext.SetActionType(ActionType.Dash);
                    _objectContext.RequestDashRestart();
                    return;
            }

            _dashStack--;
            _objectContext.SetActionType(ActionType.Dash);
            _stateMachine.ChangeState(FSMState.Action);
        }

        private void OnAttack()
        {
            switch (_stateMachine.CurrentKey)
            {
                //대시나 이전 공격이 끝나기 전에는 새 공격을 받지 않는다
                case FSMState.Action:
                case FSMState.Damage:
                case FSMState.Event:
                    return;
            }

            _objectContext.SetActionType(ActionType.Attack);
            _stateMachine.ChangeState(FSMState.Action);
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindFixedUpdate(this);
            if (_objectContext != null)
                _objectContext.OnGroundedChanged -= OnGroundedChanged;
            if (_inputContext != null)
            {
                _inputContext.OnDash -= OnDash;
                _inputContext.OnAttack -= OnAttack;
            }
        }
    }
}
