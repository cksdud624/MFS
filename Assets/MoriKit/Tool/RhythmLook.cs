using System;
using System.Collections.Generic;
using Mori.Core.Rhythm;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    // Mori 리듬 화면 색 · 노트 모양(Rhythm/RhythmDraw.cs 에서 옮김) — 킷 노트 에디터가 씀
    /// <summary>리듬 화면 색·모양 공통.</summary>
    public static class RhythmLook
    {
        public static readonly Color CS = new Color(0.20f, 0.84f, 1.00f);     // S: 청록
        public static readonly Color CD = new Color(1.00f, 0.31f, 0.64f);     // D: 자홍
        public static readonly Color CH = new Color(0.94f, 0.73f, 0.04f);     // 훅(스페이스): 금색
        public static readonly Color CF = new Color(1.00f, 0.35f, 0.21f);     // 피니시(엔터): 불꽃 주황
        public static readonly Color CMiss = new Color(1.00f, 0.30f, 0.36f);
        public static Color Of(int k) => k == Lane.S ? CS : k == Lane.D ? CD : k == Lane.HOOK ? CH : CF;
        public static Color A(Color c, float a) { c.a = a; return c; }

        /// <summary>레인 가로 위치(0~1): S 왼쪽 · D 가운데 · 스페이스(훅) 오른쪽, 세 칸 같은 폭. 피니시는 전체(노트로는 안 씀).</summary>
        public static void LaneSpan(int k, out float x0, out float x1)
        {
            const float a = 1f / 3f, b = 2f / 3f;
            switch (k)
            {
                case Lane.S: x0 = 0f; x1 = a; break;
                case Lane.D: x0 = a; x1 = b; break;
                case Lane.HOOK: x0 = b; x1 = 1f; break;
                default: x0 = 0f; x1 = 1f; break;
            }
        }

        public static void Circle(Painter2D p, Vector2 c, float r)
        {
            p.BeginPath();
            p.Arc(c, r, new Angle(0, AngleUnit.Degree), new Angle(360, AngleUnit.Degree));
            p.ClosePath();
        }

        public static void RRect(Painter2D p, float x, float y, float w, float h, float r)
        {
            r = Mathf.Max(0.1f, Mathf.Min(r, Mathf.Min(w, h) / 2));
            p.BeginPath();
            p.MoveTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x + w - r, y)); p.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + r), r);
            p.LineTo(new Vector2(x + w, y + h - r)); p.ArcTo(new Vector2(x + w, y + h), new Vector2(x + w - r, y + h), r);
            p.LineTo(new Vector2(x + r, y + h)); p.ArcTo(new Vector2(x, y + h), new Vector2(x, y + h - r), r);
            p.LineTo(new Vector2(x, y + r)); p.ArcTo(new Vector2(x, y), new Vector2(x + r, y), r);
            p.ClosePath();
        }

        /// <summary>
        /// 노트 하나 그리기(게임·에디터 공통). S · D = 같은 크기의 둥근 막대, 스페이스(훅) = 원(더 크고 테두리 두 겹).
        /// 차트 위에 떠 있어도 잘 보이게: 어두운 그림자 → 진한 색 → 흰 윤곽/광택. alpha 는 지나간·다음 구간 노트용.
        /// </summary>
        public static void Note(Painter2D p, int k, float x0, float x1, float yc, float h, float alpha = 1, float pulse = 0)
        {
            var c = A(Of(k), alpha);
            float w = x1 - x0;
            var shadow = new Color(0, 0, 0, 0.6f * alpha);
            if (k == Lane.HOOK)
            {
                float r = Mathf.Min(w * 0.3f, h * 1.15f);
                var ctr = new Vector2((x0 + x1) / 2, yc);
                p.fillColor = shadow; Circle(p, ctr + new Vector2(0, 2), r + 4); p.Fill();
                p.fillColor = A(c, 0.28f * alpha); Circle(p, ctr, r + 7 + pulse * 4); p.Fill();
                p.fillColor = c; Circle(p, ctr, r); p.Fill();
                p.strokeColor = A(Color.white, 0.95f * alpha); p.lineWidth = 2.5f; Circle(p, ctr, r - 1.5f); p.Stroke();
                p.fillColor = A(new Color(0.08f, 0.06f, 0.02f), 0.85f * alpha); Circle(p, ctr, r * 0.38f); p.Fill();
                p.fillColor = A(Color.white, 0.85f * alpha); Circle(p, ctr + new Vector2(-r * 0.35f, -r * 0.4f), r * 0.16f); p.Fill();
                return;
            }
            if (k == Lane.FINISH)
            {
                float hh = h * 1.5f;
                p.fillColor = c;
                RRect(p, x0 + 2, yc - hh / 2, w - 4, hh, 7); p.Fill();
                return;
            }
            float pad = w * 0.1f;
            p.fillColor = shadow;
            RRect(p, x0 + pad - 2, yc - h / 2 + 1, w - 2 * pad + 4, h + 4, 7); p.Fill();
            p.fillColor = c;
            RRect(p, x0 + pad, yc - h / 2, w - 2 * pad, h, 6); p.Fill();
            p.strokeColor = A(Color.white, 0.9f * alpha); p.lineWidth = 1.5f;
            RRect(p, x0 + pad + 0.75f, yc - h / 2 + 0.75f, w - 2 * pad - 1.5f, h - 1.5f, 5.5f); p.Stroke();
            p.fillColor = A(Color.white, 0.75f * alpha);
            RRect(p, x0 + pad + 6, yc - h / 2 + 3, w - 2 * pad - 12, 3, 1.5f); p.Fill();
        }
    }
}
