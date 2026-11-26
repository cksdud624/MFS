using System;
using System.Collections.Generic;

namespace Mori.Core.Rhythm
{
    /// <summary>
    /// 자동 노트 생성 — ‘드러머처럼’. BPM · 박자표(한 마디 박 수) · 시작 보정으로 마디를 나누고:
    ///  ① 곡의 드럼을 따라 친다(저음 = 킥 → S, 고음 = 스네어·하이햇 → D): 8분음표 칸마다 킥·스네어가 실제로 울린 세기를 재서,
    ///     센 칸에만 노트를 놓는다(마디 단계만큼 개수 제한). 둘 다 크게 터지는 자리(크래시)와 프레이즈 첫 박은 스페이스(훅).
    ///  ② 드럼이 잘 안 들리는 마디는 정박 위주의 드럼 패턴(그루브)으로 채운다 — 내장 테스트 차트의 구간 1(4분 S·D 교대 + 마디 끝 훅) ·
    ///     구간 2(8분 그루브)를 본보기로. 4마디 프레이즈 = 그루브 세 마디 + 필인 한 마디.
    ///  · 마디 단계(쉼 · 4분 · 8분 · 절정)는 곡 전체 안에서의 세기 순위로, 두 마디 단위로만 바뀐다. 첫 구간은 4분음표 위주로 시작해 차츰 달아오른다.
    ///  · SNAP: 1/1 = 4분음표만 · 1/2 = 8분까지(기본) · 1/4 이상 = 필인·드럼이 아주 셀 때만 16분.
    ///  · 게이트 앞 마디는 마무리 패턴(…훅)으로 끝내고 게이트 뒤 한 마디는 비운다(테스트 차트처럼).
    ///  · 박이 맞으려면 BPM · 시작 보정이 곡과 맞아야 한다 — BeatDetect 로 찾을 수 있다.
    /// </summary>
    public static class ChartGen
    {
        public sealed class Options
        {
            /// <summary>SNAP(한 박을 몇 칸으로) — 1 = 4분음표만 · 2 = 8분 · 4 이상 = 16분 조금. 0 이하면 2.</summary>
            public int Div = 2;
            /// <summary>1 쉬움 · 2 보통 · 3 어려움.</summary>
            public int Level = 2;
            public int Seed = 1;
            /// <summary>음파 세기(0~1, EnvDt 초 간격). 없으면 구간이 갈수록 세지는 것으로 친다.</summary>
            public float[] Env;
            /// <summary>저음(킥 · 베이스) / 고음(스네어 · 하이햇) 세기 — 둘 다 있으면 곡의 드럼을 따라 친다.</summary>
            public float[] Low, High;
            public double EnvDt = 0.01;
            /// <summary>곡 길이(초). 0 이면 차트 길이.</summary>
            public double AudioLen;
            public bool MakeGates = true;
            /// <summary>드럼 따라 치기를 쓸지(false 면 패턴만).</summary>
            public bool Follow = true;
        }


        const int S = Lane.S, D = Lane.D, H = Lane.HOOK;

        // 패턴: (박 위치, 레인). 두 마디 한 쌍(A, B).
        static readonly (double, int)[][][] GROOVE =
        {
            // 0: 쉼(조용한 마디) — 반 박자 느낌
            new[] {
                new[] { (0.0, S), (2.0, D) },
                new[] { (0.0, S), (2.0, D), (3.0, D) } },
            // 1: 4분음표 — 테스트 차트 구간 1
            new[] {
                new[] { (0.0, S), (1.0, D), (2.0, S), (3.0, D) },
                new[] { (0.0, S), (1.0, D), (2.0, S), (3.0, D), (3.5, H) } },
            new[] {
                new[] { (0.0, S), (1.0, D), (2.0, S), (3.0, D) },
                new[] { (0.0, S), (1.0, D), (2.0, S), (2.5, S), (3.0, H) } },
            new[] {
                new[] { (0.0, S), (1.0, D), (2.0, S), (3.0, D) },
                new[] { (0.0, S), (1.0, D), (1.5, D), (2.0, S), (3.0, H) } },
            // 2: 8분 그루브 — 테스트 차트 구간 2
            new[] {
                new[] { (0.0, S), (0.5, D), (1.0, S), (1.5, D), (2.0, S), (2.5, D), (3.0, S), (3.5, H) },
                new[] { (0.0, S), (0.5, S), (1.0, D), (1.5, H), (2.0, S), (2.5, D), (3.0, D), (3.5, H) } },
            new[] {
                new[] { (0.0, S), (1.0, D), (1.5, S), (2.0, S), (3.0, D), (3.5, H) },
                new[] { (0.0, S), (0.5, S), (1.0, D), (2.0, S), (2.5, S), (3.0, D), (3.5, H) } },
            new[] {
                new[] { (0.0, S), (0.5, D), (1.0, S), (2.0, H), (2.5, D), (3.0, S), (3.5, D) },
                new[] { (0.0, S), (1.0, D), (1.5, S), (2.0, D), (3.0, H) } },
            // 3: 절정 — 8분 그루브에 훅을 백비트(2 · 4박)로
            new[] {
                new[] { (0.0, S), (0.5, D), (1.0, H), (1.5, D), (2.0, S), (2.5, D), (3.0, H), (3.5, D) },
                new[] { (0.0, S), (0.5, S), (1.0, H), (1.5, D), (2.0, S), (2.5, D), (3.0, H), (3.5, H) } },
            new[] {
                new[] { (0.0, S), (0.5, D), (1.0, H), (2.0, S), (2.5, S), (3.0, H), (3.5, D) },
                new[] { (0.0, S), (0.5, D), (1.0, H), (1.5, S), (2.0, D), (2.5, S), (3.0, H) } },
        };
        // 단계 → GROOVE 안의 변형 범위
        static readonly int[][] TIER = { new[] { 0 }, new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8 } };

        // 필인(프레이즈 마지막 마디)
        static readonly (double, int)[][] FILL =
        {
            new[] { (0.0, S), (2.0, D), (3.0, S), (3.5, D) },
            new[] { (0.0, S), (1.0, D), (2.0, S), (2.5, D), (3.0, S), (3.5, D) },
            new[] { (0.0, S), (0.5, D), (1.0, S), (1.5, D), (2.0, S), (2.5, D), (3.0, S), (3.5, D) },
            new[] { (0.0, S), (0.5, D), (1.0, H), (1.5, D), (2.0, S), (2.5, D), (3.0, S), (3.5, D) },
        };
        // 16분 필인 끝(SNAP 1/4 이상 · 보통 이상): 마지막 박을 S D S D
        static readonly (double, int)[] FILL16 = { (3.0, S), (3.25, D), (3.5, S), (3.75, D) };
        // 게이트 앞 마무리(테스트 차트 13 · 25마디처럼): …훅 뒤 비움
        static readonly (double, int)[][] ENDING =
        {
            new[] { (0.0, S), (2.0, H) },
            new[] { (0.0, S), (1.0, D), (1.5, D), (2.0, H) },
            new[] { (0.0, S), (0.5, D), (1.0, S), (1.5, D), (2.0, H) },
            new[] { (0.0, S), (0.5, D), (1.0, S), (1.5, D), (2.0, H) },
        };
        static readonly double[] THR = { 0.8, 0.55, 0.42, 0.35 };          // 단계별 ‘드럼이 이만큼 세면 친다’ 기준
        static readonly double[] MIN_GAP = { 0.22, 0.15, 0.11 };            // 난이도별 노트 사이 최소 간격(초)

        /// <summary>차트의 노트(와 게이트)를 새로 만든다. 만든 노트 수(곡이 너무 짧으면 0, 차트는 그대로).</summary>
        public static int Generate(RhythmChart c, Options o)
        {
            if (c == null || o == null) return 0;
            int lv = Math.Max(1, Math.Min(3, o.Level));
            int div = o.Div > 0 ? o.Div : 2;
            int m = c.BeatsPerBar;
            double beat = c.Beat, bar = beat * m, off = c.OffsetMs / 1000.0;
            double len = o.AudioLen > 0 ? Math.Min(c.Length, o.AudioLen) : c.Length;
            ulong rng = (ulong)(uint)o.Seed * 2654435761UL + 0x9E3779B97F4A7C15UL;
            Func<double> rnd = () =>
            {
                rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17;
                return (rng >> 11) * (1.0 / 9007199254740992.0);
            };
            var env = o.Env != null && o.Env.Length > 10 ? o.Env : null;
            double edt = o.EnvDt > 0 ? o.EnvDt : 0.01;

            // 소리가 있는 범위(앞뒤 정적은 건너뛴다)
            double songA = Math.Max(0.5, off), songB = len - 0.6;
            if (env != null)
            {
                double a = -1, b = -1;
                for (double t = 0; t < len; t += 0.1)
                    if (Avg(env, edt, t, t + 0.3) > 0.18) { if (a < 0) a = t; b = t + 0.3; }
                if (a >= 0) { songA = Math.Max(songA, a); songB = Math.Min(songB, b); }
            }
            // 시작 = 소리가 시작된 뒤 첫 마디 경계(소리가 마디 시작 직후 한 박 안에 나면 그 마디)
            double startT = off + Math.Floor((songA - off) / bar + 1e-6) * bar;
            if (songA - startT > beat * 1.01) startT += bar;
            while (startT < 0.5) startT += bar;
            int nBars = (int)Math.Floor((songB - startT) / bar + 1e-6);
            if (nBars < 3) return 0;
            double endT = startT + nBars * bar;

            // ── 게이트(4마디 경계) ──
            var gates = new List<double>();
            var gateBar = new HashSet<int>();
            if (o.MakeGates)
            {
                int nPh = Math.Max(2, Math.Min(5, (int)Math.Round((endT - startT) / 26.0)));
                if (nBars < 10) nPh = 1;
                int prev = 0;
                for (int i = 1; i < nPh; i++)
                {
                    double target = nBars * (double)i / nPh;
                    int g4 = (int)Math.Round(target / 4) * 4, g1 = (int)Math.Round(target);
                    int g = Math.Abs(g4 - target) <= 2 ? g4 : g1;
                    if (g - prev < 4 || nBars - g < 4) continue;
                    gates.Add(Math.Round(startT + g * bar, 4));
                    gateBar.Add(g);
                    prev = g;
                }
            }

            // ── 마디 세기 → 단계 ──
            var loud = new double[nBars];
            for (int i = 0; i < nBars; i++) loud[i] = env != null ? Avg(env, edt, startT + i * bar, startT + (i + 1) * bar) : 0;
            var rank = new double[nBars];
            if (env != null)
            {
                var sorted = (double[])loud.Clone();
                Array.Sort(sorted);
                for (int i = 0; i < nBars; i++)
                {
                    // 같은 세기끼리는 가운데 순위(세기가 고른 곡이 전부 ‘가장 조용함’ 이 되지 않게)
                    int lo = 0, hi = nBars - 1;
                    while (lo < nBars && sorted[lo] < loud[i]) lo++;
                    while (hi > 0 && sorted[hi] > loud[i]) hi--;
                    rank[i] = nBars > 1 ? (lo + hi) * 0.5 / (nBars - 1) : 0.5;
                }
            }
            int maxTier = lv == 1 ? 1 : lv == 2 ? 2 : 3;
            if (div == 1) maxTier = 1;
            var tier = new int[nBars];
            int nPhase = gates.Count + 1;
            for (int i = 0; i < nBars; i += 2)
            {
                int ph = PhaseOfBar(gateBar, i);
                double q = env != null ? Math.Max(rank[i], i + 1 < nBars ? rank[i + 1] : 0) : 0.35 + 0.5 * ph / Math.Max(1, nPhase - 1);
                bool silent = env != null && Math.Max(loud[i], i + 1 < nBars ? loud[i + 1] : 0) < 0.08;
                int t = silent ? 0 : q < 0.15 ? 0 : q < 0.5 ? 1 : q < 0.85 ? 2 : 3;
                if (ph == 0) t = Math.Min(t, q > 0.75 ? 2 : 1);          // 첫 구간은 4분음표 위주 — 차츰 달아오르게
                else if (t == 0 && !silent && q >= 0.08) t = 1;
                t = Math.Min(t, maxTier);
                tier[i] = t;
                if (i + 1 < nBars) tier[i + 1] = t;
            }

            // ── 드럼 따라 치기 준비: 킥(저음) · 스네어(고음) 온셋, 곡 전체 기준으로 크기 맞추기 ──
            bool follow = o.Follow && o.Low != null && o.High != null && o.Low.Length > 10 && o.High.Length > 10;
            double[] oL = null, oH = null, oB = null;
            double sL = 1, sH = 1, sB = 1;
            if (follow)
            {
                oL = BeatDetect.Onset(o.Low);
                oH = BeatDetect.Onset(o.High);
                oB = env != null ? BeatDetect.Onset(env) : null;
                var vl = new List<double>(); var vh = new List<double>(); var vb = new List<double>();
                for (double t = startT; t < endT; t += beat / 2)
                {
                    vl.Add(Slot(oL, edt, t)); vh.Add(Slot(oH, edt, t));
                    if (oB != null) vb.Add(Slot(oB, edt, t));
                }
                sL = Pct(vl, 0.85); sH = Pct(vh, 0.85); sB = oB != null ? Pct(vb, 0.97) : 1;
                if (sL < 0.015 && sH < 0.015) follow = false;     // 타악기가 거의 없는 곡 → 패턴으로
                sL = Math.Max(sL, 0.015); sH = Math.Max(sH, 0.015); sB = Math.Max(sB, 0.015);
            }
            double gapBeats = MIN_GAP[lv - 1] / beat;

            // ── 마디마다 ──
            var notes = new List<RNote>();
            int phrase = -1, variant = 0;
            bool mirror = false;
            for (int bi = 0; bi < nBars; bi++)
            {
                double b0 = startT + bi * bar;
                int t = tier[bi];
                if (bi / 4 != phrase)
                {
                    phrase = bi / 4;
                    var vs = TIER[t];
                    variant = vs[(int)(rnd() * vs.Length) % vs.Length];
                    mirror = rnd() < 0.25;
                }
                if (Array.IndexOf(TIER[t], variant) < 0) variant = TIER[t][(int)(rnd() * TIER[t].Length) % TIER[t].Length];
                if (gateBar.Contains(bi)) continue;                 // 게이트 바로 뒤 한 마디는 쉼
                bool beforeGate = gateBar.Contains(bi + 1), lastBar = bi == nBars - 1, phraseEnd = bi % 4 == 3;
                bool ending = beforeGate || lastBar;
                bool allow16 = div >= 4 && lv >= 2 && t >= 2;

                // 본보기 패턴
                var tmpl = Template(m, t, variant, bi % 2, ending, phraseEnd);
                if (phraseEnd && !ending && allow16 && m == 4)
                {
                    tmpl.RemoveAll(x => x.pos >= 3.0);
                    foreach (var x in FILL16) tmpl.Add(x);
                }
                if (mirror) for (int k = 0; k < tmpl.Count; k++) if (tmpl[k].lane != H) tmpl[k] = (tmpl[k].pos, tmpl[k].lane == S ? D : S);

                List<(double pos, int lane)> pat = tmpl;
                if (follow && !ending)
                {
                    var hit = Follow(m, t, lv, div, allow16, b0, beat, endT, edt, oL, oH, oB, sL, sH, sB, gapBeats);
                    int need = t == 0 ? 1 : t == 1 ? Math.Max(2, m / 2) : m;
                    if (hit.Count >= need) pat = hit;
                    else
                    {
                        // 드럼이 덜 들리는 마디: 들린 것 + 패턴으로 채우기
                        pat = new List<(double pos, int lane)>(hit);
                        foreach (var x in tmpl)
                        {
                            bool near = false;
                            foreach (var y in hit) if (Math.Abs(y.pos - x.pos) < gapBeats - 1e-6) { near = true; break; }
                            if (!near) pat.Add(x);
                        }
                    }
                }
                // 프레이즈 첫 박 = 훅(크래시) · 구간 첫 마디 첫 박도 훅
                bool entry = bi == 0 || gateBar.Contains(bi - 1);
                if ((bi % 4 == 0 && t >= 1) || entry)
                {
                    bool has0 = false;
                    for (int k = 0; k < pat.Count; k++) if (pat[k].pos == 0) { pat[k] = (0, H); has0 = true; }
                    if (!has0) pat.Add((0, H));
                }
                // 1/1: 4분음표만(엇박 훅은 그 앞 정박으로)
                if (div == 1)
                {
                    var q = new List<(double pos, int lane)>();
                    foreach (var x in pat)
                    {
                        if (Math.Abs(x.pos - Math.Round(x.pos)) < 1e-9) { if (!q.Exists(y => Math.Abs(y.pos - x.pos) < 1e-9 && y.lane == x.lane)) q.Add(x); continue; }
                        if (x.lane == H)
                        {
                            double p0 = Math.Floor(x.pos);
                            int idx = q.FindIndex(y => Math.Abs(y.pos - p0) < 1e-9);
                            if (idx >= 0) q[idx] = (p0, H); else q.Add((p0, H));
                        }
                    }
                    pat = q;
                }
                foreach (var (pos, lane) in pat)
                {
                    if (pos < 0 || pos >= m - 1e-9) continue;
                    double tt = b0 + pos * beat;
                    if (tt > endT - 0.05) continue;
                    if (env != null && pos != 0 && Max(env, edt, tt - 0.03, tt + 0.06) < 0.1) continue;   // 곡이 멎은 자리(브레이크)
                    notes.Add(new RNote(Math.Round(tt, 4), lane));
                }
            }
            // 어려움: 프레이즈 시작 훅에 S 를 겹쳐 친다(8마디마다)
            if (lv == 3)
                for (int bi = 0; bi < nBars; bi += 8)
                    if (tier[bi] >= 2 && !gateBar.Contains(bi)) notes.Add(new RNote(Math.Round(startT + bi * bar, 4), S));
            if (notes.Count == 0) return 0;
            notes.Sort((x, y) => x.T != y.T ? x.T.CompareTo(y.T) : x.K.CompareTo(y.K));
            // 같은 시각 같은 레인 겹침 제거
            for (int i = notes.Count - 1; i > 0; i--) if (Math.Abs(notes[i].T - notes[i - 1].T) < 1e-4 && notes[i].K == notes[i - 1].K) notes.RemoveAt(i);
            Hands(notes);
            c.Notes = notes;
            if (o.MakeGates) c.Gates = gates;
            c.Normalize();
            return c.Notes.Count;
        }

        /// <summary>
        /// 한 마디의 드럼 따라 치기: 8분(필요하면 16분) 칸마다 킥 · 스네어 세기를 재서 센 칸을 고른다.
        /// 킥이 세면 S, 스네어가 세면 D, 둘 다 크게 + 곡 전체가 확 커지면(크래시) 훅.
        /// </summary>
        static List<(double pos, int lane)> Follow(int m, int t, int lv, int div, bool allow16, double b0, double beat, double endT, double edt,
            double[] oL, double[] oH, double[] oB, double sL, double sH, double sB, double gapBeats)
        {
            var cand = new List<(double pos, double score, double k, double s, double crash)>();
            double step = div == 1 ? 1 : allow16 ? 0.25 : 0.5;
            for (double pos = 0; pos < m - 1e-9; pos += step)
            {
                double tt = b0 + pos * beat;
                if (tt > endT - 0.05) break;
                double k = Math.Min(1.5, Slot(oL, edt, tt) / sL), s = Math.Min(1.5, Slot(oH, edt, tt) / sH);
                double cr = oB != null ? Slot(oB, edt, tt) / sB : 0;
                bool sixteenth = Math.Abs(pos * 2 - Math.Round(pos * 2)) > 1e-9;
                if (sixteenth && Math.Max(k, s) < 1.0) continue;          // 16분은 드럼이 아주 셀 때만
                double bonus = pos == 0 ? 0.25 : Math.Abs(pos - Math.Round(pos)) < 1e-9 ? 0.12 : 0;
                cand.Add((pos, Math.Max(k, s) + bonus, k, s, cr));
            }
            int maxN = t == 0 ? 2 : t == 1 ? m : 2 * m;
            if (lv == 1) maxN = Math.Min(maxN, m);
            cand.Sort((x, y) => y.score.CompareTo(x.score));
            var picked = new List<(double pos, double k, double s, double crash)>();
            foreach (var x in cand)
            {
                if (picked.Count >= maxN) break;
                if (x.score < THR[t]) break;
                bool near = false;
                foreach (var y in picked) if (Math.Abs(y.pos - x.pos) < gapBeats - 1e-6) { near = true; break; }
                if (!near) picked.Add((x.pos, x.k, x.s, x.crash));
            }
            picked.Sort((x, y) => x.pos.CompareTo(y.pos));
            var res = new List<(double pos, int lane)>();
            int prevLane = D, hooks = 0;
            foreach (var p in picked)
            {
                bool onBeat = Math.Abs(p.pos - Math.Round(p.pos)) < 1e-9;
                int lane;
                if (onBeat && p.crash >= 1.0 && p.k >= 0.8 && p.s >= 0.8 && hooks < (t >= 3 ? 2 : 1)) { lane = H; hooks++; }
                else if (p.s > p.k * 1.15) lane = D;
                else if (p.k > p.s * 1.15) lane = S;
                else lane = onBeat ? (Strong(m, (int)Math.Round(p.pos)) ? S : D) : (prevLane == S ? D : S);
                res.Add((p.pos, lane));
                prevLane = lane;
            }
            return res;
        }

        /// <summary>손이 편하게: 짧은 간격으로 같은 레인이 세 번 이어지면 가운데를 반대 손으로.</summary>
        static void Hands(List<RNote> notes)
        {
            for (int i = 1; i + 1 < notes.Count; i++)
            {
                var a = notes[i - 1]; var b = notes[i]; var c = notes[i + 1];
                if (b.K == H || a.K != b.K || b.K != c.K) continue;
                if (b.T - a.T < 0.3 && c.T - b.T < 0.3 && b.T - a.T > 1e-4 && c.T - b.T > 1e-4) b.K = b.K == S ? D : S;
            }
        }

        static bool Strong(int m, int b) => b == 0 || (m == 4 && b == 2) || (m == 6 && b == 3);

        /// <summary>본보기 패턴: 4/4 는 표(테스트 차트 구간 1 · 2), 다른 박자는 킥(강박) · 스네어(약박) 규칙으로.</summary>
        static List<(double pos, int lane)> Template(int m, int t, int variant, int pairIdx, bool ending, bool fill)
        {
            var r = new List<(double pos, int lane)>();
            if (m == 4)
            {
                var src = ending ? ENDING[t] : fill ? FILL[t] : GROOVE[variant][pairIdx];
                foreach (var (p, l) in src) r.Add((p, l));
                return r;
            }
            int Ln(int b) => Strong(m, b) ? S : D;
            if (ending)
            {
                int hp = m >= 3 ? m - 2 : 1;
                for (int b = 0; b < hp; b++) r.Add((b, Ln(b)));
                r.Add((hp, H));
                return r;
            }
            if (t == 0)
            {
                for (int b = 0; b < m; b++) if (Strong(m, b)) r.Add((b, S));
                if (pairIdx == 1 && m >= 3) r.Add((m - 1, D));
            }
            else if (t == 1)
            {
                for (int b = 0; b < m; b++) r.Add((b, Ln(b)));
                if (pairIdx == 1 && !fill) r.Add((m - 0.5, H));
            }
            else
            {
                for (int b = 0; b < m; b++)
                {
                    int l = Ln(b);
                    r.Add((b, t >= 3 && !Strong(m, b) && b % 2 == 1 ? H : l));
                    if (pairIdx == 1 || fill || m <= 3) r.Add((b + 0.5, l == S ? D : S));
                }
                if (pairIdx == 1 && !fill) { r.RemoveAll(x => x.pos == m - 0.5); r.Add((m - 0.5, H)); }
            }
            if (fill && t >= 1)
            {
                // 필인: 마지막 한 박(3박 이하) · 두 박을 8분으로 S D S D
                int from = m <= 3 ? m - 1 : m - 2;
                r.RemoveAll(x => x.pos >= from);
                for (double p = from; p < m - 1e-9; p += 0.5) r.Add((p, ((int)Math.Round((p - from) * 2)) % 2 == 0 ? S : D));
            }
            return r;
        }

        static int PhaseOfBar(HashSet<int> gateBars, int bi)
        {
            int p = 0;
            foreach (var g in gateBars) if (bi >= g) p++;
            return p;
        }

        /// <summary>그 시각 근처(−40 ~ +50 ms) 온셋의 최댓값(사람·곡의 작은 어긋남을 감안).</summary>
        static double Slot(double[] on, double dt, double t)
        {
            int i0 = Math.Max(0, (int)((t - 0.04) / dt)), i1 = Math.Min(on.Length - 1, (int)((t + 0.05) / dt));
            double mx = 0;
            for (int i = i0; i <= i1; i++) if (on[i] > mx) mx = on[i];
            return mx;
        }

        static double Pct(List<double> v, double q)
        {
            if (v.Count == 0) return 0;
            v.Sort();
            return v[Math.Min(v.Count - 1, (int)(v.Count * q))];
        }

        internal static double Avg(float[] env, double dt, double a, double b)
        {
            int i0 = Math.Max(0, (int)(a / dt)), i1 = Math.Min(env.Length - 1, (int)(b / dt));
            if (i1 < i0) return 0;
            double s = 0;
            for (int i = i0; i <= i1; i++) s += env[i];
            return s / (i1 - i0 + 1);
        }

        internal static double Max(float[] env, double dt, double a, double b)
        {
            int i0 = Math.Max(0, (int)(a / dt)), i1 = Math.Min(env.Length - 1, (int)(b / dt));
            double m = 0;
            for (int i = i0; i <= i1; i++) if (env[i] > m) m = env[i];
            return m;
        }
    }

    /// <summary>
    /// 곡의 BPM · 첫 박(시작 보정) 찾기: 소리가 갑자기 커지는 순간(온셋)들이 가장 규칙적으로 맞아떨어지는 박 간격과 위치를 찾는다.
    /// 70~180 BPM 범위로 맞추고(두 배·절반 혼동은 120 근처를 조금 선호), 마디의 박 중 가장 센 박(저음이 있으면 킥)을 마디 첫 박으로.
    /// </summary>
    public static class BeatDetect
    {
        /// <summary>찾았으면 true. bpm 은 소수 한 자리, offsetSec 은 첫 마디 첫 박(0 ≤ x &lt; 한 마디).</summary>
        /// <param name="meter">한 마디 박 수(첫 박 찾기에 씀).</param>
        /// <param name="low">저음(킥) 세기 — 있으면 마디 첫 박을 킥이 가장 센 박으로.</param>
        public static bool Estimate(float[] env, double dt, out double bpm, out double offsetSec, out double confidence, int meter = 4, float[] low = null)
        {
            bpm = 120; offsetSec = 0; confidence = 0;
            if (env == null || env.Length < (int)(8 / dt)) return false;
            int n = env.Length;
            // 온셋: 지금 세기 − 바로 앞 평균(양수만)
            var on = new double[n];
            double mean = 0;
            for (int i = 4; i < n; i++)
            {
                double prev = (env[i - 1] + env[i - 2] + env[i - 3] + env[i - 4]) * 0.25;
                on[i] = Math.Max(0, env[i] - prev);
                mean += on[i];
            }
            mean /= n;
            for (int i = 0; i < n; i++) on[i] = Math.Max(0, on[i] - mean);
            // 0 이 아닌 온셋만 모아 계산(빠르게)
            var idx = new List<int>();
            var val = new List<double>();
            for (int i = 0; i < n; i++) if (on[i] > 0) { idx.Add(i); val.Add(on[i]); }
            if (idx.Count < 16) return false;
            var I = idx.ToArray();
            var V = val.ToArray();
            double Score(double b, out double ph)
            {
                double s = Strength(I, V, dt, b, out ph) + 0.5 * Strength(I, V, dt, b * 2, out _) + 0.3 * Strength(I, V, dt, b / 2, out _);
                double prior = Math.Exp(-Math.Pow(Math.Log(b / 120.0, 2), 2) / (2 * 0.5 * 0.5));
                return s * (0.75 + 0.25 * prior);
            }
            // 거칠게(0.5 BPM) → 가장 좋은 몇 곳을 곱게(0.05 BPM)
            var coarse = new List<(double b, double s)>();
            for (double b = 70; b <= 180.001; b += 0.5) coarse.Add((b, Score(b, out _)));
            coarse.Sort((x, y) => y.s.CompareTo(x.s));
            double best = -1, bestB = 120, bestPh = 0;
            for (int c = 0; c < Math.Min(4, coarse.Count); c++)
                for (double b = coarse[c].b - 0.5; b <= coarse[c].b + 0.5001; b += 0.05)
                {
                    double sc = Score(b, out double ph);
                    if (sc > best) { best = sc; bestB = b; bestPh = ph; }
                }
            double second = 0;
            foreach (var x in coarse) if (Math.Abs(x.b - bestB) > 3 && Math.Abs(x.b - bestB * 2) > 3 && Math.Abs(x.b - bestB / 2) > 3) { second = x.s; break; }
            if (best <= 0) return false;
            double period = 60.0 / bestB;
            double first = bestPh;
            // 마디의 박 중 가장 센 박(저음이 있으면 킥) = 마디 첫 박
            int m = meter >= 2 && meter <= 7 ? meter : 4;
            var acc = on;
            if (low != null && low.Length == n) acc = Onset(low);
            int bestK = 0;
            double bk = -1;
            for (int k = 0; k < m; k++)
            {
                double sum = 0;
                for (double t = first + k * period; t < n * dt; t += period * m) sum += Peak(acc, dt, t);
                if (sum > bk) { bk = sum; bestK = k; }
            }
            double bar = period * m;
            offsetSec = (first + bestK * period) % bar;
            if (offsetSec < 0) offsetSec += bar;
            bpm = Math.Round(bestB, 1);
            confidence = second > 0 ? Math.Min(1, (best / second - 1) * 2) : 1;
            return true;
        }

        /// <summary>그 BPM 의 박에 온셋이 얼마나 몰리는지(복소 합의 크기)와 그때의 첫 박 위치(초).</summary>
        static double Strength(int[] idx, double[] val, double dt, double bpm, out double phaseSec)
        {
            double period = 60.0 / bpm, w = 2 * Math.PI / period, re = 0, im = 0, tot = 0;
            for (int j = 0; j < idx.Length; j++)
            {
                double v = val[j];
                double a = w * idx[j] * dt;
                re += v * Math.Cos(a);
                im += v * Math.Sin(a);
                tot += v;
            }
            double ang = Math.Atan2(im, re);
            if (ang < 0) ang += 2 * Math.PI;
            phaseSec = ang / w;
            return tot > 0 ? Math.Sqrt(re * re + im * im) / tot : 0;
        }

        /// <summary>온셋(지금 세기 − 바로 앞 40ms 평균, 양수만).</summary>
        public static double[] Onset(float[] x)
        {
            var o = new double[x.Length];
            for (int i = 4; i < x.Length; i++)
                o[i] = Math.Max(0, x[i] - (x[i - 1] + x[i - 2] + x[i - 3] + x[i - 4]) * 0.25);
            return o;
        }

        internal static double Peak(double[] on, double dt, double t)
        {
            int i0 = Math.Max(0, (int)((t - 0.03) / dt)), i1 = Math.Min(on.Length - 1, (int)((t + 0.03) / dt));
            double m = 0;
            for (int i = i0; i <= i1; i++) if (on[i] > m) m = on[i];
            return m;
        }
    }
}
