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
    /// 무엇을 할지 정하는 부분(행동 상태 머신)은 상속받는 쪽이 바꿀 수 있다. 보스는 여기를 자기 패턴으로 채운다 (ControllerBossAI)
    /// </summary>
    public class ControllerAI : ControllerBase, IFixedUpdateable
    {
        #region Fields
        private StateMachine<AIState> _stateMachine;
        private PhysicsController _physicsController;
        private float _moveInput;
        //Init을 마쳤는지. 붙이자마자 떨어져 나가면 구독한 것이 없으므로 해제도 하지 않는다
        private bool _initialized;

        //추격 대상. 적대 진영만 노린다. 지금 적대 진영 캐릭터는 플레이어뿐이라 플레이어를 본다.
        //NPC(중립)는 적대가 아니므로 대상이 되지 않고, 이 AI가 중립(NPC)이면 아무도 쫓지 않는다
        public Transform Target
        {
            get
            {
                var player = InGameContext?.Player;
                if (player == null || !ObjectContext.IsHostileTo(player.Team)) return null;
                return player.transform;
            }
        }
        #endregion

        #region Init
        public override void Init(InGameContext inGameContext, InputContext inputContext, ObjectContext objectContext)
        {
            base.Init(inGameContext, inputContext, objectContext);
            _physicsController = GetComponent<PhysicsController>();

            InitBrain();
            _initialized = true;

            ObjectContext.OnDamagedChanged += OnDamagedChanged;
            Global.Instance.BindFixedUpdate(this);
        }
        #endregion

        #region LifeCycle
        public void OnFixedUpdate()
        {
            //피격 중에는 입력이 전부 막히므로 판단도 멈춘다.
            //계속 돌리면 타이머와 이동 판단만 앞서 나가서 복귀하는 순간 방향과 입력이 어긋난다
            if (ObjectContext.IsDamaged) return;
            //시나리오가 강제로 제어하는 동안에도 판단을 멈춘다. 돌리면 AI 입력이 시나리오 입력을 덮어쓴다
            if (IsSuspended) return;
            UpdateBrain();
        }

        private void OnDestroy() => Dispose();
        #endregion

        #region Public
        public override void Dispose()
        {
            base.Dispose();
            if (!_initialized) return;

            _initialized = false;
            if (ObjectContext != null)
                ObjectContext.OnDamagedChanged -= OnDamagedChanged;
            Global.Instance?.UnBindFixedUpdate(this);
            DisposeBrain();
        }
        #endregion

        #region Public - Target
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

        #region Public - Input
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
        public void RequestAttack() => RequestAttack(AttackButtonDefault);
        //버튼 번호가 커맨드를 정한다. 공격이 여러 갈래인 AI(보스)는 패턴마다 다른 버튼을 누른다
        public void RequestAttack(int attackButton) => InputContext.NotifyAttack(attackButton);
        public void RequestCounter() => InputContext.NotifyCounter();

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

        #region Public - Terrain
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

        #region Protected
        //AI는 같은 값이면 입력을 다시 내지 않으므로 기억해둔 이동 입력도 같이 비운다.
        //그대로 두면 다시 켰을 때 같은 방향으로 가려는 입력이 무시되어 멈춘 채로 남는다
        protected override void OnSuspended()
        {
            base.OnSuspended();
            _moveInput = 0f;
        }

        //강제로 움직인 사이에 자리가 바뀌었으므로 처음부터 다시 판단한다
        protected override void OnResumed() => ResetBrain();
        #endregion

        #region Protected - Brain
        /// <summary>
        /// 행동을 정하는 상태 머신을 만든다. 기본은 대기 → 순찰 → 추격 → 공격이다.
        /// 다르게 움직이는 AI는 여기와 아래 셋을 같이 바꿔서 자기 상태 머신으로 채운다
        /// </summary>
        protected virtual void InitBrain()
        {
            _stateMachine = new StateMachine<AIState>();
            _stateMachine.AddState(AIState.Idle, new AIIdleState(_stateMachine, InputContext, ObjectContext, this));
            _stateMachine.AddState(AIState.Patrol, new AIPatrolState(_stateMachine, InputContext, ObjectContext, this));
            _stateMachine.AddState(AIState.Chase, new AIChaseState(_stateMachine, InputContext, ObjectContext, this));
            _stateMachine.AddState(AIState.Attack, new AIAttackState(_stateMachine, InputContext, ObjectContext, this));
            _stateMachine.ChangeState(AIState.Idle);
        }

        /// <summary>물리 스텝마다 판단한다. 피격 중이거나 강제 제어 중에는 불리지 않는다</summary>
        protected virtual void UpdateBrain() => _stateMachine.OnFixedUpdate();

        /// <summary>하던 행동을 내려놓고 처음부터 다시 판단한다 (맞아서 굳었을 때, 강제 제어가 끝났을 때)</summary>
        protected virtual void ResetBrain() => _stateMachine.ChangeState(AIState.Idle);

        protected virtual void DisposeBrain() => _stateMachine = null;

        /// <summary>굳어 있다가 풀려났다. 풀리자마자 할 것이 있는 AI가 다시 정한다</summary>
        protected virtual void OnRecovered() { }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 맞아서 굳으면 하던 행동을 내려놓는다. 복귀하면 처음부터 다시 판단한다.
        /// 이동 입력도 비워둬야 Ground/Air가 복귀할 때 피격 전 입력을 이어받지 않는다
        /// </summary>
        private void OnDamagedChanged(bool damaged)
        {
            if (!damaged)
            {
                OnRecovered();
                return;
            }

            ResetBrain();
            //이미 처음 상태였으면 다시 들어가지 않으므로 입력은 여기서 한 번 더 비운다
            StopMove();
        }
        #endregion
    }
}
