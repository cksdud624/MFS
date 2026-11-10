using InGame.Input;
using InGame.Context;
using UnityEngine;
using UnityEngine.InputSystem;
using static Common.GameDefine;

namespace InGame.Component.Controller
{
    public class ControllerPlayer : ControllerBase
    {
        #region Fields
        private PlayerInputAction _inputAction;
        #endregion

        #region Init
        public override void Init(InGameContext inGameContext, InputContext inputContext, ObjectContext objectContext)
        {
            base.Init(inGameContext, inputContext, objectContext);
            _inputAction = new PlayerInputAction();
            _inputAction.Player.Move.performed += OnMove;
            _inputAction.Player.Move.canceled += OnMove;
            _inputAction.Player.Jump.performed += OnJump;
            _inputAction.Player.Dash.performed += OnDash;
            _inputAction.Player.Attack.performed += OnAttack;
            _inputAction.Player.Counter.performed += OnCounter;
            _inputAction.Player.Interact.performed += OnInteract;
            //콜백을 다 건 뒤에 켠다. 먼저 켜면 그 사이에 들어온 입력은 받을 곳이 없다
            _inputAction.Enable();

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        #endregion

        #region LifeCycle
        private void OnDestroy()
        {
            Dispose();
        }
        #endregion

        #region Public
        public override void Dispose()
        {
            base.Dispose();
            if(_inputAction == null) return;

            _inputAction.Disable();
            _inputAction.Player.Move.performed -= OnMove;
            _inputAction.Player.Move.canceled -= OnMove;
            _inputAction.Player.Jump.performed -= OnJump;
            _inputAction.Player.Dash.performed -= OnDash;
            _inputAction.Player.Attack.performed -= OnAttack;
            _inputAction.Player.Counter.performed -= OnCounter;
            _inputAction.Player.Interact.performed -= OnInteract;
            _inputAction.Dispose();
            _inputAction = null;
        }
        #endregion

        #region Protected
        //멈춰 있는 동안 누르거나 뗀 이동 키는 버렸으므로, 다시 켤 때 지금 눌려 있는 상태로 맞춘다.
        //이렇게 해야 키를 누른 채로 강제 이동이 끝나도 바로 이어서 걷는다
        protected override void OnResumed() => ApplyMove(_inputAction.Player.Move.ReadValue<Vector2>());
        #endregion

        #region Event Handlers
        //시나리오가 강제로 제어하는 동안에는 캐릭터를 움직이는 입력을 받지 않는다
        private void OnMove(InputAction.CallbackContext context)
        {
            if (IsSuspended) return;
            ApplyMove(context.ReadValue<Vector2>());
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (!IsSuspended)
                InputContext.NotifyJump();
        }

        private void OnDash(InputAction.CallbackContext context)
        {
            if (!IsSuspended)
                InputContext.NotifyDash();
        }

        //공격 버튼이 늘어나면 버튼마다 자기 번호를 넘긴다
        private void OnAttack(InputAction.CallbackContext context)
        {
            if (!IsSuspended)
                InputContext.NotifyAttack(AttackButtonDefault);
        }

        private void OnCounter(InputAction.CallbackContext context)
        {
            if (!IsSuspended)
                InputContext.NotifyCounter();
        }

        //말 걸기와 대사 넘기기는 게임 흐름이 받으므로 InGameContext로 보낸다.
        //캐릭터를 움직이는 입력이 아니라서 강제 제어 중에도 막지 않는다
        private void OnInteract(InputAction.CallbackContext context) => InGameContext.NotifyInteract();
        #endregion

        #region Private
        private void ApplyMove(Vector2 moveDirection)
        {
            InputContext.NotifyMove(moveDirection);

            //연출 중에는 입력으로 돌아서지 않는다. 연출이 돌려세운 방향(대화 상대 쪽)을 지킨다.
            //입력 상태는 위에서 남겨두므로 연출이 끝나면 Ground/Air가 그대로 이어받는다
            if (ObjectContext.IsInEvent) return;

            if (moveDirection.x > 0f)
                ObjectContext.SetDirection(Direction.Right);
            else if (moveDirection.x < 0f)
                ObjectContext.SetDirection(Direction.Left);
        }
        #endregion
    }
}
