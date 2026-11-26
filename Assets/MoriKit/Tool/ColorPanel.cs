using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 색 패널(클립 스튜디오식): 메인 · 서브 · 투명색 칩, 컬러 서클(색상 고리 + 채도 · 명도 사각), 컬러 세트(팔레트), 컬러 슬라이더(RGB · HSV · CMYK + 투명도).
    /// 팔레트는 StreamingAssets/Fight/palette.txt 에 저장(포토샵 · 다른 컴퓨터와 공유 가능한 #RRGGBBAA 줄 목록).
    /// </summary>
    public sealed class ColorPanel
    {
        public readonly VisualElement Root;
        public Color32 Main = new Color32(255, 255, 255, 255), Sub = new Color32(0, 0, 0, 255);
        /// <summary>투명색(지우기)을 고른 상태.</summary>
        public bool UseClear;
        public Color32 Current => UseClear ? new Color32(0, 0, 0, 0) : Main;
        public event Action Changed;

        float h, s = 0, v = 1;          // 메인 색의 HSV(채도 0 일 때도 색상을 기억)
        string mode = "RGB";
        int selSwatch = -1;
        List<Color32> palette = new List<Color32>();
        /// <summary>색 세트(톤 잇기 ‘팔레트에 맞추기’ 용).</summary>
        public IReadOnlyList<Color32> Palette => palette;
        float rampHue = 12f;
        /// <summary>색 계단의 색조 이동(도) — 도트 편집기 톤 잇기 설정과 같이.</summary>
        public void SetRampHue(float deg) => rampHue = deg;

        readonly VisualElement mainChip, subChip, clearChip, ring, sv, ringMark, svMark, setGrid, sliders;
        readonly Texture2D ringTex, svTex;
        readonly Dictionary<string, Button> modeBtns = new Dictionary<string, Button>();
        readonly List<(string name, Texture2D tex, VisualElement track, VisualElement mark, Label val)> rows = new List<(string, Texture2D, VisualElement, VisualElement, Label)>();
        readonly TextField hex;
        const int CIRCLE = 176, RING_IN = 66, RING_OUT = 86, SQ = 88;

        public ColorPanel(VisualElement parent)
        {
            Root = new VisualElement();
            Root.AddToClassList("cp");
            parent.Add(Root);

            // ── 칩: 메인 · 서브 · 투명
            var chips = El(Root, "cp-chips");
            mainChip = Chip(chips, "메인", () => { UseClear = false; Fire(); });
            subChip = Chip(chips, "서브", Swap);
            clearChip = Chip(chips, "투명", () => { UseClear = true; Fire(); });
            clearChip.AddToClassList("cp-chip--clear");
            var sw = new Button(Swap) { text = "⇄", tooltip = "메인 · 서브 바꾸기" }; sw.AddToClassList("ae-btn"); chips.Add(sw);
            hex = new TextField { value = "#FFFFFFFF" };
            hex.AddToClassList("ae-text");
            hex.RegisterCallback<FocusOutEvent>(_ => ApplyHex());
            hex.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) ApplyHex(); });
            chips.Add(hex);

            // ── 컬러 서클
            Title(Root, "컬러 서클");
            var circle = El(Root, "cp-circle");
            circle.style.width = CIRCLE; circle.style.height = CIRCLE;
            ringTex = MakeRing();
            svTex = new Texture2D(48, 48, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            ring = Abs(circle, 0, 0, CIRCLE, CIRCLE); ring.style.backgroundImage = new StyleBackground(ringTex);
            float sq0 = (CIRCLE - SQ) / 2f;
            sv = Abs(circle, sq0, sq0, SQ, SQ); sv.style.backgroundImage = new StyleBackground(svTex);
            ringMark = Abs(circle, 0, 0, 12, 12); ringMark.AddToClassList("cp-mark");
            svMark = Abs(circle, 0, 0, 10, 10); svMark.AddToClassList("cp-mark");
            foreach (var x in new[] { ring, sv, ringMark, svMark }) x.pickingMode = PickingMode.Ignore;
            bool dragRing = false, dragSv = false;
            circle.RegisterCallback<PointerDownEvent>(e =>
            {
                var p = (Vector2)e.localPosition - new Vector2(CIRCLE / 2f, CIRCLE / 2f);
                float d = p.magnitude;
                dragRing = d >= RING_IN - 4 && d <= RING_OUT + 4;
                dragSv = !dragRing && Mathf.Abs(p.x) <= SQ / 2f + 2 && Mathf.Abs(p.y) <= SQ / 2f + 2;
                if (!dragRing && !dragSv) return;
                circle.CapturePointer(e.pointerId);
                CirclePick(p, dragRing);
                e.StopPropagation();
            });
            circle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragRing && !dragSv) return;
                CirclePick((Vector2)e.localPosition - new Vector2(CIRCLE / 2f, CIRCLE / 2f), dragRing);
            });
            circle.RegisterCallback<PointerUpEvent>(e => { dragRing = dragSv = false; circle.ReleasePointer(e.pointerId); });

            // ── 컬러 세트
            Title(Root, "컬러 세트");
            var sb = El(Root, "ae-wrap");
            Btn(sb, "+ 추가", () => { palette.Add(Main); selSwatch = palette.Count - 1; SavePalette(); BuildSet(); }, "지금 메인 색을 세트에 추가");
            Btn(sb, "삭제", () => { if (selSwatch >= 0 && selSwatch < palette.Count) { palette.RemoveAt(selSwatch); selSwatch = -1; SavePalette(); BuildSet(); } }, "고른 칸 빼기(오른쪽 클릭도 됨)");
            Btn(sb, "표준 세트로", () => { palette = Standard(); selSwatch = -1; SavePalette(); BuildSet(); }, "기본 48색으로 되돌리기");
            Btn(sb, "색 계단", () =>
            {
                // 메인 → 보조 사이 중간색 5개를 세트에 추가(사람 눈에 고르게 · 색조 이동은 톤 잇기 설정을 따름)
                foreach (var c in ToneMix.Ramp(Main, Sub, 5, rampHue)) if (!palette.Contains(c)) palette.Add(c);
                selSwatch = -1; SavePalette(); BuildSet();
            }, "메인 색 → 보조 색 사이 중간색 5개를 세트에 추가(톤 잇기 색조 이동 설정을 따름)");
            setGrid = El(Root, "cp-set");

            // ── 컬러 슬라이더
            Title(Root, "컬러 슬라이더");
            var mb = El(Root, "ae-wrap");
            foreach (var m in new[] { "RGB", "HSV", "CMYK" })
            {
                var mm = m;
                modeBtns[m] = Btn(mb, m, () => { mode = mm; BuildSliders(); Refresh(); }, null);
            }
            sliders = El(Root, "cp-sliders");

            LoadPalette();
            BuildSet();
            BuildSliders();
            SetMain(Main, true);
        }

        // ───────── 공개 ─────────
        public void SetMain(Color32 c, bool silent = false)
        {
            Main = c;
            UseClear = false;
            Color.RGBToHSV(c, out float hh, out float ss, out float vv);
            if (ss > 0.001f && vv > 0.001f) h = hh;
            s = ss; v = vv;
            Refresh();
            if (!silent) Changed?.Invoke();
        }

        public void Swap()
        {
            (Main, Sub) = (Sub, Main);
            SetMain(Main);
        }

        // ───────── 고르기 ─────────
        void CirclePick(Vector2 p, bool isRing)
        {
            if (isRing)
            {
                float a = Mathf.Atan2(-p.y, p.x) * Mathf.Rad2Deg;      // 오른쪽 0°, 반시계
                h = Mathf.Repeat(a, 360f) / 360f;
            }
            else
            {
                s = Mathf.Clamp01(p.x / SQ + 0.5f);
                v = Mathf.Clamp01(0.5f - p.y / SQ);
            }
            FromHsv();
        }

        void FromHsv()
        {
            var c = (Color32)Color.HSVToRGB(h, s, v);
            c.a = Main.a;
            Main = c;
            UseClear = false;
            Refresh();
            Changed?.Invoke();
        }

        void Fire() { Refresh(); Changed?.Invoke(); }

        void ApplyHex()
        {
            var t = hex.value.Trim();
            if (!t.StartsWith("#")) t = "#" + t;
            if (ColorUtility.TryParseHtmlString(t, out var c)) SetMain(c);
        }

        // ───────── 슬라이더 ─────────
        static readonly Dictionary<string, string[]> CH = new Dictionary<string, string[]>
        {
            ["RGB"] = new[] { "R", "G", "B", "A" },
            ["HSV"] = new[] { "H", "S", "V", "A" },
            ["CMYK"] = new[] { "C", "M", "Y", "K", "A" },
        };

        void BuildSliders()
        {
            foreach (var r in rows) TexGrave.Kill(r.tex);
            rows.Clear();
            sliders.Clear();
            foreach (var kv in modeBtns) kv.Value.EnableInClassList("ae-btn--on", kv.Key == mode);
            foreach (var ch in CH[mode])
            {
                var name = ch;
                var row = El(sliders, "cp-row");
                var lb = new Label(name == "A" ? "투명도" : name); lb.AddToClassList("cp-ch"); row.Add(lb);
                var track = El(row, "cp-track");
                var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                track.style.backgroundImage = new StyleBackground(tex);
                var mark = new VisualElement { pickingMode = PickingMode.Ignore }; mark.AddToClassList("cp-tmark"); track.Add(mark);
                var val = new Label(); val.AddToClassList("cp-val"); row.Add(val);
                Btn(row, "▼", () => Nudge(name, -1), null);
                Btn(row, "▲", () => Nudge(name, 1), null);
                bool drag = false;
                track.RegisterCallback<PointerDownEvent>(e => { drag = true; track.CapturePointer(e.pointerId); SetCh(name, e.localPosition.x / Mathf.Max(1, track.resolvedStyle.width)); e.StopPropagation(); });
                track.RegisterCallback<PointerMoveEvent>(e => { if (drag) SetCh(name, e.localPosition.x / Mathf.Max(1, track.resolvedStyle.width)); });
                track.RegisterCallback<PointerUpEvent>(e => { drag = false; track.ReleasePointer(e.pointerId); });
                rows.Add((name, tex, track, mark, val));
            }
        }

        /// <summary>채널 값(0~1).</summary>
        float GetCh(string ch)
        {
            var c = (Color)Main;
            ToCmyk(c, out float C, out float M, out float Y, out float K);
            switch (ch)
            {
                case "R": return c.r; case "G": return c.g; case "B": return c.b; case "A": return c.a;
                case "H": return h; case "S": return s; case "V": return v;
                case "C": return C; case "M": return M; case "Y": return Y; case "K": return K;
            }
            return 0;
        }

        void SetCh(string ch, float x)
        {
            x = Mathf.Clamp01(x);
            var c = (Color)Main;
            switch (ch)
            {
                case "A": var m = Main; m.a = (byte)Mathf.RoundToInt(x * 255); Main = m; UseClear = false; Fire(); return;
                case "H": h = Mathf.Min(x, 0.9999f); FromHsv(); return;
                case "S": s = x; FromHsv(); return;
                case "V": v = x; FromHsv(); return;
                case "R": c.r = x; break; case "G": c.g = x; break; case "B": c.b = x; break;
                default:
                    ToCmyk(c, out float C, out float M, out float Y, out float K);
                    if (ch == "C") C = x; else if (ch == "M") M = x; else if (ch == "Y") Y = x; else K = x;
                    c = FromCmyk(C, M, Y, K, c.a);
                    break;
            }
            var cc = (Color32)c; cc.a = Main.a;
            SetMain(cc);
        }

        void Nudge(string ch, int d)
        {
            float steps = ch == "H" ? 360 : ch == "R" || ch == "G" || ch == "B" || ch == "A" ? 255 : 100;
            SetCh(ch, (Mathf.Round(GetCh(ch) * steps) + d) / steps);
        }

        static void ToCmyk(Color c, out float C, out float M, out float Y, out float K)
        {
            K = 1 - Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (K >= 0.9999f) { C = M = Y = 0; return; }
            C = (1 - c.r - K) / (1 - K); M = (1 - c.g - K) / (1 - K); Y = (1 - c.b - K) / (1 - K);
        }

        static Color FromCmyk(float C, float M, float Y, float K, float a) => new Color((1 - C) * (1 - K), (1 - M) * (1 - K), (1 - Y) * (1 - K), a);

        /// <summary>채널 값 x 에서의 색(슬라이더 띠 그리기용).</summary>
        Color Sample(string ch, float x)
        {
            var c = (Color)Main; c.a = 1;
            switch (ch)
            {
                case "R": c.r = x; return c; case "G": c.g = x; return c; case "B": c.b = x; return c;
                case "A": var a = (Color)Main; a.a = x; return a;
                case "H": return Color.HSVToRGB(Mathf.Min(x, 0.9999f), Mathf.Max(s, 0.6f), Mathf.Max(v, 0.6f));
                case "S": return Color.HSVToRGB(h, x, v);
                case "V": return Color.HSVToRGB(h, s, x);
            }
            ToCmyk(c, out float C, out float M, out float Y, out float K);
            if (ch == "C") C = x; else if (ch == "M") M = x; else if (ch == "Y") Y = x; else K = x;
            return FromCmyk(C, M, Y, K, 1);
        }

        string ValText(string ch)
        {
            float x = GetCh(ch);
            if (ch == "H") return $"{Mathf.RoundToInt(x * 360) % 360}°";
            if (ch == "R" || ch == "G" || ch == "B" || ch == "A") return Mathf.RoundToInt(x * 255).ToString();
            return $"{Mathf.RoundToInt(x * 100)}%";
        }

        // ───────── 그리기 ─────────
        void Refresh()
        {
            mainChip.style.backgroundColor = (Color)Main;
            subChip.style.backgroundColor = (Color)Sub;
            mainChip.EnableInClassList("cp-chip--on", !UseClear);
            clearChip.EnableInClassList("cp-chip--on", UseClear);
            hex.SetValueWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(Main));
            // 채도 · 명도 사각
            var px = new Color32[48 * 48];
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++) px[y * 48 + x] = Color.HSVToRGB(h, x / 47f, y / 47f);
            svTex.SetPixels32(px); svTex.Apply(false);
            float ang = h * Mathf.PI * 2, rr = (RING_IN + RING_OUT) / 2f;
            ringMark.style.left = CIRCLE / 2f + Mathf.Cos(ang) * rr - 6; ringMark.style.top = CIRCLE / 2f - Mathf.Sin(ang) * rr - 6;
            float sq0 = (CIRCLE - SQ) / 2f;
            svMark.style.left = sq0 + s * SQ - 5; svMark.style.top = sq0 + (1 - v) * SQ - 5;
            // 슬라이더
            foreach (var r in rows)
            {
                var line = new Color32[64];
                for (int i = 0; i < 64; i++) line[i] = Sample(r.name, i / 63f);
                r.tex.SetPixels32(line); r.tex.Apply(false);
                float w = r.track.resolvedStyle.width;
                if (float.IsNaN(w) || w <= 0) w = 120;
                r.mark.style.left = GetCh(r.name) * w - 3;
                r.val.text = ValText(r.name);
            }
            for (int i = 0; i < setGrid.childCount; i++) setGrid[i].EnableInClassList("cp-sw--on", i == selSwatch);
        }

        static Texture2D MakeRing()
        {
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            float k = N / (float)CIRCLE, rin = RING_IN * k, rout = RING_OUT * k, c = N / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(Mathf.Min(d - rin, rout - d) + 0.5f);
                    if (a <= 0) { px[y * N + x] = new Color32(0, 0, 0, 0); continue; }
                    float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, 360f) / 360f;   // 텍스처는 아래 → 위라 dy 가 위쪽(+) = 반시계
                    var col = (Color32)Color.HSVToRGB(hue, 1, 1);
                    col.a = (byte)(a * 255);
                    px[y * N + x] = col;
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // ───────── 컬러 세트 ─────────
        static string PalPath => Path.Combine(ArtLib.Dir, "palette.txt");

        static List<Color32> Standard()
        {
            var hx = new[]
            {
                "#000000", "#FFFFFF", "#FF3366", "#FF5A4E", "#FF8A47", "#C27A26", "#FFC23D", "#FFF7C4",
                "#FFFF3D", "#CCFF7A", "#88E066", "#30A030", "#1F6B3A", "#38A88A", "#26D8D0", "#8AF5FF",
                "#C2EEFF", "#7EC8FF", "#3399EE", "#2B6FDB", "#1A3FD6", "#5555FF", "#6640DD", "#B888FF",
                "#9933CC", "#EE88FF", "#FFC4F5", "#FF55BB", "#AA3366", "#AA7766", "#774433", "#4A3020",
                "#D98060", "#F2B090", "#FFD0C8", "#FFE4DC", "#FFEEDD", "#FFD8B0", "#F0B888", "#D89458",
                "#E85870", "#FF8A8A", "#FFAA90", "#D8B0A8", "#DDA0AA", "#AAA8D8", "#B8A088", "#8C8C8C",
            };
            var o = new List<Color32>();
            foreach (var x in hx) if (ColorUtility.TryParseHtmlString(x, out var c)) o.Add(c);
            return o;
        }

        void LoadPalette()
        {
            palette = new List<Color32>();
            try
            {
                if (File.Exists(PalPath))
                    foreach (var line in File.ReadAllLines(PalPath))
                        if (ColorUtility.TryParseHtmlString(line.Trim(), out var c)) palette.Add(c);
            }
            catch { }
            if (palette.Count == 0) palette = Standard();
        }

        void SavePalette()
        {
            try { Directory.CreateDirectory(ArtLib.Dir); KitBackup.Before(PalPath); File.WriteAllLines(PalPath, palette.Select(c => "#" + ColorUtility.ToHtmlStringRGBA(c))); }
            catch { }
        }

        void BuildSet()
        {
            setGrid.Clear();
            for (int i = 0; i < palette.Count; i++)
            {
                int ii = i;
                var sw = new VisualElement();
                sw.AddToClassList("cp-sw");
                sw.style.backgroundColor = (Color)palette[i];
                sw.tooltip = "#" + ColorUtility.ToHtmlStringRGBA(palette[i]);
                sw.RegisterCallback<PointerDownEvent>(e =>
                {
                    selSwatch = ii;
                    if (e.button == 1) { palette.RemoveAt(ii); selSwatch = -1; SavePalette(); BuildSet(); }
                    else SetMain(palette[ii]);
                    e.StopPropagation();
                });
                setGrid.Add(sw);
            }
            Refresh();
        }

        // ───────── 도우미 ─────────
        static VisualElement El(VisualElement p, string cls) { var v = new VisualElement(); v.AddToClassList(cls); p.Add(v); return v; }
        static void Title(VisualElement p, string t) { var l = new Label(t); l.AddToClassList("ae-sec"); p.Add(l); }
        static Button Btn(VisualElement p, string t, Action a, string tip)
        {
            var b = new Button(a) { text = t };
            if (tip != null) b.tooltip = tip;
            b.AddToClassList("ae-btn");
            p.Add(b);
            return b;
        }
        static VisualElement Abs(VisualElement p, float x, float y, float w, float h)
        {
            var v = new VisualElement();
            v.style.position = Position.Absolute;
            v.style.left = x; v.style.top = y; v.style.width = w; v.style.height = h;
            p.Add(v);
            return v;
        }
        static VisualElement Chip(VisualElement p, string tip, Action a)
        {
            var c = new VisualElement { tooltip = tip };
            c.AddToClassList("cp-chip");
            c.RegisterCallback<PointerDownEvent>(e => { a(); e.StopPropagation(); });
            p.Add(c);
            return c;
        }
    }
}
