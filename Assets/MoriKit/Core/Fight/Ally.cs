using System;
using System.Collections.Generic;

namespace Mori.Core.Fight
{
    public enum AllyState { Follow, Hold, Fly }
    public enum AllyEv { Shot, Strike, Fly }

    /// <summary>
    /// 동료(기계 공격 담당 여주인공) — 땅에 서서 플레이어를 엄호하는 노련한 사격수. 판정 · 피격 없이 보여 주는 존재,
    /// 기계 공격의 ‘누가 쏘는지’ 만 맡는다.
    ///
    /// 자리 잡기(엄호):
    ///  · 늘 플레이어보다 적에게서 먼 쪽(플레이어 뒤, 적 반대편)에 섬 — 최전선은 플레이어, 동료는 그보다 안전한 곳.
    ///  · 중장거리 사격수(저격수와 소총수 사이): 화면 안에서 적에게서 될 수 있는 한 먼 자리 — 단, 사거리 안 · 플레이어에서 최대 거리 안 · 화면 가장자리 안.
    ///  · 높은 곳 선호: 그 범위 안에 발판 · 기둥 윗면이 있으면 그 위(가장 먼 끝)를 저격 자리로 — 1층(바닥)은 높은 곳이 없을 때만.
    ///    다른 높이로 옮길 땐 드론에 매달려 날아가 내려앉음. 같은 면 위에서는 걸어서(면 끝 밖으로는 안 걸어 나감).
    ///  · 구석 · 고립 피하기: 결투장 벽 가까운 자리 · 플레이어에게서 너무 먼 자리 · 적 너머(플레이어와 갈라진) 자리는 벌점 — 드론으로 옮길 때도 같은 기준.
    ///  · 자리를 지킴: 이상적인 자리가 조금 움직이는 정도로는 서 있고, 범위를 벗어나거나 위험해지면(적이 플레이어보다 가까움) 그때 자리를 옮김.
    ///    플레이어가 꾸준히 걸어가면 같이 걸음(멈췄다 섰다 반복 없이).
    ///  · 움직임: 남은 거리에 비례한 속도 + 일정한 가속 — 출발 · 멈춤이 부드러움.
    ///  · 시선: 늘 적을 봄(적을 보며 뒷걸음 · 옆걸음). 멀리 옮겨야 할 때만 돌아서서 뜀.
    ///  · 적이 플레이어를 넘어가면 반대편으로 천천히 돌아 들어감(바로 뒤집히지 않게 잠깐 지켜본 뒤).
    /// 길 찾기:
    ///  · 기둥에 막히면 뛰어넘고, 플레이어가 발판 위에 서면 따라 올라가고, 아래로 내려가면 뚫고 내려감.
    ///  · 드론 비행: 적이 갑자기 가까워지거나(겹침 · 접촉 금지), 가야 할 자리가 적 너머이거나, 막혀서 못 가거나, 플레이어와 너무 멀어지면
    ///    작은 드론에 매달려 날아올라 적 위를 넘어 엄호 자리로 가서 내려앉음. 뒤쪽에 자리가 없으면(구석) 적 반대편에서 엄호.
    /// 사격:
    ///  · 플레이어 공격의 기계 순간(마법 → 기계 지연 뒤)에 그 자리에서 결투자 쪽으로 쏨(가슴 높이에서).
    ///  · 강한 사격: 3타 · 카운터 성공 · 크래시 때 버티고 서서 굵은 한 방(멀리 닿음) — 반동으로 살짝 밀리고 잠깐 굳은 뒤 다시 움직임.
    /// 데미지 판정은 Duel 이 Events(Shot · Strike)를 받아서 한다.
    /// </summary>
    public sealed class Ally
    {
        public const double HALF = 0.3 * FightUnit.S, HEIGHT = 1.7 * FightUnit.S;
        readonly FightTune T;
        public double X, Y, Vx, Vy;          // 발 가운데(U)
        public int Facing = 1;
        public bool Grounded = true;
        public AllyState State;
        /// <summary>지금 상태가 흐른 시간 · 마지막 사격 뒤 시간 · 돌아서기 남은 시간(그림 고르기).</summary>
        public double StateT, ShotAgo = 9, TurnT;
        /// <summary>마지막으로 쏜 곳(화면 광선) · 맞았는지.</summary>
        public double AimX, AimY;
        public bool AimHit;
        /// <summary>이번 명령의 콤보 단계 · 카운터 때 강한 사격인지.</summary>
        public int Combo;
        public bool CounterStrike;
        public readonly List<(AllyEv ev, int combo)> Events = new List<(AllyEv, int)>();
        /// <summary>그림 투명도(지금은 늘 1 — 예전 순간이동 연출 자리).</summary>
        public float Alpha => 1f;
        /// <summary>화면에 보이는 가로 범위(U) — 화면 쪽이 매 프레임 알려 줌. 모르면 무한.</summary>
        public double ViewL = double.NegativeInfinity, ViewR = double.PositiveInfinity;
        public bool Flying => State == AllyState.Fly;
        /// <summary>적을 보면서 적 반대쪽으로 걷는 중(뒷걸음 그림).</summary>
        public bool Backpedal => Math.Abs(Vx) > 0.25 * FightUnit.S && Math.Sign(Vx) == -Facing;

        double kickV, dropT, jumpCd, stuckT, highT, tgtX, anchorX, anchorY, sideHoldT, flyCd, flyTgt, flyTgtY, outRangeT;
        int anchorSide = -1;
        double offT, badT;
        bool placed, tgtSet, running;
        int side = -1;                       // 적 기준 동료가 설 쪽(−1 = 적의 왼쪽) — 늘 플레이어와 같은 쪽
        Arena map;
        Fighter foe;

        public Ally(FightTune t) { T = t; }

        public bool Busy => State != AllyState.Follow;
        /// <summary>쏘는 자리(가슴 앞).</summary>
        public double MuzzleX => X + Facing * 0.35 * FightUnit.S;
        public double MuzzleY => Y + Math.Max(0, T.AllyChestY);
        /// <summary>지금 가로 속도(그림: 걷기 · 뛰기 고르기).</summary>
        public double Speed => Math.Abs(Vx);

        double Lim => map.HalfW - HALF - 0.05 * FightUnit.S;
        double Clamp(double x) => Math.Max(-Lim, Math.Min(Lim, x));
        bool Engaged => foe != null && !(foe is Duelist d && d.KO);

        /// <summary>엄호 거리 범위(sd 쪽, 적에게서 가로로): dmin = 플레이어보다 최소 뒤, dmax = 될 수 있는 한 멀리(교전 거리 · 사거리 · 플레이어에서 최대 · 화면 끝).</summary>
        void Limits(PlayerFsm p, int sd, out double dmin, out double dmax)
        {
            double pd = Math.Abs(foe.X - p.X);
            if (Math.Sign(p.X - foe.X) != sd) pd = 0;                         // 반대편(구석 탈출): 플레이어와 상관없이 적 기준
            double minB = Math.Max(0, T.AllyBehindMin), maxB = Math.Max(minB, T.AllyMaxBehind);
            double d = Math.Min(Math.Max(0, T.AllyPrefDist), Math.Max(0.5 * FightUnit.S, T.AllyRange) * 0.92);
            if (pd > 0) d = Math.Min(d, pd + maxB);
            double m = Math.Max(0, T.AllyScreenMargin);
            double edge = sd < 0 ? foe.X - (ViewL + m) : (ViewR - m) - foe.X;
            if (!double.IsInfinity(edge)) d = Math.Min(d, edge);
            dmin = pd + minB;
            dmax = Math.Max(d, dmin);
        }

        /// <summary>바닥 엄호 자리(sd 쪽): 적에게서 될 수 있는 한 멀리. 적이 없으면 플레이어가 보는 방향의 반대쪽 뒤.</summary>
        double IdealX(PlayerFsm p, int sd)
        {
            if (!Engaged) return Clamp(p.X - p.Facing * Math.Max(0, T.AllyBackX));
            Limits(p, sd, out _, out double dmax);
            return Clamp(foe.X + sd * dmax);
        }
        double IdealX(PlayerFsm p) => IdealX(p, side);

        /// <summary>
        /// 저격 자리 고르기: 바닥(가장 먼 곳) vs 엄호 범위 안의 발판 · 기둥 윗면(그 면에서 가장 먼 끝).
        /// 점수 = 높이 × 높은 곳 선호 + 먼 정도(0 ~ 1), 지금 서 있는 면은 덤(자리를 자꾸 바꾸지 않게). 적과 너무 가까운 자리 · 너무 높은 곳은 뺌.
        /// </summary>
        void BestPerch(PlayerFsm p, int sd, out double px, out double py)
        {
            px = IdealX(p, sd); py = 0;
            if (!Engaged) return;
            Limits(p, sd, out double dmin, out double dmax);
            double a = foe.X + sd * dmin, b = foe.X + sd * dmax;
            double lo = Math.Max(-Lim, Math.Min(a, b)), hi = Math.Min(Lim, Math.Max(a, b));
            if (lo > hi) { lo = hi = Clamp(px); }
            // 바닥: 범위 안 여러 곳을 재서 가장 좋은 곳(구석 · 고립은 벌점)
            double best = SampleBest(p, lo, hi, 0, dmax, out px);
            if (Math.Abs(Y) < 1e-3 && Grounded && State != AllyState.Fly) best += 0.15;
            if (T.AllyHighPref <= 0) return;
            double maxTop = Math.Max(0, p.Grounded ? p.Y : map.SupportBelow(p.X, p.Y + 0.01, PlayerFsm.HALF)) + Math.Max(0.5 * FightUnit.S, T.AllyPerchMaxH);
            double w = Math.Max(0, T.AllyHighPref) * 0.5 / FightUnit.S;        // 높이 1(예전 단위)당 점수
            for (int k = 0; k < 2; k++)
                foreach (var bx in k == 0 ? map.Platforms : map.Solids)
                {
                    double top = bx.Y1;
                    if (top < 0.4 * FightUnit.S || top > maxTop) continue;
                    double sLo = bx.X0 + HALF * 0.8, sHi = bx.X1 - HALF * 0.8;
                    double iLo = Math.Max(lo, sLo), iHi = Math.Min(hi, sHi);
                    if (iLo > iHi) continue;
                    double sc = SampleBest(p, iLo, iHi, top, dmax, out double x);
                    if (double.IsNegativeInfinity(sc)) continue;
                    sc += top * w;
                    if (Math.Abs(Y - top) < 1e-3 && X >= sLo - 0.05 * FightUnit.S && X <= sHi + 0.05 * FightUnit.S && State != AllyState.Fly) sc += 0.6;   // 지금 서 있는 면
                    if (sc > best) { best = sc; px = x; py = top; }
                }
        }

        /// <summary>
        /// 한 면(높이 y)의 가로 범위 [lo, hi] 안에서 가장 좋은 자리 — 점수 = 적에게서 먼 정도 − 옮기는 거리 − 구석 벌점 − 고립 벌점.
        /// 위가 막혔거나 적과 위험한 곳은 뺌(모두 안 되면 −∞).
        /// </summary>
        double SampleBest(PlayerFsm p, double lo, double hi, double y, double dmax, out double bestX)
        {
            bestX = (lo + hi) / 2;
            double best = double.NegativeInfinity;
            int n = hi - lo < 0.05 * FightUnit.S ? 1 : 9;
            for (int i = 0; i < n; i++)
            {
                double x = n == 1 ? (lo + hi) / 2 : lo + (hi - lo) * i / (n - 1);
                if (y > 0 && map.HitsSolid(x, y, HALF, HEIGHT, out _)) continue;   // 위가 막힘
                if (!SafeAt(x, y)) continue;
                double sc = Score(p, x, y, dmax);
                if (sc > best) { best = sc; bestX = x; }
            }
            return best;
        }

        /// <summary>자리 점수(높이 빼고): 먼 정도(0 ~ 1) − 옮기는 거리 − 구석 − 고립.</summary>
        double Score(PlayerFsm p, double x, double y, double dmax)
        {
            double md = Math.Max(0.01, dmax);
            double sc = Math.Abs(x - foe.X) / md;
            sc -= Math.Abs(x - X) * Math.Max(0, T.AllyTravelW) / md + (Math.Abs(Y - y) > 1e-3 ? Math.Max(0, T.AllyTravelW) * 0.3 : 0);
            sc -= CornerPen(x) + IsoPen(p, x, y);
            return sc;
        }

        /// <summary>구석 벌점: 결투장 벽에 가까울수록(도망칠 곳이 없는 자리).</summary>
        double CornerPen(double x)
        {
            double cd = Math.Max(0.01, T.AllyCornerDist), dw = Lim - Math.Abs(x);
            double k = Math.Max(0, Math.Min(1, 1 - dw / cd));
            return Math.Max(0, T.AllyCornerW) * k * k;
        }

        /// <summary>고립 벌점: 플레이어에게서 너무 멀거나, 적이 플레이어와 나 사이에 끼면(혼자 떨어진 자리).</summary>
        double IsoPen(PlayerFsm p, double x, double y)
        {
            double id = Math.Max(0.01, T.AllyIsoDist);
            double dp = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));
            double pen = Math.Max(0, dp - id) / id;
            if (Engaged && Math.Sign(foe.X - p.X) == Math.Sign(x - foe.X) && Math.Abs(p.X - foe.X) > 0.5 * FightUnit.S) pen += 0.6;   // 적 너머(플레이어와 갈라짐)
            return Math.Max(0, T.AllyIsoW) * pen;
        }

        bool otherLevelPick(double y) => Math.Abs(y - Y) > 0.1 * FightUnit.S;

        /// <summary>지금 서 있는 면의 걸을 수 있는 가로 범위(바닥이면 결투장 전체).</summary>
        void SurfaceRange(out double lo, out double hi)
        {
            lo = -Lim; hi = Lim;
            if (Y < 1e-3) return;
            foreach (var bx in map.Platforms) if (Math.Abs(bx.Y1 - Y) < 1e-3 && X + HALF > bx.X0 && X - HALF < bx.X1) { lo = Math.Max(lo, bx.X0 + HALF * 0.8); hi = Math.Min(hi, bx.X1 - HALF * 0.8); return; }
            foreach (var bx in map.Solids) if (Math.Abs(bx.Y1 - Y) < 1e-3 && X + HALF > bx.X0 && X - HALF < bx.X1) { lo = Math.Max(lo, bx.X0 + HALF * 0.8); hi = Math.Min(hi, bx.X1 - HALF * 0.8); return; }
        }

        /// <summary>그 자리가 적과 안전한지: 가로로 충분히 멀거나, 적 머리보다 확실히 높음.</summary>
        bool SafeAt(double x, double y = 0) => !Engaged || Math.Abs(foe.X - x) >= Math.Max(0, T.AllyDangerDist) * 1.3 || y > foe.Y + foe.Height + 1.0 * FightUnit.S;
        /// <summary>적이 지금 위험하게 가까움(겹침 · 접촉 직전).</summary>
        bool Danger => Engaged && Math.Abs(foe.X - X) < Math.Max(0, T.AllyDangerDist) && foe.Y < Y + HEIGHT + 0.5 * FightUnit.S && foe.Y + foe.Height > Y - 0.5 * FightUnit.S;

        /// <summary>드론에 매달려 날아오름(목표는 비행 중 계속 엄호 자리로 갱신).</summary>
        void TakeOff(PlayerFsm p)
        {
            if (State == AllyState.Fly) return;
            State = AllyState.Fly; StateT = 0;
            Grounded = false; dropT = 0;
            Vy = Math.Max(Vy, 0.5 * Math.Max(0.1, T.AllyFlySpeed));            // 휙 떠오름
            BestPerch(p, side, out flyTgt, out flyTgtY);
            Events.Add((AllyEv.Fly, 0));
        }

        /// <summary>처음 · 라운드 시작: 바로 제자리.</summary>
        public void Snap(PlayerFsm p, Fighter e = null)
        {
            map = p.Map;
            if (e != null) foe = e;
            if (Engaged && Math.Abs(p.X - foe.X) > 1e-6) side = p.X < foe.X ? -1 : 1;
            else side = -p.Facing;
            BestPerch(p, side, out double sx, out double sy);
            X = anchorX = tgtX = sx; anchorY = sy; anchorSide = side; outRangeT = 0;
            tgtSet = true;
            Y = map.SupportBelow(X, sy + 0.01, HALF);
            Vx = Vy = 0; Grounded = true;
            Facing = Engaged ? (foe.X > X ? 1 : -1) : p.Facing;
            State = AllyState.Follow; StateT = 0; TurnT = 0;
            stuckT = highT = dropT = sideHoldT = flyCd = 0; running = false;
            placed = true;
        }


        void Face(int dir)
        {
            if (dir == 0 || dir == Facing) return;
            Facing = dir;
            TurnT = Math.Max(0, T.AllyTurnTime);
        }

        /// <summary>기계 공격 순간: 그 자리(플레이어 뒤)에서 결투자 쪽으로 사격. strong = 강한 사격(멀리 · 굵게 · 버팀).</summary>
        public void Command(int combo, bool strong, bool counter, PlayerFsm p, Fighter e)
        {
            foe = e;
            if (!placed) Snap(p, e);
            Combo = combo; CounterStrike = counter;
            bool inRange = Engaged && InRange(e, strong);
            int fdir = Engaged && Math.Abs(e.X - X) > 1e-6 ? (e.X > X ? 1 : -1) : p.Facing;
            Facing = fdir; TurnT = 0; running = false;
            AimHit = inRange;
            double reach = Math.Max(0.5 * FightUnit.S, strong ? T.AllyStrikeRange : T.AllyRange);
            AimX = inRange ? e.X : MuzzleX + fdir * reach;
            AimY = inRange ? e.Cy : MuzzleY;
            ShotAgo = 0;
            if (State == AllyState.Fly) { Events.Add((strong ? AllyEv.Strike : AllyEv.Shot, combo)); return; }    // 날면서도 쏨(버티기 없음)
            Vx *= 0.3;                                                         // 쏠 땐 거의 멈춤
            if (strong)
            {
                Vx = 0;
                kickV = -fdir * Math.Max(0, T.AllyKick) * 8;                   // 반동: 뒤로 미끄러짐(약 1/8초)
                State = AllyState.Hold; StateT = 0;
                Events.Add((AllyEv.Strike, combo));
            }
            else Events.Add((AllyEv.Shot, combo));
        }

        /// <summary>사격이 맞을 수 있는지(Duel 판정용).</summary>
        public bool InRange(Fighter e, bool strong = false)
        {
            double dx = e.X - X, dy = e.Cy - MuzzleY, r = Math.Max(0, strong ? T.AllyStrikeRange : T.AllyRange);
            return dx * dx + dy * dy <= r * r && Math.Abs(dy) <= Math.Max(0, T.AllyRangeH) * (strong ? 1.3 : 1);     // 높은 곳에서 내려 쏘기 포함
        }

        public void Tick(double dt, PlayerFsm p, Fighter e)
        {
            foe = e;
            if (!placed) Snap(p, e);
            map = p.Map;
            StateT += dt;
            ShotAgo += dt;
            if (TurnT > 0) TurnT -= dt;
            if (dropT > 0) dropT -= dt;
            if (jumpCd > 0) jumpCd -= dt;
            if (flyCd > 0) flyCd -= dt;

            if (State == AllyState.Hold)
            {
                // 강한 사격 버티기: 반동으로 뒤로 조금 미끄러진 뒤 굳음
                Vx = kickV;
                kickV *= Math.Exp(-dt * 8);
                X = map.MoveX(X, Y, Vx * dt, HALF, HEIGHT, out bool kw);
                if (kw) kickV = 0;
                Fall(dt);
                if (Danger) { TakeOff(p); return; }
                if (StateT >= Math.Max(0, T.AllyHoldTime)) { State = AllyState.Follow; StateT = 0; anchorX = X; anchorY = Y; anchorSide = side; }
                return;
            }

            // ── 설 쪽: 적이 플레이어를 넘어가면 잠깐 지켜본 뒤 반대편으로(왔다 갔다 하지 않게).
            //    플레이어 뒤에 안전한 자리가 없으면(구석) 적 반대편에서 엄호.
            if (Engaged)
            {
                int want = p.X < foe.X ? -1 : 1;
                if (!SafeAt(IdealX(p, want)) && SafeAt(IdealX(p, -want))) want = -want;
                bool clear = Math.Abs(p.X - foe.X) > 0.8 * FightUnit.S;
                sideHoldT = want != side && clear ? sideHoldT + dt : 0;
                if (sideHoldT > Math.Max(0, T.AllySideDelay)) { side = want; sideHoldT = 0; }
            }
            else side = -p.Facing;

            if (State == AllyState.Fly) { TickFly(dt, p); return; }

            // ── 위험: 적이 갑자기 가까워짐 → 바로 드론으로
            if (Danger) { TakeOff(p); TickFly(dt, p); return; }

            // ── 자리 지키기: 이상적인 자리가 범위를 벗어나거나, 위험해지거나(적이 플레이어보다 가까움), 플레이어가 꾸준히 걸어가면 그때 옮김
            // ── 자리 사수: 특별한 경우가 아니면 지금 자리를 지킴(플레이어가 조금 움직였다고 따라 옮기지 않음).
            //    다시 자리를 잡는 경우: ① 화면 밖으로 밀려남 ② 최전선이 됨(적이 플레이어보다 나에게 가까움, 높은 곳이면 괜찮음)
            //    ③ 적이 사거리 밖에 한동안 있음 ④ 설 쪽이 바뀜(적이 플레이어를 넘어감) — 위험(적 접근)은 위에서 바로 드론.
            //    다시 잡을 때도 같은 높이 · 같은 면에서 걸어서 갈 수 있으면 그쪽을 먼저(드론은 꼭 필요할 때만).
            double band = Math.Max(0.05 * FightUnit.S, T.AllyHoldBand);
            // 저격수: 화면 밖으로 실제로 밀려났을 때(잠깐 지나도) · 적이 꽤 가까운 최전선이 됐을 때만 — 설 쪽이 바뀌어도 쏠 수 있으면 그대로
            bool offNow = X < ViewL || X > ViewR;
            offT = offNow ? offT + dt : 0;
            bool offScreen = offT > 0.4;
            bool frontLine = Engaged && Math.Abs(foe.X - X) < Math.Abs(foe.X - p.X) && Math.Abs(foe.X - X) < Math.Max(0, T.AllyDangerDist) * 2.2 && !SafeAt(X, Y);
            bool outRange = Engaged && !InRange(foe);
            outRangeT = outRange ? outRangeT + dt : 0;
            // 구석 · 고립: 한동안 그런 자리에 있으면(벌점이 큼) 더 나은 자리로 — 급한 경우는 아니라 드론 쉬는 시간은 지킴
            bool badSpot = Engaged && (CornerPen(X) > Math.Max(0, T.AllyCornerW) * 0.45 || IsoPen(p, X, Y) > Math.Max(0, T.AllyIsoW) * 0.5);
            badT = badSpot ? badT + dt : 0;
            bool repick = offScreen || frontLine || (T.AllyOutRangeTime > 0 && outRangeT > T.AllyOutRangeTime) || badT > Math.Max(0, T.AllyBadSpotTime);
            bool hard = offScreen || frontLine;                                // 드론 쉬는 시간을 무시하고 날아도 되는 경우
            if (repick)
            {
                BestPerch(p, side, out double ideal, out double idealY);
                // 같은 면에서 걸어서 갈 수 있는 자리가 있으면 그쪽(먼 정도만 비교) — 드론을 아낌
                SurfaceRange(out double cLo, out double cHi);
                Limits(p, side, out double dmin, out double dmax);
                double a0 = foe != null ? foe.X + side * dmin : X, b0 = foe != null ? foe.X + side * dmax : X;
                double wLo = Math.Max(cLo, Math.Min(a0, b0)), wHi = Math.Min(cHi, Math.Max(a0, b0));
                // 높은 곳 위면 그 면에 남고, 바닥이면 고른 자리도 바닥일 때(또는 아직 드론을 못 쓸 때)만 걸어서
                bool walkable = Engaged && wLo <= wHi && (Y > 1e-3 || !otherLevelPick(idealY) || (flyCd > 0 && !hard));
                if (walkable)
                {
                    double ws = SampleBest(p, wLo, wHi, Y, dmax, out double wx);
                    if (!double.IsNegativeInfinity(ws)) { ideal = wx; idealY = Y; }
                }
                anchorX = ideal; anchorY = idealY; anchorSide = side; outRangeT = 0; badT = 0;
            }
            // 다른 높이의 자리 · 지금 면 밖의 자리 · 적 너머의 자리 → 걷지 않고 드론으로 날아감
            SurfaceRange(out double sLo, out double sHi);
            bool otherLevel = Math.Abs(anchorY - Y) > 0.1 * FightUnit.S;
            bool offSurface = Y > 1e-3 && (anchorX < sLo - band || anchorX > sHi + band);
            bool across = Engaged && Math.Sign(foe.X - X) == Math.Sign(anchorX - X) && Math.Abs(foe.X - X) < Math.Abs(anchorX - X) && !SafeAt(foe.X, Y);
            if (Grounded && (flyCd <= 0 || hard) && (otherLevel || offSurface || across)) { TakeOff(p); TickFly(dt, p); return; }
            if (otherLevel || offSurface) { anchorX = Math.Max(sLo, Math.Min(sHi, anchorX)); anchorY = Y; }   // 아직 못 날면 지금 면에서 버팀
            if (Y > 1e-3) anchorX = Math.Max(sLo, Math.Min(sHi, anchorX));       // 높은 곳 위: 면 끝 밖으로는 안 걸어 나감
            bool playerWalks = false;
            if (!tgtSet) { tgtX = anchorX; tgtSet = true; }
            tgtX += (anchorX - tgtX) * (1 - Math.Exp(-dt * Math.Max(0.1, T.AllyTargetSmooth)));

            // ── 움직임: 남은 거리 × 세기(+ 플레이어 속도 맞추기), 일정한 가속 · 감속
            double dx = tgtX - X, ad = Math.Abs(dx);
            double tol = Math.Max(0.02 * FightUnit.S, T.AllyTol);
            double eff = Math.Max(0, ad - tol * 0.5);
            double maxV = Math.Max(0.1 * FightUnit.S, T.AllyRunSpeed);
            double wantV = Math.Sign(dx) * eff * Math.Max(0.1, T.AllyGain);
            if (playerWalks) wantV += p.Vx * Math.Max(0, T.AllyMatch) * Math.Min(1, eff / Math.Max(0.01, band));
            if (Math.Sign(wantV) != Math.Sign(dx)) wantV = 0;                  // 자리 반대쪽으로는 안 감
            wantV = Math.Max(-maxV, Math.Min(maxV, wantV));
            bool moving = ad > tol;
            double acc = Math.Max(0.5 * FightUnit.S, T.AllyAccel) * dt * (Grounded ? 1 : 0.6);
            Vx += Math.Max(-acc, Math.Min(acc, wantV - Vx));
            if (Math.Abs(wantV) < 0.05 * FightUnit.S && Math.Abs(Vx) < 0.1 * FightUnit.S) Vx = 0;
            double ox = X;
            X = map.MoveX(X, Y, Vx * dt, HALF, HEIGHT, out bool wall);
            bool blocked = wall && moving && Math.Abs(X - ox) < Math.Abs(Vx * dt) * 0.5;
            if (wall) Vx = 0;

            // ── 길 찾기(땅에 있을 때만): 막힘 → 뛰어넘기 · 플레이어가 위 발판 → 올라가기 · 아래 → 뚫고 내려가기
            if (Grounded && T.AllyJumpOn >= 0.5 && jumpCd <= 0)
            {
                if (blocked)
                {
                    int dir = Math.Sign(dx);
                    if (map.HitsSolid(X + dir * (HALF + 0.1 * FightUnit.S), Y, HALF, HEIGHT, out var sb)) Jump(sb.Y1 - Y + 0.4 * FightUnit.S);
                }
            }
            Fall(dt);

            // ── 막혀서 못 따라감 · 못 올라감 · 너무 멀어짐 → 드론에 매달려 날아감
            bool stuck = moving && Grounded && Math.Abs(X - ox) < 0.15 * FightUnit.S * dt;
            stuckT = stuck ? stuckT + dt : 0;
            highT = 0;
            double stuckLim = Math.Max(0.1, T.AllyStuckTime);
            if (stuckT > stuckLim || Math.Abs(p.X - X) > Math.Max(2 * FightUnit.S, T.AllyWarpDist)) { stuckT = highT = 0; TakeOff(p); return; }

            // ── 시선: 적을 보며 움직임(뒷걸음 · 옆걸음). 멀리 옮길 때만 돌아서서 뜀 — 가까워지면 다시 적 쪽으로
            if (ShotAgo > Math.Max(0.05, T.AllyShootTime))
            {
                double runD = Math.Max(0.2 * FightUnit.S, T.AllyTurnRunDist);
                if (!running && ad > runD && Math.Abs(Vx) > 1 * FightUnit.S) running = true;
                if (running && ad < runD * 0.4) running = false;
                int look = Engaged ? (foe.X > X ? 1 : -1) : p.Facing;
                Face(running && Math.Abs(Vx) > 0.5 * FightUnit.S ? Math.Sign(Vx) : look);
            }
        }

        /// <summary>
        /// 드론 비행: 떠올라 순항 높이로(적 너머로 가면 적 머리 위보다 높게) 엄호 자리 쪽으로 날아가고, 가까워지면 그 자리 면으로 내려앉음.
        /// 목표는 매 프레임 엄호 자리로 갱신(그 자리가 위험하면 반대편). 가속 · 감속은 부드럽게, 기둥은 위로 넘어감(비행 중엔 부딪히지 않음).
        /// </summary>
        void TickFly(double dt, PlayerFsm p)
        {
            // 목표는 날아오를 때 정한 곳 그대로(빙빙 돌지 않게) — 그곳이 위험해지거나 화면 밖이 될 때만 다시 고름
            if (!SafeAt(flyTgt, flyTgtY) || flyTgt < ViewL || flyTgt > ViewR) BestPerch(p, side, out flyTgt, out flyTgtY);
            double tx = Clamp(flyTgt);
            double land = map.SupportBelow(tx, flyTgtY + 0.01, HALF);                // 목표 면(없어졌으면 그 아래 면)
            double cruise = Math.Max(Math.Max(land, p.Y), map.SupportBelow(X, Y + 0.01, HALF)) + Math.Max(0.5 * FightUnit.S, T.AllyFlyHeight);
            cruise = Math.Max(cruise, land + Math.Max(0.5 * FightUnit.S, T.AllyFlyHeight) * 0.6);   // 발판엔 위에서 내려앉음
            if (Engaged)
            {
                bool cross = Math.Sign(foe.X - X) == Math.Sign(tx - X) && Math.Abs(foe.X - X) < Math.Abs(tx - X) + Math.Max(0, T.AllyDangerDist);
                if (cross || Danger) cruise = Math.Max(cruise, foe.Y + foe.Height + 1.2 * FightUnit.S);   // 적 머리 위로 넘어감
            }
            foreach (var b in map.Solids)                                      // 지나가는 길의 기둥 위로
                if (Math.Min(X, tx) - HALF < b.X1 && Math.Max(X, tx) + HALF > b.X0) cruise = Math.Max(cruise, b.Y1 + 0.6 * FightUnit.S);
            double ax = Math.Abs(tx - X);
            bool arriving = ax < 0.6 * FightUnit.S && SafeAt(X, land);
            double ty = arriving ? land : cruise;
            double vmax = Math.Max(0.5 * FightUnit.S, T.AllyFlySpeed);
            double wvx = Math.Max(-vmax, Math.Min(vmax, (tx - X) * 4));
            double wvy = Math.Max(-vmax * 0.7, Math.Min(vmax * 0.7, (ty - Y) * 5));
            if (!arriving && Y < cruise - 0.5 * FightUnit.S && Engaged && Math.Abs(foe.X - X) < Math.Max(0, T.AllyDangerDist) * 1.5) wvx *= 0.35;   // 적 옆에선 먼저 위로
            double acc = Math.Max(1 * FightUnit.S, T.AllyFlyAccel) * dt;
            Vx += Math.Max(-acc, Math.Min(acc, wvx - Vx));
            Vy += Math.Max(-acc, Math.Min(acc, wvy - Vy));
            X = Clamp(X + Vx * dt);
            Y = Math.Max(0, Y + Vy * dt);
            if (Engaged) Face(foe.X > X ? 1 : -1);                             // 날면서도 적을 봄
            if ((arriving && Y <= land + 0.03 * FightUnit.S && Vy <= 0.5 * FightUnit.S) || StateT > 5)
            {
                Y = map.SupportBelow(X, Y + 0.05 * FightUnit.S, HALF);
                Vy = 0; Vx *= 0.3; Grounded = true;
                State = AllyState.Follow; StateT = 0;
                anchorX = tgtX = X; anchorY = Y; anchorSide = side; outRangeT = 0;
                stuckT = highT = 0;
                flyCd = Math.Max(0, T.AllyFlyCd);
            }
        }

        /// <summary>점프: 높이 h(U)에 닿을 만큼(최대 점프 높이까지).</summary>
        void Jump(double h)
        {
            double g = Math.Max(0.1, T.Gravity);
            h = Math.Max(0.3 * FightUnit.S, Math.Min(Math.Max(0.3 * FightUnit.S, T.AllyMaxJumpH), h));
            Vy = Math.Sqrt(2 * g * h);
            Grounded = false;
            jumpCd = 0.35;
        }

        bool OnSolid()
        {
            foreach (var b in map.Solids) if (X + HALF > b.X0 && X - HALF < b.X1 && Math.Abs(b.Y1 - Y) < 1e-3) return true;
            return Y <= 1e-4;          // 바닥도 뚫을 수 없음
        }

        /// <summary>중력 · 착지(기둥 · 발판 · 바닥), 위로 뛸 때 기둥 아래에 막힘.</summary>
        void Fall(double dt)
        {
            if (Grounded && Vy <= 0)
            {
                if (map.OnSupport(X, Y, HALF) && dropT <= 0) { Vy = 0; return; }
                Grounded = false;                                              // 발판 끝 · 기둥 끝 → 떨어짐
            }
            Vy = Math.Max(-Math.Max(0.1, T.MaxFall), Vy - Math.Max(0, T.Gravity) * dt);
            Y = map.MoveY(X, Y, Vy * dt, HALF, HEIGHT, dropT > 0, out bool landed, out bool bonk);
            if (bonk) Vy = 0;
            if (landed && Vy <= 0) { Vy = 0; Grounded = true; }
        }
    }
}
