using System;
using System.Collections.Generic;

namespace Mori.Core.Fight
{
    /// <summary>축 정렬 상자(U). X0 &lt; X1, Y0 &lt; Y1(위가 +).</summary>
    public struct Box
    {
        public double X0, X1, Y0, Y1;
        public Box(double x0, double x1, double y0, double y1) { X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; }
        public bool Overlaps(Box b) => X0 < b.X1 && b.X0 < X1 && Y0 < b.Y1 && b.Y0 < Y1;
        public double Cx => (X0 + X1) * 0.5;
        public double Cy => (Y0 + Y1) * 0.5;
    }

    /// <summary>
    /// 결투장: 넓은 바닥(±HalfW, 양 끝은 벽) + 기둥(Solids — 막히고 위에 설 수 있고, 넉백되면 월 바운스) + 발판(Platforms — 위에서만 착지, 아래에서는 통과).
    /// 위치 선정 · 카이팅 · 벽을 이용한 월 바운스를 위해 지형을 둔다.
    /// </summary>
    public sealed class Arena
    {
        public double HalfW = 30 * FightUnit.S, Ceil = 11 * FightUnit.S;
        /// <summary>천장이 뚫림: 몸 · 넉백이 천장에 막히지 않는다(Ceil 은 맵 패턴 높이 기준으로만 씀).</summary>
        public bool OpenTop;
        public readonly List<Box> Solids = new List<Box>();
        public readonly List<Box> Platforms = new List<Box>();

        /// <summary>지형 조각 하나: 기둥(바닥부터 Top 까지, 막힘 · 위에 설 수 있음 · 월 바운스) 또는 발판(위에서만 착지, 두께 Thick). 튜닝 패널 ‘결투장 지형’ 에서 바꾼다(다음 판부터).</summary>
        public sealed class Piece
        {
            public string Name;
            public bool Platform;
            public double Cx, Width, Top, Thick = 0.3 * FightUnit.S;
            public Box ToBox() => Platform ? new Box(Cx - Width / 2, Cx + Width / 2, Top - Thick, Top) : new Box(Cx - Width / 2, Cx + Width / 2, 0, Top);
        }

        /// <summary>기본 결투장 지형: 기둥 넷 · 발판 다섯(좌우 대칭).</summary>
        public static readonly List<Piece> Layout = new List<Piece>
        {
            new Piece { Name = "기둥 1(왼쪽 끝)", Cx = -21 * FightUnit.S, Width = 1.0 * FightUnit.S, Top = 3.2 * FightUnit.S },
            new Piece { Name = "기둥 2(왼쪽 안)", Cx = -7.2 * FightUnit.S, Width = 0.8 * FightUnit.S, Top = 1.6 * FightUnit.S },
            new Piece { Name = "기둥 3(오른쪽 안)", Cx = 7.2 * FightUnit.S, Width = 0.8 * FightUnit.S, Top = 1.6 * FightUnit.S },
            new Piece { Name = "기둥 4(오른쪽 끝)", Cx = 21 * FightUnit.S, Width = 1.0 * FightUnit.S, Top = 3.2 * FightUnit.S },
            new Piece { Name = "발판 1(왼쪽 끝)", Platform = true, Cx = -25 * FightUnit.S, Width = 4 * FightUnit.S, Top = 3.1 * FightUnit.S },
            new Piece { Name = "발판 2(왼쪽)", Platform = true, Cx = -12.75 * FightUnit.S, Width = 4.5 * FightUnit.S, Top = 3.3 * FightUnit.S },
            new Piece { Name = "발판 3(가운데)", Platform = true, Cx = 0 * FightUnit.S, Width = 6 * FightUnit.S, Top = 4.5 * FightUnit.S },
            new Piece { Name = "발판 4(오른쪽)", Platform = true, Cx = 12.75 * FightUnit.S, Width = 4.5 * FightUnit.S, Top = 3.3 * FightUnit.S },
            new Piece { Name = "발판 5(오른쪽 끝)", Platform = true, Cx = 25 * FightUnit.S, Width = 4 * FightUnit.S, Top = 3.1 * FightUnit.S },
        };

        /// <summary>기본 결투장: 폭 60U, 지형은 Layout(폭 · 높이가 0 이하인 조각은 뺀다).</summary>
        public static Arena Standard()
        {
            var a = new Arena { HalfW = 30 * FightUnit.S, Ceil = 11 * FightUnit.S };
            foreach (var p in Layout)
            {
                if (p.Width <= 0 || p.Top <= 0 || (p.Platform && p.Thick <= 0)) continue;
                (p.Platform ? a.Platforms : a.Solids).Add(p.ToBox());
            }
            return a;
        }

        /// <summary>자유 평지: 지형 없이 바닥만, 양 끝 벽, 천장 뚫림. halfW = 가운데 → 벽(U).</summary>
        public static Arena Flat(double halfW) => new Arena { HalfW = halfW, Ceil = 11 * FightUnit.S, OpenTop = true };

        /// <summary>몸(가로 반폭 half, 높이 h)이 x 에서 y 높이일 때 겹치는 기둥이 있는지.</summary>
        public bool HitsSolid(double x, double y, double half, double h, out Box hit)
        {
            var b = new Box(x - half, x + half, y + 0.02 * FightUnit.S, y + h);
            foreach (var s in Solids) if (s.Overlaps(b)) { hit = s; return true; }
            hit = default;
            return false;
        }

        /// <summary>가로 이동: 벽 · 기둥에 막힘. 막혔으면 wall = true.</summary>
        public double MoveX(double x, double y, double dx, double half, double h, out bool wall)
        {
            wall = false;
            double nx = x + dx;
            if (nx < -HalfW + half) { nx = -HalfW + half; wall = true; }
            if (nx > HalfW - half) { nx = HalfW - half; wall = true; }
            if (HitsSolid(nx, y, half, h, out var s))
            {
                wall = true;
                nx = dx > 0 ? s.X0 - half - 1e-4 : dx < 0 ? s.X1 + half + 1e-4 : (x < s.Cx ? s.X0 - half - 1e-4 : s.X1 + half + 1e-4);
            }
            return nx;
        }

        /// <summary>세로 이동: 떨어지다 바닥 · 기둥 위 · 발판 위에 닿으면 착지(landed), 올라가다 기둥 아래면 막힘(bonk).</summary>
        public double MoveY(double x, double y, double dy, double half, double h, bool dropThrough, out bool landed, out bool bonk)
        {
            landed = bonk = false;
            double ny = y + dy;
            if (dy <= 0)
            {
                double best = 0;
                if (ny <= 0) { landed = true; best = 0; }
                foreach (var s in Solids)
                    if (x + half > s.X0 && x - half < s.X1 && y >= s.Y1 - 1e-6 && ny <= s.Y1) { if (!landed || s.Y1 > best) best = s.Y1; landed = true; }
                if (!dropThrough)
                    foreach (var p in Platforms)
                        if (x + half > p.X0 && x - half < p.X1 && y >= p.Y1 - 1e-6 && ny <= p.Y1) { if (!landed || p.Y1 > best) best = p.Y1; landed = true; }
                if (landed) return best;
            }
            else
            {
                if (!OpenTop && ny + h > Ceil) { bonk = true; return Ceil - h; }
                foreach (var s in Solids)
                    if (x + half > s.X0 && x - half < s.X1 && y + h <= s.Y0 + 1e-6 && ny + h > s.Y0) { bonk = true; return s.Y0 - h; }
            }
            return ny;
        }

        /// <summary>그 자리에 서 있을 수 있는 높이(발 아래 가장 높은 면). 공중이면 그 아래 면.</summary>
        public double SupportBelow(double x, double y, double half)
        {
            double best = 0;
            foreach (var s in Solids) if (x + half > s.X0 && x - half < s.X1 && s.Y1 <= y + 1e-6 && s.Y1 > best) best = s.Y1;
            foreach (var p in Platforms) if (x + half > p.X0 && x - half < p.X1 && p.Y1 <= y + 1e-6 && p.Y1 > best) best = p.Y1;
            return best;
        }

        public bool OnSupport(double x, double y, double half) => Math.Abs(SupportBelow(x, y, half) - y) < 1e-4;
    }
}
