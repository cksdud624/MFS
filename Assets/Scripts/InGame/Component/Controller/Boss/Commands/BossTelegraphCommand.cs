using System.Threading;
using Cysharp.Threading.Tasks;
using AttackTelegraph = Common.GameDefine.AttackTelegraph;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// Telegraph : 곧 어떤 공격이 오는지 보스의 스프라이트에 색을 섞어 알린다. 피하는 법이 종류마다 달라서 치기 전에 미리 보여준다.
    /// 패턴이 끝나거나 끊기면 저절로 지워지므로 끄는 줄은 따로 두지 않아도 된다. 패턴 도중에 끄려면 None을 적는다.
    /// Param1 = 예고 종류 (Melee, Low, Charge, Counter, None)
    /// </summary>
    public class BossTelegraphCommand : BossCommand
    {
        #region Fields
        private AttackTelegraph _telegraph;
        #endregion

        #region Public
        public override UniTask Run(ControllerBossAI boss, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            boss.Context.SetTelegraph(_telegraph);
            return UniTask.CompletedTask;
        }
        #endregion

        #region Protected
        protected override bool Parse(BossParamReader reader) => reader.ReadEnum(1, "Telegraph", out _telegraph);
        #endregion
    }
}
