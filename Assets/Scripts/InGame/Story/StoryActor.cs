using System;
using Common;

namespace InGame.Story
{
    /// <summary>
    /// 시나리오, 퀘스트, 일반 대화가 대상을 가리키는 키. 표의 Actor 칸에 적는 값 하나로 정해진다.
    /// 플레이어는 하나뿐이라 Player로 적고, 나머지는 Actor 표의 번호로 적는다 (1-2).
    /// 같은 캐릭터를 여럿 세워도 번호가 다르므로, 어느 것을 가리키는지 위치나 순서로 추측하지 않는다
    /// </summary>
    public readonly struct StoryActor : IEquatable<StoryActor>
    {
        #region Fields
        public const string PlayerName = "Player";

        public static readonly StoryActor None = default;
        public static readonly StoryActor Player = new(true, default);

        public bool IsPlayer { get; }
        //Actor 표의 번호. 플레이어면 비어 있다
        public ChapterNumber Number { get; }

        public bool IsNone => !IsPlayer && Number.IsNone;
        #endregion

        #region Init
        private StoryActor(bool isPlayer, ChapterNumber number)
        {
            IsPlayer = isPlayer;
            Number = number;
        }
        #endregion

        #region Public
        public static StoryActor FromNumber(ChapterNumber number) => new(false, number);

        /// <summary>표의 Actor 칸을 읽는다. Player 또는 Actor 표의 번호(1-2)여야 한다</summary>
        public static bool TryParse(string raw, out StoryActor actor)
        {
            actor = None;
            if (raw == PlayerName)
            {
                actor = Player;
                return true;
            }
            if (!ChapterNumber.TryParse(raw, out var number)) return false;

            actor = FromNumber(number);
            return true;
        }

        public bool Equals(StoryActor other) => IsPlayer == other.IsPlayer && Number.Equals(other.Number);
        public override bool Equals(object obj) => obj is StoryActor other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(IsPlayer, Number);

        //로그와 에러에서 어느 표의 값인지 알 수 있게 Actor 번호에는 이름을 붙인다
        public override string ToString() => IsNone ? string.Empty : IsPlayer ? PlayerName : $"Actor {Number}";
        #endregion
    }
}
