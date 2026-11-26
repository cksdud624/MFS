using System;
using System.Collections.Generic;
using Mori.Core.Rhythm;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 노트 에디터의 시간축(세로, 위가 미래 — 게임과 같은 방향).
    ///  [마디·시간 눈금 64px] [음파 110px] [  S  |  D  | ● SPACE ] — 레인은 한 칸 최대 150px 로 가운데에 모은다(넓은 화면에서 늘어나지 않게).
    /// 맨 위 30px 은 레인 이름 머리줄. 재생선은 아래쪽 78% 높이. 글자는 이 칸 밖으로 나가지 않게 잘라 낸다.
    /// </summary>
    public sealed class EditorField : VisualElement
    {
        public RhythmChart Chart;
        public double ViewT;                     // 재생선이 가리키는 곡 시각(초)
        public float Pps = 240;                  // 1초 = 몇 px
        public float[] Wave;                     // 10ms 마다 최대 진폭(0~1)
        public const double WAVE_DT = 0.01;
        public int SnapDiv = 2;                  // 한 박을 몇으로 나눌지(0 = 자유) — 8분이 기본(자동 생성도 8분 그루브)
        public bool Recording, Playing;
        public int HoverLane = -1;
        public readonly float[] Flash = new float[3];   // 키를 누른 레인 빛(재생선 자리)
        public double HoverT;

        public const float RULER = 64, WAVE = 110, HEAD = 30, LANE_MAX = 150;
        public const float GUTTER = RULER + WAVE;
        static readonly int[] ORDER = { Lane.S, Lane.D, Lane.HOOK };
        static readonly string[] HEAD_TX = { "S", "D", "● SPACE" };
        readonly List<Label> labels = new List<Label>();
        readonly Label[] heads = new Label[3];

        public EditorField()
        {
            generateVisualContent += Draw;
            style.overflow = Overflow.Hidden;
            for (int i = 0; i < 3; i++)
            {
                var h = new Label(HEAD_TX[i]) { pickingMode = PickingMode.Ignore };
                h.style.position = Position.Absolute;
                h.style.top = 0;
                h.style.height = HEAD;
                h.style.unityTextAlign = TextAnchor.MiddleCenter;
                h.style.fontSize = 14;
                h.style.unityFontStyleAndWeight = FontStyle.Bold;
                h.style.color = RhythmLook.Of(ORDER[i]);
                h.style.whiteSpace = WhiteSpace.NoWrap;
                Add(h);
                heads[i] = h;
            }
        }

        public float PlayY => HEAD + (contentRect.height - HEAD) * 0.78f;
        public double TimeAt(float y) => ViewT + (PlayY - y) / Pps;
        public float YOf(double t) => PlayY - (float)((t - ViewT) * Pps);

        float LaneW => Mathf.Min(LANE_MAX, Mathf.Max(60, (contentRect.width - GUTTER - 24) / 3f));
        float LanesX0 => GUTTER + Mathf.Max(12, (contentRect.width - GUTTER - LaneW * 3) / 2f);

        /// <summary>x 위치의 레인(없으면 −1, 왼쪽 눈금·음파 칸이면 −2).</summary>
        public int LaneAt(float x)
        {
            if (x < GUTTER) return -2;
            float a = LanesX0, w = LaneW;
            for (int i = 0; i < 3; i++)
            {
                if (x >= a + i * w && x < a + (i + 1) * w) return ORDER[i];
            }
            return -1;
        }

        public void LaneX(int k, out float x0, out float x1)
        {
            float a = LanesX0, w = LaneW;
            for (int i = 0; i < 3; i++)
                if (ORDER[i] == k) { x0 = a + i * w; x1 = x0 + w; return; }
            x0 = x1 = 0;
        }

        public double SnapT(double t) => SnapDiv > 0 && Chart != null ? Chart.Snap(t, SnapDiv) : Math.Max(0, t);

        Label Lab(int i)
        {
            while (labels.Count <= i)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute;
                l.style.whiteSpace = WhiteSpace.NoWrap;
                l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
                l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
                Insert(0, l);
                labels.Add(l);
            }
            return labels[i];
        }

        /// <summary>마디 번호 · 게이트 이름 · 재생선 시각 글자, 레인 머리줄 위치(매 프레임).</summary>
        public void UpdateLabels()
        {
            int used = 0;
            var r = contentRect;
            for (int i = 0; i < 3; i++)
            {
                LaneX(ORDER[i], out float x0, out float x1);
                heads[i].style.left = x0;
                heads[i].style.width = x1 - x0;
                heads[i].BringToFront();
            }
            if (Chart != null && r.height > 10)
            {
                double t0 = TimeAt(r.height), t1 = TimeAt(HEAD);
                double bar = Chart.Bar, off = Chart.OffsetMs / 1000.0;
                // 확대가 작으면 마디 번호를 건너뛰어 겹치지 않게
                int every = Math.Max(1, (int)Math.Ceiling(34 / Math.Max(1, bar * Pps)));
                int b0 = (int)Math.Floor((t0 - off) / bar), b1 = (int)Math.Ceiling((t1 - off) / bar);
                for (int bi = Math.Max(0, b0); bi <= b1 && used < 80; bi++)
                {
                    if (bi % every != 0) continue;
                    double bt = off + bi * bar;
                    float y = YOf(bt);
                    if (y < HEAD + 8 || y > r.height - 4) continue;
                    var l = Lab(used++);
                    l.text = $"{bi + 1}";
                    l.style.left = 8; l.style.top = y - 19;
                    l.style.fontSize = 13; l.style.unityFontStyleAndWeight = FontStyle.Bold;
                    l.style.color = new Color(0.92f, 0.93f, 0.94f);
                    l.style.display = DisplayStyle.Flex;
                    var l2 = Lab(used++);
                    l2.text = $"{bt:0.00}s";
                    l2.style.left = 26; l2.style.top = y - 17;
                    l2.style.fontSize = 10; l2.style.unityFontStyleAndWeight = FontStyle.Normal;
                    l2.style.color = new Color(0.6f, 0.64f, 0.7f);
                    l2.style.display = DisplayStyle.Flex;
                }
                for (int g = 0; g < Chart.Gates.Count && used < 90; g++)
                {
                    float y = YOf(Chart.Gates[g]);
                    if (y < HEAD + 10 || y > r.height - 4) continue;
                    var l = Lab(used++);
                    l.text = $"GATE {g + 1}  ·  PHASE {g + 1} → {g + 2}";
                    LaneX(Lane.S, out float gx, out _);
                    l.style.left = gx + 6; l.style.top = y - 18;
                    l.style.fontSize = 11; l.style.unityFontStyleAndWeight = FontStyle.Bold;
                    l.style.color = new Color(1f, 0.6f, 0.66f);
                    l.style.display = DisplayStyle.Flex;
                }
                for (int ph = 0; ph < Chart.PhaseCount && used < 100; ph++)
                {
                    float yb = YOf(Chart.PhaseStart(ph));
                    float ya = YOf(Chart.PhaseEnd(ph));
                    float y = Mathf.Min(yb, r.height - 4) - 20;           // 구간 시작(아래쪽) 바로 위
                    if (y < Mathf.Max(HEAD + 4, ya)) continue;
                    var l = Lab(used++);
                    l.text = $"PHASE {ph + 1}";
                    l.style.left = RULER + 6; l.style.top = y;
                    l.style.fontSize = 12; l.style.unityFontStyleAndWeight = FontStyle.Bold;
                    l.style.color = PhaseColor(ph);
                    l.style.display = DisplayStyle.Flex;
                }
                var pl = Lab(used++);
                pl.text = $"▶ {ViewT:0.000}s";
                pl.style.left = 6; pl.style.top = PlayY + 4;
                pl.style.fontSize = 12; pl.style.unityFontStyleAndWeight = FontStyle.Bold;
                pl.style.color = new Color(0.99f, 0.84f, 0.21f);
                pl.style.display = DisplayStyle.Flex;
            }
            for (int i = used; i < labels.Count; i++) labels[i].style.display = DisplayStyle.None;
        }

        static readonly Color[] PH = { new Color(0.2f, 0.84f, 1f), new Color(0.94f, 0.73f, 0.04f), new Color(1f, 0.42f, 0.5f), new Color(0.72f, 0.45f, 1f), new Color(0.06f, 0.8f, 0.5f) };
        public static Color PhaseColor(int i) => PH[i % PH.Length];

        static void Rect(Painter2D p, float x0, float y0, float x1, float y1)
        {
            p.BeginPath(); p.MoveTo(new Vector2(x0, y0)); p.LineTo(new Vector2(x1, y0)); p.LineTo(new Vector2(x1, y1)); p.LineTo(new Vector2(x0, y1)); p.ClosePath();
        }

        static void HLine(Painter2D p, float x0, float x1, float y)
        {
            p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            if (r.width < 200 || r.height < 120 || Chart == null) return;
            var p = mgc.painter2D;
            float W = r.width, H = r.height;
            LaneX(Lane.S, out float lx0, out _);
            LaneX(Lane.HOOK, out _, out float lx1);
            double t0 = TimeAt(H), t1 = TimeAt(HEAD);

            // 바탕: 전체 어둡게, 눈금 칸·음파 칸·레인 영역을 구분
            p.fillColor = new Color(0.03f, 0.035f, 0.045f); Rect(p, 0, 0, W, H); p.Fill();
            p.fillColor = new Color(0.055f, 0.063f, 0.078f); Rect(p, 0, 0, RULER, H); p.Fill();
            p.fillColor = new Color(0.042f, 0.05f, 0.064f); Rect(p, RULER, 0, GUTTER, H); p.Fill();
            p.fillColor = new Color(0.06f, 0.068f, 0.082f); Rect(p, lx0, 0, lx1, H); p.Fill();

            // 구간 바탕(번갈아 옅게)
            for (int ph = 1; ph < Chart.PhaseCount; ph += 2)
            {
                float ya = Mathf.Max(HEAD, YOf(Chart.PhaseEnd(ph))), yb = Mathf.Min(H, YOf(Chart.PhaseStart(ph)));
                if (yb <= ya) continue;
                p.fillColor = new Color(1, 1, 1, 0.045f); Rect(p, lx0, ya, lx1, yb); p.Fill();
            }
            // 구간 색 띠(음파 칸 오른쪽 가장자리): 구간마다 색이 바뀐다 — 게이트가 곡을 어떻게 나눴는지 한눈에
            for (int ph = 0; ph < Chart.PhaseCount; ph++)
            {
                float ya = Mathf.Max(HEAD, YOf(Chart.PhaseEnd(ph))), yb = Mathf.Min(H, YOf(Chart.PhaseStart(ph)));
                if (yb <= ya) continue;
                p.fillColor = PhaseColor(ph);
                Rect(p, GUTTER - 7, ya, GUTTER - 2, yb); p.Fill();
            }
            // 레인 칸: 아주 옅은 색 + 가장자리 색 선
            for (int i = 0; i < 3; i++)
            {
                LaneX(ORDER[i], out float x0, out float x1);
                var c = RhythmLook.Of(ORDER[i]);
                p.fillColor = RhythmLook.A(c, HoverLane == ORDER[i] ? 0.08f : 0.03f);
                Rect(p, x0, HEAD, x1, H); p.Fill();
                p.strokeColor = RhythmLook.A(c, 0.28f);
                p.lineWidth = 1;
                p.BeginPath(); p.MoveTo(new Vector2(x0 + 0.5f, HEAD)); p.LineTo(new Vector2(x0 + 0.5f, H)); p.Stroke();
            }
            p.strokeColor = RhythmLook.A(RhythmLook.CH, 0.28f);
            p.BeginPath(); p.MoveTo(new Vector2(lx1 - 0.5f, HEAD)); p.LineTo(new Vector2(lx1 - 0.5f, H)); p.Stroke();

            // 음파(가운데 기준 좌우 대칭, 옅게)
            if (Wave != null && Wave.Length > 0)
            {
                p.strokeColor = new Color(0.45f, 0.6f, 0.95f, 0.38f);
                p.lineWidth = 1.5f;
                float mid = RULER + WAVE / 2, half = WAVE * 0.42f;
                for (float y = HEAD; y < H; y += 3)
                {
                    int i = (int)(TimeAt(y) / WAVE_DT);
                    if (i < 0 || i >= Wave.Length) continue;
                    float a = Wave[i] * half;
                    if (a < 0.8f) continue;
                    p.BeginPath(); p.MoveTo(new Vector2(mid - a, y)); p.LineTo(new Vector2(mid + a, y)); p.Stroke();
                }
            }

            // 박자 격자(마디 진하게 · 박 중간 · 쪼갠 칸 옅게)
            double beat = Chart.Beat, off = Chart.OffsetMs / 1000.0;
            int div = SnapDiv > 0 ? SnapDiv : 1;
            double step = beat / div;
            long s0 = (long)Math.Floor((t0 - off) / step), s1 = (long)Math.Ceiling((t1 - off) / step);
            if (s1 - s0 < 3000)
                for (long si = Math.Max(0, s0); si <= s1; si++)
                {
                    float y = YOf(off + si * step);
                    if (y < HEAD) continue;
                    bool isBar = si % (div * Chart.BeatsPerBar) == 0, isBeat = si % div == 0;
                    p.strokeColor = new Color(1, 1, 1, isBar ? 0.32f : isBeat ? 0.13f : 0.05f);
                    p.lineWidth = isBar ? 1.5f : 1;
                    HLine(p, isBar ? 0 : lx0, lx1, y);
                }
            // 곡 끝
            float ye = YOf(Chart.Length);
            if (ye > HEAD)
            {
                p.fillColor = new Color(0, 0, 0, 0.45f); Rect(p, lx0, HEAD, lx1, Mathf.Min(H, ye)); p.Fill();
                p.strokeColor = new Color(1, 1, 1, 0.5f); p.lineWidth = 2; HLine(p, 0, lx1, ye);
            }
            // 게이트: 굵은 선 + 빛 번짐 + 양끝 마름모
            foreach (var g in Chart.Gates)
            {
                float y = YOf(g);
                if (y < HEAD || y > H) continue;
                var gc = new Color(1f, 0.42f, 0.5f);
                p.strokeColor = RhythmLook.A(gc, 0.18f); p.lineWidth = 12; HLine(p, RULER, lx1, y);
                p.strokeColor = gc; p.lineWidth = 3; HLine(p, RULER, lx1, y);
                foreach (var x in new[] { lx0 - 8, lx1 + 8 })
                {
                    p.fillColor = gc;
                    p.BeginPath(); p.MoveTo(new Vector2(x, y - 7)); p.LineTo(new Vector2(x + 7, y)); p.LineTo(new Vector2(x, y + 7)); p.LineTo(new Vector2(x - 7, y)); p.ClosePath(); p.Fill();
                }
            }
            // 노트
            foreach (var n in Chart.Notes)
            {
                if (n.K == Lane.FINISH || n.T < t0 - 0.1 || n.T > t1 + 0.1) continue;
                LaneX(n.K, out float x0, out float x1);
                bool past = Playing && n.T < ViewT;
                RhythmLook.Note(p, n.K, x0 + 6, x1 - 6, YOf(n.T), 14, past ? 0.4f : 1f);
            }
            // 마우스 위 미리보기
            if (HoverLane >= 0 && !Playing)
            {
                LaneX(HoverLane, out float x0, out float x1);
                RhythmLook.Note(p, HoverLane, x0 + 6, x1 - 6, YOf(HoverT), 14, 0.35f);
            }
            // 키를 누른 레인: 재생선 자리가 번쩍
            for (int i = 0; i < 3; i++)
            {
                if (Flash[ORDER[i]] <= 0.01f) continue;
                LaneX(ORDER[i], out float x0, out float x1);
                float f = Flash[ORDER[i]];
                p.fillColor = RhythmLook.A(RhythmLook.Of(ORDER[i]), 0.35f * f);
                Rect(p, x0 + 2, PlayY - 26 * f - 6, x1 - 2, PlayY + 6); p.Fill();
            }
            // 재생선
            p.strokeColor = Recording ? new Color(1f, 0.3f, 0.36f) : new Color(0.99f, 0.84f, 0.21f);
            p.lineWidth = 2.5f;
            HLine(p, 0, lx1 + 8, PlayY);
            // 머리줄(레인 이름 칸)
            p.fillColor = new Color(0.08f, 0.09f, 0.11f); Rect(p, 0, 0, W, HEAD); p.Fill();
            for (int i = 0; i < 3; i++)
            {
                LaneX(ORDER[i], out float x0, out float x1);
                p.fillColor = RhythmLook.A(RhythmLook.Of(ORDER[i]), 0.85f);
                Rect(p, x0 + 2, HEAD - 3, x1 - 2, HEAD); p.Fill();
            }
            p.strokeColor = new Color(1, 1, 1, 0.1f); p.lineWidth = 1; HLine(p, 0, W, HEAD);
            if (Recording)
            {
                p.strokeColor = new Color(1f, 0.3f, 0.36f, 0.8f);
                p.lineWidth = 3;
                Rect(p, 1.5f, 1.5f, W - 1.5f, H - 1.5f); p.Stroke();
            }
        }
    }
}
