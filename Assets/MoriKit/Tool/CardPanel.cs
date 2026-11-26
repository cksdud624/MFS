using System;
using System.Collections.Generic;
using System.Linq;
using MoriFx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 작업실 ‘연출’ 칸: 순간(공격 · 대쉬 · 카운터 …)마다 연출 카드(시간표)를 만든다.
    ///  · 위: 순간 고르기 · 재생 단추(처음 · 한 칸 · 재생/멈춤 · 되풀이 · 속도)
    ///  · 가운데: 시간표 — 눈금(프레임) · 캐릭터 그림 띠(컷 경계) · 트랙 막대(끌어서 옮기기 · 끝을 끌어 길이) · 빨간 재생선(눈금을 눌러 끌기)
    ///  · 아래: 트랙 더하기 · 고른 트랙의 수치(− / + · 숫자 입력, Shift+누르기 = 10배)
    /// 바꾸면 바로 StreamingAssets/Fight/fx-cards/&lt;순간&gt;.json 에 저장, 되돌리기 · 다시하기 100번.
    /// </summary>
    public sealed class CardPanel
    {
        readonly VisualElement root, head, keyList, transport, body;
        readonly Timeline tl;
        readonly Func<StageWorld> world;
        readonly Func<StageField> field;
        readonly Func<bool> playing;
        string key = FxKeys.ALL[0].key;
        FxCard card;
        bool saved;                 // 파일로 있는 카드인지(처음 바꿀 때 만들어짐)
        int sel = -1;
        bool showKeys;
        FxRun run;
        bool running;               // 재생 중(멈춤 아님)
        bool loop = true;
        float speed = 1;
        float t;                    // 카드 시간(초)
        // 되돌리기 사진: 카드 json + (고른 이펙트 트랙의) 프리팹 조율 json — 둘 다 같은 되돌리기로
        readonly List<(string card, string tp, string tj)> undo = new List<(string, string, string)>(), redo = new List<(string, string, string)>();
        string tweakSys = "";       // 빠른 조율: 고른 파티클 시스템("" = 전체)
        static string clipCard, clipTrack;      // 복사 · 붙여넣기(순간 사이)
        bool showPresets, showPost;
        string presetName = "";
        (string card, string tp, string tj)? abA, abB;
        bool showA;
        bool bakeArm;               // ‘프리팹에 적용’ 한 번 더 누르면 실행
        string bakeMsg;
        Label timeLbl;
        Button playBtn;
        int seenStore = -1, seenLib = -1, seenArt = -1;
        bool active;

        static readonly (string type, string name, string color)[] TYPES =
        {
            (FxTrack.EFFECT, "이펙트", "7FD6FF"), (FxTrack.SPRITE, "그림", "FFB86B"), (FxTrack.HITSTOP, "히트스톱", "FF6B8A"),
            (FxTrack.SHAKE, "흔들림", "C9A0FF"), (FxTrack.ZOOM, "줌", "6FD08C"), (FxTrack.FLASH, "섬광", "FCE9A0"),
            (FxTrack.GHOST, "잔상", "9FE8FF"), (FxTrack.BODY, "몸 효과", "FF9A7A"), (FxTrack.GLOW, "빛 번짐", "FFD27A"),
            (FxTrack.CHROMA, "색수차", "FF7AD9"), (FxTrack.BLUR, "방사 블러", "8FB8FF"), (FxTrack.WAVE, "충격파", "7AF0D0"),
            (FxTrack.GRADE, "색 보정", "E0A0FF"), (FxTrack.TILT, "기울기", "A0C4FF"),
            (FxTrack.SHATTER, "조각내기", "C8F07A"), (FxTrack.LIGHT, "빛(주변)", "FFF0A0"), (FxTrack.HAZE, "일렁임", "9AD0C0"),
        };
        public static string TypeName(string t) { foreach (var x in TYPES) if (x.type == t) return x.name; return t; }
        public static Color TypeColor(string t) { foreach (var x in TYPES) if (x.type == t) { ColorUtility.TryParseHtmlString("#" + x.color, out var c); return c; } return Color.gray; }

        static readonly (string id, string name)[] ANCHORS = { ("self", "쓰는 쪽"), ("target", "맞는 쪽"), ("hit", "타격 · 피격 지점"), ("mid", "둘 사이") };
        // 미리보기 방향(움직이는 · 치는 방향): 대쉬 8방향 등을 작업실에서 시험
        static readonly (string name, Vector2 d)[] DIRS =
        {
            ("→", new Vector2(1, 0)), ("↗", new Vector2(1, 1)), ("↑", new Vector2(0, 1)), ("↖", new Vector2(-1, 1)),
            ("←", new Vector2(-1, 0)), ("↙", new Vector2(-1, -1)), ("↓", new Vector2(0, -1)), ("↘", new Vector2(1, -1)),
        };
        Vector2 previewDir = Vector2.zero;      // 0 = 순간마다 기본(바라보는 쪽)
        static readonly string[] COLORS = { "FFFFFF", "FFE27A", "FF9A3C", "FF5A5A", "FF7AD9", "B07CFF", "7FD6FF", "6FD08C", "000000" };

        public CardPanel(VisualElement parent, Func<StageWorld> world, Func<StageField> field, Func<bool> playing)
        {
            this.world = world; this.field = field; this.playing = playing;
            root = new VisualElement(); root.AddToClassList("ws-chain"); parent.Add(root);
            head = new VisualElement(); head.AddToClassList("ae-wrap"); root.Add(head);
            keyList = new VisualElement(); keyList.AddToClassList("ae-wrap"); root.Add(keyList);
            transport = new VisualElement(); transport.AddToClassList("ae-wrap"); root.Add(transport);
            tl = new Timeline(this); root.Add(tl);
            body = new VisualElement(); body.AddToClassList("ws-tune"); root.Add(body);
            Load(key);
        }

        // ───────── 카드 고르기 · 저장 · 되돌리기 ─────────
        void Load(string k)
        {
            key = k;
            FxCardStore.EnsureLoaded();
            var c = FxCardStore.Get(k);
            saved = c != null;
            card = c != null ? c.Clone() : new FxCard { key = k, name = FxKeys.NameOf(k), length = 40, preview = GuessPreview(k) };
            sel = card.tracks.Count > 0 ? 0 : -1;
            undo.Clear(); redo.Clear();
            abA = abB = null; showA = false;
            t = 0; running = false;
            StopRun();
            Rebuild();
        }

        static string GuessPreview(string k)
        {
            string p = k switch
            {
                "hero.slash1" or "hero.mech1" or "hero.hit.slash1" or "hero.hit.mech1" => "hero-attack1",
                "hero.slash2" or "hero.mech2" or "hero.hit.slash2" or "hero.hit.mech2" => "hero-attack2",
                "hero.slash3" or "hero.mech3" or "hero.hit.slash3" or "hero.hit.mech3" => "hero-attack3",
                "hero.counter" or "hero.counterAtk" or "hero.hit.counter" or "counter.success" or "counter.crash" => "hero-counter",
                "hero.dash" or "hero.backdash" => "hero-dash",
                "hero.jump" or "hero.djump" => "hero-jump",
                "hero.hurt" or "hero.countered" => "hero-hurt",
                "hero.wallbounce" => "hero-down",
                _ => "",
            };
            if (p.StartsWith("hero-attack") && ArtLib.Get(p) == null && ArtLib.Get("hero-attack") != null) p = "hero-attack";
            return p;
        }

        /// <summary>바꾸기 전에 부름(되돌리기용 사진).</summary>
        void Snap()
        {
            undo.Add(Shot());
            if (undo.Count > 100) undo.RemoveAt(0);
            redo.Clear();
        }

        /// <summary>바꾼 뒤에 부름: 저장 · 미리보기 다시 맞추기 · 화면 갱신(full = 아래 칸까지).</summary>
        void Commit(bool full = true)
        {
            if (FxCardStore.Save(card.Clone())) saved = true;
            seenStore = FxCardStore.Version;
            if (run != null) { run.card = card; field()?.Fx.Dir.Refresh(run); }
            if (full) Rebuild(); else { tl.Rebuild(); UpdateTime(); }
        }

        string SelPrefab => sel >= 0 && sel < card.tracks.Count && card.tracks[sel].type == FxTrack.EFFECT && !string.IsNullOrEmpty(card.tracks[sel].prefab) ? card.tracks[sel].prefab : null;

        (string card, string tp, string tj) Shot()
        {
            string tp = SelPrefab;
            return (card.ToJson(), tp, tp != null ? JsonUtility.ToJson(FxTweaks.Find(tp) ?? new FxTweak { prefab = tp }) : null);
        }

        void Restore((string card, string tp, string tj) s)
        {
            card = FxCard.FromJson(s.card);
            if (s.tp != null && s.tj != null)
            {
                var t = JsonUtility.FromJson<FxTweak>(s.tj) ?? new FxTweak();
                t.prefab = s.tp;
                FxTweaks.Set(t);
            }
        }

        void Undo()
        {
            if (undo.Count == 0) return;
            redo.Add(Shot());
            Restore(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1);
            sel = Mathf.Min(sel, card.tracks.Count - 1);
            Commit();
        }

        void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(Shot());
            Restore(redo[redo.Count - 1]); redo.RemoveAt(redo.Count - 1);
            sel = Mathf.Min(sel, card.tracks.Count - 1);
            Commit();
        }

        // ───────── 매 프레임 ─────────
        /// <summary>on = 이 칸이 보이는 중. 보일 때만 미리보기 재생 · 캐릭터 시간 맞추기.</summary>
        public void Tick(bool on, float dt)
        {
            var w = world(); var f = field();
            bool live = on && w != null && f != null && !playing();
            if (!live)
            {
                if (active) { StopRun(); StageField.ForceHeroMs = -1; }
                active = false;
                return;
            }
            if (!active) { active = true; Rebuild(); }
            if (seenLib != PrefabLib.Version || seenArt != ArtLib.Version) Rebuild();
            if (seenStore != FxCardStore.Version && !saved) { var c = FxCardStore.Get(key); if (c != null) Load(key); else seenStore = FxCardStore.Version; }

            var ctx = FxTest.Ctx(w, key);
            if (previewDir != Vector2.zero)
            {
                ctx.dir = previewDir;
                if (Mathf.Abs(previewDir.x) > 0.01f) ctx.facing = previewDir.x > 0 ? 1 : -1;
            }
            if (run == null || run.Done) { run = f.Fx.Dir.Play(card, ctx); if (run != null) { run.hold = true; run.paused = true; f.Fx.Dir.Seek(run, t); } }
            if (run != null)
            {
                run.ctx = ctx;
                run.speed = speed;
                run.loop = loop;
                run.hold = true;
                if (running)
                {
                    // 재생 중: 시간은 감독이 진행(파티클은 자연스럽게 흐름). 끝에 닿아 멈추면 재생 끝
                    if (run.paused) { running = false; RefreshPlayBtn(); }
                    t = run.t;
                }
                else
                {
                    run.paused = true;
                    if (Mathf.Abs(run.t - t) > 0.0001f) f.Fx.Dir.Seek(run, t);
                }
            }
            // 캐릭터 그림: 카드의 미리보기 애니메이션을 카드 시간(히트스톱 반영)에 맞춰
            if (!string.IsNullOrEmpty(card.preview) && ArtLib.Get(card.preview) != null)
            {
                StageField.ForceHero = card.preview;
                StageField.ForceHeroMs = FxDirector.SlowedTime(card, t, HeroOwner) * 1000f;
            }
            else StageField.ForceHeroMs = -1;
            UpdateTime();
        }

        /// <summary>작업실을 나갈 때: 미리보기로 띄워 둔 것을 모두 지움(결투 화면에 남지 않게).</summary>
        public void Close()
        {
            StopRun();
            running = false;
            active = false;
            StageField.ForceHeroMs = -1;
        }

        void StopRun()
        {
            var f = field();
            if (run != null && f != null) f.Fx.Dir.Stop(run);
            run = null;
        }

        void Seek(float sec)
        {
            var f = field();
            t = Mathf.Clamp(sec, 0, f != null ? f.Fx.Dir.EndOf(card) : card.Sec(card.length));
            running = false;
            RefreshPlayBtn();
            if (run != null && f != null) { run.paused = true; f.Fx.Dir.Seek(run, t); }
            UpdateTime();
        }

        void TogglePlay()
        {
            var f = field();
            if (!running && f != null && t >= f.Fx.Dir.EndOf(card) - 0.001f) Seek(0);
            running = !running;
            if (run != null && f != null)
            {
                if (running) { f.Fx.Dir.Seek(run, t); run.paused = false; }
                else { run.paused = true; t = run.t; }
            }
            RefreshPlayBtn();
        }

        int Cur => Mathf.RoundToInt(t * card.fps);
        /// <summary>미리보기 계산에서 플레이어의 번호(기본 문맥: 쓰는 쪽 1 · 맞는 쪽 2).</summary>
        int HeroOwner => PrefabFx.HeroSide(key) ? 1 : 2;

        void UpdateTime()
        {
            if (timeLbl != null) timeLbl.text = $"{Cur} / {card.length} 프레임";
            tl.Mark();
        }

        void RefreshPlayBtn()
        {
            if (playBtn == null) return;
            playBtn.text = running ? "멈춤" : "재생";
            playBtn.EnableInClassList("ae-btn--on", running);
        }

        // ───────── 화면 만들기 ─────────
        public void Rebuild()
        {
            seenLib = PrefabLib.Version; seenArt = ArtLib.Version; seenStore = FxCardStore.Version;
            head.Clear(); keyList.Clear(); transport.Clear(); body.Clear();

            Lbl(head, "순간");
            Btn(head, FxKeys.NameOf(key) + (saved ? "" : " (새 카드)"), () => { showKeys = !showKeys; Rebuild(); }, "눌러서 다른 순간 고르기", showKeys ? "ae-btn--on" : null);
            Btn(head, "되돌리기", Undo, $"마지막 바꾼 것 되돌리기({undo.Count})").SetEnabled(undo.Count > 0);
            Btn(head, "다시하기", Redo, $"되돌린 것 다시({redo.Count})").SetEnabled(redo.Count > 0);
            var old = FxStore.Find(key);
            if (card.tracks.Count == 0 && old != null && !string.IsNullOrEmpty(old.prefab))
                Btn(head, "예전 프리팹 연결 가져오기", () => { Snap(); card = FxCardStore.FromCfg(old); card.preview = GuessPreview(key); sel = 0; Commit(); }, "‘프리팹’ 칸에서 이 순간에 연결해 둔 프리팹을 이펙트 트랙으로");
            Btn(head, "카드 복사", () => { clipCard = card.ToJson(); Rebuild(); }, "이 카드(트랙 전부)를 복사 — 다른 순간에서 붙여넣기");
            if (clipCard != null) Btn(head, "카드 붙여넣기", () =>
            {
                var c = FxCard.FromJson(clipCard); if (c == null) return;
                Snap(); card.tracks = c.tracks; card.length = c.length; if (string.IsNullOrEmpty(card.preview)) card.preview = c.preview; sel = card.tracks.Count > 0 ? 0 : -1; Commit();
            }, "복사한 카드의 트랙 · 길이로 바꿈(되돌리기 가능)");
            On(Btn(head, "프리셋", () => { showPresets = !showPresets; Rebuild(); }, "이름 붙여 저장해 둔 카드 · 지금 카드를 프리셋으로 저장"), showPresets);
            On(Btn(head, "화면 기본값", () => { showPost = !showPost; Rebuild(); }, "후처리 기본값: 빛 번짐 · 비네트 · 색수차 · 채도(모든 화면 공통, post.json)"), showPost);
            if (saved) Btn(head, "카드 지우기", () => { Snap(); FxCardStore.Delete(key); saved = false; card.tracks.Clear(); sel = -1; seenStore = FxCardStore.Version; if (run != null) field()?.Fx.Dir.Refresh(run); Rebuild(); },
                "이 순간의 카드 파일을 지움(되돌리기로 살릴 수 있음) · 지우면 ‘프리팹’ 칸 연결이 대신 쓰임");

            if (showPresets) Presets();
            if (showPost) PostPanel();
            if (showKeys)
                foreach (var (k, n) in FxKeys.ALL)
                {
                    string kk = k;
                    var has = FxCardStore.Find(k) != null;
                    var b = Btn(keyList, n, () => { showKeys = false; Load(kk); }, has ? "카드 있음" : "아직 카드 없음");
                    b.EnableInClassList("ae-btn--on", k == key);
                    if (has && k != key) b.AddToClassList("ws-fx-used");
                }

            // 재생 단추
            Btn(transport, "처음", () => Seek(0), "카드 처음으로");
            Btn(transport, "◀ 1", () => Seek(card.Sec(Cur - 1)), "한 프레임 뒤로");
            playBtn = Btn(transport, "재생", TogglePlay, "재생 / 멈춤", "ae-btn--main");
            RefreshPlayBtn();
            Btn(transport, "1 ▶", () => Seek(card.Sec(Cur + 1)), "한 프레임 앞으로");
            On(Btn(transport, "되풀이", () => { loop = !loop; Rebuild(); }, "끝나면 처음부터 다시"), loop);
            foreach (var s in new[] { 0.25f, 0.5f, 1f })
            {
                float ss = s;
                On(Btn(transport, s == 1 ? "1×" : s == 0.5f ? "½×" : "¼×", () => { speed = ss; Rebuild(); }, "미리보기 재생 속도(카드에는 저장 안 됨)"), Mathf.Approximately(speed, s));
            }
            timeLbl = new Label(); timeLbl.AddToClassList("ae-k"); transport.Add(timeLbl);
            // A/B 비교: 지금 상태를 A 로 잡고 고친 뒤 번갈아 보기
            Btn(transport, abA == null ? "A로 잡기" : "A 다시 잡기", () => { abA = Shot(); abB = null; showA = false; Rebuild(); }, "지금 카드 · 조율을 A 로 기억(비교 기준)");
            if (abA != null)
                On(Btn(transport, showA ? "지금: A (눌러서 B)" : "지금: B (눌러서 A)", () =>
                {
                    if (!showA) { abB = Shot(); Restore(abA.Value); showA = true; }
                    else { abA = Shot(); if (abB != null) Restore(abB.Value); showA = false; }
                    Commit();
                }, "A(잡아 둔 것)와 B(고친 것)를 번갈아 보기 — 보이는 쪽이 저장됨"), showA);

            // 카드 자체: 길이 · 미리보기 애니메이션
            Num(body, "카드 길이(프레임 · 60 = 1초)", () => card.length, v => card.length = Mathf.Clamp(Mathf.RoundToInt(v), 1, 600), 1, true);
            var pv = new VisualElement(); pv.AddToClassList("ae-wrap"); body.Add(pv);
            Lbl(pv, "캐릭터 동작(미리보기)");
            On(Btn(pv, "없음", () => { Snap(); card.preview = ""; Commit(); }, "캐릭터는 평소대로"), string.IsNullOrEmpty(card.preview));
            foreach (var a in ArtLib.AllAnims.Select(x => x.Key).Where(x => x != null && x.StartsWith("hero-")).Distinct().OrderBy(x => x))
            {
                string aa = a;
                On(Btn(pv, a.Substring(5), () => { Snap(); card.preview = aa; Commit(); }, "이 동작을 카드 시간에 맞춰 재생(시간표의 ‘그림 띠’ 에 컷 경계가 보임)"), card.preview == a);
            }

            // 트랙 더하기
            var add = new VisualElement(); add.AddToClassList("ae-wrap"); body.Add(add);
            Lbl(add, "트랙 더하기");
            if (clipTrack != null) Btn(add, "+ 붙여넣기", () =>
            {
                var tk = JsonUtility.FromJson<FxTrack>(clipTrack); if (tk == null) return;
                Snap(); tk.at = Mathf.Clamp(Cur, 0, 600); card.tracks.Add(tk); sel = card.tracks.Count - 1; Commit();
            }, "복사한 트랙을 지금 재생선 자리에");
            foreach (var (ty, nm, _) in TYPES)
            {
                string tt = ty;
                Btn(add, "+ " + nm, () => AddTrack(tt), AddTip(ty));
            }

            tl.Rebuild();
            if (card.tracks.Count == 0)
                Note(body, "아직 트랙이 없어요. ‘+ 이펙트’(프리팹) · ‘+ 그림’(편집기에서 그린 애니메이션) · 화면 연출(히트스톱 · 흔들림 · 줌 · 섬광)을 더해 보세요. 빨간 선(지금 시간) 자리에 생겨요.");
            if (sel >= 0 && sel < card.tracks.Count) Props(card.tracks[sel]);
        }

        static string AddTip(string ty) => ty switch
        {
            FxTrack.EFFECT => "등록한 프리팹(파티클 · 3D)을 띄움",
            FxTrack.SPRITE => "편집기에서 그린 애니메이션을 이펙트로(빛 섞기 · 캐릭터 캔버스에 맞춤)",
            FxTrack.HITSTOP => "타격 순간 시간을 멈추거나 느리게",
            FxTrack.SHAKE => "화면 흔들기",
            FxTrack.ZOOM => "화면 당기기(+) · 밀기(−)",
            FxTrack.GHOST => "캐릭터의 그 순간 모습을 몇 프레임마다 그 자리에 남기고 흐려지게(잔상 대쉬)",
            FxTrack.BODY => "캐릭터 몸을 번쩍(빛) · 색 입히기(피격 번쩍임 등)",
            FxTrack.GLOW => "빛 번짐(Bloom)을 잠깐 세게",
            FxTrack.CHROMA => "색수차: 빨강 · 파랑이 갈라짐(강한 타격)",
            FxTrack.BLUR => "방사 블러: 한 점으로 빨려 드는 흐림",
            FxTrack.WAVE => "충격파: 둥글게 퍼지며 화면을 일그러뜨림",
            FxTrack.GRADE => "색 보정: 흑백 · 반전 · 색 입히기",
            FxTrack.TILT => "화면 기울기(바라보는 쪽으로)",
            FxTrack.SHATTER => "그림을 격자 조각으로 쪼개 날림(물체가 부서져 파티클처럼 흩어짐) — 침식 · 발광 곡선과 함께",
            FxTrack.LIGHT => "주변을 그 색으로 밝힘(번개가 떨어지는 순간 캐릭터 · 바닥이 밝아짐)",
            FxTrack.HAZE => "아지랑이처럼 한 곳의 화면을 일렁이게",
            _ => "화면 전체를 한 색으로 번쩍",
        };

        void AddTrack(string ty)
        {
            Snap();
            var k = new FxTrack { type = ty, at = Mathf.Clamp(Cur, 0, 600) };
            switch (ty)
            {
                case FxTrack.EFFECT:
                    k.frames = 0;
                    k.prefab = PrefabLib.All.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? "";
                    break;
                case FxTrack.SPRITE:
                    k.frames = 30;
                    k.anim = ArtLib.AllAnims.Select(x => x.Key).Where(x => x != null && !x.StartsWith("hero-") && !x.StartsWith("foe-")).OrderBy(x => x).FirstOrDefault() ?? "";
                    k.speed = 1; k.blend = "add"; k.pivot = "feet";
                    break;
                case FxTrack.HITSTOP: k.frames = 6; k.power = 0; k.curve = "flat"; break;
                case FxTrack.SHAKE: k.frames = 12; k.power = 12; k.freq = 30; k.curve = "decay"; break;
                case FxTrack.ZOOM: k.frames = 10; k.power = 0.06f; k.curve = "punch"; k.anchor = "hit"; break;
                case FxTrack.FLASH: k.frames = 4; k.alpha = 0.6f; k.color = "FFFFFF"; k.curve = "decay"; break;
                case FxTrack.GHOST: k.frames = 18; k.gap = 2; k.life = 12; k.alpha = 0.6f; k.color = "7FD6FF"; k.color2 = "B07CFF"; k.blend = "add"; k.anchor = "self"; break;
                case FxTrack.BODY: k.frames = 6; k.color = "FFFFFF"; k.alpha = 1; k.blend = "add"; k.who = "target"; k.curve = "decay"; break;
                case FxTrack.GLOW: k.frames = 12; k.power = 1; k.curve = "decay"; break;
                case FxTrack.CHROMA: k.frames = 10; k.power = 8; k.curve = "decay"; break;
                case FxTrack.BLUR: k.frames = 10; k.power = 0.35f; k.anchor = "hit"; k.curve = "decay"; break;
                case FxTrack.WAVE: k.frames = 18; k.power = 20; k.radius = 5 * FxCard.LEGACY_U; k.width = 0.8f * FxCard.LEGACY_U; k.anchor = "hit"; k.curve = "linear"; break;
                case FxTrack.GRADE: k.frames = 8; k.sat = 0; k.invert = 0; k.alpha = 0; k.color = "FFFFFF"; k.curve = "decay"; break;
                case FxTrack.TILT: k.frames = 14; k.power = 2; k.curve = "punch"; break;
                case FxTrack.SHATTER:
                    k.frames = 40; k.cols = 8; k.rows = 8; k.power = 3 * FxCard.LEGACY_U; k.spin = 360; k.grav = -6 * FxCard.LEGACY_U; k.shrink = 0.6f; k.jx = 0.4f * FxCard.LEGACY_U; k.jat = 6;
                    k.blend = "add"; k.pivot = "feet"; k.fadeOut = 12;
                    k.anim = ArtLib.AllAnims.Select(x => x.Key).Where(x => x != null && !x.StartsWith("hero-") && !x.StartsWith("foe-")).OrderBy(x => x).FirstOrDefault() ?? "";
                    k.mats.Add(new FxParam { name = "_Erode", from = 0, to = 1, curve = "custom", pts = "0,0;0.55,0;1,1" });
                    break;
                case FxTrack.LIGHT: k.frames = 14; k.power = 1.5f; k.radius = 6 * FxCard.LEGACY_U; k.color = "BFE4FF"; k.anchor = "hit"; k.curve = "punch"; break;
                case FxTrack.HAZE: k.frames = 30; k.power = 6; k.radius = 3 * FxCard.LEGACY_U; k.freq = 1.5f; k.anchor = "self"; k.curve = "bell"; break;
            }
            card.tracks.Add(k);
            sel = card.tracks.Count - 1;
            Commit();
        }

        // ───────── 고른 트랙의 수치 ─────────
        void Props(FxTrack k)
        {
            var hd = new VisualElement(); hd.AddToClassList("ae-wrap"); body.Add(hd);
            var tag = new Label($"{sel + 1}. {TypeName(k.type)}"); tag.AddToClassList("ae-k"); tag.style.color = TypeColor(k.type); hd.Add(tag);
            On(Btn(hd, k.on ? "켜짐" : "꺼짐", () => { Snap(); k.on = !k.on; Commit(); }, "끄면 남겨 두고 재생만 안 함"), k.on);
            Btn(hd, "복제", () => { Snap(); card.tracks.Insert(sel + 1, k.Clone()); sel++; Commit(); }, "이 트랙을 하나 더(바로 아래에)");
            Btn(hd, "트랙 복사", () => { clipTrack = JsonUtility.ToJson(k); Rebuild(); }, "이 트랙을 복사 — 다른 카드에서 붙여넣기");
            Btn(hd, "위로", () => Move(-1), "트랙 순서 위로(그리는 순서는 아래가 위에)").SetEnabled(sel > 0);
            Btn(hd, "아래로", () => Move(1), "트랙 순서 아래로").SetEnabled(sel < card.tracks.Count - 1);
            Btn(hd, "지우기", () => { Snap(); card.tracks.RemoveAt(sel); sel = Mathf.Min(sel, card.tracks.Count - 1); Commit(); }, "이 트랙 지우기(되돌리기 가능)");

            Num(body, "시작(프레임)", () => k.at, v => k.at = Mathf.Clamp(Mathf.RoundToInt(v), 0, 600), 1, true);
            Num(body, k.type == FxTrack.EFFECT ? "길이(프레임 · 0 = 자동)" : "길이(프레임)", () => k.frames, v => k.frames = Mathf.Clamp(Mathf.RoundToInt(v), k.type == FxTrack.EFFECT ? 0 : 1, 600), 1, true);

            switch (k.type)
            {
                case FxTrack.EFFECT:
                {
                    var names = PrefabLib.All.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    var pr = new VisualElement(); pr.AddToClassList("ae-wrap"); body.Add(pr);
                    Lbl(pr, "프리팹");
                    foreach (var n in names) { string nn = n; On(Btn(pr, n, () => { Snap(); k.prefab = nn; Commit(); }, "이 프리팹을 씀"), string.Equals(k.prefab, n, StringComparison.OrdinalIgnoreCase)); }
                    if (names.Count == 0) Note(body, "등록된 프리팹이 없어요 — ‘프리팹’ 칸의 안내대로 Resources/MoriFx 에 넣고 ‘다시 읽기’.");
                    else if (!string.IsNullOrEmpty(k.prefab) && PrefabLib.Get(k.prefab) == null) Note(body, $"‘{k.prefab}’ 프리팹을 찾지 못했어요.");
                    Place(k);
                    Motion(k);
                    Mats(k);
                    Num(body, "재생 속도(배)", () => k.speed, v => k.speed = Mathf.Clamp(v, 0, 10), 0.1f);
                    var sd = new VisualElement(); sd.AddToClassList("ae-wrap"); body.Add(sd);
                    Lbl(sd, k.seed == 0 ? "무작위 씨앗: 매번 다르게" : $"무작위 씨앗: {k.seed}");
                    Btn(sd, "씨앗 바꾸기", () => { Snap(); k.seed = UnityEngine.Random.Range(1, 100000); Commit(); }, "같은 씨앗 = 언제나 같은 모양(되감기 · 비교가 정확). 바꾸면 다른 경우를 볼 수 있음");
                    On(Btn(sd, k.seed == 0 ? "고정: 끔" : "고정: 켬", () => { Snap(); k.seed = k.seed == 0 ? 1 : 0; Commit(); }, "끄면 터질 때마다 무작위(게임에서도)"), k.seed != 0);
                    if (PrefabLib.Get(k.prefab) != null) Tweak(k.prefab);
                    break;
                }
                case FxTrack.SPRITE:
                {
                    var pr = new VisualElement(); pr.AddToClassList("ae-wrap"); body.Add(pr);
                    Lbl(pr, "그림(애니메이션)");
                    var keys = ArtLib.AllAnims.Select(x => x.Key).Where(x => x != null).Distinct()
                        .OrderBy(x => x.StartsWith("hero-") || x.StartsWith("foe-") ? 1 : 0).ThenBy(x => x).ToList();
                    foreach (var n in keys) { string nn = n; On(Btn(pr, n, () => { Snap(); k.anim = nn; var aa = ArtLib.Get(nn); if (aa != null) k.pivot = aa.Center ? "center" : "feet"; Commit(); }, "이 애니메이션을 이펙트로 — 그림의 캔버스 기준(발밑 · 가운데)에 맞춰 기준이 자동으로 잡혀요"), k.anim == n); }
                    if (keys.Count == 0) Note(body, "그림이 없어요 — 왼쪽 편집기에서 새 이름(예: fx-slash1)으로 그려 저장하면 여기 나와요.");
                    var a = ArtLib.Get(k.anim);
                    if (a != null) Note(body, $"{a.W}×{a.H} · {a.Frames.Length}컷 · {Mathf.RoundToInt(a.TotalMs * 60 / 1000f)}프레임 길이");
                    var tg = new VisualElement(); tg.AddToClassList("ae-wrap"); body.Add(tg);
                    On(Btn(tg, k.blend == "alpha" ? "섞기: 보통" : "섞기: 빛(더하기)", () => { Snap(); k.blend = k.blend == "alpha" ? "add" : "alpha"; Commit(); }, "빛 = 검은 곳은 투명 · 밝을수록 빛남 / 보통 = 그린 그대로(투명도)"), k.blend != "alpha");
                    On(Btn(tg, k.pivot == "center" ? "기준: 그림 가운데" : "기준: 캐릭터 캔버스", () => { Snap(); k.pivot = k.pivot == "center" ? "feet" : "center"; Commit(); }, "캐릭터 캔버스 = 캐릭터와 같은 크기 캔버스에 그리면 정확히 겹침(발밑 기준) / 가운데 = 그림 가운데를 기준점에 — 그림을 고르면 도트 편집기의 ‘캔버스 기준’ 대로 자동으로 잡히고, 여기서 바꿀 수도 있어요"), k.pivot != "center");
                    On(Btn(tg, k.loop ? "되풀이: 켬" : "되풀이: 끔", () => { Snap(); k.loop = !k.loop; Commit(); }, "길이 동안 애니메이션을 되풀이"), k.loop);
                    Place(k);
                    Motion(k);
                    Mats(k);
                    Num(body, "재생 속도(배)", () => k.speed, v => k.speed = Mathf.Clamp(v, 0, 10), 0.1f);
                    Num(body, "진하기(0 ~ 1)", () => k.alpha, v => k.alpha = Mathf.Clamp01(v), 0.05f);
                    Colors(k, "색 곱하기");
                    break;
                }
                case FxTrack.HITSTOP:
                {
                    Num(body, "멈춘 동안 시간 배율(0 = 완전히 멈춤)", () => k.power, v => k.power = Mathf.Clamp01(v), 0.05f);
                    var wr = new VisualElement(); wr.AddToClassList("ae-wrap"); body.Add(wr);
                    Lbl(wr, "누구를");
                    foreach (var (id, nm, tip) in new[] { ("both", "모두", "결투 전체(움직임 · 두 캐릭터 그림)를 느리게"), ("target", "맞는 쪽만", "맞은 캐릭터 그림만 멈춤 — 때린 쪽은 그대로 움직여 타격감이 살아남"), ("self", "쓰는 쪽만", "때린 캐릭터 그림만 멈춤") })
                    { string ii = id; On(Btn(wr, nm, () => { Snap(); k.who = ii; Commit(); }, tip), k.who == id); }
                    Curve(k);
                    Note(body, "이펙트 · 화면 연출은 멈추지 않아요. ‘한쪽만’ 은 그 캐릭터의 그림(동작)만 멈추고 위치 계산은 그대로예요. 슬로모션 = 시간 배율을 0.3 쯤 · 길이를 길게.");
                    break;
                }
                case FxTrack.SHAKE:
                {
                    Num(body, "세기(px · 1080p 기준)", () => k.power, v => k.power = Mathf.Clamp(v, 0, 200), 1);
                    Num(body, "떨림(초당 번)", () => k.freq, v => k.freq = Mathf.Clamp(v, 1, 120), 5);
                    var dr = new VisualElement(); dr.AddToClassList("ae-wrap"); body.Add(dr);
                    Lbl(dr, "방향");
                    foreach (var (id, nm) in new[] { ("both", "모두"), ("x", "가로"), ("y", "세로"), ("hit", "타격 방향") }) { string ii = id; On(Btn(dr, nm, () => { Snap(); k.dir = ii; Commit(); }, id == "hit" ? "처음엔 바라보는 쪽(맞은 쪽)으로 밀렸다가 되돌아오며 떨림" : "흔드는 방향"), k.dir == id); }
                    Curve(k);
                    break;
                }
                case FxTrack.ZOOM:
                {
                    Num(body, "세기(+0.1 = 10% 당김 · 음수 = 밀기)", () => k.power, v => k.power = Mathf.Clamp(v, -0.8f, 2f), 0.01f);
                    var zr = new VisualElement(); zr.AddToClassList("ae-wrap"); body.Add(zr);
                    Lbl(zr, "줌 중심");
                    On(Btn(zr, "화면 가운데", () => { Snap(); k.anchor = "screen"; Commit(); }, "카메라는 그대로 · 화면 가운데로 당김"), k.anchor == "screen");
                    foreach (var (id, nm) in ANCHORS) { string ii = id; On(Btn(zr, nm, () => { Snap(); k.anchor = ii; Commit(); }, "이 점이 화면에서 제자리에 머물며 당겨짐"), k.anchor == id); }
                    if (k.anchor != "screen")
                    {
                        Num(body, "중심 옆으로(U · 바라보는 쪽 +)", () => k.x, v => k.x = v, 0.02f);
                        Num(body, "중심 위로(U)", () => k.y, v => k.y = v, 0.02f);
                    }
                    Curve(k);
                    break;
                }
                case FxTrack.GHOST:
                {
                    var an = new VisualElement(); an.AddToClassList("ae-wrap"); body.Add(an);
                    Lbl(an, "누구의 잔상");
                    On(Btn(an, "쓰는 쪽", () => { Snap(); k.anchor = "self"; Commit(); }, "이 순간을 쓰는 캐릭터"), k.anchor != "target");
                    On(Btn(an, "맞는 쪽", () => { Snap(); k.anchor = "target"; Commit(); }, "상대 캐릭터"), k.anchor == "target");
                    Num(body, "몇 프레임마다 남길지", () => k.gap, v => k.gap = Mathf.Clamp(Mathf.RoundToInt(v), 1, 60), 1, true);
                    Num(body, "하나가 남는 길이(프레임)", () => k.life, v => k.life = Mathf.Clamp(Mathf.RoundToInt(v), 1, 240), 1, true);
                    Num(body, "진하기(0 ~ 1)", () => k.alpha, v => k.alpha = Mathf.Clamp01(v), 0.05f);
                    Num(body, "나타나기(프레임)", () => k.fadeIn, v => k.fadeIn = Mathf.Clamp(Mathf.RoundToInt(v), 0, 120), 1, true);
                    Num(body, "크기(배)", () => k.scale, v => k.scale = Mathf.Clamp(v, 0.01f, 10), 0.05f);
                    Num(body, "비켜 놓기 옆(U)", () => k.x, v => k.x = v, 0.01f);
                    Num(body, "비켜 놓기 위(U)", () => k.y, v => k.y = v, 0.01f);
                    var tg = new VisualElement(); tg.AddToClassList("ae-wrap"); body.Add(tg);
                    On(Btn(tg, k.blend == "alpha" ? "섞기: 보통" : "섞기: 빛(더하기)", () => { Snap(); k.blend = k.blend == "alpha" ? "add" : "alpha"; Commit(); }, "빛 = 밝게 겹쳐 빛나는 잔상 / 보통 = 반투명 실루엣"), k.blend != "alpha");
                    Colors(k, "처음 색");
                    ColorRow("끝 색(흐려지며)", () => k.color2, v => k.color2 = v, true);
                    Note(body, "길이(프레임) 동안 캐릭터의 지금 모습을 남기고, 하나하나는 ‘남는 길이’ 동안 처음 색 → 끝 색으로 바뀌며 사라져요. 빛 섞기 + 파랑 → 보라가 화려한 대쉬 잔상.");
                    break;
                }
                case FxTrack.BODY:
                {
                    var wr = new VisualElement(); wr.AddToClassList("ae-wrap"); body.Add(wr);
                    Lbl(wr, "누구");
                    foreach (var (id, nm) in new[] { ("target", "맞는 쪽"), ("self", "쓰는 쪽"), ("both", "둘 다") }) { string ii = id; On(Btn(wr, nm, () => { Snap(); k.who = ii; Commit(); }, "몸 효과를 걸 캐릭터"), k.who == id); }
                    var md = new VisualElement(); md.AddToClassList("ae-wrap"); body.Add(md);
                    Lbl(md, "방식");
                    On(Btn(md, "빛 번쩍", () => { Snap(); k.blend = "add"; Commit(); }, "캐릭터 그림을 빛으로 한 겹 더 — 하얗게 번쩍(피격)"), k.blend != "alpha");
                    On(Btn(md, "색 입히기", () => { Snap(); k.blend = "alpha"; Commit(); }, "캐릭터 색을 이 색 쪽으로"), k.blend == "alpha");
                    Num(body, "세기(0 ~ 1)", () => k.alpha, v => k.alpha = Mathf.Clamp01(v), 0.05f);
                    Colors(k, "색");
                    Curve(k);
                    break;
                }
                case FxTrack.GLOW:
                    Num(body, "빛 번짐 더하기(배)", () => k.power, v => k.power = Mathf.Clamp(v, 0, 10), 0.1f);
                    Curve(k);
                    Note(body, "기본 빛 번짐(‘화면 기본값’)에 더해져요. 밝은 이펙트일수록 크게 번져요.");
                    break;
                case FxTrack.CHROMA:
                    Num(body, "세기(px · 1080p 기준)", () => k.power, v => k.power = Mathf.Clamp(v, 0, 80), 1);
                    Curve(k);
                    break;
                case FxTrack.BLUR:
                    Num(body, "세기(0 ~ 1)", () => k.power, v => k.power = Mathf.Clamp01(v), 0.05f);
                    Center(k, "블러 중심");
                    Curve(k);
                    break;
                case FxTrack.WAVE:
                    Num(body, "세기(px · 1080p 기준)", () => k.power, v => k.power = Mathf.Clamp(v, 0, 120), 1);
                    Num(body, "퍼지는 거리(U)", () => k.radius, v => k.radius = Mathf.Clamp(v, 0.02f, 40), 0.05f);
                    Num(body, "띠 두께(U)", () => k.width, v => k.width = Mathf.Clamp(v, 0.01f, 10), 0.01f);
                    Center(k, "충격파 중심");
                    Curve(k);
                    Note(body, "중심에서 원이 퍼지며 화면을 밀어내요. 세기 곡선 = 퍼지는 동안 세기(보통 ‘곧게 줄기’).");
                    break;
                case FxTrack.GRADE:
                    Num(body, "채도(0 = 흑백 · 1 = 그대로 · 2 = 진하게)", () => k.sat, v => k.sat = Mathf.Clamp(v, 0, 3), 0.1f);
                    Num(body, "반전(0 ~ 1)", () => k.invert, v => k.invert = Mathf.Clamp01(v), 0.1f);
                    Num(body, "색 입히기 세기(0 ~ 1)", () => k.alpha, v => k.alpha = Mathf.Clamp01(v), 0.05f);
                    Colors(k, "입힐 색");
                    Curve(k);
                    break;
                case FxTrack.TILT:
                    Num(body, "기울기(도 · 바라보는 쪽 +)", () => k.power, v => k.power = Mathf.Clamp(v, -30, 30), 0.5f);
                    Curve(k);
                    break;
                case FxTrack.SHATTER:
                {
                    var pr = new VisualElement(); pr.AddToClassList("ae-wrap"); body.Add(pr);
                    Lbl(pr, "쪼갤 그림");
                    var keys = ArtLib.AllAnims.Select(x => x.Key).Where(x => x != null).Distinct()
                        .OrderBy(x => x.StartsWith("hero-") || x.StartsWith("foe-") ? 1 : 0).ThenBy(x => x).ToList();
                    foreach (var n in keys) { string nn = n; On(Btn(pr, n, () => { Snap(); k.anim = nn; Commit(); }, "이 그림(첫 컷)을 조각내기"), k.anim == n); }
                    Place(k);
                    Num(body, "가로 조각 수", () => k.cols, v => k.cols = Mathf.Clamp(Mathf.RoundToInt(v), 1, 48), 1, true);
                    Num(body, "세로 조각 수", () => k.rows, v => k.rows = Mathf.Clamp(Mathf.RoundToInt(v), 1, 48), 1, true);
                    Num(body, "날아가는 세기(U/초)", () => k.power, v => k.power = Mathf.Clamp(v, 0, 60), 0.05f);
                    Num(body, "퍼짐 무작위(0 ~ 1)", () => k.jx, v => k.jx = Mathf.Clamp01(v), 0.05f);
                    Num(body, "조각마다 늦게 출발(0 ~ 프레임)", () => k.jat, v => k.jat = Mathf.Clamp(Mathf.RoundToInt(v), 0, 120), 1, true);
                    Num(body, "조각 회전(도/초 · ±무작위)", () => k.spin, v => k.spin = Mathf.Clamp(v, 0, 3600), 30);
                    Num(body, "중력(U/초² · 음수 = 아래)", () => k.grav, v => k.grav = Mathf.Clamp(v, -60, 60), 0.1f);
                    Num(body, "작아지기(0 ~ 1)", () => k.shrink, v => k.shrink = Mathf.Clamp01(v), 0.05f);
                    Num(body, "진하기(0 ~ 1)", () => k.alpha, v => k.alpha = Mathf.Clamp01(v), 0.05f);
                    Num(body, "사라지기(프레임)", () => k.fadeOut, v => k.fadeOut = Mathf.Clamp(Mathf.RoundToInt(v), 0, 240), 1, true);
                    var tg = new VisualElement(); tg.AddToClassList("ae-wrap"); body.Add(tg);
                    On(Btn(tg, k.blend == "alpha" ? "섞기: 보통" : "섞기: 빛(더하기)", () => { Snap(); k.blend = k.blend == "alpha" ? "add" : "alpha"; Commit(); }, "빛 = 밝게 · 보통 = 그린 그대로"), k.blend != "alpha");
                    On(Btn(tg, k.pivot == "center" ? "기준: 그림 가운데" : "기준: 캐릭터 캔버스", () => { Snap(); k.pivot = k.pivot == "center" ? "feet" : "center"; Commit(); }, "캐릭터 캔버스 = 발밑 기준(캐릭터와 겹침)"), k.pivot != "center");
                    Colors(k, "처음 색");
                    ColorRow("끝 색", () => k.color2, v => k.color2 = v, true);
                    Mats(k);
                    break;
                }
                case FxTrack.LIGHT:
                    Num(body, "세기", () => k.power, v => k.power = Mathf.Clamp(v, 0, 10), 0.1f);
                    Num(body, "밝히는 거리(U)", () => k.radius, v => k.radius = Mathf.Clamp(v, 0.02f, 60), 0.05f);
                    Colors(k, "빛 색");
                    Center(k, "빛 중심");
                    Curve(k);
                    Note(body, "화면 안 물체를 그 색으로 밝혀요(후처리 켬일 때). 번개는 ‘치고 빠지기’ 곡선 + 짧은 길이 + 섬광 · 흔들림과 같은 프레임에.");
                    break;
                case FxTrack.HAZE:
                    Num(body, "세기(px · 1080p 기준)", () => k.power, v => k.power = Mathf.Clamp(v, 0, 60), 0.5f);
                    Num(body, "넓이(반지름 U)", () => k.radius, v => k.radius = Mathf.Clamp(v, 0.02f, 40), 0.05f);
                    Num(body, "빠르기", () => k.freq, v => k.freq = Mathf.Clamp(v, 0.1f, 20), 0.1f);
                    Center(k, "일렁임 중심");
                    Curve(k);
                    break;
                case FxTrack.FLASH:
                    Num(body, "진하기(0 ~ 1)", () => k.alpha, v => k.alpha = Mathf.Clamp01(v), 0.05f);
                    Colors(k, "색");
                    Curve(k);
                    break;
            }
        }

        // ───────── 빠른 조율(프리팹 안 파티클) ─────────
        /// <summary>
        /// 프리팹 안의 파티클 시스템을 찾아 자주 쓰는 값만 배율로: 크기 · 속도 · 수명 · 수량 · 밝기 · 중력 · 색.
        /// ‘전체’ 는 모든 시스템에, 하나를 고르면 그 시스템에만(둘은 곱해짐). 이 프리팹을 쓰는 모든 카드에 같이 적용.
        /// </summary>
        void Tweak(string prefab)
        {
            var sys = FxTweaks.Systems(PrefabLib.Get(prefab));
            if (sys.Count == 0) { Note(body, "이 프리팹엔 파티클 시스템이 없어서 빠른 조율 값이 없어요."); return; }
            var hd = new VisualElement(); hd.AddToClassList("ae-wrap"); body.Add(hd);
            var tl0 = new Label("빠른 조율 — " + prefab); tl0.AddToClassList("ae-sec"); hd.Add(tl0);
            var sr = new VisualElement(); sr.AddToClassList("ae-wrap"); body.Add(sr);
            if (tweakSys != "" && !sys.Any(x => x.path == tweakSys)) tweakSys = "";
            var t0 = FxTweaks.Find(prefab);
            On(Btn(sr, "전체", () => { tweakSys = ""; Rebuild(); }, "모든 파티클 시스템에 같이 곱함"), tweakSys == "");
            foreach (var (path, _) in sys)
            {
                string pp = path;
                bool changed = t0?.Get(path, false) is FxTweakSys ts && !ts.IsDefault;
                var b = Btn(sr, path == "." ? "(맨 위)" : path, () => { tweakSys = pp; Rebuild(); }, "이 파티클 시스템에만" + (changed ? " · 바꾼 값 있음" : ""));
                On(b, tweakSys == path);
                if (changed && tweakSys != path) b.AddToClassList("ws-fx-used");
            }

            string sp = tweakSys;
            FxTweakSys G() => FxTweaks.Find(prefab)?.Get(sp, false);
            FxTweakSys S() => FxTweaks.Get(prefab).Get(sp, true);
            void Save() { FxTweaks.Save(FxTweaks.Get(prefab)); Live(); }
            Num(body, "크기(배)", () => G()?.size ?? 1, v => S().size = Mathf.Clamp(v, 0, 20), 0.05f, false, Save);
            Num(body, "속도(배)", () => G()?.speed ?? 1, v => S().speed = Mathf.Clamp(v, 0, 20), 0.05f, false, Save);
            Num(body, "수명(배)", () => G()?.life ?? 1, v => S().life = Mathf.Clamp(v, 0.01f, 20), 0.05f, false, Save);
            Num(body, "수량(배)", () => G()?.count ?? 1, v => S().count = Mathf.Clamp(v, 0, 20), 0.1f, false, Save);
            Num(body, "밝기(배 · 1 넘으면 HDR)", () => G()?.glow ?? 1, v => S().glow = Mathf.Clamp(v, 0, 20), 0.1f, false, Save);
            Num(body, "중력(배)", () => G()?.gravity ?? 1, v => S().gravity = Mathf.Clamp(v, -20, 20), 0.1f, false, Save);
            var cr = new VisualElement(); cr.AddToClassList("ae-wrap"); body.Add(cr);
            Lbl(cr, "색 곱하기");
            foreach (var hex in COLORS)
            {
                if (hex == "000000") continue;
                string hh = hex;
                var b = new Button(() => { Snap(); S().color = hh; Save(); Rebuild(); }) { tooltip = "#" + hex };
                b.AddToClassList("ae-onion-sw"); b.AddToClassList("fx-sw");
                ColorUtility.TryParseHtmlString("#" + hex, out var c);
                b.style.backgroundColor = c;
                b.EnableInClassList("ae-onion-sw--on", string.Equals(G()?.color ?? "FFFFFF", hex, StringComparison.OrdinalIgnoreCase));
                cr.Add(b);
            }

            var ft = new VisualElement(); ft.AddToClassList("ae-wrap"); body.Add(ft);
            Btn(ft, sp == "" ? "전체 배율 원래대로" : "이 시스템 원래대로", () => { Snap(); var t = FxTweaks.Get(prefab); t.systems.RemoveAll(x => x.path == sp); Save(); Rebuild(); }, "고른 칸의 값을 모두 1(원래 값)로");
            var cur = FxTweaks.Find(prefab);
            bool any = cur != null && !cur.IsDefault;
            if (any)
                Note(body, "바꾼 값: " + string.Join(" · ", cur.systems.Where(x => !x.IsDefault).Select(Describe)));
#if UNITY_EDITOR
            var bb = Btn(ft, bakeArm ? "정말 적용? (원본은 킷 백업에)" : "프리팹에 적용", () =>
            {
                if (!bakeArm) { bakeArm = true; bakeMsg = null; Rebuild(); return; }
                bakeArm = false;
                bakeMsg = FxTweaks.BakeToPrefab(prefab);
                field()?.Fx.Forget(prefab);
                run = null;
                undo.Clear(); redo.Clear();
                Rebuild();
            }, "조율 값을 프리팹 파일에 써 넣음(에디터 Play 중에만) — 쓰기 전에 원본을 백업. 적용하면 조율 값은 1로 돌아감(이제 프리팹이 그 값)", bakeArm ? "ae-btn--main" : null);
            bb.SetEnabled(any);
            if (bakeArm) Btn(ft, "취소", () => { bakeArm = false; Rebuild(); }, "적용하지 않음");
#else
            Note(body, "‘프리팹에 적용’ 은 유니티 에디터에서 Play 중일 때만 돼요. 지금 값은 fx-tweaks 에 저장돼 있다가 에디터에서 적용할 수 있어요.");
#endif
            if (!string.IsNullOrEmpty(bakeMsg)) Note(body, bakeMsg);
        }

        /// <summary>셰이더 값 곡선: 머티리얼 값(침식 · 발광 · 일렁임 …)을 길이 동안 from → to. 프리셋으로 시작.</summary>
        void Mats(FxTrack k)
        {
            k.mats ??= new List<FxParam>();
            var hd = new VisualElement(); hd.AddToClassList("ae-wrap"); body.Add(hd);
            var tl0 = new Label("셰이더 값 곡선"); tl0.AddToClassList("ae-sec"); hd.Add(tl0);
            foreach (var (nm, tip, mk) in new (string, string, Func<FxParam>)[]
            {
                ("+ 침식으로 사라지기", "끝 무렵 노이즈를 따라 깎이며 사라짐(가장자리 발광)", () => new FxParam { name = "_Erode", from = 0, to = 1, curve = "custom", pts = "0,0;0.55,0;1,1" }),
                ("+ 발광 올렸다 터짐", "밝기를 점점 올림(HDR → 빛 번짐 커짐)", () => new FxParam { name = "_Glow", from = 1, to = 4, curve = "rise" }),
                ("+ 일렁임", "처음에 크게 일렁이다 잦아듦", () => new FxParam { name = "_Wobble", from = 0, to = 0.04f, curve = "decay" }),
                ("+ 가장자리 빛", "침식 경계 밝기", () => new FxParam { name = "_EdgeGlow", from = 2, to = 6, curve = "bell" }),
                ("+ 직접", "머티리얼 값 이름을 직접 적기", () => new FxParam { name = "_Erode", from = 0, to = 1, curve = "rise" }),
            })
            {
                var mm = mk;
                Btn(hd, nm, () => { Snap(); k.mats.Add(mm()); Commit(); }, tip);
            }
            for (int i = 0; i < k.mats.Count; i++)
            {
                var m = k.mats[i];
                int ii = i;
                var r = new VisualElement(); r.AddToClassList("ae-wrap"); body.Add(r);
                var tf = new TextField { value = m.name, isDelayed = true, maxLength = 40, tooltip = "머티리얼 값 이름(MoriFx/Erode: _Erode 침식 · _Glow 밝기 · _Wobble 일렁임 · _EdgeGlow 가장자리 · _Scroll 흐름 · _EdgeWidth 가장자리 폭)" };
                tf.AddToClassList("ae-text"); tf.style.width = 120;
                tf.RegisterValueChangedCallback(e => { Snap(); m.name = (e.newValue ?? "").Trim(); Commit(); });
                r.Add(tf);
                foreach (var (id, cn) in FxCurve.ALL)
                {
                    if (id == "custom" && m.curve != "custom") continue;
                    string cc = id;
                    On(Btn(r, cn, () => { Snap(); m.curve = cc; Commit(); }, "값이 바뀌는 모양"), m.curve == id);
                }
                Btn(r, "지우기", () => { Snap(); k.mats.RemoveAt(ii); Commit(); }, "이 값 곡선 지우기");
                Num(body, $"  {m.name} 처음", () => m.from, v => m.from = v, 0.05f);
                Num(body, $"  {m.name} 끝", () => m.to, v => m.to = v, 0.05f);
            }
            if (k.mats.Count > 0 && k.type == FxTrack.EFFECT)
                Note(body, "프리팹의 머티리얼에 그 이름의 값이 있어야 바뀌어요(MoriFx/Erode 셰이더를 쓰면 위 프리셋이 모두 먹어요).");
        }

        void Center(FxTrack k, string title)
        {
            var zr = new VisualElement(); zr.AddToClassList("ae-wrap"); body.Add(zr);
            Lbl(zr, title);
            On(Btn(zr, "화면 가운데", () => { Snap(); k.anchor = "screen"; Commit(); }, "화면 가운데"), k.anchor == "screen");
            foreach (var (id, nm) in ANCHORS) { string ii = id; On(Btn(zr, nm, () => { Snap(); k.anchor = ii; Commit(); }, AnchorTip(id)), k.anchor == id); }
            if (k.anchor != "screen")
            {
                Num(body, "중심 옆으로(U · 바라보는 쪽 +)", () => k.x, v => k.x = v, 0.02f);
                Num(body, "중심 위로(U)", () => k.y, v => k.y = v, 0.02f);
            }
        }

        /// <summary>이동 · 늘이기 · 흐려지기(이펙트 · 그림).</summary>
        void Motion(FxTrack k)
        {
            var mh = new VisualElement(); mh.AddToClassList("ae-wrap"); body.Add(mh);
            var ml = new Label("이동 · 늘이기"); ml.AddToClassList("ae-sec"); mh.Add(ml);
            Num(body, "날아가기 옆(U · 바라보는 쪽 +)", () => k.mx, v => k.mx = Mathf.Clamp(v, -60, 60), 0.05f);
            Num(body, "날아가기 위(U)", () => k.my, v => k.my = Mathf.Clamp(v, -60, 60), 0.05f);
            var me = new VisualElement(); me.AddToClassList("ae-wrap"); body.Add(me);
            Lbl(me, "이동 모양");
            foreach (var (id, nm, tip) in new[] { ("out", "빠르게 출발", "처음에 확 나가고 끝에서 느려짐(참격)"), ("linear", "고르게", "같은 속도"), ("in", "천천히 출발", "점점 빨라짐") })
            { string ii = id; On(Btn(me, nm, () => { Snap(); k.mease = ii; Commit(); }, tip), k.mease == id); }
            var tr = new VisualElement(); tr.AddToClassList("ae-wrap"); body.Add(tr);
            Lbl(tr, "늘이기(끝점)");
            On(Btn(tr, "안 함", () => { Snap(); k.to = ""; Commit(); }, "늘이지 않음"), string.IsNullOrEmpty(k.to));
            foreach (var (id, nm) in ANCHORS) { string ii = id; On(Btn(tr, nm, () => { Snap(); k.to = ii; Commit(); }, "시작점(기준점 + 비켜 놓기)에서 여기까지 길게 늘임 — 번개 · 빔. 그림은 왼쪽 가운데가 시작점, 폭이 길이가 됨"), k.to == id); }
            if (k.type == FxTrack.SPRITE)
            {
                Num(body, "나타나기(프레임)", () => k.fadeIn, v => k.fadeIn = Mathf.Clamp(Mathf.RoundToInt(v), 0, 240), 1, true);
                Num(body, "사라지기(프레임)", () => k.fadeOut, v => k.fadeOut = Mathf.Clamp(Mathf.RoundToInt(v), 0, 240), 1, true);
                ColorRow("끝 색(길이 동안 바뀜)", () => k.color2, v => k.color2 = v, true);
            }
        }

        static string Describe(FxTweakSys x)
        {
            var parts = new List<string>();
            if (x.size != 1) parts.Add($"크기×{x.size:0.##}");
            if (x.speed != 1) parts.Add($"속도×{x.speed:0.##}");
            if (x.life != 1) parts.Add($"수명×{x.life:0.##}");
            if (x.count != 1) parts.Add($"수량×{x.count:0.##}");
            if (x.glow != 1) parts.Add($"밝기×{x.glow:0.##}");
            if (x.gravity != 1) parts.Add($"중력×{x.gravity:0.##}");
            if (!string.Equals(x.color, "FFFFFF", StringComparison.OrdinalIgnoreCase)) parts.Add("색 #" + x.color);
            return (x.path == "" ? "전체" : x.path == "." ? "(맨 위)" : x.path) + " " + string.Join(" ", parts);
        }

        void Move(int d)
        {
            int j = sel + d;
            if (j < 0 || j >= card.tracks.Count) return;
            Snap();
            (card.tracks[sel], card.tracks[j]) = (card.tracks[j], card.tracks[sel]);
            sel = j;
            Commit();
        }

        void Place(FxTrack k)
        {
            var an = new VisualElement(); an.AddToClassList("ae-wrap"); body.Add(an);
            Lbl(an, "기준점");
            foreach (var (id, nm) in ANCHORS) { string ii = id; On(Btn(an, nm, () => { Snap(); k.anchor = ii; Commit(); }, AnchorTip(id)), k.anchor == id); }
            Num(body, "옆으로(U · 바라보는 쪽 +)", () => k.x, v => k.x = v, 0.02f);
            Num(body, "위로(U)", () => k.y, v => k.y = v, 0.02f);
            Num(body, "크기(배)", () => k.scale, v => k.scale = Mathf.Clamp(v, 0.01f, 50), 0.1f);
            Num(body, "회전(도)", () => k.rot, v => k.rot = Mathf.Repeat(v + 180, 360) - 180, 15);
            var am = new VisualElement(); am.AddToClassList("ae-wrap"); body.Add(am);
            Lbl(am, "방향");
            On(Btn(am, "좌우만", () => { Snap(); k.aim = "face"; Commit(); }, "바라보는 쪽에 따라 좌우만 뒤집음"), k.aim != "dir");
            On(Btn(am, "움직이는 · 치는 방향", () => { Snap(); k.aim = "dir"; Commit(); },
                "대쉬 8방향(위 · 아래 · 대각) · 점프 = 위 · 착지 = 아래 · 타격 = 맞은 쪽으로 돌림. 오른쪽을 보는 그림 · 프리팹 기준, 비켜 놓기(옆 · 위)도 같이 돌아감"), k.aim == "dir");
            if (k.aim == "dir")
            {
                Lbl(am, "미리보기:");
                On(Btn(am, "기본", () => { previewDir = Vector2.zero; StopRun(); Rebuild(); }, "순간마다 기본 방향(바라보는 쪽)"), previewDir == Vector2.zero);
                foreach (var (nm, d) in DIRS) { var dd = d; On(Btn(am, nm, () => { previewDir = dd; StopRun(); Rebuild(); }, "작업실 미리보기에서 이 방향으로 시험(카드엔 저장 안 됨)"), previewDir == d); }
            }

            // 여러 개 · 무작위
            var rh = new VisualElement(); rh.AddToClassList("ae-wrap"); body.Add(rh);
            var rl = new Label("여러 개 · 무작위"); rl.AddToClassList("ae-sec"); rh.Add(rl);
            Btn(rh, "무작위 다시 굴리기", () => { var f = field(); if (run != null && f != null) f.Fx.Dir.Reroll(run); }, "같은 설정으로 다른 경우 보기(게임에선 터질 때마다 새로 굴림)");
            Num(body, "개수", () => k.count, v => k.count = Mathf.Clamp(Mathf.RoundToInt(v), 1, 64), 1, true);
            Num(body, "하나씩 늦게(프레임 · 차례로)", () => k.gap, v => k.gap = Mathf.Clamp(Mathf.RoundToInt(v), 0, 120), 1, true);
            Num(body, "시작 무작위(0 ~ 프레임)", () => k.jat, v => k.jat = Mathf.Clamp(Mathf.RoundToInt(v), 0, 120), 1, true);
            Num(body, "위치 무작위 옆(± U)", () => k.jx, v => k.jx = Mathf.Clamp(v, 0, 20), 0.01f);
            Num(body, "위치 무작위 위(± U)", () => k.jy, v => k.jy = Mathf.Clamp(v, 0, 20), 0.01f);
            Num(body, "회전 무작위(± 도)", () => k.jrot, v => k.jrot = Mathf.Clamp(v, 0, 180), 5);
            Num(body, "크기 무작위(± 배 · 0.2 = ±20%)", () => k.jscale, v => k.jscale = Mathf.Clamp(v, 0, 0.95f), 0.05f);
            var tg = new VisualElement(); tg.AddToClassList("ae-wrap"); body.Add(tg);
            On(Btn(tg, k.flip ? "방향 따라 뒤집기: 켬" : "방향 따라 뒤집기: 끔", () => { Snap(); k.flip = !k.flip; Commit(); }, "왼쪽을 보면 좌우를 뒤집음(오른쪽을 보는 그림 · 프리팹 기준)"), k.flip);
            On(Btn(tg, k.follow ? "따라가기: 켬" : "따라가기: 끔", () => { Snap(); k.follow = !k.follow; Commit(); }, "켬 = 기준을 따라 움직임(쓰는 쪽 · 맞는 쪽 = 그 캐릭터, 둘 사이 = 두 캐릭터의 가운데) · 끔 = 나온 자리에 남음"), k.follow);
        }

        static string AnchorTip(string id) => id switch
        {
            "self" => "이 순간을 쓰는 캐릭터 발밑",
            "target" => "상대 캐릭터 발밑",
            "hit" => "타격 · 피격 지점: 휘두를 땐 공격 판정 가운데, 적중 · 맞음 · 피격 땐 실제로 맞은 곳(그 밖엔 쓰는 쪽)",
            _ => "두 캐릭터 몸 사이: 두 히트박스(몸) 가운데의 가운데 — 따라가기를 켜면 둘이 움직여도 계속 가운데",
        };

        void Curve(FxTrack k)
        {
            var r = new VisualElement(); r.AddToClassList("ae-wrap"); body.Add(r);
            Lbl(r, "세기 곡선");
            foreach (var (id, nm) in FxCurve.ALL)
            {
                string ii = id;
                On(Btn(r, nm, () =>
                {
                    Snap();
                    // ‘직접’ 은 지금 곡선에서 시작(점 6개) → 끌어서 다듬기
                    if (ii == "custom" && k.curve != "custom") k.pts = FxCurve.Sample(k.curve);
                    k.curve = ii; Commit();
                }, ii == "custom" ? "지금 곡선을 점으로 바꿔 직접 다듬기" : "시간에 따라 세기가 변하는 모양"), k.curve == id);
            }
            if (k.curve == "custom")
            {
                body.Add(new CurveBox(this, k));
                Note(body, "점 끌기 · 빈 곳 더블클릭 = 점 더하기 · 점 오른쪽 클릭 = 지우기 · 처음 · 끝 점은 좌우로 안 움직여요.");
            }
        }

        void Colors(FxTrack k, string title) => ColorRow(title, () => k.color, v => k.color = v, false);

        /// <summary>색 한 줄: 견본 · 16진수 칸(allowNone = ‘없음’ 단추 — 끝 색 같은 선택 값).</summary>
        void ColorRow(string title, Func<string> get, Action<string> set, bool allowNone)
        {
            var r = new VisualElement(); r.AddToClassList("ae-wrap"); body.Add(r);
            Lbl(r, title);
            if (allowNone) On(Btn(r, "없음", () => { Snap(); set(""); Commit(); }, "끝 색을 쓰지 않음(색 그대로)"), string.IsNullOrEmpty(get()));
            foreach (var hex in COLORS)
            {
                string hh = hex;
                var b = new Button(() => { Snap(); set(hh); Commit(); }) { tooltip = "#" + hex };
                b.AddToClassList("ae-onion-sw"); b.AddToClassList("fx-sw");
                ColorUtility.TryParseHtmlString("#" + hex, out var c);
                b.style.backgroundColor = c;
                b.EnableInClassList("ae-onion-sw--on", string.Equals(get(), hex, StringComparison.OrdinalIgnoreCase));
                r.Add(b);
            }
            var tf = new TextField { value = get() ?? "", isDelayed = true, maxLength = 7, tooltip = "16진수 색(예: FF9A3C)" };
            tf.AddToClassList("ae-text"); tf.style.width = 84;
            tf.RegisterValueChangedCallback(e =>
            {
                var v = (e.newValue ?? "").Trim().TrimStart('#').ToUpperInvariant();
                if (v.Length == 0 && allowNone) { Snap(); set(""); Commit(); return; }
                if (!ColorUtility.TryParseHtmlString("#" + v, out _)) { tf.SetValueWithoutNotify(get() ?? ""); return; }
                Snap(); set(v); Commit();
            });
            r.Add(tf);
        }

        /// <summary>
        /// 수치 한 줄: 이름을 좌우로 끌면 바뀜(Shift = 10배 거칠게 · Ctrl = 10배 섬세하게) · − · 숫자 칸 · +(Shift = 10배).
        /// 바꾸면 바로 미리보기에 반영, 저장은 손을 뗄 때. save = 저장 방법(비우면 카드 저장).
        /// </summary>
        void Num(VisualElement p, string name, Func<float> get, Action<float> set, float step, bool whole = false, Action save = null)
        {
            save ??= () => Commit(false);
            var row = new VisualElement(); row.AddToClassList("ws-tune-row"); p.Add(row);
            var l = new Label(name) { tooltip = "좌우로 끌어서 바꾸기 · Shift 10배 거칠게 · Ctrl 10배 섬세하게" };
            l.AddToClassList("ae-k"); l.AddToClassList("fx-drag"); row.Add(l);
            var fld = new FloatField { isDelayed = true };
            fld.AddToClassList("ae-int");
            void Show() => fld.SetValueWithoutNotify((float)Math.Round(get(), 3));
            Show();
            fld.RegisterValueChangedCallback(e =>
            {
                if (float.IsNaN(e.newValue) || float.IsInfinity(e.newValue)) { Show(); return; }
                Snap(); set(e.newValue); Show(); save();
            });
            void Do(float d) { Snap(); set((float)Math.Round(get() + d, 3)); Show(); save(); }
            var m = new Button { text = "−", tooltip = "Shift = 10배" }; m.AddToClassList("ae-btn");
            m.RegisterCallback<ClickEvent>(e => Do(-step * (e.shiftKey ? 10 : 1)));
            var pl = new Button { text = "+", tooltip = "Shift = 10배" }; pl.AddToClassList("ae-btn");
            pl.RegisterCallback<ClickEvent>(e => Do(step * (e.shiftKey ? 10 : 1)));
            row.Add(m); row.Add(fld); row.Add(pl);

            // 끌어서 바꾸기: 8px 마다 한 칸
            float startV = 0, startX = 0; bool dragging = false, moved = false;
            l.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                dragging = true; moved = false; startV = get(); startX = e.position.x;
                l.CapturePointer(e.pointerId); e.StopPropagation();
            });
            l.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging || !l.HasPointerCapture(e.pointerId)) return;
                float k = e.shiftKey ? 10 : e.ctrlKey || e.commandKey ? 0.1f : 1;
                float n = (e.position.x - startX) / 8f;
                float v = startV + (whole ? Mathf.Round(n * k) : n * k) * step;
                if (!moved && Mathf.Abs(e.position.x - startX) < 3) return;
                if (!moved) { moved = true; Snap(); }
                set((float)Math.Round(v, 3)); Show(); Live();
            });
            void Release(int id)
            {
                if (l.HasPointerCapture(id)) l.ReleasePointer(id);
                if (dragging && moved) save();
                dragging = moved = false;
            }
            l.RegisterCallback<PointerUpEvent>(e => Release(e.pointerId));
            l.RegisterCallback<PointerCaptureOutEvent>(e => { if (dragging && moved) save(); dragging = moved = false; });
        }

        /// <summary>끄는 중: 저장 없이 미리보기만 다시 맞춤.</summary>
        void Live()
        {
            var f = field();
            if (run != null && f != null) { run.card = card; f.Fx.Dir.Refresh(run); }
            tl.Mark();
        }

        static Button Btn(VisualElement p, string t, Action a, string tip, string extra = null)
        {
            var b = new Button(a) { text = t, tooltip = tip };
            b.AddToClassList("ae-btn");
            if (extra != null) b.AddToClassList(extra);
            p.Add(b);
            return b;
        }

        static void On(Button b, bool on) => b.EnableInClassList("ae-btn--on", on);
        static void Lbl(VisualElement p, string t) { var l = new Label(t); l.AddToClassList("ae-k"); p.Add(l); }
        static void Note(VisualElement p, string t) { var l = new Label(t); l.AddToClassList("ae-note"); p.Add(l); }

        // ───────── 화면 기본값(후처리) ─────────
        void PostPanel()
        {
            var P = PostSettings.F;
            var row = new VisualElement(); row.AddToClassList("ae-wrap"); keyList.Add(row);
            var tl0 = new Label("화면 기본값(후처리)"); tl0.AddToClassList("ae-sec"); row.Add(tl0);
            On(Btn(row, P.on ? "후처리: 켬" : "후처리: 끔", () => { P.on = !P.on; PostSettings.Save(); Rebuild(); }, "끄면 빛 번짐 · 화면 효과 트랙(색수차 · 블러 · 충격파 · 색 보정 · 기울기)이 안 보임(가벼움)"), P.on);
            On(Btn(row, P.flipY ? "중심 위아래: 뒤집음" : "중심 위아래: 그대로", () => { P.flipY = !P.flipY; PostSettings.Save(); Rebuild(); }, "충격파 · 블러 중심이 위아래 반대 자리에 보이면 바꾸기(그래픽 장치에 따라)"), P.flipY);
            void Save() { PostSettings.Save(); }

            // MFS 화면(URP): 게임과 같은 카메라 설정 · 볼륨 · 글로벌 라이트 2D
            var mr = new VisualElement(); mr.AddToClassList("ae-wrap"); keyList.Add(mr);
            Lbl(mr, "MFS 화면");
            if (!PrefabFx.UrpAvailable) Lbl(mr, "(이 프로젝트는 URP 가 아니라 쓸 수 없어요)");
            else
            {
                On(Btn(mr, P.mfs ? "URP 카메라: 켬" : "URP 카메라: 끔", () => { P.mfs = !P.mfs; PostSettings.Save(); Rebuild(); },
                    "켬 = 결투 그림 + 이펙트를 장면의 Main Camera 와 같은 URP 설정으로 그림 → 게임의 글로벌 볼륨 · 글로벌 라이트 2D 가 게임과 같은 코드로 먹음"), P.mfs);
                On(Btn(mr, P.volume ? "볼륨: 켬" : "볼륨: 끔", () => { P.volume = !P.volume; PostSettings.Save(); Rebuild(); }, "게임의 볼륨(Bloom · 톤매핑 · 컬러 그레이딩) 켜고 끄기 — 비교용"), P.volume);
                On(Btn(mr, P.stageFlip ? "무대 위아래: 뒤집음" : "무대 위아래: 그대로", () => { P.stageFlip = !P.stageFlip; PostSettings.Save(); Rebuild(); }, "URP 카메라를 켰을 때 결투 그림이 위아래 거꾸로 보이면 바꾸기(그래픽 장치에 따라)"), P.stageFlip);
                Btn(mr, "색 진단", () =>
                {
                    var f = field();
                    string rep = f?.Fx != null ? f.Fx.Diagnose() : "미리보기가 아직 없어요.";
                    Debug.Log("[Mori 색 진단]\n" + rep);
                    GUIUtility.systemCopyBuffer = rep;                                  // 길어서 창에서 잘려도 통째로 붙여넣을 수 있게
                    string file = null;
                    try { file = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "mori-color-diag.txt"); System.IO.File.WriteAllText(file, rep); } catch { file = null; }
                    Dialog.Info("색 진단", "결과를 클립보드에 복사했어요 — 채팅창에 Ctrl+V 로 붙여 넣으면 돼요."
                        + (file != null ? "\n파일로도 저장했어요: 바탕화면 mori-color-diag.txt" : "")
                        + "\n\n" + (rep.Length > 900 ? rep.Substring(0, 900) + " …(이하 생략 — 붙여넣기 · 파일에 전부 있어요)" : rep));
                }, "게임 카메라 · 볼륨(Global/Local) · 재생 중인 이펙트의 재질 · 정렬 레이어 · 비추는 글로벌 라이트를 보여 줌 — 게임과 색이 다를 때 원인 찾기");
                On(Btn(mr, P.light ? "글로벌 라이트: 켬" : "글로벌 라이트: 끔", () => { P.light = !P.light; PostSettings.Save(); Rebuild(); }, "켬 = 무대(캐릭터 · 배경)가 게임의 라이트 2D 를 받음 / 끔 = 원래 밝기"), P.light);
                var lr = new VisualElement(); lr.AddToClassList("ae-wrap"); keyList.Add(lr);
                Lbl(lr, "무대(캐릭터) 정렬 레이어");
                foreach (var l in SortingLayer.layers)
                {
                    string nm = l.name; bool lit = PrefabFx.LitByGlobal(nm);
                    var b = Btn(lr, lit ? nm + " · 빛" : nm, () => { P.charLayer = nm; PostSettings.Save(); Rebuild(); },
                        lit ? "글로벌 라이트 2D 가 비추는 레이어(· 빛)" : "글로벌 라이트 2D 가 비추지 않는 레이어 — 글로벌 라이트를 켜면 새까맣게 나와요");
                    On(b, PrefabFx.CharLayer == nm);
                }
                Btn(lr, "자동", () => { P.charLayer = ""; PostSettings.Save(); Rebuild(); }, "글로벌 라이트 2D 가 비추는 레이어 중에서 자동으로(캐릭터 · 가운데 이름 먼저)");
                if (P.light && !PrefabFx.LitByGlobal(PrefabFx.CharLayer))
                    Note(keyList, $"‘{PrefabFx.CharLayer}’ 레이어는 글로벌 라이트 2D 가 비추지 않아 무대가 새까맣게 나와요 — ‘· 빛’ 이 붙은 레이어를 고르세요.");
                var fr = new VisualElement(); fr.AddToClassList("ae-wrap"); keyList.Add(fr);
                Lbl(fr, "킷 이펙트 정렬 레이어");
                foreach (var l in SortingLayer.layers) { string nm = l.name; On(Btn(fr, nm, () => { P.fxLayer = nm; PostSettings.Save(); Rebuild(); }, "킷이 만드는 그림 · 잔상 · 조각 이펙트의 정렬 레이어(프리팹 이펙트는 프리팹 설정 그대로)"), PrefabFx.FxLayer == nm); }
                if (P.mfs) Note(keyList, "URP 카메라가 켜져 있으면 빛 번짐 · 비네트 · 채도 · 기본 색수차는 게임 볼륨이 맡아요(아래 값은 URP 카메라를 끌 때만). 화면 효과 트랙(충격파 · 블러 · 빛 · 일렁임 등)은 그 위에 더해져요.");
            }
            Num(keyList, "빛 번짐 세기", () => P.bloom, v => P.bloom = Mathf.Clamp(v, 0, 5), 0.05f, false, Save);
            Num(keyList, "빛 번짐 문턱(이 밝기부터 · 0 ~ 1)", () => P.threshold, v => P.threshold = Mathf.Clamp01(v), 0.02f, false, Save);
            Num(keyList, "문턱 부드럽게", () => P.knee, v => P.knee = Mathf.Clamp(v, 0.01f, 1), 0.02f, false, Save);
            Num(keyList, "빛 번짐 넓이(단계 1 ~ 8)", () => P.steps, v => P.steps = Mathf.Clamp(Mathf.RoundToInt(v), 1, 8), 1, true, Save);
            Num(keyList, "가장자리 어둡게(0 ~ 1)", () => P.vignette, v => P.vignette = Mathf.Clamp01(v), 0.05f, false, Save);
            Num(keyList, "기본 색수차(px)", () => P.chroma, v => P.chroma = Mathf.Clamp(v, 0, 20), 0.5f, false, Save);
            Num(keyList, "기본 채도", () => P.sat, v => P.sat = Mathf.Clamp(v, 0, 2), 0.05f, false, Save);
            Num(keyList, "후처리 해상도(0.25 ~ 1)", () => P.res, v => P.res = Mathf.Clamp(v, 0.25f, 1), 0.25f, false, Save);
        }

        // ───────── 프리셋(이름 붙인 카드) ─────────
        static string PresetDir => System.IO.Path.Combine(ArtLib.Dir, "fx-presets");

        void Presets()
        {
            var row = new VisualElement(); row.AddToClassList("ae-wrap"); keyList.Add(row);
            Lbl(row, "프리셋");
            string[] files = Array.Empty<string>();
            try { if (System.IO.Directory.Exists(PresetDir)) files = System.IO.Directory.GetFiles(PresetDir, "*.json").OrderBy(x => x).ToArray(); } catch { }
            if (files.Length == 0) Lbl(row, "(아직 없음)");
            foreach (var f in files)
            {
                string ff = f, nm = System.IO.Path.GetFileNameWithoutExtension(f);
                Btn(row, nm, () =>
                {
                    FxCard c = null;
                    try { c = FxCard.FromJson(System.IO.File.ReadAllText(ff)); } catch { }
                    if (c == null) return;
                    Snap(); card.tracks = c.tracks; card.length = c.length; sel = card.tracks.Count > 0 ? 0 : -1; showPresets = false; Commit();
                }, "이 프리셋의 트랙 · 길이로 바꿈(되돌리기 가능)");
            }
            var sv = new VisualElement(); sv.AddToClassList("ae-wrap"); keyList.Add(sv);
            Lbl(sv, "지금 카드를 이름으로");
            var tf = new TextField { value = presetName, maxLength = 40 };
            tf.AddToClassList("ae-text"); tf.style.width = 160;
            tf.RegisterValueChangedCallback(e => presetName = e.newValue ?? "");
            sv.Add(tf);
            Btn(sv, "저장", () =>
            {
                var bad = System.IO.Path.GetInvalidFileNameChars();
                string n = new string((presetName ?? "").Trim().Select(ch => bad.Contains(ch) ? '_' : ch).ToArray());
                if (n.Length == 0) return;
                try
                {
                    System.IO.Directory.CreateDirectory(PresetDir);
                    var c = card.Clone(); c.key = n; c.name = n;
                    KitBackup.Before(System.IO.Path.Combine(PresetDir, n + ".json"));
                    System.IO.File.WriteAllText(System.IO.Path.Combine(PresetDir, n + ".json"), c.ToJson());
                }
                catch (Exception e) { Debug.LogWarning("[Mori] 프리셋 저장 실패: " + e.Message); }
                presetName = "";
                Rebuild();
            }, "킷 데이터(Data~/Fight/fx-presets)에 저장(같은 이름이면 덮어씀 — 덮기 전 자동 백업)");
        }

        // ───────── 곡선 직접 다듬기 ─────────
        /// <summary>세기 곡선 점 편집: 점 끌기 · 빈 곳 더블클릭 = 더하기 · 점 오른쪽 클릭 = 지우기. 끄는 중엔 미리보기만, 놓으면 저장.</summary>
        sealed class CurveBox : VisualElement
        {
            const float PAD = 10;
            readonly CardPanel P;
            readonly FxTrack k;
            List<Vector2> pts;
            int drag = -1;

            public CurveBox(CardPanel p, FxTrack k)
            {
                P = p; this.k = k;
                pts = FxCurve.Parse(k.pts);
                AddToClassList("fx-curve");
                generateVisualContent += Draw;
                RegisterCallback<PointerDownEvent>(Down);
                RegisterCallback<PointerMoveEvent>(Move);
                RegisterCallback<PointerUpEvent>(e => { if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId); End(); });
                RegisterCallback<PointerCaptureOutEvent>(_ => End());
            }

            Rect R => new Rect(PAD, PAD, Mathf.Max(10, contentRect.width - PAD * 2), Mathf.Max(10, contentRect.height - PAD * 2));
            Vector2 ToPx(Vector2 v) { var r = R; return new Vector2(r.x + v.x * r.width, r.yMax - v.y * r.height); }
            Vector2 FromPx(Vector2 p) { var r = R; return new Vector2(Mathf.Clamp01((p.x - r.x) / r.width), Mathf.Clamp01((r.yMax - p.y) / r.height)); }

            int Hit(Vector2 p)
            {
                int best = -1; float bd = 12;
                for (int i = 0; i < pts.Count; i++) { float d = Vector2.Distance(ToPx(pts[i]), p); if (d < bd) { bd = d; best = i; } }
                return best;
            }

            void Down(PointerDownEvent e)
            {
                var p = (Vector2)e.localPosition;
                int i = Hit(p);
                if (e.button == 1)
                {
                    if (i > 0 && i < pts.Count - 1) { P.Snap(); pts.RemoveAt(i); Save(true); }
                    e.StopPropagation(); return;
                }
                if (e.button != 0) return;
                if (i < 0 && e.clickCount >= 2)
                {
                    var v = FromPx(p);
                    P.Snap(); pts.Add(v); pts.Sort((a, b) => a.x.CompareTo(b.x)); Save(true);
                    e.StopPropagation(); return;
                }
                if (i >= 0) { P.Snap(); drag = i; this.CapturePointer(e.pointerId); }
                e.StopPropagation();
            }

            void Move(PointerMoveEvent e)
            {
                if (drag < 0 || !this.HasPointerCapture(e.pointerId)) return;
                var v = FromPx(e.localPosition);
                // 처음 · 끝 점은 시간 고정, 가운데 점은 이웃 점 사이에서만
                if (drag == 0) v.x = 0;
                else if (drag == pts.Count - 1) v.x = 1;
                else v.x = Mathf.Clamp(v.x, pts[drag - 1].x + 0.01f, pts[drag + 1].x - 0.01f);
                pts[drag] = v;
                k.pts = FxCurve.Format(pts);
                P.Live();
                MarkDirtyRepaint();
            }

            void End()
            {
                if (drag >= 0) { drag = -1; Save(false); }
            }

            void Save(bool rebuild)
            {
                k.pts = FxCurve.Format(pts);
                P.Commit(false);
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                var r = R;
                if (contentRect.width < 30) return;
                p.fillColor = new Color(0.055f, 0.063f, 0.086f);
                p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(contentRect.width, 0)); p.LineTo(new Vector2(contentRect.width, contentRect.height)); p.LineTo(new Vector2(0, contentRect.height)); p.ClosePath(); p.Fill();
                p.strokeColor = new Color(1, 1, 1, 0.08f); p.lineWidth = 1;
                for (int i = 0; i <= 4; i++)
                {
                    float x = r.x + r.width * i / 4f, y = r.y + r.height * i / 4f;
                    p.BeginPath(); p.MoveTo(new Vector2(x, r.y)); p.LineTo(new Vector2(x, r.yMax)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(r.x, y)); p.LineTo(new Vector2(r.xMax, y)); p.Stroke();
                }
                var col = TypeColor(k.type);
                p.strokeColor = col; p.lineWidth = 2.5f;
                p.BeginPath();
                int n = Mathf.Clamp(Mathf.RoundToInt(r.width / 3), 20, 200);
                for (int i = 0; i <= n; i++)
                {
                    float u = i / (float)n;
                    var pt = ToPx(new Vector2(u, Mathf.Clamp01(FxCurve.EvalPts(pts, u))));
                    if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                }
                p.Stroke();
                for (int i = 0; i < pts.Count; i++)
                {
                    var c = ToPx(pts[i]);
                    p.fillColor = i == drag ? Color.white : new Color(0.95f, 0.96f, 0.98f);
                    p.strokeColor = new Color(0.05f, 0.06f, 0.08f); p.lineWidth = 2;
                    p.BeginPath(); p.Arc(c, 6, new Angle(0, AngleUnit.Degree), new Angle(360, AngleUnit.Degree)); p.Fill(); p.Stroke();
                }
            }
        }

        // ───────── 시간표 ─────────
        /// <summary>
        /// 시간표 그림: 왼쪽 이름 칸 + 오른쪽 막대 칸.
        /// 눈금 줄 · 그림 띠를 누르고 끌면 재생선 이동, 막대 가운데를 끌면 옮기기, 오른끝 = 길이, 왼끝 = 시작(끝은 그대로).
        /// </summary>
        sealed class Timeline : VisualElement
        {
            const float NAME_W = 104, RULER = 22, BAND = 24, ROW = 26;
            readonly CardPanel P;
            readonly VisualElement names, canvas, nums;
            int drag;               // 0 없음 · 1 재생선 · 2 옮기기 · 3 오른끝 · 4 왼끝
            int dragTrack, downFrame, origAt, origLen;
            bool changed;
            string before;

            public Timeline(CardPanel p)
            {
                P = p;
                AddToClassList("fx-tl");
                names = new VisualElement(); names.AddToClassList("fx-tl-names"); Add(names);
                canvas = new VisualElement(); canvas.AddToClassList("fx-tl-canvas"); Add(canvas);
                nums = new VisualElement { pickingMode = PickingMode.Ignore }; nums.style.position = Position.Absolute; nums.style.left = 0; nums.style.top = 0; nums.style.right = 0; nums.style.bottom = 0; canvas.Add(nums);
                canvas.generateVisualContent += Draw;
                canvas.RegisterCallback<PointerDownEvent>(Down);
                canvas.RegisterCallback<PointerMoveEvent>(MoveEv);
                canvas.RegisterCallback<PointerUpEvent>(Up);
                canvas.RegisterCallback<PointerCaptureOutEvent>(_ => End());
                canvas.RegisterCallback<GeometryChangedEvent>(_ => Labels());
            }

            FxCard C => P.card;

            int Frames()
            {
                var f = P.field();
                float end = f != null ? f.Fx.Dir.EndOf(C) : C.Sec(C.length);
                return Mathf.Max(10, Mathf.CeilToInt(end * C.fps) + 4);
            }

            float Ppf => Mathf.Max(0.5f, (canvas.contentRect.width - 8) / Frames());
            float X(float frame) => 4 + frame * Ppf;
            float FrameAt(float x) => (x - 4) / Ppf;

            public void Mark() => canvas.MarkDirtyRepaint();

            public void Rebuild()
            {
                names.Clear();
                float h = RULER + BAND + Mathf.Max(1, C.tracks.Count) * ROW + 4;
                style.height = h;
                var r0 = Name("", RULER); r0.text = "프레임";
                var r1 = Name("", BAND); r1.text = string.IsNullOrEmpty(C.preview) ? "캐릭터 —" : C.preview;
                for (int i = 0; i < C.tracks.Count; i++)
                {
                    var k = C.tracks[i];
                    int ii = i;
                    string label = TypeName(k.type) + (k.type == FxTrack.EFFECT && !string.IsNullOrEmpty(k.prefab) ? " " + k.prefab : k.type == FxTrack.SPRITE && !string.IsNullOrEmpty(k.anim) ? " " + k.anim : "");
                    var l = Name(label, ROW);
                    l.style.color = k.on ? TypeColor(k.type) : new Color(0.5f, 0.53f, 0.6f);
                    l.EnableInClassList("fx-tl-name--sel", i == P.sel);
                    l.tooltip = label;
                    l.RegisterCallback<PointerDownEvent>(e => { P.sel = ii; P.Rebuild(); e.StopPropagation(); });
                }
                Labels();
                Mark();
            }

            Label Name(string t, float h)
            {
                var l = new Label(t); l.AddToClassList("fx-tl-name"); l.style.height = h; names.Add(l);
                return l;
            }

            /// <summary>눈금 숫자(사이가 36px 이상 되게 간격을 고름).</summary>
            void Labels()
            {
                nums.Clear();
                if (canvas.contentRect.width < 20) return;
                int n = Frames();
                int step = 1;
                foreach (var s in new[] { 1, 2, 5, 10, 15, 20, 30, 60, 120 }) { step = s; if (s * Ppf >= 36) break; }
                for (int f = 0; f <= n; f += step)
                {
                    var l = new Label(f.ToString()) { pickingMode = PickingMode.Ignore };
                    l.AddToClassList("fx-tl-num");
                    l.style.left = X(f) + 2; l.style.top = 2;
                    nums.Add(l);
                }
            }

            void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                var r = canvas.contentRect;
                if (r.width < 20) return;
                int n = Frames();
                float ppf = Ppf;
                // 바탕 · 카드 길이 밖은 어둡게
                Fill(p, 0, 0, r.width, r.height, new Color(0.055f, 0.063f, 0.086f));
                Fill(p, X(C.length), 0, r.width - X(C.length), r.height, new Color(0, 0, 0, 0.35f));
                // 눈금
                int minor = ppf >= 6 ? 1 : ppf >= 2 ? 5 : 10;
                for (int f = 0; f <= n; f += minor)
                {
                    bool major = f % (minor * 5) == 0 || f % 10 == 0;
                    Line(p, X(f), major ? 0 : RULER * 0.55f, X(f), RULER, major ? new Color(0.55f, 0.6f, 0.7f) : new Color(0.3f, 0.34f, 0.42f), 1);
                    if (major) Line(p, X(f), RULER, X(f), r.height, new Color(1, 1, 1, 0.05f), 1);
                }
                Line(p, X(C.length), 0, X(C.length), r.height, new Color(0.99f, 0.84f, 0.21f, 0.8f), 2);       // 카드 끝(노랑)

                // 그림 띠: 캐릭터 동작의 컷 경계(히트스톱으로 늘어난 시간 반영)
                var a = string.IsNullOrEmpty(C.preview) ? null : ArtLib.Get(C.preview);
                float by = RULER + 2, bh = BAND - 4;
                if (a != null && a.Frames.Length > 0)
                {
                    // 카드 프레임 f 의 캐릭터 시간 → 컷 번호. 컷이 바뀌는 곳마다 칸을 나눔
                    int last = -1; float start = 0; int ci = 0;
                    float acc = 0, step = C.Sec(1);
                    for (int f = 0; f <= n; f++)
                    {
                        if (f > 0) acc += step * FxDirector.EvalCard(C, C.Sec(f - 1)).ScaleOf(P.HeroOwner);
                        int idx = f == n ? -2 : a.IndexAt(acc * 1000f);
                        if (idx != last)
                        {
                            if (last >= 0)
                            {
                                var col = ci % 2 == 0 ? new Color(0.24f, 0.29f, 0.4f) : new Color(0.17f, 0.2f, 0.28f);
                                Fill(p, X(start), by, X(f) - X(start) - 1, bh, col);
                                ci++;
                            }
                            last = idx; start = f;
                        }
                    }
                }
                else Fill(p, X(0), by, X(n) - X(0), bh, new Color(0.12f, 0.13f, 0.17f));

                // 트랙 막대
                for (int i = 0; i < C.tracks.Count; i++)
                {
                    var k = C.tracks[i];
                    float y = RULER + BAND + i * ROW + 3, h = ROW - 6;
                    if (i == P.sel) Fill(p, 0, y - 3, r.width, ROW, new Color(1, 1, 1, 0.05f));
                    var (x0, x1, auto) = Bar(k);
                    var col = k.on ? TypeColor(k.type) : new Color(0.4f, 0.42f, 0.48f);
                    Fill(p, x0, y, Mathf.Max(3, x1 - x0), h, new Color(col.r, col.g, col.b, auto ? 0.45f : 0.75f));
                    // 화면 트랙: 세기 곡선을 막대 안에
                    if (k.IsScreen && x1 - x0 > 6)
                    {
                        p.strokeColor = new Color(0.05f, 0.06f, 0.08f, 0.9f); p.lineWidth = 1.5f;
                        p.BeginPath();
                        int seg = Mathf.Clamp(Mathf.RoundToInt((x1 - x0) / 3), 4, 80);
                        for (int s = 0; s <= seg; s++)
                        {
                            float u = s / (float)seg;
                            float v = FxCurve.Eval(k, u);
                            var pt = new Vector2(x0 + (x1 - x0) * u, y + h - 2 - (h - 4) * v);
                            if (s == 0) p.MoveTo(pt); else p.LineTo(pt);
                        }
                        p.Stroke();
                    }
                    if (i == P.sel)
                    {
                        p.strokeColor = Color.white; p.lineWidth = 2;
                        p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.LineTo(new Vector2(x1, y + h)); p.LineTo(new Vector2(x0, y + h)); p.ClosePath(); p.Stroke();
                    }
                    // 끝 손잡이
                    Fill(p, x1 - 3, y + 3, 2, h - 6, new Color(0.05f, 0.06f, 0.08f, 0.8f));
                }

                // 재생선
                float px = X(P.t * C.fps);
                Line(p, px, 0, px, r.height, new Color(1f, 0.35f, 0.35f), 2);
                p.fillColor = new Color(1f, 0.35f, 0.35f);
                p.BeginPath(); p.MoveTo(new Vector2(px - 6, 0)); p.LineTo(new Vector2(px + 6, 0)); p.LineTo(new Vector2(px, 8)); p.ClosePath(); p.Fill();
            }

            (float x0, float x1, bool auto) Bar(FxTrack k)
            {
                var f = P.field();
                float dur = f != null ? f.Fx.Dir.Dur(C, k) : C.Sec(Mathf.Max(1, k.frames));
                // 여러 개 · 시작 무작위면 마지막 것이 끝날 수 있는 곳까지
                return (X(k.at), X(k.at + FxDirector.LastStart(k) + dur * C.fps), k.frames == 0 && k.type == FxTrack.EFFECT);
            }

            static void Fill(Painter2D p, float x, float y, float w, float h, Color c)
            {
                if (w <= 0 || h <= 0) return;
                p.fillColor = c;
                p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath(); p.Fill();
            }

            static void Line(Painter2D p, float x0, float y0, float x1, float y1, Color c, float w)
            {
                p.strokeColor = c; p.lineWidth = w;
                p.BeginPath(); p.MoveTo(new Vector2(x0, y0)); p.LineTo(new Vector2(x1, y1)); p.Stroke();
            }

            void Down(PointerDownEvent e)
            {
                if (e.button != 0) return;
                var pos = (Vector2)e.localPosition;
                float fr = FrameAt(pos.x);
                drag = 0; changed = false;
                if (pos.y < RULER + BAND)
                {
                    drag = 1;
                    P.Seek(P.card.Sec(Mathf.Max(0, Mathf.RoundToInt(fr))));
                }
                else
                {
                    int i = Mathf.FloorToInt((pos.y - RULER - BAND) / ROW);
                    if (i >= 0 && i < C.tracks.Count)
                    {
                        var k = C.tracks[i];
                        var (x0, x1, auto) = Bar(k);
                        if (P.sel != i) { P.sel = i; P.Rebuild(); }
                        if (pos.x >= x0 - 6 && pos.x <= x1 + 6)
                        {
                            drag = !auto && pos.x >= x1 - 7 ? 3 : pos.x <= x0 + 6 && x1 - x0 > 14 ? 4 : 2;
                            dragTrack = i; downFrame = Mathf.RoundToInt(fr); origAt = k.at; origLen = k.frames;
                            before = C.ToJson();
                        }
                        else { drag = 1; P.Seek(P.card.Sec(Mathf.Max(0, Mathf.RoundToInt(fr)))); }
                    }
                    else { drag = 1; P.Seek(P.card.Sec(Mathf.Max(0, Mathf.RoundToInt(fr)))); }
                }
                if (drag != 0) { canvas.CapturePointer(e.pointerId); e.StopPropagation(); }
            }

            void MoveEv(PointerMoveEvent e)
            {
                if (drag == 0 || !canvas.HasPointerCapture(e.pointerId)) return;
                float fr = FrameAt(e.localPosition.x);
                if (drag == 1) { P.Seek(P.card.Sec(Mathf.Max(0, Mathf.RoundToInt(fr)))); return; }
                if (dragTrack < 0 || dragTrack >= C.tracks.Count) return;
                var k = C.tracks[dragTrack];
                int d = Mathf.RoundToInt(fr) - downFrame;
                int at = k.at, len = k.frames;
                if (drag == 2) at = Mathf.Clamp(origAt + d, 0, 600);
                else if (drag == 3) len = Mathf.Clamp((origLen > 0 ? origLen : 1) + d, 1, 600);
                else if (drag == 4)
                {
                    int end = origAt + Mathf.Max(1, origLen);
                    at = Mathf.Clamp(origAt + d, 0, end - 1);
                    if (origLen > 0) len = end - at;
                }
                if (at == k.at && len == k.frames) return;
                k.at = at; k.frames = len;
                changed = true;
                var f = P.field();
                if (P.run != null && f != null) { P.run.card = C; f.Fx.Dir.Refresh(P.run); }
                Mark();
            }

            void Up(PointerUpEvent e)
            {
                if (canvas.HasPointerCapture(e.pointerId)) canvas.ReleasePointer(e.pointerId);
                End();
            }

            void End()
            {
                if (drag >= 2 && changed && before != null)
                {
                    P.undo.Add((before, null, null)); if (P.undo.Count > 100) P.undo.RemoveAt(0);
                    P.redo.Clear();
                    P.Commit();
                }
                drag = 0; changed = false; before = null;
            }
        }
    }
}
