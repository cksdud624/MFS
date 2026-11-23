using InGame.Component.Controller.Boss.Patterns;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss.Profiles
{
    /// <summary>
    /// 결투자. mori-kit의 결투자를 옮긴 근접 보스다. 거리를 재다가 마디 첫 박마다 베기, 연격, 바닥 쓸기, 돌진, 카운터 자세 중 하나를 낸다.
    /// 체력바는 셋이고, 뒤 페이즈일수록 빨라지고 덜 쉬고 카운터를 자주 쓴다.
    /// 근접 패턴의 거리는 mori-kit의 값에 결투자 사거리 배율(3)을 곱해 둔 것이다. 타격하는 박은 그대로다.
    /// 공격의 위력, 판정, 전진은 AttackCommand, AttackHitBox 표의 1004번 줄에 있다. 아래 버튼 번호가 그 줄의 Command다
    /// </summary>
    public class DuelistProfile : BossProfile
    {
        #region Fields
        //테이블로 차후 관리해야하는 부분
        private const int ButtonSlash = 1;
        private const int ButtonDouble = 2;
        private const int ButtonTriple = 3;
        private const int ButtonSweep = 4;
        private const int ButtonCharge = 5;
        private const int ButtonShove = 6;

        //카운터 자세 패턴이 위장을 시작하는 단계. 그 전에는 처음부터 카운터 색으로 예고한다
        private const int CounterDisguiseStage = 1;
        #endregion

        #region Init
        public DuelistProfile()
        {
            PhaseCount = 3;
            //mori-kit의 기본값은 1단계에서 늘 쉬게 되어 있어서(쉴 확률 1) 그대로 옮기지 않고 0.3으로 둔다. 단계마다 줄어드는 비율은 같다
            RestChances = new[] { 0.3f, 0.18f, 0.09f };
            RestAfterChances = new[] { 0.5f, 0.3f, 0f };
            PhaseStageBonuses = new[] { 0, 1, 1 };
            PhaseSpeedScales = new[] { 1f, 1.15f, 1.3f };
            PhaseRestScales = new[] { 1f, 0.75f, 0.5f };
            PhaseCounterChargeScales = new[] { 1f, 0.8f, 0.6f };
            //2, 3페이즈에 들어갈 때 공격력을 올린다 (StatusEffect 표의 3, 4번)
            PhaseEffects = new long[] { 0, 3, 4 };

            //3박 뒤 한 번. 물러나거나 카운터로 받아낸다
            PatternList.Add(new BossAttackPattern("베기", new BossPatternPick(0f, 15f, 5.7f, 1.65f),
                AttackTelegraph.Melee, ButtonSlash, true, 3f));
            //2박, 2박 반
            PatternList.Add(new BossAttackPattern("2연격", new BossPatternPick(0f, 15f, 5.7f, 1f),
                AttackTelegraph.Melee, ButtonDouble, true, 2f, 2.5f));
            //2, 3, 4박. 칠 때마다 밀고 들어온다. 2단계부터 쓴다
            PatternList.Add(new BossAttackPattern("3연격", new BossPatternPick(0f, 16.5f, 6f, 1f, 1),
                AttackTelegraph.Melee, ButtonTriple, true, 2f, 3f, 4f));
            //2박 뒤 낮게. 점프로 넘거나 카운터로 받아낸다
            PatternList.Add(new BossAttackPattern("바닥 쓸기", new BossPatternPick(0f, 16.5f, 7.2f, 1f),
                AttackTelegraph.Low, ButtonSweep, true, 2f));
            //멀리서 2박 뒤 돌진. 대시로 빠지거나 뛰어넘거나 카운터로 받아낸다. 제자리에서 준비한다
            PatternList.Add(new BossAttackPattern("돌진", new BossPatternPick(4f, 16f, 7f, 0.9f),
                AttackTelegraph.Charge, ButtonCharge, false, 2f));
            //4박째에 카운터 자세. 그때 때리면 반격당한다
            PatternList.Add(new BossCounterPattern("카운터 자세", new BossPatternPick(0f, 15f, 6.6f, 0.7f),
                4f, true, CounterDisguiseStage));

            //반응으로 나가는 패턴은 거리와 비중으로 고르지 않으므로 고르는 조건을 비워둔다.
            //받아치기는 그 자리에서 바로 카운터 색을 보이고(멈출 틈) 시작하는 박에 자세를 잡는다
            CounterReaction = new BossCounterPattern("받아치기", default, 0f, false, BossMaxStage + 1);
            //카운터가 없을 때는 1박 뒤에 밀쳐낸다
            SpamReaction = new BossAttackPattern("밀쳐내기", new BossPatternPick(0f, 0f, 4.8f, 0f),
                AttackTelegraph.Melee, ButtonShove, true, 1f);
        }
        #endregion
    }
}
