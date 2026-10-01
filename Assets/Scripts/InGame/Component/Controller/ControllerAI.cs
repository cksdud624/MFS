using Common;
using Common.Template.FSM;
using Common.Template.Interface;
using InGame.Component.Controller.AI;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

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
            _stateMachine.AddState(AIState.Attack, new AIAttackState(_stateMachine, inputContext, objectContext, this));
            _stateMachine.ChangeState(AIState.Idle);

            ObjectContext.OnDamagedChanged += OnDamagedChanged;
            Global.Instance.BindFixedUpdate(this);
        }

        public void OnFixedUpdate()
        {
            //피격 중에는 입력이 전부 막히므로 판단도 멈춘다.
            //계속 돌리면 타이머와 이동 판단만 앞서 나가서 복귀하는 순간 방향과 입력이 어긋난다
            if (ObjectContext.IsDamaged) return;
            _stateMachine?.OnFixedUpdate();
        }

        /// <summary>
        /// 맞으면 하던 행동을 내려놓고 대기로 돌아간다. 복귀하면 대기에서 대상을 다시 찾으며 새로 판단한다.
        /// 이동 입력도 비워둬야 Ground/Air가 복귀할 때 피격 전 입력을 이어받지 않는다
        /// </summary>
        private void OnDamagedChanged(bool damaged)
        {
            if (!damaged || _stateMachine == null) return;

            _stateMachine.ChangeState(AIState.Idle);
            //이미 대기 중이었으면 다시 들어가지 않으므로 입력은 여기서 한 번 더 비운다
            StopMove();
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_stateMachine == null) return;

            if (ObjectContext != null)
                ObjectContext.OnDamagedChanged -= OnDamagedChanged;
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
        //공격 중에 누르면 다음 단계로 예약되므로 플레이어가 연타하는 것과 같이 콤보가 이어진다
        public void RequestAttack() => InputContext.NotifyAttack(AttackButtonDefault);

        /// <summary>움직이지 않고 대상 쪽으로 돌아선다. 공격은 시작한 방향으로 잠기므로 누르기 전에 맞춘다</summary>
        public void FaceTarget()
        {
            float directionX = GetTargetDirectionX();
            if (directionX > 0f)
                ObjectContext.SetDirection(Direction.Right);
            else if (directionX < 0f)
                ObjectContext.SetDirection(Direction.Left);
        }
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
