using System;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 코드로 만드는 소리(소리 파일 없이). 저작권 걱정 없는 테스트 곡과 리듬 키 효과음.
    ///  · TestSong: 128 BPM, 40마디(75초) — 인트로 2 · 구간1 12 · 구간2 12 · 구간3 12 · 아웃트로 2.
    ///    tools/gen_test_chart.py 가 만드는 테스트 차트(Resources/Rhythm/test.json)와 박자가 맞는다.
    ///  · 효과음: S(밝은 틱) · D(손뼉) · 훅(묵직한 펀치) · 피니시(거대한 충격음) · MISS · 카운트다운 · 게이트.
    /// </summary>
    public static class Synth
    {
        public const int SR = 44100;
        [ThreadStatic] static System.Random trnd;   // 테스트 곡은 백그라운드 스레드에서 만들므로 스레드마다 따로
        static float N() => (float)((trnd ?? (trnd = new System.Random(20261111))).NextDouble() * 2 - 1);

        public static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, Math.Max(1, data.Length), 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        static int S(double sec) => (int)(sec * SR);

        // ───────── 악기 ─────────
        static void Kick(float[] b, int at, float amp, double f0 = 150, double f1 = 45, double sweep = 0.11, double dec = 0.32)
        {
            double ph = 0;
            int len = S(dec * 3);
            for (int i = 0; i < len && at + i < b.Length; i++)
            {
                double t = i / (double)SR;
                double f = f1 + (f0 - f1) * Math.Exp(-t / (sweep / 3));
                ph += 2 * Math.PI * f / SR;
                float env = (float)Math.Exp(-t / dec);
                float click = i < 90 ? N() * (1 - i / 90f) * 0.5f : 0;
                b[at + i] += amp * (env * (float)Math.Sin(ph) + click);
            }
        }

        static void Snare(float[] b, int at, float amp, double dec = 0.14)
        {
            int len = S(dec * 4);
            double ph = 0;
            float lp = 0;
            for (int i = 0; i < len && at + i < b.Length; i++)
            {
                double t = i / (double)SR;
                ph += 2 * Math.PI * 190 / SR;
                float n = N();
                lp += (n - lp) * 0.55f;
                float env = (float)Math.Exp(-t / dec), tenv = (float)Math.Exp(-t / 0.05);
                b[at + i] += amp * (0.75f * lp * env + 0.45f * tenv * (float)Math.Sin(ph));
            }
        }

        static void Hat(float[] b, int at, float amp, double dec = 0.035)
        {
            int len = S(dec * 5);
            float prev = 0;
            for (int i = 0; i < len && at + i < b.Length; i++)
            {
                double t = i / (double)SR;
                float n = N(), hp = n - prev;
                prev = n;
                b[at + i] += amp * 0.5f * hp * (float)Math.Exp(-t / dec);
            }
        }

        static void Crash(float[] b, int at, float amp, double dec = 1.4)
        {
            int len = S(dec * 3);
            float prev = 0;
            for (int i = 0; i < len && at + i < b.Length; i++)
            {
                double t = i / (double)SR;
                float n = N(), hp = n - prev * 0.6f;
                prev = n;
                b[at + i] += amp * 0.4f * hp * (float)Math.Exp(-t / dec);
            }
        }

        /// <summary>톱니 비슷한 소리(배음 몇 개) + 한 극 저역 통과.</summary>
        static void Tone(float[] b, int at, double freq, double dur, float amp, int harm = 4, double att = 0.005, double rel = 0.08, float cut = 0.25f, double detune = 0)
        {
            int len = S(dur + rel);
            float lp = 0;
            double ph = 0, ph2 = 0;
            for (int i = 0; i < len && at + i < b.Length; i++)
            {
                double t = i / (double)SR;
                ph += 2 * Math.PI * freq / SR;
                ph2 += 2 * Math.PI * freq * (1 + detune) / SR;
                double v = 0;
                for (int h = 1; h <= harm; h++) v += Math.Sin(ph * h) / h + (detune != 0 ? Math.Sin(ph2 * h) / h : 0);
                if (detune != 0) v *= 0.5;
                float env = t < att ? (float)(t / att) : t < dur ? 1f : (float)Math.Exp(-(t - dur) / (rel / 3));
                lp += ((float)v - lp) * cut;
                b[at + i] += amp * env * lp;
            }
        }

        static void Pluck(float[] b, int at, double freq, float amp, double dec = 0.18)
        {
            int len = S(dec * 4);
            double ph = 0;
            for (int i = 0; i < len && at + i < b.Length; i++)
            {
                double t = i / (double)SR;
                ph += 2 * Math.PI * freq / SR;
                double sq = Math.Sin(ph) + Math.Sin(ph * 3) / 3 + Math.Sin(ph * 5) / 5;
                b[at + i] += amp * (float)(sq * Math.Exp(-t / dec));
            }
        }

        static void Riser(float[] b, int at, double dur, float amp)
        {
            int len = S(dur);
            float prev = 0;
            double ph = 0;
            for (int i = 0; i < len && at + i < b.Length; i++)
            {
                double t = i / (double)SR, k = t / dur;
                ph += 2 * Math.PI * (200 + 1800 * k * k) / SR;
                float n = N(), hp = n - prev;
                prev = n;
                b[at + i] += amp * (float)(k * k) * (0.5f * hp + 0.25f * (float)Math.Sin(ph));
            }
        }

        static double Hz(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

        static void Finish(float[] b, float gain)
        {
            float peak = 0;
            foreach (var v in b) peak = Math.Max(peak, Math.Abs(v));
            float k = peak > 0 ? gain / peak : 1;
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * k * 1.15) * 0.92f;
        }

        // ───────── 테스트 곡 ─────────
        public const double TEST_BPM = 128;
        static volatile float[] testSong;
        static System.Threading.Tasks.Task<float[]> warm;
        static readonly object gate = new object();

        /// <summary>게임을 켤 때 백그라운드에서 테스트 곡을 미리 만든다(약 0.5~1초).</summary>
        public static void WarmUp()
        {
            lock (gate) { if (warm == null && testSong == null) warm = System.Threading.Tasks.Task.Run(() => TestSong()); }
        }

        /// <summary>테스트 곡이 준비됐는지(안 됐으면 TestSong() 이 기다린다).</summary>
        public static bool TestSongReady => testSong != null;

        /// <summary>테스트 곡 PCM(처음 한 번 만들고 기억).</summary>
        public static float[] TestSong()
        {
            if (testSong != null) return testSong;
            System.Threading.Tasks.Task<float[]> w;
            lock (gate) w = warm;
            if (w != null && !w.IsCompleted && System.Threading.Tasks.Task.CurrentId != w.Id) return w.Result;
            lock (gate) { if (testSong != null) return testSong; }
            double beat = 60 / TEST_BPM, bar = beat * 4;
            var b = new float[S(40 * bar) + SR];
            // 진행: Am F C G (마디마다)
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } };
            int[] roots = { 45, 41, 48, 43 };
            for (int m = 0; m < 40; m++)
            {
                int sec = m < 2 ? 0 : m < 14 ? 1 : m < 26 ? 2 : m < 38 ? 3 : 4;   // 0 인트로 · 1~3 구간 · 4 아웃트로
                int bar0 = S(m * bar);
                int ci = m % 4;
                bool first = m == 2 || m == 14 || m == 26;
                if (first) Crash(b, bar0, 0.55f);
                for (int q = 0; q < 4; q++)
                {
                    int at = S(m * bar + q * beat);
                    if (sec == 0) { Kick(b, at, 0.8f); if (m == 1 && q == 0) Riser(b, bar0, bar, 0.35f); continue; }
                    if (sec == 4) { if (q == 0) Kick(b, at, 0.7f); continue; }
                    Kick(b, at, 0.95f);
                    if (sec >= 2 && (q == 1 || q == 3)) Snare(b, at, 0.55f);
                    if (sec == 1 && q == 3) Snare(b, at, 0.35f, 0.08);
                    // 하이햇: 1구간 8분 · 2구간 8분+ · 3구간 16분
                    int hs = sec == 3 ? 4 : 2;
                    for (int h = 0; h < hs; h++) Hat(b, at + S(h * beat / hs), h % 2 == 1 ? 0.22f : 0.14f, sec == 3 && h == 3 ? 0.07 : 0.035);
                    // 베이스: 1구간 4분 · 그 뒤 8분
                    double bf = Hz(roots[ci]);
                    if (sec == 1) Tone(b, at, bf, beat * 0.8, 0.28f, 5, 0.004, 0.05, 0.18f);
                    else { Tone(b, at, bf, beat * 0.4, 0.3f, 5, 0.004, 0.04, 0.2f); Tone(b, at + S(beat / 2), bf * (q == 3 ? 1.5 : 2), beat * 0.35, 0.22f, 5, 0.004, 0.04, 0.2f); }
                    // 아르페지오: 2구간 8분 · 3구간 16분
                    if (sec >= 2)
                    {
                        int steps = sec == 3 ? 4 : 2;
                        for (int a = 0; a < steps; a++)
                        {
                            int note = chords[ci][(q * steps + a) % 3] + 12 + (sec == 3 && a % 2 == 1 ? 12 : 0);
                            Pluck(b, at + S(a * beat / steps), Hz(note), sec == 3 ? 0.07f : 0.08f, sec == 3 ? 0.12 : 0.16);
                        }
                    }
                }
                // 패드(코드 깔기)
                if (sec >= 1 && sec <= 3)
                    foreach (var n in chords[ci]) Tone(b, bar0, Hz(n), bar * 0.95, 0.045f, 3, 0.08, 0.2, 0.08f, 0.004);
                if (sec == 4 && m == 38) foreach (var n in chords[0]) Tone(b, bar0, Hz(n), bar * 1.8, 0.06f, 3, 0.1, 1.0, 0.06f, 0.004);
                // 구간 끝(게이트 직전) 마디: 피니시 자리에 큰 크래시
                if (m == 13 || m == 25) Crash(b, S(m * bar + 2 * beat), 0.6f, 1.0);
                if (m == 37) Crash(b, bar0, 0.7f, 2.0);
            }
            Finish(b, 0.9f);
            lock (gate) testSong = b;
            return b;
        }

        // ───────── 효과음 ─────────
        static AudioClip sfxS, sfxD, sfxHook, sfxFinish, sfxMiss, sfxTick, sfxGo, sfxGate;

        public static AudioClip SfxS => sfxS ?? (sfxS = Make("sfx-s", 0.12, b =>
        {
            Hat(b, 0, 0.9f, 0.02);
            double ph = 0;
            for (int i = 0; i < S(0.06); i++) { ph += 2 * Math.PI * 2600 / SR; b[i] += 0.35f * (float)(Math.Sin(ph) * Math.Exp(-i / (double)S(0.015))); }
        }, 0.7f));

        public static AudioClip SfxD => sfxD ?? (sfxD = Make("sfx-d", 0.2, b =>
        {
            for (int k = 0; k < 3; k++) Snare(b, S(k * 0.009), 0.6f - k * 0.12f, 0.05);
            Tone(b, 0, 880, 0.02, 0.25f, 2, 0.001, 0.05, 0.6f);
        }, 0.72f));

        public static AudioClip SfxHook => sfxHook ?? (sfxHook = Make("sfx-hook", 0.45, b =>
        {
            Kick(b, 0, 1f, 190, 48, 0.07, 0.16);
            Tone(b, 0, 55, 0.18, 0.5f, 2, 0.002, 0.15, 0.4f);
            Hat(b, 0, 0.6f, 0.012);
        }, 0.85f));

        public static AudioClip SfxFinish => sfxFinish ?? (sfxFinish = Make("sfx-finish", 2.6, b =>
        {
            Kick(b, 0, 1f, 240, 28, 0.25, 0.7);
            Kick(b, S(0.012), 0.6f, 120, 35, 0.4, 0.9);
            Crash(b, 0, 1f, 1.1);
            foreach (var f in new[] { 523.0, 787.0, 1131.0, 1513.0, 2093.0 })
            {
                double ph = 0;
                for (int i = 0; i < b.Length; i++) { ph += 2 * Math.PI * f / SR; b[i] += 0.07f * (float)(Math.Sin(ph) * Math.Exp(-i / (double)S(0.9))); }
            }
            Tone(b, 0, 36.7, 0.9, 0.5f, 2, 0.002, 0.8, 0.3f);
        }, 0.98f));

        public static AudioClip SfxMiss => sfxMiss ?? (sfxMiss = Make("sfx-miss", 0.2, b =>
        {
            Kick(b, 0, 0.5f, 90, 50, 0.05, 0.06);
            float lp = 0;
            for (int i = 0; i < S(0.12); i++) { lp += (N() - lp) * 0.08f; b[i] += 0.5f * lp * (float)Math.Exp(-i / (double)S(0.04)); }
        }, 0.4f));

        public static AudioClip SfxTick => sfxTick ?? (sfxTick = Make("sfx-tick", 0.12, b => Tone(b, 0, 1000, 0.04, 0.6f, 1, 0.001, 0.05, 1f), 0.5f));
        public static AudioClip SfxGo => sfxGo ?? (sfxGo = Make("sfx-go", 0.3, b => { Tone(b, 0, 1500, 0.12, 0.6f, 2, 0.001, 0.1, 1f); Hat(b, 0, 0.5f, 0.05); }, 0.6f));
        public static AudioClip SfxGate => sfxGate ?? (sfxGate = Make("sfx-gate", 1.0, b => { Tone(b, 0, 659, 0.25, 0.4f, 3, 0.002, 0.3, 0.5f); Tone(b, S(0.18), 988, 0.4, 0.4f, 3, 0.002, 0.5, 0.5f); }, 0.6f));

        // ───────── 결투 드럼(플레이어 동작 = 드럼): 공격 스네어 · 기계 무기 하이햇 · 카운터 킥+크래시 · 대쉬 탐 · 점프 킥 ─────────
        static AudioClip drSnare, drHat, drKick, drCrash, drTom, drMute, atkMagic, atkMech;

        /// <summary>마법 공격(검기): 공기를 가르는 ‘슉’ + 은은한 울림 — 튀지 않게 고음은 낮추고 작게.</summary>
        public static AudioClip AtkMagic => atkMagic ?? (atkMagic = Make("atk-magic", 0.3, b =>
        {
            Snare(b, 0, 0.5f, 0.07);
            // 검기 바람: 부드럽게 걸러 낸 노이즈가 빠르게 차올랐다 사라짐
            float lp = 0;
            int n = S(0.09);
            for (int i = 0; i < n && i < b.Length; i++)
            {
                lp += (N() - lp) * 0.18f;
                double t = i / (double)n, env = Math.Sin(Math.PI * Math.Min(1, t * 1.6)) * (1 - t);
                b[i] += 0.55f * lp * (float)env;
            }
            Tone(b, 0, 784, 0.05, 0.16f, 2, 0.002, 0.12, 0.45f);      // 낮춘 울림(G5) — 박 느낌만
        }, 0.75f));

        /// <summary>기계 공격: 묵직하고 금속성인 ‘쿵철컥’ — 낮은 킥 + 거친 금속 톤 + 짧은 노이즈 두 번.</summary>
        public static AudioClip AtkMech => atkMech ?? (atkMech = Make("atk-mech", 0.35, b =>
        {
            Kick(b, 0, 0.9f, 210, 60, 0.05, 0.16);
            Tone(b, 0, 311, 0.04, 0.32f, 7, 0.001, 0.07, 0.12f);
            Hat(b, 0, 0.7f, 0.018);
            Hat(b, S(0.035), 0.55f, 0.022);
            Tone(b, S(0.035), 466, 0.03, 0.18f, 6, 0.001, 0.05, 0.12f);
        }, 0.85f));

        public static AudioClip DrumSnare => drSnare ?? (drSnare = Make("dr-snare", 0.3, b => { Snare(b, 0, 0.9f, 0.13); Tone(b, 0, 196, 0.05, 0.3f, 2, 0.001, 0.04, 0.5f); }, 0.8f));
        public static AudioClip DrumHat => drHat ?? (drHat = Make("dr-hat", 0.12, b => Hat(b, 0, 0.9f, 0.04), 0.6f));
        public static AudioClip DrumKick => drKick ?? (drKick = Make("dr-kick", 0.4, b => Kick(b, 0, 1f, 160, 45, 0.1, 0.3), 0.85f));
        public static AudioClip DrumCrash => drCrash ?? (drCrash = Make("dr-crash", 1.4, b => { Kick(b, 0, 1f, 180, 40, 0.1, 0.35); Crash(b, 0, 0.8f, 1.0); }, 0.85f));
        public static AudioClip DrumTom => drTom ?? (drTom = Make("dr-tom", 0.4, b => Kick(b, 0, 0.8f, 260, 110, 0.12, 0.25), 0.7f));
        /// <summary>엇박 동작: 짧고 먹먹한 ‘툭’(틀렸다는 느낌은 주되 거슬리지 않게).</summary>
        public static AudioClip DrumMute => drMute ?? (drMute = Make("dr-mute", 0.12, b =>
        {
            float lp = 0;
            for (int i = 0; i < S(0.08); i++) { lp += (N() - lp) * 0.05f; b[i] += 0.6f * lp * (float)Math.Exp(-i / (double)S(0.02)); }
        }, 0.35f));

        static AudioClip Make(string name, double sec, Action<float[]> f, float gain)
        {
            var b = new float[S(sec)];
            f(b);
            Finish(b, gain);
            return Clip(name, b);
        }
    }
}
