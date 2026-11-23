namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 패턴을 고르는 조건. 보스는 마디 첫 박마다 대상과의 거리와 단계가 맞는 패턴 중에서 비중대로 하나를 뽑는다.
    /// 거리는 대상과의 가로 거리다
    /// </summary>
    public readonly struct BossPatternPick
    {
        #region Fields
        //대상이 이 거리 안에 있을 때만 고른다
        public float MinDistance { get; }
        public float MaxDistance { get; }
        //패턴을 준비하면서 맞추려는 거리. 타격 직전까지 이 거리로 다가가거나 물러난다
        public float PreferDistance { get; }
        //뽑힐 비중. 고를 수 있는 패턴들의 비중을 더한 것에서 이만큼의 몫으로 뽑힌다
        public float Weight { get; }
        //이 단계부터 고른다
        public int MinStage { get; }
        #endregion

        #region Init
        public BossPatternPick(float minDistance, float maxDistance, float preferDistance, float weight, int minStage = 0)
        {
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            PreferDistance = preferDistance;
            Weight = weight;
            MinStage = minStage;
        }
        #endregion
    }
}
