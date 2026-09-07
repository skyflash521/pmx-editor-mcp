using System;
using System.Collections.Generic;
using System.Globalization;

namespace PmxEditorMcp
{
    /// <summary>
    /// 要求の引数から、決まった形の値を取り出す。形が合わなければ不正な引数として断る。検査から
    /// だけ使う入口が同じ形を何度も受け取るので、判定をここ1つに置く。
    /// </summary>
    public static class RequestParameter
    {
        /// <summary>空でない文字列を取り出す。空白だけのものも空として断る。</summary>
        public static string Text(IDictionary<string, object> parameters, string name)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            object value;
            string text = parameters.TryGetValue(name, out value) ? value as string : null;
            if (text == null || text.Trim().Length == 0)
            {
                throw new InvalidParamsException(name + " は空でない文字列でなければならない。");
            }

            return text;
        }

        /// <summary>1以上の整数を取り出す。小数・真偽・文字列・範囲外はいずれも断る。</summary>
        public static int PositiveInteger(IDictionary<string, object> parameters, string name)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            object value;
            if (!parameters.TryGetValue(name, out value) || !ValueInput.IsNumber(value))
            {
                throw new InvalidParamsException(name + " は正の整数でなければならない。");
            }

            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (number != Math.Floor(number) || number < 1 || number > int.MaxValue)
            {
                throw new InvalidParamsException(name + " は正の整数でなければならない。");
            }

            return (int)number;
        }
    }
}
