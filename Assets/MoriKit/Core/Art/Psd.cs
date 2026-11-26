using System;
using System.Collections.Generic;
using System.Text;

namespace Mori.Core.Art
{
    /// <summary>
    /// PSD(포토샵 · 클립 스튜디오가 저장한 .psd / .psb) 의 레이어를 읽는다 — 게임이 변환 없이 바로 쓰기 위한 최소 판.
    /// 지원: 8비트 RGB, 일반 픽셀 레이어, 폴더(중첩), 보이기 · 숨기기, 불투명도 · 칠 불투명도, 클리핑 마스크,
    ///       혼합 모드(일반 · 곱하기 · 스크린 · 더하기(선형 닷지) — 나머지는 일반으로), 압축 없음 · RLE.
    /// 무시: 레이어 효과 · 조정 레이어 · 레이어 마스크 · 고급 개체 · 텍스트(래스터화하면 읽힘).
    /// 레이어가 하나도 없는(합쳐진) 파일은 합쳐진 그림을 레이어 하나로 읽는다.
    /// </summary>
    public sealed class PsdNode
    {
        public string Name = "";
        public bool Group, Visible = true, Clip;
        public double Opacity = 1;          // 불투명도 × 칠 불투명도
        public string Blend = "norm";
        public int L, T, R, B;              // 캔버스 좌표(픽셀) — 레이어만
        public byte[] Rgba;                 // (R-L)×(B-T)×4, 위에서 아래로, 곧은 알파
        public readonly List<PsdNode> Kids = new List<PsdNode>();   // 아래 → 위
        public int W => R - L;
        public int H => B - T;
        public PsdNode Parent;

        /// <summary>이 노드 아래 모든 레이어(폴더 제외).</summary>
        public IEnumerable<PsdNode> Leaves()
        {
            if (!Group) { yield return this; yield break; }
            foreach (var k in Kids) foreach (var x in k.Leaves()) yield return x;
        }
    }

    public sealed class PsdDoc
    {
        public int W, H;
        public readonly PsdNode Root = new PsdNode { Group = true, Name = "(root)" };
    }

    public static class Psd
    {
        // ───────── 읽기 ─────────
        sealed class Rd
        {
            readonly byte[] b; public long P; public readonly bool Big;
            public Rd(byte[] b, bool big = false) { this.b = b; Big = big; }
            public Rd WithBig(bool big) => new Rd(b, big) { P = P };
            public long Len => b.Length;
            public byte U8() => b[P++];
            public int U16() { int v = (b[P] << 8) | b[P + 1]; P += 2; return v; }
            public int I16() { int v = (short)((b[P] << 8) | b[P + 1]); P += 2; return v; }
            public uint U32() { uint v = ((uint)b[P] << 24) | ((uint)b[P + 1] << 16) | ((uint)b[P + 2] << 8) | b[P + 3]; P += 4; return v; }
            public int I32() => (int)U32();
            public long U64() { long hi = U32(); long lo = U32(); return (hi << 32) | lo; }
            public long LenField() => Big ? U64() : U32();
            public string Ascii(int n) { var s = Encoding.ASCII.GetString(b, (int)P, n); P += n; return s; }
            public void Skip(long n) { P += n; }
            public byte this[long i] => b[i];
        }

        public static bool LooksLikePsd(byte[] b) => b != null && b.Length > 26 && b[0] == '8' && b[1] == 'B' && b[2] == 'P' && b[3] == 'S';

        public static PsdDoc Read(byte[] bytes)
        {
            if (!LooksLikePsd(bytes)) throw new FormatException("PSD 파일이 아니에요(8BPS 없음)");
            var r = new Rd(bytes);
            r.Skip(4);
            int ver = r.U16();
            if (ver != 1 && ver != 2) throw new FormatException("모르는 PSD 버전 " + ver);
            r = r.WithBig(ver == 2);
            r.Skip(6);
            int channels = r.U16();
            int h = r.I32(), w = r.I32();
            int depth = r.U16(), mode = r.U16();
            if (depth != 8) throw new FormatException($"8비트만 읽어요(지금 {depth}비트) — 이미지 › 모드 › 8비트");
            if (mode != 3) throw new FormatException($"RGB 모드만 읽어요(지금 모드 {mode}) — 이미지 › 모드 › RGB");
            var doc = new PsdDoc { W = w, H = h };
            r.Skip(r.U32());                    // 색상 모드 데이터
            r.Skip(r.U32());                    // 이미지 리소스
            long lmLen = r.LenField();
            long lmEnd = r.P + lmLen;
            bool any = false;
            if (lmLen > 0)
            {
                long liLen = r.LenField();
                long liEnd = r.P + liLen;
                if (liLen > 0) { ReadLayers(r, doc); any = doc.Root.Kids.Count > 0; }
                r.P = liEnd;
            }
            r.P = lmEnd;
            if (!any) ReadMerged(r, doc, channels);
            return doc;
        }

        sealed class Rec
        {
            public PsdNode N;
            public List<(int id, long len)> Ch = new List<(int, long)>();
            public int Divider;                 // 0 레이어 · 1/2 폴더 머리 · 3 폴더 끝 표시
        }

        static readonly HashSet<string> LongKeys = new HashSet<string> { "LMsk", "Lr16", "Lr32", "Layr", "Mt16", "Mt32", "Mtrn", "Alph", "FMsk", "lnk2", "FEid", "FXid", "PxSD" };

        static void ReadLayers(Rd r, PsdDoc doc)
        {
            int count = Math.Abs(r.I16());
            var recs = new List<Rec>(count);
            for (int i = 0; i < count; i++)
            {
                var n = new PsdNode();
                int t = r.I32(), l = r.I32(), bt = r.I32(), rt = r.I32();
                n.T = t; n.L = l; n.B = bt; n.R = rt;
                var rec = new Rec { N = n };
                int nch = r.U16();
                for (int c = 0; c < nch; c++) rec.Ch.Add((r.I16(), r.LenField()));
                r.Skip(4);                          // 8BIM
                n.Blend = r.Ascii(4);
                double opacity = r.U8() / 255.0, fill = 1;
                n.Clip = r.U8() != 0;
                int flags = r.U8();
                n.Visible = (flags & 2) == 0;
                r.Skip(1);
                long extraLen = r.U32();
                long extraEnd = r.P + extraLen;
                r.Skip(r.U32());                    // 레이어 마스크
                r.Skip(r.U32());                    // 혼합 범위
                int nl = r.U8();
                var nameBytes = new byte[nl];
                for (int k = 0; k < nl; k++) nameBytes[k] = r.U8();
                n.Name = Encoding.UTF8.GetString(nameBytes);
                int pad = (1 + nl) % 4; if (pad != 0) r.Skip(4 - pad);
                while (r.P + 12 <= extraEnd)
                {
                    string sig = r.Ascii(4);
                    if (sig != "8BIM" && sig != "8B64") break;
                    string key = r.Ascii(4);
                    long len = r.Big && LongKeys.Contains(key) ? r.U64() : r.U32();
                    long dEnd = r.P + len;
                    if (key == "luni" && len >= 4)
                    {
                        int cnt = (int)r.U32();
                        var sb = new StringBuilder(cnt);
                        for (int k = 0; k < cnt && r.P + 2 <= dEnd; k++) sb.Append((char)r.U16());
                        n.Name = sb.ToString().TrimEnd('\0');
                    }
                    else if ((key == "lsct" || key == "lsdk") && len >= 4)
                    {
                        rec.Divider = (int)r.U32();
                        if (len >= 12) { r.Skip(4); string bk = r.Ascii(4); if (rec.Divider == 1 || rec.Divider == 2) n.Blend = bk; }
                    }
                    else if (key == "iOpa" && len >= 1) fill = r.U8() / 255.0;
                    r.P = dEnd;
                }
                r.P = extraEnd;
                n.Opacity = opacity * fill;
                recs.Add(rec);
            }
            // 채널 데이터(레코드 순서대로)
            foreach (var rec in recs)
            {
                var n = rec.N;
                int lw = n.W, lh = n.H;
                byte[] rr = null, gg = null, bb = null, aa = null;
                foreach (var (id, len) in rec.Ch)
                {
                    long start = r.P;
                    if (len < 2) { r.P = start + len; continue; }
                    int comp = r.U16();
                    if (id >= -1 && id <= 2 && lw > 0 && lh > 0 && rec.Divider == 0)
                    {
                        var plane = Decode(r, comp, lw, lh, start + len);
                        if (id == 0) rr = plane; else if (id == 1) gg = plane; else if (id == 2) bb = plane; else aa = plane;
                    }
                    r.P = start + len;
                }
                if (rec.Divider == 0 && lw > 0 && lh > 0)
                {
                    var px = new byte[lw * lh * 4];
                    for (int i = 0, o = 0; i < lw * lh; i++, o += 4)
                    {
                        px[o] = rr?[i] ?? 0; px[o + 1] = gg?[i] ?? 0; px[o + 2] = bb?[i] ?? 0; px[o + 3] = aa?[i] ?? 255;
                    }
                    n.Rgba = px;
                }
            }
            // 계층: 레코드는 아래 → 위. 폴더 끝 표시(3)가 먼저, 내용, 그다음 폴더 머리(1/2).
            var stack = new Stack<PsdNode>();
            var cur = doc.Root;
            foreach (var rec in recs)
            {
                var n = rec.N;
                if (rec.Divider == 3)
                {
                    var g = new PsdNode { Group = true, Parent = cur };
                    cur.Kids.Add(g);
                    stack.Push(cur);
                    cur = g;
                }
                else if (rec.Divider == 1 || rec.Divider == 2)
                {
                    if (stack.Count == 0) continue;     // 짝 없는 머리는 무시
                    cur.Name = n.Name; cur.Visible = n.Visible; cur.Opacity = n.Opacity; cur.Blend = n.Blend; cur.Clip = n.Clip;
                    cur = stack.Pop();
                }
                else
                {
                    if (n.Rgba == null) continue;        // 빈 레이어
                    n.Parent = cur;
                    cur.Kids.Add(n);
                }
            }
        }

        static byte[] Decode(Rd r, int comp, int w, int h, long end)
        {
            var o = new byte[w * h];
            if (comp == 0)
            {
                int n = (int)Math.Min(w * h, end - r.P);
                for (int i = 0; i < n; i++) o[i] = r.U8();
            }
            else if (comp == 1)
            {
                r.Skip((long)h * (r.Big ? 4 : 2));    // 줄마다 바이트 수
                int pos = 0, total = w * h;
                while (pos < total && r.P < end)
                {
                    int hdr = (sbyte)r.U8();
                    if (hdr >= 0) { int c = hdr + 1; for (int i = 0; i < c && r.P < end; i++) { byte v = r.U8(); if (pos < total) o[pos++] = v; } }
                    else if (hdr > -128) { int c = 1 - hdr; byte v = r.U8(); for (int i = 0; i < c; i++) if (pos < total) o[pos++] = v; }
                }
            }
            // 2 · 3(ZIP) 은 읽지 않는다 — 투명으로 남음
            return o;
        }

        /// <summary>레이어 정보가 없는 파일: 맨 끝의 합쳐진 그림을 레이어 하나로.</summary>
        static void ReadMerged(Rd r, PsdDoc doc, int channels)
        {
            int w = doc.W, h = doc.H;
            if (r.P + 2 > r.Len) return;
            int comp = r.U16();
            var planes = new byte[Math.Min(channels, 4)][];
            if (comp == 0)
            {
                for (int c = 0; c < channels; c++)
                {
                    var p = new byte[w * h];
                    for (int i = 0; i < w * h && r.P < r.Len; i++) p[i] = r.U8();
                    if (c < planes.Length) planes[c] = p;
                }
            }
            else if (comp == 1)
            {
                r.Skip((long)h * channels * (r.Big ? 4 : 2));
                for (int c = 0; c < channels; c++)
                {
                    var p = new byte[w * h];
                    int pos = 0, total = w * h;
                    while (pos < total && r.P < r.Len)
                    {
                        int hdr = (sbyte)r.U8();
                        if (hdr >= 0) { int k = hdr + 1; for (int i = 0; i < k; i++) { byte v = r.U8(); if (pos < total) p[pos++] = v; } }
                        else if (hdr > -128) { int k = 1 - hdr; byte v = r.U8(); for (int i = 0; i < k; i++) if (pos < total) p[pos++] = v; }
                    }
                    if (c < planes.Length) planes[c] = p;
                }
            }
            else return;
            var px = new byte[w * h * 4];
            for (int i = 0, o = 0; i < w * h; i++, o += 4)
            {
                px[o] = planes.Length > 0 && planes[0] != null ? planes[0][i] : (byte)0;
                px[o + 1] = planes.Length > 1 && planes[1] != null ? planes[1][i] : (byte)0;
                px[o + 2] = planes.Length > 2 && planes[2] != null ? planes[2][i] : (byte)0;
                px[o + 3] = planes.Length > 3 && planes[3] != null ? planes[3][i] : (byte)255;
            }
            doc.Root.Kids.Add(new PsdNode { Name = "합쳐진 그림", L = 0, T = 0, R = w, B = h, Rgba = px, Parent = doc.Root });
        }

        // ───────── 합치기 ─────────
        /// <summary>
        /// 노드들(아래 → 위)을 캔버스의 한 영역(x, y, w, h)에 합쳐 곧은 알파 RGBA(위에서 아래로)로 돌려준다.
        /// forceVisible: 맨 위 폴더들의 숨김을 무시(컷 폴더를 켜고 끄며 작업해도 모든 컷이 읽히게).
        /// </summary>
        public static byte[] Compose(IList<PsdNode> nodes, int x, int y, int w, int h, bool forceVisible = false)
        {
            var acc = new float[w * h * 4];                // 미리 곱한 알파
            ComposeInto(acc, nodes, x, y, w, h, forceVisible);
            var o = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                float a = acc[i * 4 + 3];
                if (a <= 0.0001f) continue;
                float inv = 1f / a;
                o[i * 4] = ToByte(acc[i * 4] * inv);
                o[i * 4 + 1] = ToByte(acc[i * 4 + 1] * inv);
                o[i * 4 + 2] = ToByte(acc[i * 4 + 2] * inv);
                o[i * 4 + 3] = ToByte(a);
            }
            return o;
        }

        static byte ToByte(float v) => (byte)(v <= 0 ? 0 : v >= 1 ? 255 : (int)(v * 255f + 0.5f));

        static void ComposeInto(float[] dst, IList<PsdNode> nodes, int x, int y, int w, int h, bool forceVisible)
        {
            float[] baseA = null;        // 클리핑 기준 레이어의 알파(이 영역)
            bool baseOn = false;
            foreach (var n in nodes)
            {
                bool vis = n.Visible || (forceVisible && n.Group);     // 숨김 무시는 폴더(컷)만 — 공통 레이어의 숨김은 그대로
                if (!n.Clip)
                {
                    baseOn = vis;
                    if (!vis) { baseA = null; continue; }
                    var src = Source(n, x, y, w, h);
                    if (src == null) { baseA = null; continue; }
                    baseA = new float[w * h];
                    for (int i = 0; i < w * h; i++) baseA[i] = src[i * 4 + 3];
                    BlendInto(dst, src, n.Blend, w * h);
                }
                else
                {
                    if (!vis || !baseOn || baseA == null) continue;
                    var src = Source(n, x, y, w, h);
                    if (src == null) continue;
                    for (int i = 0; i < w * h; i++)
                    {
                        float k = baseA[i];
                        src[i * 4] *= k; src[i * 4 + 1] *= k; src[i * 4 + 2] *= k; src[i * 4 + 3] *= k;
                    }
                    BlendInto(dst, src, n.Blend, w * h);
                }
            }
        }

        /// <summary>노드 하나를 영역 크기의 미리 곱한 RGBA(불투명도 적용)로. 영역과 안 겹치면 null.</summary>
        static float[] Source(PsdNode n, int x, int y, int w, int h)
        {
            if (n.Group)
            {
                var g = new float[w * h * 4];
                ComposeInto(g, n.Kids, x, y, w, h, false);
                if (n.Opacity < 1) { float o = (float)n.Opacity; for (int i = 0; i < g.Length; i++) g[i] *= o; }
                return g;
            }
            int x0 = Math.Max(x, n.L), y0 = Math.Max(y, n.T), x1 = Math.Min(x + w, n.R), y1 = Math.Min(y + h, n.B);
            if (x0 >= x1 || y0 >= y1 || n.Rgba == null) return null;
            var s = new float[w * h * 4];
            float op = (float)n.Opacity / 255f;
            int lw = n.W;
            for (int yy = y0; yy < y1; yy++)
            {
                int si = ((yy - n.T) * lw + (x0 - n.L)) * 4;
                int di = ((yy - y) * w + (x0 - x)) * 4;
                for (int xx = x0; xx < x1; xx++, si += 4, di += 4)
                {
                    float a = n.Rgba[si + 3] * op;
                    if (a <= 0) continue;
                    s[di] = n.Rgba[si] / 255f * a;
                    s[di + 1] = n.Rgba[si + 1] / 255f * a;
                    s[di + 2] = n.Rgba[si + 2] / 255f * a;
                    s[di + 3] = a;
                }
            }
            return s;
        }

        /// <summary>미리 곱한 알파 혼합: C = (1−da)·Cs + (1−sa)·Cd + sa·da·B(cs, cd).</summary>
        static void BlendInto(float[] d, float[] s, string mode, int n)
        {
            int m = mode == "mul " ? 1 : mode == "scrn" ? 2 : mode == "lddg" || mode == "lnDg" ? 3 : 0;
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                float sa = s[o + 3];
                if (sa <= 0) continue;
                float da = d[o + 3];
                if (m == 0 || da <= 0)
                {
                    float k = 1 - sa;
                    d[o] = s[o] + d[o] * k; d[o + 1] = s[o + 1] + d[o + 1] * k; d[o + 2] = s[o + 2] + d[o + 2] * k; d[o + 3] = sa + da * k;
                    continue;
                }
                for (int c = 0; c < 3; c++)
                {
                    float cs = s[o + c] / sa, cd = d[o + c] / da;
                    float bl = m == 1 ? cs * cd : m == 2 ? cs + cd - cs * cd : Math.Min(1f, cs + cd);
                    d[o + c] = (1 - da) * s[o + c] + (1 - sa) * d[o + c] + sa * da * bl;
                }
                d[o + 3] = sa + da - sa * da;
            }
        }

        // ───────── 쓰기(테스트 · 나중의 PSD 내보내기용) ─────────
        /// <summary>간단한 PSD 쓰기: 8비트 RGB, 압축 없음, 폴더 · 숨김 · 불투명도 · 클리핑 · 혼합 모드.</summary>
        public static byte[] Write(PsdDoc doc)
        {
            var o = new List<byte>();
            void B(int v) => o.Add((byte)v);
            void U16(int v) { o.Add((byte)(v >> 8)); o.Add((byte)v); }
            void U32(long v) { o.Add((byte)(v >> 24)); o.Add((byte)(v >> 16)); o.Add((byte)(v >> 8)); o.Add((byte)v); }
            void Str(string s) { foreach (var ch in Encoding.ASCII.GetBytes(s)) o.Add(ch); }
            Str("8BPS"); U16(1); for (int i = 0; i < 6; i++) B(0); U16(4); U32(doc.H); U32(doc.W); U16(8); U16(3);
            U32(0); U32(0);
            // 레코드(아래 → 위)로 펼치기
            var flat = new List<(PsdNode n, int div)>();
            void Flat(PsdNode g)
            {
                foreach (var k in g.Kids)
                {
                    if (k.Group) { flat.Add((k, 3)); Flat(k); flat.Add((k, 1)); }
                    else flat.Add((k, 0));
                }
            }
            Flat(doc.Root);
            var li = new List<byte>();
            void LB(int v) => li.Add((byte)v);
            void L16(int v) { li.Add((byte)(v >> 8)); li.Add((byte)v); }
            void L32(long v) { li.Add((byte)(v >> 24)); li.Add((byte)(v >> 16)); li.Add((byte)(v >> 8)); li.Add((byte)v); }
            void LStr(string s) { foreach (var ch in Encoding.ASCII.GetBytes(s)) li.Add(ch); }
            L16(flat.Count);
            foreach (var (n, div) in flat)
            {
                bool px = div == 0;
                int w = px ? n.W : 0, h = px ? n.H : 0;
                L32(px ? n.T : 0); L32(px ? n.L : 0); L32(px ? n.B : 0); L32(px ? n.R : 0);
                L16(4);
                foreach (var id in new[] { -1, 0, 1, 2 }) { L16(id & 0xFFFF); L32(2 + (long)w * h); }
                LStr("8BIM"); LStr(div == 3 ? "norm" : (n.Blend ?? "norm").PadRight(4).Substring(0, 4));
                LB((int)Math.Round(Math.Max(0, Math.Min(1, n.Opacity)) * 255)); LB(n.Clip && div == 0 ? 1 : 0); LB(n.Visible || div == 3 ? 0 : 2); LB(0);
                string name = div == 3 ? "</Layer group>" : n.Name ?? "";
                var nameU = Encoding.BigEndianUnicode.GetBytes(name);
                var nameA = new List<byte>(); foreach (var ch in name) nameA.Add(ch < 128 ? (byte)ch : (byte)'?');
                int nl = Math.Min(255, nameA.Count);
                int pascal = 1 + nl; int pascalPad = (4 - pascal % 4) % 4;
                int luniLen = 4 + nameU.Length; int luniPad = luniLen % 2;
                int extra = 4 + 4 + pascal + pascalPad + (12 + luniLen + luniPad) + (div != 0 ? 12 + 4 : 0);
                L32(extra);
                L32(0); L32(0);
                LB(nl); for (int i = 0; i < nl; i++) LB(nameA[i]); for (int i = 0; i < pascalPad; i++) LB(0);
                LStr("8BIM"); LStr("luni"); L32(luniLen + luniPad); L32(name.Length); foreach (var bt in nameU) LB(bt); for (int i = 0; i < luniPad; i++) LB(0);
                if (div != 0) { LStr("8BIM"); LStr("lsct"); L32(4); L32(div); }
            }
            foreach (var (n, div) in flat)
            {
                bool px = div == 0;
                int w = px ? n.W : 0, h = px ? n.H : 0;
                foreach (var c in new[] { 3, 0, 1, 2 })
                {
                    L16(0);
                    for (int i = 0; i < w * h; i++) LB(n.Rgba[i * 4 + c]);
                }
            }
            if (li.Count % 2 == 1) LB(0);
            U32(4 + li.Count + 4);      // 레이어 · 마스크 섹션
            U32(li.Count);
            o.AddRange(li);
            U32(0);                     // 전역 마스크
            // 합쳐진 그림(빈 투명) — 압축 없음
            U16(0);
            for (int c = 0; c < 4; c++) for (int i = 0; i < doc.W * doc.H; i++) B(0);
            return o.ToArray();
        }
    }
}
