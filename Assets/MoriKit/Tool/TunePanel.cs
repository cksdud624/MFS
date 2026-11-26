using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mori.Core;
using Mori.Core.Fight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>튜닝 항목 하나: 전투 수치(FightTune) 또는 결투자 패턴의 숫자(거리 · 비중 · 타격별 박 · 사거리 · 돌진 · 데미지 · 높이 · 속도).</summary>
    public sealed class TuneItem
    {
        /// <summary>Group = ‘대분류/탭’(탭 하나), Section = 탭 안의 묶음(테두리 상자).</summary>
        public string Key, Group, Cat, Tab, Section, Name, Spec;
        public double Default, Min, Max;
        public bool NextRound, IsPattern;
        public Func<double> Get;
        public Action<double> Set;
    }

    /// <summary>
    /// 전투 수치 보관: 게임 전체가 같은 FightTune 하나와 같은 패턴 목록을 쓴다(결투 중에 바꾸면 바로 적용).
    /// 값의 한계는 없다(직접 입력). 슬라이더 최소 · 최대는 드래그 구간일 뿐이고 항목마다 입력 · 저장한다.
    /// 저장 = persistentDataPath 의 fight-tune.json(수치) · fight-patterns.json(패턴) · fight-tune-ranges.json(슬라이더 구간).
    /// 내보내기 = 바뀐 값 + 전체 값 JSON 을 클립보드와 fight-tune-export.json 에.
    /// </summary>
    public static class TuneStore
    {
        static FightTune cur;
        static List<TuneItem> items;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        public static readonly FightTune Defaults = new FightTune();
        static string Dir => KitPaths.Data;
        public static string SavePath => Path.Combine(Dir, "fight-tune.json");
        public static string PatternPath => Path.Combine(Dir, "fight-patterns.json");
        public static string RangePath => Path.Combine(Dir, "fight-tune-ranges.json");
        public static string ExportPath => Path.Combine(Dir, "fight-tune-export.json");

        public static FightTune Current { get { if (cur == null) Load(); return cur; } }

        /// <summary>FightTune 의 튜닝 필드.</summary>
        public static IEnumerable<(FieldInfo f, TuneAttribute a)> Fields =>
            typeof(FightTune).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(double))
                .Select(f => (f, (TuneAttribute)Attribute.GetCustomAttribute(f, typeof(TuneAttribute))))
                .Where(x => x.Item2 != null);

        /// <summary>모든 튜닝 항목(전투 수치 → 패턴 순).</summary>
        public static List<TuneItem> Items { get { if (items == null) Build(); return items; } }

        static void Build()
        {
            if (cur == null) { Load(); return; }        // Load 가 수치를 읽은 뒤 Build 를 부른다
            items = new List<TuneItem>();
            foreach (var (f, a) in Fields)
            {
                var ff = f;
                var path = (a.Group ?? "").Split('/');
                string c0 = path.Length > 0 ? path[0] : "기타", t0 = path.Length > 1 ? path[1] : c0, s0 = path.Length > 2 ? path[2] : t0;
                items.Add(new TuneItem
                {
                    Key = f.Name, Cat = c0, Tab = t0, Group = c0 + "/" + t0, Section = s0, Name = a.Name, Spec = a.Spec, NextRound = a.NextRound,
                    Default = (double)f.GetValue(Defaults), Min = a.Min, Max = a.Max,
                    Get = () => (double)ff.GetValue(cur), Set = v => ff.SetValue(cur, v),
                });
            }
            // 결투장 지형(기둥 · 발판 — 다음 판부터)
            for (int i = 0; i < Arena.Layout.Count; i++)
            {
                var pc = Arena.Layout[i];
                string g = ARENA, sec = "지형 · " + pc.Name, k = "arena." + i + ".";
                AddPat(g, sec, k + "Cx", "가운데 위치(X)", "0 = 결투장 가운데", () => pc.Cx, v => pc.Cx = v, pc.Cx - 8 * FightUnit.S, pc.Cx + 8 * FightUnit.S, true);
                AddPat(g, sec, k + "Width", "폭(U)", "0 이하면 없앰", () => pc.Width, v => pc.Width = v, null, null, true);
                AddPat(g, sec, k + "Top", pc.Platform ? "윗면 높이(U)" : "높이(U)", "", () => pc.Top, v => pc.Top = v, null, null, true);
                if (pc.Platform) AddPat(g, sec, k + "Thick", "두께(U)", "", () => pc.Thick, v => pc.Thick = v, 0, 1 * FightUnit.S, true);
            }
            // 결투자 패턴(기본 세트 + 추격 · 빈틈 찌르기 · 밀쳐내기): 지금 값을 기본값으로 잡는다(불러온 저장값보다 먼저)
            foreach (var p in Pattern.BASIC.Concat(new[] { Pattern.PUNISH, Pattern.JAB, Pattern.SHOVE }))
            {
                var pp = p;
                string g = PAT_CAT + "/" + p.Name, k = "pat." + p.Id + ".";
                string sec = "등장 · 거리";
                AddPat(g, sec, k + "Weight", "고르는 비중(배)", "클수록 자주", () => pp.Weight, v => pp.Weight = v);
                AddPat(g, sec, k + "MinPhase", "나오기 시작하는 단계", "0 = 1단계 · 1 = 2단계 · 2 = 3단계(격노)", () => pp.MinPhase, v => pp.MinPhase = (int)Math.Round(v), 0, 2);
                string far = pp.Far ? "그대로(U)" : "× 공격 사거리 배율";
                AddPat(g, sec, k + "MinD", "최소 거리", far, () => pp.MinD, v => pp.MinD = v);
                AddPat(g, sec, k + "MaxD", "최대 거리", pp.Far ? far : far + " + 근접 최대 거리 여유", () => pp.MaxD, v => pp.MaxD = v);
                AddPat(g, sec, k + "Pref", "다가가는 거리", far, () => pp.Pref, v => pp.Pref = v);

                // 패턴별 덮어쓰기(−1 = 공통값) — 공통값이 어디 있는지 같이 적는다
                bool melee = p.Steps.Any(x => x.Kind == HitKind.Melee || x.Kind == HitKind.Low);
                bool shot = p.Steps.Any(x => x.Kind == HitKind.Projectile);
                sec = "예고 보여 주기 · 이 패턴만";
                Ovr(g, sec, k, "Telegraph", "색 변화 · 줄어드는 원 길이(박)", nameof(FightTune.TelegraphBeats), () => pp.Telegraph, v => pp.Telegraph = v);
                Ovr(g, sec, k, "ZoneShow", "위험 구역 보이기 시작(박)", nameof(FightTune.ZoneShowBeats), () => pp.ZoneShow, v => pp.ZoneShow = v);
                Ovr(g, sec, k, "ZoneHot", "위험 구역 진해지는 시점(박)", nameof(FightTune.ZoneHotBeats), () => pp.ZoneHot, v => pp.ZoneHot = v);
                Ovr(g, sec, k, "CueLead", "예고음 시점(박)", nameof(FightTune.CueLeadBeats), () => pp.CueLead, v => pp.CueLead = v);
                sec = "접근 · 끝 · 이 패턴만";
                string apr = p.Counter ? nameof(FightTune.CounterApproachMul) : p == Pattern.PUNISH ? nameof(FightTune.EnemyPunishChase) : nameof(FightTune.ApproachMul);
                Ovr(g, sec, k, "ApproachMul", "접근 속도 배율(배)", apr, () => pp.ApproachMul, v => pp.ApproachMul = v);
                Ovr(g, sec, k, "ApproachStop", "다가가기 멈춤(박)", nameof(FightTune.ApproachStopBeats), () => pp.ApproachStop, v => pp.ApproachStop = v);
                Ovr(g, sec, k, "EndPad", "패턴 끝 여유(박)", nameof(FightTune.PatternEndPad), () => pp.EndPad, v => pp.EndPad = v);
                sec = "판정 · 이 패턴만";
                Ovr(g, sec, k, "ReachMul", "사거리 배율(배)", nameof(FightTune.EnemyReachMul), () => pp.ReachMul, v => pp.ReachMul = v);
                if (melee)
                {
                    Ovr(g, sec, k, "LungeMul", "돌진 배율(배)", nameof(FightTune.EnemyLungeMul), () => pp.LungeMul, v => pp.LungeMul = v);
                    Ovr(g, sec, k, "MeleeActive", "근접 판정 유지(초)", nameof(FightTune.MeleeActive), () => pp.MeleeActive, v => pp.MeleeActive = v);
                    Ovr(g, sec, k, "BoxBottom", "근접 판정 아래 끝(U)", nameof(FightTune.MeleeBoxBottom), () => pp.BoxBottom, v => pp.BoxBottom = v);
                }
                if (p.Charge) Ovr(g, sec, k, "ChargeHalf", "돌진 판정 폭(± U)", nameof(FightTune.ChargeHalf), () => pp.ChargeHalf, v => pp.ChargeHalf = v);
                if (p.Leap)
                {
                    sec = "내려찍기 · 이 패턴만";
                    Ovr(g, sec, k, "LeapHeight", "점프 높이(U)", nameof(FightTune.LeapHeight), () => pp.LeapHeight, v => pp.LeapHeight = v);
                    Ovr(g, sec, k, "LeapBeats", "체공(박)", nameof(FightTune.LeapBeats), () => pp.LeapBeats, v => pp.LeapBeats = v);
                }
                if (shot)
                {
                    sec = "에너지탄 · 이 패턴만";
                    Ovr(g, sec, k, "ShotRadius", "판정 반지름(U)", nameof(FightTune.ShotRadius), () => pp.ShotRadius, v => pp.ShotRadius = v);
                    Ovr(g, sec, k, "ShotLife", "수명(초)", nameof(FightTune.ShotLife), () => pp.ShotLife, v => pp.ShotLife = v);
                    Ovr(g, sec, k, "ShotAimY", "조준 높이(U)", nameof(FightTune.ShotAimY), () => pp.ShotAimY, v => pp.ShotAimY = v);
                    Ovr(g, sec, k, "ShotOriginX", "나가는 앞쪽 거리(U)", nameof(FightTune.ShotOriginX), () => pp.ShotOriginX, v => pp.ShotOriginX = v);
                    Ovr(g, sec, k, "ShotOriginY", "나가는 높이(U)", nameof(FightTune.ShotOriginY), () => pp.ShotOriginY, v => pp.ShotOriginY = v);
                }

                for (int i = 0; i < p.Steps.Count; i++)
                {
                    var s = p.Steps[i];
                    string sk = k + "s" + i + ".";
                    string kind = KindName(s.Kind);
                    sec = $"{i + 1}타 · {kind}";
                    AddPat(g, sec, sk + "Beat", "타격 박(박)", "패턴 시작부터 몇 박 뒤", () => s.Beat, v => s.Beat = v);
                    AddPat(g, sec, sk + "Damage", "데미지", "× 결투자 데미지 배율", () => s.Damage, v => s.Damage = v);
                    if (s.Kind != HitKind.Projectile)
                    {
                        AddPat(g, sec, sk + "Reach", s.Kind == HitKind.Charge ? "돌진 거리(U)" : "사거리", s.Kind == HitKind.Charge ? "그대로(U)" : "× 사거리 배율", () => s.Reach, v => s.Reach = v);
                        AddPat(g, sec, sk + "Height", "판정 위쪽 끝(U)", "", () => s.Height, v => s.Height = v);
                    }
                    if (s.Kind == HitKind.Melee || s.Kind == HitKind.Low) AddPat(g, sec, sk + "Lunge", "칠 때 앞으로(U)", "× 돌진 배율", () => s.Lunge, v => s.Lunge = v);
                    if (s.Kind == HitKind.Projectile || s.Kind == HitKind.Charge) AddPat(g, sec, sk + "Speed", "속도(U/s)", "", () => s.Speed, v => s.Speed = v);
                }
            }
        }

        public const string PAT_CAT = "결투자 패턴";
        public const string ARENA = "결투장 · 맵 패턴/결투장";

        public static string KindName(HitKind k) =>
            k == HitKind.Melee ? "근접" : k == HitKind.Low ? "낮은 공격" : k == HitKind.Projectile ? "에너지탄" : "돌진";

        /// <summary>공통 수치가 놓인 자리(‘대분류 › 탭 › 묶음 · 이름’).</summary>
        public static string Where(string field)
        {
            var f = typeof(FightTune).GetField(field);
            var a = f == null ? null : (TuneAttribute)Attribute.GetCustomAttribute(f, typeof(TuneAttribute));
            return a == null ? field : a.Group.Replace("/", " › ") + " · " + a.Name;
        }

        /// <summary>패턴별 덮어쓰기: −1 = 공통값 사용. 슬라이더는 −1 ~ 공통 기본값의 2배.</summary>
        static void Ovr(string g, string sec, string k, string key, string name, string common, Func<double> get, Action<double> set)
        {
            var f = typeof(FightTune).GetField(common);
            double cd = f == null ? 1 : (double)f.GetValue(Defaults);
            AddPat(g, sec, k + key, name, "−1 = 공통값 사용 · 공통: " + Where(common), get, set, -1, cd > 0 ? cd * 2 : 1);
        }

        static void AddPat(string g, string sec, string key, string name, string spec, Func<double> get, Action<double> set, double? min = null, double? max = null, bool nextRound = false)
        {
            double d = get();
            int cut = g.IndexOf('/');
            items.Add(new TuneItem
            {
                Key = key, Group = g, Cat = g.Substring(0, cut), Tab = g.Substring(cut + 1), Section = sec,
                Name = name, Spec = spec, IsPattern = true, Default = d, NextRound = nextRound,
                Min = min ?? (d < 0 ? d * 2 : 0), Max = max ?? (d > 0 ? d * 2 : d < 0 ? 0 : 1), Get = get, Set = set,
            });
        }

        public sealed class TuneValue { public string Name; public double Value; }
        public sealed class TuneValues { public int V = 2; public List<TuneValue> Items = new List<TuneValue>(); }
        public sealed class TuneRange { public string Name; public double Min, Max; }
        public sealed class TuneRanges { public int V = 2; public List<TuneRange> Items = new List<TuneRange>(); }
        static Dictionary<string, (double min, double max)> ranges;

        public static void Load()
        {
            try
            {
                string sp = Bundled.Pick(SavePath, "fight-tune.json");      // 내 것 → 없으면 빌드에 담긴 것
                if (File.Exists(sp))
                {
                    var t = MoriJson.Deserialize<FightTune>(File.ReadAllText(sp));
                    if (t != null)
                    {
                        t.Clamp();
                        // 예전 단위(V 1 — 캐릭터 그림 3U 기준)로 저장된 값: 길이 값을 지금 단위(1U = 그림 100px)로 줄임
                        if (t.V < 2) { t.ScaleLengths(FightUnit.S); Debug.Log("[Mori] 전투 수치를 새 길이 단위로 바꿨어요(× " + FightUnit.S.ToString("0.####") + "). 저장하면 새 단위로 남아요."); }
                        if (t.V < 3) { t.CamZoom = 1; t.V = 3; }      // 넓이 배율 기본 1(1080p 에서 1U = 128px)
                        if (t.V < 4)
                        {
                            // 궁극기: 9초 · 컷씬 3.5초 · 수급을 줄이고 모든 데미지에서 조금씩(예전 저장값은 새 기본으로)
                            var nd = new FightTune();
                            t.UltTime = nd.UltTime; t.UltCut = nd.UltCut;
                            t.UltGainHurt = nd.UltGainHurt; t.UltGainCounter = nd.UltGainCounter; t.UltGainNear = nd.UltGainNear; t.UltGainCrash = nd.UltGainCrash;
                            t.UltGainHit = nd.UltGainHit; t.UltGainCtrAtk = nd.UltGainCtrAtk;
                            t.V = 4;
                        }
                        if (t.V < 5) { if (Math.Abs(t.CrashGoodBeats - 0.25) < 1e-6) t.CrashGoodBeats = 0.4; t.V = 5; }     // 크래시 굿 판정 창 0.25 → 0.4박(직접 바꾼 값은 그대로)
                        if (t.V < 8)
                        {
                            // 동료(기계): 새로 만든 시스템이라 저장된 동료 값은 새 기본값으로(엄호 자리 · 높은 곳 · 드론 자제 · 컷인 끔)
                            var nd6 = new FightTune();
                            foreach (var f in typeof(FightTune).GetFields())
                                if (f.FieldType == typeof(double) && (f.Name.StartsWith("Ally") || f.Name == "ImgScaleAlly")) f.SetValue(t, f.GetValue(nd6));
                            t.V = 8;
                        }
                        if (t.V < 9) { t.AllyOn = 0; t.V = 9; }
                        if (t.V < 10) { if (t.ReadySec <= 0) t.ReadySec = 1.5; t.V = 10; }       // 준비: 1.5초 루프 → 준비 → 대기 연결(직접 정한 값은 그대로)       // 동료(저격수)는 실험 기능 — 기본 끔(작업실 ‘저격수’ 버튼 · F2 › 동료 › 켬으로 켜기)
                        cur = t;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 전투 튜닝을 읽지 못해 기본값을 씁니다: " + e.Message); }
            if (cur == null) cur = new FightTune();
            Build();                                     // 패턴 기본값을 먼저 잡고
            try
            {
                string pp = Bundled.Pick(PatternPath, "fight-patterns.json");
                if (File.Exists(pp))                     // 저장한 패턴 값을 덮는다
                {
                    var pv = MoriJson.Deserialize<TuneValues>(File.ReadAllText(pp));
                    var map = items.ToDictionary(x => x.Key);
                    bool old = pv != null && pv.V < 2;
                    if (pv?.Items != null)
                        foreach (var v in pv.Items)
                            if (v?.Name != null && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value) && map.TryGetValue(v.Name, out var it))
                                it.Set(old ? OldToNew(it.Key, v.Value) : v.Value);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 패턴 튜닝을 읽지 못했습니다: " + e.Message); }
        }

        static void LoadRanges()
        {
            ranges = new Dictionary<string, (double, double)>();
            try
            {
                string rp = Bundled.Pick(RangePath, "fight-tune-ranges.json");
                if (!File.Exists(rp)) return;
                var r = MoriJson.Deserialize<TuneRanges>(File.ReadAllText(rp));
                if (r?.Items == null) return;
                bool old = r.V < 2;
                foreach (var it in r.Items)
                    if (it?.Name != null && !double.IsNaN(it.Min) && !double.IsNaN(it.Max) && it.Max > it.Min)
                        ranges[it.Name] = old ? (OldToNew(it.Name, it.Min), OldToNew(it.Name, it.Max)) : (it.Min, it.Max);
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 슬라이더 범위를 읽지 못해 기본 범위를 씁니다: " + e.Message); }
        }

        // ───────── 길이 단위 바꾸기(V 1 → 2) ─────────
        static readonly string[] PAT_LEN = { "MinD", "MaxD", "Pref", "Reach", "Lunge", "Height", "Speed", "Cx", "Width", "Top", "Thick" };
        static readonly string[] PAT_LEN_OVR = { "BoxBottom", "ChargeHalf", "LeapHeight", "ShotRadius", "ShotAimY", "ShotOriginX", "ShotOriginY" };

        /// <summary>항목 키가 길이 단위(U · U/s)인지 — 전투 수치는 이름의 단위로, 패턴 · 지형은 키로.</summary>
        public static bool IsLength(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key.StartsWith("pat.") || key.StartsWith("arena."))
            {
                string last = key.Substring(key.LastIndexOf('.') + 1);
                return PAT_LEN.Contains(last) || PAT_LEN_OVR.Contains(last);
            }
            var f = typeof(FightTune).GetField(key);
            var a = f == null ? null : (TuneAttribute)Attribute.GetCustomAttribute(f, typeof(TuneAttribute));
            return a != null && FightTune.IsLength(key, a.Name);
        }

        /// <summary>예전 단위로 저장된 값 → 지금 단위. 패턴 덮어쓰기의 −1(공통값 사용)처럼 음수 표시는 그대로.</summary>
        static double OldToNew(string key, double v)
        {
            if (!IsLength(key)) return v;
            string last = key.Substring(key.LastIndexOf('.') + 1);
            if (key.StartsWith("pat.") && PAT_LEN_OVR.Contains(last) && v < 0) return v;
            return v * FightUnit.S;
        }

        public static (double min, double max) Range(TuneItem it)
        {
            if (ranges == null) LoadRanges();
            return ranges.TryGetValue(it.Key, out var r) ? r : (it.Min, it.Max);
        }

        public static void SetRange(TuneItem it, double min, double max)
        {
            if (ranges == null) LoadRanges();
            ranges[it.Key] = (min, max);
        }

        public static void ResetRanges(string group = null)
        {
            if (ranges == null) LoadRanges();
            foreach (var it in Items) if (group == null || it.Group == group) ranges.Remove(it.Key);
        }

        /// <summary>슬라이더 · ± 버튼 눈금: 범위의 약 1/40 을 보기 좋은 수로.</summary>
        public static double StepOf(double min, double max)
        {
            double raw = Math.Abs(max - min) / 40;
            if (raw <= 0 || double.IsNaN(raw)) return 0.01;
            double p10 = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double m = raw / p10;
            return (m < 1.5 ? 1 : m < 3.5 ? 2 : m < 7.5 ? 5 : 10) * p10;
        }

        public static bool Save(out string err)
        {
            err = null;
            try
            {
                KitBackup.Before(SavePath); KitBackup.Before(PatternPath); KitBackup.Before(RangePath);      // 덮어쓰기 전 자동 백업
                File.WriteAllText(SavePath, MoriJson.Serialize(Current, true));
                var pv = new TuneValues();
                foreach (var it in Items) if (it.IsPattern && Changed(it)) pv.Items.Add(new TuneValue { Name = it.Key, Value = it.Get() });
                File.WriteAllText(PatternPath, MoriJson.Serialize(pv, true));
                if (ranges == null) LoadRanges();
                var r = new TuneRanges();
                foreach (var kv in ranges) r.Items.Add(new TuneRange { Name = kv.Key, Min = kv.Value.min, Max = kv.Value.max });
                File.WriteAllText(RangePath, MoriJson.Serialize(r, true));
                return true;
            }
            catch (Exception e) { err = e.Message; return false; }
        }

        /// <summary>기본값으로(같은 객체를 고쳐서 — 진행 중인 결투에도 바로 적용). group 이 null 이면 전부.</summary>
        public static void Reset(string group = null)
        {
            foreach (var it in Items) if (group == null || it.Group == group) it.Set(it.Default);
        }

        public static bool Changed(TuneItem it) => Math.Abs(it.Get() - it.Default) > 1e-9;

        static string Num(double v) => v.ToString("0.######", Ci);
        static string Esc(string s) => (s ?? "").Replace("\\", "/").Replace("\"", "'");

        /// <summary>Claude 에게 붙여 줄 텍스트: 바뀐 값(기본값 → 지금, 슬라이더 구간) + 전체 값.</summary>
        public static string ExportText()
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"mori_fight_tune\": 2,\n");
            sb.Append("  \"exported\": \"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", Ci)).Append("\",\n");
            sb.Append("  \"changed\": {");
            bool first = true;
            foreach (var it in Items)
            {
                if (!Changed(it)) continue;
                var (lo, hi) = Range(it);
                sb.Append(first ? "\n" : ",\n");
                first = false;
                sb.Append("    \"").Append(it.Key).Append("\": { \"value\": ").Append(Num(it.Get()))
                  .Append(", \"default\": ").Append(Num(it.Default))
                  .Append(", \"slider\": [").Append(Num(lo)).Append(", ").Append(Num(hi)).Append("]")
                  .Append(", \"name\": \"").Append(Esc(it.Cat + " › " + it.Tab + " › " + it.Section)).Append(" · ").Append(Esc(it.Name)).Append("\", \"spec\": \"").Append(Esc(it.Spec)).Append("\" }");
            }
            sb.Append(first ? "},\n" : "\n  },\n");
            sb.Append(KeyBinds.ExportJson("  ")).Append(",\n");
            sb.Append("  \"all\": {");
            first = true;
            foreach (var it in Items)
            {
                sb.Append(first ? "\n" : ",\n");
                first = false;
                sb.Append("    \"").Append(it.Key).Append("\": ").Append(Num(it.Get()));
            }
            sb.Append("\n  }\n}\n");
            return sb.ToString();
        }

        public static bool Export(out string text, out string err)
        {
            text = ExportText();
            err = null;
            try { GUIUtility.systemCopyBuffer = text; } catch (Exception e) { err = "클립보드: " + e.Message; }
            try { File.WriteAllText(ExportPath, text); }
            catch (Exception e) { err = (err == null ? "" : err + " · ") + "파일: " + e.Message; return false; }
            return true;
        }
    }

    /// <summary>
    /// 전투 튜닝 패널(F2): 탭(수치 묶음 · 패턴마다) → 항목마다 이름 · 명세서 항목명 · [최소 칸] − 슬라이더 + [최대 칸] · 현재값 칸. 바뀐 값은 금색.
    /// 값은 한계 없이 직접 입력(Enter 또는 칸 밖 클릭으로 반영), 슬라이더는 최소 · 최대 사이를 드래그. 입력 중에는 결투 키가 막힌다. 결투는 멈추지 않는다.
    /// </summary>
    public sealed class TunePanel
    {
        readonly VisualElement el, tabs;
        readonly ScrollView tabSv;
        readonly ScrollView list;
        readonly Label status;
        readonly List<Action> refresh = new List<Action>();
        readonly Dictionary<string, Button> tabBtns = new Dictionary<string, Button>();
        string group;
        float statusAge = 99;
        int typing;
        /// <summary>맵 패턴 탭의 ‘지금 시전’ 버튼(결투 중일 때만 동작).</summary>
        public Action<MapKind> OnForceMap;

        public bool IsOpen => el.style.display == DisplayStyle.Flex;
        /// <summary>입력 칸에 글자를 넣는 중이거나 바꿀 키를 기다리는 중(이때는 결투 키 · Esc 를 막는다).</summary>
        public bool Typing => IsOpen && (typing > 0 || capSlot >= 0);
        const string GUIDE_CAT = "안내 · 조작";
        const string GLOSSARY_TAB = GUIDE_CAT + "/용어 · 단위";
        const string KEYS_TAB = GUIDE_CAT + "/조작 키";
        const string SOUND_TAB = GUIDE_CAT + "/소리";
        const string FILES_TAB = GUIDE_CAT + "/파일 · 백업";
        /// <summary>음량을 바꿈(작업실이 음악 켬/끔과 함께 다시 적용).</summary>
        public Action OnVolume;
        /// <summary>패널을 누름(작업실: 키보드 제어권을 패널로) · 패널이 닫힘(제어권을 편집기로 돌려줌).</summary>
        public Action OnFocus, OnHidden;

        /// <summary>제어권이 패널에 있는지 표시(테두리).</summary>
        public void SetCtl(bool on) => el.EnableInClassList("tp--ctl", on);
        bool soundDirty;
        float soundSaveT;
        /// <summary>대분류 순서(탭 막대에서 테두리 상자 하나씩). 목록에 없는 대분류는 뒤에.</summary>
        static readonly string[] CAT_ORDER = { GUIDE_CAT, "플레이어", "동료(기계)", "결투자", TuneStore.PAT_CAT, "공통 규칙", "결투장 · 맵 패턴", "리듬 · 예고", "화면" };
        static readonly Dictionary<string, string> CAT_DESC = new Dictionary<string, string>
        {
            [GUIDE_CAT] = "용어 뜻 · 키 설정",
            ["플레이어"] = "내 캐릭터가 하는 행동",
            ["동료(기계)"] = "기계 공격을 맡는 여주인공(땅에 선 인간형) — Z 를 누르면 따라 쏨(조작은 그대로)",
            ["결투자"] = "적이 하는 행동 · 판단(모든 패턴 공통)",
            [TuneStore.PAT_CAT] = "적 공격 하나하나 — 공통값을 패턴마다 덮어쓰기",
            ["공통 규칙"] = "양쪽에 똑같이 걸리는 규칙(중력 · 경직 · 넉백 · 크래시)",
            ["결투장 · 맵 패턴"] = "지형과 맵 전체 공격",
            ["리듬 · 예고"] = "박 판정 · 그루브 · 공격 전 신호",
            ["화면"] = "카메라 · 타격감(판정에는 영향 없음)",
        };
        int capSlot = -1;
        bool capReplace;
        float capAge;

        public TunePanel(VisualElement parent)
        {
            el = new VisualElement { focusable = true };
            el.AddToClassList("tp");
            parent.Add(el);
            // 패널 어디를 눌러도 제어권을 패널로(누른 버튼 · 칸이 그 뒤에 포커스를 가져감 — 빈 곳이면 패널 자체가)
            el.RegisterCallback<PointerDownEvent>(_ => { el.Focus(); OnFocus?.Invoke(); }, TrickleDown.TrickleDown);
            var head = new VisualElement(); head.AddToClassList("tp-head"); el.Add(head);
            head.Add(Lbl("전투 튜닝", "tp-title"));
            var close = new Button(Hide) { text = "닫기 (F2)" };
            close.AddToClassList("tp-btn");
            head.Add(close);
            el.Add(Lbl("값은 바로 적용돼요. 오른쪽 위 칸 = 현재값(직접 입력, 한계 없음), 아래 양옆 칸 = 슬라이더 최소 · 최대(드래그 구간). 입력은 Enter 또는 칸 밖을 누르면 반영돼요. ‘저장’ = 값 · 패턴 · 슬라이더 구간을 다음 실행에도 유지, ‘내보내기’ = 바뀐 값을 클립보드에 복사(그대로 Claude 에게 붙여 주세요). (다음 판) = 새 판부터 적용.", "tp-sub"));
            // 탭이 많아도(패턴마다 하나) 목록을 밀어내지 않게: 탭은 자기 스크롤 안에서 줄바꿈
            tabSv = UiKit.Scroll("tp-tabs-sv", "tp-tabs");
            el.Add(tabSv);
            tabs = tabSv.contentContainer;
            // 탭 막대: 대분류마다 테두리 상자(제목 · 한 줄 설명 · 탭 버튼)
            var groups = new[] { GLOSSARY_TAB, KEYS_TAB, SOUND_TAB, FILES_TAB }.Concat(TuneStore.Items.Select(x => x.Group)).Distinct().ToList();
            var cats = groups.Select(CatOf).Distinct().OrderBy(c => { int i = Array.IndexOf(CAT_ORDER, c); return i < 0 ? 99 : i; }).ToList();
            foreach (var c in cats)
            {
                var box = new VisualElement(); box.AddToClassList("tp-cat");
                if (c == TuneStore.PAT_CAT) box.AddToClassList("tp-cat--pat");
                tabs.Add(box);
                var ch = new VisualElement(); ch.AddToClassList("tp-cat-h"); box.Add(ch);
                ch.Add(Lbl(c, "tp-cat-t"));
                if (CAT_DESC.TryGetValue(c, out var cd)) ch.Add(Lbl(cd, "tp-cat-d"));
                var row = new VisualElement(); row.AddToClassList("tp-cat-tabs"); box.Add(row);
                foreach (var g in groups.Where(x => CatOf(x) == c))
                {
                    string gg = g;
                    var b = new Button(() => ShowGroup(gg)) { text = TabOf(g) };
                    b.AddToClassList("tp-tab");
                    if (c == TuneStore.PAT_CAT) b.AddToClassList("tp-tab--pat");
                    row.Add(b);
                    tabBtns[g] = b;
                }
            }
            list = UiKit.Scroll("tp-list", "tp-list-c");
            el.Add(list);
            var foot = new VisualElement(); foot.AddToClassList("tp-foot"); el.Add(foot);
            Btn(foot, "저장", true, () =>
            {
                if (TuneStore.Save(out string err)) Status("저장했어요 · " + TuneStore.SavePath);
                else Status("저장하지 못했어요: " + err);
            });
            Btn(foot, "내보내기(복사)", true, () =>
            {
                bool ok = TuneStore.Export(out _, out string err);
                int n = TuneStore.Items.Count(TuneStore.Changed) + Enumerable.Range(0, KeyBinds.COUNT).Count(KeyBinds.Changed);
                Status(ok && err == null ? $"바뀐 값 {n}개를 클립보드에 복사했어요 · 파일: {TuneStore.ExportPath}" : ok ? $"파일로 저장했어요({TuneStore.ExportPath}) · {err}" : "내보내지 못했어요: " + err);
            });
            Btn(foot, "이 탭 기본값", false, () =>
            {
                if (group == KEYS_TAB) { KeyBinds.Reset(); RefreshAll(); Status("조작 키를 모두 기본으로 되돌렸어요."); return; }
                if (group == FILES_TAB) { KitBackup.Keep = 10; KitBackup.Dir = null; ShowGroup(FILES_TAB); Status("백업 위치 · 개수를 기본으로 되돌렸어요."); return; }
                if (group == SOUND_TAB) { var d = new GameSettings(); var st = KitRoot.Settings; st.VolMaster = d.VolMaster; st.VolMusic = d.VolMusic; st.VolSfx = d.VolSfx; st.VolAtkMagic = d.VolAtkMagic; st.VolAtkMech = d.VolAtkMech; st.AtkSoundOnPress = d.AtkSoundOnPress; st.StageCue = d.StageCue; st.Save(); OnVolume?.Invoke(); ShowGroup(SOUND_TAB); Status("소리를 기본으로 되돌렸어요."); return; }
                if (group == GLOSSARY_TAB) { Status("용어 탭에는 값이 없어요."); return; }
                TuneStore.Reset(group); TuneStore.ResetRanges(group); RefreshAll(); Status($"‘{PathText(group)}’ 의 값 · 슬라이더 구간을 기본으로 되돌렸어요(저장해야 유지).");
            });
            Btn(foot, "전체 기본값", false, () => { TuneStore.Reset(); TuneStore.ResetRanges(); RefreshAll(); Status("모든 값 · 슬라이더 구간을 기본으로 되돌렸어요(저장해야 유지)."); });
            status = Lbl("", "tp-status");
            el.Add(status);
            el.style.display = DisplayStyle.None;
        }

        static Label Lbl(string t, string cls) { var l = new Label(t); l.AddToClassList(cls); return l; }
        static string CatOf(string g) { int i = g.IndexOf('/'); return i < 0 ? g : g.Substring(0, i); }
        static string TabOf(string g) { int i = g.IndexOf('/'); return i < 0 ? g : g.Substring(i + 1); }
        static string PathText(string g) => g.Replace("/", " › ");

        static void Btn(VisualElement p, string t, bool main, Action a)
        {
            var b = new Button(a) { text = t };
            b.AddToClassList("tp-btn");
            if (main) b.AddToClassList("tp-btn--main");
            p.Add(b);
        }

        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        public void Show()
        {
            el.style.display = DisplayStyle.Flex;
            el.BringToFront();
            ShowGroup(group ?? KEYS_TAB);
        }

        public void Hide()
        {
            var f = el.panel?.focusController?.focusedElement as VisualElement;
            if (f != null) f.Blur();
            typing = 0;
            capSlot = -1;
            el.style.display = DisplayStyle.None;
            OnHidden?.Invoke();
        }

        public void Tick()
        {
            // 소리 설정: 슬라이더를 움직이는 동안은 미뤘다가 잠시 뒤 한 번 저장
            if (soundDirty) { soundSaveT -= Time.unscaledDeltaTime; if (soundSaveT <= 0) { soundDirty = false; KitRoot.Settings.Save(); } }
            if (!IsOpen) return;
            statusAge += Time.unscaledDeltaTime;
            status.style.opacity = statusAge < 6 ? 1 : Mathf.Max(0, 1 - (statusAge - 6));
            // 키 바꾸기: 버튼을 누른 그 프레임의 입력은 건너뛰고, 다음에 눌린 키를 받는다(Esc = 취소, F2 는 패널 키라 못 씀)
            if (capSlot >= 0)
            {
                capAge += Time.unscaledDeltaTime;
                if (capAge < 0.1f) return;
                string k = KeyBinds.PressedThisFrame();
                if (k == null) return;
                int slot = capSlot;
                capSlot = -1;
                if (k == "Escape") { Status("키 바꾸기를 취소했어요."); RefreshAll(); return; }
                if (k == "F2") { Status("F2 는 튜닝 패널 키라 쓸 수 없어요."); RefreshAll(); return; }
                string moved = KeyBinds.Assign(slot, k, capReplace);
                Status($"‘{KeyBinds.NAME[slot]}’ = {KeyBinds.Label(slot)}" + (moved != null ? $"  (‘{moved}’ 에서 옮겨 왔어요)" : "") + " · 바로 저장됨");
                RefreshAll();
            }
        }

        void Status(string t) { status.text = t; statusAge = 0; status.style.opacity = 1; }

        void RefreshAll() { foreach (var r in refresh) r(); }
        /// <summary>바깥에서 값을 바꾼 뒤 칸들을 다시 그림(작업실 ‘저격수’ 버튼 등).</summary>
        public void RefreshValues() => RefreshAll();

        void ShowGroup(string g)
        {
            if (!tabBtns.ContainsKey(g)) g = KEYS_TAB;
            group = g;
            foreach (var kv in tabBtns) kv.Value.EnableInClassList("tp-tab--on", kv.Key == g);
            list.contentContainer.Clear();
            refresh.Clear();
            typing = 0;
            list.contentContainer.Add(Lbl(PathText(g), "tp-path"));
            if (g == GLOSSARY_TAB) { Glossary(); TextGuard.Schedule(el); return; }
            if (g == KEYS_TAB) { KeyRows(); TextGuard.Schedule(el); return; }
            if (g == SOUND_TAB) { SoundRows(); TextGuard.Schedule(el); return; }
            if (g == FILES_TAB) { FilesRows(); TextGuard.Schedule(el); return; }
            if (CatOf(g) == "결투장 · 맵 패턴") MapButtons(TabOf(g));
            if (CatOf(g) == TuneStore.PAT_CAT)
                list.contentContainer.Add(Lbl("‘이 패턴만’ 묶음의 −1 = 공통값을 그대로 씀. 0 이상을 넣으면 이 패턴에서만 그 값으로 바뀌어요(공통값 자리는 항목 설명에 적혀 있어요).", "tp-note"));
            // 묶음(Section)마다 테두리 상자: 제목 · 무엇에 작용하는지 · 항목들
            foreach (var sec in TuneStore.Items.Where(x => x.Group == g).GroupBy(x => x.Section))
            {
                var box = Section(sec.Key, SectionDesc(g, sec.Key));
                foreach (var it in sec) Row(box, it);
            }
            if (g == FLOW_TAB) { ReadyRows(); FlowRows(); }
            TextGuard.Schedule(el);
        }

        const string FLOW_TAB = "화면/캐릭터 흐름";

        /// <summary>연결 표: 줄마다 [이전] → [다음] = [연결 그림] · 그림 있는지 · 빼기. 바꾸면 바로 저장(Data~/Fight/flow.txt).</summary>
        void FlowRows()
        {
            var box = Section("연결 표", "이전 상태 → 다음 상태로 바뀔 때 그 사이에 한 번 보여 줄 그림. * = 아무 상태. 최소 이전 = 이전 상태가 그 초 이상 이어졌을 때만 재생(멈춤 기본 0.2 — 착지 직후 잠깐 달리다 서면 멈춤 없이 착지를 끝까지 보여 줌). 그림이 아직 없으면 그 줄은 건너뛰어요(바로 다음 상태). 연결 그림은 도트 에디터 파일 목록에 나와요 — 그려서 저장하면 바로 쓰여요. 연결 도중에도 입력이 오면 즉시 끊겨요.");
            var names = SpriteEditor.KnownNames().ToList();
            var rows = FlowTable.Rows;
            if (rows.Count == 0) box.Add(Lbl("줄이 없어요 — ‘줄 추가’ 로 만들어 주세요.", "tp-spec"));
            for (int i = 0; i < rows.Count; i++)
            {
                int idx = i;
                var r = rows[i];
                var line = new VisualElement(); line.AddToClassList("tp-flow");
                box.Add(line);
                var from = FlowDd(new[] { "*" }.Concat(names), r.From, v => { var x = FlowTable.Rows[idx]; x.From = v; FlowTable.Rows[idx] = x; FlowTable.Save(); });
                line.Add(from);
                line.Add(Lbl("→", "tp-flow-sym"));
                var to = FlowDd(new[] { "*" }.Concat(names), r.To, v => { var x = FlowTable.Rows[idx]; x.To = v; FlowTable.Rows[idx] = x; FlowTable.Save(); });
                line.Add(to);
                line.Add(Lbl("=", "tp-flow-sym"));
                var has = Lbl("", "tp-flow-has");
                void Mark(string a) { bool ok = ArtLib.Get(a) != null; has.text = ok ? "그림 있음" : "안 그림"; has.EnableInClassList("tp-flow-has--no", !ok); }
                var anim = FlowDd(names, r.Anim, v => { var x = FlowTable.Rows[idx]; x.Anim = v; FlowTable.Rows[idx] = x; FlowTable.Save(); Mark(v); });
                line.Add(anim);
                line.Add(has);
                Mark(r.Anim);
                line.Add(Lbl("최소 이전", "tp-flow-sym"));
                var min = Num("tp-flow-min");
                min.tooltip = "이전 상태가 이 시간(초) 이상 이어졌을 때만 이 연결을 재생 · 0 = 언제나. 예: 멈춤 0.2 = 잠깐 움직였다 서면 멈춤 없이 바로 준비";
                min.SetValueWithoutNotify(Math.Round(r.MinPrev, 3));
                min.RegisterValueChangedCallback(e => { var x = FlowTable.Rows[idx]; x.MinPrev = Mathf.Max(0, (float)e.newValue); FlowTable.Rows[idx] = x; FlowTable.Save(); if (e.newValue < 0) min.SetValueWithoutNotify(0); });
                line.Add(min);
                line.Add(Lbl("초", "tp-flow-sym"));
                Btn(line, "빼기", false, () => { FlowTable.Rows.RemoveAt(idx); FlowTable.Save(); ShowGroup(FLOW_TAB); Status("연결 줄을 뺐어요."); });
            }
            var foot = new VisualElement(); foot.AddToClassList("tp-foot"); box.Add(foot);
            Btn(foot, "줄 추가", true, () =>
            {
                FlowTable.Rows.Add(new FlowTable.Row { From = "hero-run", To = "hero-idle", Anim = "hero-stop" });
                FlowTable.Save(); ShowGroup(FLOW_TAB); Status("줄을 추가했어요 — 이전 · 다음 · 그림을 골라 주세요.");
            });
            Btn(foot, "기본 표로", false, () =>
                Dialog.Confirm("연결 표 기본으로", "연결 표를 기본(착지 · 멈춤 · 출발)으로 되돌릴까요? 지금 표는 백업에 남아요.", "되돌리기", true, () => { FlowTable.ResetDefault(); ShowGroup(FLOW_TAB); Status("연결 표를 기본으로 되돌렸어요."); }));
            var T = TuneStore.Current;
            double g = Math.Max(0.0001, T.Gravity), tJ = T.JumpV / g;
            double g2 = g * T.DJumpGravityMul, t1 = g2 > 0 ? Math.Min(T.DJumpTime, T.DJumpV / g2) : T.DJumpTime;
            double tD = t1 + Math.Max(0, T.DJumpV - g2 * t1) / g;
            string Sec(double x) => $"약 {x:0.00}초({Math.Round(x * 60):0}f)";
            var air = Section("공중 그림", "hero-air(공중 몸통)를 ‘오름 → 정점 → 낙하’ 순서로 그리면 시간 대신 세로 속도로 프레임을 골라요 — 가운데 프레임 = 정점(플레이어 › 점프 › 체공 느낌의 ‘정점 판정 세로 속도’ 안). 그리면 hero-jump · hero-doublejump 는 짧은 도약 모션(1회)이 되고, 끝나면 그때 속도의 공중 프레임으로 이어져요. 도약은 정점을 넘겨도 끝까지 재생(착지 · 공격 · 대쉬만 끊음). 오래 떨어지면 hero-fall(긴 낙하 루프)로 — 첫 프레임을 hero-air 마지막 프레임과 비슷한 자세로 그리면 이어짐이 자연스러워요.");
            air.Add(Lbl($"정점까지 걸리는 시간(지금 튜닝 기준) — 점프 {Sec(tJ)} · 더블 점프 {Sec(tD)}. 도약 모션이 이보다 짧으면 오름 중에 공중 몸통으로 넘어가고, 길면 정점을 넘겨 끝나요(60fps 프레임 기준).", "tp-spec"));
            box.Add(Lbl("이름 규칙 — 준비: hero-ready (이전 행동별: hero-ready-attack · hero-ready-run …) · 대기: hero-idle · 긴 대기: hero-idle-long, hero-idle-long2 … (결투자는 foe-)", "tp-spec"));
        }

        /// <summary>준비 그림별 재생: 줄마다 [그림] [루프 | 1회] [초] · 그림 있는지. 바꾸면 바로 저장(Data~/Fight/ready.txt).</summary>
        void ReadyRows()
        {
            var box = Section("준비 그림별 재생", "행동이 끝난 뒤 나오는 준비 그림을 그림마다 루프 또는 1회로. 루프 = 시간 동안 되풀이(비우면 위의 ‘준비 시간’) → 준비 → 대기 연결(-ready-end) → 대기. 1회 = 한 번 재생하고 넘어감(시간을 그림보다 길게 주면 마지막 프레임에서 그만큼 기다림). 공격 뒤 준비(-ready-attack…, 여운)는 기본 1회 — 끝나면 그냥 준비(-ready)로 이어지고 → 준비 끝(-ready-end) → 대기. 입력이 오면 언제든 즉시 끊겨요.");
            var keys = new SortedSet<string>(StringComparer.Ordinal) { "hero-ready", "foe-ready" };
            foreach (var n in SpriteEditor.KnownNames()) if (ReadyPlay.IsReady(n)) keys.Add(n);
            foreach (var a in ArtLib.AllAnims) if (a != null && ReadyPlay.IsReady(a.Key)) keys.Add(a.Key);
            foreach (var k in keys)
            {
                string key = k;
                var line = new VisualElement(); line.AddToClassList("tp-flow");
                box.Add(line);
                var name = Lbl(key, "tp-flow-sym"); name.AddToClassList("tp-ready-name"); line.Add(name);
                var m = ReadyPlay.Get(key);
                Button loopB = null, onceB = null;
                void Paint(bool lp) { loopB.EnableInClassList("tp-btn--on", lp); onceB.EnableInClassList("tp-btn--on", !lp); }
                loopB = new Button(() => { var x = ReadyPlay.Get(key); x.Loop = true; ReadyPlay.Set(key, x); Paint(true); Status($"{key}: 루프"); }) { text = "루프" };
                onceB = new Button(() => { var x = ReadyPlay.Get(key); x.Loop = false; ReadyPlay.Set(key, x); Paint(false); Status($"{key}: 1회"); }) { text = "1회" };
                loopB.AddToClassList("tp-btn"); onceB.AddToClassList("tp-btn");
                line.Add(loopB); line.Add(onceB);
                loopB.tooltip = "준비 시간 동안 되풀이한 뒤 대기로";
                onceB.tooltip = "한 번 재생하고 넘어감(시간을 더 주면 마지막 프레임에서 기다림)";
                Paint(m.Loop);
                line.Add(Lbl("시간", "tp-flow-sym"));
                var sec = Num("tp-flow-min");
                sec.tooltip = "0 = 기본(루프 → 위의 ‘준비 시간’, 1회 → 그림 길이)";
                sec.SetValueWithoutNotify(Math.Round(m.Sec, 3));
                sec.RegisterValueChangedCallback(e => { var x = ReadyPlay.Get(key); x.Sec = Mathf.Max(0, (float)e.newValue); ReadyPlay.Set(key, x); if (e.newValue < 0) sec.SetValueWithoutNotify(0); });
                line.Add(sec);
                line.Add(Lbl("초", "tp-flow-sym"));
                bool ok = ArtLib.Get(key) != null;
                var has = Lbl(ok ? "그림 있음" : "안 그림", "tp-flow-has");
                has.EnableInClassList("tp-flow-has--no", !ok);
                line.Add(has);
            }
        }

        static DropdownField FlowDd(IEnumerable<string> choices, string value, Action<string> set)
        {
            var list = choices.ToList();
            if (!string.IsNullOrEmpty(value) && !list.Contains(value)) list.Insert(0, value);
            var dd = new DropdownField(list, Mathf.Max(0, list.IndexOf(value)));
            dd.AddToClassList("dd");
            dd.AddToClassList("tp-flow-dd");
            dd.RegisterValueChangedCallback(e => { if (e.newValue != null && e.newValue != e.previousValue) set(e.newValue); });
            return dd;
        }

        VisualElement Section(string title, string desc)
        {
            var box = new VisualElement(); box.AddToClassList("tp-sec");
            list.contentContainer.Add(box);
            box.Add(Lbl(title, "tp-sec-t"));
            if (!string.IsNullOrEmpty(desc)) box.Add(Lbl(desc, "tp-sec-d"));
            return box;
        }

        /// <summary>맵 패턴 탭의 ‘지금 시전’(결투 중에만): 공통 탭 = 넷 다, 각 맵 패턴 탭 = 그것 하나.</summary>
        void MapButtons(string tab)
        {
            var kinds = new[] { MapKind.Missiles, MapKind.Columns, MapKind.Lasers, MapKind.Walls };
            var show = tab == "맵 패턴 공통" ? kinds : kinds.Where(k => Duel.MapName(k) == tab).ToArray();
            if (show.Length == 0) return;
            var box = Section("지금 시전", "결투 중에만 · 수치를 바꾸고 다시 눌러 확인");
            var row = new VisualElement(); row.AddToClassList("tp-foot"); box.Add(row);
            foreach (var k in show)
            {
                var kk = k;
                Btn(row, Duel.MapName(k), false, () => { OnForceMap?.Invoke(kk); Status($"‘{Duel.MapName(kk)}’ 시전 — 진행 중인 맵 패턴이 있으면 끝난 뒤에 눌러 주세요."); });
            }
        }

        /// <summary>묶음 설명 = 이 값들이 어떤 행동에 작용하는지.</summary>
        static string SectionDesc(string g, string sec)
        {
            if (CatOf(g) == TuneStore.PAT_CAT)
            {
                if (sec.StartsWith("등장")) return "이 패턴을 언제 · 어느 거리에서 고르는지";
                if (sec.StartsWith("예고")) return "이 패턴의 공격 전 신호(색 변화 · 위험 구역 · 예고음)";
                if (sec.StartsWith("접근")) return "타격 전 다가오는 움직임 · 끝난 뒤 여유";
                if (sec.StartsWith("판정")) return "이 패턴 타격의 닿는 범위";
                if (sec.StartsWith("내려찍기")) return "뛰어올라 내려찍는 궤적";
                if (sec.StartsWith("에너지탄")) return "이 패턴이 쏘는 탄";
                return "이 타격 하나 — 박 · 데미지 · 범위";
            }
            if (g == TuneStore.ARENA) return sec == "크기" ? "벽 · 천장 위치(다음 판부터)" : "이 기둥 · 발판의 위치와 크기(다음 판부터)";
            return SEC_DESC.TryGetValue(g + "/" + sec, out var d) ? d : null;
        }

        static readonly Dictionary<string, string> SEC_DESC = new Dictionary<string, string>
        {
            ["화면/카메라/보이는 넓이"] = "한 화면에 담기는 결투장 범위",
            ["동료(기계)/공통"] = "켜면 기계 공격이 동료의 사격으로 바뀜 — 검기(내 몸) + 사격(동료)의 데미지 합은 예전과 같음. 이야기 합류 전에는 꺼 두기",
            ["동료(기계)/엄호 자리"] = "저격수: 늘 플레이어 뒤(적 반대편), 화면 안에서 적에게서 될 수 있는 한 먼 자리, 그 안의 높은 곳(발판 · 기둥 위)을 저격 자리로 잡고 지킴 — 구석 · 고립된 자리는 피함 — 조금 움직이는 건 버티고, 벗어나거나 위험하면 그때 옮김. 적을 보며 뒷걸음 · 옆걸음, 멀 때만 돌아서서 뜀",
            ["동료(기계)/드론 비행"] = "꼭 필요할 때만: 적이 갑자기 가까워짐(겹침 · 접촉 금지) · 화면 밖으로 밀려남 · 최전선이 됨 · 다른 높이로 옮길 때. 한 번 내려앉으면 쉬는 시간 동안은 자리를 사수",
            ["동료(기계)/따라가기"] = "걷기 · 뛰기 · 부드러움 · 길 찾기(기둥은 뛰어넘고 발판은 따라 올라감 · 막히면 드론으로 날아감)",
            ["동료(기계)/사격"] = "늘 플레이어 뒤에서 엄호 — 1 · 2타는 그 자리에서 결투자 쪽으로 쏨(마법 → 기계 지연 뒤). 결투자가 반격 자세면 막히기만 함(벌 없음)",
            ["동료(기계)/강한 사격"] = "3타 · 카운터 성공 · 크래시: 앞으로 나가지 않고 뒤에서 버티고 서서 굵은 한 방(멀리 닿음) → 반동으로 살짝 밀림 → 잠깐 굳은 뒤 다시 따라감",
            ["동료(기계)/컷인"] = "여주인공 얼굴 띠(ally-cutin 그림이 있을 때만, 글자 알림 없음 · 기본 끔). 프리팹 순간 ‘동료 컷인’ 에도 연결할 수 있음",
            ["화면/카메라/따라가기"] = "카메라가 누구를 얼마나 따라가는지",
            ["화면/타격감/히트스톱"] = "타격 순간 시간이 멈칫하는 연출",
            ["플레이어/기본 공격/타별 위치 고정"] = "타마다 공격 상태(= 공격 그림이 보이는 시간)를 따로. 본동작은 이 안에, 휘두른 뒤 여운은 마무리 그림(hero-ready-attack1 ~ 3)으로 — 다음 타를 누르면 마무리는 즉시 끊김",
            ["화면/캐릭터 흐름/켜기"] = "행동 사이를 잇는 그림(연결 · 준비 · 대기 · 긴 대기) — 그림만 바뀌고 조작은 그대로",
            ["화면/캐릭터 흐름/공중"] = "오래 떨어질 때 공중 몸통 마지막 프레임에 굳어 있지 않게 — hero-fall 을 펄럭이는 짧은 루프로 그리면 그리로 넘어감",
            ["화면/캐릭터 흐름/준비 · 대기"] = "행동 → [연결] → 준비 → 대기 → (간격마다) 긴 대기 → 대기. 어디서든 입력이 오면 바로 다음 행동",
            ["화면/타격감/흔들림 · 섬광"] = "화면 효과 세기(판정 영향 없음)",
            ["플레이어/몸 · 체력/몸"] = "맞는 판정 크기 · 시작 자리",
            ["플레이어/이동/달리기"] = "← → 이동",
            ["플레이어/이동/방향 전환"] = "반대 방향으로 꺾을 때",
            ["플레이어/점프/점프"] = "점프 키 첫 점프",
            ["플레이어/점프/더블 점프"] = "공중에서 한 번 더 점프",
            ["플레이어/점프/체공 느낌"] = "정점 · 낙하 때 중력(점프가 뜨는 느낌)",
            ["플레이어/점프/발판 내려가기"] = "↓ + 점프로 발판 통과",
            ["플레이어/대쉬/대쉬"] = "대쉬 키 한 번의 움직임",
            ["플레이어/대쉬/충전"] = "대쉬를 몇 번 연달아 쓸 수 있는지",
            ["플레이어/대쉬/대쉬 뒤"] = "대쉬가 끝난 직후 남는 속도 · 떠 있기",
            ["플레이어/기본 공격/공통"] = "공격 키 3연타 전체",
            ["플레이어/카운터/자세 · 공격"] = "카운터 키: 자세(받아치기 대기) → 카운터 공격",
            ["플레이어/카운터/데미지"] = "카운터 공격 · 카운터 성공 때 데미지",
            ["플레이어/카운터/충전"] = "카운터를 몇 번 쓸 수 있는지",
            ["공통 규칙/중력/중력"] = "모든 ‘중력 배율’ 의 기준값",
            ["공통 규칙/경직/카운터 성공 경직"] = "카운터 성공으로 상대가 굳는 시간(가까울수록 · 벽에 부딪히면 더)",
            ["공통 규칙/경직/한계 · 맞은 뒤"] = "경직 누적 한계 · 맞은 직후 무적",
            ["공통 규칙/넉백 · 월 바운스/넉백"] = "카운터 성공으로 밀려나는 거리 · 속도",
            ["공통 규칙/넉백 · 월 바운스/월 바운스 반동"] = "밀려나다 벽에 부딪힌 뒤 튕기는 움직임",
            ["공통 규칙/카운터 크래시/크래시"] = "양쪽 카운터 공격이 맞부딪칠 때",
            ["공통 규칙/카운터 크래시/퍼펙트 크래시"] = "결투자 카운터 공격 순간에 딱 맞춰 카운터를 시작했을 때",
            ["결투자/몸 · 체력 · 단계/단계"] = "체력이 줄면 단계가 오르고 빨라짐 · 3단계 = 격노",
            ["결투자/이동 · 반응/쉴 때 거리 두기"] = "패턴 사이 쉬는 동안 플레이어와 거리",
            ["결투자/이동 · 반응/박 스텝"] = "쉬는 동안 박에 맞춰 밟는 스텝",
            ["결투자/이동 · 반응/회피"] = "플레이어 공격을 보고 뒤로 빠짐",
            ["결투자/이동 · 반응/경직 풀린 뒤"] = "경직이 풀리자마자 물러남",
            ["결투자/이동 · 반응/지형 따라가기"] = "발판 위 플레이어를 따라 점프 · 기둥 넘기",
            ["결투자/공격 공통/모든 패턴 배율"] = "모든 패턴에 곱해지는 배율",
            ["결투자/공격 공통/근접 판정"] = "근접 · 낮은 공격 판정(패턴마다 덮어쓰기 가능)",
            ["결투자/공격 공통/에너지탄"] = "에너지탄 패턴의 탄(패턴마다 덮어쓰기 가능)",
            ["결투자/공격 공통/돌진 · 내려찍기"] = "돌진 · 내려찍기 패턴(패턴마다 덮어쓰기 가능)",
            ["결투자/패턴 진행/고르기"] = "마디마다 어떤 패턴을 고를지",
            ["결투자/패턴 진행/접근"] = "첫 타격 전 다가오기(패턴마다 덮어쓰기 가능)",
            ["결투자/패턴 진행/끝 · 쉬는 마디"] = "패턴이 끝난 뒤 · 아무것도 안 하는 마디",
            ["결투자/반응 패턴/빈틈 찌르기"] = "플레이어가 헛치면 찌르기",
            ["결투자/반응 패턴/추격"] = "결투자 카운터 성공 뒤 따라가 치기",
            ["결투자/반응 패턴/연타 대응"] = "짧은 시간에 많이 맞으면 카운터 · 밀쳐내기",
            ["결투자/카운터/충전"] = "결투자가 카운터를 몇 번 쓸 수 있는지",
            ["결투자/카운터/자세 · 공격"] = "카운터 패턴: 접근 → 카운터 자세 → 카운터 공격",
            ["결투자/카운터/데미지"] = "결투자 카운터 공격 데미지",
            ["결투자/카운터/위장 예고"] = "카운터 패턴을 베기처럼 보이게 숨기기",
            ["결투자/카운터/습관 읽기"] = "플레이어 공격 간격을 읽고 카운터",
            ["결투자/카운터/엇박 카운터"] = "카운터 공격을 박에서 밀어 내기",
            ["결투장 · 맵 패턴/맵 패턴 공통/일정"] = "맵 패턴이 언제 · 얼마나 자주 나오는지",
            ["결투장 · 맵 패턴/맵 패턴 공통/결투자 무적 비행"] = "무적 맵 패턴 동안 결투자가 떠 있는 움직임",
            ["리듬 · 예고/박 · 그루브/박 판정"] = "박에 맞춘 공격 · 대쉬",
            ["리듬 · 예고/박 · 그루브/그루브"] = "박을 맞출수록 차는 게이지",
            ["리듬 · 예고/박 · 그루브/음악 필터"] = "그루브가 낮으면 음악이 먹먹해짐",
            ["리듬 · 예고/예고 공통/색 변화 · 표식"] = "결투자 몸 색 · 머리 위 표식(모든 패턴 공통 — 패턴 탭에서 덮어쓰기)",
            ["리듬 · 예고/예고 공통/위험 구역"] = "맞을 자리를 미리 칠함(모든 패턴 공통 — 패턴 탭에서 덮어쓰기)",
            ["리듬 · 예고/예고 공통/예고음"] = "타격 전 ‘딱’ 소리(모든 패턴 공통 — 패턴 탭에서 덮어쓰기)",
        };

        /// <summary>용어 · 단위 탭: 같은 말은 같은 뜻으로만 쓴다.</summary>
        void Glossary()
        {
            void G(string t, params string[] lines)
            {
                var b = Section(t, null);
                foreach (var l in lines) b.Add(Lbl(l, "tp-gl"));
            }
            G("사람",
                "플레이어 = 내 캐릭터 · 결투자 = 적",
                "공통 규칙 = 둘 다에게 똑같이 걸리는 규칙");
            G("카운터(양쪽 같은 말)",
                "카운터 자세 = 카운터 키를 누른 뒤 받아칠 준비를 하는 시간. 이때 맞으면 ‘카운터 성공’",
                "카운터 공격 = 자세 뒤에 나가는 반격 한 방",
                "카운터 성공 = 자세 중에 공격을 받아 냄 → 상대 경직 · 넉백",
                "크래시 = 양쪽 카운터 공격이 맞부딪침 · 퍼펙트 크래시 = 결투자 카운터 공격 순간에 딱 맞춰 시작",
                "카운터 패턴 = 결투자가 카운터 자세를 잡는 패턴(위장 예고 · 습관 읽기 · 엇박이 여기에 걸림)");
            G("횟수 · 시간",
                "최대 충전 횟수 = 연달아 쓸 수 있는 횟수 · 1회 충전 시간 = 한 번 다시 차는 시간",
                "사용 후 쿨타임 = 한 번 쓴 뒤 다시 못 쓰는 시간 · 휴식 = 특정 상황 뒤 결투자가 그 행동을 쉬는 시간",
                "시작 지연 = 반응 패턴이 시작하기까지 기다리는 박");
            G("예고(공격 전 신호)",
                "색 변화 = 결투자 몸이 공격 색으로 물들고 머리 위 원이 줄어듦",
                "위험 구역 = 맞을 자리를 바닥 · 공중에 칠함(보이기 시작 → 진해지는 시점)",
                "예고음 = 타격 몇 박 전 ‘딱’",
                "모두 ‘리듬 · 예고 › 예고 공통’ 이 기본, ‘결투자 패턴 › 패턴 이름 › 예고 보여 주기’ 에서 패턴마다 덮어씀");
            G("공통값과 덮어쓰기",
                "공통값 = 모든 패턴이 같이 쓰는 값(결투자 · 리듬 · 예고 탭)",
                "−1 = 공통값 사용. 패턴 탭에서 0 이상을 넣으면 그 패턴만 바뀜");
            G("단위",
                "U = 거리(1U ≈ 캐릭터 몸 반 개) · U/s = 초당 거리 · U/s² = 가속",
                "초 = 실제 시간 · 박 = 음악 박자(BPM 따라 길이가 바뀜) · 마디 = 4박",
                "배 = 곱하는 배율(1 = 그대로) · 0 ~ 1 = 확률 또는 비율 · (1 켬 · 0 끔) = 스위치",
                "± = 가운데에서 양쪽으로 · (다음 판) = 새 판부터 적용");
        }

        /// <summary>조작 키 탭: 동작마다 지금 키 · [바꾸기](이 키 하나로) · [추가] · [기본].</summary>
        /// <summary>소리 탭: 마스터 · 음악 · 효과음 음량(0 ~ 100%) · 예고음 켜기. 바꾸면 바로 들리고 잠시 뒤 설정에 저장.</summary>
        void SoundRows()
        {
            var st = KitRoot.Settings;
            var box = Section("음량", "50% = 기준 크기(기본) · 100% = 더 크게 · 0% = 소리 없음. 마스터 = 전체 · 음악 = 결투 곡 · 효과음 = 동작 드럼 · 타격 · 예고음. 작업실 ‘음악 끔’ 이면 음악은 음량과 상관없이 꺼져 있어요.");
            VolRow(box, "마스터 음량", () => st.VolMaster, v => st.VolMaster = v);
            VolRow(box, "음악 음량", () => st.VolMusic, v => st.VolMusic = v);
            VolRow(box, "효과음 음량", () => st.VolSfx, v => st.VolSfx = v);
            var atk = Section("공격 소리", "기본 공격 소리는 한 덩어리: 스네어(마법) → ‘마법 → 기계 지연’ 뒤 하이햇 ‘칫’(기계) — 마법 · 기계가 같이 써요. 50% = 기준 · 효과음 음량에 한 번 더 곱해요.");
            var tr = new VisualElement(); tr.AddToClassList("tp-foot"); atk.Add(tr);
            Button tb = null;
            string TbText() => st.AtkSoundOnPress ? "스네어: 누를 때마다(연타 소리)" : "스네어: 공격이 나갈 때";
            tb = new Button(() => { st.AtkSoundOnPress = !st.AtkSoundOnPress; tb.text = TbText(); soundDirty = true; Status(st.AtkSoundOnPress ? "공격 스네어 = 누를 때마다(예전 연타 소리)." : "공격 스네어 = 공격이 실제로 나갈 때(모션과 같은 순간)."); }) { text = TbText(), tooltip = "누를 때마다 = 예전처럼 연타한 만큼 스네어(공격이 아직 안 나가도) · 나갈 때 = 검기 모션과 같은 순간에만" };
            tb.AddToClassList("tp-btn"); tr.Add(tb);
            VolRow(atk, "마법 공격 소리", () => st.VolAtkMagic, v => st.VolAtkMagic = v);
            VolRow(atk, "기계 공격 소리", () => st.VolAtkMech, v => st.VolAtkMech = v);
            var files = Section("효과음 파일(바꿔 끼우기)", "아래 이름으로 .wav · .ogg · .mp3 를 이 폴더에 넣으면 코드로 만든 소리 대신 그 파일이 나와요. 넣거나 바꾼 뒤 ‘다시 읽기’.");
            files.Add(Lbl(SfxFiles.Dir, "tp-spec"));
            var fl = Lbl("", "tp-spec");
            files.Add(fl);
            void FileList() { fl.text = string.Join("  ·  ", SfxFiles.KEYS.Select(k => $"{k.key} ({k.name}) — {(SfxFiles.Has(k.key) ? "파일 사용 중" : "기본 소리")}")); }
            FileList();
            var fr = new VisualElement(); fr.AddToClassList("tp-foot"); files.Add(fr);
            Btn(fr, "폴더 열기", false, () => KitBackup.Open(SfxFiles.Dir));
            Btn(fr, "다시 읽기", false, () => SfxFiles.Reload(n => { FileList(); Status(n > 0 ? $"효과음 파일 {n}개를 읽었어요." : "효과음 파일이 없어요 — 기본 소리를 써요."); }));
            var cue = Section("예고음", "결투자 타격 1박 전마다 ‘딱’ — 카운터 가능한 근접은 낮게, 피해야 하는 공격은 높게(크기는 ‘리듬 · 예고’ 의 예고음 크기)");
            var row = new VisualElement(); row.AddToClassList("tp-foot"); cue.Add(row);
            Button cb = null;
            cb = new Button(() => { st.StageCue = !st.StageCue; cb.text = st.StageCue ? "예고음 켬" : "예고음 끔"; soundDirty = true; }) { text = st.StageCue ? "예고음 켬" : "예고음 끔" };
            cb.AddToClassList("tp-btn");
            row.Add(cb);
        }

        /// <summary>파일 · 백업 탭: 킷 데이터 폴더 · 자동 백업(프로젝트 밖) · MFS 로 내보내기(킷을 빼기 전에).</summary>
        void FilesRows()
        {
            var data = Section("킷 데이터", "그림 · 연출 카드 · 튜닝 · 차트 · 음악 · 설정이 모두 여기(Assets/MoriKit/Data~). 끝의 ~ 때문에 Unity 가 가져오지 않고 게임 빌드에도 안 들어가요. 킷을 업데이트할 때는 MoriKit 폴더를 지우지 말고 덮어쓰세요(지우면 이 폴더도 사라져요 — 그래도 백업은 남아요).");
            data.Add(Lbl(KitPaths.Root, "tp-spec"));
            var dr = new VisualElement(); dr.AddToClassList("tp-foot"); data.Add(dr);
            Btn(dr, "폴더 열기", false, () => KitBackup.Open(KitPaths.Root));

            var bk = Section("자동 백업(프로젝트 밖)", "파일을 덮어쓰기 · 지우기 직전에 그 파일을 백업 폴더에 날짜 · 시간을 붙여 남겨요(파일마다 최근 N개). 자주 저장되는 파일은 3분에 한 번만. 캔버스 크기 바꾸기 같은 되돌릴 수 없는 작업 직전엔 언제나. OneDrive · 구글 드라이브 같은 동기화 폴더로 바꾸면 PC 가 망가져도 남아요.");
            var path = Lbl(KitBackup.Dir, "tp-spec");
            bk.Add(path);
            var br = new VisualElement(); br.AddToClassList("tp-foot"); bk.Add(br);
            Btn(br, "백업 폴더 열기", false, () => KitBackup.Open(KitBackup.Dir));
            Btn(br, "위치 바꾸기…", false, () =>
            {
                string d = UnityEditor.EditorUtility.OpenFolderPanel("백업 폴더 고르기(프로젝트 밖 추천)", KitBackup.Dir, "");
                if (string.IsNullOrEmpty(d)) return;
                if (Path.GetFullPath(d).StartsWith(Path.GetFullPath(KitPaths.Root), StringComparison.OrdinalIgnoreCase)) { Status("킷 데이터 폴더 안은 안 돼요 — 밖에서 골라 주세요."); return; }
                KitBackup.Dir = d; path.text = KitBackup.Dir; Status("백업 위치: " + KitBackup.Dir);
            });
            Btn(br, "기본 위치로", false, () => { KitBackup.Dir = null; path.text = KitBackup.Dir; Status("백업 위치를 기본(문서/MoriKitBackup)으로."); });
            var kr = new VisualElement(); kr.AddToClassList("tp-foot"); bk.Add(kr);
            var keepL = Lbl($"파일마다 {KitBackup.Keep}개", "tp-name");
            Btn(kr, "−", false, () => { KitBackup.Keep = KitBackup.Keep - (KitBackup.Keep > 10 ? 5 : 1); keepL.text = $"파일마다 {KitBackup.Keep}개"; });
            kr.Add(keepL);
            Btn(kr, "+", false, () => { KitBackup.Keep = KitBackup.Keep + (KitBackup.Keep >= 10 ? 5 : 1); keepL.text = $"파일마다 {KitBackup.Keep}개"; });
            Btn(kr, "지금 전체 백업", true, () =>
            {
                try { string d = KitBackup.All(out int n); Status($"전체 백업: 파일 {n}개 → {d}(전체 백업은 최근 5개)"); }
                catch (Exception e) { Status("전체 백업 실패: " + e.Message); }
            });

            var ex = Section("MFS 로 내보내기(킷을 빼기 전에)", "게임이 쓸 것만 킷 밖으로. 그림 = 컷마다 스프라이트 PNG(PPU = 그림 크기 탭의 1U px · 도트 그대로 · 발밑 가운데) + 컷 길이 json. 연출 카드 = StreamingAssets/fx-cards(게임의 MoriFxPlayer 가 읽는 곳). 게임 쪽 파일은 덮어써요(연결 유지).");
            var gl = Lbl("그림 → " + KitExport.GameDir + "/Sprites", "tp-spec");
            ex.Add(gl);
            var er = new VisualElement(); er.AddToClassList("tp-foot"); ex.Add(er);
            Btn(er, "게임 쪽 폴더 바꾸기…", false, () => { if (KitExport.PickGameDir(out var m)) gl.text = "그림 → " + KitExport.GameDir + "/Sprites"; if (m != null) Status(m); });
            var er2 = new VisualElement(); er2.AddToClassList("tp-foot"); ex.Add(er2);
            Btn(er2, "그림 내보내기", false, () => Status(Try(KitExport.Sprites)));
            Btn(er2, "연출 카드 내보내기", false, () => Status(Try(KitExport.Cards)));
            Btn(er2, "모두 내보내기", true, () => Status(Try(KitExport.All)));
            var rt = Section("런타임을 킷 밖으로", "연출 재생기(MoriFx) 코드 · 셰이더를 Assets/MoriKit/Runtime → Assets/MoriFx 로 옮겨요(GUID 그대로라 연결이 안 끊겨요). 킷을 빼기 직전에 한 번. 옮긴 뒤에도 킷은 그대로 써요.");
            var rr = new VisualElement(); rr.AddToClassList("tp-foot"); rt.Add(rr);
            Btn(rr, KitExport.RuntimeInside ? "런타임 옮기기" : "이미 옮겼어요", false, () =>
                Dialog.Confirm("런타임을 킷 밖으로", "Assets/MoriKit/Runtime 을 Assets/MoriFx 로 옮길까요? 킷을 빼기 직전에 하는 작업이에요.", "옮기기", false, () => { Status(Try(KitExport.MoveRuntime)); ShowGroup(FILES_TAB); }));
        }

        static string Try(Func<string> f)
        {
            try { return f(); }
            catch (Exception e) { return "실패: " + e.Message; }
        }

        void VolRow(VisualElement parent, string name, Func<float> get, Action<float> set)
        {
            var row = new VisualElement(); row.AddToClassList("tp-row");
            parent.Add(row);
            var head = new VisualElement(); head.AddToClassList("tp-row-h"); row.Add(head);
            head.Add(Lbl(name, "tp-name"));
            var val = Lbl(Mathf.RoundToInt(get() * 100) + "%", "tp-name");
            head.Add(val);
            var ctl = new VisualElement(); ctl.AddToClassList("tp-ctl"); row.Add(ctl);
            var sl = new Slider(0, 100) { value = get() * 100 };
            sl.AddToClassList("tp-slider");
            sl.style.flexGrow = 1;
            sl.RegisterValueChangedCallback(e =>
            {
                set(Mathf.Clamp01(e.newValue / 100f));
                val.text = Mathf.RoundToInt(e.newValue) + "%";
                OnVolume?.Invoke();
                soundDirty = true; soundSaveT = 0.5f;
            });
            ctl.Add(sl);
        }

        void KeyRows()
        {
            var info = new VisualElement(); info.AddToClassList("tp-row");
            list.contentContainer.Add(info);
            info.Add(Lbl("‘바꾸기’ 또는 ‘추가’ 를 누른 뒤 쓸 키를 누르세요(Esc = 취소). 다른 동작에 있던 같은 키는 이쪽으로 옮겨 와요. 바꾸면 바로 저장되고, ‘내보내기’ 에도 함께 담겨요.", "tp-spec"));
            for (int i = 0; i < KeyBinds.COUNT; i++)
            {
                int slot = i;
                var row = new VisualElement(); row.AddToClassList("tp-row");
                list.contentContainer.Add(row);
                var head = new VisualElement(); head.AddToClassList("tp-row-h"); row.Add(head);
                head.Add(Lbl(KeyBinds.NAME[slot], "tp-name"));
                var keys = Lbl("", "tp-keys");
                head.Add(keys);
                var btns = new VisualElement(); btns.AddToClassList("tp-foot"); row.Add(btns);
                Btn(btns, "바꾸기", false, () => StartCapture(slot, true));
                Btn(btns, "추가", false, () => StartCapture(slot, false));
                Btn(btns, "기본", false, () => { KeyBinds.Reset(slot); RefreshAll(); Status($"‘{KeyBinds.NAME[slot]}’ 을 기본 키로 되돌렸어요."); });
                void Refresh()
                {
                    bool waiting = capSlot == slot;
                    keys.text = waiting ? "키를 누르세요…" : string.Join(" · ", KeyBinds.Get(slot).Select(KeyBinds.Pretty).DefaultIfEmpty("(없음)"));
                    keys.EnableInClassList("tp-keys--wait", waiting);
                    keys.EnableInClassList("tp-keys--changed", !waiting && KeyBinds.Changed(slot));
                }
                refresh.Add(Refresh);
                Refresh();
            }
        }

        void StartCapture(int slot, bool replace)
        {
            var f = el.panel?.focusController?.focusedElement as VisualElement;
            if (f != null) f.Blur();                 // 누른 버튼이 스페이스 · 엔터를 먹지 않게
            capSlot = slot;
            capReplace = replace;
            capAge = 0;
            Status($"‘{KeyBinds.NAME[slot]}’ 에 쓸 키를 누르세요 · Esc = 취소");
            RefreshAll();
        }

        static double Tidy(double v) => Math.Round(v, 6);

        DoubleField Num(string cls)
        {
            var f = new DoubleField { isDelayed = true };
            f.AddToClassList("tp-num");
            f.AddToClassList(cls);
            f.RegisterCallback<FocusInEvent>(_ => typing++);
            f.RegisterCallback<FocusOutEvent>(_ => typing = Math.Max(0, typing - 1));
            return f;
        }

        /// <summary>설명 앞에 붙던 ‘(자체)’ 는 지우고, 명세서 항목은 ‘명세:’ 로.</summary>
        static string SpecText(string sp)
        {
            sp = (sp ?? "").Trim();
            if (sp.StartsWith("(자체)")) sp = sp.Substring(4).Trim();
            return sp;
        }

        void Row(VisualElement parent, TuneItem it)
        {
            var row = new VisualElement(); row.AddToClassList("tp-row");
            parent.Add(row);
            var head = new VisualElement(); head.AddToClassList("tp-row-h"); row.Add(head);
            head.Add(Lbl(it.Name, "tp-name"));
            var val = Num("tp-num--val");
            head.Add(val);
            string sp = SpecText(it.Spec);
            row.Add(Lbl((sp.Length > 0 ? sp + "  · " : "") + (it.NextRound ? "다음 판  · " : "") + $"기본 {Tidy(it.Default).ToString(CultureInfo.InvariantCulture)}", "tp-spec"));
            var ctl = new VisualElement(); ctl.AddToClassList("tp-ctl"); row.Add(ctl);
            var (r0, r1) = TuneStore.Range(it);
            var minF = Num("tp-num--range");
            var maxF = Num("tp-num--range");
            var sl = new Slider((float)r0, (float)r1);
            sl.AddToClassList("tp-slider");
            double Lo() => TuneStore.Range(it).min;
            double Hi() => TuneStore.Range(it).max;
            void ApplyRange(double lo, double hi)
            {
                TuneStore.SetRange(it, lo, hi);
                sl.lowValue = (float)lo;
                sl.highValue = (float)hi;
            }
            void Refresh()
            {
                double v = it.Get();
                val.SetValueWithoutNotify(v);
                minF.SetValueWithoutNotify(Tidy(Lo()));
                maxF.SetValueWithoutNotify(Tidy(Hi()));
                sl.SetValueWithoutNotify((float)Math.Max(Lo(), Math.Min(Hi(), v)));
                val.EnableInClassList("tp-num--changed", TuneStore.Changed(it));
            }
            void Set(double v)
            {
                if (double.IsNaN(v) || double.IsInfinity(v)) { Refresh(); return; }
                v = Tidy(v);
                it.Set(v);
                // 슬라이더 구간 밖 값을 넣으면 구간을 넓혀 그 값까지 담는다(값은 자르지 않음)
                if (v < Lo()) ApplyRange(v, Hi());
                if (v > Hi()) ApplyRange(Lo(), v);
                Refresh();
            }
            val.RegisterValueChangedCallback(ev => Set(ev.newValue));
            minF.RegisterValueChangedCallback(ev =>
            {
                if (double.IsNaN(ev.newValue) || ev.newValue >= Hi()) { Status("최소값은 최대값보다 작아야 해요."); Refresh(); return; }
                ApplyRange(Tidy(ev.newValue), Hi());
                Refresh();
            });
            maxF.RegisterValueChangedCallback(ev =>
            {
                if (double.IsNaN(ev.newValue) || ev.newValue <= Lo()) { Status("최대값은 최소값보다 커야 해요."); Refresh(); return; }
                ApplyRange(Lo(), Tidy(ev.newValue));
                Refresh();
            });
            sl.RegisterValueChangedCallback(ev =>
            {
                double step = TuneStore.StepOf(Lo(), Hi());
                double v = Lo() + Math.Round((ev.newValue - Lo()) / step) * step;
                Set(Math.Max(Lo(), Math.Min(Hi(), v)));
            });
            var minus = new Button(() => Set(it.Get() - TuneStore.StepOf(Lo(), Hi()))) { text = "−" };
            var plus = new Button(() => Set(it.Get() + TuneStore.StepOf(Lo(), Hi()))) { text = "+" };
            minus.AddToClassList("tp-step");
            plus.AddToClassList("tp-step");
            ctl.Add(minF);
            ctl.Add(minus);
            ctl.Add(sl);
            ctl.Add(plus);
            ctl.Add(maxF);
            refresh.Add(() => { ApplyRange(Lo(), Hi()); Refresh(); });
            Refresh();
        }
    }
}
