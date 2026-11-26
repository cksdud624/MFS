using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 톤 잇기 계산: 두 색 사이 중간색을 사람 눈에 고르게(Oklab) · 색조 이동(어두운 쪽 차갑게 · 밝은 쪽 따뜻하게) · 팔레트에 맞추기.
    /// 도트 편집기의 ‘톤 잇기’ 브러시와 색 패널의 ‘색 계단’ 이 같이 쓴다. 설정은 PlayerPrefs.
    /// </summary>
    public static class ToneMix
    {
        const string P = "mori.editor.tone.";
        /// <summary>0 = 중간색 · 1 = 디더 · 2 = 명도 잇기.</summary>
        public static int Mode { get => Mathf.Clamp(PlayerPrefs.GetInt(P + "mode", 0), 0, 2); set => PlayerPrefs.SetInt(P + "mode", ((value % 3) + 3) % 3); }
        /// <summary>명도 잇기: 밝기 단계(3 · 4 · 5) · 단계 사이 디더 · 중간 톤 채도 살리기.</summary>
        public static int LumLevels { get => Mathf.Clamp(PlayerPrefs.GetInt(P + "lumLevels", 4), 3, 5); set => PlayerPrefs.SetInt(P + "lumLevels", Mathf.Clamp(value, 3, 5)); }
        public static bool LumDither { get => PlayerPrefs.GetInt(P + "lumDither", 1) == 1; set => PlayerPrefs.SetInt(P + "lumDither", value ? 1 : 0); }
        public static bool LumChroma { get => PlayerPrefs.GetInt(P + "lumChroma", 0) == 1; set => PlayerPrefs.SetInt(P + "lumChroma", value ? 1 : 0); }

        static readonly float[] BAYER4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        /// <summary>4×4 바이어 문턱(0 ~ 1).</summary>
        public static float Bayer(int x, int y) => (BAYER4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
        /// <summary>중간색 단계(1 ~ 3).</summary>
        public static int Steps { get => Mathf.Clamp(PlayerPrefs.GetInt(P + "steps", 2), 1, 3); set => PlayerPrefs.SetInt(P + "steps", Mathf.Clamp(value, 1, 3)); }
        /// <summary>디더 농도(25 · 50 · 75 %).</summary>
        public static int Dither { get { int v = PlayerPrefs.GetInt(P + "dither", 50); return v <= 25 ? 25 : v >= 75 ? 75 : 50; } set => PlayerPrefs.SetInt(P + "dither", value); }
        /// <summary>색조 이동(도, 0 = 끔).</summary>
        public static float HueShift { get => PlayerPrefs.GetFloat(P + "hue", 12f); set => PlayerPrefs.SetFloat(P + "hue", Mathf.Clamp(value, 0, 45)); }
        /// <summary>팔레트에 있는 색으로만.</summary>
        public static bool Snap { get => PlayerPrefs.GetInt(P + "snap", 0) == 1; set => PlayerPrefs.SetInt(P + "snap", value ? 1 : 0); }

        // ───────── Oklab ─────────
        static float Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float Gam(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(Mathf.Max(0, c), 1 / 2.4f) - 0.055f;

        public static Vector3 ToLab(Color32 c)
        {
            float r = Lin(c.r / 255f), g = Lin(c.g / 255f), b = Lin(c.b / 255f);
            float l = Mathf.Pow(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b, 1 / 3f);
            float m = Mathf.Pow(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b, 1 / 3f);
            float s = Mathf.Pow(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b, 1 / 3f);
            return new Vector3(0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                               1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                               0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
        }

        public static Color32 FromLab(Vector3 lab, byte a)
        {
            float l = lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z;
            float m = lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z;
            float s = lab.x - 0.0894841775f * lab.y - 1.2914855480f * lab.z;
            l = l * l * l; m = m * m * m; s = s * s * s;
            float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
            float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
            byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(Gam(Mathf.Clamp01(v)) * 255f), 0, 255);
            return new Color32(B(r), B(g), B(b), a);
        }

        /// <summary>a → b 사이 t(0 ~ 1) 색: Oklab 보간 + 색조 이동(가운데일수록 크게 — 어두우면 차가운 쪽 · 밝으면 따뜻한 쪽).</summary>
        public static Color32 Mix(Color32 a, Color32 b, float t, float hueShiftDeg)
        {
            Vector3 la = ToLab(a), lb = ToLab(b);
            var m = Vector3.Lerp(la, lb, t);
            if (hueShiftDeg > 0.01f)
            {
                float C = Mathf.Sqrt(m.y * m.y + m.z * m.z), h = Mathf.Atan2(m.z, m.y) * Mathf.Rad2Deg;
                bool darker = la.x < lb.x ? t < 0.5f : t > 0.5f;                     // 어두운 끝에 가까운 중간색인지
                float target = darker ? 265f : 75f;                                   // 차가운(파랑) · 따뜻한(노랑) 쪽
                float d = Mathf.DeltaAngle(h, target);
                float amt = hueShiftDeg * 4f * t * (1 - t);
                h += Mathf.Clamp(d, -amt, amt);
                // 채도가 거의 없는 회색은 색조를 틀어도 티가 안 나므로 아주 조금 채도를 줌
                C = Mathf.Max(C, 0.012f * (amt / Mathf.Max(1f, hueShiftDeg)));
                m.y = C * Mathf.Cos(h * Mathf.Deg2Rad); m.z = C * Mathf.Sin(h * Mathf.Deg2Rad);
            }
            byte al = (byte)Mathf.RoundToInt(Mathf.Lerp(a.a, b.a, t));
            return FromLab(m, al);
        }

        /// <summary>a → b 사이 중간색 n 개(양 끝 제외).</summary>
        public static List<Color32> Ramp(Color32 a, Color32 b, int n, float hueShiftDeg)
        {
            var o = new List<Color32>();
            for (int k = 1; k <= n; k++) o.Add(Mix(a, b, k / (float)(n + 1), hueShiftDeg));
            return o;
        }

        /// <summary>팔레트에서 가장 가까운 색(Oklab 거리). 팔레트가 비었으면 그대로.</summary>
        public static Color32 Nearest(Color32 c, IReadOnlyList<Color32> pal)
        {
            if (pal == null || pal.Count == 0) return c;
            var lc = ToLab(c);
            float best = float.MaxValue; Color32 bc = c;
            foreach (var p in pal)
            {
                if (p.a == 0) continue;
                float d = (ToLab(p) - lc).sqrMagnitude;
                if (d < best) { best = d; bc = p; }
            }
            bc.a = c.a;
            return bc;
        }
    }
}
