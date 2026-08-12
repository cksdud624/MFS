using Common;
using Common.Template.FSM;
using Common.Template.Interface;
using InGame.Component.Controller.AI;
using InGame.Context;
using UnityEngine;
using AIState = Common.GameDefine.AIState;
using Direction = Common.GameDefine.Direction;

namespace InGame.Component.Controller
{
    /// <summary>
    /// 적(비플레이어)용 컨트롤러.
    /// 플레이어와 동일하게 InputContext로만 신호를 보내기 때문에
    /// 이후 처리(FSM / 물리 / 애니메이션)는 플레이어와 완전히 같은 경로를 탄다.
    /// </summary>
    public class ControllerAI : ControllerBase, IFixedUpdateable
    {
        private StateMachine<AIState> _stateMachine;
        private PhysicsController _physicsController;
        private float _moveInput;

        public override void Init(InGameContext inGameContext, InputContext inputContext, ObjectContext objectContext)
        {
            base.Init(inGameContext, inputContext, objectContext);
            _physicsController = GetComponent<PhysicsController>();

            _stateMachine = new StateMachine<AIState>();
            _stateMachine.AddState(AIState.Idle, new AIIdleState(_stateMachine, inputContext, objectContext, this));
            _stateMachine.AddState(AIState.Patrol, new AIPatrolState(_stateMachine, inputContext, objectContext, this));
            _stateMachine.AddState(AIState.Chase, new AIChaseState(_stateMachine, inputContext, objectContext, this));
            _stateMachine.ChangeState(AIState.Idle);

            Global.Instance.BindFixedUpdate(this);
        }

        public void OnFixedUpdate() => _stateMachine?.OnFixedUpdate();

        public override void Dispose()
        {
            base.Dispose();
            if (_stateMachine == null) return;

            Global.Instance?.UnBindFixedUpdate(this);
            _stateMachine = null;
        }

        #region Target
        //추격 대상. 지금은 플레이어 고정이지만 이후 진영 개념이 생기면 여기만 바꾸면 된다
        public Transform Target
        {
            get
            {
                var player = InGameContext?.Player;
                return player != null ? player.transform : null;
            }
        }

        public bool TryGetTargetDistance(out float distance)
        {
            var target = Target;
            if (target == null)
            {
                distance = float.MaxValue;
                return false;
            }
            distance = Vector2.Distance(target.position, transform.position);
            return true;
        }

        /// <summary>대상이 있는 쪽(-1 / 0 / 1). 대상이 없으면 0</summary>
        public float GetTargetDirectionX()
        {
            var target = Target;
            if (target == null) return 0f;

            float diffX = target.position.x - transform.position.x;
            return Mathf.Abs(diffX) < TargetDirectionDeadZone ? 0f : Mathf.Sign(diffX);
        }
        private const float TargetDirectionDeadZone = 0.05f;
        #endregion

        #region Input
        /// <summary>이동 입력. 값이 바뀔 때만 발행해서 플레이어 입력과 동일한 빈도로 동작시킨다</summary>
        public void SetMoveInput(float directionX)
        {
            directionX = directionX == 0f ? 0f : Mathf.Sign(directionX);
            if (Mathf.Approximately(_moveInput, directionX)) return;

            _moveInput = directionX;
            InputContext.NotifyMove(new Vector2(directionX, 0f));

            if (directionX > 0f)
                ObjectContext.SetDirection(Direction.Right);
            else if (directionX < 0f)
                ObjectContext.SetDirection(Direction.Left);
        }

        public void StopMove() => SetMoveInput(0f);
        public void RequestJump() => InputContext.NotifyJump();
        public void RequestDash() => InputContext.NotifyDash();
        #endregion

        #region Terrain
        //진행 방향이 낭떠러지인지 (지상에서만 판정)
        public bool IsLedgeAhead(float directionX)
        {
            if (_physicsController == null || directionX == 0f) return false;
            return _physicsController.IsLedgeAhead(Mathf.Sign(directionX) * ObjectContext.ObjectData.MoveSpeed);
        }

        //진행 방향이 벽인지
        public bool IsWallAhead(float directionX)
        {
            if (_physicsController == null || directionX == 0f) return false;
            return _physicsController.IsWallAhead(directionX);
        }
        #endregion

        private void OnDestroy() => Dispose();
    }
}
