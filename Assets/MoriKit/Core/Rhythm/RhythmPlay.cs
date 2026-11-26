using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori.Core.Rhythm
{
    public enum Judge { None = 0, Perfect = 1, Great = 2, Good = 3, Miss = 4 }

    /// <summary>시장 이벤트가 정하는 리듬 난이도·힘. 리듬 도중 이벤트가 터지면 바뀐다(이후 판정부터 적용).</summary>
    public sealed class RhythmTuning
    {
        public int Level = 1;          // 1~5
        public double Gain = 1;        // 리듬이 가격을 미는 힘의 배수(이벤트)
        public string EventId;
        public void Set(int level, double gain, string eventId) { Level = Math.Max(1, Math.Min(5, level)); Gain = gain; EventId = eventId; }
        public double WindowMul => Level >= 5 ? 0.78 : Level == 4 ? 0.85 : Level == 3 ? 0.92 : 1;
        public double SpeedMul => 1 + 0.12 * (Math.Max(1, Level) - 1);
    }

    /// <summary>판정 하나의 결과.</summary>
    public sealed class Hit
    {
        public RNote Note;
        public int K;
        public Judge J;
        public double Delta;           // 누른 시각 − 노트 시각(초, +면 늦음)
        public double Impulse;         // 이 판정이 가격을 민 양(로그 수익률, + = 포지션에 유리)
        public double GaugeDelta;
        public bool Ghost;             // 노트 없이 누름(벌점 없음, 이펙트만)
        public bool FailedNow;         // 이 판정으로 방어 게이지가 0
    }

    /// <summary>
    /// 리듬 판정 · 방어 게이지 · 가격 영향(순수 규칙, 화면과 무관).
    ///  · 판정 폭: PERFECT ±45ms · GREAT ±85ms · GOOD ±130ms (이벤트 난이도가 높으면 좁아짐). 그보다 늦으면 MISS.
    ///  · 노트는 S · D · 훅(스페이스) 세 가지. 엔터(피니시)는 노트가 아니라 게이트에서 고른 것을 확정하는 키라 차트의 피니시 노트는 무시한다.
    ///  · 가격 영향: 구간을 완벽하게 치면 PHASE_TARGET 만큼 포지션 쪽으로 민다(이벤트 Gain 배 — 판정할 때의 Gain).
    ///    훅(스페이스)은 S·D 의 1.6배 무게. MISS 는 반대로 민다. 콤보가 길면 최대 +50%.
    ///  · 방어 게이지 100 에서 시작, MISS 마다 깎이고(구간이 올라갈수록 크게) PERFECT·GREAT 로 조금씩 회복. 0 이 되면 실패.
    ///  · 게이트에서 계속을 고른 뒤(2구간부터) 실패하면 붕괴: 그동안 민 이익을 모두 되돌리고 그 이상 반대로 민다.
    ///  · 임계치: 구간에서 민 가격이 그 구간 목표의 GATE_RATIO(50%) 이상이어야 게이트에서 ‘멈출지(엔터 피니시) / 계속할지’ 를 고를 수 있다.
    ///    못 미치면 고를 수 없고 다음 구간으로 넘어간다(마지막 구간이면 그대로 끝).
    /// </summary>
    public sealed class RhythmPlay
    {
        public const double W_PERFECT = 0.045, W_GREAT = 0.085, W_GOOD = 0.130;
        public const double GATE_RATIO = 0.5;
        public static readonly double[] PHASE_TARGET = { 0.010, 0.020, 0.035, 0.055, 0.080 };
        static readonly double[] FACTOR = { 0, 1.0, 0.7, 0.35, -0.9 };

        public readonly RhythmChart Chart;
        public readonly RhythmTuning Tune;
        public readonly List<RNote> Notes;
        readonly int[] phaseOf;
        readonly Judge[] judged;
        readonly double[] unit;

        public int Phase;
        public double Gauge = 100;
        public int Combo, MaxCombo;
        public readonly int[] Count = new int[5];
        public double Impulse, Favor, PhaseImpulse;
        public bool Failed;

        public RhythmPlay(RhythmChart chart, RhythmTuning tune = null)
        {
            Chart = chart;
            Tune = tune ?? new RhythmTuning();
            chart.Normalize();
            Notes = chart.Notes.Where(n => n.K == Lane.S || n.K == Lane.D || n.K == Lane.HOOK).ToList();   // 피니시 · 점프(액션 모드 전용)는 리듬 판정에서 뺀다
            phaseOf = Notes.Select(n => chart.PhaseOf(n.T)).ToArray();
            judged = new Judge[Notes.Count];
            unit = new double[chart.PhaseCount];
            for (int p = 0; p < chart.PhaseCount; p++)
            {
                double w = 0;
                for (int i = 0; i < Notes.Count; i++) if (phaseOf[i] == p) w += Notes[i].K == Lane.HOOK ? 1.6 : 1;
                unit[p] = PHASE_TARGET[Math.Min(p, PHASE_TARGET.Length - 1)] / Math.Max(1, w);   // Gain 은 판정할 때 곱한다
            }
        }

        public double WinPerfect => W_PERFECT * Tune.WindowMul;
        public double WinGreat => W_GREAT * Tune.WindowMul;
        public double WinGood => W_GOOD * Tune.WindowMul;
        public double PhaseTarget(int p) => PHASE_TARGET[Math.Min(p, PHASE_TARGET.Length - 1)] * Tune.Gain;
        public bool IsLastPhase => Phase >= Chart.PhaseCount - 1;
        /// <summary>이 구간의 임계치(구간에서 민 가격이 이만큼 되면 게이트에서 고를 수 있다).</summary>
        public double GateNeed => PhaseTarget(Phase) * GATE_RATIO;
        public bool GateOpen => PhaseImpulse >= GateNeed - 1e-12;
        /// <summary>임계치까지 얼마나 왔는지(0~1).</summary>
        public double GateProgress => GateNeed <= 0 ? 1 : Math.Max(0, Math.Min(1, PhaseImpulse / GateNeed));
        public Judge JudgedOf(int i) => judged[i];
        public int PhaseOfNote(int i) => phaseOf[i];
        public int JudgedCount => Count[1] + Count[2] + Count[3] + Count[4];

        /// <summary>키를 눌렀다(t = 곡 시각, 초). 이 구간에서 그 레인의 가장 가까운 노트를 판정한다.</summary>
        public Hit Press(int k, double t)
        {
            if (Failed) return null;
            int best = -1;
            double bd = double.MaxValue;
            for (int i = 0; i < Notes.Count; i++)
            {
                if (judged[i] != Judge.None || Notes[i].K != k || phaseOf[i] != Phase) continue;
                double d = t - Notes[i].T;
                if (d < -WinGood) break;                     // 시간순이라 이후는 더 멀다
                if (Math.Abs(d) <= WinGood && Math.Abs(d) < bd) { bd = Math.Abs(d); best = i; }
            }
            if (best < 0) return new Hit { K = k, Ghost = true };
            double delta = t - Notes[best].T;
            double a = Math.Abs(delta);
            var j = a <= WinPerfect ? Judge.Perfect : a <= WinGreat ? Judge.Great : Judge.Good;
            return Apply(best, j, delta);
        }

        /// <summary>매 프레임: 지나간 노트를 MISS 로.</summary>
        public List<Hit> Update(double t)
        {
            var res = new List<Hit>();
            if (Failed) return res;
            for (int i = 0; i < Notes.Count; i++)
            {
                if (judged[i] != Judge.None || phaseOf[i] != Phase) continue;
                if (t - Notes[i].T <= WinGood) break;
                res.Add(Apply(i, Judge.Miss, t - Notes[i].T));
                if (Failed) break;
            }
            return res;
        }

        Hit Apply(int i, Judge j, double delta)
        {
            var n = Notes[i];
            judged[i] = j;
            Count[(int)j]++;
            int p = phaseOf[i];
            var h = new Hit { Note = n, K = n.K, J = j, Delta = delta };
            if (j == Judge.Miss) Combo = 0; else { Combo++; MaxCombo = Math.Max(MaxCombo, Combo); }

            double imp = unit[p] * (n.K == Lane.HOOK ? 1.6 : 1) * FACTOR[(int)j] * Tune.Gain;
            if (imp > 0) imp *= 1 + Math.Min(Combo, 50) / 100.0;
            h.Impulse = imp;
            Impulse += imp;
            PhaseImpulse += imp;
            if (imp > 0) Favor += imp;

            double g;
            if (j == Judge.Perfect) g = 1.2;
            else if (j == Judge.Great) g = 0.6;
            else if (j == Judge.Good) g = -1.5;
            else g = -(6 + 2.5 * p) * (n.K == Lane.HOOK ? 1.3 : 1) * (Tune.Level >= 4 ? 1.25 : 1);
            double before = Gauge;
            Gauge = Math.Max(0, Math.Min(100, Gauge + g));
            h.GaugeDelta = Gauge - before;
            if (Gauge <= 0) { Failed = true; h.FailedNow = true; }
            return h;
        }

        /// <summary>이 구간 노트가 모두 판정됐고 마지막 노트가 지났는지.</summary>
        public bool PhaseDone(double t)
        {
            double last = -1;
            for (int i = 0; i < Notes.Count; i++)
            {
                if (phaseOf[i] != Phase) continue;
                if (judged[i] == Judge.None) return false;
                last = Math.Max(last, Notes[i].T);
            }
            double end = IsLastPhase ? Math.Max(last + 0.6, 0) : Math.Min(Chart.PhaseEnd(Phase) - 0.05, last + 0.9);
            return t >= Math.Max(end, last + 0.4);
        }

        /// <summary>게이트에서 계속. 다음 구간으로(게이지 조금 회복).</summary>
        public void Continue()
        {
            if (IsLastPhase) return;
            Phase++;
            PhaseImpulse = 0;
            Gauge = Math.Min(100, Gauge + 10);
        }

        /// <summary>계속한 뒤 실패했을 때 반대로 미는 양(음수). 1구간 실패는 붕괴 없음(이미 MISS 만큼은 반대로 밀렸다).</summary>
        public double MeltdownImpulse()
        {
            if (Phase <= 0) return 0;
            return -(Math.Max(0, Impulse) * (1.0 + 0.5 * Phase) + 0.006 * Phase * Tune.Gain);
        }

        /// <summary>정확도 0~1 (PERFECT 1 · GREAT 0.7 · GOOD 0.35 · MISS 0).</summary>
        public double Accuracy
        {
            get
            {
                int n = JudgedCount;
                return n == 0 ? 0 : (Count[1] + Count[2] * 0.7 + Count[3] * 0.35) / n;
            }
        }

        public string Grade => Failed ? "F" : Accuracy >= 0.95 ? "S" : Accuracy >= 0.88 ? "A" : Accuracy >= 0.75 ? "B" : Accuracy >= 0.6 ? "C" : "D";

        /// <summary>‘계속’ 했을 때의 위험 미리보기: 다음 구간 완벽 시 추가 영향, 실패 시 붕괴 영향(로그 수익률).</summary>
        public void Preview(out double nextBest, out double meltdown)
        {
            int p = Math.Min(Phase + 1, Chart.PhaseCount - 1);
            nextBest = PhaseTarget(p) * 1.25;
            meltdown = -(Math.Max(0, Impulse + nextBest * 0.5) * (1.0 + 0.5 * p) + 0.006 * p * Tune.Gain);
        }
    }
}
