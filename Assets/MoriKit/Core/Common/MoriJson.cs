using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Mori.Core
{
    /// <summary>저장하지 않을 공개 필드에 붙인다.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NoSaveAttribute : Attribute { }

    /// <summary>
    /// 세이브 파일용 JSON(명세서 13장). Unity JsonUtility 가 못 하는 Dictionary·Nullable·다형 인자(Msg)를 다룬다.
    /// 규칙: 공개 인스턴스 필드만 저장(속성·static·const·대리자·인터페이스 필드는 건너뜀).
    /// 숫자는 왕복 정밀도(R/G17), NaN·무한대는 문자열. 모르는 필드는 무시하고 없는 필드는 기본값(앞뒤 버전 호환).
    /// </summary>
    public static class MoriJson
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

        static FieldInfo[] Fields(Type t)
        {
            lock (FieldCache)
            {
                if (FieldCache.TryGetValue(t, out var fs)) return fs;
                var list = new List<FieldInfo>();
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f.IsInitOnly || f.IsLiteral) continue;
                    if (f.GetCustomAttribute<NoSaveAttribute>() != null) continue;
                    var ft = f.FieldType;
                    if (typeof(Delegate).IsAssignableFrom(ft) || (ft.IsInterface && !IsListLike(ft))) continue;
                    list.Add(f);
                }
                fs = list.ToArray();
                FieldCache[t] = fs;
                return fs;
            }
        }

        static bool IsListLike(Type t) => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IList<>) || t.GetGenericTypeDefinition() == typeof(List<>));

        // ───────── 쓰기 ─────────
        public static string Serialize(object o, bool pretty = false)
        {
            var sb = new StringBuilder(1 << 16);
            Write(sb, o, o?.GetType() ?? typeof(object), pretty ? 0 : -1);
            return sb.ToString();
        }

        static void NewLine(StringBuilder sb, int indent)
        {
            if (indent < 0) return;
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        static void Write(StringBuilder sb, object v, Type declared, int indent)
        {
            if (v == null) { sb.Append("null"); return; }
            var t = v.GetType();
            switch (v)
            {
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case double d: WriteDouble(sb, d); return;
                case float f: WriteDouble(sb, f); return;
                case int i: sb.Append(i.ToString(Ci)); return;
                case long l: sb.Append(l.ToString(Ci)); return;
                case uint u: sb.Append(u.ToString(Ci)); return;
                case ulong ul: sb.Append('"').Append(ul.ToString(Ci)).Append('"'); return;   // 53비트를 넘으니 문자열로
                case short sh: sb.Append(sh.ToString(Ci)); return;
                case byte by: sb.Append(by.ToString(Ci)); return;
                case Msg m: WriteMsg(sb, m, indent); return;
            }
            if (t.IsEnum) { WriteString(sb, v.ToString()); return; }
            if (v is IDictionary dict)
            {
                sb.Append('{');
                bool first = true;
                var vt = t.IsGenericType ? t.GetGenericArguments()[1] : typeof(object);
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, indent < 0 ? -1 : indent + 1);
                    WriteString(sb, Convert.ToString(e.Key, Ci));
                    sb.Append(':');
                    Write(sb, e.Value, vt, indent < 0 ? -1 : indent + 1);
                }
                if (!first) NewLine(sb, indent);
                sb.Append('}');
                return;
            }
            if (v is IList list)
            {
                var et = t.IsArray ? t.GetElementType() : t.IsGenericType ? t.GetGenericArguments()[0] : typeof(object);
                bool simple = et.IsPrimitive || et == typeof(string) || et == typeof(double);
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    if (!simple) NewLine(sb, indent < 0 ? -1 : indent + 1);
                    Write(sb, list[i], et, indent < 0 ? -1 : indent + 1);
                }
                if (!simple && list.Count > 0) NewLine(sb, indent);
                sb.Append(']');
                return;
            }
            sb.Append('{');
            bool any = false;
            foreach (var f in Fields(t))
            {
                var fv = f.GetValue(v);
                if (fv == null) continue;
                if (any) sb.Append(',');
                any = true;
                NewLine(sb, indent < 0 ? -1 : indent + 1);
                WriteString(sb, f.Name);
                sb.Append(':');
                Write(sb, fv, f.FieldType, indent < 0 ? -1 : indent + 1);
            }
            if (any) NewLine(sb, indent);
            sb.Append('}');
        }

        static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d)) { sb.Append("\"NaN\""); return; }
            if (double.IsPositiveInfinity(d)) { sb.Append("\"Infinity\""); return; }
            if (double.IsNegativeInfinity(d)) { sb.Append("\"-Infinity\""); return; }
            string s = d.ToString("R", Ci);
            if (double.Parse(s, Ci) != d) s = d.ToString("G17", Ci);
            sb.Append(s);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", Ci));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        /// <summary>Msg = {"k": 키, "a": [ {"s":문자열} | {"i":정수} | {"d":실수} | {"n":값,"p":자릿수,"g":구분,"m":화폐} | {"m":Msg} ]}</summary>
        static void WriteMsg(StringBuilder sb, Msg m, int indent)
        {
            sb.Append("{\"k\":");
            WriteString(sb, m.Key ?? "");
            if (m.Args != null && m.Args.Length > 0)
            {
                sb.Append(",\"a\":[");
                for (int i = 0; i < m.Args.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    var a = m.Args[i];
                    switch (a)
                    {
                        case null: sb.Append("null"); break;
                        case Msg mm: sb.Append("{\"msg\":"); WriteMsg(sb, mm, indent); sb.Append('}'); break;
                        case Num n:
                            sb.Append("{\"n\":"); WriteDouble(sb, n.Value);
                            sb.Append(",\"p\":").Append(n.Decimals.ToString(Ci));
                            if (n.Grouping) sb.Append(",\"g\":true");
                            if (n.Money) sb.Append(",\"$\":true");
                            sb.Append('}');
                            break;
                        case int ii: sb.Append("{\"i\":").Append(ii.ToString(Ci)).Append('}'); break;
                        case long ll: sb.Append("{\"i\":").Append(ll.ToString(Ci)).Append('}'); break;
                        case double dd: sb.Append("{\"d\":"); WriteDouble(sb, dd); sb.Append('}'); break;
                        case float ff: sb.Append("{\"d\":"); WriteDouble(sb, ff); sb.Append('}'); break;
                        default: sb.Append("{\"s\":"); WriteString(sb, Convert.ToString(a, Ci)); sb.Append('}'); break;
                    }
                }
                sb.Append(']');
            }
            sb.Append('}');
        }

        // ───────── 읽기: 먼저 나무(Dictionary/List/Raw)로 파싱 ─────────
        /// <summary>숫자·문자열 원문(대상 타입에 맞춰 나중에 변환).</summary>
        sealed class Raw
        {
            public string Text;
            public bool Quoted;
        }

        sealed class Parser
        {
            readonly string s;
            int i;
            public Parser(string s) { this.s = s; }

            Exception Err(string what) => new FormatException($"JSON {what} (위치 {i})");

            void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            public object ParseRoot()
            {
                var v = Value();
                Ws();
                if (i != s.Length) throw Err("뒤에 남은 글자");
                return v;
            }

            object Value()
            {
                Ws();
                if (i >= s.Length) throw Err("끝남");
                char c = s[i];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return new Raw { Text = Str(), Quoted = true };
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return new Raw { Text = "true" }; }
                if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return new Raw { Text = "false" }; }
                int st = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (st == i) throw Err("알 수 없는 값");
                return new Raw { Text = s.Substring(st, i - st) };
            }

            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                i++;
                Ws();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws();
                    if (i >= s.Length || s[i] != '"') throw Err("키가 필요");
                    string k = Str();
                    Ws();
                    if (i >= s.Length || s[i] != ':') throw Err("':' 가 필요");
                    i++;
                    d[k] = Value();
                    Ws();
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    throw Err("',' 또는 '}' 가 필요");
                }
            }

            List<object> Arr()
            {
                var l = new List<object>();
                i++;
                Ws();
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value());
                    Ws();
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    throw Err("',' 또는 ']' 가 필요");
                }
            }

            string Str()
            {
                var sb = new StringBuilder();
                i++;
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Err("\\u 가 짧음");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, Ci));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                throw Err("문자열이 끝나지 않음");
            }
        }

        public static T Deserialize<T>(string json) => (T)Deserialize(json, typeof(T));

        public static object Deserialize(string json, Type t) => FromNode(new Parser(json).ParseRoot(), t);

        static object FromNode(object node, Type t)
        {
            if (node == null) return t.IsValueType && Nullable.GetUnderlyingType(t) == null ? Activator.CreateInstance(t) : null;
            var nt = Nullable.GetUnderlyingType(t);
            if (nt != null) t = nt;
            if (node is Raw r) return Scalar(r, t);
            if (t == typeof(Msg)) return ReadMsg(node as Dictionary<string, object>);
            if (node is List<object> arr)
            {
                if (t.IsArray)
                {
                    var et = t.GetElementType();
                    var a = Array.CreateInstance(et, arr.Count);
                    for (int k = 0; k < arr.Count; k++) a.SetValue(FromNode(arr[k], et), k);
                    return a;
                }
                var lt = t.IsInterface && t.IsGenericType ? typeof(List<>).MakeGenericType(t.GetGenericArguments()[0]) : t;
                var list = (IList)Activator.CreateInstance(lt);
                var eltT = lt.IsGenericType ? lt.GetGenericArguments()[0] : typeof(object);
                foreach (var x in arr) list.Add(FromNode(x, eltT));
                return list;
            }
            if (node is Dictionary<string, object> obj)
            {
                if (typeof(IDictionary).IsAssignableFrom(t))
                {
                    var d = (IDictionary)Activator.CreateInstance(t);
                    var ga = t.GetGenericArguments();
                    foreach (var kv in obj) d[ScalarText(kv.Key, ga[0])] = FromNode(kv.Value, ga[1]);
                    return d;
                }
                object o = Activator.CreateInstance(t);
                foreach (var f in Fields(t))
                    if (obj.TryGetValue(f.Name, out var fv)) f.SetValue(o, FromNode(fv, f.FieldType));
                return o;
            }
            throw new FormatException("JSON 형식이 " + t.Name + " 와 맞지 않음");
        }

        static object ScalarText(string text, Type t) => Scalar(new Raw { Text = text, Quoted = true }, t);

        static object Scalar(Raw r, Type t)
        {
            string x = r.Text;
            if (t == typeof(string)) return x;
            if (t == typeof(bool)) return x == "true";
            if (t == typeof(double) || t == typeof(float))
            {
                double d;
                if (x == "NaN") d = double.NaN;
                else if (x == "Infinity") d = double.PositiveInfinity;
                else if (x == "-Infinity") d = double.NegativeInfinity;
                else d = double.Parse(x, NumberStyles.Float, Ci);
                return t == typeof(float) ? (object)(float)d : d;
            }
            if (t == typeof(int)) return int.Parse(x, NumberStyles.Integer, Ci);
            if (t == typeof(long)) return long.Parse(x, NumberStyles.Integer, Ci);
            if (t == typeof(uint)) return uint.Parse(x, NumberStyles.Integer, Ci);
            if (t == typeof(ulong)) return ulong.Parse(x, NumberStyles.Integer, Ci);
            if (t == typeof(short)) return short.Parse(x, NumberStyles.Integer, Ci);
            if (t == typeof(byte)) return byte.Parse(x, NumberStyles.Integer, Ci);
            if (t.IsEnum) return Enum.Parse(t, x);
            if (t == typeof(object)) return x;
            throw new FormatException("값 " + x + " 를 " + t.Name + " 로 바꿀 수 없음");
        }

        static Msg ReadMsg(Dictionary<string, object> o)
        {
            if (o == null) return null;
            string key = o.TryGetValue("k", out var k) && k is Raw kr ? kr.Text : "";
            var args = new List<object>();
            if (o.TryGetValue("a", out var a) && a is List<object> al)
            {
                foreach (var x in al)
                {
                    if (!(x is Dictionary<string, object> e)) { args.Add(null); continue; }
                    if (e.TryGetValue("msg", out var mm)) args.Add(ReadMsg(mm as Dictionary<string, object>));
                    else if (e.TryGetValue("n", out var n))
                    {
                        double v = (double)Scalar((Raw)n, typeof(double));
                        int p = e.TryGetValue("p", out var pp) ? (int)Scalar((Raw)pp, typeof(int)) : 0;
                        bool g = e.TryGetValue("g", out var gg) && ((Raw)gg).Text == "true";
                        bool money = e.TryGetValue("$", out var mo) && ((Raw)mo).Text == "true";
                        args.Add(new Num(v, p, g, money));
                    }
                    else if (e.TryGetValue("i", out var iv))
                    {
                        long l = (long)Scalar((Raw)iv, typeof(long));
                        args.Add(l >= int.MinValue && l <= int.MaxValue ? (object)(int)l : l);
                    }
                    else if (e.TryGetValue("d", out var dv)) args.Add((double)Scalar((Raw)dv, typeof(double)));
                    else if (e.TryGetValue("s", out var sv)) args.Add(sv is Raw sr ? sr.Text : null);
                    else args.Add(null);
                }
            }
            return new Msg(key, args.ToArray());
        }
    }
}
