using System;
using System.Collections.Generic;

namespace Mori.Core.Art
{
    /// <summary>
    /// 뼈대 흔들기(그림만 — 판정과 무관). 뼈 마디를 이은 줄을 중력 · 관성으로 흔든다(베를레 적분).
    /// 좌표는 게임 세계 단위(U, 위 = +y). 첫 마디는 몸에 붙고, 나머지는 끌려가며 늦게 따라온다.
    /// 뻣뻣함 = 그린 모양(쉬는 자세)으로 돌아가려는 힘 — 앞 뼈가 꺾인 만큼 뒤 뼈의 쉬는 방향도 같이 돈다(모양 유지).
    /// </summary>
    public sealed class ChainSim
    {
        public sealed class Params
        {
            public int Segs = 6;                 // 마디 수(Lens 가 없을 때)
            public double SegLen = 0.1;          // 마디 길이(U, Lens 가 없을 때)
            public double[] Lens;                // 뼈마다 길이(U) — 있으면 이것
            public double[] RestX, RestY;        // 뼈마다 쉬는 방향(세계, 단위 벡터) — 없으면 RestDirX/Y 하나로
            public double RestDirX = 0, RestDirY = -1;
            public double Gravity = 20;          // 아래로(U/s²)
            public double Drag = 0.08;           // 공기 저항(0 ~ 1, 60fps 기준 한 번에 줄어드는 속도 비율)
            public double Stiff = 0.15;          // 쉬는 모양으로 돌아가려는 힘(0 ~ 1)
            public double Follow = 0.3;          // 몸이 움직인 만큼 같이 옮겨지는 정도(0 = 다 늦게 · 1 = 딱 붙어 감)

            public int N => Lens != null ? Lens.Length : Math.Max(1, Segs);
            public double Len(int i) => Lens != null ? Lens[i] : SegLen;
            public (double, double) Rest(int i)
            {
                if (RestX != null && i < RestX.Length) return Norm(RestX[i], RestY[i]);
                return Norm(RestDirX, RestDirY);
            }
            public double Total { get { double s = 0; for (int i = 0; i < N; i++) s += Len(i); return s; } }
        }

        public double[] X = new double[0], Y = new double[0], PX = new double[0], PY = new double[0];
        public bool Ready;
        double lastAx, lastAy;

        public int Count => X.Length;

        /// <summary>기준점에서 쉬는 모양 그대로.</summary>
        public void Reset(double ax, double ay, Params p)
        {
            int n = p.N + 1;
            if (X.Length != n) { X = new double[n]; Y = new double[n]; PX = new double[n]; PY = new double[n]; }
            X[0] = PX[0] = ax; Y[0] = PY[0] = ay;
            for (int i = 1; i < n; i++)
            {
                var (rx, ry) = p.Rest(i - 1);
                X[i] = PX[i] = X[i - 1] + rx * p.Len(i - 1);
                Y[i] = PY[i] = Y[i - 1] + ry * p.Len(i - 1);
            }
            lastAx = ax; lastAy = ay;
            Ready = true;
        }

        /// <summary>dt 초만큼. (ax, ay) = 이번 뿌리 자리. 너무 멀리 뛰면(순간이동 · 처음) 쉬는 모양으로 다시.</summary>
        public void Step(double dt, double ax, double ay, Params p)
        {
            int n = p.N + 1;
            double jump = Math.Sqrt((ax - lastAx) * (ax - lastAx) + (ay - lastAy) * (ay - lastAy));
            if (!Ready || X.Length != n || jump > Math.Max(1.5, p.Total * 4)) { Reset(ax, ay, p); return; }
            if (dt <= 0) return;
            dt = Math.Min(dt, 0.05);
            int sub = Math.Max(1, (int)Math.Ceiling(dt / (1.0 / 120)));
            double h = dt / sub;
            double sx = lastAx, sy = lastAy;
            double keep = Math.Pow(Clamp01(1 - p.Drag), h * 60);
            double follow = Clamp01(p.Follow), stiff = Clamp01(p.Stiff);
            for (int s = 1; s <= sub; s++)
            {
                double nx = lastAx + (ax - lastAx) * s / sub, ny = lastAy + (ay - lastAy) * s / sub;
                double dax = nx - sx, day = ny - sy;
                sx = nx; sy = ny;
                for (int i = 1; i < n; i++)
                {
                    double vx = (X[i] - PX[i]) * keep, vy = (Y[i] - PY[i]) * keep;
                    PX[i] = X[i] + dax * follow; PY[i] = Y[i] + day * follow;      // 몸을 따라 옮긴 만큼은 속도로 치지 않음
                    X[i] += vx + dax * follow;
                    Y[i] += vy + day * follow - p.Gravity * h * h;
                }
                X[0] = PX[0] = nx; Y[0] = PY[0] = ny;
                // 모양 유지: 그린 모양 쪽으로 당김(앞 뼈가 돈 만큼 뒤 뼈의 쉬는 방향도 같이 돎) — 세기 = 뻣뻣함²
                double k = stiff * stiff * 0.4;
                if (k > 0)
                {
                    double rot = 0;
                    for (int i = 1; i < n; i++)
                    {
                        double len = p.Len(i - 1);
                        var (r0x, r0y) = p.Rest(i - 1);
                        double rx = r0x, ry = r0y;
                        if (rot != 0) { double c = Math.Cos(rot), sn = Math.Sin(rot); (rx, ry) = (r0x * c - r0y * sn, r0x * sn + r0y * c); }
                        double tx = X[i - 1] + rx * len, ty = Y[i - 1] + ry * len;
                        X[i] += (tx - X[i]) * k; Y[i] += (ty - Y[i]) * k;
                        rot = Math.Atan2(Y[i] - Y[i - 1], X[i] - X[i - 1]) - Math.Atan2(r0y, r0x);
                    }
                }
                // 길이 맞추기(뿌리부터)
                for (int it = 0; it < 4; it++)
                    for (int i = 1; i < n; i++)
                    {
                        double len = p.Len(i - 1);
                        double dx = X[i] - X[i - 1], dy = Y[i] - Y[i - 1];
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        if (d < 1e-9) { var (rx, ry) = p.Rest(i - 1); X[i] = X[i - 1] + rx * len; Y[i] = Y[i - 1] + ry * len; continue; }
                        double f = len / d;
                        X[i] = X[i - 1] + dx * f; Y[i] = Y[i - 1] + dy * f;
                    }
            }
            lastAx = ax; lastAy = ay;
        }

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
        internal static (double, double) Norm(double x, double y)
        {
            double d = Math.Sqrt(x * x + y * y);
            return d < 1e-9 ? (0, -1) : (x / d, y / d);
        }
    }

    /// <summary>
    /// PSD 의 사슬 레이어:
    ///  · ‘~이름’ = 휘는 그림(머리카락 · 천 — 직접 그린 것). 몸 그림에는 안 들어가고 뼈대를 따라 휘어서 그려짐.
    ///  · ‘@이름’ = 그 그림의 뼈 마디(점들). 점 색의 초록 값이 순서(0, 16, 32 … — 편집기 ‘뼈대’ 도구가 찍음), 첫 점 = 뿌리.
    ///  · 이름 뒤 ‘(앞)’ = 몸 앞에, 아니면 몸 뒤에.
    /// </summary>
    public static class ChainAnchors
    {
        public const byte BoneR = 255, BoneB = 77;
        /// <summary>i 번째 뼈 마디를 찍을 색(초록 = 순서).</summary>
        public static (byte r, byte g, byte b) JointColor(int i) => (BoneR, (byte)Math.Min(255, i * 16), BoneB);

        /// <summary>"@머리1(뒤)" → ("머리1", 뒤). 뼈 레이어(@)만.</summary>
        public static bool Parse(string layerName, out string name, out bool back)
        {
            bool ok = ParseAny(layerName, out bool deform, out name, out back);
            return ok && !deform;
        }

        /// <summary>‘@이름’(뼈) 또는 ‘~이름’(휘는 그림). deform = ~.</summary>
        public static bool ParseAny(string layerName, out bool deform, out string name, out bool back)
        {
            name = null; back = true; deform = false;
            if (string.IsNullOrEmpty(layerName)) return false;
            var s = layerName.Trim();
            if (s.StartsWith("@")) deform = false;
            else if (s.StartsWith("~")) deform = true;
            else return false;
            s = s.Substring(1).Trim();
            if (s.Contains("(앞)") || s.EndsWith(" 앞") || s.Contains("(front)")) back = false;
            foreach (var tag in new[] { "(앞)", "(뒤)", "(front)", "(back)" }) s = s.Replace(tag, "");
            s = s.Trim();
            if (s.EndsWith(" 앞") || s.EndsWith(" 뒤")) s = s.Substring(0, s.Length - 2).Trim();
            if (s.Length == 0) s = "사슬";
            name = s;
            return true;
        }

        /// <summary>
        /// 점 픽셀들 → 뼈 마디(순서대로, 캔버스 좌표 · 픽셀 가운데). 초록 값이 다르면 그 순서, 다 같으면(손으로 찍은 점 하나) 가운데 한 점.
        /// </summary>
        public static List<(double x, double y)> Joints(List<(int x, int y, byte g, byte a)> px)
        {
            var o = new List<(double, double)>();
            if (px == null || px.Count == 0) return o;
            var groups = new SortedDictionary<int, (double sx, double sy, double sa)>();
            foreach (var (x, y, g, a) in px)
            {
                if (a == 0) continue;
                int k = (g + 8) / 16;
                groups.TryGetValue(k, out var v);
                groups[k] = (v.sx + (x + 0.5) * a, v.sy + (y + 0.5) * a, v.sa + a);
            }
            foreach (var v in groups.Values) if (v.sa > 0) o.Add((v.sx / v.sa, v.sy / v.sa));
            return o;
        }

        public static List<(double x, double y)> Joints(PsdNode n)
        {
            var px = new List<(int, int, byte, byte)>();
            if (n != null && n.Rgba != null)
                for (int yy = 0; yy < n.H; yy++)
                    for (int xx = 0; xx < n.W; xx++)
                    {
                        int k = (yy * n.W + xx) * 4;
                        if (n.Rgba[k + 3] != 0) px.Add((n.L + xx, n.T + yy, n.Rgba[k + 1], n.Rgba[k + 3]));
                    }
            return Joints(px);
        }

        /// <summary>레이어 그림의 불투명 픽셀 가운데(캔버스 좌표). 비었으면 false.</summary>
        public static bool Centroid(PsdNode n, out double x, out double y)
        {
            x = y = 0;
            if (n == null || n.Rgba == null || n.W <= 0 || n.H <= 0) return false;
            double sx = 0, sy = 0, sa = 0;
            for (int yy = 0; yy < n.H; yy++)
                for (int xx = 0; xx < n.W; xx++)
                {
                    int a = n.Rgba[(yy * n.W + xx) * 4 + 3];
                    if (a == 0) continue;
                    sx += (n.L + xx + 0.5) * a; sy += (n.T + yy + 0.5) * a; sa += a;
                }
            if (sa <= 0) return false;
            x = sx / sa; y = sy / sa;
            return true;
        }

        /// <summary>
        /// 문서에서 @ · ~ 레이어를 떼어 낸다(몸 그림 합치기에 안 들어가게). (레이어, 그 레이어가 든 맨 위 폴더 — 폴더 밖이면 null).
        /// </summary>
        public static List<(PsdNode leaf, PsdNode top)> Strip(PsdDoc doc)
        {
            var o = new List<(PsdNode, PsdNode)>();
            void Walk(PsdNode g, PsdNode top)
            {
                for (int i = g.Kids.Count - 1; i >= 0; i--)
                {
                    var k = g.Kids[i];
                    if (k.Group) { Walk(k, top ?? k); continue; }
                    if (!ParseAny(k.Name, out _, out _, out _)) continue;
                    o.Add((k, top));
                    g.Kids.RemoveAt(i);
                }
            }
            Walk(doc.Root, null);
            return o;
        }
    }

    /// <summary>
    /// 휘는 그림 그리기(픽셀 격자 그대로): 쉬는 뼈대를 따라 띠 모양 그물을 씌우고, 지금 뼈대로 옮긴 그물의 칸마다 원래 그림을 가장 가까운 픽셀로 가져온다.
    /// 쉬는 자세면 원래 그림과 똑같고, 꺾이면 그림이 그 모양대로 휜다(회전된 픽셀 · 번짐 없음).
    /// </summary>
    public static class BoneWarp
    {
        /// <summary>그림 픽셀이 뼈대에서 가장 먼 거리(띠 반폭) — 끝 너머 그림도 들어가게 양끝을 그만큼 늘림.</summary>
        public static double HalfWidth(byte[] art, int aw, int ah, int ax0, int ay0, IList<(double x, double y)> bones)
        {
            double best = 1;
            for (int y = 0; y < ah; y++)
                for (int x = 0; x < aw; x++)
                {
                    if (art[(y * aw + x) * 4 + 3] == 0) continue;
                    double px = ax0 + x + 0.5, py = ay0 + y + 0.5, d = double.MaxValue;
                    for (int i = 0; i + 1 < bones.Count; i++) d = Math.Min(d, SegDist(px, py, bones[i], bones[i + 1]));
                    if (bones.Count == 1) d = Math.Sqrt((px - bones[0].x) * (px - bones[0].x) + (py - bones[0].y) * (py - bones[0].y));
                    if (d > best) best = d;
                }
            return best + 1;
        }

        static double SegDist(double px, double py, (double x, double y) a, (double x, double y) b)
        {
            double vx = b.x - a.x, vy = b.y - a.y, l2 = vx * vx + vy * vy;
            double t = l2 <= 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((px - a.x) * vx + (py - a.y) * vy) / l2));
            double dx = px - (a.x + vx * t), dy = py - (a.y + vy * t);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>뼈대 양끝을 hw 만큼 늘린 점들(끝 너머 그림까지 덮게).</summary>
        static List<(double x, double y)> Extend(IList<(double x, double y)> b, double hw)
        {
            var o = new List<(double, double)>(b);
            if (b.Count < 2) return o;
            var (sx, sy) = ChainSim.Norm(b[0].x - b[1].x, b[0].y - b[1].y);
            var (ex, ey) = ChainSim.Norm(b[b.Count - 1].x - b[b.Count - 2].x, b[b.Count - 1].y - b[b.Count - 2].y);
            o.Insert(0, (b[0].x + sx * hw, b[0].y + sy * hw));
            o.Add((b[b.Count - 1].x + ex * hw, b[b.Count - 1].y + ey * hw));
            return o;
        }

        /// <summary>점마다 띠의 왼 · 오른 가장자리(앞뒤 뼈 방향의 평균에 수직).</summary>
        static void Edges(List<(double x, double y)> b, double hw, out (double x, double y)[] L, out (double x, double y)[] R)
        {
            int n = b.Count;
            L = new (double, double)[n]; R = new (double, double)[n];
            for (int i = 0; i < n; i++)
            {
                var (ax, ay) = i > 0 ? ChainSim.Norm(b[i].x - b[i - 1].x, b[i].y - b[i - 1].y) : ChainSim.Norm(b[1].x - b[0].x, b[1].y - b[0].y);
                var (bx, by) = i < n - 1 ? ChainSim.Norm(b[i + 1].x - b[i].x, b[i + 1].y - b[i].y) : (ax, ay);
                var (tx, ty) = ChainSim.Norm(ax + bx, ay + by);
                double nx = -ty, ny = tx;
                L[i] = (b[i].x + nx * hw, b[i].y + ny * hw);
                R[i] = (b[i].x - nx * hw, b[i].y - ny * hw);
            }
        }

        /// <summary>
        /// rest = 쉬는 뼈대(그림 좌표), cur = 지금 뼈대(같은 좌표계, 점 수 같음). art = (ax0, ay0) 에 놓인 aw×ah 그림.
        /// 결과 = (ox, oy) 부터 ow×oh 의 RGBA(위 → 아래). 돌려주는 값 = 칠한 픽셀 수.
        /// </summary>
        public static int Warp(byte[] art, int aw, int ah, int ax0, int ay0, IList<(double x, double y)> rest, IList<(double x, double y)> cur, double hw,
                               byte[] dst, int ox, int oy, int ow, int oh)
        {
            if (rest.Count < 2 || cur.Count != rest.Count) return 0;
            var rb = Extend(rest, hw); var cb = Extend(cur, hw);
            Edges(rb, hw, out var rL, out var rR);
            Edges(cb, hw, out var cL, out var cR);
            int painted = 0;
            for (int i = 0; i + 1 < rb.Count; i++)
            {
                // 칸 하나 = 삼각형 두 개(L_i, R_i, R_i+1) · (L_i, R_i+1, L_i+1)
                painted += Tri(art, aw, ah, ax0, ay0, cL[i], cR[i], cR[i + 1], rL[i], rR[i], rR[i + 1], dst, ox, oy, ow, oh);
                painted += Tri(art, aw, ah, ax0, ay0, cL[i], cR[i + 1], cL[i + 1], rL[i], rR[i + 1], rL[i + 1], dst, ox, oy, ow, oh);
            }
            return painted;
        }

        static int Tri(byte[] art, int aw, int ah, int ax0, int ay0,
                       (double x, double y) c0, (double x, double y) c1, (double x, double y) c2,
                       (double x, double y) r0, (double x, double y) r1, (double x, double y) r2,
                       byte[] dst, int ox, int oy, int ow, int oh)
        {
            double den = (c1.y - c2.y) * (c0.x - c2.x) + (c2.x - c1.x) * (c0.y - c2.y);
            if (Math.Abs(den) < 1e-9) return 0;
            int x0 = (int)Math.Floor(Math.Min(c0.x, Math.Min(c1.x, c2.x))), x1 = (int)Math.Ceiling(Math.Max(c0.x, Math.Max(c1.x, c2.x)));
            int y0 = (int)Math.Floor(Math.Min(c0.y, Math.Min(c1.y, c2.y))), y1 = (int)Math.Ceiling(Math.Max(c0.y, Math.Max(c1.y, c2.y)));
            x0 = Math.Max(x0, ox); y0 = Math.Max(y0, oy); x1 = Math.Min(x1, ox + ow); y1 = Math.Min(y1, oy + oh);
            int n = 0;
            const double E = -1e-7;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    double w0 = ((c1.y - c2.y) * (px - c2.x) + (c2.x - c1.x) * (py - c2.y)) / den;
                    double w1 = ((c2.y - c0.y) * (px - c2.x) + (c0.x - c2.x) * (py - c2.y)) / den;
                    double w2 = 1 - w0 - w1;
                    if (w0 < E || w1 < E || w2 < E) continue;
                    double sx = w0 * r0.x + w1 * r1.x + w2 * r2.x, sy = w0 * r0.y + w1 * r1.y + w2 * r2.y;
                    int ix = (int)Math.Floor(sx) - ax0, iy = (int)Math.Floor(sy) - ay0;
                    if (ix < 0 || iy < 0 || ix >= aw || iy >= ah) continue;
                    int si = (iy * aw + ix) * 4;
                    if (art[si + 3] == 0) continue;
                    int di = ((y - oy) * ow + (x - ox)) * 4;
                    if (dst[di + 3] != 0) continue;                 // 먼저 칠한 칸(뿌리 쪽)이 이김
                    dst[di] = art[si]; dst[di + 1] = art[si + 1]; dst[di + 2] = art[si + 2]; dst[di + 3] = art[si + 3];
                    n++;
                }
            return n;
        }
    }
}
