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
    /// 액션 키(기본 Ctrl 공격 · Z 카운터 · Space/Shift 대쉬 · Alt 점프, 방향키 ← → ↑ ↓) — 실제 키는 KeyBinds(플레이어가 바꾸고 저장).
    /// 동작 키는 키 이벤트의 ‘실제로 누른 시각’ 까지 받는다(프레임이 늦어도 판정이 밀리지 않게). 방향키는 누르고 있는지만 본다.
    /// </summary>
    public static class ActInput
    {
        public struct Press { public int Act; public double Age; }

        static readonly List<(int act, double time)> q = new List<(int, double)>();
        static bool hooked;

#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
        static Key[][] BIND => KeyBinds.InputKeys;          // 동작 4개(공격 · 카운터 · 대쉬 · 점프) + 방향 4개 — 플레이어가 바꿀 수 있다
#endif
        static KeyCode[][] LEGACY => KeyBinds.Codes;
        const int ACTS = 5;
        /// <summary>동작 번호(0 공격 · 1 카운터 · 2 대쉬 · 3 점프 · 4 궁극기) → 키 슬롯.</summary>
        static readonly int[] SLOT = { KeyBinds.ATK, KeyBinds.CTR, KeyBinds.DASH, KeyBinds.JUMP, KeyBinds.ULT };

        public static void Hook()
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            if (hooked) return;
            hooked = true;
            InputSystem.onEvent += OnEvent;
#endif
        }

#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
        static void OnEvent(InputEventPtr ptr, InputDevice dev)
        {
            if (!(dev is Keyboard kb)) return;
            if (!ptr.IsA<StateEvent>() && !ptr.IsA<DeltaStateEvent>()) return;
            for (int a = 0; a < ACTS; a++)
                foreach (var key in BIND[SLOT[a]])
                {
                    var c = kb[key];
                    if (c == null || !c.ReadValueFromEvent(ptr, out float v)) continue;
                    if (v >= 0.5f && !c.isPressed) { q.Add((a, ptr.time)); if (q.Count > 64) q.RemoveAt(0); }
                }
        }
#endif

        public static void Clear() => q.Clear();

        /// <summary>지난 호출 뒤에 눌린 동작 키(순서대로, 몇 초 전에 눌렸는지까지).</summary>
        public static void Take(List<Press> into)
        {
            into.Clear();
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var kb = Keyboard.current;
            if (hooked && kb != null)
            {
                double now = InputState.currentTime;
                foreach (var (a, time) in q)
                {
                    double age = now - time;
                    if (age > 0.5) continue;
                    into.Add(new Press { Act = a, Age = age < 0 ? 0 : age });
                }
                q.Clear();
                for (int a = 0; a < ACTS; a++)
                {
                    bool seen = false;
                    foreach (var p in into) if (p.Act == a) { seen = true; break; }
                    if (seen) continue;
                    foreach (var key in BIND[SLOT[a]]) if (kb[key].wasPressedThisFrame) { into.Add(new Press { Act = a, Age = 0 }); break; }
                }
                return;
            }
            if (kb != null)
            {
                for (int a = 0; a < ACTS; a++)
                    foreach (var key in BIND[SLOT[a]]) if (kb[key].wasPressedThisFrame) { into.Add(new Press { Act = a, Age = 0 }); break; }
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int a = 0; a < ACTS; a++)
                foreach (var k in LEGACY[SLOT[a]]) if (Input.GetKeyDown(k)) { into.Add(new Press { Act = a, Age = 0 }); break; }
#endif
        }

        public static bool Left => Held(0);
        public static bool Right => Held(1);
        public static bool Up => Held(2);
        public static bool Down => Held(3);
        public static bool LeftDown => Pressed(0);
        public static bool RightDown => Pressed(1);

        static bool Held(int d)
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var kb = Keyboard.current;
            if (kb != null)
            {
                foreach (var k in BIND[KeyBinds.LEFT + d]) if (kb[k].isPressed) return true;
                return false;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            foreach (var k in LEGACY[KeyBinds.LEFT + d]) if (Input.GetKey(k)) return true;
#endif
            return false;
        }

        static bool Pressed(int d)
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var kb = Keyboard.current;
            if (kb != null)
            {
                foreach (var k in BIND[KeyBinds.LEFT + d]) if (kb[k].wasPressedThisFrame) return true;
                return false;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            foreach (var k in LEGACY[KeyBinds.LEFT + d]) if (Input.GetKeyDown(k)) return true;
#endif
            return false;
        }
    }
}
