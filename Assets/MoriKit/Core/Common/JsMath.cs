using System;

namespace Mori.Core
{
    /// <summary>
    /// JS(V8)와 같은 결과를 내기 위한 수학 도우미. 명세서 2.2·2.3.
    /// C# Math.Round 의 기본값(은행가 반올림)은 JS Math.round 와 다르므로 절대 직접 쓰지 않는다.
    /// </summary>
    public static class JsMath
    {
        /// <summary>JS Math.round: .5 는 +∞ 쪽으로 (−2.5 → −2).</summary>
        public static double Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        /// <summary>소수 8자리 반올림 (은행·엔진 금액).</summary>
        public static double R8(double x) => Round(x * 1e8) / 1e8;

        /// <summary>소수 2자리 반올림 (살롱·게임).</summary>
        public static double R2(double x) => Round(x * 100) / 100;

        /// <summary>floor(x×100)/100 — 한도·잔고 표시.</summary>
        public static double Floor2(double x) => Math.Floor(x * 100) / 100;

        /// <summary>ceil(x×100)/100 — 빚 표시.</summary>
        public static double Ceil2(double x) => Math.Ceiling(x * 100) / 100;

        /// <summary>JS Number.prototype.toFixed(n) 뒤 숫자로 되돌린 값(+x.toFixed(n)). 표시용 값에만 쓴다.</summary>
        public static double Fixed(double x, int digits) => Math.Round(x, digits, MidpointRounding.AwayFromZero);

        /// <summary>JS ToInt32: double 을 2^32 로 감은 부호 있는 32비트 정수.</summary>
        public static int ToInt32(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return 0;
            double t = Math.Truncate(x);
            double m = t % 4294967296.0;
            if (m < 0) m += 4294967296.0;
            return unchecked((int)(uint)m);
        }

        /// <summary>JS ToUint32.</summary>
        public static uint ToUint32(double x) => unchecked((uint)ToInt32(x));

        /// <summary>JS Math.imul (비트 결과를 uint 로 돌려준다).</summary>
        public static uint Imul(uint a, uint b) => unchecked(a * b);

        public static double Clamp(double x, double lo, double hi) => x < lo ? lo : x > hi ? hi : x;

        /// <summary>JS Math.max(a, b) — NaN 처리는 신경 쓰지 않는다(규칙 코드에는 NaN 이 들어오지 않는다).</summary>
        public static double Max(double a, double b) => a > b ? a : b;
        public static double Min(double a, double b) => a < b ? a : b;

        /// <summary>정수 나눗셈의 JS 식 floor (음수에서도 내림).</summary>
        public static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }
    }
}
