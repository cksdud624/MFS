using UnityEngine;

namespace MoriFx
{
    /// <summary>
    /// 트랙 하나의 자세 계산(게임 · 개발킷 공용): 기준점 + 비켜 놓기 + 이동(mx, my) + 늘이기(to) + 흐려지기 · 끝 색.
    /// 재생기(IFxHost)는 이 결과를 그대로 위치 · 회전 · 크기 · 색에 넣으면 된다.
    /// </summary>
    public static class FxMotion
    {
        public struct Pose
        {
            public Vector2 pos;            // 게임 단위(U)
            public float rot;              // 도(뒤집기 반영 끝)
            public bool flip;              // 좌우 뒤집기
            public bool stretched;         // 늘이기(to): pos = 시작점, 오른쪽(+x)으로 len 만큼
            public float len;              // 늘이기 길이(U)
        }

        public static float Ease(string m, float p)
        {
            p = Mathf.Clamp01(p);
            switch (m)
            {
                case "linear": return p;
                case "in": return p * p;
                default: return 1 - (1 - p) * (1 - p);
            }
        }

        /// <summary>prog = 0 ~ 1(길이 중 진행).</summary>
        public static Pose Of(FxTrack k, FxCtx ctx, float prog)
        {
            var o = new Pose();
            o.flip = k.flip && ctx.facing < 0;
            float sg = o.flip ? -1 : 1;
            float e = Ease(k.mease, prog);
            o.pos = ctx.Anchor(k.anchor) + new Vector2((k.x + k.mx * e) * sg, k.y + k.my * e);
            o.rot = o.flip ? -k.rot : k.rot;
            if (!string.IsNullOrEmpty(k.to))
            {
                var d = ctx.Anchor(k.to) - o.pos;
                o.len = d.magnitude;
                o.rot = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + k.rot;
                o.flip = false;
                o.stretched = true;
            }
            return o;
        }

        /// <summary>진하기: alpha × 나타나기 × 사라지기(프레임 기준).</summary>
        public static float Alpha(FxTrack k, float ageSec, float durSec, int fps)
        {
            float a = Mathf.Clamp01(k.alpha);
            float af = ageSec * fps, df = durSec * fps;
            if (k.fadeIn > 0) a *= Mathf.Clamp01(af / k.fadeIn);
            if (k.fadeOut > 0) a *= Mathf.Clamp01((df - af) / k.fadeOut);
            return a;
        }

        /// <summary>색: 시작 색 → 끝 색(color2).</summary>
        public static Color Tint(FxTrack k, float prog) => string.IsNullOrEmpty(k.color2) ? k.Col : UnityEngine.Color.Lerp(k.Col, k.Col2, Mathf.Clamp01(prog));

        /// <summary>트랙 길이(초): frames 가 0 이면 auto(이펙트 자동 길이).</summary>
        public static float Dur(FxCard c, FxTrack k, float auto) => k.frames > 0 ? c.Sec(k.frames) : Mathf.Max(0.05f, auto);
    }
}
