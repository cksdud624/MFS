using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story.Quest.Steps
{
    /// <summary>
    /// Reach (목표) : 플레이어 발밑이 목적지에서 반경 안에 들어오면 넘어간다.
    /// 발판 위처럼 높이가 다른 곳도 가리킬 수 있게 x, y를 다 본다.
    /// Zone + Param1 = 목적지 (Zone의 왼쪽 아래를 0,0으로 보는 로컬 좌표 x,y), Param2 = 반경 (비우면 말 걸기 거리)
    /// </summary>
    public class ReachStep : StoryStep
    {
        #region Fields
        public override StoryStepKind Kind => StoryStepKind.Goal;

        private long _zone;
        private Vector2 _destination;
        private float _radius;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator)
        {
            validator.CheckZone(_zone);
            if (_radius <= 0f)
                validator.Error($"Param2 (Radius) must be greater than 0 : {_radius}");
        }

        public override UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            var destination = context.ToWorld(_zone, _destination);
            return UniTask.WaitUntil(() => IsReached(context, destination), cancellationToken: cancellationToken);
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
        {
            actor = StoryActor.None;
            return reader.ReadZone(true, out _zone)
                   & reader.ReadVector2(1, "Destination", out _destination)
                   & reader.ReadOptionalFloat(2, "Radius", InteractDistance, out _radius);
        }
        #endregion

        #region Private
        private bool IsReached(StoryContext context, Vector2 destination)
        {
            var player = context.InGameContext.Player;
            return player != null && Vector2.Distance(player.transform.position, destination) <= _radius;
        }
        #endregion
    }
}
