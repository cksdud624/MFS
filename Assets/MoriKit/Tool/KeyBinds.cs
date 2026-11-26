using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mori.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
using UnityEngine.InputSystem;
#endif

namespace Mori.Client
{
    /// <summary>
    /// 결투 조작 키(플레이어가 바꾸고 저장): 슬롯 8개 = 공격 · 카운터 · 대쉬 · 점프 · 왼쪽 · 오른쪽 · 위 · 아래. 슬롯마다 키 여러 개.
    /// 키 이름은 Input System 의 Key 이름(LeftCtrl, Z, Space …)으로 저장하고, 옛 입력 방식에서는 KeyCode 로 바꿔 쓴다.
    /// 바꾸면 바로 persistentDataPath/keybinds.json 에 저장된다. 튜닝 ‘내보내기’ 에도 함께 담긴다.
    /// </summary>
    public static class KeyBinds
    {
        public const int ATK = 0, CTR = 1, DASH = 2, JUMP = 3, LEFT = 4, RIGHT = 5, UP = 6, DOWN = 7, ULT = 8, COUNT = 9;
        public static readonly string[] ID = { "attack", "counter", "dash", "jump", "left", "right", "up", "down", "ult" };
        public static readonly string[] NAME = { "공격", "카운터", "대쉬", "점프", "왼쪽", "오른쪽", "위", "아래", "궁극기" };
        public static readonly string[][] DEFAULT =
        {
            new[] { "LeftCtrl", "RightCtrl" },
            new[] { "Z" },
            new[] { "Space", "LeftShift", "RightShift" },
            new[] { "LeftAlt", "RightAlt" },
            new[] { "LeftArrow" },
            new[] { "RightArrow" },
            new[] { "UpArrow" },
            new[] { "DownArrow" },
            new[] { "C" },
        };

        static List<string>[] cur;
        /// <summary>바뀔 때마다 오른다(입력 · 안내 글자가 다시 읽게).</summary>
        public static int Version { get; private set; }
        public static string SavePath => Path.Combine(KitPaths.Data, "keybinds.json");

        public sealed class Slot { public string Name; public List<string> Keys = new List<string>(); }
        public sealed class File1 { public int V = 1; public List<Slot> Slots = new List<Slot>(); }

        static List<string>[] Cur { get { if (cur == null) Load(); return cur; } }

        public static IReadOnlyList<string> Get(int slot) => Cur[slot];

        static void Defaults() { cur = DEFAULT.Select(a => a.ToList()).ToArray(); }

        public static void Load()
        {
            Defaults();
            try
            {
                string sp = Bundled.Pick(SavePath, "keybinds.json");        // 내 것 → 없으면 빌드에 담긴 것
                if (!File.Exists(sp)) return;
                var f = MoriJson.Deserialize<File1>(File.ReadAllText(sp));
                if (f?.Slots == null) return;
                foreach (var s in f.Slots)
                {
                    int i = Array.IndexOf(ID, s?.Name);
                    if (i >= 0 && s.Keys != null) cur[i] = s.Keys.Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
                }
            }
            catch (Exception e) { Debug.LogWarning("[Mori] 조작 키를 읽지 못해 기본 키를 씁니다: " + e.Message); Defaults(); }
            Version++;
        }

        public static bool Save(out string err)
        {
            err = null;
            try
            {
                var f = new File1();
                for (int i = 0; i < COUNT; i++) f.Slots.Add(new Slot { Name = ID[i], Keys = Cur[i].ToList() });
                KitBackup.Before(SavePath);
                File.WriteAllText(SavePath, MoriJson.Serialize(f, true));
                return true;
            }
            catch (Exception e) { err = e.Message; return false; }
        }

        /// <summary>키를 슬롯에 넣는다(replace = 그 슬롯의 다른 키를 지우고 이것만). 다른 슬롯에 있던 같은 키는 옮겨 온다. 옮겨 온 슬롯 이름(없으면 null).</summary>
        public static string Assign(int slot, string key, bool replace)
        {
            string moved = null;
            for (int i = 0; i < COUNT; i++)
                if (i != slot && Cur[i].Remove(key)) moved = NAME[i];
            if (replace) Cur[slot].Clear();
            if (!Cur[slot].Contains(key)) Cur[slot].Add(key);
            Version++;
            Save(out _);
            return moved;
        }

        public static void Reset(int slot = -1)
        {
            if (slot < 0) Defaults(); else cur[slot] = DEFAULT[slot].ToList();
            if (slot >= 0)
                foreach (var k in DEFAULT[slot]) for (int i = 0; i < COUNT; i++) if (i != slot) Cur[i].Remove(k);
            Version++;
            Save(out _);
        }

        public static bool Changed(int slot) => !Cur[slot].SequenceEqual(DEFAULT[slot]);

        /// <summary>보기 좋은 키 이름(어두운 칩 위 흰 글자로 쓰이니 짧게).</summary>
        public static string Pretty(string k)
        {
            switch (k)
            {
                case "LeftCtrl": return "L-CTRL";
                case "RightCtrl": return "R-CTRL";
                case "LeftShift": return "L-SHIFT";
                case "RightShift": return "R-SHIFT";
                case "LeftAlt": return "L-ALT";
                case "RightAlt": return "R-ALT";
                case "LeftArrow": return "←";
                case "RightArrow": return "→";
                case "UpArrow": return "↑";
                case "DownArrow": return "↓";
                case "Space": return "SPACE";
                case "Enter": return "ENTER";
                case "Backspace": return "BKSP";
                case "Tab": return "TAB";
                default:
                    if (k.StartsWith("Digit")) return k.Substring(5);
                    if (k.StartsWith("Numpad")) return "NUM" + k.Substring(6);
                    return k.ToUpperInvariant();
            }
        }

        /// <summary>슬롯 키 안내(같은 키의 왼쪽 · 오른쪽은 하나로: L-CTRL · R-CTRL → CTRL).</summary>
        public static string Label(int slot)
        {
            var names = new List<string>();
            foreach (var k in Cur[slot])
            {
                string p = Pretty(k);
                if (p.StartsWith("L-") || p.StartsWith("R-")) p = p.Substring(2);
                if (!names.Contains(p)) names.Add(p);
            }
            return names.Count == 0 ? "(없음)" : string.Join(" · ", names);
        }

        public static string LabelDirs() => $"{Label(LEFT)} {Label(RIGHT)} {Label(UP)} {Label(DOWN)}";

        /// <summary>내보내기용 JSON 조각: "keys": { "attack": ["LeftCtrl", ...], ... }</summary>
        public static string ExportJson(string indent)
        {
            var sb = new StringBuilder();
            sb.Append(indent).Append("\"keys\": {");
            for (int i = 0; i < COUNT; i++)
            {
                sb.Append(i == 0 ? "\n" : ",\n").Append(indent).Append("  \"").Append(ID[i]).Append("\": [");
                sb.Append(string.Join(", ", Cur[i].Select(k => "\"" + k.Replace("\"", "") + "\"")));
                sb.Append("]");
            }
            sb.Append("\n").Append(indent).Append("}");
            return sb.ToString();
        }

#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
        static Key[][] keys;
        static int keysVer = -1;

        /// <summary>슬롯마다 Input System 키(이름을 못 읽는 키는 뺀다).</summary>
        public static Key[][] InputKeys
        {
            get
            {
                if (keys == null || keysVer != Version)
                {
                    keys = new Key[COUNT][];
                    for (int i = 0; i < COUNT; i++)
                        keys[i] = Cur[i].Select(n => Enum.TryParse(n, out Key k) && k != Key.None ? k : Key.None).Where(k => k != Key.None).ToArray();
                    keysVer = Version;
                }
                return keys;
            }
        }
#endif
        static KeyCode[][] codes;
        static int codesVer = -1;

        /// <summary>옛 입력 방식용 KeyCode(이름 차이를 맞춘다: LeftCtrl → LeftControl, Digit1 → Alpha1 …).</summary>
        public static KeyCode[][] Codes
        {
            get
            {
                if (codes == null || codesVer != Version)
                {
                    codes = new KeyCode[COUNT][];
                    for (int i = 0; i < COUNT; i++) codes[i] = Cur[i].Select(ToCode).Where(c => c != KeyCode.None).ToArray();
                    codesVer = Version;
                }
                return codes;
            }
        }

        public static KeyCode ToCode(string n)
        {
            if (string.IsNullOrEmpty(n)) return KeyCode.None;
            string m = n == "LeftCtrl" ? "LeftControl" : n == "RightCtrl" ? "RightControl" : n == "Enter" ? "Return" : n == "Backquote" ? "BackQuote"
                : n.StartsWith("Digit") ? "Alpha" + n.Substring(5) : n.StartsWith("Numpad") ? "Keypad" + n.Substring(6) : n;
            return Enum.TryParse(m, out KeyCode c) ? c : KeyCode.None;
        }

        /// <summary>이번 프레임에 새로 눌린 키 이름(키 바꾸기 기다릴 때). Esc 는 "Escape".</summary>
        public static string PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM && MORI_INPUTSYS
            var kb = Keyboard.current;
            if (kb != null)
            {
                foreach (var c in kb.allKeys)
                    if (c != null && c.wasPressedThisFrame && c.keyCode != Key.None) return c.keyCode.ToString();
                return null;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            foreach (KeyCode c in Enum.GetValues(typeof(KeyCode)))
            {
                if (c >= KeyCode.Mouse0) break;
                if (Input.GetKeyDown(c))
                {
                    string s = c.ToString();
                    return s == "LeftControl" ? "LeftCtrl" : s == "RightControl" ? "RightCtrl" : s == "Return" ? "Enter" : s.StartsWith("Alpha") ? "Digit" + s.Substring(5) : s.StartsWith("Keypad") ? "Numpad" + s.Substring(6) : s;
                }
            }
#endif
            return null;
        }
    }
}
