using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 画面の選択を、全選択・反転・拡張・縮小・子の連なり・半モデルで置き換えるツール。
    /// </summary>
    public static class ViewSelectElements
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_select_elements";

        /// <summary>その種類の要素を全部選ぶ。</summary>
        public const string All = "all";

        /// <summary>いま選んでいるものと選んでいないものを入れ替える。</summary>
        public const string Invert = "invert";

        /// <summary>いまの選択に、面で隣り合う要素を足す。</summary>
        public const string Expand = "expand";

        /// <summary>いまの選択から、選んでいない要素と面で隣り合うものを外す。</summary>
        public const string Reduce = "reduce";

        /// <summary>いま選んでいるボーンの子孫を足す。</summary>
        public const string ChildChain = "childChain";

        /// <summary>指した軸の片側にある要素だけを選ぶ。</summary>
        public const string HalfModel = "halfModel";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { All, Invert, Expand, Reduce, ChildChain, HalfModel };
            }
        }

        /// <summary>選ぶ要素の種類を受け取る入力の名前。</summary>
        public const string KindName = "kind";

        public const string KindsName = "kinds";

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>X軸の値が0以下の側。</summary>
        public const string NegativeX = "negativeX";

        /// <summary>Y軸の値が0以下の側。</summary>
        public const string NegativeY = "negativeY";

        /// <summary>Z軸の値が0以下の側。</summary>
        public const string NegativeZ = "negativeZ";

        /// <summary>
        /// 受け取れる軸。スキーマが並べる順。x・y・z はその軸の値が0以上の側を、negative の3つは
        /// 0以下の側を指す。
        /// </summary>
        public static IList<string> Axes
        {
            get
            {
                return new[]
                {
                    ModelEditVertices.AxisX,
                    ModelEditVertices.AxisY,
                    ModelEditVertices.AxisZ,
                    NegativeX,
                    NegativeY,
                    NegativeZ,
                };
            }
        }

        /// <summary>選んだ要素の数を返す項目の名前。</summary>
        public const string SelectedName = "selected";

        public const string CountsName = "counts";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            List<string> known = new List<string>
            {
                ComposedOperation.OperationName,
                KindName,
                KindsName,
                AxisName,
            };
            methods.Add(
                ToolName, screen.Method(known, ScreenNeeds.View | ScreenNeeds.Pmx, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, ScreenParts parts)
        {
            IPXPmx model = (IPXPmx)parts.Pmx;
            string operation;
            string axis;
            string code;
            string message;
            IList<string> kinds;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !TryKinds(context, operation, out kinds, out code, out message)
                || !ComposedInput.TryChoice(
                    context,
                    AxisName,
                    operation,
                    new[] { HalfModel },
                    Axes,
                    out axis,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int selected = 0;
            IList<object> counts = new List<object>();
            foreach (string kind in kinds)
            {
                int took = 0;
                ComposedEditResult refused =
                    Chosen(model, parts, operation, kind, axis, ref took);
                if (refused != null)
                {
                    return refused;
                }

                selected += took;
                counts.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { KindName, kind },
                    { SelectedName, took },
                });
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { SelectedName, selected },
                    { CountsName, counts },
                });
        }

        /// <summary>選び直せたなら null を、断るなら断りを返す。</summary>
        private static ComposedEditResult Chosen(
            IPXPmx model,
            ScreenParts parts,
            string operation,
            string kind,
            string axis,
            ref int selected)
        {
            int count = ViewSelection.Count(model, kind);
            IList<int> held = ViewSelection.Taken(parts.View, kind, count);
            IList<int> made;
            switch (operation)
            {
                case All:
                    made = Enumerable.Range(0, count).ToList();

                    break;

                case Invert:
                    made = Enumerable.Range(0, count).Except(held).ToList();

                    break;

                case ChildChain:
                    if (!string.Equals(kind, ElementKinds.Bone, StringComparison.Ordinal))
                    {
                        return Only(ChildChain, ElementKinds.Bone);
                    }

                    made = Below(model, held);

                    break;

                case HalfModel:
                    made = Halved(model, kind, axis);

                    break;

                default:
                    if (!string.Equals(kind, ElementKinds.Vertex, StringComparison.Ordinal))
                    {
                        return Only(operation, ElementKinds.Vertex);
                    }

                    made = Touching(model, held, operation);

                    break;
            }

            ViewSelection.Put(parts.View, kind, made);
            selected += made.Count;

            return null;
        }

        /// <summary>
        /// 選び直す種類を読む。<paramref name="operation"/> が全選択か反転のときだけ kinds で
        /// まとめて渡せる。
        /// </summary>
        private static bool TryKinds(
            McpMethodContext context,
            string operation,
            out IList<string> kinds,
            out string code,
            out string message)
        {
            kinds = null;
            if (!context.Params.ContainsKey(KindsName))
            {
                string kind;
                if (!ViewSelection.TryKind(context, KindName, out kind, out code, out message))
                {
                    return false;
                }

                kinds = new[] { kind };

                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            message = null;
            if (!string.Equals(operation, All, StringComparison.Ordinal)
                && !string.Equals(operation, Invert, StringComparison.Ordinal))
            {
                message = KindsName + " を渡せるのは " + All + "・" + Invert + " のときだけである。";

                return false;
            }

            if (context.Params.ContainsKey(KindName))
            {
                message = KindName + " と " + KindsName + " は、どちらか1つだけを渡す。";

                return false;
            }

            object given;
            context.Params.TryGetValue(KindsName, out given);
            object[] items = given as object[];
            if (items == null || items.Length == 0)
            {
                message = KindsName + " は、選ぶ要素の種類を1つ以上並べたものでなければならない。";

                return false;
            }

            List<string> made = new List<string>();
            foreach (object item in items)
            {
                string kind = item as string;
                if (kind == null || !ViewSelection.Kinds.Contains(kind, StringComparer.Ordinal))
                {
                    message = KindsName + " は次のどれかを並べる: "
                        + string.Join("・", ViewSelection.Kinds.ToArray());

                    return false;
                }

                if (made.Contains(kind, StringComparer.Ordinal))
                {
                    message = KindsName + " へ同じ種類を二度並べている: " + kind;

                    return false;
                }

                made.Add(kind);
            }

            code = null;
            kinds = made;

            return true;
        }

        private static ComposedEditResult Only(string operation, string kind)
        {
            return ComposedEditResult.Refuse(
                ToolEnvelope.NotApplicable,
                operation + " を当てられるのは " + kind + " の選択だけである。");
        }

        /// <summary>
        /// 面で隣り合う頂点で選び直す。広げる操作は選んでいる頂点と面を共にする頂点を足し、狭める
        /// 操作は選んでいない頂点と面を共にする頂点を外す。
        /// </summary>
        private static IList<int> Touching(IPXPmx model, IList<int> held, string operation)
        {
            bool widening = string.Equals(operation, Expand, StringComparison.Ordinal);
            IDictionary<IPXVertex, int> at = Placed(model.Vertex);
            HashSet<int> chosen = new HashSet<int>(held);
            HashSet<int> found = new HashSet<int>();
            foreach (IPXFace face in ViewSelection.Faces(model))
            {
                IList<int> corners = ViewSelection.Corners(face)
                    .Where(vertex => vertex != null && at.ContainsKey(vertex))
                    .Select(vertex => at[vertex])
                    .ToList();
                if (widening
                    ? corners.Any(chosen.Contains)
                    : corners.Any(corner => !chosen.Contains(corner)))
                {
                    found.UnionWith(corners);
                }
            }

            return widening
                ? chosen.Union(found).ToList()
                : chosen.Except(found).ToList();
        }

        /// <summary>選んだボーンと、そのボーンを先祖に持つボーン。</summary>
        private static IList<int> Below(IPXPmx model, IList<int> held)
        {
            IDictionary<IPXBone, int> at = Placed(model.Bone);
            HashSet<int> chosen = new HashSet<int>(held);
            List<int> made = new List<int>();
            for (int position = 0; position < model.Bone.Count; position++)
            {
                if (chosen.Contains(position) || Descends(model.Bone[position], at, chosen))
                {
                    made.Add(position);
                }
            }

            return made;
        }

        /// <summary>そのボーンが、選んだボーンのどれかを先祖に持つか。</summary>
        private static bool Descends(
            IPXBone bone, IDictionary<IPXBone, int> at, ICollection<int> chosen)
        {
            HashSet<IPXBone> met = new HashSet<IPXBone>(ReferenceComparer<IPXBone>.Instance);
            for (IPXBone above = bone.Parent; above != null && met.Add(above); above = above.Parent)
            {
                int position;
                if (at.TryGetValue(above, out position) && chosen.Contains(position))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>その軸の片側に置かれている要素。面は3つの頂点がすべて片側にあるものを選ぶ。</summary>
        private static IList<int> Halved(IPXPmx model, string kind, string axis)
        {
            IList<IList<V3>> spots = ViewSelection.Spots(model, kind);

            return Enumerable.Range(0, spots.Count)
                .Where(at => spots[at].All(spot => Aside(spot, axis)))
                .ToList();
        }

        /// <summary>その点が、指した軸の側にあるか。軸の上にある点はどちらの側にも入る。</summary>
        private static bool Aside(V3 spot, string axis)
        {
            switch (axis)
            {
                case ModelEditVertices.AxisX:
                    return spot.X >= 0f;

                case ModelEditVertices.AxisY:
                    return spot.Y >= 0f;

                case ModelEditVertices.AxisZ:
                    return spot.Z >= 0f;

                case NegativeX:
                    return spot.X <= 0f;

                case NegativeY:
                    return spot.Y <= 0f;

                default:
                    return spot.Z <= 0f;
            }
        }

        private static IDictionary<T, int> Placed<T>(IList<T> items)
            where T : class
        {
            Dictionary<T, int> made =
                new Dictionary<T, int>(ReferenceComparer<T>.Instance);
            for (int at = 0; at < items.Count; at++)
            {
                if (items[at] != null && !made.ContainsKey(items[at]))
                {
                    made.Add(items[at], at);
                }
            }

            return made;
        }
    }
}
