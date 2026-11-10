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
    /// Actor = 세울 대상, Param1 = 위치 (발밑 기준 x,y. 비우면 Actor 표의 Position, Player는 꼭 적는다)
    /// </summary>
    public class SpawnStep : StoryStep
    {
        #region Fields
        public override StoryStepKind Kind => StoryStepKind.Start;

        private Vector2 _position;
        //위치를 적었는지. 비우면 Actor 표의 자리에 세운다. 0,0도 실제 자리라서 값으로는 구분할 수 없다
        private bool _hasPosition;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator)
        {
            //플레이어는 Actor 표에 없어서 기본 자리가 없다
            if (Actor.IsPlayer && !_hasPosition)
                validator.Error("Player needs Param1 (Position).");
            validator.Spawn(Actor);
        }

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            var position = _hasPosition ? _position : context.Book.GetActor(Actor).Position;
            if (await context.Spawn(Actor, position) == null)
                LogError($"{Actor} is not spawned. Check the errors above.");
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
        {
            _hasPosition = reader.IsWritten(1);
            return reader.ReadActor(true, out actor) & reader.ReadOptionalVector2(1, "Position", Vector2.zero, out _position);
        }
        #endregion
    }
}
