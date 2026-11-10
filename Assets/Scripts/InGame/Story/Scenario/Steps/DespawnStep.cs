using System.Threading;
using Cysharp.Threading.Tasks;
using static Common.GameDefine;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Despawn (종료) : 대상을 월드에서 없앤다. 플레이어는 없앨 수 없다.
    /// 퀘스트 목표를 지난 적은 쓰러졌을 수 있어서 Despawn으로 가리킬 수 없고, Clear로 기다린다.
    /// Actor = 대상
    /// </summary>
    public class DespawnStep : StoryStep
    {
        #region Fields
        public override StoryStepKind Kind => StoryStepKind.End;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.Despawn(Actor);

        public override UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            if (context.TryGetActor(this, out _))
                context.Despawn(Actor);
            return UniTask.CompletedTask;
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor) => reader.ReadActor(true, out actor);
        #endregion
    }
}
