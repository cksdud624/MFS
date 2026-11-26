using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// Approach : 대상(플레이어) 쪽으로 걸어간다. 좌우 거리가 멈출 거리 안으로 들어오거나, 최대 시간이 지나거나,
    /// 벽이나 낭떠러지에 막히면 멈추고 끝난다. 걸음 속도는 Object 표의 MoveSpeed다 (보스도 이동 입력만 낸다).
    /// Param1 = 멈출 거리, Param2 = 최대 시간(초)
    /// </summary>
    public class BossApproachCommand : BossCommand
    {
        #region Fields
        private float _stopDistance;
        private float _maxDuration;
        #endregion

        #region Public
        public override async UniTask Run(ControllerBossAI boss, CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            try
            {
                while (elapsed < _maxDuration)
                {
                    if (!boss.TryGetHorizontalDistance(out float distance) || distance <= _stopDistance) return;

                    //막힌 채로 걸으면 최대 시간까지 제자리걸음만 하므로 거기서 끝낸다
                    float directionX = boss.GetTargetDirectionX();
                    if (directionX == 0f || !boss.CanMove(directionX)) return;

                    boss.SetMoveInput(directionX);
                    await UniTask.WaitForFixedUpdate(cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                }
            }
            finally
            {
                //다음 커맨드(대개 FaceTarget, Attack)는 멈춘 채로 시작해야 한다. 걷던 속도가 남으면 공격이 미끄러지며 나간다
                boss.StopMove();
            }
        }
        #endregion

        #region Protected
        protected override bool Parse(BossParamReader reader)
            => reader.ReadPositiveFloat(1, "StopDistance", out _stopDistance) & reader.ReadPositiveFloat(2, "MaxDuration", out _maxDuration);
        #endregion
    }
}
