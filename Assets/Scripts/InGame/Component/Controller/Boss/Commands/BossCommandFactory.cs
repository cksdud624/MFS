using System;
using System.Collections.Generic;
using Generated.Table;
using UnityEngine;

namespace InGame.Component.Controller.Boss.Commands
{
    /// <summary>
    /// BossCommand 표의 Command 이름으로 커맨드를 만든다. 커맨드는 모든 보스가 같이 쓴다.
    /// 커맨드를 추가할 때는 BossCommand를 상속한 클래스를 Commands에 만들고 여기에 이름을 한 줄 등록한다
    /// </summary>
    public static class BossCommandFactory
    {
        #region Fields
        private static readonly Dictionary<string, Func<BossCommand>> Creators = new()
        {
            //움직임
            { "Approach", () => new BossApproachCommand() },
            { "Retreat", () => new BossRetreatCommand() },
            { "FaceTarget", () => new BossFaceTargetCommand() },

            //공격과 받아내기
            { "Attack", () => new BossAttackCommand() },
            { "Counter", () => new BossCounterCommand() },

            //예고와 간격
            { "Telegraph", () => new BossTelegraphCommand() },
            { "Wait", () => new BossWaitCommand() },
        };
        #endregion

        #region Public
        /// <summary>커맨드를 만들고 칸을 해석한다. 없는 이름이거나 칸이 틀리면 에러를 남기고 false</summary>
        public static bool TryCreate(BossCommandData data, out BossCommand command)
        {
            command = null;
            if (!Creators.TryGetValue(data.Command, out var create))
            {
                Debug.LogError($"BossCommand {data.Id} : unknown Command '{data.Command}'.");
                return false;
            }

            command = create();
            return command.Init(data);
        }
        #endregion
    }
}
