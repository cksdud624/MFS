using System.Threading;
using Cysharp.Threading.Tasks;
using static Common.GameDefine;

namespace InGame.Story.Quest.Steps
{
    /// <summary>
    /// Clear (목표) : 적이 쓰러지면 넘어간다. 대상을 적으면 그 적만, 비우면 월드의 적 전부를 기다린다.
    /// 통과하면 그 적은 월드에서 나간 것이므로 뒤에 Despawn을 적지 않는다. 여럿을 차례로 적으면 모두 쓰러질 때까지 기다리는 것과 같다.
    /// 쓰러짐 처리가 생기기 전에는 Despawn으로만 사라진다. 쓰러질 때 오브젝트를 파괴하게 되면 그대로 동작한다.
    /// Actor = 쓰러뜨릴 적 (비워도 된다)
    /// </summary>
    public class ClearStep : StoryStep
    {
        #region Fields
        public override StoryStepKind Kind => StoryStepKind.Goal;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.Clear(Actor);

        public override UniTask Run(StoryContext context, CancellationToken cancellationToken)
            => Actor.IsNone
                ? UniTask.WaitUntil(context.IsEnemyCleared, cancellationToken: cancellationToken)
                : UniTask.WaitUntil(() => context.IsDefeated(Actor), cancellationToken: cancellationToken);
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor) => reader.ReadActor(false, out actor);
        #endregion
    }
}
