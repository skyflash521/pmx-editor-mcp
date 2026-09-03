using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 応答サイズ予算の分け方。予算はホストが返す本文の文字数で、その内訳はツールの値と警告の
    /// 2つである。ホストは値と警告をそれぞれの枠に収めて返す。
    /// </summary>
    public static class ResponseSize
    {
        /// <summary>警告に充てる枠。切り詰めた旨の注記もこの中に収める。</summary>
        public const int WarningChars = 2000;

        /// <summary>切り詰めたときに末尾へ置く注記。</summary>
        public const string TruncatedNotice = "(以降の警告は切り詰めた)";

        /// <summary>警告1件が本文で使う、警告そのもの以外の文字数(行の区切りと接頭辞)。</summary>
        public const int LineOverheadChars = 5;

        /// <summary>ツールの値に充てる枠。予算から警告の枠を引いたもの。</summary>
        public static int ValueChars(int budgetChars)
        {
            if (budgetChars < ResponseBudget.MinimumChars)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(budgetChars),
                    budgetChars,
                    ResponseBudget.MinimumChars + " 以上でなければならない。");
            }

            return budgetChars - WarningChars;
        }

        /// <summary>
        /// 警告を枠に収める。連結した長さが枠を超えるときは、切り詰めた旨の注記を末尾へ置いた形が
        /// 枠に収まるところまで前から採る。
        /// </summary>
        public static IList<string> Fit(IEnumerable<string> warnings)
        {
            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            string[] listed = warnings.ToArray();
            if (Length(listed) <= WarningChars)
            {
                return new ReadOnlyCollection<string>(listed);
            }

            List<string> fitted = new List<string>();
            foreach (string warning in listed)
            {
                fitted.Add(warning);
                if (Length(fitted.Concat(new[] { TruncatedNotice })) > WarningChars)
                {
                    fitted.RemoveAt(fitted.Count - 1);
                    break;
                }
            }

            fitted.Add(TruncatedNotice);

            return new ReadOnlyCollection<string>(fitted);
        }

        /// <summary>
        /// 警告が本文で使う文字数。1件ごとに行の区切りと接頭辞を伴うので、それも数える。
        /// </summary>
        public static int Length(IEnumerable<string> warnings)
        {
            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            int length = 0;
            foreach (string warning in warnings)
            {
                if (warning == null)
                {
                    throw new ArgumentException("無い警告は数えられない。", nameof(warnings));
                }

                length += warning.Length + LineOverheadChars;
            }

            return length;
        }
    }
}
