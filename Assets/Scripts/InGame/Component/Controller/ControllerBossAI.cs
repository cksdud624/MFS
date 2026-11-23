using System.Collections.Generic;
using Common;
using Common.Template.FSM;
using InGame.Component.Controller.Boss;
using InGame.Component.Controller.Boss.Patterns;
using UnityEngine;
using static Common.GameDefine;
#if UNITY_EDITOR
using Common.Test;
#endif

namespace InGame.Component.Controller
{
    /// <summary>
    /// 보스용 컨트롤러. 일반 AI의 행동(대기 → 순찰 → 추격 → 공격) 대신 보스의 패턴으로 움직인다.
    /// 보스는 자기 시계(박)를 따라간다. 패턴 사이에는 거리를 재고, 마디 첫 박마다 보스의 설계(BossProfile)에서 패턴을 하나 골라 진행한다.
    /// 맞으면서도 굳지 않는 대신(경직 면역) 연타에는 카운터 자세로 받아치고, 플레이어의 반격에 굳었다 풀리면 물러나거나 받아칠 준비를 한다.
    /// 체력바 하나가 다 깎이면 페이즈가 넘어가고, 마지막 체력바까지 깎여야 쓰러진다.
    /// 입력은 일반 AI와 같이 InputContext로만 내므로 이동, 공격, 판정, 피격은 플레이어와 같은 경로를 탄다.
    /// 보스마다 다른 것은 BossProfile에, 여기에는 모든 보스가 같이 쓰는 진행 규칙만 둔다
    /// </summary>
    public class ControllerBossAI : ControllerAI
    {
        #region Fields
        private StateMachine<BossAIState> _stateMachine;
        //같은 상태로는 다시 들어갈 수 없어서, 패턴 도중에 다른 패턴으로 바꿀 때 직접 불러야 한다
        private BossPatternState _patternState;
        //이 보스의 설계 (패턴, 페이즈)
        private BossProfile _profile;

        //전투가 시작되었는지. 시작 전에는 제자리에 서 있고 맞지 않는다
        public bool IsEngaged { get; private set; }
        //전투가 벌어지는 구역 (보스가 서 있던 Zone). 전투가 시작될 때 받는다
        public Rect Arena { get; private set; }
        //전투가 시작된 뒤 흐른 시간(초). 굳어 있거나 연출로 멈춘 동안에는 흐르지 않는다
        private float _battleTime;
        //보스의 시계. 전투가 시작된 뒤 지난 박 수다. 패턴의 타이밍은 모두 이 시계로 잰다
        public float Beat => _battleTime / BossBeatDuration;

        //다음에 진행할 패턴과 그 패턴이 시작되는 박. Pattern 상태가 들어가면서 받아 간다
        public BossPattern NextPattern { get; private set; }
        public float NextPatternStartBeat { get; private set; }
        //패턴을 고르지 않고 쉴 마디 수
        private int _restBars;
        //고를 수 있는 패턴을 모으는 임시 목록. 고를 때마다 할당하지 않게 재사용한다
        private readonly List<BossPattern> _candidates = new();

        //카운터 스택과 다음 스택이 차기까지 흐른 시간, 쓴 뒤의 쿨타임
        private int _counterStack = BossCounterMaxStack;
        private float _counterChargeTimer;
        private float _counterCooldown;
        public bool IsCounterReady => _counterStack > 0 && _counterCooldown <= 0f;

        //최근에 맞은 시각 (전투 시간). 짧은 시간에 여러 번 맞았는지 본다
        private readonly List<float> _hitTimes = new();
        //마지막으로 본 Hp. Hp가 줄었을 때만 맞은 것으로 센다
        private int _lastHp;
        //회피한 뒤 다시 회피할 수 있을 때까지 남은 시간(초)
        private float _dodgeCooldown;
        //굳어 있다가 방금 풀려났는지. 풀려난 순간에는 오브젝트의 상태가 바뀌는 도중이라 입력을 받지 못하므로, 다음 판단 때 반응한다
        private bool _hasRecovered;
        //범위를 예고하고 있는 공격의 커맨드. 패턴이 매 틱 같은 예고를 걸어도 한 번만 알리게 기억해둔다. 예고가 없으면 null
        private string _previewCommand;

        public bool IsAttacking => ObjectContext.IsAttacking;
        public bool IsCountering => ObjectContext.IsCountering;

        /// <summary>
        /// 단계 (0 기본, 1 격해짐, 2 격노). 지금 체력바에서 Hp가 줄수록 오르고, 뒤 페이즈는 더 높은 단계에서 시작한다.
        /// 단계가 오르면 빨라지고 덜 쉬고, 단계가 되어야 쓰는 패턴이 풀린다
        /// </summary>
        public int Stage
        {
            get
            {
                float hpRatio = ObjectContext.MaxHp > 0 ? (float)ObjectContext.Hp / ObjectContext.MaxHp : 0f;
                int stage = hpRatio > BossStage1HpRatio ? 0 : hpRatio > BossStage2HpRatio ? 1 : 2;
                return Mathf.Min(BossMaxStage, stage + _profile.GetStageBonus(ObjectContext.Phase));
            }
        }
        #endregion

        #region Public
        /// <summary>
        /// 전투를 시작한다. arena는 전투가 벌어지는 구역이다. 여기서부터 보스의 시계가 흐르고 맞기 시작한다.
        /// 시작하자마자 치지 않도록 첫 마디는 거리만 잰다
        /// </summary>
        public void Engage(Rect arena)
        {
            if (IsEngaged) return;

            IsEngaged = true;
            Arena = arena;
            _battleTime = 0f;
            _restBars = BossFirstRestBars;
            Debug.Log($"[보스] {name} 전투 시작 (페이즈 1/{_profile.PhaseCount})");
            _stateMachine.ChangeState(BossAIState.Spacing);
        }
        #endregion

        #region Public - Target
        /// <summary>대상까지의 가로 거리와 대상이 있는 쪽(-1 / 1). 대상이 없으면 false</summary>
        public bool TryGetTargetGap(out float distance, out float directionX)
        {
            distance = float.MaxValue;
            directionX = 0f;
            var target = Target;
            if (target == null) return false;

            float diffX = target.position.x - transform.position.x;
            distance = Mathf.Abs(diffX);
            directionX = diffX >= 0f ? 1f : -1f;
            return true;
        }
        #endregion

        #region Public - Input
        /// <summary>
        /// directionX 쪽으로 걷는다. 걸음 속도에 speedScale을 곱하고, 단계와 페이즈에 따라 더 빨라진다.
        /// 걷는 동안에도 몸은 대상을 본다 (뒤로 걸으면 물러서는 모습이 된다). 벽이나 낭떠러지 앞에서는 멈춘다
        /// </summary>
        public void Walk(float directionX, float speedScale)
        {
            if (directionX == 0f || IsWallAhead(directionX) || IsLedgeAhead(directionX))
            {
                Hold();
                return;
            }

            //걸음 속도는 이동 입력이 들어올 때 정해지는데 같은 방향이면 입력을 다시 내지 않는다. 속도가 바뀌면 입력을 한 번 비워서 다시 내게 한다
            float multiplier = speedScale * GetSpeedScale();
            if (!Mathf.Approximately(ObjectContext.MoveSpeedMultiplier, multiplier))
            {
                ObjectContext.SetMoveSpeedMultiplier(multiplier);
                StopMove();
            }
            SetMoveInput(directionX);
            //이동 입력은 가는 쪽으로 돌려세우므로, 입력을 낸 뒤에 대상 쪽으로 다시 맞춘다
            FaceTarget();
        }

        /// <summary>제자리에 서서 대상을 본다</summary>
        public void Hold()
        {
            StopMove();
            FaceTarget();
        }

        /// <summary>
        /// 대상과의 거리가 preferDistance가 되도록 다가가거나 물러난다. 그 거리에서 deadZone 안쪽이면 서 있는다.
        /// 서 있는 범위가 없으면 그 거리를 사이에 두고 앞뒤로 떤다
        /// </summary>
        public void Approach(float preferDistance, float speedScale, float deadZone = BossApproachDeadZone)
        {
            if (!TryGetTargetGap(out float distance, out float directionX))
            {
                Hold();
                return;
            }

            float gap = distance - preferDistance;
            if (Mathf.Abs(gap) < deadZone)
                Hold();
            else
                Walk(Mathf.Sign(gap) * directionX, speedScale);
        }

        public void SetTelegraph(AttackTelegraph telegraph) => ObjectContext.SetTelegraph(telegraph);

        //지금 하고 있는 것의 이름을 화면에 띄운다 (확인용). 끝나면 빈 문자열로 지운다
        public void SetPatternName(string patternName) => ObjectContext.SetPatternName(patternName);

        /// <summary>
        /// 곧 나갈 공격의 범위를 미리 보여준다. command는 그 공격의 커맨드(AttackCommand 표의 Command)다. null을 넘기면 예고를 거둔다.
        /// 어디에 어떤 모양으로 보일지는 그 공격의 판정과 전진이 정한다 (AttackShapeView)
        /// </summary>
        public void PreviewAttack(string command)
        {
            if (_previewCommand == command) return;

            _previewCommand = command;
            ObjectContext.SetAttackPreview(command == null
                ? null
                : Global.Instance.TableManager.AttackCommandRecord.GetCommands(ObjectContext.ObjectData, command));
        }

        /// <summary>카운터 대기에 들어간다. 스택을 하나 쓰고 쿨타임이 돈다</summary>
        public void UseCounter()
        {
            _counterStack--;
            _counterCooldown = BossCounterCooldown;
            RequestCounter();
        }
        #endregion

        #region Public - Pattern
        /// <summary>
        /// 마디 첫 박에 다음 패턴을 고른다. 쉬는 마디면 고르지 않는다.
        /// 거리와 단계가 맞고 지금 쓸 수 있는 패턴 중에서 비중대로 하나를 뽑아 startBeat부터 진행한다. 골랐으면 true
        /// </summary>
        public bool TryPickPattern(float startBeat)
        {
            if (_restBars > 0)
            {
                _restBars--;
                return false;
            }

            int stage = Stage;
            if (Random.value < _profile.GetRestChance(stage, ObjectContext.Phase)) return false;
            if (!TryGetTargetGap(out float distance, out _)) return false;

            _candidates.Clear();
            float totalWeight = 0f;
            var patterns = _profile.Patterns;
            for (int i = 0; i < patterns.Count; i++)
            {
                var pick = patterns[i].Pick;
                if (pick.MinStage > stage || distance < pick.MinDistance || distance > pick.MaxDistance) continue;
                if (!patterns[i].CanStart(this)) continue;

                _candidates.Add(patterns[i]);
                totalWeight += patterns[i].GetWeight(stage);
            }
            if (_candidates.Count == 0) return false;

            //비중을 이어 붙인 구간에서 뽑는다. 소수 오차로 끝까지 넘어가면 마지막 패턴이 된다
            float roll = Random.value * totalWeight;
            var picked = _candidates[^1];
            for (int i = 0; i < _candidates.Count; i++)
            {
                float weight = _candidates[i].GetWeight(stage);
                if (roll < weight)
                {
                    picked = _candidates[i];
                    break;
                }
                roll -= weight;
            }

            StartPattern(picked, startBeat);
            return true;
        }

        /// <summary>패턴이 끝났다. 단계에 따라 한 마디를 쉬기도 하고, 거리 재기로 돌아간다</summary>
        public void FinishPattern()
        {
            if (Random.value < _profile.GetRestAfterChance(Stage, ObjectContext.Phase))
                _restBars = 1;
            _stateMachine.ChangeState(BossAIState.Spacing);
        }

        /// <summary>물러난다. 대상의 반대쪽으로 대시해서 빠진 뒤 거리 재기로 돌아간다</summary>
        public void Evade() => _stateMachine.ChangeState(BossAIState.Evade);

        /// <summary>페이즈 전환이 끝났다. 새 페이즈는 쉬지 않고 바로 시작한다</summary>
        public void EndPhaseBreak()
        {
            _restBars = 0;
            _stateMachine.ChangeState(BossAIState.Spacing);
        }
        #endregion

        #region Protected
        //연출로 멈추거나 쓰러지면 예고도 거둔다. 예고 색이 입혀진 채로 연출에 나오거나 쓰러지지 않게 한다
        protected override void OnSuspended()
        {
            base.OnSuspended();
            ObjectContext.SetTelegraph(AttackTelegraph.None);
            ObjectContext.SetPatternName(string.Empty);
            PreviewAttack(null);
        }
        #endregion

        #region Protected - Brain
        protected override void InitBrain()
        {
            //보스가 아닌 오브젝트에는 이 컨트롤러를 붙이지 않으므로(ObjectBase.AttachController) 여기서는 등록된 이름이라고 본다
            BossFactory.TryCreate(ObjectContext.BossName, out _profile);
            ObjectContext.SetPhaseCount(_profile.PhaseCount);
            _lastHp = ObjectContext.Hp;

            _stateMachine = new StateMachine<BossAIState>();
            _patternState = new BossPatternState(_stateMachine, ObjectContext, this);
            _stateMachine.AddState(BossAIState.Standby, new BossStandbyState(_stateMachine, ObjectContext, this));
            _stateMachine.AddState(BossAIState.Spacing, new BossSpacingState(_stateMachine, ObjectContext, this));
            _stateMachine.AddState(BossAIState.Pattern, _patternState);
            _stateMachine.AddState(BossAIState.Evade, new BossEvadeState(_stateMachine, ObjectContext, this));
            _stateMachine.AddState(BossAIState.PhaseBreak, new BossPhaseBreakState(_stateMachine, ObjectContext, this));
            _stateMachine.ChangeState(BossAIState.Standby);

            //핸들러가 상태 머신을 바로 보므로 첫 상태에 들어간 뒤에 구독한다
            ObjectContext.OnHpChanged += HandleHpChanged;
            ObjectContext.OnPhaseChanged += HandlePhaseChanged;
#if UNITY_EDITOR
            EditorTestKeys.OnBossPhaseSkip += OnTestPhaseSkip;
#endif
        }

        protected override void UpdateBrain()
        {
            if (IsEngaged)
            {
                float deltaTime = Time.fixedDeltaTime;
                _battleTime += deltaTime;
                UpdateCounterCharge(deltaTime);
                if (_dodgeCooldown > 0f)
                    _dodgeCooldown -= deltaTime;
            }

            if (_hasRecovered)
            {
                _hasRecovered = false;
                ReactToRecovery();
            }
            _stateMachine.OnFixedUpdate();
        }

        //맞아서 굳거나 연출로 멈추면 하던 패턴은 버린다. 풀린 뒤에는 다음 마디 첫 박에 다시 고른다
        protected override void ResetBrain()
            => _stateMachine.ChangeState(IsEngaged ? BossAIState.Spacing : BossAIState.Standby);

        protected override void DisposeBrain()
        {
#if UNITY_EDITOR
            EditorTestKeys.OnBossPhaseSkip -= OnTestPhaseSkip;
#endif
            if (ObjectContext != null)
            {
                ObjectContext.OnHpChanged -= HandleHpChanged;
                ObjectContext.OnPhaseChanged -= HandlePhaseChanged;
            }
            _stateMachine = null;
        }

        //굳어 있다가 풀려났다. 지금은 오브젝트가 피격 상태를 빠져나오는 도중이라 대시 같은 입력이 먹지 않는다.
        //표시만 해두고 다음 판단 때 반응한다
        protected override void OnRecovered() => _hasRecovered = true;
        #endregion

        #region Event Handlers
        /// <summary>
        /// Hp가 줄었으면 맞은 것이다. 보스는 맞아도 굳지 않으므로(경직 면역) 맞는 것에 반응해서 받아친다.
        /// 굳어 있는 동안과 페이즈 전환 중에는 반응하지 않고, 카운터 대기 중이면 이미 받아칠 준비를 한 것이다
        /// </summary>
        private void HandleHpChanged(int hp, int maxHp)
        {
            bool isHit = hp < _lastHp;
            _lastHp = hp;
            if (!isHit || !IsEngaged) return;
            if (ObjectContext.IsDamaged || IsCountering || _stateMachine.CurrentKey == BossAIState.PhaseBreak) return;

            if (TryReactToSpam()) return;
            TryDodge();
        }

        /// <summary>
        /// 체력바 하나가 다 깎여서 다음 페이즈로 넘어간다. 하던 패턴을 버리고 전환에 들어가고, 새 페이즈의 강화를 건다.
        /// 굳어 있는 중이었으면 전환은 굳은 것이 풀린 뒤부터 진행된다
        /// </summary>
        private void HandlePhaseChanged(int phase)
        {
            Debug.Log($"[보스] {name} 페이즈 {phase + 1}/{_profile.PhaseCount}");
            long effectId = _profile.GetPhaseEffect(phase);
            if (effectId != 0)
                ObjectContext.StatusEffects.Add(effectId);

            _hitTimes.Clear();
            _stateMachine.ChangeState(BossAIState.PhaseBreak);
        }
        #endregion

        #region Private
        /// <summary>다음 패턴을 정하고 Pattern 상태로 넘어간다. 이미 패턴 중이면 그 패턴을 끊고 바꾼다</summary>
        private void StartPattern(BossPattern pattern, float startBeat)
        {
            NextPattern = pattern;
            NextPatternStartBeat = startBeat;
            Debug.Log($"[보스] {name} 패턴 : {pattern.Name}");

            if (_stateMachine.CurrentKey == BossAIState.Pattern)
                _patternState.Restart();
            else
                _stateMachine.ChangeState(BossAIState.Pattern);
        }

        //걸음 속도의 배율. 단계가 오를 때마다 일정한 속도가 더해지고, 페이즈의 배율이 곱해진다
        private float GetSpeedScale()
        {
            float moveSpeed = ObjectContext.ObjectData.MoveSpeed;
            float stageScale = moveSpeed > 0f ? (moveSpeed + BossStageSpeedBonus * Stage) / moveSpeed : 1f;
            return stageScale * _profile.GetSpeedScale(ObjectContext.Phase);
        }

        /// <summary>
        /// 플레이어의 반격에 굳었다가 풀려난 뒤의 반응. 가만히 있으면 이어서 맞으므로, 플레이어가 가까우면 대시로 빠지거나 받아칠 준비를 한다.
        /// 굳어 있는 사이에 페이즈가 넘어갔으면 전환을 그대로 이어간다
        /// </summary>
        private void ReactToRecovery()
        {
            if (!IsEngaged || _stateMachine.CurrentKey == BossAIState.PhaseBreak) return;
            if (!TryGetTargetGap(out float distance, out _) || distance > BossRecoverDistance) return;

            if (Random.value < BossRecoverEvadeChance)
                Evade();
            else if (IsCounterReady && _profile.CounterReaction != null)
                StartPattern(_profile.CounterReaction, GetReactionStartBeat());
        }

        private void UpdateCounterCharge(float deltaTime)
        {
            if (_counterCooldown > 0f)
                _counterCooldown -= deltaTime;
            if (_counterStack >= BossCounterMaxStack) return;

            _counterChargeTimer += deltaTime;
            if (_counterChargeTimer < BossCounterChargeInterval * _profile.GetCounterChargeScale(ObjectContext.Phase)) return;

            _counterChargeTimer = 0f;
            _counterStack++;
        }

        /// <summary>
        /// 연타 대응. 짧은 시간에 여러 번 맞았으면 카운터 자세로 받아친다. 하던 패턴이 있어도 끊고 자세를 잡는다.
        /// 자세를 잡기 전에 카운터 색을 먼저 보여서 플레이어가 보고 멈출 틈을 준다.
        /// 카운터를 쓸 수 없으면 패턴 사이일 때만 밀쳐내기로 대신한다. 반응했으면 true
        /// </summary>
        private bool TryReactToSpam()
        {
            _hitTimes.Add(_battleTime);
            while (_hitTimes.Count > 0 && _hitTimes[0] < _battleTime - BossSpamWindow)
                _hitTimes.RemoveAt(0);
            if (_hitTimes.Count < BossSpamHitCount) return false;

            _hitTimes.Clear();
            if (IsCounterReady && _profile.CounterReaction != null)
            {
                StartPattern(_profile.CounterReaction, GetReactionStartBeat());
                return true;
            }
            if (_stateMachine.CurrentKey == BossAIState.Spacing && _profile.SpamReaction != null)
            {
                StartPattern(_profile.SpamReaction, GetReactionStartBeat());
                return true;
            }
            return false;
        }

        /// <summary>회피. 패턴 사이에 맞으면 가끔 대시로 빠져서 이어지는 콤보를 피한다</summary>
        private void TryDodge()
        {
            if (_stateMachine.CurrentKey != BossAIState.Spacing || _dodgeCooldown > 0f) return;
            if (Random.value >= BossDodgeChance) return;

            _dodgeCooldown = BossDodgeCooldown;
            Evade();
        }

        //반응으로 나가는 패턴이 시작되는 박. 지금부터 BossReactionLeadBeats 뒤의 첫 반 박 자리다. 반응도 박에 맞춰 나간다
        private float GetReactionStartBeat()
            => Mathf.Ceil((Beat + BossReactionLeadBeats) / BossReactionQuantBeats) * BossReactionQuantBeats;
        #endregion

        #region Editor
#if UNITY_EDITOR
        //보스 체력바 테스트 키. 전투 중인 보스의 지금 체력바를 비워서 페이즈 전환과 쓰러짐을 바로 확인한다
        private void OnTestPhaseSkip()
        {
            if (IsEngaged)
                ObjectContext.SetHp(0);
        }
#endif
        #endregion
    }
}
