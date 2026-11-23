using System;
using System.Collections.Generic;
using InGame.Component.Controller.Boss.Profiles;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// Character 표의 Boss 칸에 적은 이름으로 보스의 설계(BossProfile)를 만든다.
    /// 보스를 추가할 때는 BossProfile을 상속한 클래스를 Profiles에 만들고 여기에 이름을 한 줄 등록한다
    /// </summary>
    public static class BossFactory
    {
        #region Fields
        private static readonly Dictionary<string, Func<BossProfile>> Creators = new()
        {
            //결투자 : 거리를 재다가 박에 맞춰 베고, 받아치는 근접 보스
            { "Duelist", () => new DuelistProfile() },
        };
        #endregion

        #region Public
        /// <summary>등록된 보스 이름인지. 빈 이름은 보스가 아니라는 뜻이다</summary>
        public static bool IsRegistered(string bossName)
            => !string.IsNullOrEmpty(bossName) && Creators.ContainsKey(bossName);

        /// <summary>보스의 설계를 만든다. 보스마다 패턴의 진행 상태를 따로 들고 있어야 해서 부를 때마다 새로 만든다. 없는 이름이면 false</summary>
        public static bool TryCreate(string bossName, out BossProfile profile)
        {
            profile = null;
            if (!IsRegistered(bossName)) return false;

            profile = Creators[bossName]();
            return true;
        }
        #endregion
    }
}
