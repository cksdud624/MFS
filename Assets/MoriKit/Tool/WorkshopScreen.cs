using System;
using System.Collections.Generic;
using Mori.Core.Fight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 작업실(타이틀 › 작업실): 게임 플레이 없이 그림만 작업하는 곳.
    /// 왼쪽 = 도트 편집기(화면 전체), 오른쪽 = 실제 결투 크기 미리보기(배경 · 지형 · 플레이어 · 결투자 — 결투는 돌지 않음).
    /// 편집 중인 상태를 캐릭터가 계속 재생하고, PSD 를 포토샵에서 저장해도 바로 바뀐다.
    /// </summary>
    public sealed class WorkshopScreen
    {
        readonly VisualElement el, previewHost;
        readonly StageField field;
        readonly SpriteEditor editor;
        readonly Label note, ctl;
        readonly Button foeBtn;
        StageWorld world;
        double t;
        float pvScale = Mathf.Clamp(PlayerPrefs.GetFloat("mori.kit.pvScale", 2f), 1f, 3f);

        /// <summary>미리보기 맵. halfW = 0 → 기본 결투장(튜닝 ‘결투장’ 그대로), 그 외 = 자유 평지(지형 없음 · 천장 뚫림 · 양 끝 벽).</summary>
        static readonly (string name, double halfW, string tip)[] MAPS =
        {
            ("기본 결투장", 0, "기둥 · 발판 · 천장이 있는 기본 결투장(F2 › 결투장 · 맵 패턴 › 결투장 값 그대로)"),
            ("평지 넓음", 60 * FightUnit.S, "지형 없는 평지 · 천장 뚫림 · 기본 결투장의 2배 넓이"),
            ("평지 아주 넓음", 120 * FightUnit.S, "지형 없는 평지 · 천장 뚫림 · 기본 결투장의 4배 넓이"),
            ("평지 끝없이", 400 * FightUnit.S, "지형 없는 평지 · 천장 뚫림 · 기본 결투장의 약 13배 — 벽을 거의 만날 일 없음(이동 · 점프 모션 확인용)"),
        };
        int mapIdx = Mathf.Clamp(PlayerPrefs.GetInt("mori.kit.map", 0), 0, MAPS.Length - 1);
        readonly List<(float s, Button b)> zoomBtns = new List<(float, Button)>();
        TunePanel tune;                                  // F2 전투 튜닝 패널(Mori 와 같음)
        PreviewHud hud;
        float overT;                                     // 결투가 끝난(K.O.) 뒤 흐른 시간 → 잠시 뒤 처음 위치로
        /// <summary>미리보기가 조작을 가짐(실제 플레이처럼 움직이기). 편집기를 누르면 편집기로 돌아감.</summary>
        bool play;
        bool freeMode = true;
        readonly FxPanel fx;
        readonly CardPanel cards;
        int tab;
        readonly ChainPanel chains;                      // 테스트 환경: 대쉬 · 카운터 충전 무제한, 플레이어 체력 안 닳음
        Button freeBtn;
        bool foeActive;
        Button allyBtn;

        void ToggleAlly()
        {
            var t = TuneStore.Current;
            t.AllyOn = t.AllyOn >= 0.5 ? 0 : 1;
            if (t.AllyOn >= 0.5 && world != null) world.D.A.Snap(world.D.P, world.D.E);     // 켤 때 플레이어 뒤에 바로 섬
            allyBtn.EnableInClassList("ae-btn--on", t.AllyOn >= 0.5);
            if (!TuneStore.Save(out string err)) Debug.LogWarning("[Mori] 저격수 설정 저장 실패: " + err);
            tune?.RefreshValues();
            note.text = t.AllyOn >= 0.5 ? "저격수 켬 — 기계 공격을 동료가 쏴요(실험)" : "저격수 끔 — 기계 공격은 예전처럼 플레이어 판정";
        }
        readonly List<ActInput.Press> presses = new List<ActInput.Press>();
        public bool IsOpen { get; private set; }
        public bool TextFocus => editor.TextFocus;

        readonly AudioHub audio;
        Mori.Core.Rhythm.RhythmChart chart;
        string chartName = ChartStore.TEST;
        bool musicOn = PlayerPrefs.GetInt("mori.kit.music", 1) == 1;     // 끄면 소리만 없음(박자 시계는 그대로)
        readonly Button musicBtn;
        readonly DropdownField songDd;
        readonly ChartEditor notes;                      // 노트 에디터(Mori 에서 옮김) — 작업실 왼쪽을 덮음

        public WorkshopScreen(VisualElement root, AudioHub audio)
        {
            this.audio = audio;
            el = new VisualElement();
            el.AddToClassList("ws");
            root.Add(el);
            el.style.display = DisplayStyle.None;

            var hd = new VisualElement(); hd.AddToClassList("ws-hd"); el.Add(hd);
            var t0 = new Label("작업실"); t0.AddToClassList("ws-title"); hd.Add(t0);
            var t1 = new Label("게임 플레이 없이 그림만 — 그리는 즉시 오른쪽 미리보기에 실제 크기로 보여요. Esc = 나가기(자동 저장)"); t1.AddToClassList("ws-sub"); hd.Add(t1);
            var x = new Button(Hide) { text = "나가기" }; x.AddToClassList("ae-btn"); hd.Add(x);

            var body = new VisualElement(); body.AddToClassList("ws-body"); el.Add(body);
            var left = new VisualElement(); left.AddToClassList("ws-left"); body.Add(left);
            editor = new SpriteEditor(left, true);
            editor.OnFocus = () => { ReleaseTune(); SetPlay(false); };
            notes = new ChartEditor(audio, left);
            notes.OnPlay = c => { UseChart(c, null); SetPlay(true); };
            notes.OnClosed = () => { RefreshSongs(); if (IsOpen) editor.Root.Focus(); };

            var right = new VisualElement(); right.AddToClassList("ws-right"); body.Add(right);
            var pr = new VisualElement(); pr.AddToClassList("ae-wrap"); right.Add(pr);
            var pl = new Label("미리보기"); pl.AddToClassList("ae-sec"); pr.Add(pl);
            foreach (var s in new[] { 1f, 1.5f, 2f, 3f })
            {
                float ss = s;
                var b = new Button(() => { pvScale = ss; PlayerPrefs.SetFloat("mori.kit.pvScale", ss); ApplyScale(); })
                { text = $"{s:0.#}×", tooltip = "미리보기 카메라 확대 — 1× = 게임 카메라 넓이 · 플레이어를 따라가며 확대(도트를 크게 보기)" };
                b.AddToClassList("ae-btn"); pr.Add(b); zoomBtns.Add((ss, b));
            }
            fullBtn = new Button(() => SetFull(!full)) { text = "전체화면 F4", tooltip = "F4 = 전체화면 + 바로 조작 · 다시 F4 = 돌아와서 도트 편집기로(Esc 도)" }; fullBtn.AddToClassList("ae-btn"); pr.Add(fullBtn);
            var sw = new Button(() => { if (world == null) return; (world.Hero.X, world.Foe.X) = (world.Foe.X, world.Hero.X); Face(); }) { text = "자리 바꾸기" }; sw.AddToClassList("ae-btn"); pr.Add(sw);
            var rs = new Button(Reset) { text = "처음 위치로" }; rs.AddToClassList("ae-btn"); pr.Add(rs);
            foeBtn = new Button(() => { foeActive = !foeActive; if (world != null) world.D.Dummy = !foeActive; foeBtn.EnableInClassList("ae-btn--on", foeActive); }) { text = "결투자 공격", tooltip = "끄면 허수아비(맞기만 함), 켜면 실제 결투처럼 공격" };
            foeBtn.AddToClassList("ae-btn"); pr.Add(foeBtn);
            // 저격수(동료) 실험 기능 켜기 · 끄기 — 전투 튜닝 ‘동료(기계) › 켬’ 과 같은 값(바로 저장)
            allyBtn = new Button(ToggleAlly) { text = "저격수(실험)", tooltip = "기계 공격을 맡는 동료 저격수 켜기 · 끄기 — 실험 기능이라 기본 끔. 끄면 예전처럼 기계 공격이 플레이어 판정 그대로(F2 › 동료(기계) › 켬과 같은 값)" };
            allyBtn.AddToClassList("ae-btn"); allyBtn.EnableInClassList("ae-btn--on", TuneStore.Current.AllyOn >= 0.5); pr.Add(allyBtn);
            freeBtn = new Button(() => { freeMode = !freeMode; freeBtn.EnableInClassList("ae-btn--on", freeMode); })
            { text = "쿨타임 없음", tooltip = "작업실은 테스트 환경 — 켜면(기본) 대쉬 · 카운터 충전 횟수 · 쿨타임 없이 계속 쓰고, 플레이어 체력도 바닥나지 않아요. 끄면 실제 게임 수치 그대로." };
            freeBtn.AddToClassList("ae-btn"); freeBtn.AddToClassList("ae-btn--on"); pr.Add(freeBtn);
            // 곡: 결투는 이 곡의 박자로(결투자 패턴 · 맵 패턴 · 예고음) — 오디오 시계 기준, 마디 단위로 되풀이
            var songRow = new VisualElement(); songRow.AddToClassList("ae-wrap"); right.Add(songRow);
            var sl = new Label("곡"); sl.AddToClassList("ae-k"); songRow.Add(sl);
            songDd = new DropdownField { tooltip = "결투 곡(차트) — 노트 에디터에서 저장한 차트가 여기 나와요" };
            songDd.AddToClassList("dd"); songDd.style.minWidth = 180;
            songDd.RegisterValueChangedCallback(e => { if (e.newValue != null && e.newValue != e.previousValue) LoadSong(e.newValue); });
            songRow.Add(songDd);
            musicBtn = new Button(() => { musicOn = !musicOn; PlayerPrefs.SetInt("mori.kit.music", musicOn ? 1 : 0); ApplyVol(); }) { tooltip = "곡 소리 켜기 · 끄기 — 꺼도 박자 시계는 그대로 흘러요(결투자 패턴 박자 유지)" };
            musicBtn.AddToClassList("ae-btn"); songRow.Add(musicBtn);
            var nb = new Button(OpenNotes) { text = "노트 에디터", tooltip = "곡을 넣고 BPM · 박자 · 노트를 편집 — ▶ 미리보기로 플레이 = 이 미리보기 결투를 그 곡으로 바로" };
            nb.AddToClassList("ae-btn"); songRow.Add(nb);
            // 배경 프리셋: 게임 배경 · 밝은 배경 · 흰색(그림 확인용)
            var bgRow = new VisualElement(); bgRow.AddToClassList("ae-wrap"); right.Add(bgRow);
            var bgl = new Label("배경"); bgl.AddToClassList("ae-k"); bgRow.Add(bgl);
            var bgBtns = new List<Button>();
            foreach (var (n, v) in new[] { ("게임", 0), ("밝은 배경", 1), ("흰색", 2) })
            {
                int vv = v;
                var b = new Button(() =>
                {
                    field.BgPreset = vv;
                    for (int i = 0; i < bgBtns.Count; i++) bgBtns[i].EnableInClassList("ae-btn--on", i == vv);
                }) { text = n };
                b.AddToClassList("ae-btn"); bgRow.Add(b); bgBtns.Add(b);
            }
            bgBtns[0].AddToClassList("ae-btn--on");
            // 맵: 기본 결투장(기둥 · 발판 · 천장) 또는 자유 평지(바닥만 · 천장 뚫림) — 바꾸면 처음부터
            var mapRow = new VisualElement(); mapRow.AddToClassList("ae-wrap"); right.Add(mapRow);
            var ml = new Label("맵"); ml.AddToClassList("ae-k"); mapRow.Add(ml);
            var mapBtns = new List<Button>();
            for (int i = 0; i < MAPS.Length; i++)
            {
                int ii = i;
                var b = new Button(() =>
                {
                    mapIdx = ii; PlayerPrefs.SetInt("mori.kit.map", ii);
                    for (int j = 0; j < mapBtns.Count; j++) mapBtns[j].EnableInClassList("ae-btn--on", j == ii);
                    Reset();
                }) { text = MAPS[i].name, tooltip = MAPS[i].tip };
                b.AddToClassList("ae-btn"); mapRow.Add(b); mapBtns.Add(b);
            }
            mapBtns[mapIdx].AddToClassList("ae-btn--on");
            // 표시: Mori 결투 화면의 정보 · 연출을 항목마다 켜고 끄기
            var showRow = new VisualElement(); showRow.AddToClassList("ae-wrap"); right.Add(showRow);
            var shl = new Label("표시"); shl.AddToClassList("ae-k"); showRow.Add(shl);
            var showBtns = new List<(string id, Button b)>();
            void SyncShow() { foreach (var (id, b) in showBtns) b.EnableInClassList("ae-btn--on", PreviewHud.On(id)); }
            foreach (var (id, name, tip, _) in PreviewHud.TOGGLES)
            {
                string iid = id;
                var b = new Button(() => { PreviewHud.Set(iid, !PreviewHud.On(iid)); SyncShow(); }) { text = name, tooltip = tip + " — 켜기 · 끄기" };
                b.AddToClassList("ae-btn");
                showRow.Add(b); showBtns.Add((id, b));
            }
            var allOn = new Button(() => { foreach (var t in PreviewHud.TOGGLES) PreviewHud.Set(t.id, true); SyncShow(); }) { text = "모두 켜기", tooltip = "표시 항목 전부 켜기(판정 상자 포함)" };
            allOn.AddToClassList("ae-btn"); showRow.Add(allOn);
            var allOff = new Button(() => { foreach (var t in PreviewHud.TOGGLES) PreviewHud.Set(t.id, false); SyncShow(); }) { text = "모두 끄기", tooltip = "그림만 보기 — 표시 항목 전부 끄기" };
            allOff.AddToClassList("ae-btn"); showRow.Add(allOff);
            SyncShow();

            ctl = new Label(""); ctl.AddToClassList("ws-ctl"); right.Add(ctl);
            previewHost = new VisualElement { focusable = true }; previewHost.AddToClassList("ws-preview"); right.Add(previewHost);
            field = new StageField();
            stageView = new StageView(field);
            previewHost.Add(stageView);   // 결투와 같은 후처리
            hud = new PreviewHud();                  // Mori 결투 화면 정보 · 연출(글자 · 이펙트 · 체력 · 콤보 …)
            previewHost.Add(hud.Root);
            // 미리보기를 누르면 조작이 이쪽으로(키보드로 플레이어 움직이기). 키가 화면 단추로 새지 않게 막는다.
            previewHost.RegisterCallback<PointerDownEvent>(e => { SetPlay(true); e.StopPropagation(); });
            // 전체화면에서 돌아가기(미리보기 위 오른쪽 위에 뜸)
            fullExit = new Button(ExitFull) { text = "돌아가기 (F4 · Esc)", tooltip = "전체화면 끝내고 도트 편집기로" };
            fullExit.AddToClassList("ae-btn"); fullExit.AddToClassList("ws-full-exit");
            fullExit.style.display = DisplayStyle.None;
            el.Add(fullExit);
            previewHost.RegisterCallback<KeyDownEvent>(e => { if (play) e.StopPropagation(); });
            el.RegisterCallback<NavigationMoveEvent>(e => { if (play) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            el.RegisterCallback<NavigationSubmitEvent>(e => { if (play) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            el.RegisterCallback<NavigationCancelEvent>(e => { if (play) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);

            // 아래 칸: ‘그림 크기’ · ‘사슬’ 탭(넘치면 스크롤 — 미리보기가 찌그러지지 않게)
            var tabs = new VisualElement(); tabs.AddToClassList("ae-wrap"); right.Add(tabs);
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.AddToClassList("ws-tunesv"); right.Add(sv);
            var tb = new VisualElement(); tb.AddToClassList("ws-tune"); sv.Add(tb);
            var chainHost = new VisualElement(); sv.Add(chainHost);
            chains = new ChainPanel(chainHost, () => editor.PickColor);
            var fxHost = new VisualElement(); sv.Add(fxHost);
            fx = new FxPanel(fxHost, () => world, () => field);
            var cardHost = new VisualElement(); sv.Add(cardHost);
            cards = new CardPanel(cardHost, () => world, () => field, () => play);
            Button tabSize = null, tabChain = null, tabFx = null, tabCard = null;
            void Tab(int k)
            {
                tab = k;
                tb.style.display = k == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                chainHost.style.display = k == 1 ? DisplayStyle.Flex : DisplayStyle.None;
                fxHost.style.display = k == 2 ? DisplayStyle.Flex : DisplayStyle.None;
                cardHost.style.display = k == 3 ? DisplayStyle.Flex : DisplayStyle.None;
                sv.EnableInClassList("ws-tunesv--tall", k == 3);      // 연출 칸은 시간표 · 수치가 많아 더 높게
                tabSize.EnableInClassList("ae-btn--on", k == 0); tabChain.EnableInClassList("ae-btn--on", k == 1); tabFx.EnableInClassList("ae-btn--on", k == 2); tabCard.EnableInClassList("ae-btn--on", k == 3);
                if (k == 1) chains.Rebuild();
                if (k == 2) fx.Rebuild();
                if (k == 3) cards.Rebuild();
            }
            tabSize = new Button(() => Tab(0)) { text = "그림 크기", tooltip = "1U = px(PPU) · 내리기 · 보이는 크기 배율" }; tabSize.AddToClassList("ae-btn"); tabs.Add(tabSize);
            tabChain = new Button(() => Tab(1)) { text = "사슬", tooltip = "머리카락 · 옷자락 사슬(@ 기준점) — 프리셋 · 수치 · 색" }; tabChain.AddToClassList("ae-btn"); tabs.Add(tabChain);
            tabFx = new Button(() => Tab(2)) { text = "프리팹", tooltip = "다른 프로젝트에서 가져온 3D · 파티클 프리팹을 순간(대쉬 · 공격 · 카운터 …)마다 붙이기" }; tabFx.AddToClassList("ae-btn"); tabs.Add(tabFx);
            tabCard = new Button(() => Tab(3)) { text = "연출", tooltip = "연출 카드: 순간마다 이펙트 · 그림 · 히트스톱 · 흔들림 · 줌 · 섬광을 시간표로(프레임 단위)" }; tabCard.AddToClassList("ae-btn"); tabs.Add(tabCard);
            Tab(0);
            PpuRow(tb);
            Tune(tb, "플레이어 아래로 내리기(U)", () => TuneStore.Current.HeroImgDown, v => TuneStore.Current.HeroImgDown = v, 0.01);
            Tune(tb, "결투자 아래로 내리기(U)", () => TuneStore.Current.FoeImgDown, v => TuneStore.Current.FoeImgDown = v, 0.01);
            Tune(tb, "넓이 배율 따라가기(0 ~ 1)", () => TuneStore.Current.ImgFollowZoom, v => TuneStore.Current.ImgFollowZoom = Math.Max(0, Math.Min(1, v)), 0.05);
            Tune(tb, "그림 크기 배율 · 전체(배)", () => TuneStore.Current.ImgScaleAll, v => TuneStore.Current.ImgScaleAll = Math.Max(0.1, v), 0.05);
            Tune(tb, "플레이어 그림 배율(배)", () => TuneStore.Current.ImgScaleHero, v => TuneStore.Current.ImgScaleHero = Math.Max(0.1, v), 0.05);
            Tune(tb, "결투자 그림 배율(배)", () => TuneStore.Current.ImgScaleFoe, v => TuneStore.Current.ImgScaleFoe = Math.Max(0.1, v), 0.05);
            Tune(tb, "그림이 보는 방향(1 오른쪽 · 0 왼쪽)", () => TuneStore.Current.ImgFaceRight, v => TuneStore.Current.ImgFaceRight = v >= 0.5 ? 1 : 0, 1);
            note = new Label(""); note.AddToClassList("ae-note"); right.Add(note);

            // F2 = 전투 튜닝 패널(Mori 결투 화면과 같은 것) — 작업실 위에 뜸
            tune = new TunePanel(el);
            // 제어권은 한 곳에만: 튜닝 패널을 누르면 패널(결투 · 곡 멈춤, 편집기 단축키 끔) · 미리보기를 누르면 미리보기 · 편집기를 누르면 편집기
            tune.OnFocus = () =>
            {
                if (tuneCtl) return;
                SetPlay(false);
                tuneCtl = true; tune.SetCtl(true);
                ctl.text = "튜닝 패널 조작 중 — 키보드는 패널로. 미리보기를 누르면 다시 조작(F4 도), 편집기를 누르면 편집으로.";
            };
            tune.OnHidden = () => { if (!tuneCtl) return; ReleaseTune(); if (IsOpen && !play) editor.Root.Focus(); };
            tune.OnVolume = ApplyVol;                // 소리 탭 음량 → 음악 켬/끔과 함께 다시 적용
            hud.OnStage = rage => audio.Play(Synth.SfxGate, 0.9f, rage ? 0.6f : 1f);      // 2단계 · 격노 전환음
            tune.OnForceMap = k => { var d = world?.D; if (d != null && d.CurMap == MapKind.None && !d.Over) d.ForceMap(k); };
        }

        /// <summary>F4: 전체화면 + 미리보기 조작 ↔ 원래 화면 + 도트 편집기.</summary>
        void ToggleF4()
        {
            if (full) { ExitFull(); return; }
            SetFull(true);
            SetPlay(true);
        }

        void ExitFull()
        {
            SetFull(false);
            SetPlay(false);
            editor.Root.Focus();
        }

        /// <summary>그림 크기 = 1U 에 몇 px(MFS 스프라이트 Pixels Per Unit). 모든 캔버스 · 연출 그림에 같이 적용.</summary>
        void PpuRow(VisualElement p)
        {
            var row = new VisualElement(); row.AddToClassList("ws-tune-row"); p.Add(row);
            row.tooltip = "1U 에 몇 px — MFS 캐릭터 스프라이트의 Pixels Per Unit 과 같게(기본 100). 캔버스를 키워도 캐릭터 · 이펙트 픽셀 크기는 그대로";
            var l = new Label("1U = px(PPU)"); l.AddToClassList("ae-k"); row.Add(l);
            var v = new Label(PostSettings.Ppu.ToString("0")); v.AddToClassList("ws-tune-v");
            void Do(float d) { PostSettings.F.ppu = Mathf.Clamp(Mathf.Round(PostSettings.Ppu + d), 1, 2000); v.text = PostSettings.Ppu.ToString("0"); PostSettings.Save(); }
            var m = new Button(() => Do(-1)) { text = "−" }; m.AddToClassList("ae-btn"); row.Add(m);
            row.Add(v);
            var pl = new Button(() => Do(1)) { text = "+" }; pl.AddToClassList("ae-btn"); row.Add(pl);
            var r = new Button(() => Do(100 - PostSettings.Ppu)) { text = "100", tooltip = "MFS 기본값(100)으로" }; r.AddToClassList("ae-btn"); row.Add(r);
        }

        void Tune(VisualElement p, string name, Func<double> get, Action<double> set, double step)
        {
            var row = new VisualElement(); row.AddToClassList("ws-tune-row"); p.Add(row);
            var l = new Label(name); l.AddToClassList("ae-k"); row.Add(l);
            var v = new Label(get().ToString("0.##")); v.AddToClassList("ws-tune-v");
            void Do(double d) { set(Math.Round(get() + d, 3)); v.text = get().ToString("0.##"); TuneStore.Save(out _); }
            var m = new Button(() => Do(-step)) { text = "−" }; m.AddToClassList("ae-btn"); row.Add(m);
            row.Add(v);
            var pl = new Button(() => Do(step)) { text = "+" }; pl.AddToClassList("ae-btn"); row.Add(pl);
        }

        void ApplyScale()
        {
            // 미리보기 확대 = 카메라를 다가가게(그림이 흐려지거나 잘리지 않고, 카메라가 플레이어를 따라감)
            field.style.scale = new Scale(Vector3.one);
            if (world != null) world.ViewZoom = pvScale;
            foreach (var (s, b) in zoomBtns) b.EnableInClassList("ae-btn--on", Mathf.Approximately(s, pvScale));
        }

        void Face()
        {
            world.Hero.Facing = world.Foe.X > world.Hero.X ? 1 : -1;
            world.Foe.Facing = -world.Hero.Facing;
        }

        /// <summary>테스트 환경: 대쉬 · 카운터를 바로 다시 쓸 수 있게 채우고, 플레이어 체력이 바닥나지 않게.</summary>
        static void Refill(Duel d)
        {
            var tn = TuneStore.Current;
            d.P.DashStack = tn.DashCapI; d.P.DashRech = 0;
            d.P.CtrStack = tn.CtrCapI; d.P.CtrRech = 0; d.P.CtrCd = 0;
            if (d.P.UltT <= 0) d.P.Ult = tn.UltMax;                     // 쿨타임 없음: 궁극기 게이지도 늘 가득
            if (d.PlayerHp < d.MaxPlayerHp * 0.25) d.PlayerHp = d.MaxPlayerHp;
        }

        /// <summary>튜닝 패널이 제어권을 가지고 있음(키보드 → 패널).</summary>
        bool tuneCtl;

        void ReleaseTune()
        {
            if (!tuneCtl) return;
            tuneCtl = false; tune.SetCtl(false);
            if (!play) ctl.text = "미리보기를 누르면 실제 플레이처럼 조작할 수 있어요.";
        }

        void SetPlay(bool on)
        {
            if (on) ReleaseTune();                    // 미리보기로 제어권이 오면 패널은 놓음
            editor.KeysOff = on;
            if (on) previewHost.Focus();             // 미리보기를 누를 때마다 키는 미리보기로
            if (play == on) return;
            play = on;
            editor.KeysOff = on;                     // 미리보기 조작 중엔 편집기 단축키 끔
            // 곡 · 결투 시계는 조작하는 동안만 흐름 — 조작을 끝내면 멈췄다가 다음 조작 때 그 자리부터 이어서
            if (on && clip != null && !notes.IsOpen) StartAt(t);
            else if (!on) StopClock();
            previewHost.EnableInClassList("ws-preview--on", on);
            if (on) { previewHost.Focus(); ActInput.Clear(); }
            ctl.text = on ? "▶ 미리보기 조작 중 — 설정한 키로 움직이기 · 공격 · 카운터 · 대쉬 · 점프. 편집기를 누르면 편집으로 돌아가요(Esc 도)."
                          : "미리보기를 누르면 실제 플레이처럼 조작할 수 있어요.";
        }

        void Reset()
        {
            // 곡 없는 결투장: 결투자는 기본 허수아비(맞기만), ‘결투자 공격’ 을 켜면 실제처럼
            var m = MAPS[Mathf.Clamp(mapIdx, 0, MAPS.Length - 1)];
            var d = new Duel(chart, 1, m.halfW > 0 ? Arena.Flat(m.halfW) : null, TuneStore.Current) { Dummy = !foeActive };
            d.P.X = -2.5 * FightUnit.S; d.E.X = 2.5 * FightUnit.S;
            world = new StageWorld(d) { ViewZoom = pvScale };
            Face();
            field.World = world;
            t = 0;
            overT = 0;
            lastCue = -1;
            hud?.Reset(world);
            if (play && clip != null && !notes.IsOpen) StartAt(0);      // 조작 중이면 곡도 처음부터
            else StopClock();
        }

        /// <summary>Esc: 조작 중이면 편집으로 돌아가기, 아니면 작업실 나가기.</summary>
        // ───────── 미리보기 전체화면 ─────────
        bool full;
        Button fullBtn, fullExit;
        VisualElement fullParent;
        int fullIndex;

        /// <summary>미리보기를 작업실 화면 가득(편집기 · 탭을 가림). 끄면 원래 자리로.</summary>
        void SetFull(bool on)
        {
            if (on == full) return;
            full = on;
            if (on)
            {
                fullParent = previewHost.parent;
                fullIndex = fullParent != null ? fullParent.IndexOf(previewHost) : 0;
                el.Add(previewHost);
                previewHost.AddToClassList("ws-preview--full");
                fullExit.style.display = DisplayStyle.Flex;
                fullExit.BringToFront();
                if (tune.IsOpen) tune.Show();          // 튜닝 패널은 전체화면 위에
            }
            else
            {
                previewHost.RemoveFromClassList("ws-preview--full");
                if (fullParent != null) fullParent.Insert(Mathf.Clamp(fullIndex, 0, fullParent.childCount), previewHost);
                fullExit.style.display = DisplayStyle.None;
            }
            fullBtn.EnableInClassList("ae-btn--on", full);
        }

        public void Back()
        {
            if (tune.IsOpen) { tune.Hide(); return; }
            if (notes.IsOpen) { notes.Back(); return; }
            if (full) { ExitFull(); return; }
            if (play) { SetPlay(false); editor.Root.Focus(); return; }
            if (editor.BackHandled()) return;          // 단축키 바꾸는 중 · 단축키 창
            if (!TextFocus) Hide();
        }

        // ───────── 곡 · 박자 시계(오디오 시계 기준 — Mori 결투 화면과 같은 방식) ─────────
        AudioClip clip;
        string clipFor;
        double dspStart, loopStart, loopLen, nextSwap, smooth;
        bool audioClock, swapQueued;
        AudioSource curSrc, pendingSrc;

        void RefreshSongs()
        {
            var names = new List<string> { ChartStore.TEST };
            names.AddRange(ChartStore.List());
            songDd.choices = names;
            songDd.SetValueWithoutNotify(chartName != null && names.Contains(chartName) ? chartName : names[0]);
        }

        void LoadSong(string name)
        {
            try { UseChart(ChartStore.Load(name), name); }
            catch (Exception e) { note.text = "곡을 열지 못했어요: " + e.Message; }
        }

        /// <summary>이 차트로 결투를 처음부터(노트 에디터의 ▶ 미리보기로 플레이도 여기로).</summary>
        void UseChart(Mori.Core.Rhythm.RhythmChart c, string name)
        {
            if (c == null) return;
            c.Notes.RemoveAll(x => x.K == Mori.Core.Rhythm.Lane.FINISH);
            chart = c;
            chartName = name;
            if (name != null && KitRoot.Settings.RhythmChart != name) { KitRoot.Settings.RhythmChart = name; KitRoot.Settings.Save(); }     // 다음에 켜도 이 차트로
            if (name != null) songDd.SetValueWithoutNotify(name);
            else songDd.SetValueWithoutNotify((string.IsNullOrWhiteSpace(c.Title) ? "에디터 차트" : c.Title) + " (저장 전)");
            StopClock();
            Reset();
            LoadClip();
        }

        /// <summary>차트의 곡을 불러와 시계를 그 곡에 맞춤(같은 곡이면 다시 읽지 않음).</summary>
        void LoadClip()
        {
            if (chart == null) return;
            string want = chart.Audio;
            if (clip != null && clipFor == want) { SetLoop(); if (play && !notes.IsOpen) StartAt(t); return; }
            clip = null; clipFor = want;
            AudioHub.Load(want, (cl, err) =>
            {
                if (chart == null || chart.Audio != want) return;
                if (cl == null) { note.text = "곡을 불러오지 못했어요: " + err; return; }
                clip = cl;
                SetLoop();
                if (play && !notes.IsOpen) StartAt(t);
            });
        }

        void SetLoop()
        {
            double bar = chart.Bar, off = Math.Max(0, chart.OffsetMs / 1000.0);
            loopStart = off;
            loopLen = Math.Floor((clip.length - off - 0.05) / bar) * bar;
            if (loopLen < bar * 2) loopLen = 0;          // 너무 짧으면 그냥 끝까지(되풀이 없음)
        }

        /// <summary>결투 시각 tt → 곡 파일 안의 위치(마디 단위로 되풀이).</summary>
        double ClipPos(double tt)
        {
            if (loopLen <= 0 || tt < loopStart + loopLen) return tt;
            return loopStart + ((tt - loopStart) % loopLen);
        }

        /// <summary>결투 시각 tt 부터 곡을 틀고 시계를 오디오에 묶음.</summary>
        void StartAt(double tt, double lead = 0.05)
        {
            if (clip == null) { audioClock = false; return; }
            audio.StopMusic();
            audio.SetCutoff(22000);
            curSrc = audio.Music;
            curSrc.clip = clip;
            curSrc.loop = false;
            double pos = Math.Max(0, ClipPos(tt));
            curSrc.time = Mathf.Clamp((float)pos, 0, Mathf.Max(0, clip.length - 0.05f));
            double dsp = AudioSettings.dspTime;
            if (pos < clip.length) curSrc.PlayScheduled(dsp + lead + Math.Max(0, -tt));
            dspStart = dsp + lead - tt;
            if (loopLen > 0)
            {
                double k = Math.Floor((Math.Max(tt, loopStart) - loopStart) / loopLen) + 1;
                nextSwap = loopStart + k * loopLen;
            }
            swapQueued = false;
            smooth = tt;
            audioClock = true;
            ApplyVol();
        }

        void StopClock()
        {
            audio.StopMusic();
            audioClock = false;
        }

        /// <summary>곡 끝(마디 단위) 직전에 다른 소리 통로로 처음을 예약해 끊김 없이 되풀이.</summary>
        void LoopTick(double dspT)
        {
            if (loopLen <= 0 || curSrc == null) return;
            if (!swapQueued && dspT > nextSwap - 0.6)
            {
                var other = curSrc == audio.Music ? audio.Loop : audio.Music;
                other.Stop();
                other.clip = clip;
                other.time = (float)loopStart;
                double at = dspStart + nextSwap;
                other.PlayScheduled(at);
                curSrc.SetScheduledEndTime(at);
                swapQueued = true;
                pendingSrc = other;
                ApplyVol();
            }
            if (swapQueued && dspT >= nextSwap)
            {
                curSrc = pendingSrc;
                nextSwap += loopLen;
                swapQueued = false;
            }
        }

        /// <summary>음악 켬/끔 = 소리만(시계는 계속).</summary>
        void ApplyVol()
        {
            musicBtn.text = musicOn ? "음악 켬" : "음악 끔";
            musicBtn.EnableInClassList("ae-btn--on", musicOn);
            float v = musicOn ? audio.MusicVol : 0;
            if (audio.Music != null) audio.Music.volume = v;
            if (audio.Loop != null) audio.Loop.volume = v;
        }

        /// <summary>노트 에디터 열기: 미리보기 결투는 멈추고 곡은 에디터가 씀.</summary>
        void OpenNotes()
        {
            SetPlay(false);
            SetFull(false);
            StopClock();
            notes.Show();
        }

        /// <summary>예고음: 결투자 타격 1박 전마다 ‘딱’(카운터 가능한 근접은 낮게, 피해야 하는 공격은 높게). F2 ▸ 소리에서 끌 수 있다.</summary>
        double lastCue = -1;
        void Cues(double tt)
        {
            var d = world.D;
            if (!KitRoot.Settings.StageCue || !d.NextThreat(out double left, out HitKind kind, out bool stance)) return;
            double target = d.BeatOf(tt) + left;
            double lead = d.CueLeadOf(d.E.Cur);         // 패턴 탭에서 덮어썼으면 그 값
            if (left <= lead + 0.02 && left > lead - 0.6 && Math.Abs(target - lastCue) > 0.01)
            {
                lastCue = target;
                audio.Play(Synth.SfxTick, (float)d.T.CueVolume, stance ? 0.6f : kind == HitKind.Melee ? 0.85f : 1.35f);
            }
        }

        /// <summary>동작별 마지막으로 받아들인 입력의 박 판정(0 공격 · 1 카운터 · 2 대쉬 · 3 점프) — 버퍼된 동작이 나갈 때 소리에 씀.</summary>
        readonly bool[] pressBeat = new bool[5];

        /// <summary>마법 공격 소리: 박에 맞으면 또렷하게, 엇박이면 작게 + 먹먹한 ‘툭’(다른 동작 드럼과 같은 규칙).</summary>
        void AtkMagicSound(bool onBeat)
        {
            var c = SfxFiles.Get("atk-magic", Synth.DrumSnare);      // 기본 = 원래 공격 스네어
            float k = KitRoot.Settings.VolAtkMagic * 2f;
            if (onBeat) audio.Play(c, 0.85f * k);
            else { audio.Play(c, 0.3f * k, 0.92f); audio.Play(Synth.DrumMute, 0.5f); }
            // 공격 소리 한 덩어리: 스네어 → ‘마법 → 기계 지연’ 뒤 하이햇(마법 · 기계가 같이 쓰는 소리)
            // 동료가 켜져 있으면 하이햇은 동료가 실제로 쏘는 순간(AllyShot · AllyStrike)에 울림 — 그림과 소리가 딱 맞게
            if (TuneStore.Current.AllyOn < 0.5) hatAt.Add(Time.unscaledTime + Mathf.Max(0, (float)TuneStore.Current.MechDelay));
        }

        void AllyHat(float k = 1f) => audio.Play(SfxFiles.Get("atk-mech", Synth.DrumHat), 0.4f * k * KitRoot.Settings.VolAtkMech * 2f);

        /// <summary>공격 소리 덩어리의 뒤쪽 하이햇이 울릴 시각들(실제 시간).</summary>
        readonly List<float> hatAt = new List<float>();

        void TickHats()
        {
            if (hatAt.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = hatAt.Count - 1; i >= 0; i--)
            {
                if (now < hatAt[i]) continue;
                hatAt.RemoveAt(i);
                audio.Play(SfxFiles.Get("atk-mech", Synth.DrumHat), 0.4f * KitRoot.Settings.VolAtkMech * 2f);     // 기계 = 하이햇 ‘칫’
            }
        }

        void Drum(int act, bool onBeat)
        {
            var c = act == 0 ? Synth.DrumSnare : act == 1 ? Synth.DrumCrash : act == 2 ? Synth.DrumTom : Synth.DrumKick;
            if (onBeat) audio.Play(c, act == 3 ? 0.55f : 0.85f);
            else { audio.Play(c, 0.3f, 0.92f); audio.Play(Synth.DrumMute, 0.5f); }
        }

        /// <summary>결투와 같은 효과음(소리만 — 화면 연출은 결투 화면 쪽).</summary>
        void Sfx(DuelEvent e)
        {
            switch (e.Kind)
            {
                case DuelEv.PatternStart:
                    // 카운터 자세 패턴 예고: 낮은 경고음(때리지 말 것)
                    if (world.D.E.Cur != null && world.D.E.Cur.Counter && e.Act == 0) audio.Play(Synth.SfxD, 0.6f, 0.55f);
                    break;
                case DuelEv.Hit: if (e.Act < 0) audio.Play(Synth.DrumKick, 0.5f, 0.8f); else audio.Play(Synth.SfxHook, 0.25f, 0.8f); break;
                case DuelEv.CounterSuccess: audio.Play(Synth.SfxFinish, 0.6f, 1.5f); field.HeroReact("hero-counter-hit", 1); break;
                case DuelEv.WallBounce: audio.Play(Synth.SfxHook, 0.8f, 0.7f); break;
                case DuelEv.EnemyCounter: audio.Play(Synth.SfxMiss, 1f, 0.6f); break;
                case DuelEv.CounterCrash: audio.Play(Synth.SfxFinish, 0.7f, 1.2f); field.HeroReact("hero-counter-crash", 2); break;
                case DuelEv.EnemyDash: audio.Play(Synth.DrumTom, 0.35f, 0.8f); break;
                case DuelEv.EnemyJump: audio.Play(Synth.DrumKick, 0.3f, e.Value > 0 ? 1.3f : 1.1f); break;
                // 궁극기(궤도 폭격): 요청 → 조준 → 박마다 레이저 → 끝
                case DuelEv.UltStart: { float uk = (float)TuneStore.Current.UltSfx; audio.Play(Synth.SfxGate, 0.8f * uk, 0.7f); audio.Play(Synth.DrumCrash, 0.6f * uk, 0.6f); break; }
                case DuelEv.UltStrike: { float uk = (float)TuneStore.Current.UltSfx; audio.Play(Synth.DrumKick, 0.9f * uk, 0.7f); audio.Play(Synth.DrumCrash, (e.Value > 0 ? 0.55f : 0.3f) * uk, 0.75f); break; }
                case DuelEv.UltEnd: audio.Play(Synth.SfxFinish, 0.6f * (float)TuneStore.Current.UltSfx, 0.8f); break;
                case DuelEv.CounterBounce: audio.Play(Synth.SfxD, 0.6f, 1.5f); if (e.Act == 1) field.HeroReact("hero-counter-crash", 2); break;
                case DuelEv.PlayerHurt: audio.Play(Synth.SfxMiss, 0.9f, 0.85f); break;
                // 동료: 사격 = 하이햇(공격 소리 덩어리의 뒤쪽), 돌진 한 방 = 하이햇 + 묵직한 금속음
                case DuelEv.AllyShot: AllyHat(); world.Swings.Add(AllyBeam(e, false)); break;
                case DuelEv.AllyStrike: AllyHat(1.2f); audio.Play(Synth.DrumKick, 0.55f, 1.1f); audio.Play(Synth.SfxHook, 0.4f, 0.65f); world.Swings.Add(AllyBeam(e, true)); break;
                case DuelEv.AllyFly: audio.Play(Synth.SfxGate, 0.35f, 1.7f); audio.Play(Synth.DrumHat, 0.25f, 0.6f); break;     // 드론 붕 —
                case DuelEv.AllyCutIn: field.AllyCutIn((float)e.Value); break;
                case DuelEv.Shot: audio.Play(Synth.SfxTick, 0.4f, 1.6f); break;
                case DuelEv.EnemyCounterStart: audio.Play(Synth.SfxD, 0.5f, 0.6f); break;
                case DuelEv.CounterTell: audio.Play(Synth.DrumCrash, 0.7f, 1.5f); audio.Play(Synth.SfxD, 0.9f, 1.8f); break;
                case DuelEv.SpamReact: audio.Play(Synth.SfxD, 0.7f, 0.5f); break;
                case DuelEv.MapStart: audio.Play(Synth.SfxGate, 0.8f, 0.7f); break;
                case DuelEv.MapStrike:
                    var mk = (MapKind)(int)e.Value;
                    if (mk == MapKind.Columns) { audio.Play(Synth.DrumKick, 0.7f, 0.9f); audio.Play(Synth.DrumCrash, 0.25f, 1.3f); }
                    else if (mk == MapKind.Lasers) audio.Play(Synth.DrumSnare, 0.6f, 0.7f);
                    else if (mk == MapKind.Walls) audio.Play(Synth.DrumTom, 0.7f, 0.7f);
                    else audio.Play(Synth.DrumHat, 0.5f, 0.8f);
                    break;
                case DuelEv.PlayerWallBounce: audio.Play(Synth.SfxHook, 0.6f, 0.6f); break;
                case DuelEv.PhaseBreak: audio.Play(Synth.SfxFinish, 0.8f, 0.8f); break;
                case DuelEv.PhaseStart: audio.Play(Synth.SfxGate, 0.8f, 1.2f); break;
                case DuelEv.KO: audio.Play(Synth.SfxFinish, 1f); break;
            }
        }

        /// <summary>동료 사격 광선 잔상(동료 → 쏜 곳).</summary>
        StageWorld.Swing AllyBeam(DuelEvent e, bool strike)
        {
            var a = world.D.A;
            return new StageWorld.Swing { kind = 3, combo = strike ? 3 : 1, x = a.MuzzleX, y = a.MuzzleY, x2 = e.X, y2 = e.Y, dur = strike ? 0.28f : 0.16f, hit = a.AimHit };
        }

        VisualElement stageView;

        public void Show()
        {
            IsOpen = true;
            el.style.display = DisplayStyle.Flex;
            el.BringToFront();
            if (chart == null)
            {
                // 마지막에 쓴 차트(없거나 못 열면 내장 테스트 차트)
                string last = KitRoot.Settings.RhythmChart;
                if (string.IsNullOrEmpty(last) || !ChartStore.List().Contains(last)) last = ChartStore.TEST;
                try { chart = ChartStore.Load(last); chartName = last; }
                catch (Exception) { try { chart = ChartStore.Load(ChartStore.TEST); chartName = ChartStore.TEST; } catch (Exception e) { note.text = "테스트 곡을 열지 못했어요: " + e.Message; } }
                chart?.Notes.RemoveAll(x => x.K == Mori.Core.Rhythm.Lane.FINISH);
            }
            RefreshSongs();
            ApplyVol();
            Reset();
            LoadClip();
            ApplyScale();
            play = true; SetPlay(false);
            ActInput.Hook();
            editor.Show();
            note.text = ArtLib.LastError != null ? "읽기 오류: " + ArtLib.LastError : $"그림 폴더: {ArtLib.Dir}";
        }

        public void Hide()
        {
            if (!IsOpen) return;
            SetPlay(false);
            SetFull(false);
            tune.Hide();
            editor.Hide();
            notes.Hide();
            StopClock();
            if (audio.Music != null) audio.Music.loop = false;
            IsOpen = false;
            el.style.display = DisplayStyle.None;
            cards.Close();
            field.Fx.Dir.StopAll();
            field.Fx.Update(null, 0, 0, 1, 1, Vector2.zero, 0);
            field.World = null;
            world = null;
        }

        /// <summary>킷을 닫을 때: 편집 중인 그림이 바뀌었으면 저장.</summary>
        public void SaveAll() { try { editor?.SaveIfDirty(); } catch (System.Exception e) { Debug.LogWarning("[Mori 킷] 닫을 때 저장 실패: " + e.Message); } }

        public void OnUpdate(float dt)
        {
            TickHats();
            if (allyBtn != null) allyBtn.EnableInClassList("ae-btn--on", TuneStore.Current.AllyOn >= 0.5);     // F2 에서 바꿔도 버튼이 따라감
            if (!IsOpen) return;
            if (notes.IsOpen)
            {
                // 노트 에디터가 열린 동안: 에디터만(미리보기 결투 · 도트 편집기는 멈춤)
                editor.KeysOff = true;
                notes.OnUpdate(dt);
                return;
            }
            // F2 = 튜닝 패널 · F4 = 전체화면 조작 켜고 끄기(값 입력 중엔 무시)
            bool typing = tune.Typing || editor.TextFocus;
            if (!typing && Keys.Down(Keys.K.F2)) tune.Toggle();
            if (!typing && Keys.Down(Keys.K.F4)) ToggleF4();
            if (!typing && Keys.Down(Keys.K.F5)) { Reset(); note.text = "F5 — 처음부터 다시"; }      // 결투 · 곡 처음부터
            tune.Tick();
            editor.KeysOff = play || tune.Typing || tuneCtl;      // 미리보기 조작 · 튜닝 패널 조작 중엔 편집기 단축키 끔
            editor.Tick(dt);
            chains.Tick();
            fx.Tick();
            cards.Tick(tab == 3, dt);
            if (world == null) return;
            if (!play) { }                       // 조작하지 않는 동안 결투 시계는 멈춤(그림 애니메이션은 따로 계속)
            else if (audioClock)
            {
                // 오디오 시계: 프레임 시간으로 부드럽게 흐르다가 많이 어긋나면 오디오 시각으로 맞춤
                double dspT = AudioSettings.dspTime - dspStart;
                smooth += dt;
                double err = dspT - smooth;
                if (Math.Abs(err) > 0.08) smooth = dspT; else smooth += err * 0.1;
                LoopTick(dspT);
                t = Math.Max(0, smooth - KitRoot.Settings.RhythmOffsetMs / 1000.0);
            }
            else t += dt;
            if (play && !tune.Typing)
            {
                // 실제 결투처럼: 방향키 이동 · 동작 키(키 설정 그대로)
                int dir = (ActInput.Right ? 1 : 0) - (ActInput.Left ? 1 : 0);
                int vdir = (ActInput.Up ? 1 : 0) - (ActInput.Down ? 1 : 0);
                ActInput.Take(presses);
                var d = world.D;
                if (freeMode) Refill(d);
                foreach (var pr in presses)
                {
                    double pt = t - Math.Min(pr.Age, 0.25);
                    bool ok;
                    switch (pr.Act)
                    {
                        case 0: ok = d.PressAttack(pt); break;
                        case 1: ok = d.PressCounter(pt); break;
                        case 2: ok = d.PressDash(pt, dir, vdir); break;
                        case 4: ok = d.PressUlt(pt); break;
                        default: ok = d.PressJump(pt, dir, vdir < 0); break;
                    }
                    // 박 판정 · 박 표시 = 누른 순간. 동작 소리는 실제로 나가는 순간(아래 이벤트) — 버퍼 · 재사용 대기로 늦게 나가도 어긋나지 않게
                    if (ok)
                    {
                        pressBeat[Mathf.Clamp(pr.Act, 0, 4)] = d.LastOnBeat;
                        if (d.LastOnBeat) hud.OnBeat();
                        if (pr.Act == 0 && KitRoot.Settings.AtkSoundOnPress) AtkMagicSound(d.LastOnBeat);    // 예전 연타 소리: 누를 때마다 스네어
                    }
                    else hud.Refused(pr.Act);
                }
                StageField.ForceHero = StageField.ForceFoe = null;       // 조작 중엔 실제 상태대로
                world.Tick(dt, t, dir, true);
                if (freeMode) Refill(world.D);
                var p = d.P;
                foreach (var (ev, combo) in p.Events)
                {
                    int act = ev == FsmEv.Slash ? 0 : ev == FsmEv.CounterStart ? 1 : ev == FsmEv.Dash || ev == FsmEv.BackDash ? 2 : ev == FsmEv.Jump || ev == FsmEv.DoubleJump ? 3 : -1;
                    if (act == 0) { if (!KitRoot.Settings.AtkSoundOnPress) AtkMagicSound(pressBeat[0]); }     // 마법 공격(검기) = 스네어(‘나갈 때’ 설정이면 여기서)
                    else if (act >= 0) Drum(act, pressBeat[act]);         // 동작 소리 = 실제로 나간 횟수만큼
                    // 기계 공격 소리는 따로 내지 않음 — 공격 소리 한 덩어리(스네어 → 지연 뒤 하이햇)에 들어 있음
                    if (ev == FsmEv.Mech && d.AllyOn) { }                 // 동료가 쏨 — 잔상은 AllyShot 에서 동료 자리에
                    else if (ev == FsmEv.Slash || ev == FsmEv.Mech)
                        world.Swings.Add(new StageWorld.Swing { kind = ev == FsmEv.Slash ? 0 : 1, combo = combo, x = p.X + p.AtkDir * 0.9 * FightUnit.S, y = p.Cy, dx = p.AtkDir, dy = 0, box = p.AttackBox(combo), hasBox = ev == FsmEv.Slash });
                    else if (ev == FsmEv.CounterAtk)
                        world.Swings.Add(new StageWorld.Swing { kind = 2, x = p.X, y = p.Cy, dur = 0.3f, box = p.CounterBox(), hasBox = true });
                }
                foreach (var e in d.Events) Sfx(e);
                hud.OnEvents(p.Events, d.Events);                  // 글자 알림 · 코드 이펙트 · 섬광 · 멈칫
                Cues(t);                                           // 예고음(결투자 타격 1박 전)
                // 그루브 음악 필터: 그루브가 낮으면 음악이 살짝 먹먹하고, 박에 맞춰 칠수록 열린다
                var T = d.T;
                if (T.GrooveFilter >= 0.5) audio.SetCutoff(Mathf.Lerp(Mathf.Clamp((float)T.GrooveFilterLow, 10f, 22000f), 22000f, Mathf.Clamp01((float)(d.Groove / FightTune.Pos(T.GrooveFilterFull)))));
                else audio.SetCutoff(22000);
                field.Fx.OnEvents(world, p.Events, d.Events);      // 순간마다 연결한 프리팹
                d.ClearFrame();
            }
            else
            {
                ActInput.Take(presses);        // 편집 중 눌린 키는 버림
                world.Tick(dt, t, 0, false);       // 결투는 멈춤(연출만 흐름) — 편집 중엔 시계도 멈춰 있음
            }
            field.Guide = PreviewHud.On("range");
            StageField.ShowBoxes = PreviewHud.On("boxes");
            StageField.ShowAtkRange = PreviewHud.On("atkrange");
            StageField.ShowSwing = PreviewHud.On("swing");
            field.Repaint();
            hud.Tick(dt, t, chart);
            if (world.D.Over) { overT += dt; if (overT > 3) Reset(); }
            if (ArtLib.LastError != null) note.text = "읽기 오류: " + ArtLib.LastError;
        }
    }
}
