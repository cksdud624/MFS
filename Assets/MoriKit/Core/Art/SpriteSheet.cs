using System;
using System.Collections.Generic;

namespace Mori.Core.Art
{
    /// <summary>
    /// 스프라이트 시트 자동 나누기 · 컷 정렬(RGBA, 위 → 아래).
    ///  · Detect: 칸 사이의 투명한 줄을 찾아 몇 칸 × 몇 줄인지(등분). 나누어떨어지지 않아도(443.5) 칸마다 반올림해 밀림 없이.
    ///  · Shifts: 컷마다 옮길 양 — 0 원본 · 1 발밑(가로 · 세로) · 2 몸통(가로, 컷끼리 겹쳐 맞추기 — 날리는 머리 · 천은 덜 봄).
    /// </summary>
    public static class SpriteSheet
    {
        public const int ALPHA = 24;           // 이보다 투명하면 빈 곳

        public enum Align { None = 0, Feet = 1, Body = 2 }

        public sealed class Cell
        {
            public byte[] Px; public int W, H;
            public double Ax, Ay;               // 바닥 기준점(캔버스의 아래 가운데에 놓일 점)
            public int Col, Row;
        }

        /// <summary>칸 수 찾기. 못 찾으면 1.</summary>
        public static void Detect(byte[] px, int w, int h, out int cols, out int rows)
        {
            var colOcc = new int[w]; var rowOcc = new int[h];
            for (int y = 0; y < h; y++)
            {
                int k = y * w * 4 + 3;
                for (int x = 0; x < w; x++, k += 4)
                    if (px[k] > ALPHA) { colOcc[x]++; rowOcc[y]++; }
            }
            cols = Best(colOcc, h);
            rows = Best(rowOcc, w);
        }

        /// <summary>occ = 줄마다 불투명 픽셀 수, depth = 줄 길이. 경계가 모두 (거의) 빈 줄에 걸리는 가장 큰 N.</summary>
        static int Best(int[] occ, int depth)
        {
            int n = occ.Length;
            long total = 0; foreach (var v in occ) total += v;
            if (total == 0) return 1;
            int tol = Math.Max(2, depth * 3 / 100);           // 머리카락 몇 가닥이 걸쳐도 됨
            double mean = (double)total / n;
            int best = 1;
            for (int N = 2; N <= 16 && n / N >= 8; N++)
            {
                double cw = (double)n / N;
                int hw = Math.Max(1, (int)(cw * 0.06));
                // 경계마다 근처에서 가장 빈 줄 — 완전히 비었거나(tol), 잔상 · 머리카락이 이어져도 골짜기(평균의 60% 이하)면 칸 경계
                int worst = 0;
                for (int i = 1; i < N; i++)
                {
                    int b = (int)Math.Round(i * cw);
                    int mn = int.MaxValue;
                    for (int x = Math.Max(0, b - hw); x <= Math.Min(n - 1, b + hw); x++) mn = Math.Min(mn, occ[x]);
                    worst = Math.Max(worst, mn);
                }
                if (worst > tol && worst > mean * 0.6) continue;
                // 칸마다 그림이 있어야(빈 칸이 반 넘으면 아님)
                int empty = 0;
                for (int i = 0; i < N; i++)
                {
                    long s = 0;
                    for (int x = (int)Math.Round(i * cw); x < Math.Min(n, (int)Math.Round((i + 1) * cw)); x++) s += occ[x];
                    if (s < total / (N * 20)) empty++;
                }
                if (empty * 2 > N) continue;
                best = N;
            }
            return best;
        }

        /// <summary>
        /// 시트를 칸으로 잘라 컷들로(빈 칸 건너뜀, 왼쪽 → 오른쪽, 위 → 아래). 칸 크기는 실수(443.5) 가능.
        /// 그림은 칸 경계로 자르지 않는다 — 붙어 있는 덩어리(8방향)마다 어느 칸의 것인지 정해서(무게 중심, 작은 조각은 가장 가까운 캐릭터로)
        /// 칸 밖으로 삐져나온 발끝 · 머리카락도 그 컷에 그대로 붙인다. 두 칸에 크게 걸친 덩어리(캐릭터끼리 붙음)만 칸 경계로 나눔.
        /// 모든 컷은 같은 크기(모든 그림 + 정렬 이동이 다 들어가게). 바닥 기준점 = 원래 칸의 아래 가운데.
        /// </summary>
        public static List<Cell> Split(byte[] px, int w, int h, double cellW, double cellH, int cols, int rows, Align align, int refIndex = 0)
        {
            int cw = (int)Math.Ceiling(cellW - 1e-6), ch = (int)Math.Ceiling(cellH - 1e-6);
            int nCell = cols * rows;
            int CellOf(int x, int y)
            {
                int c = Math.Min(cols - 1, Math.Max(0, (int)Math.Floor((x + 0.5) / cellW)));
                int r = Math.Min(rows - 1, Math.Max(0, (int)Math.Floor((y + 0.5) / cellH)));
                return r * cols + c;
            }
            // 1) 덩어리 나누기(진한 픽셀만 — 옅은 잡티로 캐릭터끼리 이어지지 않게)
            var lab = new int[w * h];
            var cnt = new List<int> { 0 }; var sx = new List<double> { 0 }; var sy = new List<double> { 0 };
            var bx0 = new List<int> { 0 }; var by0 = new List<int> { 0 }; var bx1 = new List<int> { 0 }; var by1 = new List<int> { 0 };
            var stack = new Stack<int>();
            for (int s0 = 0; s0 < w * h; s0++)
            {
                if (lab[s0] != 0 || px[s0 * 4 + 3] <= ALPHA) continue;
                int id = cnt.Count;
                cnt.Add(0); sx.Add(0); sy.Add(0); bx0.Add(int.MaxValue); by0.Add(int.MaxValue); bx1.Add(-1); by1.Add(-1);
                lab[s0] = id; stack.Push(s0);
                while (stack.Count > 0)
                {
                    int k = stack.Pop();
                    int x = k % w, y = k / w;
                    cnt[id]++; sx[id] += x + 0.5; sy[id] += y + 0.5;
                    if (x < bx0[id]) bx0[id] = x; if (x > bx1[id]) bx1[id] = x;
                    if (y < by0[id]) by0[id] = y; if (y > by1[id]) by1[id] = y;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy; if (ny < 0 || ny >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx; if (nx < 0 || nx >= w) continue;
                            int q = ny * w + nx;
                            if (lab[q] != 0 || px[q * 4 + 3] <= ALPHA) continue;
                            lab[q] = id; stack.Push(q);
                        }
                    }
                }
            }
            int nComp = cnt.Count;
            // 2) 덩어리마다 칸별 픽셀 수 → 두 칸에 크게 걸치면 ‘나눔’(-1), 아니면 무게 중심 칸
            var owner = new int[nComp];
            var perCell = new int[nCell];
            var split = new bool[nComp];
            {
                // 칸별 수를 덩어리마다 세려면 한 번 더 훑는다(덩어리 수 × 칸 수 배열은 클 수 있어 사전 사용)
                var dist = new Dictionary<long, int>();
                for (int k = 0; k < w * h; k++)
                {
                    int id = lab[k]; if (id == 0) continue;
                    long key = (long)id * nCell + CellOf(k % w, k / w);
                    dist.TryGetValue(key, out int v); dist[key] = v + 1;
                }
                var best1 = new int[nComp]; var best2 = new int[nComp];
                foreach (var kv in dist)
                {
                    int id = (int)(kv.Key / nCell);
                    if (kv.Value > best1[id]) { best2[id] = best1[id]; best1[id] = kv.Value; }
                    else if (kv.Value > best2[id]) best2[id] = kv.Value;
                }
                for (int id = 1; id < nComp; id++)
                {
                    // 옆 칸으로 넘어간 양이 칸 넓이의 2% 넘으면(잔상 · 캐릭터끼리 이어짐) 칸 경계로 나눔, 작으면(발끝) 통째로 주인 칸에
                    split[id] = cnt[id] - best1[id] > (long)cw * ch / 50;
                    owner[id] = split[id] ? -1 : CellOf((int)(sx[id] / cnt[id]), (int)(sy[id] / cnt[id]));
                    if (owner[id] >= 0) perCell[owner[id]] += cnt[id];
                }
            }
            // 3) 작은 조각(그 칸 그림의 2% 미만)은 큰 덩어리가 가장 가까운 칸으로
            var bigL = new int[nCell]; var bigT = new int[nCell]; var bigR = new int[nCell]; var bigB = new int[nCell];
            for (int c = 0; c < nCell; c++) { bigL[c] = bigT[c] = int.MaxValue; bigR[c] = bigB[c] = -1; }
            for (int id = 1; id < nComp; id++)
            {
                int o = owner[id]; if (o < 0 || cnt[id] * 50 < perCell[o]) continue;
                bigL[o] = Math.Min(bigL[o], bx0[id]); bigT[o] = Math.Min(bigT[o], by0[id]); bigR[o] = Math.Max(bigR[o], bx1[id]); bigB[o] = Math.Max(bigB[o], by1[id]);
            }
            for (int id = 1; id < nComp; id++)
            {
                int o = owner[id]; if (o < 0 || cnt[id] * 50 >= perCell[o]) continue;
                long best = long.MaxValue; int bc = o;
                for (int c = 0; c < nCell; c++)
                {
                    if (bigR[c] < 0) continue;
                    long gx = Math.Max(0, Math.Max(bigL[c] - bx1[id], bx0[id] - bigR[c]));
                    long gy = Math.Max(0, Math.Max(bigT[c] - by1[id], by0[id] - bigB[c]));
                    long d = gx * gx + gy * gy;
                    if (d < best || (d == best && c == o)) { best = d; bc = c; }
                }
                owner[id] = bc;
            }
            // 4) 칸마다 픽셀 주인: 덩어리 주인, 나눈 덩어리 · 옅은 픽셀은 칸 경계로
            int PixOwner(int k)
            {
                int id = lab[k];
                if (id != 0 && owner[id] >= 0) return owner[id];
                return CellOf(k % w, k / w);
            }
            var cl = new int[nCell]; var ct = new int[nCell]; var cr = new int[nCell]; var cb = new int[nCell];
            var solid = new bool[nCell];
            for (int c = 0; c < nCell; c++) { cl[c] = ct[c] = int.MaxValue; cr[c] = cb[c] = -1; }
            for (int k = 0; k < w * h; k++)
            {
                if (px[k * 4 + 3] == 0) continue;
                int c = PixOwner(k), x = k % w, y = k / w;
                if (x < cl[c]) cl[c] = x; if (x > cr[c]) cr[c] = x; if (y < ct[c]) ct[c] = y; if (y > cb[c]) cb[c] = y;
                if (px[k * 4 + 3] > ALPHA) solid[c] = true;
            }
            // 공통 상자(칸 원점 기준): 모든 컷의 그림이 다 들어가게
            int L = 0, T = 0, R = cw, B = ch;
            var keep = new List<int>();
            for (int c = 0; c < nCell; c++)
            {
                if (!solid[c]) continue;
                keep.Add(c);
                int x0 = (int)Math.Round((c % cols) * cellW), y0 = (int)Math.Round((c / cols) * cellH);
                L = Math.Min(L, cl[c] - x0); T = Math.Min(T, ct[c] - y0); R = Math.Max(R, cr[c] + 1 - x0); B = Math.Max(B, cb[c] + 1 - y0);
            }
            var cells = new List<Cell>();
            if (keep.Count == 0) return cells;
            int bw = R - L, bh = B - T;
            var index = new Dictionary<int, int>();
            foreach (var c in keep)
            {
                index[c] = cells.Count;
                cells.Add(new Cell { Px = new byte[bw * bh * 4], W = bw, H = bh, Ax = cw / 2.0 - L, Ay = ch - T, Col = c % cols, Row = c / cols });
            }
            for (int k = 0; k < w * h; k++)
            {
                if (px[k * 4 + 3] == 0) continue;
                int c = PixOwner(k);
                if (!index.TryGetValue(c, out int ci)) continue;
                int x0 = (int)Math.Round((c % cols) * cellW), y0 = (int)Math.Round((c / cols) * cellH);
                int x = k % w - x0 - L, y = k / w - y0 - T;
                Buffer.BlockCopy(px, k * 4, cells[ci].Px, (y * bw + x) * 4, 4);
            }
            if (align == Align.None) return cells;
            // 5) 정렬 — 옮긴 뒤에도 안 잘리게 상자를 다시 잡는다
            var imgs = new List<byte[]>(); foreach (var c in cells) imgs.Add(c.Px);
            var sh = Shifts(imgs, bw, bh, align, Math.Min(Math.Max(0, refIndex), cells.Count - 1), cw / 2.0 - L, ch - T);
            int L2 = int.MaxValue, T2 = int.MaxValue, R2 = int.MinValue, B2 = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!Bounds(cells[i].Px, bw, bh, out int l, out int t, out int r, out int b)) continue;
                L2 = Math.Min(L2, l + sh[i].dx); T2 = Math.Min(T2, t + sh[i].dy); R2 = Math.Max(R2, r + sh[i].dx); B2 = Math.Max(B2, b + sh[i].dy);
            }
            double ax = cw / 2.0 - L, ay = ch - T;
            L2 = Math.Min(L2, (int)Math.Floor(ax)); R2 = Math.Max(R2, (int)Math.Ceiling(ax)); T2 = Math.Min(T2, (int)Math.Floor(ay) - 1); B2 = Math.Max(B2, (int)Math.Ceiling(ay));
            int fw = R2 - L2, fh = B2 - T2;
            for (int i = 0; i < cells.Count; i++)
            {
                var o = new byte[fw * fh * 4];
                var src = cells[i].Px;
                int ox = sh[i].dx - L2, oy = sh[i].dy - T2;
                for (int y = 0; y < bh; y++)
                {
                    int ty = y + oy; if (ty < 0 || ty >= fh) continue;
                    for (int x = 0; x < bw; x++)
                    {
                        int si = (y * bw + x) * 4;
                        if (src[si + 3] == 0) continue;
                        int tx = x + ox; if (tx < 0 || tx >= fw) continue;
                        Buffer.BlockCopy(src, si, o, (ty * fw + tx) * 4, 4);
                    }
                }
                cells[i].Px = o; cells[i].W = fw; cells[i].H = fh; cells[i].Ax = ax - L2; cells[i].Ay = ay - T2;
            }
            return cells;
        }

        /// <summary>그림이 있는 최소 사각형(알파 &gt; 0, r · b 는 끝 다음).</summary>
        public static bool Bounds(byte[] px, int w, int h, out int l, out int t, out int r, out int b)
        {
            l = w; t = h; r = 0; b = 0;
            for (int y = 0; y < h; y++)
            {
                int k = y * w * 4 + 3;
                for (int x = 0; x < w; x++, k += 4)
                {
                    if (px[k] == 0) continue;
                    if (x < l) l = x;
                    if (x + 1 > r) r = x + 1;
                    if (y < t) t = y;
                    b = y + 1;
                }
            }
            return r > l && b > t;
        }

        /// <summary>w×h 그림을 (dx, dy) 만큼(밖으로 나간 곳은 버림).</summary>
        public static byte[] Shift(byte[] src, int w, int h, int dx, int dy)
        {
            if (dx == 0 && dy == 0) return src;
            var o = new byte[w * h * 4];
            int x0 = Math.Max(0, dx), x1 = Math.Min(w, w + dx);
            if (x1 <= x0) return o;
            for (int y = 0; y < h; y++)
            {
                int sy = y - dy; if (sy < 0 || sy >= h) continue;
                Buffer.BlockCopy(src, (sy * w + (x0 - dx)) * 4, o, (y * w + x0) * 4, (x1 - x0) * 4);
            }
            return o;
        }

        /// <summary>
        /// 컷마다 옮길 양. (ax, ay) = 바닥 기준점(보통 아래 가운데).
        ///  · Feet: 발밑(맨 아래 줄 근처 불투명 픽셀의 가운데)을 기준점에.
        ///  · Body: 기준 컷의 몸통 가운데를 ax 에, 다른 컷은 기준 컷과 몸통이 가장 많이 겹치게(가로만, 세로 움직임은 살림).
        /// </summary>
        public static (int dx, int dy)[] Shifts(IList<byte[]> imgs, int w, int h, Align mode, int refIndex, double ax, double ay)
        {
            var res = new (int, int)[imgs.Count];
            if (mode == Align.None || imgs.Count == 0) return res;
            if (mode == Align.Feet)
            {
                for (int i = 0; i < imgs.Count; i++)
                    if (Feet(imgs[i], w, h, out double fx, out int fb)) res[i] = ((int)Math.Round(ax - fx), (int)Math.Round(ay - fb));
                return res;
            }
            // 몸통: 굵은 부분만 남긴 지도(가는 머리카락 · 천 자락은 사라짐)
            int R = Math.Max(2, Math.Min(w, h) / 40);
            var refCore = Core(imgs[refIndex], w, h, R);
            double cx = 0, cs = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { float v = refCore[y * w + x]; cx += v * x; cs += v; }
            if (cs <= 0) return res;
            int refDx = (int)Math.Round(ax - (cx / cs + 0.5));
            int f0 = Math.Max(1, (int)Math.Ceiling(Math.Max(w, h) / 128.0));
            var refPyr = new Dictionary<int, (float[] a, int w, int h)>();
            for (int i = 0; i < imgs.Count; i++)
            {
                if (i == refIndex) { res[i] = (refDx, 0); continue; }
                var core = Core(imgs[i], w, h, R);
                int f = f0;
                var A = Level(refPyr, refCore, w, h, f); var B = Down(core, w, h, f);
                int range = Math.Max(2, A.w / 4);
                int d = BestDx(A.a, B.a, A.w, A.h, -range, range);
                while (f > 1)
                {
                    int nf = Math.Max(1, f / 4);
                    int k = f / nf;
                    A = Level(refPyr, refCore, w, h, nf); B = nf == 1 ? (core, w, h) : Down(core, w, h, nf);
                    d = BestDx(A.a, B.a, A.w, A.h, d * k - k, d * k + k);
                    f = nf;
                }
                res[i] = (d + refDx, 0);
            }
            return res;
        }

        static (float[] a, int w, int h) Level(Dictionary<int, (float[], int, int)> cache, float[] full, int w, int h, int f)
        {
            if (f == 1) return (full, w, h);
            if (!cache.TryGetValue(f, out var v)) cache[f] = v = Down(full, w, h, f);
            return v;
        }

        /// <summary>발밑: 맨 아래 불투명 줄(b, 끝 다음 줄) · 아래쪽 12% 의 불투명 픽셀 가로 가운데.</summary>
        public static bool Feet(byte[] px, int w, int h, out double fx, out int bottom)
        {
            fx = 0; bottom = 0;
            int top = -1, b = -1;
            for (int y = 0; y < h && top < 0; y++) for (int x = 0; x < w; x++) if (px[(y * w + x) * 4 + 3] > ALPHA) { top = y; break; }
            if (top < 0) return false;
            for (int y = h - 1; y >= 0 && b < 0; y--) for (int x = 0; x < w; x++) if (px[(y * w + x) * 4 + 3] > ALPHA) { b = y; break; }
            int band = Math.Max(2, (b - top + 1) * 12 / 100);
            double sx = 0; int n = 0;
            for (int y = Math.Max(top, b - band + 1); y <= b; y++)
                for (int x = 0; x < w; x++) if (px[(y * w + x) * 4 + 3] > ALPHA) { sx += x + 0.5; n++; }
            fx = n > 0 ? sx / n : w / 2.0;
            bottom = b + 1;
            return true;
        }

        /// <summary>굵은 부분 지도: 알파의 (2R+1)² 평균에서 0.55 넘는 만큼(0~1).</summary>
        static float[] Core(byte[] px, int w, int h, int R)
        {
            // 누적합(정수)
            var sat = new long[(w + 1) * (h + 1)];
            for (int y = 0; y < h; y++)
            {
                long run = 0;
                for (int x = 0; x < w; x++)
                {
                    run += px[(y * w + x) * 4 + 3];
                    sat[(y + 1) * (w + 1) + x + 1] = sat[y * (w + 1) + x + 1] + run;
                }
            }
            var o = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                int y0 = Math.Max(0, y - R), y1 = Math.Min(h, y + R + 1);
                for (int x = 0; x < w; x++)
                {
                    int x0 = Math.Max(0, x - R), x1 = Math.Min(w, x + R + 1);
                    long s = sat[y1 * (w + 1) + x1] - sat[y0 * (w + 1) + x1] - sat[y1 * (w + 1) + x0] + sat[y0 * (w + 1) + x0];
                    float m = s / (255f * (2 * R + 1) * (2 * R + 1));       // 가장자리는 밖을 투명으로
                    o[y * w + x] = m > 0.55f ? (m - 0.55f) / 0.45f : 0;
                }
            }
            return o;
        }

        static (float[] a, int w, int h) Down(float[] a, int w, int h, int f)
        {
            int dw = (w + f - 1) / f, dh = (h + f - 1) / f;
            var o = new float[dw * dh];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) o[(y / f) * dw + x / f] += a[y * w + x];
            float k = 1f / (f * f);
            for (int i = 0; i < o.Length; i++) o[i] *= k;
            return (o, dw, dh);
        }

        /// <summary>B 를 d 만큼 옮겼을 때 A 와 가장 많이 겹치는 d.</summary>
        static int BestDx(float[] A, float[] B, int w, int h, int d0, int d1)
        {
            double best = double.NegativeInfinity; int bd = 0;
            for (int d = d0; d <= d1; d++)
            {
                double s = 0;
                int xa = Math.Max(0, d), xb = Math.Min(w, w + d);
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = xa; x < xb; x++) s += A[row + x] * B[row + x - d];
                }
                if (s > best + 1e-9 || (Math.Abs(s - best) <= 1e-9 && Math.Abs(d) < Math.Abs(bd))) { best = s; bd = d; }
            }
            return bd;
        }
    }
}
