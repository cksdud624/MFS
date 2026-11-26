using System.Threading;
using Cysharp.Threading.Tasks;
using InGame.Component.Controller.Boss.Commands;

namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 표에서 만든 패턴(BossPatternDefinition)을 실행한다. 커맨드를 위에서 아래로(Id 순) 하나씩 끝나기를 기다리며 실행한다.
    /// 쿨타임은 이 객체가 보스마다 따로 들고, 설계와 커맨드는 같은 보스끼리 같이 쓴다
    /// </summary>
    public class BossCommandPattern : BossPattern
    {
        #region Fields
        private readonly ControllerBossAI _owner;
        private readonly BossPatternDefinition _definition;

        //지금 실행 중인 커맨드. 실행 중이 아니면 null이다
        public BossCommand CurrentCommand { get; private set; }
        #endregion

        #region Init
        public BossCommandPattern(ControllerBossAI owner, BossPatternDefinition definition)
            : base(definition.Name, definition.Settings)
        {
            _owner = owner;
            _definition = definition;
        }
        #endregion

        #region Public
        public override async UniTask Run(CancellationToken cancellationToken)
        {
            var commands = _definition.Commands;
            try
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    CurrentCommand = commands[i];
                    await CurrentCommand.Run(_owner, cancellationToken);
                }
            }
            finally
            {
                //끝까지 했든 끊겼든 실행 중인 커맨드가 남지 않게 한다
                CurrentCommand = null;
            }
        }
        #endregion
    }
}
