using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>
    /// 글자가 한두 자씩 세로로 떨어지는 것을 막는 실행 중 안전장치(스타일 검사 tools/uss_lint.py 와 짝).
    /// 화면을 그린 다음 프레임에 한 번 훑어서
    ///  · 자간이 있는데 줄바꿈이 켜진 글자 → 한 줄로(Unity 가 자간을 빼고 폭을 재서 마지막 글자가 떨어짐)
    ///  · 줄 안에서 눌려 글자 두 개 폭보다 좁아진 짧은 글자 → 한 줄 + 줄어들지 않게
    /// 로 고친다. 고친 것이 있으면 개발 중에 Console 로 알려 준다(스타일을 고치라는 신호).
    /// 또 개발 중에는 글자가 자기 칸이나 부모 칸 밖으로 나가는지 점검해 경고한다(LayoutGuard).
    /// </summary>
    public static class TextGuard
    {
        public static void Schedule(VisualElement scope)
        {
            if (scope == null) return;
            scope.schedule.Execute(() => Fix(scope)).StartingIn(30);
            LayoutGuard.Check(scope);
        }

        public static int Fix(VisualElement scope)
        {
            int n = 0;
            scope.Query<Label>().ForEach(l =>
            {
                if (l.resolvedStyle.display == DisplayStyle.None || string.IsNullOrEmpty(l.text)) return;
                bool wraps = l.resolvedStyle.whiteSpace == WhiteSpace.Normal;
                if (!wraps) return;
                float ls = l.resolvedStyle.letterSpacing;
                float fs = l.resolvedStyle.fontSize;
                float w = l.layout.width;
                bool spaced = ls > 0.01f;
                bool squeezed = !float.IsNaN(w) && w > 0 && w < fs * 2.2f && l.text.Length > 2 && l.text.IndexOf('\n') < 0;
                if (!spaced && !squeezed) return;
                l.style.whiteSpace = WhiteSpace.NoWrap;
                if (squeezed) l.style.flexShrink = 0;
                n++;
#if UNITY_EDITOR
                Debug.Log($"[Mori TextGuard] 한 줄로 고침: \"{Short(l.text)}\" (클래스 {string.Join(" ", l.GetClasses())}, 자간 {ls:0.#}, 폭 {w:0})");
#endif
            });
#if UNITY_EDITOR
            CheckClip(scope);
#endif
            return n;
        }

        static string Short(string t) => t.Length > 24 ? t.Substring(0, 24) + "…" : t;

#if UNITY_EDITOR
        /// <summary>
        /// 개발 중 점검: 글자가 자기 칸보다 크거나(고정 높이) 부모 칸 밖으로 나가면 Console 에 알린다.
        /// 같은 글자는 한 번만 알린다.
        /// </summary>
        static readonly System.Collections.Generic.HashSet<string> told = new System.Collections.Generic.HashSet<string>();

        static void CheckClip(VisualElement scope)
        {
            scope.Query<Label>().ForEach(l =>
            {
                if (l.resolvedStyle.display == DisplayStyle.None || string.IsNullOrEmpty(l.text) || l.resolvedStyle.position == Position.Absolute) return;
                var r = l.layout;
                if (float.IsNaN(r.width) || r.width <= 0 || r.height <= 0) return;
                var need = l.MeasureTextSize(l.text, r.width - l.resolvedStyle.paddingLeft - l.resolvedStyle.paddingRight, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                float inner = r.height - l.resolvedStyle.paddingTop - l.resolvedStyle.paddingBottom;
                string why = null;
                if (need.y > inner + 2) why = $"칸 높이 {inner:0} < 글자 {need.y:0}";
                else
                {
                    // 부모 칸 밖으로 삐져나온 글자(부모가 잘라 내면 잘려 보이고, 아니면 옆 칸을 덮는다)
                    var p = l.parent;
                    if (p != null && !p.ClassListContains("unity-scroll-view__content-container"))
                    {
                        var wb = l.worldBound; var pb = p.worldBound;
                        if (wb.yMax > pb.yMax + 2 || wb.xMax > pb.xMax + 2 || wb.yMin < pb.yMin - 2 || wb.xMin < pb.xMin - 2)
                            why = $"부모 칸({string.Join(" ", p.GetClasses())}) 밖으로 나감";
                    }
                }
                if (why == null || !told.Add(l.text + why)) return;
                Debug.LogWarning($"[Mori LayoutGuard] 글자가 잘림: \"{Short(l.text)}\" (클래스 {string.Join(" ", l.GetClasses())}) — {why}");
            });
        }
#endif
    }
}
