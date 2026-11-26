using System.Threading;
using Cysharp.Threading.Tasks;
using Generated.Table;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// Attack : 공격 버튼을 눌러서 친다. 적은 커맨드까지 콤보를 잇고, 마지막 단계를 다 휘두르면 끝난다.
    /// 커맨드는 AttackCommand 표의 Command 그대로다. 버튼 번호를 누르는 순서대로 이어붙인 것이라 "22"는 2를 두 번 눌러 2단계까지 잇는다.
    /// 무엇을 얼마나 세게, 얼마나 나아가며 치는지는 AttackCommand, AttackHitBox 표가 정한다.
    /// 공격은 시작한 방향으로 잠기므로 앞에 FaceTarget을 둔다. 공격이 나가지 못하면(굳어 있음 등) 그대로 끝난다.
    /// Param1 = 커맨드 ("1", "22", "333")
    /// </summary>
    public class BossAttackCommand : BossCommand
    {
        #region Fields
        private string _command;
        //단계마다 누를 버튼 번호. 커맨드의 글자 하나가 버튼 하나다
        private int[] _buttons;
        #endregion

        #region Public
        /// <summary>콤보의 모든 단계("2", "22")가 그 보스의 AttackCommand 표에 있는지 본다. 빠진 단계가 있으면 거기서 콤보가 끊긴다</summary>
        public override bool Validate(ObjectData bossObject, TableManager tableManager)
        {
            bool valid = true;
            for (int length = 1; length <= _command.Length; length++)
            {
                string stage = _command.Substring(0, length);
                var commands = tableManager.AttackCommandRecord.GetCommands(bossObject, stage);
                if (commands is { Count: > 0 }) continue;

                Debug.LogError($"{this} : Object {bossObject.Id} has no attack command '{stage}' in AttackCommand table.");
                valid = false;
            }
            return valid;
        }

        public override async UniTask Run(ControllerBossAI boss, CancellationToken cancellationToken)
        {
            var context = boss.Context;

            //누른 버튼은 앞 공격이 남긴 콤보 뒤에 붙는다. 콤보가 풀리기 전에 누르면 "2" 뒤의 "2"가 "22"로 이어져 엉뚱한 단계가 나간다.
            //플레이어가 콤보를 새로 시작할 때처럼 앞 공격이 끝나고 콤보가 풀릴 때까지 기다린다. 한 스텝을 더 두는 것은
            //콤보를 푸는 쪽(ObjectStateController)과 같은 물리 스텝에서 어느 쪽이 먼저 도는지 모르기 때문이다
            while (context.IsAttacking || boss.TimeSinceAttackEnd <= AttackCommandResetTime + Time.fixedDeltaTime)
                await UniTask.WaitForFixedUpdate(cancellationToken);

            //단계가 시작된 횟수. 공격 중에 누른 버튼은 지금 단계가 끝날 때 이어붙이도록 하나만 예약되므로,
            //앞 단계가 시작된 것을 본 뒤에 다음 버튼을 누른다. 미리 다 누르면 마지막 것만 남는다
            int started = 0;
            void HandleAttackStart() => started++;

            context.OnAttackStart += HandleAttackStart;
            try
            {
                //첫 단계는 누르는 순간 바로 시작되므로(OnAttackStart가 그 자리에서 불린다) 시작되지 않았으면 나가지 못한 것이다
                boss.RequestAttack(_buttons[0]);
                int pressed = 1;

                while (context.IsAttacking)
                {
                    if (pressed < _buttons.Length && started >= pressed)
                        boss.RequestAttack(_buttons[pressed++]);
                    await UniTask.WaitForFixedUpdate(cancellationToken);
                }
            }
            finally
            {
                context.OnAttackStart -= HandleAttackStart;
            }
        }
        #endregion

        #region Protected
        protected override bool Parse(BossParamReader reader)
        {
            if (!reader.ReadText(1, "Command", out _command)) return false;

            _buttons = new int[_command.Length];
            for (int i = 0; i < _command.Length; i++)
            {
                //버튼 번호는 한 글자다. 반격(C)은 카운터로 받아냈을 때만 나가므로 누를 수 없다
                char button = _command[i];
                if (button < '1' || button > '9')
                    return reader.Error($"Param1 (Command) must be attack button numbers 1~9 such as 22 : '{_command}'");
                _buttons[i] = button - '0';
            }
            return true;
        }
        #endregion
    }
}
