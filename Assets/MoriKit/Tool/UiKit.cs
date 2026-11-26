using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>모든 창이 같이 쓰는 UI 도구. 스크롤 영역은 반드시 여기서 만든다(막대 모양·여백·휠 속도 통일).</summary>
    public static class UiKit
    {
        /// <summary>
        /// 스크롤 영역. 막대는 MoriUI.uss 의 '스크롤 막대' 규칙 하나로 통일되고,
        /// 안쪽 여백은 contentClass(내용 상자에 붙는 클래스)로 준다.
        /// (USS 로 '.x > .unity-scroll-view__content-viewport' 처럼 내부 구조를 짐작해 고르면 Unity 버전에 따라 안 맞는다 — tools/uss_lint.py E4)
        /// </summary>
        public static ScrollView Scroll(string cls, string contentClass, ScrollViewMode mode = ScrollViewMode.Vertical)
        {
            var sv = new ScrollView(mode);
            sv.AddToClassList("mori-scroll");
            if (!string.IsNullOrEmpty(cls)) foreach (var c in cls.Split(' ')) sv.AddToClassList(c);
            if (!string.IsNullOrEmpty(contentClass)) sv.contentContainer.AddToClassList(contentClass);
            sv.mouseWheelScrollSize = 60;
            sv.verticalScrollerVisibility = mode == ScrollViewMode.Horizontal ? ScrollerVisibility.Hidden : ScrollerVisibility.Auto;
            sv.horizontalScrollerVisibility = mode == ScrollViewMode.Vertical ? ScrollerVisibility.Hidden : ScrollerVisibility.Auto;
            return sv;
        }
    }
}
