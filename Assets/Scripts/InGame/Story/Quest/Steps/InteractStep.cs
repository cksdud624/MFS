using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Story.Quest.Steps
{
    /// <summary>
    /// Interact (목표) : 플레이어가 대상 가까이에서 Interact(F)를 누르면 넘어간다. 말 걸기에 쓰고, 대상은 Npc여야 한다.
    /// 멀리서 누른 것은 넘기고 계속 기다린다. 이어지는 대화는 다음 줄에 적는다 (가벼우면 Dialogue, 연출이 필요하면 Scenario).
    /// Actor = 대상
    /// </summary>
    public class InteractStep : StoryStep
    {
        #region Fields
        public override StoryStepKind Kind => StoryStepKind.Goal;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.RequireType(Actor, StoryActorType.Npc);

        public override async UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            if (!context.TryGetActor(this, out var actor)) return;

            //이 대상에게 거는 말은 일반 대화가 아니라 이 목표로 받는다
            context.SetInteractTarget(Actor);
            //기다리는 사이에 대상이 사라지면 조건을 채울 수 없다. 입력만 기다리면 퀘스트가 원인을 남기지 않고 그 자리에서 멈추므로
            //대상이 파괴되는 것도 같이 기다린다
            using var waitSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, actor.GetCancellationTokenOnDestroy());
            try
            {
                while (true)
                {
                    await context.InGameContext.WaitInteract(waitSource.Token);

                    //대화창이 열려 있는 동안(다른 Npc와 일반 대화) 누른 것은 대사를 넘기는 입력이다
                    if (context.IsInDialogue) continue;
                    var player = context.InGameContext.Player;
                    if (player == null) continue;
                    if (Mathf.Abs(player.transform.position.x - actor.transform.position.x) <= InteractDistance) return;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                //Play를 멈추거나 씬이 내려갈 때는 오브젝트가 한꺼번에 파괴되고, 대상이 이야기보다 먼저 파괴될 수 있다.
                //한 프레임 기다려서 이야기도 취소되었으면 에러 없이 취소로 끝낸다
                await UniTask.Yield(cancellationToken);

                //대상만 사라졌으면 목표를 채울 수 없다. 다음 줄로 넘어가면 말을 걸지 않았는데 대화가 이어지므로 이야기를 멈춘다
                LogError($"{Actor} is destroyed while waiting. The story stops here.");
                throw;
            }
            finally
            {
                context.SetInteractTarget(StoryActor.None);
            }
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor) => reader.ReadActor(true, out actor);
        #endregion
    }
}
