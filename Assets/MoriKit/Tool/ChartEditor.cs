using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mori.Core.Rhythm;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 노트 에디터(Mori 에서 옮김 — 킷 작업실 ▸ 노트 에디터). ▶ 미리보기로 플레이 = 작업실 미리보기 결투를 이 곡 · 차트로 바로 시작. 내 음악 파일에 직접 노트를 찍는다.
    ///  · 음악: persistentDataPath/music 에 ogg · wav · mp3 (또는 mp4 · mov · webm 등 동영상 — 소리만 뽑아 씀)를 넣고 [새로고침] → 곡 고르기. 내장 합성 테스트 곡도 있음.
    ///  · 찍기: 레인 칸을 누르면 박자에 맞춰 노트 추가, 있는 노트를 누르면 지우기(오른쪽 버튼도 지우기).
    ///    Shift + 누르기 = 그 자리에 게이트(멈춤/계속 선택 지점) 넣기·빼기.
    ///  · 노트는 S · D · 스페이스(훅, 원) 세 가지. 엔터(피니시)는 게임에서 게이트의 선택을 확정하는 키라 노트로 찍지 않는다.
    ///  · 실시간 녹음: ● 녹음을 켜고 재생하면서 S · D · 스페이스를 누르면 그 순간에 노트가 찍힌다(박자 맞춤 적용).
    ///  · 키: P 재생/정지 · R 녹음 · ←/→ 한 칸 · ↑/↓ 한 박 · Delete 재생선 위 노트 지우기 · Ctrl+Z 되돌리기 · T 테스트 플레이
    ///    (재생을 멈춘 상태에서 S·D·스페이스 = 재생선 위치에 노트)
    ///  · 저장: persistentDataPath/charts/*.json. 이벤트 id 로 시작하는 이름(예: quad_witching-1.json)은 그 이벤트 때 쓰인다.
    /// </summary>
    public sealed class ChartEditor
    {
        readonly AudioHub audio;
        /// <summary>▶ 미리보기로 플레이: 이 차트(복사본)로 작업실 결투를 시작.</summary>
        public Action<RhythmChart> OnPlay;
        /// <summary>에디터를 닫음(작업실이 도트 편집기 · 미리보기를 되살림).</summary>
        public Action OnClosed;
        readonly VisualElement el, side;
        readonly EditorField tl;
        readonly DropdownField chartDd, audioDd;
        readonly TextField titleF, bpmF, offF, lenF;
        readonly Label info, msg, recBtnL;
        readonly Button playBtn, recBtn;
        readonly Dictionary<int, Button> snapBtns = new Dictionary<int, Button>();
        readonly Dictionary<int, Button> lvBtns = new Dictionary<int, Button>();
        // 자동 생성
        int genLevel = 2, genSeed = 1;
        bool genLink = true, genGates = true, genFollow = true, genActive, beatKnown;
        Button genLinkB, tapB;
        readonly Dictionary<int, Button> meterBtns = new Dictionary<int, Button>();
        readonly List<double> taps = new List<double>();
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        RhythmChart chart = new RhythmChart();
        string fileName;                 // null = 아직 저장 안 함(내장 테스트 차트를 열면 null)
        AudioClip clip;
        bool dirty, playing, metro = true, assist = true, fitLength, stepAdvance = true;
        readonly HashSet<KeyCode> held = new HashSet<KeyCode>();   // 누르고 있는 키(키 반복으로 노트가 여러 개 찍히지 않게)
        bool stepPending;
        int stepMask;                    // 이번 ‘한 칸’ 에 이미 찍은 레인(같은 레인을 또 누르면 먼저 한 칸 가고 찍는다)
        float stepWait;
        readonly List<Keys.Tap> tapBuf = new List<Keys.Tap>();
        Label modeL;
        double playDsp, lastT;
        readonly List<string> undo = new List<string>();
        bool dragScroll;
        float dragY;
        double dragT;

        public bool IsOpen { get; private set; }

        public ChartEditor(AudioHub audio, VisualElement root)
        {
            this.audio = audio;
            el = E(root, "ed");
            // 위쪽 막대 1: 차트
            var top = E(el, "ed-top");
            L(top, "NOTE EDITOR", "ed-t");
            L(top, "차트", "ed-lbl");
            chartDd = new DropdownField();
            chartDd.AddToClassList("dd");
            chartDd.style.minWidth = 220;
            chartDd.RegisterValueChangedCallback(e => { if (e.newValue != null && e.newValue != e.previousValue) AskOpen(e.newValue); });
            top.Add(chartDd);
            B(top, "새 차트", "", NewChart);
            B(top, "저장", "mb--gold", () => Save(false));
            B(top, "다른 이름으로", "", () => Save(true));
            B(top, "▶ 미리보기로 플레이 (T)", "mb--gold", TestPlay).tooltip = "이 곡 · 차트로 작업실 미리보기 결투를 바로 시작(결투자 패턴 · 맵 패턴 · 예고음이 이 박자에 맞춰 움직여요)";
            B(top, "닫기", "mb--ghost", () => Back());
            // 위쪽 막대 2: 곡 · 속도
            var top2 = E(el, "ed-top");
            L(top2, "곡", "ed-lbl");
            audioDd = new DropdownField();
            audioDd.AddToClassList("dd");
            audioDd.style.minWidth = 220;
            audioDd.RegisterValueChangedCallback(e => { if (e.newValue != null && e.newValue != e.previousValue) SetAudio(e.newValue); });
            top2.Add(audioDd);
            B(top2, "새로고침", "mb--ghost", RefreshLists);
            B(top2, "음악 폴더 열기", "mb--ghost", () => OpenFolder(AudioHub.MusicDir));
            L(top2, "제목", "ed-lbl");
            titleF = Field(top2, "ed-in ed-in--wide", v => { chart.Title = v; Dirty(); });
            L(top2, "BPM", "ed-lbl");
            bpmF = Field(top2, "ed-in", v => { if (double.TryParse(v, NumberStyles.Float, Ci, out var b) && b >= 30 && b <= 400 && Math.Abs(b - chart.Bpm) > 1e-6) { chart.Bpm = b; beatKnown = true; Dirty(); Relink("BPM"); } });
            L(top2, "시작 보정 ms", "ed-lbl");
            offF = Field(top2, "ed-in", v => { if (double.TryParse(v, NumberStyles.Float, Ci, out var o) && Math.Abs(o) < 20000 && Math.Abs(o - chart.OffsetMs) > 1e-6) { chart.OffsetMs = o; Dirty(); Relink("시작 보정"); } });
            L(top2, "길이 초", "ed-lbl");
            lenF = Field(top2, "ed-in", v => { if (double.TryParse(v, NumberStyles.Float, Ci, out var l) && l > 1) { chart.Length = l; Dirty(); } });

            var main = E(el, "ed-main");
            tl = new EditorField();
            tl.AddToClassList("ed-tl");
            main.Add(tl);
            tl.RegisterCallback<PointerDownEvent>(e => { held.Clear(); el.Focus(); }, TrickleDown.TrickleDown);
            tl.RegisterCallback<PointerDownEvent>(OnDown);
            modeL = new Label("") { pickingMode = PickingMode.Ignore };
            modeL.AddToClassList("ed-mode");
            tl.Add(modeL);
            tl.RegisterCallback<PointerMoveEvent>(OnMove);
            tl.RegisterCallback<PointerUpEvent>(e => { dragScroll = false; tl.ReleasePointer(e.pointerId); });
            tl.RegisterCallback<PointerLeaveEvent>(_ => tl.HoverLane = -1);
            tl.RegisterCallback<WheelEvent>(OnWheel);

            // 오른쪽 도구 칸: 스크롤(칸이 넘쳐도 줄어들어 겹치지 않게 — 항목은 줄어들지 않음)
            var sideSv = UiKit.Scroll("ed-side", "ed-side-c");
            main.Add(sideSv);
            side = sideSv.contentContainer;
            L(side, "TRANSPORT", "ed-h");
            var tr = E(side, "ed-row");
            B(tr, "⏮ 처음", "", () => { StopPlay(); tl.ViewT = 0; }).tooltip = "처음으로";
            playBtn = B(tr, "▶ 재생 (P)", "mb--gold", TogglePlay);
            recBtn = B(tr, "● 녹음 (R)", "", ToggleRec);
            recBtnL = L(side, "녹음을 켜고 재생하며 S · D · 스페이스를 누르면 그 순간에 찍혀요.", "ed-p");
            L(side, "KEYBOARD", "ed-h");
            L(side, "멈춘 상태: S · D · 스페이스 = 재생선 자리에 노트(있으면 지움)\n● 녹음(R): 곡을 틀고 누르는 순간에 노트\n▶ 재생(P): 키는 소리만(연습)", "ed-p");
            var kr = E(side, "ed-row");
            Button sa = null;
            sa = B(kr, "찍고 한 칸 앞으로: 켬", "", () => { stepAdvance = !stepAdvance; sa.text = stepAdvance ? "찍고 한 칸 앞으로: 켬" : "찍고 한 칸 앞으로: 끔"; });
            sa.tooltip = "멈춘 상태에서 노트를 찍으면 재생선이 박자 한 칸(SNAP) 앞으로 간다. 두 키를 거의 동시에 누르면 같은 자리에 찍힌다.";
            L(side, "SNAP — 박자 맞춤", "ed-h");
            var sn = E(side, "st-seg ed-seg");
            foreach (var (d, n) in new[] { (1, "1/1"), (2, "1/2"), (4, "1/4"), (8, "1/8"), (16, "1/16"), (0, "자유") })
            {
                int dd = d;
                var b = new Button(() => { bool ch = tl.SnapDiv != dd; tl.SnapDiv = dd; RefreshSnap(); if (ch) Relink("박자"); }) { text = n };
                b.AddToClassList("st-sb");
                sn.Add(b);
                snapBtns[dd] = b;
            }
            L(side, "BEAT — BPM · 박자", "ed-h");
            L(side, "노트가 곡과 맞으려면 BPM(빠르기) · 박자표(한 마디 박 수) · 시작 보정(첫 마디 첫 박 위치)이 곡과 같아야 해요. 메트로놈을 켜고 재생해서 ‘딱딱’ 이 곡의 킥과 겹치면 맞은 거예요.", "ed-p");
            var br1 = E(side, "ed-row");
            B(br1, "−1", "", () => SetBpm(chart.Bpm - 1)).tooltip = "BPM −1";
            B(br1, "+1", "", () => SetBpm(chart.Bpm + 1)).tooltip = "BPM +1";
            B(br1, "−0.1", "", () => SetBpm(chart.Bpm - 0.1));
            B(br1, "+0.1", "", () => SetBpm(chart.Bpm + 0.1));
            B(br1, "½", "", () => SetBpm(chart.Bpm / 2)).tooltip = "두 배로 빠르게 잡혔을 때";
            B(br1, "×2", "", () => SetBpm(chart.Bpm * 2)).tooltip = "절반으로 느리게 잡혔을 때";
            var br2 = E(side, "ed-row");
            tapB = B(br2, "👆 탭 템포", "", Tap);
            tapB.tooltip = "곡을 들으며 박에 맞춰 여러 번 누르면 그 빠르기로 BPM 을 맞춰요(4번부터 반영 · 2초 쉬면 새로).";
            B(br2, "📍 재생선 = 마디 첫 박", "", DownbeatHere).tooltip = "재생선을 곡의 마디 첫 박(보통 킥 + 크래시 자리)에 두고 누르면 시작 보정을 거기에 맞춰요.";
            L(side, "박자표 (한 마디 박 수)", "ed-p");
            var mSeg = E(side, "st-seg ed-seg");
            foreach (var (mv, mn) in new[] { (4, "4/4"), (3, "3/4"), (2, "2/4"), (6, "6/8") })
            {
                int m2 = mv;
                var b = new Button(() => { if (chart.BeatsPerBar == m2) return; Snapshot(); chart.Meter = m2; Dirty(); RefreshMeter(); Relink("박자표"); }) { text = mn };
                b.AddToClassList("st-sb");
                mSeg.Add(b);
                meterBtns[m2] = b;
            }
            var gr0 = E(side, "ed-row");
            B(gr0, "🎯 BPM · 첫 박 찾기", "", () => DetectBeat(false)).tooltip = "곡의 박을 들어 보고 BPM 과 시작 보정(첫 마디 첫 박)을 맞춰요. 박이 뚜렷한 곡일수록 정확해요.";
            L(side, "AUTO — 자동 노트 생성", "ed-h");
            L(side, "드러머처럼: 곡에서 킥이 울린 자리에 S, 스네어 자리에 D, 크래시 · 프레이즈 첫 박에 스페이스. 드럼이 잘 안 들리는 마디는 정박 드럼 패턴(4마디 = 그루브 셋 + 필인)으로 채워요. 조용하면 4분음표, 커지면 8분. SNAP 1/1 = 4분음표만 · 1/2 = 8분 · 1/4 이상 = 아주 센 자리만 16분. 위 BEAT 가 맞아야 박이 맞아요(처음 자동 생성할 때 모르면 알아서 찾아요).", "ed-p");
            var lvSeg = E(side, "st-seg ed-seg");
            foreach (var (lvv, ln) in new[] { (1, "쉬움"), (2, "보통"), (3, "어려움") })
            {
                int l2 = lvv;
                var b = new Button(() => { bool ch = genLevel != l2; genLevel = l2; RefreshGen(); if (ch) Relink("난이도"); }) { text = ln };
                b.AddToClassList("st-sb");
                lvSeg.Add(b);
                lvBtns[l2] = b;
            }
            var gr1 = E(side, "ed-row");
            B(gr1, "⚡ 자동 생성", "mb--gold", () => AutoGen(false)).tooltip = "지금 노트·게이트를 지우고 새로 만들어요(Ctrl+Z 로 되돌리기)";
            B(gr1, "🎲 다시 뽑기", "", () => AutoGen(true)).tooltip = "같은 설정으로 다른 패턴";
            var gr2 = E(side, "ed-row");
            genLinkB = B(gr2, "", "", () => { genLink = !genLink; RefreshGen(); });
            genLinkB.tooltip = "켜 두면 자동 생성한 차트는 SNAP 박자 · 난이도 · BPM · 시작 보정을 바꿀 때마다 다시 만들어져요(그 뒤에 손으로 고친 건 덮여요).";
            Button gg = null;
            gg = B(gr2, "게이트도 자동: 켬", "", () => { genGates = !genGates; gg.text = genGates ? "게이트도 자동: 켬" : "게이트도 자동: 끔"; });
            Button fb = null;
            fb = B(gr2, "곡의 드럼 따라 치기: 켬", "", () => { genFollow = !genFollow; fb.text = genFollow ? "곡의 드럼 따라 치기: 켬" : "곡의 드럼 따라 치기: 끔"; Relink("드럼 따라 치기"); });
            fb.tooltip = "켜면 곡에서 킥(저음)이 울린 자리에 S, 스네어(고음) 자리에 D 를 놓아요. 끄면 정해진 드럼 패턴만으로(테스트 차트 느낌).";
            L(side, "VIEW", "ed-h");
            var zr = E(side, "ed-row");
            B(zr, "확대 +", "", () => tl.Pps = Mathf.Min(1200, tl.Pps * 1.25f));
            B(zr, "축소 −", "", () => tl.Pps = Mathf.Max(60, tl.Pps / 1.25f));
            var tg = E(side, "ed-row");
            Button mb = null, ab = null;
            mb = B(tg, "메트로놈 켬", "", () => { metro = !metro; mb.text = metro ? "메트로놈 켬" : "메트로놈 끔"; });
            ab = B(tg, "노트 소리 켬", "", () => { assist = !assist; ab.text = assist ? "노트 소리 켬" : "노트 소리 끔"; });
            L(side, "GATE", "ed-h");
            var gr = E(side, "ed-row");
            B(gr, "재생선 위치에 게이트 넣기/빼기", "", () => ToggleGate(tl.SnapT(tl.ViewT)));
            L(side, "INFO", "ed-h");
            info = L(side, "", "ed-v");
            msg = L(side, "", "ed-msg");
            L(side, "HOW TO", "ed-h");
            L(side, "· 레인 클릭 = 노트 넣기\n· 노트 클릭 / 오른쪽 클릭 = 지우기\n· Shift + 클릭 = 게이트\n· 휠 = 위아래 이동 · Ctrl + 휠 = 확대\n· 왼쪽 눈금·음파 칸을 끌어도 이동", "ed-p");
            L(side, "키: S · D · 스페이스 노트 · P 재생 · R 녹음 · ←/→ 한 칸 · ↑/↓ 한 박 · Home 처음 · G 게이트 · Delete/Backspace 재생선 위 노트 지우기 · Ctrl+Z 되돌리기 · T 테스트", "ed-p");
            L(side, "게이트로 곡을 구간으로 나누면, 게임에서 구간이 끝날 때마다 화면이 느려지며 ‘멈춘다 / 더 강하게’ 를 묻습니다(엔터로 확정). 게이트는 구간 마지막 노트 조금 뒤, 마디가 바뀌는 자리에 두세요.", "ed-p");
            el.style.display = DisplayStyle.None;
            // S · D · 스페이스 · 엔터 · 화살표가 마지막으로 누른 버튼을 다시 누르거나 포커스를 옮기지 않게(글자 칸은 예외)
            el.focusable = true;
            el.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            el.RegisterCallback<KeyUpEvent>(e => held.Remove(e.keyCode), TrickleDown.TrickleDown);
            // 포커스가 빠지면 뗀 키(KeyUp)를 못 받아 ‘누르고 있음’ 이 남는다 → 다음 누름이 무시되던 원인. 비워 둔다.
            el.RegisterCallback<FocusOutEvent>(_ => held.Clear(), TrickleDown.TrickleDown);
            el.RegisterCallback<FocusInEvent>(_ => held.Clear(), TrickleDown.TrickleDown);
            el.RegisterCallback<NavigationSubmitEvent>(e => { if (!InText(e.target)) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            el.RegisterCallback<NavigationMoveEvent>(e => { if (!InText(e.target)) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
        }

        static bool InText(IEventHandler t)
        {
            for (var v = t as VisualElement; v != null; v = v.parent) if (v is TextField) return true;
            return false;
        }

        // ───────── 도우미 ─────────
        static VisualElement E(VisualElement p, string cls)
        {
            var e = new VisualElement();
            if (cls != null) foreach (var c in cls.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
            p.Add(e);
            return e;
        }

        static Label L(VisualElement p, string t, string cls)
        {
            var l = new Label(t);
            if (cls != null) foreach (var c in cls.Split(' ')) if (c.Length > 0) l.AddToClassList(c);
            p.Add(l);
            return l;
        }

        static Button B(VisualElement p, string t, string cls, Action a)
        {
            var b = new Button(a) { text = t };
            foreach (var c in ("mb " + cls).Split(' ')) if (c.Length > 0) b.AddToClassList(c);
            p.Add(b);
            return b;
        }

        static TextField Field(VisualElement p, string cls, Action<string> changed)
        {
            var f = new TextField();
            foreach (var c in cls.Split(' ')) if (c.Length > 0) f.AddToClassList(c);
            f.RegisterCallback<FocusOutEvent>(_ => changed(f.value));
            f.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) changed(f.value); });
            p.Add(f);
            return f;
        }

        void Msg(string t, bool bad = false)
        {
            msg.text = t;
            msg.EnableInClassList("ed-msg--bad", bad);
        }

        void Dirty() { dirty = true; Info(); }

        void Snapshot()
        {
            undo.Add(chart.ToJson());
            if (undo.Count > 60) undo.RemoveAt(0);
        }

        void Undo()
        {
            if (undo.Count == 0) { Msg("되돌릴 것이 없어요"); return; }
            string j = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            chart = RhythmChart.FromJson(j);
            tl.Chart = chart;
            Fields();
            Dirty();
            Msg("되돌렸어요");
        }

        bool Typing()
        {
            var f = el.panel?.focusController?.focusedElement as VisualElement;
            for (var v = f; v != null; v = v.parent) if (v is TextField) return true;
            return false;
        }

        // ───────── 열기 · 닫기 ─────────
        public void Show()
        {
            IsOpen = true;
            el.style.display = DisplayStyle.Flex;
            el.BringToFront();
            RefreshLists();
            if (chart.Notes.Count == 0 && fileName == null)
            {
                // 마지막에 쓴 차트(없으면 내장 테스트 차트)
                string last = KitRoot.Settings.RhythmChart;
                Open(!string.IsNullOrEmpty(last) && ChartStore.List().Contains(last) ? last : ChartStore.TEST);
            }
            RefreshSnap();
            RefreshGen();
            Info();
            held.Clear();
            Keys.ClearTaps();
            el.schedule.Execute(() => { el.panel?.focusController?.focusedElement?.Blur(); el.Focus(); }).StartingIn(30);
        }

        public void Hide()
        {
            if (!IsOpen) return;
            StopPlay();
            IsOpen = false;
            el.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        /// <summary>Esc: 재생 중이면 정지, 아니면 (저장 안 한 변경이 있으면 물어보고) 닫기.</summary>
        public void Back()
        {
            if (playing) { StopPlay(); return; }
            if (dirty) Dialog.Confirm("저장하지 않은 변경이 있어요", "닫으면 마지막으로 저장한 뒤의 변경이 사라져요.", "저장 안 하고 닫기", true, () => { dirty = false; Hide(); });
            else Hide();
        }

        void RefreshLists()
        {
            var charts = new List<string> { ChartStore.TEST };
            charts.AddRange(ChartStore.List());
            chartDd.choices = charts;
            chartDd.SetValueWithoutNotify(fileName ?? (charts.Contains(chartDd.value) ? chartDd.value : ChartStore.TEST));
            var auds = new List<string> { "합성 테스트 곡" };
            auds.AddRange(AudioHub.MusicFiles());
            audioDd.choices = auds;
            audioDd.SetValueWithoutNotify(AudioName(chart.Audio));
        }

        static string AudioName(string a) => string.IsNullOrEmpty(a) || a.StartsWith("synth:") ? "합성 테스트 곡" : a;

        void AskOpen(string name)
        {
            if (dirty) Dialog.Confirm("다른 차트를 열까요?", "저장하지 않은 변경이 사라져요.", "열기", true, () => Open(name), "취소", () => chartDd.SetValueWithoutNotify(fileName ?? ChartStore.TEST));
            else Open(name);
        }

        void Open(string name)
        {
            StopPlay();
            try
            {
                chart = ChartStore.Load(name);
                int fin = chart.Notes.RemoveAll(n => n.K == Lane.FINISH);   // 예전 차트의 피니시 노트는 쓰지 않는다
                fileName = name == ChartStore.TEST ? null : name;
                chartDd.SetValueWithoutNotify(name);
                Remember(name);
                undo.Clear();
                dirty = false;
                genActive = false;
                beatKnown = true;      // 저장된 차트는 BPM 을 맞춰 둔 것으로 본다
                tl.Chart = chart;
                tl.ViewT = 0;
                Fields();
                LoadAudio();
                Msg((name == ChartStore.TEST ? "내장 테스트 차트를 열었어요. 고친 뒤 ‘다른 이름으로’ 저장하세요." : $"‘{name}’ 을 열었어요.")
                    + (fin > 0 ? $" (피니시 노트 {fin}개는 이제 쓰지 않아 뺐어요)" : ""));
                if (fin > 0 && fileName != null) dirty = true;
            }
            catch (Exception e) { Msg("차트를 열지 못했어요: " + e.Message, true); }
            Info();
        }

        /// <summary>마지막에 쓴 차트 — 다음에 킷을 켜도 작업실 미리보기 · 에디터가 이 차트로.</summary>
        static void Remember(string name)
        {
            if (string.IsNullOrEmpty(name) || KitRoot.Settings.RhythmChart == name) return;
            KitRoot.Settings.RhythmChart = name;
            KitRoot.Settings.Save();
        }

        void NewChart()
        {
            Action go = () =>
            {
                StopPlay();
                string aud = chart.Audio;
                chart = new RhythmChart { Title = "새 차트", Audio = aud, Bpm = 120, Length = clip != null ? Math.Round(clip.length, 2) : 60 };
                fileName = null;
                undo.Clear();
                dirty = false;
                genActive = false;
                tl.Chart = chart;
                tl.ViewT = 0;
                Fields();
                chartDd.SetValueWithoutNotify(ChartStore.TEST);
                Msg("새 차트예요. 곡의 BPM 을 맞추고 노트를 찍거나 ⚡ 자동 생성을 눌러 보세요.");
                RefreshGen();
                Info();
            };
            if (dirty) Dialog.Confirm("새 차트를 만들까요?", "저장하지 않은 변경이 사라져요.", "새로 만들기", true, go);
            else go();
        }

        void Fields()
        {
            RefreshMeter();
            titleF.SetValueWithoutNotify(chart.Title);
            bpmF.SetValueWithoutNotify(chart.Bpm.ToString("0.###", Ci));
            offF.SetValueWithoutNotify(chart.OffsetMs.ToString("0.#", Ci));
            lenF.SetValueWithoutNotify(chart.Length.ToString("0.##", Ci));
            audioDd.SetValueWithoutNotify(AudioName(chart.Audio));
        }

        void Save(bool asNew)
        {
            if (fileName == null || asNew)
            {
                Dialog.Prompt("차트 저장", "파일 이름을 적어 저장해요. 작업실 미리보기 곡 목록에도 나와요.",
                    fileName != null ? Path.GetFileNameWithoutExtension(fileName) : "my-chart", "저장", n => DoSave(n), v => v.Length == 0 ? "이름을 적어 주세요." : null, 40);
                return;
            }
            DoSave(fileName);
        }

        void DoSave(string name)
        {
            try
            {
                fileName = ChartStore.Save(name, chart);
                dirty = false;
                Remember(fileName);
                RefreshLists();
                chartDd.SetValueWithoutNotify(fileName);
                Msg($"💾 저장했어요: {fileName}");
            }
            catch (Exception e) { Msg("저장하지 못했어요: " + e.Message, true); }
            Info();
        }

        void SetAudio(string name)
        {
            StopPlay();
            Snapshot();
            chart.Audio = name == "합성 테스트 곡" ? "synth:test" : name;
            fitLength = true;
            beatKnown = chart.Audio == "synth:test";
            if (chart.Audio == "synth:test") { chart.Bpm = Synth.TEST_BPM; chart.OffsetMs = 0; }
            Dirty();
            Fields();
            LoadAudio();
        }

        void LoadAudio()
        {
            clip = null;
            tl.Wave = null;
            waveLow = waveHigh = null;
            Msg("곡을 불러오는 중…");
            string want = chart.Audio;
            AudioHub.Load(want, (c, err) =>
            {
                if (chart.Audio != want) return;
                if (c == null) { Msg(err, true); return; }
                clip = c;
                if (fitLength || chart.Length < 1 || (chart.Notes.Count == 0 && Math.Abs(chart.Length - 60) < 0.01)) { chart.Length = Math.Round(Math.Max(c.length, chart.LastNoteT + 1), 2); Fields(); Info(); }
                fitLength = false;
                tl.Wave = Envelope(c, out waveLow, out waveHigh);
                Msg($"곡: {c.name} · {c.length:0.0}초");
                Info();
            });
        }

        /// <summary>10ms 마다 최대 진폭(음파 그림용). 1초씩 읽어서 메모리를 아낀다.</summary>
        float[] waveLow, waveHigh;      // 저음(킥 · 베이스) · 고음(스네어 · 하이햇) 세기 — 자동 생성이 곡의 드럼을 따라 칠 때

        /// <summary>
        /// 10ms 마다 최대 진폭(음파 그림 · 자동 생성용) + 저음(150 Hz 아래) · 고음(2.5 kHz 위) 세기. 1초씩 읽어서 메모리를 아낀다.
        /// </summary>
        static float[] Envelope(AudioClip c, out float[] low, out float[] high)
        {
            low = high = null;
            try
            {
                int ch = Math.Max(1, c.channels), fr = c.frequency, n = c.samples;
                int per = Math.Max(1, (int)(fr * EditorField.WAVE_DT));
                var env = new float[n / per + 1];
                var lo = new float[env.Length];
                var hi = new float[env.Length];
                var buf = new float[fr * ch];
                // 한 극 저역 통과 필터 두 개: 150 Hz(킥) · 2500 Hz(그 위 = 스네어 · 하이햇)
                float aL = 1 - Mathf.Exp(-2 * Mathf.PI * 150f / fr), aH = 1 - Mathf.Exp(-2 * Mathf.PI * 2500f / fr);
                float lpL = 0, lpL2 = 0, lpH = 0;
                for (int off = 0; off < n; off += fr)
                {
                    int len = Math.Min(fr, n - off);
                    if (len <= 0) break;
                    if (len < fr) buf = new float[len * ch];
                    if (!c.GetData(buf, off)) return null;
                    for (int i = 0; i < len; i++)
                    {
                        float m = 0, x = 0;
                        for (int k = 0; k < ch; k++) { float v = buf[i * ch + k]; x += v; m = Mathf.Max(m, Mathf.Abs(v)); }
                        x /= ch;
                        lpL += aL * (x - lpL);
                        lpL2 += aL * (lpL - lpL2);          // 두 번 걸러 더 가파르게
                        lpH += aH * (x - lpH);
                        float h = x - lpH;
                        int e = (off + i) / per;
                        if (e >= env.Length) continue;
                        if (m > env[e]) env[e] = m;
                        float al = Mathf.Abs(lpL2), ah = Mathf.Abs(h);
                        if (al > lo[e]) lo[e] = al;
                        if (ah > hi[e]) hi[e] = ah;
                    }
                }
                Norm(env); Norm(lo); Norm(hi);
                low = lo;
                high = hi;
                return env;
            }
            catch (Exception) { return null; }
        }

        /// <summary>가장 큰 값으로 나눈 뒤 제곱근(작은 소리도 보이게).</summary>
        static void Norm(float[] a)
        {
            float peak = 0;
            foreach (var v in a) if (v > peak) peak = v;
            if (peak > 0) for (int i = 0; i < a.Length; i++) a[i] = Mathf.Sqrt(a[i] / peak);
        }

        static void OpenFolder(string path)
        {
            try { Directory.CreateDirectory(path); Application.OpenURL("file:///" + path.Replace('\\', '/')); }
            catch (Exception e) { Dialog.Info("폴더를 열지 못했어요", e.Message); }
        }

        void RefreshMeter()
        {
            foreach (var kv in meterBtns) kv.Value.EnableInClassList("st-sb--on", kv.Key == chart.BeatsPerBar);
        }

        void SetBpm(double b)
        {
            b = Math.Round(Math.Max(30, Math.Min(400, b)), 2);
            if (Math.Abs(b - chart.Bpm) < 1e-9) return;
            Snapshot();
            chart.Bpm = b;
            beatKnown = true;
            Fields();
            Dirty();
            Msg($"BPM {b:0.##}");
            Relink("BPM");
        }

        /// <summary>탭 템포: 누른 간격의 평균(최근 8번)으로 BPM.</summary>
        void Tap()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (taps.Count > 0 && now - taps[taps.Count - 1] > 2) taps.Clear();
            taps.Add(now);
            if (taps.Count > 9) taps.RemoveAt(0);
            audio.Play(Synth.SfxTick, 0.5f, 1.2f);
            if (taps.Count < 4) { tapB.text = $"👆 탭 템포 ({taps.Count}/4)"; return; }
            double avg = (taps[taps.Count - 1] - taps[0]) / (taps.Count - 1);
            tapB.text = $"👆 탭 템포 ({60 / avg:0.0})";
            chart.Bpm = Math.Round(60 / avg, 1);
            beatKnown = true;
            Fields();
            Dirty();
            Msg($"탭 템포 {chart.Bpm:0.0} BPM — 다 맞췄으면 📍 로 첫 박을 맞추거나 ⚡ 자동 생성");
        }

        /// <summary>재생선 자리를 마디 첫 박으로: 시작 보정 = 재생선 시각을 한 마디로 나눈 나머지.</summary>
        void DownbeatHere()
        {
            double t = CurT(), bar = chart.Bar;
            double offS = t % bar;
            if (offS < 0) offS += bar;
            Snapshot();
            chart.OffsetMs = Math.Round(offS * 1000);
            beatKnown = true;
            Fields();
            Dirty();
            Msg($"📍 {t:0.000}초를 마디 첫 박으로 — 시작 보정 {chart.OffsetMs:0} ms");
            Relink("시작 보정");
        }

        void RefreshGen()
        {
            foreach (var kv in lvBtns) kv.Value.EnableInClassList("st-sb--on", kv.Key == genLevel);
            genLinkB.text = genLink ? "박자 바꾸면 다시 생성: 켬" : "박자 바꾸면 다시 생성: 끔";
            genLinkB.EnableInClassList("ed-on", genLink && genActive);
        }

        /// <summary>BPM · 박자 · 난이도가 바뀜 → 자동 생성한 차트면 다시 만든다.</summary>
        void Relink(string what)
        {
            if (!genActive || !genLink) return;
            AutoGen(false);
            Msg($"{what} 이(가) 바뀌어 노트를 다시 만들었어요 (Ctrl+Z 로 되돌리기)");
        }

        /// <summary>곡의 BPM · 첫 박을 찾아 맞춘다. silent 면 자동 생성 앞에서 조용히(결과만 알림).</summary>
        bool DetectBeat(bool silent)
        {
            if (tl.Wave == null) { if (!silent) Msg("곡을 아직 못 불러왔어요", true); return false; }
            if (!BeatDetect.Estimate(tl.Wave, EditorField.WAVE_DT, out double bpm, out double offS, out double conf, chart.BeatsPerBar, waveLow))
            {
                if (!silent) Msg("박을 찾지 못했어요 — 박이 뚜렷하지 않은 곡이면 BPM 을 직접 적어 주세요", true);
                return false;
            }
            StopPlay();
            if (!silent) Snapshot();
            chart.Bpm = bpm;
            chart.OffsetMs = Math.Round(offS * 1000);
            beatKnown = true;
            Fields();
            Dirty();
            string c = conf > 0.5 ? "확실해요" : conf > 0.2 ? "아마 맞아요" : "확실하지 않아요 — 메트로놈을 켜고 들어 보세요";
            Msg($"🎯 {bpm:0.#} BPM · 첫 박 {offS:0.000}초 ({c}). 두 배·절반으로 들리면 BPM 칸을 고치세요.");
            if (!silent) Relink("BPM");
            return true;
        }

        void AutoGen(bool reseed)
        {
            StopPlay();
            if (reseed) genSeed = UnityEngine.Random.Range(1, 1000000);
            Snapshot();
            string found = null;
            if (!beatKnown && DetectBeat(true)) found = $"BPM {chart.Bpm:0.#} · 시작 보정 {chart.OffsetMs:0} ms 를 찾아 맞췄어요. ";
            var o = new ChartGen.Options
            {
                Div = tl.SnapDiv > 0 ? tl.SnapDiv : 2, Level = genLevel, Seed = genSeed,
                Env = tl.Wave, Low = waveLow, High = waveHigh, EnvDt = EditorField.WAVE_DT, AudioLen = clip != null ? clip.length : 0, MakeGates = genGates, Follow = genFollow
            };
            int n = ChartGen.Generate(chart, o);
            if (n == 0) { undo.RemoveAt(undo.Count - 1); Msg("곡이 너무 짧거나 소리를 찾지 못해 만들지 못했어요", true); return; }
            genActive = true;
            Dirty();
            RefreshGen();
            string grid = tl.SnapDiv == 1 ? "1/1 4분음표만" : tl.SnapDiv == 2 || tl.SnapDiv <= 0 ? "1/2 8분까지" : $"1/{tl.SnapDiv} 필인에 16분";
            Msg(found + $"⚡ 자동 생성: 노트 {n}개 · 게이트 {chart.Gates.Count}개 ({grid} 박자 격자 · {(genLevel == 1 ? "쉬움" : genLevel == 2 ? "보통" : "어려움")}{(tl.Wave == null ? " · 곡을 못 읽어 박자만으로" : "")}). 마음대로 고치세요.");
        }

        void RefreshSnap()
        {
            foreach (var kv in snapBtns) kv.Value.EnableInClassList("st-sb--on", kv.Key == tl.SnapDiv);
        }

        void Info()
        {
            int[] cnt = new int[4];
            foreach (var n in chart.Notes) if (n.K >= 0 && n.K < 4) cnt[n.K]++;
            info.text = $"{chart.Title}{(dirty ? " · 저장 안 됨" : "")}\n" +
                        $"노트 {chart.Notes.Count}개 — S {cnt[Lane.S]} · D {cnt[Lane.D]} · 스페이스 {cnt[Lane.HOOK]}\n" +
                        $"게이트 {chart.Gates.Count}개 → 구간 {chart.PhaseCount}개\n" + PhaseList() +
                        $"{chart.Bpm:0.###} BPM · 길이 {chart.Length:0.0}초 · 파일 {(fileName ?? "(저장 안 함)")}";
        }

        /// <summary>구간별 시간 · 노트 수(게이트가 곡을 어떻게 나눴는지).</summary>
        string PhaseList()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < chart.PhaseCount; i++)
            {
                double a = chart.PhaseStart(i), b = chart.PhaseEnd(i);
                int n = chart.Notes.Count(x => x.K != Lane.FINISH && chart.PhaseOf(x.T) == i);
                sb.Append($"  PHASE {i + 1}: {a:0.0}~{b:0.0}초 · 노트 {n}개{(n == 0 ? " ⚠ 비어 있음" : "")}\n");
            }
            if (chart.Gates.Count == 0) sb.Append("  ※ 게이트가 없으면 곡 끝에서 한 번만 ‘피니시’ 를 물어요\n");
            return sb.ToString();
        }

        // ───────── 노트 넣기 · 빼기 ─────────
        RNote Near(int lane, double t, double tol)
        {
            RNote best = null;
            double bd = tol;
            foreach (var n in chart.Notes)
            {
                if (n.K != lane) continue;
                double d = Math.Abs(n.T - t);
                if (d <= bd) { bd = d; best = n; }
            }
            return best;
        }

        void AddNote(int lane, double t)
        {
            if (lane < 0 || lane > 2 || t < 0) return;
            if (Near(lane, t, 0.004) != null) return;
            Snapshot();
            chart.Notes.Add(new RNote(Math.Round(t, 4), lane));
            chart.Normalize();
            Dirty();
        }

        void ToggleGate(double t)
        {
            Snapshot();
            double tol = 8 / tl.Pps;
            int i = chart.Gates.FindIndex(g => Math.Abs(g - t) <= tol);
            if (i >= 0) { chart.Gates.RemoveAt(i); Msg("게이트를 뺐어요"); }
            else { chart.Gates.Add(Math.Round(t, 4)); Msg($"게이트를 넣었어요 ({t:0.000}초)"); }
            chart.Normalize();
            Dirty();
        }

        void OnDown(PointerDownEvent e)
        {
            var pos = (Vector2)e.localPosition;
            if (pos.y < EditorField.HEAD) return;   // 레인 이름 머리줄
            int lane = tl.LaneAt(pos.x);
            double raw = tl.TimeAt(pos.y);
            if (lane == -2 || e.button == 2)
            {
                if (lane == -2) { dragScroll = true; dragY = pos.y; dragT = tl.ViewT; tl.CapturePointer(e.pointerId); return; }
            }
            if (e.shiftKey && e.button == 0) { ToggleGate(tl.SnapT(raw)); return; }
            if (lane < 0) return;
            var hit = Near(lane, raw, 9 / tl.Pps);
            if (e.button == 1 || (e.button == 0 && hit != null))
            {
                if (hit != null) { Snapshot(); chart.Notes.Remove(hit); Dirty(); }
                return;
            }
            if (e.button == 0) AddNote(lane, tl.SnapT(raw));
        }

        void OnMove(PointerMoveEvent e)
        {
            var pos = (Vector2)e.localPosition;
            if (dragScroll)
            {
                tl.ViewT = Math.Max(0, dragT + (pos.y - dragY) / tl.Pps);
                if (playing) Seek(tl.ViewT);
                return;
            }
            int lane = tl.LaneAt(pos.x);
            tl.HoverLane = lane >= 0 ? lane : -1;
            tl.HoverT = tl.SnapT(tl.TimeAt(pos.y));
        }

        void OnWheel(WheelEvent e)
        {
            if (e.ctrlKey || e.commandKey)
            {
                tl.Pps = Mathf.Clamp(tl.Pps * (e.delta.y > 0 ? 1 / 1.15f : 1.15f), 60, 1200);
            }
            else
            {
                tl.ViewT = Math.Max(0, tl.ViewT - e.delta.y * 0.06 * (240.0 / tl.Pps) * 3);
                if (playing) Seek(tl.ViewT);
            }
            e.StopPropagation();
        }

        // ───────── 재생 · 녹음 ─────────
        void TogglePlay() { if (playing) StopPlay(); else StartPlay(); }

        void StartPlay()
        {
            if (clip == null) { Msg("곡을 아직 못 불러왔어요", true); return; }
            Seek(tl.ViewT);
            playing = true;
            tl.Playing = true;
            playBtn.text = "⏸ 정지 (P)";
        }

        void Seek(double t)
        {
            audio.StopMusic();
            var m = audio.Music;
            m.clip = clip;
            m.volume = audio.MusicVol;
            if (t < clip.length)
            {
                m.time = Mathf.Clamp((float)t, 0, clip.length - 0.01f);
                m.PlayScheduled(AudioSettings.dspTime + 0.05);
            }
            playDsp = AudioSettings.dspTime + 0.05 - t;
            lastT = t;
        }

        void StopPlay()
        {
            if (!playing) return;
            playing = false;
            tl.Playing = false;
            audio.StopMusic();
            playBtn.text = "▶ 재생 (P)";
            if (tl.Recording) ToggleRec();
        }

        void ToggleRec()
        {
            tl.Recording = !tl.Recording;
            recBtn.EnableInClassList("ed-rec", tl.Recording);
            recBtn.text = tl.Recording ? "■ 녹음 중 (R)" : "● 녹음 (R)";
            if (tl.Recording)
            {
                Snapshot();
                if (!playing)
                {
                    tl.ViewT = Math.Max(0, tl.ViewT - chart.Bar);   // 한 마디 앞에서부터 틀어 준비 시간을 준다
                    StartPlay();
                }
                Msg("녹음 중 — S · D · 스페이스로 찍으세요 (R 또는 P 로 멈춤)");
            }
        }

        void TestPlay()
        {
            StopPlay();
            if (chart.Notes.Count == 0) { Msg("노트가 없어요", true); return; }
            var c = chart.Clone();
            Hide();
            OnPlay?.Invoke(c);
        }

        public void OnUpdate(float dt)
        {
            if (!IsOpen || Dialog.IsOpen)
            {
                if (IsOpen) tl.UpdateLabels();
                if (IsOpen && Dialog.IsOpen) Keys.ClearTaps();   // 대화창에 쓰는 동안 눌린 키가 나중에 노트로 들어오지 않게
                return;
            }
            if (VideoAudio.Busy && clip == null) Msg($"🎬 영상에서 소리를 뽑는 중… {VideoAudio.Progress * 100:0}% (처음 한 번만 — 곡 길이만큼 걸려요)");
            double now = tl.ViewT;
            if (playing)
            {
                now = Math.Max(0, AudioSettings.dspTime - playDsp);
                tl.ViewT = now;
                // 노트 소리 · 메트로놈
                if (assist)
                    foreach (var n in chart.Notes)
                        if (n.T > lastT && n.T <= now)
                            if (n.K != Lane.FINISH) audio.Play(n.K == Lane.S ? Synth.SfxS : n.K == Lane.D ? Synth.SfxD : Synth.SfxHook, 0.55f);
                if (metro)
                {
                    double b = chart.Beat, off = chart.OffsetMs / 1000.0;
                    long a0 = (long)Math.Floor((lastT - off) / b), a1 = (long)Math.Floor((now - off) / b);
                    if (a1 > a0 && a1 >= 0) audio.Play(Synth.SfxTick, a1 % chart.BeatsPerBar == 0 ? 0.35f : 0.18f, a1 % chart.BeatsPerBar == 0 ? 1.25f : 1f);
                }
                if (now > chart.Length + 1 || (clip != null && now > clip.length + 0.5)) StopPlay();
                lastT = now;
            }
            // S · D · 스페이스: 키 이벤트가 아니라 직접 읽는다(포커스가 어디 있든, 빠르게 연달아 눌러도 빠지지 않게).
            // 누른 실제 시각(Age)까지 알면 녹음 때 그만큼 앞당긴다. 글자 칸에 쓰는 중이면 버린다.
            if (Keys.Live)
            {
                Keys.TakeTaps(tapBuf);
                if (!Typing()) foreach (var tp in tapBuf) LaneKey(tp.Lane, tp.Age);
            }
            // 찍고 한 칸 앞으로(두 키를 거의 동시에 누르면 같은 자리에 찍히도록 0.12초 기다렸다가)
            if (stepPending)
            {
                stepWait -= dt;
                if (stepWait <= 0) { stepPending = false; stepMask = 0; if (!playing) Nudge(1, false); }
            }
            for (int k = 0; k < 3; k++) tl.Flash[k] = Mathf.Max(0, tl.Flash[k] - dt * 5);
            modeL.text = tl.Recording ? "● 녹음 중 — S · D · 스페이스를 누르는 순간 노트" : playing ? "▶ 재생 중 — 키는 소리만 (R 녹음 · P 정지)" : "✏ 찍기 — S · D · 스페이스 = 재생선 자리에 노트";
            modeL.EnableInClassList("ed-mode--rec", tl.Recording);
            tl.MarkDirtyRepaint();
            tl.UpdateLabels();
        }

        /// <summary>
        /// 에디터 키(화면이 포커스를 가진 동안). 글자 칸에 쓰는 중이면 건드리지 않는다(엔터를 치면 화면으로 포커스가 돌아온다).
        /// 키 반복은 무시 — 누르고 있어도 노트는 한 번만.
        /// </summary>
        void OnKey(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape) return;                 // 킷(KitRoot → 작업실)이 처리(재생 정지 · 닫기)
            if (InText(e.target))
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) el.schedule.Execute(() => el.Focus()).StartingIn(10);
                return;
            }
            e.StopImmediatePropagation();
            if (e.keyCode == KeyCode.None || !IsOpen || Dialog.IsOpen) return;
            bool first = held.Add(e.keyCode);
            bool ctrl = e.ctrlKey || e.commandKey;
            switch (e.keyCode)
            {
                // 레인 키는 OnUpdate 에서 직접 읽는다(직접 읽을 수 없는 환경에서만 여기서)
                case KeyCode.S: if (first && !Keys.Live) LaneKey(Lane.S, 0); break;
                case KeyCode.D: if (first && !Keys.Live) LaneKey(Lane.D, 0); break;
                case KeyCode.Space: if (first && !Keys.Live) LaneKey(Lane.HOOK, 0); break;
                case KeyCode.P: if (first) TogglePlay(); break;
                case KeyCode.R: if (first) ToggleRec(); break;
                case KeyCode.T: if (first) TestPlay(); break;
                case KeyCode.G: if (first) ToggleGate(tl.SnapT(CurT())); break;
                case KeyCode.Z: if (first && ctrl) Undo(); break;
                case KeyCode.Delete:
                case KeyCode.Backspace: DeleteAtPlayhead(); break;
                case KeyCode.Home: StopPlay(); stepPending = false; stepMask = 0; tl.ViewT = 0; break;
                case KeyCode.LeftArrow: Nudge(-1, false); break;
                case KeyCode.RightArrow: Nudge(1, false); break;
                case KeyCode.UpArrow: Nudge(1, true); break;
                case KeyCode.DownArrow: Nudge(-1, true); break;
            }
        }

        double CurT() => playing ? Math.Max(0, AudioSettings.dspTime - playDsp) : tl.ViewT;

        /// <summary>
        /// S · D · 스페이스. 녹음 중이면 누른 순간(판정 보정·박자 맞춤 적용)에 노트, 그냥 재생 중이면 소리만,
        /// 멈춘 상태면 재생선 자리에 노트(이미 있으면 지움) — ‘찍고 한 칸 앞으로’ 가 켜져 있으면 잠시 뒤 한 칸 이동.
        /// </summary>
        void LaneKey(int k, double age)
        {
            if (k < 0 || k > 2) return;
            tl.Flash[k] = 1;
            var sfx = k == Lane.S ? Synth.SfxS : k == Lane.D ? Synth.SfxD : Synth.SfxHook;
            string nm = k == Lane.HOOK ? "스페이스" : Lane.NAME[k];
            if (playing)
            {
                audio.Play(sfx, 0.7f);
                if (!tl.Recording) return;
                double t = tl.SnapT(CurT() - Math.Min(age, 0.25) - KitRoot.Settings.RhythmOffsetMs / 1000.0);
                if (Near(k, t, 0.004) == null) { chart.Notes.Add(new RNote(Math.Round(t, 4), k)); chart.Normalize(); Dirty(); }
                return;
            }
            // 같은 레인을 ‘한 칸’ 기다리는 동안 또 눌렀다 = 빠르게 연달아 찍는 중 → 먼저 한 칸 가고 찍는다(예전엔 같은 자리를 지워 버렸음)
            if (stepPending && (stepMask & (1 << k)) != 0)
            {
                stepPending = false;
                stepMask = 0;
                Nudge(1, false);
            }
            double at = tl.SnapT(tl.ViewT);
            var hit = Near(k, at, 0.004);
            if (hit != null && !stepAdvance)
            {
                Snapshot();
                chart.Notes.Remove(hit);
                Msg($"{nm} 노트를 지웠어요 ({at:0.000}초)");
                Dirty();
                return;
            }
            if (hit == null)
            {
                Snapshot();
                chart.Notes.Add(new RNote(Math.Round(at, 4), k));
                chart.Normalize();
                Msg($"{nm} 노트 ({at:0.000}초)");
                Dirty();
            }
            else Msg($"{nm} 노트가 이미 있어요 ({at:0.000}초) — 지우려면 Delete");
            audio.Play(sfx, 0.7f);
            if (stepAdvance)
            {
                if (!stepPending) stepWait = 0.12f;
                stepPending = true;
                stepMask |= 1 << k;
            }
        }

        void DeleteAtPlayhead()
        {
            double t = tl.SnapT(CurT());
            var rm = chart.Notes.Where(n => Math.Abs(n.T - t) < 0.01).ToList();
            if (rm.Count == 0) { Msg("재생선 자리에 노트가 없어요"); return; }
            Snapshot();
            foreach (var n in rm) chart.Notes.Remove(n);
            Dirty();
            Msg($"노트 {rm.Count}개를 지웠어요");
        }

        static bool KeyT()
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.tKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.T);
#else
            return false;
#endif
        }

        void Nudge(int dirn, bool wholeBeat)
        {
            stepPending = false;
            stepMask = 0;
            double step = wholeBeat || tl.SnapDiv <= 0 ? chart.Beat : chart.Beat / tl.SnapDiv;
            tl.ViewT = Math.Max(0, tl.SnapT(tl.ViewT) + dirn * step);
            if (playing) Seek(tl.ViewT);
        }
    }
}
