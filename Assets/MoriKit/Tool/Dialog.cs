using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mori.Client
{
    /// <summary>확인 · 안내 · 이름 입력 창(맨 위에 뜸). Esc 는 가장 위의 창을 닫는다(취소).</summary>
    public static class Dialog
    {
        static VisualElement host;
        static readonly List<(VisualElement dim, Action cancel)> stack = new List<(VisualElement, Action)>();

        public static void Init(VisualElement root) { host = root; stack.Clear(); }

        public static bool IsOpen => stack.Count > 0;

        static (VisualElement dim, VisualElement box) Make(string title, string body)
        {
            var dim = new VisualElement();
            dim.AddToClassList("dg-dim");
            var box = new VisualElement();
            box.AddToClassList("dg-box");
            dim.Add(box);
            var t = new Label(title); t.AddToClassList("dg-t"); box.Add(t);
            if (!string.IsNullOrEmpty(body)) { var b = new Label(body); b.AddToClassList("dg-b"); box.Add(b); }
            host.Add(dim);
            dim.BringToFront();
            box.schedule.Execute(() => box.AddToClassList("dg-box--in")).StartingIn(16);
            return (dim, box);
        }

        static Button Btn(VisualElement p, string text, string cls, Action a)
        {
            var b = new Button(a) { text = text };
            foreach (var c in ("mb " + cls).Split(' ')) if (c.Length > 0) b.AddToClassList(c);
            p.Add(b);
            return b;
        }

        static void Close(VisualElement dim)
        {
            stack.RemoveAll(x => x.dim == dim);
            dim.RemoveFromHierarchy();
        }

        /// <summary>Esc: 가장 위 창을 취소로 닫는다. 닫은 창이 있으면 true.</summary>
        public static bool CloseTop()
        {
            if (stack.Count == 0) return false;
            var top = stack[stack.Count - 1];
            Close(top.dim);
            top.cancel?.Invoke();
            return true;
        }

        public static void Info(string title, string body, string ok = "확인", Action onOk = null)
        {
            if (host == null) return;
            var (dim, box) = Make(title, body);
            var row = new VisualElement(); row.AddToClassList("dg-btns"); box.Add(row);
            Btn(row, ok, "mb--gold", () => { Close(dim); onOk?.Invoke(); }).Focus();
            stack.Add((dim, onOk));
        }

        /// <summary>확인 창. danger 면 확인 버튼이 빨간 테두리(지우기·덮어쓰기 등).</summary>
        public static void Confirm(string title, string body, string ok, bool danger, Action onOk, string cancel = "취소", Action onCancel = null)
        {
            if (host == null) return;
            var (dim, box) = Make(title, body);
            var row = new VisualElement(); row.AddToClassList("dg-btns"); box.Add(row);
            Btn(row, cancel, "mb--ghost", () => { Close(dim); onCancel?.Invoke(); });
            Btn(row, ok, danger ? "mb--danger" : "mb--gold", () => { Close(dim); onOk?.Invoke(); });
            stack.Add((dim, onCancel));
        }

        /// <summary>글자 입력 창(슬롯 이름 등). validate 가 오류 문구를 돌려주면 닫지 않고 보여 준다.</summary>
        /// <summary>picks: 고르면 그 값으로 바로 확정되는 빠른 선택 칩(예: 이벤트 이름) — 제목 picksTitle 아래 줄지어 놓인다.</summary>
        public static void Prompt(string title, string body, string initial, string ok, Action<string> onOk, Func<string, string> validate = null, int maxLen = 20,
            IList<(string label, string value, string tip)> picks = null, string picksTitle = null)
        {
            if (host == null) return;
            var (dim, box) = Make(title, body);
            if (picks != null && picks.Count > 0)
            {
                if (!string.IsNullOrEmpty(picksTitle)) { var pt = new Label(picksTitle); pt.AddToClassList("dg-pk-t"); box.Add(pt); }
                var wrap = new VisualElement(); wrap.AddToClassList("dg-pk"); box.Add(wrap);
                foreach (var pk in picks)
                {
                    var v = pk.value;
                    var b = Btn(wrap, pk.label, "dg-chip", () => { Close(dim); onOk?.Invoke(v); });
                    if (!string.IsNullOrEmpty(pk.tip)) b.tooltip = pk.tip;
                }
            }
            var tf = new TextField { value = initial ?? "", maxLength = maxLen };
            tf.AddToClassList("dg-in");
            box.Add(tf);
            var err = new Label(""); err.AddToClassList("dg-err"); box.Add(err);
            var row = new VisualElement(); row.AddToClassList("dg-btns"); box.Add(row);
            void Submit()
            {
                string v = (tf.value ?? "").Trim();
                string e = validate?.Invoke(v);
                if (!string.IsNullOrEmpty(e)) { err.text = e; return; }
                Close(dim);
                onOk?.Invoke(v);
            }
            Btn(row, "취소", "mb--ghost", () => Close(dim));
            Btn(row, ok, "mb--gold", Submit);
            tf.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Submit(); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            stack.Add((dim, null));
            tf.schedule.Execute(() => { tf.Focus(); tf.SelectAll(); }).StartingIn(40);
        }
    }
}
