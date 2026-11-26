using Common.Template.FSM;
using InGame.Component.Controller.Boss;
using InGame.Component.Controller.Boss.Patterns;
using InGame.Context;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller
{
    /// <summary>
    /// 보스용 컨트롤러. 모든 보스가 이 하나를 쓰고, 보스마다 다른 것(페이즈, 패턴, 커맨드)은 표에서 만든 설계(BossDefinition)로 받는다.
    /// - 큰 상태는 상태 머신이 맡는다 : 시작 전 → 쉬기 ⇄ 패턴, 그리고 끼어드는 굳음, 페이즈 전환, 쓰러짐 (GameDefine.BossState)
    /// - 패턴은 커맨드를 차례로 기다리며 실행하는 비동기 작업이다 (BossCommandPattern). 공격 하나하나를 상태로 두지 않는다
    /// - 패턴 고르기는 조건(거리, 쿨타임, 페이즈) + 우선순위 + 가중치다 (BossPatternSelector)
    /// 보스도 처음에는 일반 AI(ControllerAI)가 붙어 있고, 보스전이 시작될 때 BossBattleController가 이것으로 바꾼 뒤 설계를 넣고(SetDefinition) 깨운다(Engage).
    /// 입력은 일반 AI와 같은 함수로만 낸다
    /// </summary>
    public class ControllerBossAI : ControllerAI
    {
        #region Fields
        private StateMachine<BossState> _stateMachine;
        private readonly BossPatternSelector _patternSelector = new();
        //다음 쉬기 한 번에만 쓸 쉬는 시간(초). 패턴이 끝까지 갔을 때만 그 패턴이 정한다
        private float? _requestedIdleDuration;
        //마지막으로 공격 단계가 끝난 시각(초. Time.time). 앞 공격의 콤보가 풀렸는지 볼 때 쓴다
        private float _lastAttackEndTime = float.NegativeInfinity;

        //이 보스의 설계. BossBattleController가 Character 표의 Boss 칸 이름으로 BossFactory에서 만들어 넣는다
        public BossDefinition Definition { get; private set; }
        //전투를 시작했는지. Engage부터다. 그 전에는 시작 전 상태에서 서 있기만 한다
        public bool IsEngaged { get; private set; }
        //싸우는 구역(보스가 서 있는 Zone). 구석 탈출처럼 구역을 보는 패턴이 쓴다
        public Rect Arena { get; private set; }

        //패턴 상태가 다음 패턴을 고를 때 쓴다
        public BossPatternSelector PatternSelector => _patternSelector;
        //커맨드가 공격 중인지, 카운터 대기 중인지 보고 예고를 걸 때 쓴다
        public ObjectContext Context => ObjectContext;
        //지금 페이즈 (0부터). 체력바가 넘어가면 ObjectContext가 올린다
        public int Phase => ObjectContext.Phase;
        //반격을 맞고 굳어 있는지
        public bool IsStaggered => ObjectContext.IsDamaged;
        //마지막 공격 단계가 끝난 뒤 지난 시간(초)
        public float TimeSinceAttackEnd => Time.time - _lastAttackEndTime;
        #endregion

        #region Public
        /// <summary>
        /// 이 보스의 설계를 넣는다. 체력바 수를 정하고 설계의 패턴을 페이즈마다 등록한다. 보스전을 시작하기 전(Engage 전)에 한 번 부른다.
        /// 패턴은 보스마다 새로 만든다. 쿨타임을 들고 있어서 같은 설계를 쓰는 보스끼리 나눠 쓰면 서로의 쿨타임을 건다
        /// </summary>
        public void SetDefinition(BossDefinition definition)
        {
            if (Definition != null)
            {
                Debug.LogWarning($"{name} already has boss definition '{Definition.Name}'.");
                return;
            }

            Definition = definition;
            ObjectContext.SetPhaseCount(definition.PhaseCount);

            foreach (var patternDefinition in definition.Patterns)
            {
                //여러 페이즈에 같은 객체를 등록해서 페이즈가 바뀌어도 쿨타임이 이어지게 한다
                var pattern = new BossCommandPattern(this, patternDefinition);
                if (patternDefinition.Phases.Count == 0)
                {
                    for (int phase = 0; phase < definition.PhaseCount; phase++)
                        _patternSelector.AddPattern(phase, pattern);
                    continue;
                }

                foreach (int phase in patternDefinition.Phases)
                    _patternSelector.AddPattern(phase, pattern);
            }
            RegisterPatterns(_patternSelector);
        }

        /// <summary>전투를 시작한다. arena는 전투가 벌어지는 구역(보스가 서 있는 Zone)이다. 설계를 넣기 전에는 시작하지 않는다</summary>
        public void Engage(Rect arena)
        {
            if (Definition == null)
            {
                Debug.LogError($"{name} has no boss definition. Call SetDefinition before Engage.");
                return;
            }

            Arena = arena;
            IsEngaged = true;
        }

        /// <summary>다음 쉬기 한 번에만 쓸 쉬는 시간(초)을 정한다. 패턴이 끝까지 갔을 때 패턴 상태가 그 패턴의 RecoveryDuration으로 부른다</summary>
        public void RequestNextIdleDuration(float seconds) => _requestedIdleDuration = Mathf.Max(0f, seconds);

        /// <summary>
        /// 쉬기에 들어올 때 부른다. 정해진 시간이 있으면 그것을, 없으면 Boss 표의 IdleDuration을 돌려주고 정해진 것은 지운다.
        /// 그래서 굳음이나 페이즈 전환 뒤의 쉬기는 기본 시간을 쓴다
        /// </summary>
        public float ConsumeNextIdleDuration()
        {
            float duration = _requestedIdleDuration ?? Definition?.IdleDuration ?? 0f;
            _requestedIdleDuration = null;
            return duration;
        }

        /// <summary>패턴을 고를 때 쓸 판단 값을 지금 순간으로 만든다. 대상이 없으면 거리를 따지는 패턴은 뽑히지 않는다</summary>
        public BossPatternContext CreatePatternContext()
        {
            var target = Target;
            bool hasTarget = TryGetHorizontalDistance(out float distance);
            Vector2 targetPosition = hasTarget ? (Vector2)target.position : Vector2.zero;
            return new BossPatternContext(Phase, hasTarget, targetPosition, distance, Time.time);
        }

        /// <summary>
        /// 대상과의 좌우 거리. 옆에서 보는 게임이라 높이 차이는 보지 않는다. 뛰어오른 플레이어를 멀리 있다고 보면 다가가다가 공격을 놓친다.
        /// 대상이 없으면 false
        /// </summary>
        public bool TryGetHorizontalDistance(out float distance)
        {
            var target = Target;
            if (target == null)
            {
                distance = 0f;
                return false;
            }
            distance = Mathf.Abs(target.position.x - transform.position.x);
            return true;
        }

        /// <summary>그 방향으로 걸어갈 수 있는지 (벽, 낭떠러지)</summary>
        public bool CanMove(float directionX) => !IsWallAhead(directionX) && !IsLedgeAhead(directionX);
        #endregion

        #region Protected
        //시나리오가 보스를 붙잡으면 하던 패턴을 내려놓는다. 패턴은 상태 머신 밖에서 비동기로 돌기 때문에 판단을 멈추는 것만으로는 서지 않고,
        //그대로 두면 패턴의 입력이 시나리오의 입력을 덮어쓴다
        protected override void OnSuspended()
        {
            if (_stateMachine?.CurrentKey == BossState.Pattern)
                _stateMachine.ChangeState(BossState.Idle);
            base.OnSuspended();
        }

        /// <summary>
        /// 보스의 상태를 모두 등록한다. Init에서 한 번 불린다.
        /// 그 보스만 쓰는 상태가 필요하면 상속해서 base 뒤에 더한다. 키는 BossState의 100번부터 쓴다
        /// </summary>
        protected virtual void RegisterStates(StateMachine<BossState> stateMachine)
        {
            stateMachine.AddState(BossState.Intro, new BossIntroState(stateMachine, this, BossState.Idle));
            stateMachine.AddState(BossState.Idle, new BossIdleState(stateMachine, this, BossState.Pattern));
            stateMachine.AddState(BossState.Pattern, new BossPatternState(stateMachine, this, BossState.Idle));
            stateMachine.AddState(BossState.Groggy, new BossGroggyState(stateMachine, this, BossState.Idle));
            stateMachine.AddState(BossState.PhaseTransition, new BossPhaseTransitionState(stateMachine, this, BossState.Idle));
            stateMachine.AddState(BossState.Dead, new BossDeadState(stateMachine, this));
        }

        /// <summary>표로 나타낼 수 없어서 코드로 만든 패턴을 더 등록한다. 설계의 패턴을 등록한 뒤에 한 번 불린다</summary>
        protected virtual void RegisterPatterns(BossPatternSelector patternSelector) { }
        #endregion

        #region Protected - Brain
        //일반 AI의 행동(대기 → 순찰 → 추격 → 공격) 대신 보스의 상태 머신을 쓴다
        protected override void InitBrain()
        {
            _stateMachine = new StateMachine<BossState>();
            RegisterStates(_stateMachine);
            _stateMachine.ChangeState(BossState.Intro);

            ObjectContext.OnPhaseChanged += HandlePhaseChanged;
            ObjectContext.OnDead += HandleDead;
            ObjectContext.OnAttackEnd += HandleAttackEnd;
        }

        protected override void FixedUpdateBrain() => _stateMachine.OnFixedUpdate();

        /// <summary>
        /// 맞아서 굳었을 때, 시나리오가 놓아줬을 때 불린다. 굳었으면 굳음으로, 아니면 쉬기로 가서 처음부터 다시 판단한다.
        /// 페이즈 전환과 쓰러짐은 이어간다. 끊으면 전환 중의 무적이 풀리거나 쓰러진 보스가 다시 움직인다
        /// </summary>
        protected override void ResetBrain()
        {
            if (!IsEngaged) return;
            if (_stateMachine.CurrentKey is BossState.PhaseTransition or BossState.Dead) return;

            _stateMachine.ChangeState(IsStaggered ? BossState.Groggy : BossState.Idle);
        }

        protected override void DisposeBrain()
        {
            if (ObjectContext != null)
            {
                ObjectContext.OnPhaseChanged -= HandlePhaseChanged;
                ObjectContext.OnDead -= HandleDead;
                ObjectContext.OnAttackEnd -= HandleAttackEnd;
            }

            //하던 패턴의 취소가 상태의 OnExit에 있으므로 떨어져 나갈 때도 지금 상태를 끝낸다
            _stateMachine.Stop();
            _stateMachine = null;
        }
        #endregion

        #region Event Handlers
        //체력바 하나가 다 깎였다. 하던 패턴을 끊고 전환한다. 체력바는 쓰러질 때까지 하나씩만 넘어가므로 전환 중에 다시 올 일은 없다
        private void HandlePhaseChanged(int phase)
        {
            if (_stateMachine.CurrentKey == BossState.Dead) return;
            _stateMachine.ChangeState(BossState.PhaseTransition);
        }

        private void HandleDead() => _stateMachine.ChangeState(BossState.Dead);

        private void HandleAttackEnd() => _lastAttackEndTime = Time.time;
        #endregion
    }
}
