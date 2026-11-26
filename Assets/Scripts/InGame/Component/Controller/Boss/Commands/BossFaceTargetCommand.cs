using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// FaceTarget : 움직이지 않고 대상(플레이어) 쪽으로 돌아선다. 공격은 시작한 방향으로 잠기므로 Attack 앞에 둔다.
    /// 대상이 없거나 바로 위아래에 있으면 돌지 않는다. Param은 쓰지 않는다
    /// </summary>
    public class BossFaceTargetCommand : BossCommand
    {
        #region Public
        public override UniTask Run(ControllerBossAI boss, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            boss.FaceTarget();
            return UniTask.CompletedTask;
        }
        #endregion

        #region Protected
        protected override bool Parse(BossParamReader reader) => true;
        #endregion
    }
}
