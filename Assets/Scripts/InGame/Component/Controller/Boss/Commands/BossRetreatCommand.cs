using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// Retreat : 대상(플레이어)에게서 멀어지는 쪽으로 걸어간다. 좌우 거리가 벌릴 거리 이상이 되거나, 최대 시간이 지나거나,
    /// 벽이나 낭떠러지에 막히면 멈추고 끝난다. 걷는 동안은 등을 돌리므로 치기 전에 FaceTarget을 둔다.
    /// Param1 = 벌릴 거리, Param2 = 최대 시간(초)
    /// </summary>
    public class BossRetreatCommand : BossCommand
    {
        #region Fields
        private float _distance;
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
                    if (!boss.TryGetHorizontalDistance(out float distance) || distance >= _distance) return;

                    //구석에 몰려 있으면 더 물러날 곳이 없다. 그 자리에서 다음 커맨드로 넘어간다
                    float directionX = -boss.GetTargetDirectionX();
                    if (directionX == 0f || !boss.CanMove(directionX)) return;

                    boss.SetMoveInput(directionX);
                    await UniTask.WaitForFixedUpdate(cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                }
            }
            finally
            {
                boss.StopMove();
            }
        }
        #endregion

        #region Protected
        protected override bool Parse(BossParamReader reader)
            => reader.ReadPositiveFloat(1, "Distance", out _distance) & reader.ReadPositiveFloat(2, "MaxDuration", out _maxDuration);
        #endregion
    }
}
