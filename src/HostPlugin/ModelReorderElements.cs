using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した要素を、1つ上・1つ下・先頭・末尾・指した位置へ動かすツール。動かすのは並びだけで、
    /// 要素を指す参照はオブジェクトのまま変わらない。指した要素どうしの前後は保たれる。
    /// </summary>
    public static class ModelReorderElements
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_reorder_elements";

        /// <summary>どこへ動かすかを受け取る入力の名前。</summary>
        public const string MoveName = "move";

        /// <summary>動かす先の位置を受け取る入力の名前。</summary>
        public const string ToIndexName = "toIndex";

        /// <summary>1つ上へ動かす。</summary>
        public const string Up = "up";

        /// <summary>1つ下へ動かす。</summary>
        public const string Down = "down";

        /// <summary>先頭へ動かす。</summary>
        public const string Top = "top";

        /// <summary>末尾へ動かす。</summary>
        public const string Bottom = "bottom";

        /// <summary>指した位置へ動かす。</summary>
        public const string To = "to";

        /// <summary>動いた後の位置を返す項目の名前。</summary>
        public const string IndicesName = "indices";

        /// <summary>受け取れる動かし方。スキーマが並べる順。</summary>
        public static IList<string> Moves
        {
            get { return new[] { Up, Down, Top, Bottom, To }; }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            List<string> known = new List<string>(ElementScope.Names)
            {
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
                MoveName,
                ToIndexName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            ElementKind kind;
            IList<object> owners;
            string code;
            string message;
            string move;
            int? to;
            if (!ElementScope.TryTake(context, pmx, out kind, out owners, out code, out message)
                || !TryMove(context, out move, out to, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<object> moved = new List<object>();
            foreach (object owner in owners)
            {
                IList<int> chosen;
                if (!ElementScope.TryPositions(
                    context, kind, owner, out chosen, out code, out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }

                IList<object> items = kind.Items(owner);
                if (to.HasValue && (to.Value < 0 || to.Value >= items.Count))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.IndexOutOfRange,
                        ToIndexName + " が並びの外を指している: " + to.Value);
                }

                IList<int> landed = Landed(move, to, chosen, items.Count);
                kind.Replace(owner, Ordered(items, chosen, landed));
                foreach (int at in landed)
                {
                    moved.Add(at);
                }
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { IndicesName, moved.ToArray() },
                });
        }

        private static IList<int> Landed(string move, int? to, IList<int> chosen, int count)
        {
            switch (move)
            {
                case Up:
                    return Lifted(chosen);

                case Down:
                    return Lowered(chosen, count);

                case Top:
                    return Packed(0, chosen.Count);

                case Bottom:
                    return Packed(count - chosen.Count, chosen.Count);

                default:
                    return Packed(Math.Min(to.Value, count - chosen.Count), chosen.Count);
            }
        }

        private static IList<int> Lifted(IList<int> chosen)
        {
            List<int> landed = new List<int>();
            int floor = 0;
            foreach (int at in chosen)
            {
                int put = Math.Max(at - 1, floor);
                landed.Add(put);
                floor = put + 1;
            }

            return landed;
        }

        private static IList<int> Lowered(IList<int> chosen, int count)
        {
            int[] landed = new int[chosen.Count];
            int ceiling = count - 1;
            for (int at = chosen.Count - 1; at >= 0; at--)
            {
                int put = Math.Min(chosen[at] + 1, ceiling);
                landed[at] = put;
                ceiling = put - 1;
            }

            return landed;
        }

        private static IList<int> Packed(int from, int taken)
        {
            return Enumerable.Range(Math.Max(from, 0), taken).ToList();
        }

        /// <summary>動かした後の並び。指した要素を行き先へ置き、残りを元の順で埋める。</summary>
        private static IList<object> Ordered(
            IList<object> items, IList<int> chosen, IList<int> landed)
        {
            object[] written = new object[items.Count];
            bool[] filled = new bool[items.Count];
            for (int at = 0; at < chosen.Count; at++)
            {
                written[landed[at]] = items[chosen[at]];
                filled[landed[at]] = true;
            }

            Queue<object> rest = new Queue<object>(
                items.Where((item, at) => !chosen.Contains(at)));
            for (int at = 0; at < written.Length; at++)
            {
                if (!filled[at])
                {
                    written[at] = rest.Dequeue();
                }
            }

            return written;
        }

        private static bool TryMove(
            McpMethodContext context, out string move, out int? to, out string code, out string message)
        {
            move = null;
            to = null;
            code = ToolEnvelope.InvalidArgument;
            object given;
            context.Params.TryGetValue(MoveName, out given);
            move = given as string;
            if (move == null || !Moves.Contains(move, StringComparer.Ordinal))
            {
                message = MoveName + " は次のどれかでなければならない: "
                    + string.Join("・", Moves.ToArray());

                return false;
            }

            object wanted;
            bool pointed = context.Params.TryGetValue(ToIndexName, out wanted);
            if (string.Equals(move, To, StringComparison.Ordinal))
            {
                int taken;
                if (!pointed || !ValueInput.TryIndex(wanted, out taken))
                {
                    message = ToIndexName + " は " + To + " のときに渡す整数である。";

                    return false;
                }

                to = taken;
            }
            else if (pointed)
            {
                message = ToIndexName + " を渡せるのは " + MoveName + " が " + To + " のときだけである。";

                return false;
            }

            code = null;
            message = null;

            return true;
        }
    }
}
