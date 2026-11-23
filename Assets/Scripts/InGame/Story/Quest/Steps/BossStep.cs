using System.Threading;
using Cysharp.Threading.Tasks;
using static Common.GameDefine;

namespace InGame.Story.Quest.Steps
{
    /// <summary>
    /// Boss (목표) : 보스전. 보스가 서 있는 Zone에 플레이어가 들어오면 그 Zone을 잠그고 전투를 시작하고, 보스가 쓰러지면 넘어간다.
    /// 잠긴 동안에는 다른 Zone으로 나갈 수 없고 카메라도 그 Zone 밖을 보여주지 않는다. 보스는 전투가 시작되기 전에는 맞지 않는다.
    /// 통과하면 보스는 월드에서 나간 것이므로 뒤에 Despawn을 적지 않는다 (Clear와 같다).
    /// 등장 연출이 필요하면 앞 줄에 Scenario를 둔다. 플레이어가 이미 그 Zone 안에 있으면 바로 시작한다.
    /// Actor = 보스 (적이고, Character 표의 Boss 칸에 보스 이름이 적혀 있어야 한다)
    /// </summary>
    public class BossStep : StoryStep
    {
        #region Fields
        public override StoryStepKind Kind => StoryStepKind.Goal;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.Boss(Actor);

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            if (!context.TryGetActor(this, out var boss)) return;

            //기다리는 동안 보스 위에 목표 아이콘이 뜬다. 취소되어도 잠근 Zone과 아이콘이 남지 않게 정리한다
            context.SetGoal(StoryGoal.ForActor(Actor), Text);
            try
            {
                //전투가 시작되기 전의 보스는 맞지 않아서 쓰러질 수 없지만, 다른 이유로 사라졌으면 기다릴 것이 없다
                await UniTask.WaitUntil(() => boss == null || context.IsPlayerInBossZone(boss), cancellationToken: cancellationToken);
                if (boss == null || !context.BeginBossBattle(this, boss)) return;

                await UniTask.WaitUntil(() => context.IsDefeated(Actor), cancellationToken: cancellationToken);
            }
            finally
            {
                context.EndBossBattle();
                context.ClearGoal();
            }
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor) => reader.ReadActor(true, out actor);
        #endregion
    }
}
