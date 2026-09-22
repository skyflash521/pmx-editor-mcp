// 操作ごとに要る入力を読む。どの操作で要るかはツールが名前で渡す。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp
{
    public static class ComposedInput
    {
        /// <summary>上限を置かないときに渡す値。</summary>
        public const float NoCeiling = float.MaxValue;

        /// <summary>下限を置かないときに渡す値。</summary>
        public const float NoFloor = float.MinValue;

        /// <summary>
        /// 要る操作のときだけ受け取る、有限の数を読む。要る操作で欠けていれば偽、要らない操作で
        /// 渡されていれば偽を返し、断る内容を渡す。要らない操作では0を渡す。
        /// </summary>
        public static bool TryFloat(
            McpMethodContext context,
            string name,
            string operation,
            IList<string> wanted,
            float least,
            float most,
            out float number,
            out string code,
            out string message)
        {
            number = 0f;
            object given;
            bool asked;
            if (!TryWanted(context, name, operation, wanted, out given, out asked, out code, out message))
            {
                return false;
            }

            if (!asked)
            {
                return true;
            }

            if (!ValueInput.TrySingle(given, out number))
            {
                number = 0f;

                return Refuse(
                    name + " は " + Listed(wanted) + " のときに渡す、有限の数である。",
                    out code,
                    out message);
            }

            if (number >= least && number <= most)
            {
                return true;
            }

            number = 0f;

            return Refuse(
                most == NoCeiling
                    ? name + " は " + Spelled(least) + " 以上でなければならない。"
                    : name + " は " + Spelled(least) + " 以上 " + Spelled(most)
                        + " 以下でなければならない。",
                out code,
                out message);
        }

        /// <summary>
        /// 要る操作のときだけ受け取る、下限を持つ整数を読む。要る操作で欠けていれば偽、要らない
        /// 操作で渡されていれば偽を返し、断る内容を渡す。要らない操作では0を渡す。
        /// </summary>
        public static bool TryCount(
            McpMethodContext context,
            string name,
            string operation,
            IList<string> wanted,
            int least,
            out int number,
            out string code,
            out string message)
        {
            number = 0;
            object given;
            bool asked;
            if (!TryWanted(context, name, operation, wanted, out given, out asked, out code, out message))
            {
                return false;
            }

            if (!asked)
            {
                return true;
            }

            if (!ValueInput.TryIndex(given, out number) || number < least)
            {
                number = 0;

                return Refuse(
                    name + " は " + Listed(wanted) + " のときに渡す、"
                        + Spelled(least) + " 以上の整数である。",
                    out code,
                    out message);
            }

            return true;
        }

        /// <summary>
        /// 要る操作のときだけ受け取る、決まった値のどれかを読む。要る操作で欠けていれば偽、
        /// 要らない操作で渡されていれば偽を返し、断る内容を渡す。要らない操作では空を渡す。
        /// </summary>
        public static bool TryChoice(
            McpMethodContext context,
            string name,
            string operation,
            IList<string> wanted,
            IList<string> choices,
            out string value,
            out string code,
            out string message)
        {
            value = null;
            object given;
            bool asked;
            if (!TryWanted(context, name, operation, wanted, out given, out asked, out code, out message))
            {
                return false;
            }

            if (!asked)
            {
                return true;
            }

            value = given as string;
            if (value != null && choices.Contains(value, StringComparer.Ordinal))
            {
                return true;
            }

            value = null;

            return Refuse(
                name + " は次のどれかでなければならない: " + Listed(choices),
                out code,
                out message);
        }

        /// <summary>
        /// 要る操作のときだけ受け取る、空でない文字を読む。要る操作で欠けていれば偽、要らない操作で
        /// 渡されていれば偽を返し、断る内容を渡す。要らない操作では空を渡す。
        /// </summary>
        public static bool TryText(
            McpMethodContext context,
            string name,
            string operation,
            IList<string> wanted,
            out string value,
            out string code,
            out string message)
        {
            value = null;
            object given;
            bool asked;
            if (!TryWanted(context, name, operation, wanted, out given, out asked, out code, out message))
            {
                return false;
            }

            if (!asked)
            {
                return true;
            }

            value = given as string;
            if (!string.IsNullOrEmpty(value))
            {
                return true;
            }

            value = null;

            return Refuse(
                name + " は " + Listed(wanted) + " のときに渡す、空でない文字である。",
                out code,
                out message);
        }

        /// <summary>
        /// 操作によらず必ず受け取る、決まった値のどれかを読む。欠けていても知らない値でも偽を返し、
        /// 断る内容を渡す。
        /// </summary>
        public static bool TryChoice(
            McpMethodContext context,
            string name,
            IList<string> choices,
            out string value,
            out string code,
            out string message)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            object given;
            context.Params.TryGetValue(name, out given);
            value = given as string;
            if (value != null && choices.Contains(value, StringComparer.Ordinal))
            {
                code = null;
                message = null;

                return true;
            }

            value = null;

            return Refuse(
                name + " は次のどれかでなければならない: " + Listed(choices),
                out code,
                out message);
        }

        /// <summary>
        /// 操作によらず必ず受け取る、決まった値の並びを読む。空の並び・知らない値・並びでない値は
        /// いずれも偽を返し、断る内容を渡す。同じ値が重なっていても1つとして数える。
        /// </summary>
        public static bool TryChoices(
            McpMethodContext context,
            string name,
            IList<string> choices,
            out IList<string> values,
            out string code,
            out string message)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            values = null;
            object given;
            context.Params.TryGetValue(name, out given);
            object[] items = given as object[];
            if (items == null || items.Length == 0
                || items.Any(item => !choices.Contains(item as string, StringComparer.Ordinal)))
            {
                return Refuse(
                    name + " は次のどれかを1つ以上並べたものでなければならない: " + Listed(choices),
                    out code,
                    out message);
            }

            code = null;
            message = null;
            values = items.Select(item => (string)item).Distinct(StringComparer.Ordinal).ToList();

            return true;
        }

        /// <summary>
        /// 要る操作のときだけ受け取る、位置の並びを読む。要る操作で欠けていれば偽、要らない操作で
        /// 渡されていれば偽を返し、断る内容を渡す。<paramref name="ceiling"/> 以上の位置と負の位置も
        /// 断る。要らない操作と、<paramref name="omittable"/> が挙げる操作で欠けているときは、空の
        /// 並びを渡す。
        /// </summary>
        /// <param name="selectedName">
        /// 位置の並びの代わりに画面の選択で指す入力の名前。その口を持たない入力では null。
        /// </param>
        /// <param name="picked">
        /// 画面の選択を読む口。その回の相手を画面が選べないときは null。
        /// </param>
        public static bool TryIndices(
            McpMethodContext context,
            string name,
            string operation,
            IList<string> wanted,
            int ceiling,
            out IList<int> indices,
            out string code,
            out string message,
            IList<string> omittable = null,
            string selectedName = null,
            ScreenPick picked = null)
        {
            indices = new int[0];
            code = null;
            message = null;
            object given;
            bool asked = false;
            if (selectedName != null
                && !TryPicked(
                    context, selectedName, name, operation, wanted, ceiling, picked,
                    out indices, out asked, out code, out message))
            {
                return false;
            }

            if (asked)
            {
                return true;
            }

            if (!TryWanted(context, name, operation, wanted, out given, out asked, out code, out message))
            {
                return false;
            }

            if (!asked
                || (given == null
                    && omittable != null
                    && omittable.Contains(operation, StringComparer.Ordinal)))
            {
                return true;
            }

            object[] items = given as object[];
            List<int> taken = new List<int>();
            if (items != null)
            {
                foreach (object item in items)
                {
                    int number;
                    if (!ValueInput.TryIndex(item, out number) || number >= ceiling)
                    {
                        items = null;

                        break;
                    }

                    taken.Add(number);
                }
            }

            if (items == null)
            {
                return Refuse(
                    name + " は " + Listed(wanted) + " のときに渡す、0以上 "
                        + Spelled(ceiling) + " 未満の位置の並びである。",
                    out code,
                    out message);
            }

            indices = taken;

            return true;
        }

        private static bool TryPicked(
            McpMethodContext context,
            string selectedName,
            string name,
            string operation,
            IList<string> wanted,
            int ceiling,
            ScreenPick picked,
            out IList<int> indices,
            out bool took,
            out string code,
            out string message)
        {
            indices = new int[0];
            took = false;
            code = null;
            message = null;
            object given;
            bool asked;
            if (!TryWanted(
                context, selectedName, operation, wanted, out given, out asked, out code, out message))
            {
                return false;
            }

            if (!asked || !context.Params.ContainsKey(selectedName))
            {
                return true;
            }

            if (!(given is bool) || !(bool)given)
            {
                return Refuse(selectedName + " は真でなければならない。", out code, out message);
            }

            if (context.Params.ContainsKey(name))
            {
                return Refuse(
                    name + " と " + selectedName + " は同時に渡せない。どちらか1つで指す。",
                    out code,
                    out message);
            }

            if (picked == null)
            {
                return Refuse(
                    selectedName + " で指せる相手ではない。" + name + " で位置を渡す。",
                    out code,
                    out message);
            }

            int[] inside = picked.Taken().Where(at => at >= 0 && at < ceiling).ToArray();
            if (inside.Length == 0)
            {
                code = ToolEnvelope.NotApplicable;
                message = "画面で何も選ばれていない。" + picked.Picking + " で選んでから呼ぶ。";

                return false;
            }

            indices = inside;
            took = true;

            return true;
        }

        /// <summary>
        /// その操作で要る入力を取り出す。要らない操作で渡されていれば偽を返す。読む値があるときだけ
        /// asked を立て、要らない操作で渡されていないときは空を渡して真を返す。
        /// </summary>
        private static bool TryWanted(
            McpMethodContext context,
            string name,
            string operation,
            IList<string> wanted,
            out object given,
            out bool asked,
            out string code,
            out string message)
        {
            given = null;
            asked = false;
            code = null;
            message = null;
            object held;
            bool pointed = context.Params.TryGetValue(name, out held);
            if (!wanted.Contains(operation, StringComparer.Ordinal))
            {
                return !pointed
                    || Refuse(
                        name + " を渡せるのは " + Listed(wanted) + " のときだけである。",
                        out code,
                        out message);
            }

            given = held;
            asked = true;

            return true;
        }

        private static bool Refuse(string said, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = said;

            return false;
        }

        private static string Listed(IList<string> names)
        {
            return string.Join("・", names.ToArray());
        }

        private static string Spelled(int number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        private static string Spelled(float number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }
    }
}
