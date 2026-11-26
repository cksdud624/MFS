using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Mori.Core
{
    /// <summary>
    /// 번역 전 문구. 한국어 문장이 키(gettext 방식)이고 {0}, {1} 자리에 Args 가 들어간다.
    /// 규칙 코드는 번역하지 않고 Msg 만 돌려준다. 화면에서 번역한다(명세서 2.4).
    /// Args 에는 string, int/long, Num(형식이 정해진 숫자), 또는 다른 Msg(함께 번역)가 들어간다.
    /// </summary>
    public sealed class Msg
    {
        public string Key;
        public object[] Args;

        public Msg() { Key = ""; Args = Array.Empty<object>(); }
        public Msg(string key, params object[] args)
        {
            Key = key ?? "";
            Args = args ?? Array.Empty<object>();
        }

        public static Msg Of(string key, params object[] args) => new Msg(key, args);
        public static readonly Msg Empty = new Msg("");
        public bool IsEmpty => string.IsNullOrEmpty(Key);

        /// <summary>번역 없이 키 그대로 채운 문자열(테스트·로그용). translate 를 주면 키와 Msg 인자를 번역한다.</summary>
        public string Format(Func<string, string> translate = null, CultureInfo culture = null)
        {
            string tpl = translate != null ? translate(Key) : Key;
            if (Args.Length == 0) return tpl;
            var sb = new StringBuilder(tpl.Length + 16);
            for (int i = 0; i < tpl.Length; i++)
            {
                char c = tpl[i];
                if (c == '{')
                {
                    int close = tpl.IndexOf('}', i + 1);
                    if (close > i + 1 && int.TryParse(tpl.Substring(i + 1, close - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int idx) && idx < Args.Length && Args[idx] != null)
                    {
                        sb.Append(ArgText(Args[idx], translate, culture));
                        i = close;
                        continue;
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string ArgText(object a, Func<string, string> translate, CultureInfo culture)
        {
            switch (a)
            {
                case Msg m: return m.Format(translate, culture);
                case Num n: return n.Format(culture);
                case double d: return d.ToString(culture ?? CultureInfo.InvariantCulture);
                case IFormattable f: return f.ToString(null, culture ?? CultureInfo.InvariantCulture);
                default: return a.ToString();
            }
        }

        public override string ToString() => Format();
    }

    /// <summary>
    /// 문구 안의 숫자. JS 는 toFixed(2)·toLocaleString 으로 미리 문자열을 만들었지만,
    /// 게임은 언어별 숫자 형식(러시아어 소수점 쉼표 등)을 써야 하므로 값과 형식만 넘긴다.
    /// </summary>
    public struct Num
    {
        public double Value;
        public int Decimals;   // 소수 자릿수(고정)
        public bool Grouping;  // 천 단위 구분
        public bool Money;     // 화폐($) 표시 대상

        public Num(double value, int decimals, bool grouping, bool money = false)
        {
            Value = value; Decimals = decimals; Grouping = grouping; Money = money;
        }

        /// <summary>toFixed(d) 와 같은 표시.</summary>
        public static Num Fixed(double v, int d) => new Num(v, d, false);
        /// <summary>toLocaleString('en-US') 와 같은 표시(천 단위 구분, 소수는 최대 2자리로 둔다).</summary>
        public static Num Grouped(double v, int d = 0) => new Num(v, d, true);
        /// <summary>금액. 화면에서 $ 와 언어별 형식으로 바꾼다.</summary>
        public static Num Usd(double v, int d = 2) => new Num(v, d, true, true);

        public string Format(CultureInfo culture = null)
        {
            var ci = culture ?? CultureInfo.InvariantCulture;
            string fmt = (Grouping ? "N" : "F") + Decimals.ToString(CultureInfo.InvariantCulture);
            return Value.ToString(fmt, ci);
        }

        public override string ToString() => Format();
    }

    /// <summary>규칙 함수의 결과: 성공이면 Ok, 실패면 Err(번역 전 문구). JS 의 { ok } / { err }.</summary>
    public class Result
    {
        public bool Ok;
        public Msg Err;
        public static Result Success() => new Result { Ok = true };
        public static Result Fail(Msg err) => new Result { Ok = false, Err = err };
    }

    public sealed class Result<T> : Result
    {
        public T Value;
        public static Result<T> Success(T v) => new Result<T> { Ok = true, Value = v };
        public static new Result<T> Fail(Msg err) => new Result<T> { Ok = false, Err = err };
    }
}
