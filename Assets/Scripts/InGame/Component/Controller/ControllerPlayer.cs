using InGame.Input;
using InGame.Context;
using UnityEngine;
using UnityEngine.InputSystem;
using static Common.GameDefine;

namespace InGame.Component.Controller
{
    public class ControllerPlayer : ControllerBase
    {
        private PlayerInputAction _inputAction;

        public override void Init(InputContext inputContext, ObjectContext objectContext)
        {
            base.Init(inputContext, objectContext);
            _inputAction = new PlayerInputAction();
            _inputAction.Enable();

            _inputAction.Player.Move.performed += OnMove;
            _inputAction.Player.Move.canceled += OnMove;
            _inputAction.Player.Jump.performed += OnJump;
            _inputAction.Player.Dash.performed += OnDash;

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public override void Dispose()
        {
            base.Dispose();
            if(_inputAction == null) return;

            _inputAction.Disable();
            _inputAction.Player.Move.performed -= OnMove;
            _inputAction.Player.Move.canceled -= OnMove;
            _inputAction.Player.Jump.performed -= OnJump;
            _inputAction.Player.Dash.performed -= OnDash;
            _inputAction.Dispose();
            _inputAction = null;
        }

        #region Events
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
        #endregion

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
