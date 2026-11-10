using System;
using System.Globalization;

namespace InGame.Story
{
    /// <summary>
    /// 시나리오, 퀘스트, 일반 대화가 대상을 가리키는 키. 표의 Actor 칸에 적는 값 하나로 정해진다.
    /// 플레이어는 하나뿐이라 Player로 적고, 나머지는 Actor 표의 번호로 적는다 (10001).
    /// 같은 캐릭터를 여럿 세워도 번호가 다르므로, 어느 것을 가리키는지 위치나 순서로 추측하지 않는다
    /// </summary>
    public readonly struct StoryActor : IEquatable<StoryActor>
    {
        #region Fields
        //플레이어를 가리키는 값. 번호 칸의 0은 "사용 안 함"이라 플레이어 번호로 쓸 수 없어서 이름으로 적는다
        public const string PlayerName = "Player";

        public static readonly StoryActor None = default;
        public static readonly StoryActor Player = new(true, 0);

        public bool IsPlayer { get; }
        //Actor 표의 번호. 플레이어면 0이다
        public long Id { get; }

        public bool IsNone => !IsPlayer && Id == 0;
        #endregion

        #region Init
        private StoryActor(bool isPlayer, long id)
        {
            IsPlayer = isPlayer;
            Id = id;
        }
        #endregion

        #region Public
        /// <summary>Actor 표의 번호로 만든다. 표에 있는 번호인지는 검사기가 표끼리 맞춰서 본다</summary>
        public static StoryActor FromId(long id) => new(false, id);

        /// <summary>표의 Actor 칸을 읽는다. Player 또는 1 이상의 번호여야 한다</summary>
        public static bool TryParse(string raw, out StoryActor actor)
        {
            actor = None;
            if (raw == PlayerName)
            {
                actor = Player;
                return true;
            }
            if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0) return false;

            actor = FromId(id);
            return true;
        }

        public bool Equals(StoryActor other) => IsPlayer == other.IsPlayer && Id == other.Id;
        public override bool Equals(object obj) => obj is StoryActor other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(IsPlayer, Id);

        //로그와 에러에서 어느 표의 번호인지 알 수 있게 Actor를 붙인다 (Object 번호, 대화 번호와 헷갈리지 않게)
        public override string ToString() => IsNone ? string.Empty : IsPlayer ? PlayerName : $"Actor {Id}";
        #endregion
    }
}
