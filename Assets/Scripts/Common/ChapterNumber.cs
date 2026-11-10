using System;
using System.Globalization;
using static Common.GameDefine;

namespace Common
{
    /// <summary>
    /// 챕터-순번 두 단으로 매기는 번호 (1-1, 1-2, 2-1). 시나리오, 퀘스트, Actor 표가 각자 이 번호를 쓴다.
    /// 표에는 문자열로 적고 읽을 때 숫자 둘로 나눈다. 문자열로 비교하면 1-10이 1-2보다 앞에 오므로 정렬은 숫자로 한다
    /// </summary>
    public readonly struct ChapterNumber : IEquatable<ChapterNumber>, IComparable<ChapterNumber>
    {
        #region Fields
        //줄 Id에 순번을 담을 수 있는 자리 수. 1-1000이면 다음 챕터의 줄 Id와 겹친다
        private const long MaxOrder = StoryLineIdChapter / StoryLineIdOrder - 1;

        public int Chapter { get; }
        public int Order { get; }

        //번호를 적지 않은 값. 챕터와 순번은 1부터라서 0이면 비어 있다
        public bool IsNone => Chapter == 0;
        //이 번호에 속한 줄 Id의 시작. 1-2 → 1002000이고 줄은 1002010부터 매긴다
        public long FirstLineId => Chapter * StoryLineIdChapter + Order * StoryLineIdOrder;
        #endregion

        #region Init
        private ChapterNumber(int chapter, int order)
        {
            Chapter = chapter;
            Order = order;
        }
        #endregion

        #region Public
        /// <summary>"챕터-순번"을 읽는다. 둘 다 1 이상이어야 하고, 순번은 줄 Id에 담을 수 있는 만큼만 받는다</summary>
        public static bool TryParse(string raw, out ChapterNumber number)
        {
            number = default;
            if (string.IsNullOrEmpty(raw)) return false;

            string[] items = raw.Split('-');
            if (items.Length != 2) return false;
            if (!int.TryParse(items[0], NumberStyles.None, CultureInfo.InvariantCulture, out int chapter)
                || !int.TryParse(items[1], NumberStyles.None, CultureInfo.InvariantCulture, out int order))
                return false;
            if (chapter < 1 || order < 1 || order > MaxOrder) return false;

            number = new ChapterNumber(chapter, order);
            return true;
        }

        /// <summary>이 번호의 줄 Id인지. 다른 번호의 범위로 넘어가면 그 번호의 줄과 Id가 겹친다</summary>
        public bool OwnsLineId(long lineId) => lineId > FirstLineId && lineId < FirstLineId + StoryLineIdOrder;

        public int CompareTo(ChapterNumber other)
            => Chapter != other.Chapter ? Chapter.CompareTo(other.Chapter) : Order.CompareTo(other.Order);

        public bool Equals(ChapterNumber other) => Chapter == other.Chapter && Order == other.Order;
        public override bool Equals(object obj) => obj is ChapterNumber other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Chapter, Order);

        //로그와 에러에서 표에 적힌 모양 그대로 보이게 한다
        public override string ToString() => IsNone ? string.Empty : $"{Chapter}-{Order}";
        #endregion
    }
}
