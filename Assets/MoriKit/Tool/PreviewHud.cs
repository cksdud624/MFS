using System;
using System.Collections.Generic;
using System.Globalization;
using Mori.Core.Fight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 작업실 미리보기 위에 얹는 Mori 결투 화면(StageScreen) 그대로의 화면 정보 · 연출:
    ///  · 글자 알림(데미지 · 카운터! · 조금 빨랐어요 …) · 큰 알림(페이즈 · 맵 패턴 · K.O.)
    ///  · 코드 이펙트(불꽃 · 고리 · 빛줄기 · X) · 섬광 · 피격 테두리 · 멈칫(히트스톱) · 흔들림
    ///  · 체력 · 점수(왼쪽 위) · 곡 · 페이즈 · 결투자 체력(가운데 위) · 콤보 · 대쉬 · 카운터 충전 · 그루브 · 조작 안내
    /// 모두 1080p 기준 크기로 만든 뒤 미리보기 크기에 맞춰 줄여 보인다(작은 미리보기에서도 게임과 같은 비율).
    /// 항목마다 켜고 끌 수 있다(PlayerPrefs 에 기억).
    /// </summary>
    public sealed class PreviewHud
    {
        // ───────── 켜고 끄기 ─────────
        public static readonly (string id, string name, string tip, bool def)[] TOGGLES =
        {
            ("range", "범위", "결투자 공격이 닿을 위험 구역 · 예고 범위", true),
            ("atkrange", "공격 범위", "플레이어 공격 · 카운터 때 잠깐 번쩍이는 실제 판정 범위(거리 감각 잡기용) — 꺼도 검기 같은 잔상은 그대로", true),
            ("swing", "공격 잔상", "코드로 그린 동작 잔상: 검기(푸른 호) · 기계 공격(흰 줄) · 카운터 고리 — 연출 카드 · 그림 이펙트는 그대로", true),
            ("boxes", "판정", "몸판정 · 공격 판정 · 카운터 판정 상자(그림과 판정 맞추기)", false),
            ("text", "글자", "데미지 숫자 · 카운터! · 조금 빨랐어요 · 회피 같은 글자 알림", true),
            ("banner", "큰 알림", "페이즈 · 단계 · 맵 패턴 · K.O. 같은 가운데 큰 알림", true),
            ("fx", "코드 이펙트", "불꽃 · 고리 · 빛줄기 · X 같은 간단 이펙트", true),
            ("flash", "섬광", "화면 섬광 · 맞았을 때 붉은 테두리", true),
            ("feel", "멈칫 · 흔들림", "타격 · 카운터 때 멈칫(히트스톱) · 화면 흔들림", true),
            ("hp", "체력", "왼쪽 위 플레이어 체력 · 점수", true),
            ("top", "상단", "가운데 위 곡 진행 · 페이즈 · 결투자 체력", true),
            ("combo", "콤보", "오른쪽 콤보 숫자", true),
            ("cd", "충전", "오른쪽 아래 대쉬 · 카운터 남은 횟수 · 충전 · 쿨타임", true),
            ("groove", "그루브", "아래 가운데 그루브 막대", true),
            ("keys", "조작 안내", "아래 조작 키 안내", true),
        };
        static readonly Dictionary<string, bool> on = new Dictionary<string, bool>();

        public static bool On(string id)
        {
            if (on.TryGetValue(id, out var v)) return v;
            bool def = true;
            foreach (var t in TOGGLES) if (t.id == id) def = t.def;
            v = PlayerPrefs.GetInt("mori.kit.hud." + id, def ? 1 : 0) == 1;
            on[id] = v;
            return v;
        }

        public static void Set(string id, bool v)
        {
            on[id] = v;
            PlayerPrefs.SetInt("mori.kit.hud." + id, v ? 1 : 0);
        }

        // ───────── 화면 ─────────
        public readonly VisualElement Root;
        /// <summary>결투자 단계가 바뀜(true = 격노) — 작업실이 소리를 냄.</summary>
        public Action<bool> OnStage;
        readonly VisualElement box, hud, popLayer, hurtEl, flash, banner, hpCard, top, comboBox, legend, cdBox, grooveBox;
        readonly FxField fx;
        readonly Label hpL, scoreL, titleL, waveL, comboL, comboK, bannerL, bannerS, foeL, dashP, dashT, ctrP, ctrT, grooveL, ultP, ultT;
        readonly VisualElement hpFill, progFill, foeFill, dashRow, ctrRow, dashFill, ctrFill, grooveFill, ultRow, ultFill;
        readonly List<(Label l, Func<string> text)> keyLabels = new List<(Label, Func<string>)>();
        readonly List<(Label l, float age, Vector2 at)> pops = new List<(Label, float, Vector2)>();
        int keyVer = -1, phaseShown = -1;
        float bannerAge = 9, bannerDur = 2, flashA, hurtA, comboPop, k = 1;
        float W = 1920, H = 1080;
        StageWorld world;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        static readonly Color Gold = new Color(1f, 0.82f, 0.3f), Magenta = new Color(1f, 0.32f, 0.86f), Soft = new Color(0.82f, 0.85f, 0.92f);

        public PreviewHud()
        {
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.left = 0; Root.style.top = 0; Root.style.right = 0; Root.style.bottom = 0;
            Root.style.overflow = Overflow.Hidden;
            // 1080p 크기의 판을 미리보기 크기에 맞춰 줄임(왼쪽 위 기준)
            box = E(Root, null);
            box.style.position = Position.Absolute;
            box.style.left = 0; box.style.top = 0;
            box.style.transformOrigin = new TransformOrigin(0, 0);

            fx = new FxField();
            fx.style.position = Position.Absolute;
            fx.style.left = 0; fx.style.top = 0; fx.style.right = 0; fx.style.bottom = 0;
            box.Add(fx);
            popLayer = E(box, "sg-pops");
            hurtEl = E(box, "kp-hurt");
            flash = E(box, "sg-flash");

            hud = E(box, "sg-hud");
            hpCard = E(hud, "sg-card sg-hp");
            var hr = E(hpCard, "sg-row");
            L(hr, "HP", "sg-k");
            hpL = L(hr, "100", "sg-v");
            var hpBar = E(hpCard, "sg-bar");
            hpFill = E(hpBar, "sg-bar-i");
            scoreL = L(hpCard, "0", "sg-score");
            top = E(hud, "sg-top");
            titleL = L(top, "", "sg-title");
            var prog = E(top, "sg-prog");
            progFill = E(prog, "sg-prog-i");
            waveL = L(top, "", "sg-wave");
            foeL = L(top, "결투자", "sg-foe-k");
            var fb = E(top, "sg-foebar");
            foeFill = E(fb, "sg-foebar-i");
            comboBox = E(hud, "sg-combo");
            comboL = L(comboBox, "", "sg-combo-n");
            comboK = L(comboBox, "COMBO", "sg-combo-k");
            legend = E(hud, "sg-legend");
            for (int a = 0; a < Act.COUNT; a++)
            {
                var chip = E(legend, "sg-chip");
                chip.style.borderLeftColor = StageLook.ACT[a];
                int aa = a;
                var kl = L(chip, KeyBinds.Label(a), "sg-chip-k");
                keyLabels.Add((kl, () => KeyBinds.Label(aa)));
                kl.style.color = StageLook.ACT[a];
                L(chip, Act.NAME[a], "sg-chip-n");
            }
            var uchip = E(legend, "sg-chip");
            var ucol = new Color(1f, 0.6f, 0.25f);
            uchip.style.borderLeftColor = ucol;
            var ukl = L(uchip, KeyBinds.Label(KeyBinds.ULT), "sg-chip-k");
            ukl.style.color = ucol;
            keyLabels.Add((ukl, () => KeyBinds.Label(KeyBinds.ULT)));
            L(uchip, "궁극기", "sg-chip-n");
            var dchip = E(legend, "sg-chip");
            keyLabels.Add((L(dchip, KeyBinds.LabelDirs(), "sg-chip-k"), KeyBinds.LabelDirs));
            L(dchip, "이동 · 조준 · 대쉬 방향", "sg-chip-n");
            foreach (var (kk, nn) in new[] { ("Esc", "편집으로"), ("F2", "전투 튜닝"), ("F4", "전체화면 조작"), ("F5", "처음부터") })
            {
                var c = E(legend, "sg-chip");
                L(c, kk, "sg-chip-k");
                L(c, nn, "sg-chip-n");
            }
            cdBox = E(hud, "sg-cd");
            dashRow = CdRow(cdBox, "대쉬", "", out dashP, out dashFill, out dashT);
            ctrRow = CdRow(cdBox, "카운터", "", out ctrP, out ctrFill, out ctrT);
            ultRow = CdRow(cdBox, "궁극기", "", out ultP, out ultFill, out ultT);
            keyLabels.Add((dashRow.Q<Label>(className: "sg-cd-key"), () => KeyBinds.Label(KeyBinds.DASH)));
            keyLabels.Add((ctrRow.Q<Label>(className: "sg-cd-key"), () => KeyBinds.Label(KeyBinds.CTR)));
            grooveBox = E(hud, "sg-groove");
            grooveL = L(grooveBox, "GROOVE ×1.00", "sg-groove-k");
            var gb = E(grooveBox, "sg-groove-bar");
            grooveFill = E(gb, "sg-groove-i");

            banner = E(box, "sg-banner");
            bannerL = L(banner, "", "sg-banner-t");
            bannerS = L(banner, "", "sg-banner-s");
            banner.style.display = DisplayStyle.None;
            foreach (var x in new[] { popLayer, hurtEl, flash, banner, hud, box }) x.pickingMode = PickingMode.Ignore;
            SetPicking(Root);
        }

        static void SetPicking(VisualElement v) { v.pickingMode = PickingMode.Ignore; foreach (var c in v.Children()) SetPicking(c); }

        static VisualElement CdRow(VisualElement parent, string name, string key, out Label pips, out VisualElement fill, out Label time)
        {
            var row = E(parent, "sg-cd-row");
            var tp = E(row, "sg-cd-top");
            L(tp, name, "sg-cd-k");
            L(tp, key, "sg-cd-key");
            pips = L(tp, "", "sg-cd-p");
            time = L(tp, "", "sg-cd-t");
            var bar = E(row, "sg-cd-bar");
            fill = E(bar, "sg-cd-i");
            return row;
        }

        static VisualElement E(VisualElement p, string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            if (cls != null) foreach (var c in cls.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
            p.Add(e);
            return e;
        }

        static Label L(VisualElement p, string t, string cls)
        {
            var l = new Label(t) { pickingMode = PickingMode.Ignore };
            if (cls != null) foreach (var c in cls.Split(' ')) if (c.Length > 0) l.AddToClassList(c);
            p.Add(l);
            return l;
        }

        static void Show(VisualElement e, bool v) => e.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
        static float EOut(float t) => 1 - (1 - t) * (1 - t) * (1 - t);

        /// <summary>새 결투(처음 위치로 · 결투자 K.O. 뒤).</summary>
        public void Reset(StageWorld w)
        {
            world = w;
            foreach (var p in pops) p.l.RemoveFromHierarchy();
            pops.Clear();
            fx.Reset();
            flashA = hurtA = comboPop = 0;
            bannerAge = 9;
            Show(banner, false);
            phaseShown = -1;
        }

        // ───────── 매 프레임 ─────────
        /// <summary>크기 맞추기 · 글자 · 이펙트 · 섬광 흐르기 · 화면 정보 채우기. t = 결투 시각(초), chart = 곡(진행 막대).</summary>
        public void Tick(float dt, double t, Mori.Core.Rhythm.RhythmChart chart)
        {
            var r = Root.contentRect;
            if (r.width >= 16 && r.height >= 16 && !float.IsNaN(r.width))
            {
                W = r.width; H = r.height;
                k = H / 1080f;
                box.style.width = W / k; box.style.height = 1080;
                box.style.scale = new Scale(new Vector3(k, k, 1));
            }
            if (keyVer != KeyBinds.Version) { keyVer = KeyBinds.Version; foreach (var (l, f) in keyLabels) if (l != null) l.text = f(); }
            Show(fx, On("fx")); Show(popLayer, On("text")); Show(hurtEl, On("flash")); Show(flash, On("flash"));
            Show(hpCard, On("hp")); Show(top, On("top")); Show(comboBox, On("combo")); Show(cdBox, On("cd")); Show(grooveBox, On("groove")); Show(legend, On("keys"));
            fx.Tick(dt);
            PopsTick(dt);
            BannerTick(dt);
            flashA = Mathf.Max(0, flashA - dt * 2.5f);
            flash.style.opacity = Mathf.Clamp01(flashA * (world != null ? (float)world.D.T.FlashMul : 1f));
            hurtA = Mathf.Max(0, hurtA - dt * 1.8f);
            hurtEl.style.opacity = hurtA;
            comboPop = Mathf.Max(0, comboPop - dt * 5);
            if (world == null) return;
            var d = world.D;
            // 단계(체력바 안 구간)가 바뀌면 큰 알림
            if (d.PhaseBreaking) phaseShown = -1;
            else if (phaseShown < 0) phaseShown = d.Phase;
            else if (d.Phase != phaseShown)
            {
                phaseShown = d.Phase;
                bool rage = phaseShown == 2;
                OnStage?.Invoke(rage);
                BannerShow(rage ? "격노" : "2단계", rage ? "결투자가 격노했다 — 카운터 자세 · 연타가 잦아진다" : "결투자가 더 거칠어진다 — 3연격 · 내려찍기", 2f, rage);
                Flash(rage ? new Color(1f, 0.35f, 0.15f) : new Color(0.6f, 0.85f, 1f), rage ? 0.55f : 0.35f);
            }
            Hud(t, chart);
        }

        // 화면 px(미리보기) → 1080p 판 좌표
        Vector2 Px(double x, double y) => world.ToScreen(x, y, W, H) / k;
        Vector2 HeroPx(float up) => Px(world.D.P.X, world.D.P.Y + up * FightUnit.S);
        Vector2 FoePx(float up) => Px(world.D.E.X, world.D.E.Y + up * FightUnit.S);
        float PxU => world.PxU(W, H) / k;

        void Flash(Color c, float a, bool max = false)
        {
            flashA = max ? Mathf.Max(flashA, a) : a;
            flash.style.backgroundColor = c;
        }

        void Feel(float hitStop, float shake, bool maxStop = true)
        {
            if (!On("feel")) return;
            if (hitStop > 0) world.HitStop = maxStop ? Mathf.Max(world.HitStop, hitStop) : hitStop;
            if (shake > 0) world.Shake = Mathf.Max(world.Shake, shake);
        }

        /// <summary>박에 맞춘 동작(공격 · 카운터 · 대쉬 · 점프) — 금빛 고리.</summary>
        public void OnBeat()
        {
            if (world != null) fx.Ring(HeroPx(0.9f), Gold, 10, 70, 0.25f, 2);
        }

        /// <summary>누른 동작이 막혔을 때(충전 중 · 쿨타임).</summary>
        public void Refused(int act)
        {
            if (world == null) return;
            var p = world.D.P;
            if (!p.CanAct) return;
            if (act == 1) Pop(p.CtrStack < 1 ? "카운터 충전 중" : p.CtrCd > 0 ? "카운터 쿨타임" : "", HeroPx(2.3f), Soft);
            else if (act == 2 && p.DashStack < 1) Pop("대쉬 충전 중", HeroPx(2.3f), Soft);
        }

        /// <summary>플레이어 상태 기계 사건 · 결투 사건(그림만 — 소리는 작업실이 따로).</summary>
        public void OnEvents(List<(FsmEv ev, int combo)> pev, List<DuelEvent> dev)
        {
            if (world == null) return;
            foreach (var (ev, combo) in pev) OnFsm(ev);
            foreach (var e in dev) OnDuel(e);
        }

        void OnFsm(FsmEv ev)
        {
            var p = world.D.P;
            switch (ev)
            {
                case FsmEv.DoubleJump:
                case FsmEv.Land:
                    fx.Ring(HeroPx(0), new Color(0.7f, 0.8f, 1f), 6, 40, 0.25f, 2);
                    break;
                case FsmEv.Dash:
                case FsmEv.BackDash:
                    fx.Sparks(HeroPx(0.9f), new Color(1f, 0.85f, 0.4f), 8, p.Facing > 0 ? 180 : 0, 40, 120, 360, 0.3f, 2.2f, true, 0);
                    break;
                case FsmEv.Turn:
                    fx.Sparks(HeroPx(0.1f), new Color(0.8f, 0.85f, 1f), 5, p.Facing > 0 ? 180 : 0, 40, 80, 220, 0.25f, 2f, false, 300);
                    break;
                case FsmEv.WallBounce:
                case FsmEv.NearWallBounce:
                    Feel(0, 10);
                    break;
            }
        }

        void OnDuel(DuelEvent e)
        {
            var d = world.D;
            var at = Px(e.X, e.Y);
            switch (e.Kind)
            {
                case DuelEv.PatternStart:
                    Pop(e.Text, FoePx(3.0f), e.Act == 1 ? StageLook.KindColor(HitKind.Melee) : StageLook.ThreatColor(d.E.Cur));
                    if (d.E.Cur != null && d.E.Cur.Counter && e.Act == 0)
                    {
                        fx.Ring(FoePx(1f), StageLook.Stance, 30, 160, 0.45f, 5);
                        Flash(StageLook.Stance, 0.15f, true);
                    }
                    break;
                case DuelEv.Hit:
                    if (e.Act < 0) fx.Ring(at, StageLook.Stance, 20, (float)e.Value * PxU, 0.3f, 5);
                    else fx.Sparks(at, StageLook.KindColor((HitKind)e.Act), 8, 90, 160, 60, 240, 0.25f, 2f, true, 0);
                    break;
                case DuelEv.Damage:
                {
                    bool beat = e.Text != null;
                    var c = e.Act == 4 ? new Color(1f, 0.6f, 0.25f) : e.Act == 1 ? Magenta : e.Act == 3 ? Magenta : e.Act == 2 ? new Color(0.9f, 0.95f, 1f) : new Color(0.3f, 0.9f, 1f);
                    Pop(((int)Math.Round(e.Value)).ToString() + (beat ? " ♪" : ""), at + new Vector2(UnityEngine.Random.Range(-24f, 24f), 0), beat ? Gold : c);
                    fx.Sparks(at, beat ? Gold : c, beat ? 14 : 8, d.P.AtkDir > 0 ? 0 : 180, 70, 160, 600, 0.35f, 2.6f, true, 300);
                    world.FoeHitT = 0;
                    Feel(beat ? 0.05f : 0.03f, 0);
                    if (beat) { fx.Ring(at, Gold, 6, 70, 0.3f, 3); comboPop = 1; }
                    break;
                }
                case DuelEv.CounterSuccess:
                    Pop(e.Text, FoePx(2.8f), Magenta);
                    fx.Ring(at, Magenta, 10, 260, 0.5f, 7);
                    fx.Ring(at, Color.white, 6, 140, 0.3f, 3);
                    fx.Rays(at, Magenta, 16, 30, 320, 0.45f, 4, 20);
                    fx.Sparks(at, Magenta, 34, 90, 180, 200, 900, 0.7f, 3.4f, true, 400);
                    Feel(0.14f, 16, false);
                    Flash(new Color(1f, 0.6f, 0.95f), 0.45f);
                    comboPop = 1;
                    break;
                case DuelEv.WallBounce:
                    Pop(e.Text, FoePx(2.4f), Gold);
                    fx.Ring(at, Gold, 10, 180, 0.4f, 6);
                    fx.Sparks(at, Gold, 20, 90, 180, 150, 600, 0.5f, 3f, false, 600);
                    Feel(0.07f, 14);
                    break;
                case DuelEv.EnemyCounter:
                    Pop(e.Text, HeroPx(2.6f), StageLook.Hurt);
                    Pop("−" + (int)e.Value, HeroPx(1.9f) + new Vector2(30, 0), StageLook.Hurt);
                    fx.Ring(at, StageLook.Hurt, 10, 200, 0.45f, 6);
                    fx.Cross(at, StageLook.Hurt, 34, 0.45f);
                    hurtA = 0.9f;
                    Feel(0, 18);
                    world.HeroHurtT = 0;
                    break;
                case DuelEv.CounterCrash:
                    Pop(e.Text, at + new Vector2(0, -20), e.Act == 1 ? Gold : Color.white);
                    if (e.Act == 1) { fx.Ring(at, Gold, 20, 360, 0.6f, 6); comboPop = 1; }
                    fx.Ring(at, Color.white, 12, 300, 0.5f, 8);
                    fx.Rays(at, Magenta, 20, 30, 380, 0.5f, 4, 30);
                    Feel(0.16f, 18, false);
                    Flash(Color.white, 0.6f);
                    break;
                case DuelEv.AllyStrike:
                    fx.Ring(at, new Color(0.62f, 0.88f, 1f), 10, 220, 0.35f, 5);
                    fx.Sparks(at, Color.white, 10, d.A.Facing > 0 ? 0 : 180, 80, 180, 600, 0.3f, 2.4f, true, 300);
                    Feel(e.Value > 0 ? 0 : 0.06f, 12);
                    break;
                case DuelEv.AllyShot:
                    if (d.A.AimHit) fx.Sparks(at, new Color(0.62f, 0.88f, 1f), 5, d.A.Facing > 0 ? 0 : 180, 60, 120, 400, 0.2f, 2f, true, 200);
                    break;
                case DuelEv.CounterBounce:
                    Pop(e.Text, at + new Vector2(0, -20), Soft);
                    fx.Ring(at, Soft, 8, 160, 0.35f, 4);
                    fx.Ring(at, StageLook.Stance, 6, 110, 0.3f, 3);
                    Feel(0.06f, 8);
                    break;
                case DuelEv.PlayerHurt:
                    Pop("−" + (int)e.Value, HeroPx(1.9f) + new Vector2(30, 0), StageLook.Hurt);
                    if (e.Text != null) Pop(e.Text, HeroPx(2.6f), Soft);
                    fx.Sparks(HeroPx(1f), StageLook.Hurt, 16, 90, 180, 120, 500, 0.5f, 3f, false, 800);
                    hurtA = 0.7f;
                    Feel(0, 10);
                    world.HeroHurtT = 0;
                    break;
                case DuelEv.Nullified:
                    Pop(e.Text, at, Soft);
                    fx.Ring(at, Soft, 4, 50, 0.25f, 2);
                    break;
                case DuelEv.Land:
                    fx.Ring(at, StageLook.KindColor(HitKind.Low), 10, 260, 0.45f, 5);
                    Feel(0, 10);
                    break;
                case DuelEv.EnemyCounterStart:
                    Pop(e.Text, FoePx(3.0f), StageLook.Stance);
                    fx.Ring(FoePx(1f), StageLook.Stance, 20, 120, 0.3f, 4);
                    break;
                case DuelEv.CounterTell:
                    Pop(e.Text, FoePx(3.3f), StageLook.Stance, 1.6f);
                    fx.Ring(FoePx(1.1f), Color.white, 10, 150, 0.3f, 5);
                    fx.Ring(FoePx(1.1f), StageLook.Stance, 20, 220, 0.45f, 6);
                    fx.Rays(FoePx(1.1f), StageLook.Stance, 14, 20, 140, 0.35f, 3, 0);
                    Flash(StageLook.Stance, 0.28f, true);
                    Feel(0.05f, 0);
                    break;
                case DuelEv.SpamReact:
                    Pop(e.Text == "!" ? "카운터!" : e.Text, FoePx(3.3f), StageLook.Stance, 1.6f);
                    fx.Ring(FoePx(1f), StageLook.Stance, 30, 160, 0.45f, 5);
                    Flash(StageLook.Stance, 0.18f, true);
                    break;
                case DuelEv.MapStart:
                {
                    var mk = (MapKind)(int)e.Value;
                    BannerShow(Duel.MapName(mk) + (e.Act == 1 ? "  · 결투자 무적" : ""), Duel.MapHint(mk), 2.2f, true);
                    Flash(new Color(1f, 0.55f, 0.25f), 0.3f);
                    break;
                }
                case DuelEv.MapStrike:
                {
                    var mk = (MapKind)(int)e.Value;
                    if (mk == MapKind.Columns) Feel(0, 7);
                    else if (mk == MapKind.Lasers) Feel(0, 5);
                    else if (mk != MapKind.Walls) fx.Ring(at, new Color(0.4f, 0.9f, 1f), 4, 50, 0.25f, 2);
                    break;
                }
                case DuelEv.MapEnd:
                    Pop("맵 패턴 끝 — 공격 기회!", FoePx(3.0f), Gold);
                    break;
                case DuelEv.Immune:
                    Pop(e.Text, at, new Color(1f, 0.85f, 0.4f));
                    break;
                case DuelEv.Evade:
                    Pop(e.Text, at, Soft);
                    fx.Sparks(FoePx(0.3f), Soft, 6, d.E.Facing > 0 ? 180 : 0, 30, 80, 200, 0.25f, 2f, false, 300);
                    break;
                case DuelEv.PlayerWallBounce:
                    Pop(e.Text, HeroPx(2.4f), StageLook.Hurt);
                    fx.Ring(at, StageLook.Hurt, 10, 140, 0.35f, 4);
                    break;
                case DuelEv.PhaseBreak:
                    fx.Ring(at, Color.white, 20, 420, 0.6f, 9);
                    fx.Ring(at, Gold, 30, 300, 0.5f, 6);
                    fx.Sparks(at, Gold, 40, 90, 160, 180, 900, 0.8f, 3f, false, 600);
                    Flash(new Color(1f, 0.85f, 0.4f), 0.6f);
                    Feel(0.12f, 0);
                    bool lastPh = (int)e.Value >= d.PhaseCount;
                    BannerShow(e.Text, lastPh ? "마지막 페이즈 — 더 빠르고 더 세다. 쓰러뜨리면 끝" : "체력바가 다시 찼다 — 더 빠르고 더 세진다", 2.6f, lastPh);
                    break;
                case DuelEv.UltStart:
                    BannerShow("ORBITAL STRIKE", "궤도 전함에 폭격 요청 — 목표 포착", Mathf.Max(0.8f, (float)d.T.UltCut), true);
                    Flash(new Color(1f, 0.6f, 0.25f), 0.35f);
                    fx.Ring(at, new Color(1f, 0.45f, 0.2f), 30, 220, 0.6f, 4);
                    break;
                case DuelEv.UltStrike:
                    fx.Ring(at, new Color(1f, 0.55f, 0.2f), 10, (float)(d.T.UltWidth * 2) * PxU, 0.35f, 6);
                    fx.Sparks(at, new Color(1f, 0.8f, 0.4f), 16, 90, 140, 200, 700, 0.5f, 3f, false, 400);
                    Feel(0.02f, e.Value > 0 ? 10 : 5, false);
                    break;
                case DuelEv.PhaseStart:
                    Pop("시작!", at, Gold, 1.4f);
                    fx.Ring(at, Gold, 10, 200, 0.4f, 5);
                    break;
                case DuelEv.KO:
                    fx.Ring(at, Color.white, 20, 460, 0.7f, 10);
                    fx.Sparks(at, Gold, 60, 90, 180, 200, 1100, 1f, 3.8f, false, 700);
                    Flash(new Color(1f, 0.85f, 0.4f), 0.8f);
                    BannerShow("K.O.", "결투자를 쓰러뜨렸다 — 잠시 뒤 처음 위치로", 2.2f);
                    break;
            }
        }

        // ───────── 글자 알림 · 큰 알림 ─────────
        void Pop(string text, Vector2 at, Color c, float size = 1)
        {
            if (string.IsNullOrEmpty(text)) return;
            var l = L(popLayer, text, "sg-pop");
            l.style.color = c;
            l.userData = size;
            pops.Add((l, 0, at));
        }

        void PopsTick(float dt)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (l, age, at) = pops[i];
                age += dt;
                if (age > 0.8f) { l.RemoveFromHierarchy(); pops.RemoveAt(i); continue; }
                pops[i] = (l, age, at);
                float t = age / 0.8f;
                float sc = (age < 0.1f ? Mathf.Lerp(1.6f, 1f, age / 0.1f) : 1f) * (l.userData is float m ? m : 1f);
                l.style.left = at.x - 80;
                l.style.top = at.y - 40 * EOut(t);
                l.style.opacity = 1 - t * t;
                l.style.scale = new Scale(new Vector2(sc, sc));
            }
        }

        void BannerShow(string t, string sub, float dur, bool warn = false)
        {
            if (!On("banner")) return;
            bannerL.text = t;
            bannerS.text = sub;
            banner.EnableInClassList("sg-banner--warn", warn);
            Show(banner, true);
            bannerAge = 0;
            bannerDur = dur;
        }

        void BannerTick(float dt)
        {
            if (!On("banner")) { Show(banner, false); return; }
            if (bannerAge > bannerDur + 0.5f) return;
            bannerAge += dt;
            float a = bannerAge < 0.2f ? bannerAge / 0.2f : bannerAge > bannerDur ? 1 - (bannerAge - bannerDur) / 0.5f : 1;
            banner.style.opacity = Mathf.Clamp01(a);
            float sx = bannerAge < 0.25f ? Mathf.Lerp(1.5f, 1f, EOut(bannerAge / 0.25f)) : 1;
            banner.style.scale = new Scale(new Vector2(sx, 1));
            if (bannerAge > bannerDur + 0.5f) Show(banner, false);
        }

        // ───────── 화면 정보 ─────────
        static string Pips(double n, int cap)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cap; i++) sb.Append(i < (int)Math.Floor(n + 1e-9) ? "●" : "○");
            return sb.ToString();
        }

        void Hud(double t, Mori.Core.Rhythm.RhythmChart chart)
        {
            var duel = world.D;
            hpL.text = duel.PlayerHp.ToString();
            hpFill.style.width = Length.Percent(duel.PlayerHp * 100f / Math.Max(1, duel.MaxPlayerHp));
            hpFill.style.backgroundColor = duel.PlayerHp * 2 > duel.MaxPlayerHp ? new Color(0.2f, 0.85f, 0.55f) : duel.PlayerHp * 4 > duel.MaxPlayerHp ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.3f, 0.35f);
            long score = (long)(duel.Dealt * 10 + duel.Counters * 400 + duel.OnBeats * 40 + duel.MaxCombo * 20 + duel.Crashes * 300 - duel.Taken * 4);
            scoreL.text = Math.Max(0, score).ToString("N0", Ci);
            var e = duel.E;
            foeFill.style.width = Length.Percent((float)(e.Hp / Math.Max(1e-6, e.MaxHp) * 100));
            foeL.text = e.KO ? "결투자  K.O." : e.Invuln ? "결투자  무적" : e.Disabled ? "결투자  경직!" : e.InCounter ? "결투자  카운터!" : "결투자";
            waveL.text = $"PHASE {duel.PhaseNo + 1}/{duel.PhaseCount} · " + (duel.Phase == 2 ? "격노" : $"{duel.Phase + 1}단계");
            titleL.text = chart != null && !string.IsNullOrWhiteSpace(chart.Title) ? chart.Title : "작업실 결투";
            double off = chart != null ? Math.Max(0, chart.OffsetMs / 1000.0) : 0, len = chart != null ? Math.Max(1, chart.Length - off) : 60;
            progFill.style.width = Length.Percent((float)(((Math.Max(0, t - off) % len) / len) * 100));
            var p = duel.P;
            var T = duel.T;
            bool dFull = p.DashStack >= T.DashCapI, dReady = p.DashStack >= 1;
            dashP.text = Pips(p.DashStack, T.DashCapI);
            dashFill.style.width = Length.Percent(dFull ? 100 : (float)Math.Min(100, p.DashRech / FightTune.Pos(T.DashRecharge) * 100));
            dashT.text = dFull ? "준비" : $"{FightTune.Pos(T.DashRecharge) - p.DashRech:0.0}s";
            dashRow.EnableInClassList("sg-cd-row--ready", dReady);
            bool cFull = p.CtrStack >= T.CtrCapI, cReady = p.CounterReady;
            ctrP.text = Pips(p.CtrStack, T.CtrCapI);
            ctrFill.style.width = Length.Percent(cFull ? 100 : (float)Math.Min(100, p.CtrRech / FightTune.Pos(T.CtrRecharge) * 100));
            ctrT.text = p.CtrCd > 0 && p.CtrStack >= 1 ? $"쿨 {p.CtrCd:0.0}s" : cFull ? "준비" : $"{(T.CtrRecharge - p.CtrRech) / Math.Max(0.01, p.CtrRechargeSpeed):0.0}s";
            ctrRow.EnableInClassList("sg-cd-row--ready", cReady);
            ctrRow.EnableInClassList("sg-cd-row--cd", p.CtrCd > 0 && p.CtrStack >= 1);
            // 궁극기: 게이지(맞음 · 카운터로 참) → 가득이면 키 안내 · 시전 중엔 남은 시간
            double um = Math.Max(1, T.UltMax);
            bool uFull = p.Ult >= duel.UltCost - 1e-6, uOn = p.UltT > 0;
            ultP.text = "";
            ultFill.style.width = Length.Percent(uOn ? (float)Math.Min(100, p.UltT / Math.Max(0.1, T.UltTime) * 100) : (float)Math.Min(100, p.Ult / um * 100));
            ultT.text = uOn ? $"폭격 {p.UltT:0.0}s" : uFull ? $"{KeyBinds.Label(KeyBinds.ULT)} 준비" : $"{p.Ult / um * 100:0}%";
            ultRow.EnableInClassList("sg-cd-row--ready", uFull || uOn);
            grooveL.text = $"GROOVE ×{duel.GrooveMul:0.00}";
            grooveFill.style.width = Length.Percent((float)(duel.Groove * 100));
            grooveL.EnableInClassList("sg-groove-k--hot", duel.Groove >= 0.75);
            comboL.text = duel.Combo >= 2 ? duel.Combo.ToString() : "";
            comboK.style.opacity = duel.Combo >= 2 ? 1 : 0;
            float s = 1 + comboPop * 0.35f;
            comboL.style.scale = new Scale(new Vector2(s, s));
        }
    }
}
