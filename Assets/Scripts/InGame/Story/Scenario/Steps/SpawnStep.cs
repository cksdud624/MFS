using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Spawn (시작) : 대상을 월드에 세운다. 세운 대상은 Despawn이나 Clear 전까지 월드에 남아서 뒤의 퀘스트와 시나리오가 같은 번호로 쓴다.
    /// 종류에 따라 붙는 것이 다르다. Player는 조작을 붙이고, Npc는 아무것도 붙이지 않아 제자리에 서 있고(Move로 움직인다), Enemy는 AI를 붙인다.
    /// 처음부터 서 있어야 하는 대상은 Spawn 대신 Actor 표의 Resident를 켠다.
    /// Actor = 세울 대상, Zone + Param1 = 위치 (Zone의 왼쪽 아래를 0,0으로 보는 로컬 좌표, 발밑 기준 x,y).
    /// 둘 다 비우면 Actor 표의 Zone과 Position에 선다. Player는 Actor 표에 없어서 꼭 적는다
    /// </summary>
    public class SpawnStep : StoryStep
    {
        #region Fields
        public override StoryStepKind Kind => StoryStepKind.Start;

        private long _zone;
        private Vector2 _position;
        //위치를 적었는지. 비우면 Actor 표의 자리에 세운다. 0,0도 실제 자리라서 값으로는 구분할 수 없다
        private bool _hasPosition;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator)
        {
            //플레이어는 Actor 표에 없어서 기본 자리가 없다
            if (Actor.IsPlayer && !_hasPosition)
                validator.Error("Player needs Zone and Param1 (Position).");
            //위치 없이 Zone만 적으면 어디에 세울지 정할 수 없다. Actor 표의 자리에 세우려면 둘 다 비운다
            if (!_hasPosition && _zone != 0)
                validator.Error("Zone is written without Param1 (Position). Leave both empty to use the Actor table.");
            if (_hasPosition)
                validator.CheckZone(_zone);
            validator.Spawn(Actor);
        }

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            Vector2 position;
            if (_hasPosition)
                position = context.ToWorld(_zone, _position);
            else
            {
                var entry = context.Book.GetActor(Actor);
                position = context.ToWorld(entry.Zone, entry.Position);
            }

            if (await context.Spawn(Actor, position) == null)
                LogError($"{Actor} is not spawned. Check the errors above.");
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
        {
            _hasPosition = reader.IsWritten(1);
            return reader.ReadActor(true, out actor)
                   & reader.ReadZone(_hasPosition, out _zone)
                   & reader.ReadOptionalVector2(1, "Position", Vector2.zero, out _position);
        }
        #endregion
    }
}
