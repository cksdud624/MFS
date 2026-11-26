using System.Collections.Generic;
using InGame.Component.Controller.Boss.Patterns;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// 보스 하나의 설계. Boss, BossPattern, BossCommand 표에서 BossFactory가 만든다.
    /// 보스마다 다른 것은 모두 여기(표)에 있고, 상태와 패턴 고르기와 커맨드는 모든 보스가 같은 코드를 쓴다.
    /// 설계는 바뀌지 않는다. 쿨타임처럼 싸우는 동안 바뀌는 것은 ControllerBossAI가 만든 실행 객체가 들고 있는다
    /// </summary>
    public class BossDefinition
    {
        #region Fields
        //Boss 표의 Id. Character 표의 Boss 칸에 적는 이름이다
        public string Name { get; }
        //체력바의 수. 체력바 하나가 페이즈 하나다
        public int PhaseCount { get; }
        //패턴이 끝나서 온 것이 아닐 때(전투 시작, 굳음이나 페이즈 전환 뒤) 쉬는 시간(초)
        public float IdleDuration { get; }
        //페이즈 전환 동안 맞지 않고 멈춰 있는 시간(초)
        public float PhaseTransitionDuration { get; }
        public IReadOnlyList<BossPatternDefinition> Patterns { get; }
        #endregion

        #region Init
        public BossDefinition(string name, int phaseCount, float idleDuration, float phaseTransitionDuration, IReadOnlyList<BossPatternDefinition> patterns)
        {
            Name = name;
            PhaseCount = phaseCount;
            IdleDuration = idleDuration;
            PhaseTransitionDuration = phaseTransitionDuration;
            Patterns = patterns;
        }
        #endregion
    }
}
