using Common;
using Common.Template.FSM;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using InGame.Component.State;
using InGame.Context;
using UnityEngine;
using FSMState = Common.GameDefine.FSMState;

namespace InGame.Component
{
    public class ObjectStateController : MonoBehaviour, IFixedUpdateable
    {
        //테이블로 차후 관리해야하는 부분
        private const int MaxDashStack = 1;
        private const float DashChargeInterval = 6f;

        private StateMachine<FSMState> _stateMachine;
        private InputContext _inputContext;
        private ObjectContext _objectContext;

        private int _dashStack = MaxDashStack;
        private float _dashChargeTimer;

        public async UniTask Init(InputContext inputContext, ObjectContext objectContext)
        {
            _inputContext = inputContext;
            _objectContext = objectContext;
            _objectContext.OnGroundedChanged += OnGroundedChanged;
            _inputContext.OnDash += OnDash;

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
            if (_dashStack >= MaxDashStack) return;

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
                    _dashStack--;
                    _objectContext.RequestDashRestart();
                    return;
            }
            
            _dashStack--;
            _stateMachine.ChangeState(FSMState.Action);
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindFixedUpdate(this);
            if (_objectContext != null)
                _objectContext.OnGroundedChanged -= OnGroundedChanged;
            if (_inputContext != null)
                _inputContext.OnDash -= OnDash;
        }
    }
}
