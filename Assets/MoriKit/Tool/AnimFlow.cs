using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mori.Core.Fight;
using UnityEngine;

namespace Mori.Client
{
    /// <summary>
    /// 연결 표: (이전 상태 → 다음 상태) = 연결 그림. 파일 = Data~/Fight/flow.txt, 한 줄에 ‘이전 &gt; 다음 = 그림’.
    ///  · * = 아무 상태(정확히 맞는 줄이 먼저)
    ///  · 그림이 아직 없으면 그 줄은 건너뜀(연결 없이 바로 다음 상태)
    /// 파일이 없으면 기본 표(착지 · 급정지 · 출발)를 씀.
    /// </summary>
    public static class FlowTable
    {
        public struct Row
        {
            public string From, To, Anim;
            /// <summary>이전 상태가 최소 이만큼(초) 이어졌을 때만 재생(0 = 언제나). 예: 멈춤 = 충분히 달렸을 때만.</summary>
            public float MinPrev;
        }

        static List<Row> rows;
        const string V2 = "# v2";
        static string Path0 => Path.Combine(ArtLib.Dir, "flow.txt");

        public static readonly Row[] DEFAULT =
        {
            new Row { From = "hero-fall", To = "hero-idle", Anim = "hero-land" },
            new Row { From = "hero-fall", To = "hero-run", Anim = "hero-land" },
            new Row { From = "hero-jump", To = "hero-idle", Anim = "hero-land" },
            new Row { From = "hero-jump", To = "hero-run", Anim = "hero-land" },
            new Row { From = "hero-air", To = "hero-idle", Anim = "hero-land" },
            new Row { From = "hero-air", To = "hero-run", Anim = "hero-land" },
            new Row { From = "hero-doublejump", To = "hero-idle", Anim = "hero-land" },
            new Row { From = "hero-doublejump", To = "hero-run", Anim = "hero-land" },
            new Row { From = "hero-run", To = "hero-idle", Anim = "hero-stop", MinPrev = 0.2f },
            new Row { From = "hero-idle", To = "hero-run", Anim = "hero-start" },
            new Row { From = "hero-ready", To = "hero-idle", Anim = "hero-ready-end" },
            new Row { From = "foe-air", To = "foe-idle", Anim = "foe-land" },
            new Row { From = "foe-air", To = "foe-walk", Anim = "foe-land" },
            new Row { From = "foe-walk", To = "foe-idle", Anim = "foe-stop", MinPrev = 0.2f },
            new Row { From = "foe-idle", To = "foe-walk", Anim = "foe-start" },
            new Row { From = "foe-ready", To = "foe-idle", Anim = "foe-ready-end" },
        };

        public static List<Row> Rows
        {
            get
            {
                if (rows != null) return rows;
                rows = new List<Row>();
                try
                {
                    if (!File.Exists(Path0)) { rows.AddRange(DEFAULT); return rows; }
                    var all = File.ReadAllLines(Path0);
                    bool v2 = all.Any(x => x.Trim() == V2);
                    foreach (var raw in all)
                    {
                        var l = raw.Trim();
                        if (l.Length == 0 || l.StartsWith("#")) continue;
                        int gt = l.IndexOf('>'), eq = l.IndexOf('=');
                        if (gt <= 0 || eq <= gt) continue;
                        string animPart = l.Substring(eq + 1);
                        int at = animPart.IndexOf('@');
                        var r = new Row { From = l.Substring(0, gt).Trim(), To = l.Substring(gt + 1, eq - gt - 1).Trim(), Anim = (at >= 0 ? animPart.Substring(0, at) : animPart).Trim() };
                        if (at >= 0) float.TryParse(animPart.Substring(at + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r.MinPrev);
                        else { foreach (var d in DEFAULT) if (d.From == r.From && d.To == r.To && d.Anim == r.Anim) r.MinPrev = d.MinPrev; }    // @ 없는 예전 파일 = 기본 표의 값
                        r.MinPrev = Mathf.Max(0, r.MinPrev);
                        if (r.From.Length > 0 && r.To.Length > 0 && r.Anim.Length > 0) rows.Add(r);
                    }
                    if (!v2)
                    {
                        // 예전 표: 공중(hero-air) · 더블 점프에서의 착지 줄을 한 번 보태 줌
                        foreach (var d in DEFAULT)
                            if ((d.From == "hero-air" || d.From == "hero-doublejump") && !rows.Any(x => x.From == d.From && x.To == d.To)) rows.Add(d);
                        Save();
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Mori 킷] 연결 표 읽기 실패: " + e.Message); rows.Clear(); rows.AddRange(DEFAULT); }
                return rows;
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(ArtLib.Dir);
                KitBackup.Before(Path0);
                var lines = new List<string> { "# 작업실 연결 표 — 이전 > 다음 = 연결 그림 @ 최소 이전 시간(초) (* = 아무 상태)", V2 };
                lines.AddRange(Rows.Select(r => $"{r.From} > {r.To} = {r.Anim} @ {r.MinPrev.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}"));
                File.WriteAllLines(Path0, lines);
            }
            catch (Exception e) { Debug.LogWarning("[Mori 킷] 연결 표 저장 실패: " + e.Message); }
        }

        public static void ResetDefault() { rows = new List<Row>(DEFAULT); Save(); }

        /// <summary>
        /// 그려진 연결 그림(없으면 null). 정확한 줄 → 이전만 맞음(다음 *) → 다음만 맞음(이전 *) → * &gt; *.
        /// 맞는 줄을 찾았는데 이전 상태가 그 줄의 최소 시간보다 짧았으면 연결 없음(다른 줄로 넘어가지 않음).
        /// </summary>
        public static string Find(string from, string to, float prevSec, Func<string, bool> has)
        {
            if (from == null || to == null || from == to) return null;
            var rs = Rows;
            Row? Pick(Func<Row, bool> m) { foreach (var r in rs) if (m(r) && SameWho(r.Anim, to) && has(r.Anim)) return r; return null; }
            var hit = Pick(r => r.From == from && r.To == to)
                ?? Pick(r => r.From == from && r.To == "*")
                ?? Pick(r => r.From == "*" && r.To == to)
                ?? Pick(r => r.From == "*" && r.To == "*");
            if (hit == null || prevSec < hit.Value.MinPrev) return null;
            return hit.Value.Anim;
        }

        /// <summary>플레이어 줄의 그림을 결투자에게 쓰지 않게(접두사 hero- / foe- 가 같아야).</summary>
        static bool SameWho(string anim, string to)
        {
            int a = anim.IndexOf('-'), b = to.IndexOf('-');
            return a > 0 && b > 0 && string.CompareOrdinal(anim, 0, to, 0, Math.Max(a, b)) == 0;
        }
    }

    /// <summary>
    /// 준비 그림별 재생 방식: 루프(시간 동안 되풀이) · 1회(한 번 재생). 파일 = Data~/Fight/ready.txt, 한 줄에 ‘그림 = loop|once @ 초’.
    ///  · 초 0 = 기본(루프 → 튜닝의 ‘준비 시간’, 1회 → 그림 길이). 1회인데 그림보다 길게 주면 마지막 프레임에서 그만큼 기다림.
    ///  · 표에 없는 그림: 이전 행동별 준비(-ready-attack1 · -ready-run …) = 1회, 그냥 준비(-ready) = 루프.
    /// </summary>
    public static class ReadyPlay
    {
        public struct Mode { public bool Loop; public float Sec; }
        static Dictionary<string, Mode> map;
        static string Path0 => Path.Combine(ArtLib.Dir, "ready.txt");

        /// <summary>준비 그림인지(연결 그림 -ready-end 는 아님).</summary>
        public static bool IsReady(string key) =>
            key != null && (key.EndsWith("-ready") || key.Contains("-ready-")) && !key.EndsWith("-ready-end") && (key.StartsWith("hero-") || key.StartsWith("foe-") || key.StartsWith("ally-"));

        public static Mode Default(string key) => new Mode { Loop = key == null || key.EndsWith("-ready"), Sec = 0 };

        static Dictionary<string, Mode> Map
        {
            get
            {
                if (map != null) return map;
                map = new Dictionary<string, Mode>();
                try
                {
                    if (!File.Exists(Path0)) return map;
                    foreach (var raw in File.ReadAllLines(Path0))
                    {
                        var l = raw.Trim();
                        if (l.Length == 0 || l.StartsWith("#")) continue;
                        int eq = l.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = l.Substring(0, eq).Trim(), v = l.Substring(eq + 1);
                        int at = v.IndexOf('@');
                        string m = (at >= 0 ? v.Substring(0, at) : v).Trim().ToLowerInvariant();
                        float sec = 0;
                        if (at >= 0) float.TryParse(v.Substring(at + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sec);
                        if (k.Length > 0) map[k] = new Mode { Loop = m != "once", Sec = Mathf.Max(0, sec) };
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Mori 킷] 준비 재생 표 읽기 실패: " + e.Message); map.Clear(); }
                return map;
            }
        }

        public static Mode Get(string key) => key != null && Map.TryGetValue(key, out var m) ? m : Default(key);

        public static void Set(string key, Mode m)
        {
            if (key == null) return;
            Map[key] = m;
            try
            {
                Directory.CreateDirectory(ArtLib.Dir);
                KitBackup.Before(Path0);
                var lines = new List<string> { "# 준비 그림별 재생 — 그림 = loop(루프) | once(1회) @ 초(0 = 기본)" };
                lines.AddRange(Map.OrderBy(x => x.Key).Select(x => $"{x.Key} = {(x.Value.Loop ? "loop" : "once")} @ {x.Value.Sec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}"));
                File.WriteAllLines(Path0, lines);
            }
            catch (Exception e) { Debug.LogWarning("[Mori 킷] 준비 재생 표 저장 실패: " + e.Message); }
        }
    }

    /// <summary>
    /// 캐릭터 하나의 흐름(그림만): 행동 → [연결] → [행동별 준비(여운, -ready-attack1 …)] → 준비(-ready) → [준비 → 대기 연결(-ready-end)] → 대기 → (간격마다) 긴 대기 → 대기.
    ///  · 준비 → 대기 연결 = 연결 표의 ‘(그 준비) &gt; &lt;누구&gt;-idle’ 줄 → ‘&lt;누구&gt;-ready &gt; &lt;누구&gt;-idle’ 줄 → &lt;누구&gt;-ready-end 그림(있으면)
    /// 판정 상태(base)가 바뀌면 언제나 즉시 끊고 새 상태로 — 준비 · 대기 · 긴 대기 · 연결 모두 조작을 막지 않는다.
    ///  · 연결 = 연결 표(FlowTable)
    ///  · 준비 = &lt;누구&gt;-ready-&lt;이전 행동&gt; → (숫자 뗀 이름) → &lt;누구&gt;-ready
    ///  · 대기 = &lt;누구&gt;-idle(기존) · 긴 대기 = &lt;누구&gt;-idle-long, -idle-long2 … -idle-long9
    /// </summary>
    public sealed class AnimFlow
    {
        enum Phase { None, Trans, Ready, ReadyOut, Wait, Long }

        readonly string who, idle;
        string prev, trans, ready, readyOut, longKey, lastAct;
        bool loopReady;
        /// <summary>지금 준비 그림을 루프로 보여 줘야 하는지(1회 그림이어도 준비 시간 동안 되풀이).</summary>
        public bool ReadyLoop => ph == Phase.Ready && loopReady;
        Phase ph = Phase.None;
        float t, stateMs;           // t = 지금 단계 시간 · stateMs = 지금 판정 상태가 이어진 시간
        bool readyPending;
        int longIdx = -1;
        readonly System.Random rng = new System.Random();

        public AnimFlow(string who) { this.who = who; idle = who + "-idle"; }

        /// <summary>지금 보여 줄 그림 이름(흐름이 꺼졌거나 할 게 없으면 base 그대로). has = 그 이름의 그림이 있는지.</summary>
        public string Update(string baseKey, float dtMs, FightTune T, Func<string, bool> has)
        {
            if (T == null || T.FlowOn < 0.5 || baseKey == null) { prev = baseKey; ph = Phase.None; readyPending = false; return baseKey; }
            if (baseKey != prev)
            {
                string from = prev;
                float prevSec = stateMs / 1000f;
                prev = baseKey;
                stateMs = 0;
                string next = FlowTable.Find(from, baseKey, prevSec, has);
                bool keep = next == null && baseKey == idle && ph == Phase.Trans && trans != null && t < Len(trans, 100);
                if (keep) { }      // 착지 직후 잠깐 달리다 멈춤 등: 새 연결이 없고 대기로 가는 중이면 하던 연결(착지)을 끝까지
                else
                {
                    t = 0;
                    trans = next;
                    ph = trans != null ? Phase.Trans : Phase.None;
                }
                readyPending = baseKey == idle && from != null;      // 처음 켤 때(이전 없음)는 준비 없이 바로 대기
                if (readyPending) lastAct = from;
            }
            else { t += Mathf.Max(0, dtMs); stateMs += Mathf.Max(0, dtMs); }

            if (ph == Phase.Trans)
            {
                if (t < Len(trans, 100)) return trans;
                ph = Phase.None; t = 0;
            }
            if (baseKey != idle) return baseKey;

            if (readyPending)
            {
                readyPending = false;
                ready = ReadyKey(has);
                ph = ready != null ? Phase.Ready : Phase.Wait;
                t = 0;
            }
            if (ph == Phase.None) { ph = Phase.Wait; t = 0; }

            if (ph == Phase.Ready)
            {
                // 준비 그림별: 루프(시간 동안 되풀이) · 1회(한 번, 초를 더 주면 마지막 프레임에서 기다림) — F2 › 캐릭터 흐름 › 준비 그림별 재생
                var rm = ReadyPlay.Get(ready);
                loopReady = rm.Loop;
                float dur = rm.Sec > 0 ? rm.Sec * 1000f
                    : rm.Loop ? (T.ReadySec > 0 ? (float)T.ReadySec * 1000f : Len(ready, 300))
                    : Len(ready, 300);
                if (t < dur) return ready;
                string gen = who + "-ready";
                // 이전 행동별 준비(여운, 예: -ready-attack1)가 끝나면 → 그냥 준비(-ready)로 이어서(루프 · 1회는 그 그림 설정대로) → 준비 끝(-ready-end) → 대기.
                // 단, 연결 표에 ‘그 여운 &gt; 대기’ 줄을 직접 적어 두었으면 그 줄을 따름(준비를 건너뜀).
                string own = null;
                foreach (var fr in FlowTable.Rows) if (fr.From == ready && fr.To == idle && has(fr.Anim)) { own = fr.Anim; break; }
                if (own == null && ready != gen && has(gen))
                {
                    ready = gen; t = 0;
                    var gm = ReadyPlay.Get(gen);
                    loopReady = gm.Loop;
                    return ready;
                }
                // 준비 → 대기: 연결 그림이 있으면 그걸 끝까지(입력이 오면 언제든 즉시 끊김)
                readyOut = own
                    ?? FlowTable.Find(ready, idle, dur / 1000f, has)
                    ?? (has(who + "-ready-end") ? who + "-ready-end" : null);
                t = 0;
                ph = readyOut != null ? Phase.ReadyOut : Phase.Wait;
            }
            if (ph == Phase.ReadyOut)
            {
                if (t < Len(readyOut, 200)) return readyOut;
                ph = Phase.Wait; t = 0;
            }
            if (ph == Phase.Wait)
            {
                if (t >= Mathf.Max(1f, (float)T.LongWaitSec) * 1000f)
                {
                    longKey = PickLong(T.LongWaitRandom >= 0.5, has);
                    t = 0;
                    if (longKey != null) ph = Phase.Long;
                }
                return idle;
            }
            if (ph == Phase.Long)
            {
                if (t < Len(longKey, 1000)) return longKey;
                ph = Phase.Wait; t = 0;
            }
            return idle;
        }

        /// <summary>끝까지 재생하는 모션(카운터 성공 · 크래시)이 끝남: 대기 중이면 그 모션 뒤의 준비부터 다시.</summary>
        public void AfterOverride(string act)
        {
            lastAct = act;
            readyPending = true;
            ph = Phase.None;
            t = 0;
        }

        /// <summary>지금 단계 이름(표시용).</summary>
        public string PhaseName => ph switch { Phase.Trans => "연결", Phase.Ready => "준비", Phase.ReadyOut => "준비 → 대기", Phase.Wait => "대기", Phase.Long => "긴 대기", _ => "행동" };

        static float Len(string key, float fallback)
        {
            var a = key != null ? ArtLib.Get(key) : null;
            return a != null && a.TotalMs > 0 ? a.TotalMs : fallback;
        }

        string ReadyKey(Func<string, bool> has)
        {
            string act = lastAct != null && lastAct.StartsWith(who + "-") ? lastAct.Substring(who.Length + 1) : null;
            if (act != null)
            {
                string k = $"{who}-ready-{act}";
                if (has(k)) return k;
                string bare = act.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (bare.Length > 0 && bare != act && has(k = $"{who}-ready-{bare}")) return k;
            }
            return has(who + "-ready") ? who + "-ready" : null;
        }

        string PickLong(bool random, Func<string, bool> has)
        {
            var list = new List<string>();
            for (int i = 1; i <= 9; i++) { string k = i == 1 ? $"{who}-idle-long" : $"{who}-idle-long{i}"; if (has(k)) list.Add(k); }
            if (list.Count == 0) return null;
            if (random && list.Count > 1)
            {
                if (longIdx < 0 || longIdx >= list.Count) longIdx = rng.Next(list.Count);
                else { int n = rng.Next(list.Count - 1); longIdx = n >= longIdx ? n + 1 : n; }     // 같은 걸 연달아 고르지 않게
            }
            else longIdx = (longIdx + 1) % list.Count;
            longIdx = Mathf.Clamp(longIdx, 0, list.Count - 1);
            return list[longIdx];
        }
    }
}
