using System.Collections.Generic;
using InGame.Component.Controller.Boss.Commands;

namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 표에서 만든 패턴 하나의 설계. 이름, 쓸 페이즈, 고르는 조건, 실행할 커맨드 목록을 담는다.
    /// 설계는 바뀌지 않으므로 같은 보스를 여럿 세워도 같이 쓸 수 있다. 쿨타임처럼 보스마다 다른 것은 실행 객체(BossCommandPattern)가 들고 있는다
    /// </summary>
    public class BossPatternDefinition
    {
        #region Fields
        //BossPattern 표의 번호
        public long Id { get; }
        //체력바 아래에 띄우고 로그에 남기는 이름
        public string Name { get; }
        //이 패턴을 쓰는 페이즈 (0부터. 표에는 1부터 적는다). 비어 있으면 모든 페이즈에서 쓴다
        public IReadOnlyList<int> Phases { get; }
        public BossPatternSettings Settings { get; }
        //위에서 아래로(Id 순) 하나씩 실행할 커맨드
        public IReadOnlyList<BossCommand> Commands { get; }
        #endregion

        #region Init
        public BossPatternDefinition(long id, string name, IReadOnlyList<int> phases, BossPatternSettings settings, IReadOnlyList<BossCommand> commands)
        {
            Id = id;
            Name = name;
            Phases = phases;
            Settings = settings;
            Commands = commands;
        }
        #endregion
    }
}
