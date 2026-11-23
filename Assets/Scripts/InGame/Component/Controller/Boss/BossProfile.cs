using System.Collections.Generic;
using InGame.Component.Controller.Boss.Patterns;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 보스 하나의 설계. 어떤 패턴을 쓰는지, 체력바가 몇 개인지, 단계와 페이즈에 따라 얼마나 격해지는지를 정한다.
    /// 보스는 저마다 달라서 보스 하나당 이 클래스를 상속한 클래스 하나를 만들고, BossFactory에 이름을 등록한다.
    /// 여기에는 설계(무엇을 쓰는지)만 두고, 패턴을 고르고 굴리는 것은 ControllerBossAI가 한다.
    /// 단계는 체력바 하나 안에서 Hp가 줄수록 오르는 0 ~ 2이고, 페이즈는 몇 번째 체력바인지다
    /// </summary>
    public abstract class BossProfile
    {
        #region Fields
        //체력바의 수. 체력바 하나가 페이즈 하나이고, 마지막 체력바까지 깎여야 쓰러진다
        public int PhaseCount { get; protected set; } = 1;

        //마디 첫 박마다 이 중에서 하나를 고른다
        public IReadOnlyList<BossPattern> Patterns => PatternList;
        protected readonly List<BossPattern> PatternList = new();
        //연타를 맞았을 때 받아치는 카운터 자세. 경직이 풀린 뒤에도 쓴다. 없으면 null
        public BossPattern CounterReaction { get; protected set; }
        //연타를 맞았는데 카운터를 쓸 수 없을 때 내는 패턴. 없으면 null
        public BossPattern SpamReaction { get; protected set; }

        //아래 배열은 순번이 단계나 페이즈다. 배열이 짧으면 마지막 값을 이어서 쓴다

        //단계마다 : 마디 첫 박에 패턴을 내지 않고 그 마디를 쉴 확률
        protected float[] RestChances = { 0f };
        //단계마다 : 패턴이 끝난 뒤 한 마디를 쉴 확률
        protected float[] RestAfterChances = { 0f };
        //페이즈마다 : 단계에 더하는 값. 뒤 페이즈는 처음부터 높은 단계로 시작한다
        protected int[] PhaseStageBonuses = { 0 };
        //페이즈마다 : 걸음 속도의 배율
        protected float[] PhaseSpeedScales = { 1f };
        //페이즈마다 : 쉴 확률에 곱하는 배율. 작을수록 쉬지 않고 몰아친다
        protected float[] PhaseRestScales = { 1f };
        //페이즈마다 : 카운터 스택이 차는 시간의 배율. 작을수록 카운터를 자주 쓴다
        protected float[] PhaseCounterChargeScales = { 1f };
        //페이즈마다 : 그 페이즈에 들어갈 때 거는 상태 효과 (StatusEffect 표의 번호). 0이면 걸지 않는다
        protected long[] PhaseEffects = { 0 };
        #endregion

        #region Public
        public float GetRestChance(int stage, int phase) => Get(RestChances, stage) * Get(PhaseRestScales, phase);
        public float GetRestAfterChance(int stage, int phase) => Get(RestAfterChances, stage) * Get(PhaseRestScales, phase);
        public int GetStageBonus(int phase) => Get(PhaseStageBonuses, phase);
        public float GetSpeedScale(int phase) => Get(PhaseSpeedScales, phase);
        public float GetCounterChargeScale(int phase) => Get(PhaseCounterChargeScales, phase);
        public long GetPhaseEffect(int phase) => Get(PhaseEffects, phase);
        #endregion

        #region Private
        private static T Get<T>(T[] values, int index) => values[index < values.Length ? index : values.Length - 1];
        #endregion
    }
}
