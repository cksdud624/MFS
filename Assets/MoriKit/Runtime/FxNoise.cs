using UnityEngine;

namespace MoriFx
{
    /// <summary>
    /// 공용 노이즈 텍스처(256², 이어 붙여도 이음새 없음): 침식 · 일렁임 셰이더가 전역 _MoriNoise 로 씀.
    /// 처음 한 번 코드로 만들어 Shader.SetGlobalTexture — 따로 텍스처를 넣지 않아도 된다.
    /// </summary>
    public static class FxNoise
    {
        static Texture2D tex;
        public static Texture2D Texture { get { Ensure(); return tex; } }

        public static void Ensure()
        {
            if (tex != null) { Shader.SetGlobalTexture("_MoriNoise", tex); return; }
            const int N = 256;
            tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true) { name = "MoriNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // r = 부드러운 fbm(침식) · g = 다른 씨앗 fbm(일렁임) · b = 잔 노이즈(가장자리 거칠게)
                    float r = Fbm(x, y, N, 4, 0), g = Fbm(x, y, N, 4, 37), b = Fbm(x, y, N, 2, 91) * 0.5f + Fbm(x, y, N * 4, 1, 5) * 0.5f;
                    px[y * N + x] = new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            Shader.SetGlobalTexture("_MoriNoise", tex);
        }

        // 타일링 되는 값 노이즈 fbm(0 ~ 1)
        static float Fbm(int x, int y, int n, int oct, int seed)
        {
            float sum = 0, amp = 0.5f, tot = 0;
            int cells = 4;
            for (int o = 0; o < oct; o++)
            {
                sum += amp * Value(x * cells / (float)n, y * cells / (float)n, cells, seed + o * 13);
                tot += amp; amp *= 0.5f; cells *= 2;
            }
            return Mathf.Clamp01(sum / tot);
        }

        static float Value(float fx, float fy, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
            float a = H(x0, y0, period, seed), b = H(x0 + 1, y0, period, seed), c = H(x0, y0 + 1, period, seed), d = H(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float H(int x, int y, int p, int seed)
        {
            x = ((x % p) + p) % p; y = ((y % p) + p) % p;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }
    }
}
