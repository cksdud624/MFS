using System;

namespace Mori.Core
{
    /// <summary>시간 상수(ms).</summary>
    public static class GameTime
    {
        public const long DAY = 86400000L;
        public const long HOUR = 3600000L;
        public const long MINUTE = 60000L;

        /// <summary>
        /// 인게임 하루 번호 = floor(gameNow / DAY). 시간대 보정은 없다(명세서 2.6, Q5).
        /// JS 의 dayKey/dayOf 는 +9h 를 더했지만 게임에서는 쓰지 않는다.
        /// </summary>
        public static long DayOf(long t) => JsMath.FloorDiv(t + CalendarOffset, DAY);

        /// <summary>
        /// 달력 하루(미션·방문·살롱 시세·한정 입고)의 경계 보정(ms). 게임에서는 항상 0.
        /// JS 기준값과 비교하는 테스트에서만 9시간(JS 의 한국 시간 하루)으로 바꿨다가 되돌린다.
        /// 변동금리·기준금리의 날짜는 JS 도 보정 없이 floor(t / DAY) 라서 이 값을 쓰지 않는다.
        /// </summary>
        public static long CalendarOffset = 0;

        /// <summary>보정 없는 날짜 번호 floor(t / DAY) — 변동금리 경계용.</summary>
        public static long UtcDay(long t) => JsMath.FloorDiv(t, DAY);

        /// <summary>(to − from) 을 일 단위 실수로.</summary>
        public static double Days(long from, long to) => (double)(to - from) / DAY;
    }

    /// <summary>
    /// 저장 파일별 인게임 시계(명세서 2.6).
    /// · 처음 GameNow = GAME_EPOCH (= 살롱 시세 기준일 D0 × DAY) 라서 시세 식을 그대로 쓴다.
    /// · 켜 둔 동안과 꺼 둔 동안 모두 실제로 흐른 시간만큼 나아간다.
    /// · 실제 시계가 뒤로 가면 0 으로 본다(시계 조작 방지, 9.3).
    /// 트레이딩 시장은 이 시계를 쓰되 꺼져 있던 구간은 ‘휴장’이다(3단계).
    /// </summary>
    public sealed class GameClock
    {
        public const long D0 = 20760;
        public const long GAME_EPOCH = D0 * GameTime.DAY;

        public uint Seed;
        public long GameNow;
        public long LastRealNow;

        public static GameClock NewGame(uint seed, long realNow)
        {
            return new GameClock { Seed = seed, GameNow = GAME_EPOCH, LastRealNow = realNow };
        }

        /// <summary>실제 시각(Unix ms)을 받아 인게임 시계를 나아가게 하고, 나아간 ms 를 돌려준다.</summary>
        public long Advance(long realNow)
        {
            long d = realNow - LastRealNow;
            if (d < 0) d = 0;
            GameNow += d;
            if (realNow > LastRealNow) LastRealNow = realNow;
            return d;
        }

        /// <summary>
        /// 게임을 켜 둔 동안의 진행(단조 증가 스톱워치로 잰 ms). 벽시계를 쓰지 않으므로 플레이 중 PC 시계를 바꿔도 멈추거나 튀지 않는다.
        /// LastRealNow 도 같이 올려서, 다음 불러오기 때 오프라인 공백을 겹쳐 세지 않는다(명세서 9.3).
        /// </summary>
        public long AdvanceBy(long ms)
        {
            if (ms <= 0) return 0;
            GameNow += ms;
            LastRealNow += ms;
            return ms;
        }

        public long Day => GameTime.DayOf(GameNow);
    }
}
