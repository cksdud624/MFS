using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 칸이 눌려 글자가 잘리거나 서로 겹치는 것(겹친 칸 때문에 버튼이 안 눌리는 것 포함)을 막는 실행 중 안전장치.
    ///
    /// 원인: UI Toolkit 은 세로로 쌓인 칸들이 부모보다 길면 기본값(flex-shrink 1)으로 각 칸을 내용보다 작게 눌러 버린다.
    ///       그러면 제목 · 탭 · 버튼이 겹쳐 그려지고, 위에 겹친 칸이 클릭을 가로챈다.
    /// 처리: 화면 크기 · 구성이 바뀔 때마다 한 번 훑어서, 내용이 필요로 하는 높이(글자 크기 · 자식 칸 합 · 여백)보다
    ///       작게 눌린 칸은 더 줄어들지 않게(flex-shrink 0) 고친다. 넘치는 부분은 스크롤 영역이 맡는다.
    ///       개발 중에는 고친 칸을 Console 로 알려 준다 — 스타일에서도 flex-shrink: 0 을 주라는 신호.
    /// FitInside: 스크롤이 없는 고정 화면(타이틀 메뉴 등)은 내용이 넘치면 통째로 줄여(scale) 겹치지 않게 한다.
    /// 짝: tools/uss_lint.py (E6 고정 높이 · E8 Unity 가 못 읽는 값), TextGuard(자간 · 좁은 글자).
    /// </summary>
    public static class LayoutGuard
    {
        static IVisualElementScheduledItem pending;
        static readonly Dictionary<VisualElement, float> req = new Dictionary<VisualElement, float>();
        static readonly HashSet<string> told = new HashSet<string>();

        /// <summary>루트에 걸어 두면 크기 · 구성이 바뀔 때마다(잠깐 모아서) 검사한다.</summary>
        public static void Watch(VisualElement root)
        {
            root.RegisterCallback<GeometryChangedEvent>(_ => Queue(root), TrickleDown.TrickleDown);
            Queue(root);
        }

        /// <summary>화면을 연 뒤 그 범위만 한 번(두 번) 검사.</summary>
        public static void Check(VisualElement scope)
        {
            if (scope == null) return;
            scope.schedule.Execute(() => { if (Fix(scope) > 0) scope.schedule.Execute(() => Fix(scope)).StartingIn(60); }).StartingIn(80);
        }

        public static void Queue(VisualElement root)
        {
            pending?.Pause();
            pending = root.schedule.Execute(() =>
            {
                // 하나를 고치면 배치가 다시 잡히므로 한 번 더(최대 3번)
                int n = Fix(root);
                if (n > 0) root.schedule.Execute(() => { if (Fix(root) > 0) root.schedule.Execute(() => Fix(root)).StartingIn(60); }).StartingIn(60);
            }).StartingIn(120);
        }

        static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None && e.resolvedStyle.visibility != Visibility.Hidden;

        static bool ScrollInner(VisualElement e) =>
            e != null && (e.ClassListContains("unity-scroll-view__content-viewport") || e.ClassListContains("unity-scroll-view__content-container")
                          || e.ClassListContains("unity-scroll-view__content-and-vertical-scroll-container") || e is Scroller);

        /// <summary>눌린 칸을 고친 개수.</summary>
        public static int Fix(VisualElement scope)
        {
            if (scope == null || scope.panel == null) return 0;
            req.Clear();
            Req(scope);
            int n = 0;
            foreach (var kv in req)
            {
                var e = kv.Key;
                var p = e.parent;
                if (p == null || !Shown(e) || ScrollInner(p) || ScrollInner(e)) continue;
                var rs = e.resolvedStyle;
                if (rs.position == Position.Absolute || rs.flexShrink <= 0) continue;
                var pd = p.resolvedStyle.flexDirection;
                if (pd != FlexDirection.Column && pd != FlexDirection.ColumnReverse) continue;   // 세로로 쌓인 칸만 높이가 눌린다
                float h = e.layout.height;
                if (float.IsNaN(h) || kv.Value <= h + 1.5f) continue;
                e.style.flexShrink = 0;
                n++;
#if UNITY_EDITOR
                string key = string.Join(".", e.GetClasses()) + "|" + e.name;
                if (told.Add(key))
                    Debug.Log($"[Mori LayoutGuard] 눌린 칸을 고침: {Describe(e)} (높이 {h:0} < 내용 {kv.Value:0}) — 스타일에 flex-shrink: 0 을 주세요.");
#endif
            }
            return n;
        }

        /// <summary>칸이 내용 때문에 필요한 최소 높이(재귀, 이번 검사 동안 기억).</summary>
        static float Req(VisualElement e)
        {
            if (req.TryGetValue(e, out var have)) return have;
            if (!Shown(e)) { req[e] = 0; return 0; }
            var rs = e.resolvedStyle;
            float pad = rs.paddingTop + rs.paddingBottom + rs.borderTopWidth + rs.borderBottomWidth;
            float need = 0;
            if (e is ScrollView)
            {
                // 스크롤 영역은 내용이 넘쳐도 된다 — 자기 최소 높이만
                need = MinH(rs);
                foreach (var c in e.Query<VisualElement>().Build()) if (c != e) Req(c);
                req[e] = need;
                return need;
            }
            if (e is TextElement te && !string.IsNullOrEmpty(te.text) && !(e is TextField) && !(e.parent is TextElement))
            {
                float w = e.layout.width - rs.paddingLeft - rs.paddingRight - rs.borderLeftWidth - rs.borderRightWidth;
                if (w > 1 && !float.IsNaN(w))
                    need = te.MeasureTextSize(te.text, w, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined).y + pad;
            }
            bool col = rs.flexDirection == FlexDirection.Column || rs.flexDirection == FlexDirection.ColumnReverse;
            bool wrap = rs.flexWrap != Wrap.NoWrap;
            float sum = 0, max = 0, rowsBottom = 0;
            int kids = 0;
            foreach (var c in e.hierarchy.Children())
            {
                float r = Req(c);
                if (!Shown(c) || c.resolvedStyle.position == Position.Absolute) continue;
                var cs = c.resolvedStyle;
                float hc = Math.Max(r, MinH(cs)) + cs.marginTop + cs.marginBottom;
                sum += hc;
                max = Math.Max(max, hc);
                // 줄바꿈되는 가로 줄(탭 등): 실제 놓인 아래 끝까지
                if (wrap && !col && !float.IsNaN(c.layout.yMax)) rowsBottom = Math.Max(rowsBottom, c.layout.y + hc - cs.marginTop);
                kids++;
            }
            if (kids > 0) need = Math.Max(need, (col ? sum : wrap ? Math.Max(max, rowsBottom - rs.paddingTop) : max) + pad);
            need = Math.Max(need, MinH(rs));
            req[e] = need;
            return need;
        }

        static float MinH(IResolvedStyle rs)
        {
            float m = rs.minHeight.value;
            return float.IsNaN(m) || rs.minHeight.keyword == StyleKeyword.Auto ? 0 : m;
        }

        static string Describe(VisualElement e)
        {
            string cls = string.Join(" ", e.GetClasses());
            string t = e is TextElement te && !string.IsNullOrEmpty(te.text) ? $" \"{(te.text.Length > 16 ? te.text.Substring(0, 16) + "…" : te.text)}\"" : "";
            return $"{e.GetType().Name}({cls}){t}";
        }

        /// <summary>
        /// 스크롤이 없는 고정 화면용: container 의 자식들(세로로 쌓인 것)이 container 안쪽 높이보다 크면 통째로 줄여서(scale) 넘치지 않게.
        /// 창이 커지면 다시 1배로.
        /// </summary>
        public static void FitInside(VisualElement container, float minScale = 0.55f)
        {
            container.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var rs = container.resolvedStyle;
                float avail = container.layout.height - rs.paddingTop - rs.paddingBottom;
                if (float.IsNaN(avail) || avail <= 0) return;
                float need = 0;
                foreach (var c in container.Children())
                {
                    if (!Shown(c) || c.resolvedStyle.position == Position.Absolute) continue;
                    need += c.layout.height + c.resolvedStyle.marginTop + c.resolvedStyle.marginBottom;
                }
                float s = need > avail ? Mathf.Max(minScale, avail / need) : 1f;
                var cur = container.resolvedStyle.scale.value;
                if (Mathf.Abs(cur.x - s) > 0.01f) container.style.scale = new Scale(new Vector2(s, s));
            });
        }
    }
}
