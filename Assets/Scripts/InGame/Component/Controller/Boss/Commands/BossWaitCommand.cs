using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// Wait : 정한 시간만큼 아무것도 하지 않고 기다린다. 예고를 보여준 뒤 치기 전의 틈처럼, 플레이어가 반응할 시간을 둘 때 쓴다.
    /// Param1 = 초
    /// </summary>
    public class BossWaitCommand : BossCommand
    {
        #region Fields
        private float _seconds;
        #endregion

        #region Public
        //보스 AI는 물리 스텝마다 판단하므로 기다림도 물리 스텝에 맞춘다. Update에 맞추면 다음 입력이 한 스텝 늦거나 빨라진다
        public override UniTask Run(ControllerBossAI boss, CancellationToken cancellationToken)
            => UniTask.Delay(TimeSpan.FromSeconds(_seconds), false, PlayerLoopTiming.FixedUpdate, cancellationToken);
        #endregion

        #region Protected
        protected override bool Parse(BossParamReader reader) => reader.ReadPositiveFloat(1, "Seconds", out _seconds);
        #endregion
    }
}
