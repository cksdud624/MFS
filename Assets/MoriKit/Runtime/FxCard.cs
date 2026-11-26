using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoriFx
{
    /// <summary>
    /// 연출 카드 한 줄(트랙). 종류마다 쓰는 칸이 다르고, 안 쓰는 칸은 무시한다.
    /// 시간은 프레임(카드의 fps, 기본 60) 단위. 위치는 게임 단위(U), 회전은 도.
    /// </summary>
    [Serializable]
    public sealed class FxTrack
    {
        public const string EFFECT = "effect", SPRITE = "sprite", GHOST = "ghost", HITSTOP = "hitstop", SHAKE = "shake", ZOOM = "zoom", FLASH = "flash",
            GLOW = "glow", CHROMA = "chroma", BLUR = "blur", WAVE = "wave", GRADE = "grade", TILT = "tilt", BODY = "body",
            SHATTER = "shatter", LIGHT = "light", HAZE = "haze";
        public static readonly string[] TYPES = { EFFECT, SPRITE, GHOST, SHATTER, HITSTOP, SHAKE, ZOOM, FLASH, GLOW, CHROMA, BLUR, WAVE, GRADE, TILT, BODY, LIGHT, HAZE };

        public string type = EFFECT;
        public string name = "";           // 보이는 이름(비면 종류 이름)
        public bool on = true;
        public int at;                     // 시작 프레임
        public int frames = 20;            // 길이(프레임). 이펙트는 0 = 자동(파티클이 끝날 때까지)

        // 이펙트(프리팹) · 그림(스프라이트)
        public string prefab = "";         // 이펙트: 등록부 이름
        public string anim = "";           // 그림: 애니메이션 이름(시트 이름)
        public string anchor = "self";     // self 쓰는 쪽 · target 맞는 쪽 · hit 타격 지점 · mid 둘 사이
        public float x, y;                 // 기준점에서 비켜 놓기(U, x 는 바라보는 쪽이 +)
        public float rot;                  // 회전(도)
        public float scale = 1;            // 크기(배)
        public float speed = 1;            // 재생 속도(배)
        public bool flip = true;           // 왼쪽을 보면 좌우 뒤집기
        public bool follow;                // 기준 캐릭터를 따라 움직이기
        public string blend = "add";       // 그림: add 빛(더하기) · alpha 보통
        public string pivot = "feet";      // 그림: feet 캐릭터 그림과 같은 캔버스(발밑 기준) · center 가운데
        public bool loop;                  // 그림: 길이 동안 되풀이
        public float alpha = 1;            // 그림 · 섬광: 진하기
        public string color = "FFFFFF";    // 그림: 색 곱하기 · 섬광: 색

        // 화면(히트스톱 · 흔들림 · 줌 · 섬광)
        public float power = 1;            // 흔들림 px(1080p 기준) · 줌 배(+0.1 = 10% 당김) · 히트스톱: 멈춘 동안 시간 배율(0 = 완전히 멈춤)
        public string curve = "decay";     // 세기 곡선(FxCurve)
        public string dir = "both";        // 흔들림: both · x · y
        public float freq = 30;            // 흔들림: 초당 떨림
        public string who = "both";        // 히트스톱: both 모두(결투 전체) · self 쓰는 쪽만 · target 맞는 쪽만(그 캐릭터 그림만 멈춤)
        public string pts = "";            // 세기 곡선 ‘직접’(curve = custom): "시간,세기;시간,세기 …"(0 ~ 1)
        public int seed = 1;               // 이펙트: 파티클 무작위 씨앗(같은 씨앗 = 언제나 같은 모양, 0 = 매번 다르게)
        // 여러 개 · 무작위 · 방향(이펙트 · 그림)
        public int count = 1;              // 몇 개 띄울지
        public int gap;                    // 하나씩 늦게(프레임) — 차례로 터지기
        public int jat;                    // 시작 무작위(0 ~ 이 프레임만큼 늦게)
        public float jx, jy;               // 위치 무작위(± U)
        public float jrot;                 // 회전 무작위(± 도)
        public float jscale;               // 크기 무작위(± 배, 0.2 = ±20%)
        public string aim = "face";        // face 좌우만(바라보는 쪽) · dir 움직이는 · 치는 방향으로 돌리기(대쉬 8방향 · 위아래 · 대각)

        // 움직임 · 늘이기 · 흐려지기(이펙트 · 그림)
        public float mx, my;               // 길이 동안 이만큼 이동(U, 바라보는 쪽 · 방향 따라 돎) — 참격 날리기
        public string mease = "out";       // 이동 모양: linear 고르게 · out 빠르게 출발 · in 천천히 출발
        public string to = "";             // 늘이기: 이 기준점까지 길게(번개 · 빔). "" = 안 함
        public int fadeIn, fadeOut;        // 그림 · 잔상: 나타나기 · 사라지기(프레임)
        public string color2 = "";         // 그림 · 잔상: 끝 색(비우면 색 그대로)

        // 잔상(캐릭터의 그 순간 모습을 남김)
        public int life = 12;              // 잔상 하나가 남는 프레임(gap = 몇 프레임마다 남길지)

        // 화면 효과(후처리)
        public float radius = 4 * FxCard.LEGACY_U, width = 0.6f * FxCard.LEGACY_U;     // 충격파 · 빛 · 일렁임: 퍼지는 거리 · 띠 두께(U)
        public float sat = 1, invert;              // 색 보정: 채도(배) · 반전(0 ~ 1)

        // 조각내기(shatter): 그림을 격자로 쪼개 날림
        public int cols = 8, rows = 8;             // 조각 수(가로 · 세로)
        public float spin = 360;                   // 조각 회전(도/초, 무작위 ±)
        public float grav = -6 * FxCard.LEGACY_U;  // 조각 중력(U/s², 음수 = 아래로)
        public float shrink = 0.6f;                // 끝까지 작아지는 정도(0 ~ 1)

        // 셰이더 값 곡선(이펙트 · 그림 · 조각): 길이 동안 머티리얼 값을 from → to 로(곡선 모양)
        public List<FxParam> mats = new List<FxParam>();

        /// <summary>길이 값(U · U/s · U/s²)에 s 를 곱함 — 단위 바꾸기. 이펙트(프리팹)는 크기도(프리팹은 제 크기가 U 라서).</summary>
        public void ScaleUnits(float s)
        {
            x *= s; y *= s; jx *= s; jy *= s; mx *= s; my *= s;
            radius *= s; width *= s; grav *= s;
            if (type == SHATTER) power *= s;           // 조각 날아가는 세기(U/초)
            if (type == EFFECT) scale *= s;
        }

        public FxTrack Clone()
        {
            var c = (FxTrack)MemberwiseClone();
            c.mats = new List<FxParam>();
            if (mats != null) foreach (var m in mats) c.mats.Add(m.Clone());
            return c;
        }
        public bool IsScreen => type == HITSTOP || type == SHAKE || type == ZOOM || type == FLASH || type == GLOW || type == CHROMA || type == BLUR || type == WAVE || type == GRADE || type == TILT || type == BODY || type == LIGHT || type == HAZE;
        public bool IsSpawn => type == EFFECT || type == SPRITE || type == GHOST || type == SHATTER;

        public Color Col2
        {
            get
            {
                if (string.IsNullOrEmpty(color2)) return Col;
                return ColorUtility.TryParseHtmlString("#" + color2.TrimStart('#'), out var c) ? c : Col;
            }
        }

        public Color Col
        {
            get
            {
                var c = Color.white;
                if (!string.IsNullOrEmpty(color)) ColorUtility.TryParseHtmlString("#" + color.TrimStart('#'), out c);
                return c;
            }
        }
    }

    /// <summary>셰이더(머티리얼) 값 하나를 트랙 길이 동안 바꾸기: 값 = from + (to − from) × 곡선(진행).</summary>
    [Serializable]
    public sealed class FxParam
    {
        public string name = "_Erode";     // 머티리얼 값 이름(예: _Erode 침식 · _Glow 발광 · _Wobble 일렁임 · _Scroll 흐름)
        public float from, to = 1;
        public string curve = "rise";
        public string pts = "";            // curve = custom 일 때 점
        public FxParam Clone() => (FxParam)MemberwiseClone();

        public float At(float prog)
        {
            float v = curve == "custom" ? FxCurve.EvalPts(FxCurve.Parse(pts), prog) : FxCurve.Eval(curve, prog);
            return from + (to - from) * v;
        }
    }

    /// <summary>연출 카드: 한 순간(공격 · 대쉬 · 카운터 …)에 함께 터지는 것들의 시간표. 파일 하나 = 카드 하나(json).</summary>
    [Serializable]
    public sealed class FxCard
    {
        public string key = "";            // 순간 이름("hero.slash1" …) — 파일 이름과 같음
        public string name = "";           // 보이는 이름
        public string timeUnit = "frame";
        public int fps = 60;
        public int length = 60;            // 카드 길이(프레임) — 트랙이 더 길면 그만큼 늘어남
        public string preview = "";        // 작업실 미리보기에서 캐릭터가 재생할 애니메이션(게임에선 안 씀)
        public int unit = 2;               // 길이 단위 판: 2 = 1U = 그림 100px. 이 칸이 없는 예전 카드(Mori 킷 예전 단위)는 읽을 때 LEGACY_U 를 곱해 바꿈

        /// <summary>예전 Mori 킷 단위(64px 캐릭터 = 3U) → 지금 단위(1U = 100px, 64px = 0.64U).</summary>
        public const float LEGACY_U = 0.64f / 3f;
        public List<FxTrack> tracks = new List<FxTrack>();

        public float Sec(float frame) => frame / Mathf.Max(1, fps);
        public int Frame(float sec) => Mathf.RoundToInt(sec * Mathf.Max(1, fps));

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static FxCard FromJson(string json)
        {
            var c = JsonUtility.FromJson<FxCard>(json);
            if (c == null) return null;
            if (c.tracks == null) c.tracks = new List<FxTrack>();
            foreach (var t in c.tracks) if (t != null && t.mats == null) t.mats = new List<FxParam>();
            if (json.IndexOf("\"unit\"", StringComparison.Ordinal) < 0)       // 예전 단위 카드
            {
                foreach (var t in c.tracks) if (t != null) t.ScaleUnits(LEGACY_U);
                c.unit = 2;
            }
            if (c.fps <= 0) c.fps = 60;
            return c;
        }

        public FxCard Clone() => FromJson(ToJson());
    }

    /// <summary>세기 곡선(0 ~ 1 진행 → 0 ~ 1 세기). 이름 하나로 고르고, 화면 트랙(흔들림 · 줌 · 섬광 · 히트스톱)이 쓴다.</summary>
    public static class FxCurve
    {
        public static readonly (string id, string name)[] ALL =
        {
            ("decay", "확 줄기"), ("punch", "치고 빠지기"), ("linear", "곧게 줄기"), ("flat", "그대로"),
            ("rise", "차오르기"), ("bell", "올랐다 내리기"), ("blink", "깜빡이기"), ("custom", "직접"),
        };

        /// <summary>트랙의 곡선(‘직접’ 이면 점들 사이를 부드럽게 이음).</summary>
        public static float Eval(FxTrack k, float t) => k.curve == "custom" ? EvalPts(Parse(k.pts), t) : Eval(k.curve, t);

        public static List<Vector2> Parse(string pts)
        {
            var o = new List<Vector2>();
            if (!string.IsNullOrEmpty(pts))
                foreach (var part in pts.Split(';'))
                {
                    var xy = part.Split(',');
                    if (xy.Length == 2 && float.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                        && float.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
                        o.Add(new Vector2(Mathf.Clamp01(x), y));
                }
            o.Sort((a, b) => a.x.CompareTo(b.x));
            if (o.Count == 0) { o.Add(new Vector2(0, 1)); o.Add(new Vector2(1, 0)); }
            return o;
        }

        public static string Format(List<Vector2> p)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var parts = new List<string>();
            foreach (var v in p) parts.Add(v.x.ToString("0.###", ci) + "," + v.y.ToString("0.###", ci));
            return string.Join(";", parts);
        }

        /// <summary>프리셋 곡선을 점 n 개로(‘직접’ 으로 바꿔 다듬기 시작할 때).</summary>
        public static string Sample(string id, int n = 6)
        {
            var p = new List<Vector2>();
            for (int i = 0; i < n; i++) { float x = i / (float)(n - 1); p.Add(new Vector2(x, Eval(id, x))); }
            return Format(p);
        }

        public static float EvalPts(List<Vector2> p, float t)
        {
            t = Mathf.Clamp01(t);
            if (p.Count == 0) return 1;
            if (t <= p[0].x) return p[0].y;
            for (int i = 1; i < p.Count; i++)
                if (t <= p[i].x)
                {
                    float w = p[i].x - p[i - 1].x;
                    float u = w > 1e-5f ? (t - p[i - 1].x) / w : 1;
                    u = u * u * (3 - 2 * u);               // 점 사이를 부드럽게
                    return Mathf.Lerp(p[i - 1].y, p[i].y, u);
                }
            return p[p.Count - 1].y;
        }

        public static string NameOf(string id) { foreach (var (i, n) in ALL) if (i == id) return n; return id; }

        public static float Eval(string id, float t)
        {
            t = Mathf.Clamp01(t);
            switch (id)
            {
                case "flat": return 1;
                case "linear": return 1 - t;
                case "rise": return t;
                case "bell": return Mathf.Sin(t * Mathf.PI);
                case "blink": return ((int)(t * 8)) % 2 == 0 ? 1 : 0;
                case "punch": return t < 0.12f ? t / 0.12f : Sq(1 - (t - 0.12f) / 0.88f);
                default: return Sq(1 - t);                        // decay
            }
        }

        static float Sq(float v) => v * v;
    }
}
