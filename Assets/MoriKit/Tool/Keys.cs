using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Mori.Client
{
    /// <summary>
    /// 리듬 키 입력(프레임마다 확인). Input System 패키지가 있으면 그것을, 없으면 예전 Input Manager 를 쓴다.
    /// </summary>
    public static class Keys
    {
        public enum K { S, D, Space, Enter, P, R, Z, Ctrl, Shift, Left, Right, Up, Down, Delete, Escape, F2, F3, F4, F5 }

        /// <summary>프레임마다 키를 직접 읽을 수 있는지(Input System 또는 예전 Input Manager). 없으면 화면 키 이벤트(Feed)로 대신한다.</summary>
#if (ENABLE_INPUT_SYSTEM && MORI_INPUTSYS) || ENABLE_LEGACY_INPUT_MANAGER
        public const bool HasPolling = true;
#else
        public const bool HasPolling = false;
#endif
        static readonly System.Collections.Generic.Dictionary<K, int> fed = new System.Collections.Generic.Dictionary<K, int>();

        /// <summary>화면(UI Toolkit) 키 이벤트로 들어온 누름 — 직접 읽기가 안 될 때만 쓴다(같은 누름이 두 번 세어지지 않게).</summary>
        public static void Feed(KeyCode code)
        {
            if (HasPolling && Available) return;
            K k;
            switch (code)
            {
                case KeyCode.S: k = K.S; break;
                case KeyCode.D: k = K.D; break;
                case KeyCode.Space: k = K.Space; break;
                case KeyCode.Return: case KeyCode.KeypadEnter: k = K.Enter; break;
                case KeyCode.LeftArrow: k = K.Left; break;
                case KeyCode.RightArrow: k = K.Right; break;
                case KeyCode.UpArrow: k = K.Up; break;
                case KeyCode.DownArrow: k = K.Down; break;
                case KeyCode.Escape: k = K.Escape; break;
                case KeyCode.F2: k = K.F2; break;
                case KeyCode.F3: k = K.F3; break;
                case KeyCode.F4: k = K.F4; break;
                case KeyCode.F5: k = K.F5; break;
                default: return;
            }
            fed[k] = Time.frameCount;
        }

        /// <summary>S · D · 스페이스를 프레임마다(또는 이벤트로) 직접 읽을 수 있는지. false 면 화면 키 이벤트로 대신해야 한다.</summary>
        public static bool Live => hooked || (HasPolling && Available);

        /// <summary>지금 눌려 있는지(Held)를 키보드에서 직접 읽을 수 있는지.</summary>
        public static bool CanPoll => HasPolling && Available;

        /// <summary>실행 중에 키보드를 읽을 수 있는지(Input System 이 켜져 있어도 키보드 장치가 없을 수 있다).</summary>
        static bool Available
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
                return Keyboard.current != null;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return true;
#else
                return false;
#endif
            }
        }

        static bool Fed(K k) => fed.TryGetValue(k, out int f) && (f == Time.frameCount || f == Time.frameCount - 1) && fed.Remove(k);

        public static bool Down(K k)
        {
            if (Fed(k)) return true;
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case K.S: return kb.sKey.wasPressedThisFrame;
                case K.D: return kb.dKey.wasPressedThisFrame;
                case K.Space: return kb.spaceKey.wasPressedThisFrame;
                case K.Enter: return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
                case K.P: return kb.pKey.wasPressedThisFrame;
                case K.R: return kb.rKey.wasPressedThisFrame;
                case K.Z: return kb.zKey.wasPressedThisFrame;
                case K.Left: return kb.leftArrowKey.wasPressedThisFrame;
                case K.Right: return kb.rightArrowKey.wasPressedThisFrame;
                case K.Up: return kb.upArrowKey.wasPressedThisFrame;
                case K.Down: return kb.downArrowKey.wasPressedThisFrame;
                case K.Delete: return kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
                case K.Escape: return kb.escapeKey.wasPressedThisFrame;
                case K.F2: return kb.f2Key.wasPressedThisFrame;
                case K.F3: return kb.f3Key.wasPressedThisFrame;
                case K.F4: return kb.f4Key.wasPressedThisFrame;
                case K.F5: return kb.f5Key.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (k)
            {
                case K.S: return Input.GetKeyDown(KeyCode.S);
                case K.D: return Input.GetKeyDown(KeyCode.D);
                case K.Space: return Input.GetKeyDown(KeyCode.Space);
                case K.Enter: return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                case K.P: return Input.GetKeyDown(KeyCode.P);
                case K.R: return Input.GetKeyDown(KeyCode.R);
                case K.Z: return Input.GetKeyDown(KeyCode.Z);
                case K.Left: return Input.GetKeyDown(KeyCode.LeftArrow);
                case K.Right: return Input.GetKeyDown(KeyCode.RightArrow);
                case K.Up: return Input.GetKeyDown(KeyCode.UpArrow);
                case K.Down: return Input.GetKeyDown(KeyCode.DownArrow);
                case K.Delete: return Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace);
                case K.Escape: return Input.GetKeyDown(KeyCode.Escape);
                case K.F2: return Input.GetKeyDown(KeyCode.F2);
                case K.F3: return Input.GetKeyDown(KeyCode.F3);
                case K.F4: return Input.GetKeyDown(KeyCode.F4);
                case K.F5: return Input.GetKeyDown(KeyCode.F5);
                default: return false;
            }
#else
            return false;
#endif
        }

        // ───────── 아무 키나(KeyCode) 직접 읽기 — 편집기 단축키 보조 ─────────
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
        static readonly Dictionary<KeyCode, Key> codeMap = new Dictionary<KeyCode, Key>();
        /// <summary>KeyCode → Input System Key(이름 규칙이 조금 다름: Alpha1 → Digit1, Keypad1 → Numpad1, Return → Enter …). 없으면 Key.None.</summary>
        static Key ToKey(KeyCode c)
        {
            if (codeMap.TryGetValue(c, out var k)) return k;
            string n = c.ToString();
            if (n.StartsWith("Alpha")) n = "Digit" + n.Substring(5);
            else if (n.StartsWith("Keypad")) n = "Numpad" + n.Substring(6);
            else if (n == "Return") n = "Enter";
            else if (n == "BackQuote") n = "Backquote";
            else if (n == "LeftControl") n = "LeftCtrl";
            else if (n == "RightControl") n = "RightCtrl";
            else if (n == "LeftCommand") n = "LeftMeta";
            else if (n == "RightCommand") n = "RightMeta";
            if (!System.Enum.TryParse(n, out k)) k = Key.None;
            codeMap[c] = k;
            return k;
        }
#endif

        /// <summary>이 키가 이번 프레임에 눌렸는지(한글 입력기 등으로 화면 키 이벤트가 빠져도 실제 키보드에서 읽음).</summary>
        public static bool CodeDown(KeyCode c)
        {
            if (!CanPoll || c == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var k = ToKey(c);
            if (k == Key.None) return false;
            var kc = Keyboard.current[k];
            return kc != null && kc.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(c);
#else
            return false;
#endif
        }

        /// <summary>이 키가 지금 눌려 있는지.</summary>
        public static bool CodeHeld(KeyCode c)
        {
            if (!CanPoll || c == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var k = ToKey(c);
            if (k == Key.None) return false;
            var kc = Keyboard.current[k];
            return kc != null && kc.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(c);
#else
            return false;
#endif
        }

        /// <summary>Alt 가 눌려 있는지.</summary>
        public static bool AltHeld()
        {
            if (!CanPoll) return false;
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            return Keyboard.current.altKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
#else
            return false;
#endif
        }

        public static bool Held(K k)
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case K.Ctrl: return kb.ctrlKey.isPressed || (kb.leftCommandKey != null && kb.leftCommandKey.isPressed);
                case K.Shift: return kb.shiftKey.isPressed;
                case K.S: return kb.sKey.isPressed;
                case K.D: return kb.dKey.isPressed;
                case K.Space: return kb.spaceKey.isPressed;
                case K.Enter: return kb.enterKey.isPressed;
                case K.Left: return kb.leftArrowKey.isPressed;
                case K.Right: return kb.rightArrowKey.isPressed;
                case K.Up: return kb.upArrowKey.isPressed;
                case K.Down: return kb.downArrowKey.isPressed;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (k)
            {
                case K.Ctrl: return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand);
                case K.Shift: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case K.S: return Input.GetKey(KeyCode.S);
                case K.D: return Input.GetKey(KeyCode.D);
                case K.Space: return Input.GetKey(KeyCode.Space);
                case K.Enter: return Input.GetKey(KeyCode.Return);
                case K.Left: return Input.GetKey(KeyCode.LeftArrow);
                case K.Right: return Input.GetKey(KeyCode.RightArrow);
                case K.Up: return Input.GetKey(KeyCode.UpArrow);
                case K.Down: return Input.GetKey(KeyCode.DownArrow);
                default: return false;
            }
#else
            return false;
#endif
        }

        // ───────── 누른 순간(타임스탬프) ─────────
        /// <summary>한 번의 누름: 레인(0=S 1=D 2=스페이스)과 몇 초 전에 눌렸는지.</summary>
        public struct Tap { public int Lane; public double Age; }

        static readonly List<(int lane, double time)> taps = new List<(int, double)>();
        static bool hooked;

        /// <summary>
        /// Input System 의 키 이벤트를 직접 듣는다(한 번만). 프레임 사이에 눌렀다 뗀 짧은 누름도 빠지지 않고,
        /// 프레임이 늦게 와도 ‘실제로 누른 시각’ 으로 판정할 수 있다. 예전 Input Manager 면 아무것도 안 한다(폴링으로 대신).
        /// </summary>
        public static void Hook()
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            if (hooked) return;
            hooked = true;
            InputSystem.onEvent += OnInputEvent;
#endif
        }

#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
        static void OnInputEvent(InputEventPtr ptr, InputDevice dev)
        {
            if (!(dev is Keyboard kb)) return;
            if (!ptr.IsA<StateEvent>() && !ptr.IsA<DeltaStateEvent>()) return;
            TapCheck(kb.sKey, 0, ptr);
            TapCheck(kb.dKey, 1, ptr);
            TapCheck(kb.spaceKey, 2, ptr);
        }

        /// <summary>이 이벤트에서 키가 눌림으로 바뀌었는지(이벤트가 반영되기 전의 상태와 비교).</summary>
        static void TapCheck(KeyControl c, int lane, InputEventPtr ptr)
        {
            if (c == null || !c.ReadValueFromEvent(ptr, out float v)) return;
            if (v >= 0.5f && !c.isPressed)
            {
                taps.Add((lane, ptr.time));
                if (taps.Count > 64) taps.RemoveAt(0);
            }
        }
#endif

        /// <summary>쌓인 누름을 버린다(화면을 열 때 — 예전 누름이 한꺼번에 들어오지 않게).</summary>
        public static void ClearTaps() => taps.Clear();

        /// <summary>
        /// 지난 호출 뒤에 눌린 S · D · 스페이스를 순서대로 꺼낸다. 이벤트를 들을 수 있으면 누른 시각까지(Age 초 전),
        /// 아니면 이번 프레임에 눌린 키(Age 0).
        /// </summary>
        public static void TakeTaps(List<Tap> into)
        {
            into.Clear();
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            if (hooked && Keyboard.current != null)
            {
                double now = InputState.currentTime;
                foreach (var (lane, time) in taps)
                {
                    double age = now - time;
                    if (age > 0.5) continue;              // 너무 오래된 누름(화면이 멈췄던 동안 등)은 버린다
                    into.Add(new Tap { Lane = lane, Age = age < 0 ? 0 : age });
                }
                taps.Clear();
                // 이벤트로 못 잡은 누름이 있으면(드물게 이벤트가 다른 경로로 들어온 경우) 폴링으로 보충
                for (int k = 0; k < 3; k++)
                {
                    bool seen = false;
                    foreach (var t in into) if (t.Lane == k) { seen = true; break; }
                    if (!seen && Down(k == 0 ? K.S : k == 1 ? K.D : K.Space)) into.Add(new Tap { Lane = k, Age = 0 });
                }
                return;
            }
#endif
            for (int k = 0; k < 3; k++) if (Lane(k)) into.Add(new Tap { Lane = k, Age = 0 });
        }

        /// <summary>리듬 레인 키(0=S 1=D 2=스페이스 3=엔터)가 이번 프레임에 눌렸는지.</summary>
        public static bool Lane(int lane) => lane == 0 ? Down(K.S) : lane == 1 ? Down(K.D) : lane == 2 ? Down(K.Space) : lane == 3 && Down(K.Enter);
        public static bool LaneHeld(int lane) => lane == 0 ? Held(K.S) : lane == 1 ? Held(K.D) : lane == 2 ? Held(K.Space) : lane == 3 && Held(K.Enter);
    }
}
