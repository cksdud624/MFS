using System;
using System.Collections.Generic;

namespace Mori.Core.Art
{
    /// <summary>
    /// 타일로 나눈 RGBA 그림(위 → 아래, 8비트). 큰 캔버스(4K)용 바탕.
    ///  · 캔버스를 T×T 조각으로 나누고, 그린 적 있는 조각만 메모리를 쓴다(빈 조각 = null).
    ///  · 조각 배열은 되돌리기 스냅샷과 나눠 쓸 수 있다(쓰기 직전 복사 — Share() 뒤 처음 쓰는 조각만 복사).
    /// </summary>
    public sealed class TileImage
    {
        public const int T = 128;
        public readonly int W, H, Nx, Ny;
        public byte[][] Tiles;
        bool[] own;

        public TileImage(int w, int h)
        {
            W = Math.Max(1, w); H = Math.Max(1, h);
            Nx = (W + T - 1) / T; Ny = (H + T - 1) / T;
            Tiles = new byte[Nx * Ny][];
            own = new bool[Nx * Ny];
        }

        public int Count => Tiles.Length;
        public int TW(int tx) => Math.Min(T, W - tx * T);
        public int TH(int ty) => Math.Min(T, H - ty * T);
        public int TileOf(int x, int y) => (y / T) * Nx + x / T;
        public void TileRect(int i, out int x0, out int y0, out int tw, out int th)
        {
            int tx = i % Nx, ty = i / Nx;
            x0 = tx * T; y0 = ty * T; tw = TW(tx); th = TH(ty);
        }

        public bool Empty { get { foreach (var t in Tiles) if (t != null) return false; return true; } }

        /// <summary>지금 조각들은 누군가와 나눠 쓴다 — 다음에 쓰는 조각은 먼저 복사한다.</summary>
        public void Share() { Array.Clear(own, 0, own.Length); }

        /// <summary>조각 참조만 복사한 사본(빠름). 둘 다 쓰기 직전 복사로 바뀐다.</summary>
        public TileImage Clone()
        {
            var c = new TileImage(W, H);
            Array.Copy(Tiles, c.Tiles, Tiles.Length);
            Share();
            return c;
        }

        /// <summary>조각 참조 배열(되돌리기용). 이 뒤로 쓰는 조각은 복사된다.</summary>
        public byte[][] Snapshot()
        {
            var s = (byte[][])Tiles.Clone();
            Share();
            return s;
        }

        public void Restore(byte[][] snap)
        {
            if (snap == null || snap.Length != Tiles.Length) return;
            Tiles = (byte[][])snap.Clone();
            Share();
        }

        /// <summary>쓸 수 있는 조각(없으면 만들고, 나눠 쓰는 중이면 복사).</summary>
        public byte[] Writable(int i)
        {
            var t = Tiles[i];
            if (t == null)
            {
                TileRect(i, out _, out _, out int tw, out int th);
                t = Tiles[i] = new byte[tw * th * 4];
                own[i] = true;
            }
            else if (!own[i])
            {
                t = Tiles[i] = (byte[])t.Clone();
                own[i] = true;
            }
            return t;
        }

        /// <summary>점 하나 쓰기 — 바뀐 조각 번호(안 바뀌면 −1).</summary>
        public int Set(int x, int y, byte r, byte g, byte b, byte a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return -1;
            int i = TileOf(x, y);
            var t = Tiles[i];
            int tw = TW(i % Nx);
            int k = ((y % T) * tw + (x % T)) * 4;
            if (t == null) { if (a == 0) return -1; }
            else if (t[k] == r && t[k + 1] == g && t[k + 2] == b && t[k + 3] == a) return -1;
            t = Writable(i);
            t[k] = r; t[k + 1] = g; t[k + 2] = b; t[k + 3] = a;
            return i;
        }

        public void Get(int x, int y, out byte r, out byte g, out byte b, out byte a)
        {
            r = g = b = a = 0;
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = TileOf(x, y);
            var t = Tiles[i];
            if (t == null) return;
            int k = ((y % T) * TW(i % Nx) + (x % T)) * 4;
            r = t[k]; g = t[k + 1]; b = t[k + 2]; a = t[k + 3];
        }

        public byte Alpha(int x, int y) { Get(x, y, out _, out _, out _, out var a); return a; }

        /// <summary>다 투명해진 조각은 버린다.</summary>
        public void Trim(IEnumerable<int> which = null)
        {
            if (which == null) { for (int i = 0; i < Tiles.Length; i++) TrimOne(i); return; }
            foreach (var i in which) TrimOne(i);
        }

        void TrimOne(int i)
        {
            var t = Tiles[i];
            if (t == null) return;
            for (int k = 3; k < t.Length; k += 4) if (t[k] != 0) return;
            Tiles[i] = null; own[i] = false;
        }

        /// <summary>sw×sh RGBA 를 (dx, dy) 에 그대로 덮어 쓴다(투명 픽셀도). 빈 조각은 만들지 않는다.</summary>
        public void Blit(byte[] src, int sw, int sh, int dx, int dy)
        {
            if (src == null) return;
            int x0 = Math.Max(0, dx), y0 = Math.Max(0, dy), x1 = Math.Min(W, dx + sw), y1 = Math.Min(H, dy + sh);
            if (x0 >= x1 || y0 >= y1) return;
            for (int ty = y0 / T; ty <= (y1 - 1) / T; ty++)
                for (int tx = x0 / T; tx <= (x1 - 1) / T; tx++)
                {
                    int i = ty * Nx + tx;
                    int ax = Math.Max(x0, tx * T), bx = Math.Min(x1, tx * T + TW(tx));
                    int ay = Math.Max(y0, ty * T), by = Math.Min(y1, ty * T + TH(ty));
                    // 이 조각에 들어갈 부분이 다 투명하고 조각도 없으면 건너뜀
                    if (Tiles[i] == null)
                    {
                        bool any = false;
                        for (int y = ay; y < by && !any; y++)
                        {
                            int s = ((y - dy) * sw + (ax - dx)) * 4 + 3;
                            for (int x = ax; x < bx; x++, s += 4) if (src[s] != 0) { any = true; break; }
                        }
                        if (!any) continue;
                    }
                    var t = Writable(i);
                    int tw = TW(tx);
                    for (int y = ay; y < by; y++)
                        Buffer.BlockCopy(src, ((y - dy) * sw + (ax - dx)) * 4, t, ((y - ty * T) * tw + (ax - tx * T)) * 4, (bx - ax) * 4);
                }
        }

        public static TileImage FromRgba(byte[] rgba, int w, int h)
        {
            var im = new TileImage(w, h);
            im.Blit(rgba, w, h, 0, 0);
            return im;
        }

        public byte[] ToRgba()
        {
            var o = new byte[W * H * 4];
            CopyTo(o, 0, 0, W, H);
            return o;
        }

        /// <summary>(x, y, w, h) 영역을 w×h RGBA 로.</summary>
        public void CopyTo(byte[] dst, int x, int y, int w, int h)
        {
            for (int i = 0; i < Tiles.Length; i++)
            {
                var t = Tiles[i];
                if (t == null) continue;
                TileRect(i, out int tx0, out int ty0, out int tw, out int th);
                int ax = Math.Max(x, tx0), bx = Math.Min(x + w, tx0 + tw), ay = Math.Max(y, ty0), by = Math.Min(y + h, ty0 + th);
                if (ax >= bx || ay >= by) continue;
                for (int yy = ay; yy < by; yy++)
                    Buffer.BlockCopy(t, ((yy - ty0) * tw + (ax - tx0)) * 4, dst, ((yy - y) * w + (ax - x)) * 4, (bx - ax) * 4);
            }
        }

        /// <summary>그려진 영역(불투명 픽셀이 있는 최소 사각형). 비었으면 false.</summary>
        public bool Bounds(out int l, out int t, out int r, out int b)
        {
            l = W; t = H; r = 0; b = 0;
            for (int i = 0; i < Tiles.Length; i++)
            {
                var p = Tiles[i];
                if (p == null) continue;
                TileRect(i, out int x0, out int y0, out int tw, out int th);
                for (int y = 0; y < th; y++)
                {
                    int k = y * tw * 4 + 3;
                    for (int x = 0; x < tw; x++, k += 4)
                    {
                        if (p[k] == 0) continue;
                        if (x0 + x < l) l = x0 + x;
                        if (x0 + x >= r) r = x0 + x + 1;
                        if (y0 + y < t) t = y0 + y;
                        if (y0 + y >= b) b = y0 + y + 1;
                    }
                }
            }
            return r > l && b > t;
        }

        public bool SameAs(TileImage o)
        {
            if (o == null || o.W != W || o.H != H) return false;
            for (int i = 0; i < Tiles.Length; i++)
            {
                var a = Tiles[i]; var b = o.Tiles[i];
                if (ReferenceEquals(a, b)) continue;
                if (a == null || b == null)
                {
                    var n = a ?? b;
                    for (int k = 3; k < n.Length; k += 4) if (n[k] != 0) return false;
                    continue;
                }
                for (int k = 0; k < a.Length; k++)
                {
                    if (a[k] == b[k]) continue;
                    // 둘 다 투명이면 색은 상관없음
                    int q = k - k % 4;
                    if (a[q + 3] == 0 && b[q + 3] == 0) { k = q + 3; continue; }
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 레이어 조각 합치기(Psd.Compose 와 같은 결과 — 미리 곱한 알파, 클리핑, 일반 · 곱하기 · 스크린 · 더하기).
    /// 같은 크기 조각 여러 장 → 한 장. 임시 버퍼는 호출하는 쪽이 재사용(쓰레기 없음).
    /// </summary>
    public static class TileComp
    {
        public struct Layer
        {
            public byte[] Px;          // tw×th RGBA, null = 투명
            public bool Visible, Clip;
            public float Opacity;
            public int Mode;           // 0 일반 · 1 곱하기 · 2 스크린 · 3 더하기
        }

        public static int ModeOf(string blend) => blend == "mul " ? 1 : blend == "scrn" ? 2 : blend == "lddg" || blend == "lnDg" ? 3 : 0;

        public sealed class Scratch
        {
            public float[] Acc = new float[0], BaseA = new float[0];
            public void Need(int n) { if (Acc.Length < n * 4) { Acc = new float[n * 4]; BaseA = new float[n]; } }
        }

        /// <summary>
        /// layers(아래 → 위)를 합쳐 dst 에 쓴다. dst 의 (dstX, dstY) 부터, 한 줄 dstW 픽셀.
        /// flip = 아래 → 위 줄 순서로 쓰기(유니티 텍스처). 그 경우 dstH 는 dst 전체 높이.
        /// </summary>
        public static void Compose(IList<Layer> layers, int tw, int th, Scratch s, byte[] dst, int dstX, int dstY, int dstW, int dstH, bool flip)
        {
            int n = tw * th;
            s.Need(n);
            var acc = s.Acc; var baseA = s.BaseA;
            Array.Clear(acc, 0, n * 4);
            bool any = false;
            bool baseOn = false, baseLive = false;      // baseLive = 기준 레이어에 그림이 있음(없으면 클리핑된 것도 안 보임)
            for (int li = 0; li < layers.Count; li++)
            {
                var L = layers[li];
                if (!L.Clip)
                {
                    baseOn = L.Visible;
                    baseLive = false;
                    if (!L.Visible || L.Px == null || L.Opacity <= 0) continue;
                    baseLive = true;
                    float op = L.Opacity / 255f;
                    var p = L.Px;
                    for (int i = 0; i < n; i++) baseA[i] = p[i * 4 + 3] * op;
                    Blend(acc, p, baseA, null, L.Mode, n, op);
                    any = true;
                }
                else
                {
                    if (!L.Visible || !baseOn || !baseLive || L.Px == null || L.Opacity <= 0) continue;
                    Blend(acc, L.Px, null, baseA, L.Mode, n, L.Opacity / 255f);
                    any = true;
                }
            }
            // 쓰기(미리 곱한 → 일반)
            for (int y = 0; y < th; y++)
            {
                int row = flip ? dstH - 1 - (dstY + y) : dstY + y;
                int d = (row * dstW + dstX) * 4;
                int a0 = y * tw * 4;
                if (!any) { Array.Clear(dst, d, tw * 4); continue; }
                for (int x = 0; x < tw; x++, d += 4)
                {
                    int o = a0 + x * 4;
                    float a = acc[o + 3];
                    if (a <= 0.0001f) { dst[d] = dst[d + 1] = dst[d + 2] = dst[d + 3] = 0; continue; }
                    float inv = 1f / a;
                    dst[d] = B(acc[o] * inv); dst[d + 1] = B(acc[o + 1] * inv); dst[d + 2] = B(acc[o + 2] * inv); dst[d + 3] = B(a);
                }
            }
        }

        static byte B(float v) => (byte)(v <= 0 ? 0 : v >= 1 ? 255 : (int)(v * 255f + 0.5f));

        /// <summary>srcA 가 있으면 그 값이 소스 알파(불투명도 적용), 없으면 픽셀 알파 × op × (clip 기준 알파).</summary>
        static void Blend(float[] d, byte[] p, float[] srcA, float[] clipA, int m, int n, float op)
        {
            const float k255 = 1f / 255f;
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                float sa = srcA != null ? srcA[i] : p[o + 3] * op;
                if (clipA != null) sa *= clipA[i];
                if (sa <= 0) continue;
                float sr = p[o] * k255 * sa, sg = p[o + 1] * k255 * sa, sb = p[o + 2] * k255 * sa;
                float da = d[o + 3];
                if (m == 0 || da <= 0)
                {
                    float k = 1 - sa;
                    d[o] = sr + d[o] * k; d[o + 1] = sg + d[o + 1] * k; d[o + 2] = sb + d[o + 2] * k; d[o + 3] = sa + da * k;
                    continue;
                }
                d[o] = Mix(sr, d[o], sa, da, m);
                d[o + 1] = Mix(sg, d[o + 1], sa, da, m);
                d[o + 2] = Mix(sb, d[o + 2], sa, da, m);
                d[o + 3] = sa + da - sa * da;
            }
        }

        static float Mix(float s, float d, float sa, float da, int m)
        {
            float cs = s / sa, cd = d / da;
            float bl = m == 1 ? cs * cd : m == 2 ? cs + cd - cs * cd : Math.Min(1f, cs + cd);
            return (1 - da) * s + (1 - sa) * d + sa * da * bl;
        }
    }
}
