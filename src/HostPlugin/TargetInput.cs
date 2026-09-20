using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 要求が持ってきた項目から、要素の集合の指定を読み取る。読み取るだけで、範囲へ収まるかは
    /// <see cref="TargetSelection"/> が見る。
    /// </summary>
    public static class TargetInput
    {
        public const string StartName = "start";

        public const string CountName = "count";

        /// <summary>
        /// 選択の指定が1つも渡されていないことを確かめる。渡されていれば偽を返し、断る内容を渡す。
        /// <paramref name="wanted"/> は、その指定を渡せる操作の名前である。
        /// </summary>
        public static bool TryNoTarget(
            IDictionary<string, object> given,
            TargetNames names,
            IList<string> wanted,
            out string code,
            out string message)
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            code = null;
            message = null;
            string[] held = { names.Indices, names.Range, names.All };
            foreach (string name in held.Where(given.ContainsKey))
            {
                code = ToolEnvelope.InvalidArgument;
                message = name + " を渡せるのは "
                    + string.Join("・", wanted.ToArray()) + " のときだけである。";

                return false;
            }

            return true;
        }

        /// <summary>
        /// 1つの集合の指定を読む。<paramref name="handles"/> が偽なら、ハンドルの配列は読まない。
        /// 値の形が違えば偽で、断る内容を渡す。
        /// </summary>
        public static bool TryTake(
            IDictionary<string, object> parameters,
            TargetNames names,
            bool handles,
            out TargetRequest request,
            out string code,
            out string message)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            request = null;
            IList<int> indices;
            int? start;
            int? count;
            bool? all;
            IList<long> held = null;
            if (!TryIndices(parameters, names, out indices, out code, out message)
                || !TryRange(parameters, names, out start, out count, out code, out message)
                || !TryAll(parameters, names, out all, out code, out message)
                || (handles && !TryHandles(parameters, names, out held, out code, out message)))
            {
                return false;
            }

            request = new TargetRequest(indices, start, count, all, held);

            return true;
        }

        /// <summary>
        /// 受け取る名前だけが来ているかを見る。知らない名前が1つでもあれば偽で、断る内容を渡す。
        /// </summary>
        public static bool TryOnlyKnown(
            IDictionary<string, object> parameters,
            IList<string> known,
            out string code,
            out string message)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (known == null)
            {
                throw new ArgumentNullException(nameof(known));
            }

            string unknown = parameters.Keys
                .Where(n => !known.Contains(n, StringComparer.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = "知らない引数を渡している: " + unknown;

                return false;
            }

            code = null;
            message = null;

            return true;
        }

        /// <summary>
        /// その並びの中で、指した位置を解く。位置は昇順で重なりを持たない。解けなければ偽で、
        /// 断る内容を渡す。
        /// </summary>
        public static bool TryPositions(
            IDictionary<string, object> parameters,
            TargetNames names,
            int count,
            out IList<int> positions,
            out string code,
            out string message)
        {
            positions = null;
            TargetRequest request;
            if (!TryTake(parameters, names, false, out request, out code, out message))
            {
                return false;
            }

            ResolvedTargets resolved;
            if (!TargetSelection.TryResolve(
                request,
                TargetForm.Indices | TargetForm.Range | TargetForm.All,
                count,
                id => false,
                out resolved,
                out code,
                out message,
                names))
            {
                return false;
            }

            positions = resolved.Indices.OrderBy(at => at).ToList();

            return true;
        }

        private static bool TryIndices(
            IDictionary<string, object> parameters,
            TargetNames names,
            out IList<int> indices,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            indices = null;
            object value;
            if (!parameters.TryGetValue(names.Indices, out value))
            {
                return true;
            }

            object[] items = value as object[];
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Indices + " は位置の配列でなければならない。";

                return false;
            }

            List<int> taken = new List<int>();
            foreach (object item in items)
            {
                int number;
                if (!ValueInput.TryIndex(item, out number))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = names.Indices + " は整数の配列でなければならない。";

                    return false;
                }

                taken.Add(number);
            }

            indices = taken;

            return true;
        }

        private static bool TryRange(
            IDictionary<string, object> parameters,
            TargetNames names,
            out int? start,
            out int? count,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            start = null;
            count = null;
            object value;
            if (!parameters.TryGetValue(names.Range, out value))
            {
                return true;
            }

            IDictionary<string, object> members = value as IDictionary<string, object>;
            object given;
            int taken;
            if (members == null
                || !members.TryGetValue(StartName, out given)
                || !ValueInput.TryIndex(given, out taken))
            {
                code = ToolEnvelope.InvalidArgument;
                message = Pair(names);

                return false;
            }

            start = taken;
            if (!members.TryGetValue(CountName, out given)
                || !ValueInput.TryIndex(given, out taken))
            {
                code = ToolEnvelope.InvalidArgument;
                message = Pair(names);

                return false;
            }

            count = taken;

            return true;
        }

        private static string Pair(TargetNames names)
        {
            return names.Range + " は " + StartName + " と " + CountName + " の組でなければならない。";
        }

        private static bool TryAll(
            IDictionary<string, object> parameters,
            TargetNames names,
            out bool? all,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            all = null;
            object value;
            if (!parameters.TryGetValue(names.All, out value))
            {
                return true;
            }

            if (!(value is bool))
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.All + " は真偽でなければならない。";

                return false;
            }

            all = (bool)value;

            return true;
        }

        private static bool TryHandles(
            IDictionary<string, object> parameters,
            TargetNames names,
            out IList<long> handles,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            handles = null;
            object value;
            if (!parameters.TryGetValue(names.Handles, out value))
            {
                return true;
            }

            object[] items = value as object[];
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Handles + " はハンドルの配列でなければならない。";

                return false;
            }

            List<long> taken = new List<long>();
            foreach (object item in items)
            {
                long number;
                if (!ValueInput.TryInteger(item, out number))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = names.Handles + " は整数の配列でなければならない。";

                    return false;
                }

                taken.Add(number);
            }

            handles = taken;

            return true;
        }
    }
}
