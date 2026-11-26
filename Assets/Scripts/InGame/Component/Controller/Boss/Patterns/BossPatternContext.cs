using UnityEngine;

namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 패턴을 고르는 순간의 판단 값. ControllerBossAI.CreatePatternContext가 한 번 만들어 선택기와 모든 후보의 CanStart에 넘기므로
    /// 모든 후보가 같은 순간의 값으로 판단한다. 여러 패턴이 같이 보는 조건이 늘면 여기에 값을 더한다
    /// </summary>
    public readonly struct BossPatternContext
    {
        #region Fields
        //지금 페이즈 (0부터)
        public int Phase { get; }
        //대상(플레이어)이 있는지. 없으면 TargetPosition과 TargetDistance는 의미가 없다
        public bool HasTarget { get; }
        public Vector2 TargetPosition { get; }
        //대상과의 좌우 거리. 옆에서 보는 게임이라 높이 차이는 보지 않는다
        public float TargetDistance { get; }
        //판단 기준 시각(초. Time.time). 쿨타임을 볼 때 쓴다
        public float Time { get; }
        #endregion

        #region Init
        public BossPatternContext(int phase, bool hasTarget, Vector2 targetPosition, float targetDistance, float time)
        {
            Phase = phase;
            HasTarget = hasTarget;
            TargetPosition = targetPosition;
            TargetDistance = targetDistance;
            Time = time;
        }
        #endregion
    }
}
