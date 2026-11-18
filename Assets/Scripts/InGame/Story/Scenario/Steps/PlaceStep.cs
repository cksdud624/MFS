using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Place : 이미 월드에 있는 대상을 걷지 않고 바로 그 자리로 옮긴다. 돌아다니던 대상을 연출이 시작될 자리에 세울 때 쓴다.
    /// 화면에 보이는 채로 옮기면 순간이동으로 보이므로 보통 FadeOut 뒤에 쓴다. 아직 월드에 없는 대상은 Spawn으로 세운다.
    /// Player를 옮기면 카메라도 미끄러져 오지 않고 바로 따라간다.
    /// Actor = 대상, Zone + Param1 = 위치 (Zone의 왼쪽 아래를 0,0으로 보는 로컬 좌표, 발밑 기준 x,y)
    /// </summary>
    public class PlaceStep : StoryStep
    {
        #region Fields
        private long _zone;
        private Vector2 _position;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.CheckZone(_zone);

        public override long GetStageZone(StoryBook book) => _zone;

        public override UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            if (!context.TryGetActor(this, out var actor)) return UniTask.CompletedTask;

            actor.Place(context.ToWorld(_zone, _position));
            //카메라는 플레이어를 부드럽게 따라가서, 그대로 두면 옮긴 거리만큼 화면이 미끄러져 가는 모습이 보인다
            if (Actor.IsPlayer)
                context.CameraDirector?.SnapToFollow();
            return UniTask.CompletedTask;
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
            => reader.ReadActor(true, out actor)
               & reader.ReadZone(true, out _zone)
               & reader.ReadVector2(1, "Position", out _position);
        #endregion
    }
}
