using System.Collections.Generic;

namespace Generated.Table
{
    /// <summary>패턴 번호로 그 패턴의 커맨드를 찾기 위한 색인. 패턴은 커맨드를 Id 순으로 하나씩 실행한다</summary>
    public partial class BossCommandRecord
    {
        #region Fields
        private readonly Dictionary<long, List<BossCommandData>> _datasByPattern = new();
        #endregion

        #region Init
        partial void InitCustomRecord()
        {
            foreach (var data in datas)
            {
                if (!_datasByPattern.TryGetValue(data.Pattern, out var commands))
                {
                    commands = new List<BossCommandData>();
                    _datasByPattern[data.Pattern] = commands;
                }
                commands.Add(data);
            }

            //실행 순서는 Id 순이다. 줄을 끼울 때 사이 번호(101015)를 쓰면 표의 아래쪽에 적어도 제자리에서 실행된다
            foreach (var commands in _datasByPattern.Values)
                commands.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
        #endregion

        #region Public
        /// <summary>패턴의 커맨드. Id 순이다. 커맨드가 없는 패턴이면 null</summary>
        public IReadOnlyList<BossCommandData> GetCommands(long patternId)
        {
            _datasByPattern.TryGetValue(patternId, out var commands);
            return commands;
        }
        #endregion
    }
}
