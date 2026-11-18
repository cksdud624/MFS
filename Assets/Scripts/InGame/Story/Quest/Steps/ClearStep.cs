using System.Threading;
using Cysharp.Threading.Tasks;
using static Common.GameDefine;

namespace InGame.Story.Quest.Steps
{
    /// <summary>
    /// Clear (목표) : 적이 쓰러지면 넘어간다. 대상을 적으면 그 적만, 비우면 월드의 적 전부를 기다린다.
    /// 통과하면 그 적은 월드에서 나간 것이므로 뒤에 Despawn을 적지 않는다. 여럿을 차례로 적으면 모두 쓰러질 때까지 기다리는 것과 같다.
    /// 적은 Hp가 0이 되면 쓰러지는 모션을 한 뒤 사라지고, 사라지는 순간 이 목표가 채워진다.
    /// Actor = 쓰러뜨릴 적 (비워도 된다)
    /// </summary>
    public class ClearStep : StoryStep
    {
        #region Fields
        public override StoryStepKind Kind => StoryStepKind.Goal;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.Clear(Actor);

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            //기다리는 동안 쓰러뜨릴 적 위에 목표 아이콘이 뜬다. 취소되어도 남지 않게 지운다
            context.SetGoal(Actor.IsNone ? StoryGoal.ForEnemies() : StoryGoal.ForActor(Actor), Text);
            try
            {
                if (Actor.IsNone)
                    await UniTask.WaitUntil(context.IsEnemyCleared, cancellationToken: cancellationToken);
                else
                    await UniTask.WaitUntil(() => context.IsDefeated(Actor), cancellationToken: cancellationToken);
            }
            finally
            {
                context.ClearGoal();
            }
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor) => reader.ReadActor(false, out actor);
        #endregion
    }
}
