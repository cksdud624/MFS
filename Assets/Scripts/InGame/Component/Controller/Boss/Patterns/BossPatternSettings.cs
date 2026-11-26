using Generated.Table;

namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 패턴 하나를 고르는 조건과 끝난 뒤의 쉬는 시간. 표에서 만든 패턴은 BossPattern 표의 한 줄에서 읽고,
    /// 코드로만 만들 수 있는 패턴은 생성자로 직접 만든다
    /// </summary>
    public class BossPatternSettings
    {
        #region Fields
        //같은 우선순위의 후보끼리 뽑을 때의 가중치. 0이면 뽑히지 않는다
        public float Weight { get; }
        //후보 중 이 값이 가장 높은 패턴끼리만 뽑는다. 조건이 맞으면 반드시 나와야 하는 패턴을 높게 둔다
        public int Priority { get; }
        //시작한 뒤 다시 뽑힐 수 있을 때까지의 시간(초)
        public float Cooldown { get; }
        //대상과의 좌우 거리가 이보다 가까우면 뽑히지 않는다. 0이면 제한이 없다
        public float MinDistance { get; }
        //대상과의 좌우 거리가 이보다 멀면 뽑히지 않는다. 0이면 제한이 없다
        public float MaxDistance { get; }
        //끝까지 한 뒤 다음 패턴을 고르기까지 쉬는 시간(초). 이 틈이 플레이어의 공격 기회다
        public float RecoveryDuration { get; }
        #endregion

        #region Init
        public BossPatternSettings(float weight, int priority, float cooldown, float minDistance, float maxDistance, float recoveryDuration)
        {
            Weight = weight;
            Priority = priority;
            Cooldown = cooldown;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            RecoveryDuration = recoveryDuration;
        }

        public BossPatternSettings(BossPatternData data)
            : this(data.Weight, data.Priority, data.Cooldown, data.MinDistance, data.MaxDistance, data.RecoveryDuration)
        {
        }
        #endregion
    }
}
