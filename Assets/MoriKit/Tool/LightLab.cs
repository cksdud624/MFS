using System;
using System.Collections.Generic;
using UnityEngine;
#if MORI_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Mori.Client
{
    /// <summary>
    /// 조명 무대(미리보기 배경): 게임 배경 위에 사이버펑크 실시간 2D 조명(킷 전용)을 얹어 이펙트 · 캐릭터가 빛에 어떻게 보이는지 시험.
    ///  분위기
    ///   · 네온 거리   — 왼쪽 자홍 · 오른쪽 청록 두 톤 역광 + 머리 위 네온관(가끔 지직 꺼짐) + 아래 깔린 보라 반사광
    ///   · 홀로그램    — 거대한 광고 빛이 색을 바꾸며 스캔(위아래로 훑는 띠) · 중간중간 글리치로 끊김
    ///   · 단속        — 빨강 · 파랑 경광등이 번갈아 · 드론 서치라이트가 좁은 원뿔로 훑음
    ///   · 정전        — 거의 깜깜, 깨진 비상 네온만 불규칙하게 · 커서 빛이 주인공
    ///  주변 밝기 · 움직임 · 커서 빛(색 바꾸기) · 번쩍(분위기별: 네온 과부하 · 경광 연타 · 글리치)
    /// 조명은 킷 전용 층(PrefabFx.LAYER)에만 있어 게임 카메라엔 안 보이고, 켜진 동안 장면의 게임 조명은 킷 카메라에서 뺌.
    /// 전역 조명(Global Light 2D)은 장면에 하나만 있어야 해서(같은 레이어 · 블렌드에 둘이면 URP 오류) 주변 밝기도 아주 큰 점광으로 냄.
    /// URP(2D 렌더러) + ‘URP 카메라: 켬’ 일 때만 보여요.
    /// </summary>
    public sealed class LightLab
    {
        public static readonly string[] MOODS = { "네온 거리", "홀로그램", "단속", "정전" };
        public static readonly (string name, Color c)[] CURSOR_COLORS =
        {
            ("흰빛", new Color(1f, 0.96f, 0.9f)), ("자홍", new Color(1f, 0.2f, 0.75f)), ("청록", new Color(0.1f, 0.95f, 1f)), ("산성 노랑", new Color(0.85f, 1f, 0.15f)),
        };

        public bool Active;
        public int Mood = Mathf.Clamp(PlayerPrefs.GetInt("mori.kit.lab.mood2", 0), 0, 3);
        public float Ambient = Mathf.Clamp(PlayerPrefs.GetFloat("mori.kit.lab.amb", 1f), 0f, 3f);
        public bool Moving = PlayerPrefs.GetInt("mori.kit.lab.move", 1) == 1;
        public bool CursorOn = PlayerPrefs.GetInt("mori.kit.lab.cursor", 1) == 1;
        public int CursorColor = Mathf.Clamp(PlayerPrefs.GetInt("mori.kit.lab.ccol", 0), 0, 3);
        public Vector2 Cursor; public bool CursorIn;

        float t, pulse;
        int builtMood = -1;

        public void Save()
        {
            PlayerPrefs.SetInt("mori.kit.lab.mood2", Mood);
            PlayerPrefs.SetFloat("mori.kit.lab.amb", Ambient);
            PlayerPrefs.SetInt("mori.kit.lab.move", Moving ? 1 : 0);
            PlayerPrefs.SetInt("mori.kit.lab.cursor", CursorOn ? 1 : 0);
            PlayerPrefs.SetInt("mori.kit.lab.ccol", CursorColor);
        }

        /// <summary>번쩍: 분위기별 순간 연출(네온 과부하 · 경광 연타 · 글리치 · 비상등 점멸).</summary>
        public void Pulse() { pulse = 1f; }

        // 조명 하나
        enum Kind { Point, Spot, Tube }
        enum Fx { Steady, Buzz, Glitch, Strobe, Scan, Hue, Breathe }
        struct Def
        {
            public Kind kind; public Fx fx;
            public float x, y;           // 화면 기준: x −1(왼쪽 끝) ~ 1(오른쪽 끝), y 0(바닥) ~ 1(위)
            public float r;              // 반지름(화면 높이 비율) · 네온관 = 길이
            public float inten; public Color c, c2;
            public float sway, speed, angle, sweep, phase;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        static Def[] Defs(int mood)
        {
            switch (mood)
            {
                case 1:   // 홀로그램
                    return new[]
                    {
                        new Def { kind = Kind.Point, fx = Fx.Hue, x = 0.05f, y = 0.85f, r = 0.95f, inten = 1.4f, c = Hex("00E5FF"), speed = 0.06f, sway = 0.04f },
                        new Def { kind = Kind.Tube, fx = Fx.Scan, x = 0f, y = 0.5f, r = 2.2f, inten = 1.1f, c = Hex("8CF5FF"), speed = 0.45f },
                        new Def { kind = Kind.Point, fx = Fx.Glitch, x = -0.7f, y = 0.35f, r = 0.45f, inten = 1.2f, c = Hex("FF2BD6"), phase = 1.3f },
                        new Def { kind = Kind.Point, fx = Fx.Breathe, x = 0.75f, y = 0.25f, r = 0.4f, inten = 0.9f, c = Hex("7A5CFF"), speed = 0.5f },
                    };
                case 2:   // 단속
                    return new[]
                    {
                        new Def { kind = Kind.Point, fx = Fx.Strobe, x = -0.55f, y = 0.4f, r = 0.7f, inten = 2.0f, c = Hex("FF1A2E"), speed = 2.2f },
                        new Def { kind = Kind.Point, fx = Fx.Strobe, x = 0.55f, y = 0.4f, r = 0.7f, inten = 2.0f, c = Hex("1A4DFF"), speed = 2.2f, phase = 0.5f },
                        new Def { kind = Kind.Spot, fx = Fx.Steady, x = -0.2f, y = 1.15f, r = 1.3f, inten = 1.6f, c = Hex("E8F4FF"), angle = 16, sweep = 32, speed = 0.55f },
                        new Def { kind = Kind.Tube, fx = Fx.Buzz, x = 0.2f, y = 0.92f, r = 0.9f, inten = 0.7f, c = Hex("FFB000"), phase = 2.1f },
                    };
                case 3:   // 정전
                    return new[]
                    {
                        new Def { kind = Kind.Tube, fx = Fx.Glitch, x = -0.45f, y = 0.78f, r = 0.55f, inten = 1.3f, c = Hex("FF2BD6"), phase = 0.4f },
                        new Def { kind = Kind.Point, fx = Fx.Buzz, x = 0.6f, y = 0.3f, r = 0.35f, inten = 1.0f, c = Hex("FF3B1F"), phase = 3.3f },
                        new Def { kind = Kind.Point, fx = Fx.Breathe, x = 0.0f, y = 0.05f, r = 0.6f, inten = 0.35f, c = Hex("2A1A66"), speed = 0.3f },
                    };
                default:  // 네온 거리: 두 톤 역광 + 네온관 + 바닥 반사
                    return new[]
                    {
                        new Def { kind = Kind.Point, fx = Fx.Breathe, x = -0.95f, y = 0.45f, r = 1.05f, inten = 1.7f, c = Hex("FF2BD6"), speed = 0.35f, sway = 0.03f },
                        new Def { kind = Kind.Point, fx = Fx.Breathe, x = 0.95f, y = 0.45f, r = 1.05f, inten = 1.7f, c = Hex("00E5FF"), speed = 0.31f, sway = 0.03f, phase = 1.1f },
                        new Def { kind = Kind.Tube, fx = Fx.Buzz, x = -0.35f, y = 0.9f, r = 0.75f, inten = 1.3f, c = Hex("FF4FE0"), phase = 0.7f },
                        new Def { kind = Kind.Tube, fx = Fx.Buzz, x = 0.4f, y = 0.82f, r = 0.6f, inten = 1.2f, c = Hex("39FFE9"), phase = 2.9f },
                        new Def { kind = Kind.Point, fx = Fx.Steady, x = 0f, y = -0.15f, r = 0.9f, inten = 0.8f, c = Hex("7A2CFF") },
                        new Def { kind = Kind.Spot, fx = Fx.Steady, x = 0.15f, y = 1.2f, r = 1.25f, inten = 0.55f, c = Hex("C9D8FF"), angle = 22, sweep = 28, speed = 0.3f },
                    };
            }
        }

        static (Color c, float i) AmbientOf(int mood) => mood switch
        {
            1 => (Hex("1B2A55"), 0.45f),
            2 => (Hex("141A33"), 0.3f),
            3 => (Hex("0B0B1A"), 0.08f),
            _ => (Hex("221840"), 0.4f),
        };

        // 네온 지직거림: 대부분 켜져 있다가 가끔 빠르게 꺼졌다 켜짐(실제 네온처럼)
        static float Buzz(float t, float ph)
        {
            float n = Mathf.PerlinNoise(t * 1.3f, ph * 7.1f);
            if (n > 0.78f) return Mathf.PerlinNoise(t * 40f, ph) > 0.5f ? 1f : 0.08f;      // 지직 구간
            return 0.92f + 0.08f * Mathf.PerlinNoise(t * 12f, ph + 3f);
        }
        static float Glitch(float t, float ph)
        {
            float n = Mathf.PerlinNoise(t * 0.9f, ph * 5.3f);
            if (n > 0.6f) return Mathf.Repeat(Mathf.Floor(t * 24f + ph) * 0.618f, 1f) > 0.45f ? 1f : 0f;
            return n > 0.25f ? 1f : 0.15f;
        }

#if MORI_URP
        GameObject root;
        Light2D ambient, cursorLight;
        readonly List<Light2D> lights = new List<Light2D>();
        Def[] defs;
        float[] tubeLen = Array.Empty<float>();

        static System.Reflection.FieldInfo layersField;
        /// <summary>새로 만든 Light2D 의 비출 정렬 레이어 = 모두(내부 칸이라 리플렉션).</summary>
        static void AllLayers(Light2D l)
        {
            layersField ??= typeof(Light2D).GetField("m_ApplyToSortingLayers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (layersField == null) return;
            var ls = SortingLayer.layers;
            var ids = new int[ls.Length];
            for (int i = 0; i < ls.Length; i++) ids[i] = ls[i].id;
            layersField.SetValue(l, ids);
        }

        Light2D Make(string name, Light2D.LightType type, int layer)
        {
            var go = new GameObject(name) { layer = layer, hideFlags = HideFlags.DontSave };
            go.SetActive(false);                                    // 설정을 다 넣은 뒤에 켬
            go.transform.SetParent(root.transform, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = type;
            AllLayers(l);
            go.SetActive(true);
            return l;
        }

        void Build(Transform parent, int layer)
        {
            if (root == null)
            {
                root = new GameObject("Mori LightLab") { hideFlags = HideFlags.DontSave };
                root.transform.SetParent(parent, false);
                // 주변 밝기 = 화면을 다 덮는 아주 큰 점광(전역 조명은 장면에 하나만 있어야 해서)
                ambient = Make("ambient", Light2D.LightType.Point, layer);
                ambient.falloffIntensity = 0f;
                cursorLight = Make("cursor", Light2D.LightType.Point, layer);
            }
            if (builtMood == Mood) return;
            builtMood = Mood;
            foreach (var l in lights) if (l != null) UnityEngine.Object.Destroy(l.gameObject);
            lights.Clear();
            defs = Defs(Mood);
            for (int i = 0; i < defs.Length; i++)
                lights.Add(Make("light" + i, defs[i].kind == Kind.Tube ? Light2D.LightType.Freeform : Light2D.LightType.Point, layer));
            tubeLen = new float[defs.Length];
        }

        static void Tube(Light2D l, float len, float thick)
        {
            float a = len * 0.5f, b = thick * 0.5f;
            l.SetShapePath(new[] { new Vector3(-a, -b), new Vector3(a, -b), new Vector3(a, b), new Vector3(-a, b) });
            l.shapeLightFalloffSize = thick * 2.2f;
        }
#endif

        /// <summary>매 프레임(URP 카메라일 때 PrefabFx 가 부름).</summary>
        public void Update(Transform parent, int layer, Vector3 origin, float cx, float cy, float viewW, float viewH, float dt)
        {
#if MORI_URP
            if (!Active) { Hide(); return; }
            Build(parent, layer);
            if (!root.activeSelf) root.SetActive(true);
            t += dt;
            pulse = Mathf.Max(0, pulse - dt * 1.8f);
            float sp = Moving ? 1 : 0.15f;
            float bottom = cy - viewH * 0.5f;

            // 주변 밝기
            var (ac, ai) = AmbientOf(Mood);
            ambient.transform.position = origin + new Vector3(cx, cy, 0);
            ambient.pointLightOuterRadius = Mathf.Max(viewW, viewH) * 2f;
            ambient.pointLightInnerRadius = Mathf.Max(viewW, viewH) * 1.6f;
            ambient.color = ac;
            ambient.intensity = ai * Ambient * (Mood == 3 && pulse > 0 ? 1 + 4 * pulse : 1);

            for (int i = 0; i < lights.Count && i < defs.Length; i++)
            {
                var l = lights[i]; var d = defs[i];
                if (l == null) continue;
                float x = cx + (d.x + Mathf.Sin(t * Mathf.Max(0.05f, d.speed) * sp + d.phase) * d.sway) * viewW * 0.5f;
                float y = bottom + d.y * viewH;
                float k = 1;
                Color c = d.c;
                switch (d.fx)
                {
                    case Fx.Buzz: k = Buzz(t, d.phase + i); break;
                    case Fx.Glitch: k = Glitch(t, d.phase + i); break;
                    case Fx.Strobe:
                    {
                        // 경광등: 0.5주기마다 두 번 짧게 번쩍(번쩍 버튼 = 연타)
                        float f = Mathf.Repeat(t * d.speed * (pulse > 0 ? 2.5f : 1f) + d.phase, 1f);
                        k = (f < 0.12f || (f > 0.2f && f < 0.32f)) ? 1f : 0.05f;
                        break;
                    }
                    case Fx.Scan:
                    {
                        // 위아래로 훑는 띠(홀로그램 스캔라인)
                        y = bottom + (0.1f + 0.85f * (0.5f + 0.5f * Mathf.Sin(t * d.speed * (Moving ? 1 : 0.2f) * Mathf.PI))) * viewH;
                        k = 0.75f + 0.25f * Glitch(t * 0.7f, i);
                        break;
                    }
                    case Fx.Hue:
                        c = Color.HSVToRGB(Mathf.Repeat(0.5f + Mathf.Sin(t * d.speed * Mathf.Max(0.2f, sp)) * 0.2f, 1f), 0.9f, 1f);   // 청록 ↔ 자홍 사이를 오감
                        break;
                    case Fx.Breathe: k = 0.8f + 0.2f * Mathf.Sin(t * Mathf.Max(0.05f, d.speed) * 2f * Mathf.Max(0.3f, sp) + d.phase); break;
                }
                // 번쩍: 네온 과부하(밝게) · 홀로그램 글리치(끊김) · 정전 비상등
                if (pulse > 0)
                {
                    if (Mood == 0) k *= 1 + 1.8f * pulse;
                    else if (Mood == 1) k *= Mathf.Repeat(Mathf.Floor(t * 30f) * 0.618f, 1f) > 0.4f ? 1 + pulse : 0.1f;
                }
                l.transform.position = origin + new Vector3(x, y, 0);
                l.color = c;
                l.intensity = d.inten * k;
                l.falloffIntensity = 0.55f;
                switch (d.kind)
                {
                    case Kind.Tube:
                        if (Mathf.Abs(tubeLen[i] - d.r * viewH) > 0.01f) { tubeLen[i] = d.r * viewH; Tube(l, d.r * viewH, viewH * 0.022f); }   // 모양은 크기가 바뀔 때만 다시
                        l.transform.rotation = Quaternion.identity;
                        break;
                    case Kind.Spot:
                        l.pointLightOuterRadius = d.r * viewH;
                        l.pointLightInnerRadius = d.r * viewH * 0.1f;
                        l.pointLightOuterAngle = d.angle;
                        l.pointLightInnerAngle = d.angle * 0.35f;
                        l.transform.rotation = Quaternion.Euler(0, 0, 180f + Mathf.Sin(t * d.speed * sp + d.phase) * d.sweep);   // 아래를 향해 좌우로 훑음
                        break;
                    default:
                        l.pointLightOuterRadius = d.r * viewH;
                        l.pointLightInnerRadius = d.r * viewH * 0.12f;
                        l.pointLightOuterAngle = 360; l.pointLightInnerAngle = 360;
                        l.transform.rotation = Quaternion.identity;
                        break;
                }
            }

            // 커서 빛
            bool cur = CursorOn && CursorIn;
            if (cursorLight.gameObject.activeSelf != cur) cursorLight.gameObject.SetActive(cur);
            if (cur)
            {
                cursorLight.transform.position = origin + new Vector3(Cursor.x, Cursor.y, 0);
                cursorLight.color = CURSOR_COLORS[Mathf.Clamp(CursorColor, 0, CURSOR_COLORS.Length - 1)].c;
                cursorLight.pointLightOuterRadius = viewH * 0.38f;
                cursorLight.pointLightInnerRadius = viewH * 0.04f;
                cursorLight.intensity = 1.6f;
                cursorLight.falloffIntensity = 0.5f;
            }
#endif
        }

        /// <summary>URP 카메라가 꺼졌을 때 등: 조명만 숨김(설정은 그대로).</summary>
        public void Hide()
        {
#if MORI_URP
            if (root != null && root.activeSelf) root.SetActive(false);
#endif
        }

        public void Destroy()
        {
#if MORI_URP
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null; lights.Clear(); builtMood = -1;
#endif
        }
    }
}
