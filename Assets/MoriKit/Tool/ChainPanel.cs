using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 작업실 ‘사슬’ 칸: 그림의 기준점(@이름)에서 찾은 사슬들 — 프리셋 꺼내 쓰기 · 수치 · 색 · 앞뒤 · 끄기. 바꾸면 바로 chains.json 에 저장.
    /// </summary>
    public sealed class ChainPanel
    {
        readonly VisualElement root, list, body;
        readonly Func<Color32> pickColor;
        string sel;                       // "hero:머리1"
        int seenArt = -1, seenStore = -1;

        public ChainPanel(VisualElement parent, Func<Color32> pickColor)
        {
            this.pickColor = pickColor;
            root = new VisualElement(); root.AddToClassList("ws-chain"); parent.Add(root);
            var hd = new VisualElement(); hd.AddToClassList("ae-wrap"); root.Add(hd);
            Note(hd, "사슬 = 직접 그린 머리카락 · 천(~이름)에 뼈대(@이름)를 심은 것. 편집기 ‘+ 사슬’ 로 만들고 ‘뼈대’ 도구로 마디를 찍어요.");
            list = new VisualElement(); list.AddToClassList("ae-wrap"); root.Add(list);
            body = new VisualElement(); body.AddToClassList("ws-tune"); root.Add(body);
            Rebuild();
        }

        /// <summary>그림 · 설정이 바뀌었으면 다시 만듦(매 프레임 불러도 가벼움).</summary>
        public void Tick()
        {
            if (seenArt == ArtLib.Version && seenStore == ChainStore.Version) return;
            Rebuild();
        }

        static List<string> Found()
        {
            var o = new List<string>();
            foreach (var a in ArtLib.AllAnims)
            {
                if (a?.Tracks == null || a.Key == null) continue;
                string owner = a.Key.StartsWith("hero") ? "hero" : a.Key.StartsWith("foe") ? "foe" : null;
                if (owner == null) continue;
                foreach (var t in a.Tracks) { var id = owner + ":" + t.Name; if (!o.Contains(id)) o.Add(id); }
            }
            foreach (var c in ChainStore.All) if (!o.Contains(c.name) && c.name.Contains(":")) o.Add(c.name);
            o.Sort();
            return o;
        }

        public void Rebuild()
        {
            seenArt = ArtLib.Version; seenStore = ChainStore.Version;
            list.Clear(); body.Clear();
            var found = Found();
            if (found.Count == 0) { Note(list, "아직 사슬이 없어요 — hero · foe 그림에 ‘+ 사슬’ 로 ~그림 + @뼈대를 만들면 여기 나와요."); return; }
            if (sel == null || !found.Contains(sel)) sel = found[0];
            foreach (var id in found)
            {
                string ii = id;
                var parts = id.Split(new[] { ':' }, 2);
                var b = Btn(list, (parts[0] == "hero" ? "플레이어 · " : "결투자 · ") + parts[1], () => { sel = ii; Rebuild(); }, "이 사슬 고르기");
                b.EnableInClassList("ae-btn--on", id == sel);
            }
            var sp = sel.Split(new[] { ':' }, 2);
            var cfg = ChainStore.Get(sp[0], sp[1]);

            // 프리셋 꺼내 쓰기
            var pr = new VisualElement(); pr.AddToClassList("ae-wrap"); body.Add(pr);
            Lbl(pr, "프리셋");
            foreach (var p in ChainStore.Presets)
            {
                var pp = p;
                var b = Btn(pr, p.name, () => { cfg.CopyLook(pp); Changed(); }, "이 프리셋의 모양 · 색을 꺼내 씀");
                b.EnableInClassList("ae-btn--on", cfg.preset == p.name);
            }
            Btn(pr, "지금 설정을 프리셋으로", () => { ChainStore.SavePreset(sp[1] + " 설정", cfg); Rebuild(); }, $"‘{sp[1]} 설정’ 이라는 프리셋으로 저장 — 다른 사슬에서 꺼내 쓸 수 있어요");

            Row("중력(배)", () => cfg.gravity, v => cfg.gravity = Mathf.Clamp(v, 0, 4), 0.05f);
            Row("공기 저항(0 ~ 1)", () => cfg.drag, v => cfg.drag = Mathf.Clamp01(v), 0.01f);
            Row("모양 유지 · 뻣뻣함(0 ~ 1)", () => cfg.stiff, v => cfg.stiff = Mathf.Clamp01(v), 0.02f);
            Row("몸 따라가기(0 ~ 1)", () => cfg.follow, v => cfg.follow = Mathf.Clamp01(v), 0.05f);

            var cr = new VisualElement(); cr.AddToClassList("ae-wrap"); body.Add(cr);
            var off = Btn(cr, cfg.off ? "이 사슬 꺼짐(그림이 안 휨)" : "이 사슬 켜짐", () => { cfg.off = !cfg.off; Changed(); }, "끄면 게임 · 미리보기에서 이 그림을 안 그려요");
            off.EnableInClassList("ae-btn--on", !cfg.off);
            Note(body, "그림 · 뼈대는 편집기에서: ~이름 = 휘는 그림, @이름 = 뼈대(‘뼈대’ 도구). 모양 유지가 높을수록 그린 모양을 지키고, 낮을수록 중력에 처져요. 몸 앞 · 뒤는 이름의 ‘(앞)’.");
        }

        void Changed() { ChainStore.Save(); Rebuild(); }

        void Row(string name, Func<float> get, Action<float> set, float step)
        {
            var row = new VisualElement(); row.AddToClassList("ws-tune-row"); body.Add(row);
            var l = new Label(name); l.AddToClassList("ae-k"); row.Add(l);
            var v = new Label(get().ToString("0.##")); v.AddToClassList("ws-tune-v");
            void Do(float d) { set((float)Math.Round(get() + d, 3)); v.text = get().ToString("0.##"); ChainStore.Save(); seenStore = ChainStore.Version; }
            var m = new Button(() => Do(-step)) { text = "−" }; m.AddToClassList("ae-btn"); row.Add(m);
            row.Add(v);
            var p = new Button(() => Do(step)) { text = "+" }; p.AddToClassList("ae-btn"); row.Add(p);
        }

        void ColorBtn(VisualElement parent, string name, Func<string> get, Action<string> set)
        {
            var sw = new VisualElement(); sw.AddToClassList("ws-swatch");
            sw.style.backgroundColor = (Color)ChainRig.Hex(get());
            parent.Add(sw);
            Btn(parent, name + " ← 지금 색", () => { set("#" + ColorUtility.ToHtmlStringRGB(pickColor())); Changed(); }, "편집기에서 고른 색을 넣어요");
        }

        static Button Btn(VisualElement p, string t, Action a, string tip)
        {
            var b = new Button(a) { text = t, tooltip = tip };
            b.AddToClassList("ae-btn");
            p.Add(b);
            return b;
        }

        static void Lbl(VisualElement p, string t) { var l = new Label(t); l.AddToClassList("ae-k"); p.Add(l); }
        static void Note(VisualElement p, string t) { var l = new Label(t); l.AddToClassList("ae-note"); p.Add(l); }
    }
}
