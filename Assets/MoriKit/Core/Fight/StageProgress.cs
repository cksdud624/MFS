using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori.Core.Fight
{
    /// <summary>동작 키 이름(안내용). 색은 Client 의 StageLook.ACT 와 같은 순서.</summary>
    public static class Act
    {
        public const int ATK = 0, CTR = 1, DASH = 2, JUMP = 3, COUNT = 4;
        public static readonly string[] KEY = { "CTRL", "Z", "SPACE · SHIFT", "ALT" };
        public static readonly string[] NAME = { "공격", "카운터", "대쉬", "점프" };
    }

    /// <summary>슬롯 하나의 스테이지 기록(곡마다 최고 점수 · 등급 · 클리어).</summary>
    public sealed class StageProgress
    {
        public sealed class Rec { public string Chart; public long Best; public string Grade = ""; public bool Clear; public int Plays; public int MaxCombo; }
        public int V = 1;
        public List<Rec> Recs = new List<Rec>();

        public Rec Get(string chart) => Recs.FirstOrDefault(r => r.Chart == chart);

        /// <summary>한 판 기록. 새 최고 점수면 true.</summary>
        public bool Add(string chart, long score, string grade, bool clear, int maxCombo)
        {
            var r = Get(chart);
            if (r == null) { r = new Rec { Chart = chart }; Recs.Add(r); }
            r.Plays++;
            r.Clear |= clear;
            r.MaxCombo = Math.Max(r.MaxCombo, maxCombo);
            bool best = score > r.Best;
            if (best) { r.Best = score; r.Grade = grade; }
            return best;
        }
    }
}
