using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 작업실 ‘프리팹’ 칸: 순간(대쉬 · 공격 · 카운터 …)마다 어떤 프리팹을 어디에 · 얼마나 크게 재생할지. 바꾸면 바로 prefabs.json 에 저장.
    /// </summary>
    public sealed class FxPanel
    {
        readonly VisualElement root, head, keyList, body;
        readonly Func<StageWorld> world;
        readonly Func<StageField> field;
        string sel = FxKeys.ALL[0].key;
        int seenLib = -1, seenStore = -1;

        public FxPanel(VisualElement parent, Func<StageWorld> world, Func<StageField> field)
        {
            this.world = world; this.field = field;
            root = new VisualElement(); root.AddToClassList("ws-chain"); parent.Add(root);
            head = new VisualElement(); head.AddToClassList("ae-wrap"); root.Add(head);
            keyList = new VisualElement(); keyList.AddToClassList("ae-wrap"); root.Add(keyList);
            body = new VisualElement(); body.AddToClassList("ws-tune"); root.Add(body);
            Rebuild();
        }

        /// <summary>등록부 · 연결 표가 바뀌었으면 다시 만듦(매 프레임 불러도 가벼움).</summary>
        public void Tick()
        {
            if (seenLib == PrefabLib.Version && seenStore == FxStore.Version) return;
            Rebuild();
        }

        public void Rebuild()
        {
            var names = PrefabLib.All.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            seenLib = PrefabLib.Version; seenStore = FxStore.Version;
            head.Clear(); keyList.Clear(); body.Clear();
            var F = FxStore.F;

            Lbl(head, $"등록된 프리팹 {names.Count}개");
            Btn(head, "다시 읽기", () => { PrefabLib.Refresh(); Rebuild(); }, "Resources/MoriFx 폴더 · 프리팹 등록부를 다시 읽어요(프리팹을 새로 넣은 뒤)");
            On(Btn(head, F.glowFix ? "빛 보정: 켬" : "빛 보정: 끔", () => { F.glowFix = !F.glowFix; Changed(); },
                "켬 = 더하기(빛) 파티클이 투명 바탕에서 사라지지 않게 밝기를 알파로 옮김 · 이상하게 보이면 끄기"), F.glowFix);
            Btn(head, F.behind ? "그리는 곳: 캐릭터 뒤" : "그리는 곳: 캐릭터 앞", () => { F.behind = !F.behind; Changed(); }, "프리팹 층을 캐릭터 앞 · 뒤 중 어디에 둘지(모든 프리팹 공통)");
            Btn(head, $"해상도 {Mathf.RoundToInt(Mathf.Clamp(F.res, 0.25f, 1f) * 100)}%", () => { F.res = F.res >= 1f ? 0.75f : F.res >= 0.75f ? 0.5f : 1f; Changed(); },
                "프리팹 화면의 해상도(화면 대비) — 낮추면 가볍고 흐려짐");
            if (names.Count == 0)
            {
                Note(head,
                    "아직 프리팹이 없어요. ① 가져온 프리팹을 Assets/Mori/Client/Resources/MoriFx 폴더로 옮기거나, " +
                    "② 아무 Resources 폴더에서 오른쪽 클릭 ▸ Create ▸ Mori ▸ 프리팹 등록부 를 만들고 목록에 끌어다 넣은 뒤 ‘다시 읽기’.");
            }

            foreach (var (key, name) in FxKeys.ALL)
            {
                string kk = key;
                var c = FxStore.Find(key);
                bool used = c != null && !string.IsNullOrEmpty(c.prefab);
                var b = Btn(keyList, used ? name + " · " + c.prefab : name, () => { sel = kk; Rebuild(); }, "이 순간에 붙일 프리팹 고르기");
                b.EnableInClassList("ae-btn--on", key == sel);
                if (used && key != sel) b.AddToClassList("ws-fx-used");
            }

            var cfg = FxStore.Get(sel);
            Lbl(body, "‘" + FxKeys.NameOf(sel) + "’ 에 쓸 프리팹");
            var pr = new VisualElement(); pr.AddToClassList("ae-wrap"); body.Add(pr);
            On(Btn(pr, "없음", () => { cfg.prefab = ""; Changed(); }, "이 순간엔 프리팹을 쓰지 않음"), string.IsNullOrEmpty(cfg.prefab));
            foreach (var n in names)
            {
                string nn = n;
                On(Btn(pr, n, () => { cfg.prefab = nn; Changed(); }, "이 프리팹을 씀"), string.Equals(cfg.prefab, n, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(cfg.prefab) && PrefabLib.Get(cfg.prefab) == null)
                Note(body, $"‘{cfg.prefab}’ 프리팹을 찾지 못했어요 — 등록부 · MoriFx 폴더를 확인하고 ‘다시 읽기’.");

            Row("옆으로(U, 바라보는 쪽 +)", () => cfg.dx, v => cfg.dx = v, 0.02f);
            Row("위로(U)", () => cfg.dy, v => cfg.dy = v, 0.02f);
            Row("크기(배)", () => cfg.scale, v => cfg.scale = Mathf.Max(0.01f, v), 0.02f);
            Row("회전(도)", () => cfg.rot, v => cfg.rot = Mathf.Repeat(v + 180, 360) - 180, 15f);
            Row("수명(초, 0 = 자동)", () => cfg.life, v => cfg.life = Mathf.Max(0, v), 0.1f);

            var tg = new VisualElement(); tg.AddToClassList("ae-wrap"); body.Add(tg);
            On(Btn(tg, cfg.flip ? "방향 따라 뒤집기: 켬" : "방향 따라 뒤집기: 끔", () => { cfg.flip = !cfg.flip; Changed(); }, "왼쪽을 보면 좌우를 뒤집음(오른쪽을 보는 프리팹 기준)"), cfg.flip);
            On(Btn(tg, cfg.follow ? "캐릭터 따라가기: 켬" : "캐릭터 따라가기: 끔", () => { cfg.follow = !cfg.follow; Changed(); }, "켬 = 재생 중에도 캐릭터를 따라 움직임(대쉬 꼬리 등) · 끔 = 그 자리에 남음"), cfg.follow);
            On(Btn(tg, cfg.off ? "이 연결 꺼짐" : "이 연결 켜짐", () => { cfg.off = !cfg.off; Changed(); }, "끄면 설정은 남기고 재생만 안 함"), !cfg.off);
            Btn(tg, "시험 재생", () => Test(cfg), "미리보기에서 지금 바로 재생(캐릭터 자리 기준)", "ae-btn--main");
            Note(body, "위치는 플레이어 · 결투자 발밑(또는 타격 지점) 기준. 크기는 그림 크기 배율을 따라가요. 프리팹이 분홍색이면 셰이더 · 렌더 파이프라인을 확인하세요.");
        }

        void Test(FxCfg cfg)
        {
            var w = world(); var f = field();
            if (w == null || f == null) return;
            if (string.IsNullOrEmpty(cfg.prefab)) return;
            f.Fx.PlayCfg(cfg, FxTest.Ctx(w, cfg.key));
        }

        void Changed() { FxStore.Save(); Rebuild(); }


        void Row(string name, Func<float> get, Action<float> set, float step)
        {
            var row = new VisualElement(); row.AddToClassList("ws-tune-row"); body.Add(row);
            var l = new Label(name); l.AddToClassList("ae-k"); row.Add(l);
            var v = new Label(get().ToString("0.##")); v.AddToClassList("ws-tune-v");
            void Do(float d) { set((float)Math.Round(get() + d, 3)); v.text = get().ToString("0.##"); FxStore.Save(); seenStore = FxStore.Version; }
            var m = new Button(() => Do(-step)) { text = "−" }; m.AddToClassList("ae-btn"); row.Add(m);
            row.Add(v);
            var p = new Button(() => Do(step)) { text = "+" }; p.AddToClassList("ae-btn"); row.Add(p);
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
    }

    /// <summary>작업실 시험 재생의 기준점: 순간 이름으로 쓰는 쪽 · 맞는 쪽 · 타격 지점을 그럴듯하게.</summary>
    public static class FxTest
    {
        public static MoriFx.FxCtx Ctx(StageWorld w, string key)
        {
            bool hero = PrefabFx.HeroSide(key);
            var self = hero ? (w.Hero.X, w.Hero.Y) : (w.Foe.X, w.Foe.Y);
            var other = hero ? (w.Foe.X, w.Foe.Y) : (w.Hero.X, w.Hero.Y);
            bool hitAt = key.StartsWith("counter.") || key.StartsWith("foe.hit") || key == "map.strike";
            double hx = hitAt ? (self.Item1 + other.Item1) / 2 : self.Item1;
            double hy = hitAt ? Math.Max(self.Item2, other.Item2) + 1 : self.Item2;
            if (key.StartsWith("hero.hit.")) { var hb = w.Foe.Hurtbox; hx = hb.Cx; hy = hb.Cy; }          // 적중: 결투자 몸 가운데
            else if (key.StartsWith("hero.slash") || key.StartsWith("hero.mech"))
            {
                var ab = w.Hero.AttackBox(Mathf.Clamp(key[key.Length - 1] - '0', 1, 3));                    // 휘두름: 공격 판정 가운데
                hx = ab.Cx; hy = ab.Cy;
            }
            int face = hero ? w.Hero.Facing : w.Foe.Facing;
            return PrefabFx.Ctx(w, hero, hx, hy, face);
        }
    }
}
