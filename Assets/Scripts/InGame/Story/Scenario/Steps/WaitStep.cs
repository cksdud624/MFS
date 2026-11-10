using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Wait : 정해진 시간만큼 기다렸다가 다음 줄로 넘어간다. 연출 사이의 간격을 둘 때 쓴다.
    /// Param1 = 초
    /// </summary>
    public class WaitStep : StoryStep
    {
        #region Fields
        private float _seconds;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator)
        {
            if (_seconds <= 0f)
                validator.Error($"Param1 (Seconds) must be greater than 0 : {_seconds}");
        }

        public override UniTask Run(StoryContext context, CancellationToken cancellationToken)
            => UniTask.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken: cancellationToken);
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
        {
            actor = StoryActor.None;
            return reader.ReadFloat(1, "Seconds", out _seconds);
        }
        #endregion
    }
}
