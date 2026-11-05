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
            _inputAction.Dispose();
            _inputAction = null;
        }
        #endregion

        #region Event Handlers
        private void OnMove(InputAction.CallbackContext context)
        {
            Vector2 moveDirection = context.ReadValue<Vector2>();
            InputContext.NotifyMove(moveDirection);

            if (moveDirection.x > 0f)
                ObjectContext.SetDirection(Direction.Right);
            else if (moveDirection.x < 0f)
                ObjectContext.SetDirection(Direction.Left);
        }

        private void OnJump(InputAction.CallbackContext context) => InputContext.NotifyJump();

        private void OnDash(InputAction.CallbackContext context) => InputContext.NotifyDash();

        //공격 버튼이 늘어나면 버튼마다 자기 번호를 넘긴다
        private void OnAttack(InputAction.CallbackContext context) => InputContext.NotifyAttack(AttackButtonDefault);

        private void OnCounter(InputAction.CallbackContext context) => InputContext.NotifyCounter();
        #endregion
    }
}
