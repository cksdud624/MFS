using System.Threading;
using Cysharp.Threading.Tasks;
using Generated.Table;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// Counter : 카운터 자세(대기)를 잡는다. 대기 중에 맞으면 받아내고 반격(AttackCommand 표의 C)을 휘두르고, 반격까지 끝나면 끝난다.
    /// 아무도 때리지 않으면 대기 시간(CounterWaitDuration)이 지나서 끝난다. 카운터가 아직 차지 않았으면 자세를 잡지 못하고 그대로 끝난다.
    /// Param은 쓰지 않는다
    /// </summary>
    public class BossCounterCommand : BossCommand
    {
        #region Public
        /// <summary>반격이 없는 오브젝트는 카운터 대기에 들어가지 못한다 (받아내기만 하는 무적 구간이 되므로)</summary>
        public override bool Validate(ObjectData bossObject, TableManager tableManager)
        {
            var commands = tableManager.AttackCommandRecord.GetCommands(bossObject, CounterAttackCommand);
            if (commands is { Count: > 0 }) return true;

            Debug.LogError($"{this} : Object {bossObject.Id} has no counter attack '{CounterAttackCommand}' in AttackCommand table.");
            return false;
        }

        public override async UniTask Run(ControllerBossAI boss, CancellationToken cancellationToken)
        {
            var context = boss.Context;

            //대기는 누르는 순간 바로 시작된다. 시작되지 않았으면 잡지 못한 것이다
            boss.RequestCounter();
            if (!context.IsCountering) return;

            //받아내면 대기가 끝나는 동시에 반격이 시작되므로 둘 중 하나라도 이어지는 동안 기다린다
            while (context.IsCountering || context.IsAttacking)
                await UniTask.WaitForFixedUpdate(cancellationToken);
        }
        #endregion

        #region Protected
        protected override bool Parse(BossParamReader reader) => true;
        #endregion
    }
}
