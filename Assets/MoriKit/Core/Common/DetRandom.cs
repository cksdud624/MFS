namespace Mori.Core
{
    /// <summary>
    /// 결정적 난수. 저장 시드(seed)가 0 이면 v2.64.2 JS 와 비트 단위로 같은 값을 낸다(명세서 2.3·2.6).
    /// </summary>
    public static class DetRandom
    {
        const double TWO32 = 4294967296.0;

        /// <summary>
        /// 은행 rnd(b, id, salt). JS:
        /// x = imul(id + 1, 2654435761) ^ imul((t0 | 0) + salt, 2246822519) …
        /// Unity: (t0 | 0) 대신 ((t0 | 0) ^ seed).
        /// </summary>
        public static double Bank(long t0, uint seed, int id, int salt = 0)
        {
            int t0i = JsMath.ToInt32(t0);
            int mixed = t0i ^ unchecked((int)seed);
            uint a = JsMath.ToUint32((double)id + 1);
            uint b = JsMath.ToUint32((double)mixed + salt);
            uint x = JsMath.Imul(a, 2654435761u) ^ JsMath.Imul(b, 2246822519u);
            x = JsMath.Imul(x ^ (x >> 15), 2246822507u);
            x = JsMath.Imul(x ^ (x >> 13), 3266489909u);
            return (x ^ (x >> 16)) / TWO32;
        }

        /// <summary>살롱 h01(str): FNV-1a(UTF-16 코드 단위) + 섞기. 0~1.</summary>
        public static double H01(string str)
        {
            uint h = 2166136261u;
            for (int i = 0; i < str.Length; i++)
            {
                h ^= str[i];
                h = JsMath.Imul(h, 16777619u);
            }
            h ^= h >> 15;
            h = JsMath.Imul(h, 2246822507u);
            h ^= h >> 13;
            return h / TWO32;
        }

        /// <summary>시드를 섞은 h01: seed 0 이면 JS 그대로, 아니면 "seed:" 접두어.</summary>
        public static double H01(uint seed, string str) => seed == 0 ? H01(str) : H01(seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + str);

        /// <summary>살롱 smooth(key, x): 정수 격자의 난수를 smoothstep 으로 이은 잡음(−1~1).</summary>
        public static double Smooth(uint seed, string key, double x)
        {
            double fi = System.Math.Floor(x);
            double f = x - fi;
            string i0 = JsNum(fi), i1 = JsNum(fi + 1);
            double a = H01(seed, key + ":" + i0) * 2 - 1;
            double b = H01(seed, key + ":" + i1) * 2 - 1;
            double u = f * f * (3 - 2 * f);
            return a + (b - a) * u;
        }

        /// <summary>정수 값 double 을 JS String(n) 과 같은 문자열로(지수 표기 없이).</summary>
        public static string JsNum(double v)
        {
            if (v == System.Math.Floor(v) && System.Math.Abs(v) < 1e15) return ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
