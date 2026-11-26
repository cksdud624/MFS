using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori.Core.Rhythm
{
    /// <summary>
    /// 키(레인). 리듬은 S·D 로 잡고, 스페이스로 리듬 사이에 훅을 찔러 넣고, 엔터로 피니시.
    /// </summary>
    public static class Lane
    {
        public const int S = 0, D = 1, HOOK = 2, FINISH = 3, JUMP = 4, COUNT = 5;
        public static readonly string[] KEY = { "S", "D", "SPACE", "ENTER", "ALT" };
        public static readonly string[] NAME = { "S", "D", "훅", "피니시", "점프" };
    }

    /// <summary>노트 하나. T = 곡 시작부터의 초.</summary>
    public sealed class RNote
    {
        public double T;
        public int K;
        /// <summary>액션 모드: 적이 오는 방향(0 왼쪽 · 1 오른쪽 · 2 위 · 3 아래). −1 이면 자동으로 정한다.</summary>
        public int Dir = -1;
        public RNote() { }
        public RNote(double t, int k) { T = t; K = k; }
    }

    /// <summary>
    /// 노트 차트(JSON 으로 저장). 게이트(Gates)는 ‘여기서 멈출까, 계속할까’ 를 묻는 곳으로, 곡을 구간(Phase)으로 나눈다.
    /// 구간 i = [게이트 i−1, 게이트 i), 마지막 구간은 곡 끝(Length)까지. 각 구간 끝에는 보통 피니시(엔터) 노트를 둔다.
    /// Audio = "synth:test"(내장 합성 곡) 또는 음악 폴더의 파일 이름.
    /// </summary>
    public sealed class RhythmChart
    {
        public int V = 1;
        public string Title = "새 차트", Artist = "", Audio = "synth:test";
        public double Bpm = 128, OffsetMs, Length = 60;
        /// <summary>박자표: 한 마디의 박 수(2 · 3 · 4 · 6 — 6 은 6/8 처럼 셋씩 두 묶음). 예전 차트는 4.</summary>
        public int Meter = 4;
        public List<RNote> Notes = new List<RNote>();
        public List<double> Gates = new List<double>();

        public int PhaseCount => Gates.Count + 1;
        public double PhaseStart(int i) => i <= 0 ? 0 : Gates[Math.Min(i, Gates.Count) - 1];
        public double PhaseEnd(int i) => i < Gates.Count ? Gates[i] : Math.Max(Length, LastNoteT + 0.5);
        public double LastNoteT => Notes.Count == 0 ? 0 : Notes.Max(n => n.T);
        public double Beat => 60.0 / Math.Max(1, Bpm);
        /// <summary>한 마디의 박 수(이상한 값이면 4).</summary>
        public int BeatsPerBar => Meter >= 2 && Meter <= 7 ? Meter : 4;
        public double Bar => Beat * BeatsPerBar;

        public int PhaseOf(double t)
        {
            for (int i = 0; i < Gates.Count; i++) if (t < Gates[i]) return i;
            return Gates.Count;
        }

        public IEnumerable<RNote> InPhase(int i)
        {
            double a = PhaseStart(i), b = PhaseEnd(i);
            bool last = i >= Gates.Count;
            return Notes.Where(n => n.T >= a && (n.T < b || last));
        }

        /// <summary>시간순 정렬, 같은 레인·같은 시각 중복 제거, 게이트 정렬.</summary>
        public void Normalize()
        {
            Notes = Notes.Where(n => n != null && n.K >= 0 && n.K < Lane.COUNT && n.T >= 0)
                .GroupBy(n => (Math.Round(n.T * 1000), n.K)).Select(g => g.First())
                .OrderBy(n => n.T).ThenBy(n => n.K).ToList();
            Gates = Gates.Where(g => g > 0).Distinct().OrderBy(g => g).ToList();
            if (Length < LastNoteT) Length = LastNoteT + 1;
        }

        /// <summary>박자 격자에 맞춘 시각(snap = 한 박을 몇으로 나눌지).</summary>
        public double Snap(double t, int div)
        {
            if (div <= 0) return t;
            double step = Beat / div, off = OffsetMs / 1000.0;
            return Math.Max(0, Math.Round((t - off) / step) * step + off);
        }

        public RhythmChart Clone() => MoriJson.Deserialize<RhythmChart>(MoriJson.Serialize(this));
        public string ToJson() => MoriJson.Serialize(this, true);
        public static RhythmChart FromJson(string json)
        {
            var c = MoriJson.Deserialize<RhythmChart>(json);
            if (c == null) throw new FormatException("차트 파일을 읽을 수 없습니다");
            c.Normalize();
            return c;
        }
    }
}
