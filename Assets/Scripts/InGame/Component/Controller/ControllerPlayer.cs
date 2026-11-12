using InGame.Input;
using InGame.Context;
using UnityEngine;
using UnityEngine.InputSystem;
using static Common.GameDefine;
#if UNITY_EDITOR
using Common.Test;
#endif

namespace InGame.Component.Controller
{
    public class ControllerPlayer : ControllerBase
    {
        #region Fields
        private PlayerInputAction _inputAction;
#if UNITY_EDITOR
        //이동 속도 테스트 키 상태. 퀵이동(누르는 동안)이 빠르게(토글)보다 앞선다
        private bool _testFastMove;
        private bool _testQuickMove;
#endif
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
#if UNITY_EDITOR
            EditorTestKeys.OnToggleFastMove += HandleToggleFastMove;
            EditorTestKeys.OnQuickMoveChanged += HandleQuickMoveChanged;
#endif

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
#if UNITY_EDITOR
            EditorTestKeys.OnToggleFastMove -= HandleToggleFastMove;
            EditorTestKeys.OnQuickMoveChanged -= HandleQuickMoveChanged;
#endif
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

        #region Editor
#if UNITY_EDITOR
        //이동 속도 테스트 키 (빠르게 토글). 넓은 맵을 빨리 오가며 Zone 컬링을 확인할 때 쓴다
        private void HandleToggleFastMove()
        {
            _testFastMove = !_testFastMove;
            ApplyTestMoveSpeed();
        }

        //퀵이동 테스트 키. 누르고 있는 동안만 아주 빠르게 가고, 떼면 직전 속도(빠르게 또는 원래)로 돌아간다
        private void HandleQuickMoveChanged(bool held)
        {
            _testQuickMove = held;
            ApplyTestMoveSpeed();
        }

        /// <summary>
        /// 테스트 키 상태로 이동 속도 배율을 정한다. 퀵이동이 빠르게보다 앞선다.
        /// 걸음 속도는 이동 입력이 들어올 때 정해지므로, 이동 키를 누른 채로 바꿔도 바로 적용되게 지금 입력을 다시 낸다
        /// </summary>
        private void ApplyTestMoveSpeed()
        {
            float multiplier = _testQuickMove ? EditorTestKeys.QuickMoveMultiplier
                : _testFastMove ? EditorTestKeys.FastMoveMultiplier
                : 1f;
            ObjectContext.SetMoveSpeedMultiplier(multiplier);
            Debug.Log($"[테스트 키] 플레이어 이동 속도 x{multiplier}");
            if (!IsSuspended)
                ApplyMove(_inputAction.Player.Move.ReadValue<Vector2>());
        }
#endif
        #endregion
    }
}
