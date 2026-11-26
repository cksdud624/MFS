using System.Collections.Generic;

namespace Generated.Table
{
    /// <summary>보스 이름으로 그 보스의 패턴을 찾기 위한 색인. 보스전이 시작될 때 BossFactory가 한 번 읽는다</summary>
    public partial class BossPatternRecord
    {
        #region Fields
        private readonly Dictionary<string, List<BossPatternData>> _datasByBoss = new();
        #endregion

        #region Init
        partial void InitCustomRecord()
        {
            foreach (var data in datas)
            {
                if (!_datasByBoss.TryGetValue(data.Boss, out var patterns))
                {
                    patterns = new List<BossPatternData>();
                    _datasByBoss[data.Boss] = patterns;
                }
                patterns.Add(data);
            }

            //표에 적힌 순서가 아니라 Id 순으로 둔다. 같은 우선순위끼리 고를 때의 순서가 줄을 옮겨 적어도 바뀌지 않게 한다
            foreach (var patterns in _datasByBoss.Values)
                patterns.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
        #endregion

        #region Public
        /// <summary>보스의 패턴. Id 순이다. 패턴이 없는 보스면 null</summary>
        public IReadOnlyList<BossPatternData> GetPatterns(string boss)
        {
            if (string.IsNullOrEmpty(boss)) return null;

            _datasByBoss.TryGetValue(boss, out var patterns);
            return patterns;
        }
        #endregion
    }
}
