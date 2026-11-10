using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Move : 대상을 x 위치까지 강제로 걸어가게 한다. 도착하면 다음 줄로 넘어간다.
    /// Player와 Enemy도 움직일 수 있다. 움직이는 동안은 조작과 AI를 멈춰서 키를 눌러도 따라가지 않고, 도착하면 다시 켠다.
    /// 시간을 적으면 거리에 맞춰 걷는 속도를 바꿔서 그 시간에 도착한다. 걷는 애니메이션의 빠르기는 그대로다.
    /// 도착할 곳은 불러와 있는 Zone 안이어야 한다. 대상이 내려간 Zone으로 들어가면 꺼져서 도착하지 못한다.
    /// Actor = 대상, Zone + Param1 = 도착할 x (Zone의 왼쪽 끝을 0으로 보는 로컬 좌표), Param2 = 걸리는 시간 (초, 비우면 자기 걸음 속도)
    /// </summary>
    public class MoveStep : StoryStep
    {
        #region Fields
        private long _zone;
        private float _targetX;
        //도착까지 걸리는 시간. 0이면 자기 걸음 속도로 간다
        private float _seconds;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator)
        {
            validator.CheckZone(_zone);
            if (_seconds < 0f)
                validator.Error($"Param2 (Seconds) must be greater than 0, or empty : {_seconds}");
        }

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            if (!context.TryGetActor(this, out var actor)) return;

            float worldX = context.ToWorld(_zone, new Vector2(_targetX, 0f)).x;
            await actor.MoveTo(worldX, _seconds, cancellationToken);
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
            => reader.ReadActor(true, out actor)
               & reader.ReadZone(true, out _zone)
               & reader.ReadFloat(1, "X", out _targetX)
               & reader.ReadOptionalFloat(2, "Seconds", 0f, out _seconds);
        #endregion
    }
}
