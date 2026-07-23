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
        private StateMachine<FSMState> _stateMachine;
        private ObjectContext _objectContext;

        public async UniTask Init(InputContext inputContext, ObjectContext objectContext)
        {
            _objectContext = objectContext;
            _objectContext.OnGroundedChanged += OnGroundedChanged;

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

        public void OnFixedUpdate() => _stateMachine.OnFixedUpdate();

        private void OnGroundedChanged(bool grounded)
        {
            _stateMachine.ChangeState(grounded ? FSMState.Ground : FSMState.Air);
        }

        private void OnDestroy()
        {
            Global.Instance?.UnBindFixedUpdate(this);
            if (_objectContext != null)
                _objectContext.OnGroundedChanged -= OnGroundedChanged;
        }
    }
}
