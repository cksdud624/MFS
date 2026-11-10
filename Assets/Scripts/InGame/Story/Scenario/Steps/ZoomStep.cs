using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Zoom : 카메라를 확대하거나 축소한다. 다 바뀌면 다음 줄로 넘어간다.
    /// 배율은 씬에 놓인 처음 카메라를 1로 본다. 2면 두 배 확대, 0.5면 두 배 축소다.
    /// Actor를 적으면 그 대상의 몸통 쪽으로 카메라를 옮기고, 비우면 처음 위치로 돌아간다. 그래서 대상 없이 배율 1이면 원래대로 되돌린다.
    /// Actor = 바라볼 대상 (비워도 된다), Param1 = 배율, Param2 = 걸리는 시간 (초, 비우면 기본값, 0이면 바로)
    /// </summary>
    public class ZoomStep : StoryStep
    {
        #region Fields
        private float _zoom;
        private float _duration;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator)
        {
            if (_zoom <= 0f)
                validator.Error($"Param1 (Zoom) must be greater than 0 : {_zoom}");
            if (_duration < 0f)
                validator.Error($"Param2 (Duration) must be 0 or more : {_duration}");
        }

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            if (context.CameraDirector == null)
            {
                LogError("Main camera is missing.");
                return;
            }

            Vector2? focus = null;
            if (!Actor.IsNone)
            {
                if (!context.TryGetActor(this, out var actor)) return;
                focus = actor.Center;
            }

            await context.CameraDirector.Zoom(_zoom, focus, _duration, cancellationToken);
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
            => reader.ReadActor(false, out actor)
               & reader.ReadFloat(1, "Zoom", out _zoom)
               & reader.ReadOptionalFloat(2, "Duration", CameraZoomDuration, out _duration);
        #endregion
    }
}
