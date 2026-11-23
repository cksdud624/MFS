using static Common.GameDefine;

namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 카운터 자세 패턴. 정해진 박에 카운터 대기에 들어간다. 대기하는 동안 맞으면 받아내서 반격한다.
    /// 단계가 오르면 준비하는 동안은 보통 베기처럼 예고하다가 자세를 잡는 순간에만 카운터 색을 보인다(위장).
    /// 예고만 보고 받아치려고 때리면 반격당한다. 카운터 스택이 있을 때만 고른다
    /// </summary>
    public class BossCounterPattern : BossPattern
    {
        #region Fields
        //자세를 잡는 박. 패턴이 시작된 박을 0으로 본다
        private readonly float _stanceBeat;
        //준비하면서 원하는 거리로 다가가는지. 맞다가 받아치는 반응은 그 자리에서 자세를 잡는다
        private readonly bool _approaches;
        //이 단계부터 위장한다. 위장하지 않는 패턴은 가장 높은 단계보다 큰 값을 준다
        private readonly int _disguiseStage;
        //자세를 잡았는지. 잡은 뒤에는 대기(와 받아낸 뒤의 반격)가 끝나기를 기다린다
        private bool _isStanceTaken;
        #endregion

        #region Init
        public BossCounterPattern(string name, BossPatternPick pick, float stanceBeat, bool approaches, int disguiseStage)
            : base(name, pick)
        {
            _stanceBeat = stanceBeat;
            _approaches = approaches;
            _disguiseStage = disguiseStage;
        }
        #endregion

        #region LifeCycle
        public override void OnFixedUpdate()
        {
            if (_isStanceTaken)
            {
                //받아내지 못하고 대기가 끝났거나, 받아낸 뒤의 반격까지 끝났다
                if (!Controller.IsCountering && !Controller.IsAttacking)
                    IsFinished = true;
                return;
            }

            float beat = Beat;
            if (beat >= _stanceBeat - BossTelegraphBeats)
                Controller.SetTelegraph(Controller.Stage >= _disguiseStage ? AttackTelegraph.Melee : AttackTelegraph.Counter);

            //준비 : 자세를 잡기 직전까지 원하는 거리를 맞춘다
            if (beat < _stanceBeat - BossApproachStopBeats)
            {
                if (_approaches)
                    Controller.Approach(Pick.PreferDistance, BossApproachSpeedScale);
                else
                    Controller.Hold();
                return;
            }

            Controller.Hold();
            if (beat < _stanceBeat) return;

            _isStanceTaken = true;
            //위장하고 있었어도 자세를 잡는 순간에는 진짜 색을 보인다. 이때 때리면 반격이 닿는 범위도 같이 보여준다
            Controller.SetTelegraph(AttackTelegraph.Counter);
            Controller.PreviewAttack(CounterAttackCommand);
            Controller.UseCounter();
        }
        #endregion

        #region Public
        public override float GetWeight(int stage)
            => stage >= BossMaxStage ? Pick.Weight * BossRageCounterWeightScale : Pick.Weight;

        public override bool CanStart(ControllerBossAI controller) => controller.IsCounterReady;
        #endregion

        #region Protected
        protected override void OnBegin() => _isStanceTaken = false;
        #endregion
    }
}
