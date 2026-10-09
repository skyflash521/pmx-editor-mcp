using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した頂点のUVの反転・写し取り・視線方向からの貼り直しを行うツール。
    /// </summary>
    public static class ModelEditUv
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_uv";

        /// <summary>UのUVを反転する。</summary>
        public const string FlipU = "flipU";

        /// <summary>VのUVを反転する。</summary>
        public const string FlipV = "flipV";

        /// <summary>UとVの両方を反転する。</summary>
        public const string FlipUV = "flipUV";

        /// <summary>写す元の頂点のUVを、指した頂点へ写す。</summary>
        public const string Copy = "copy";

        /// <summary>
        /// 指した向きから見た位置でUVを貼り直す。Uの向きは、上の向きと見る向きの外積を正規化した
        /// もので、上の向きには0,1,0を採り、それが見る向きと平行なときだけ0,0,1を採る。Vの向きは
        /// 見る向きとUの向きの外積を正規化したもので、UVは頂点の位置をこの2つの向きへ落とした値に
        /// する。長さの無い向きは受け取らない。
        /// </summary>
        public const string ProjectFromView = "projectFromView";

        /// <summary>UVを写す元の頂点の位置を受け取る入力の名前。</summary>
        public const string SourceName = "source";

        /// <summary>見る向きを受け取る入力の名前。</summary>
        public const string DirectionName = "direction";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        private const float FlipFrom = 1f;

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get { return new[] { FlipU, FlipV, FlipUV, Copy, ProjectFromView }; }
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

            List<string> known = new List<string>
            {
                ComposedOperation.OperationName,
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
                ModelFindVertexBounds.MaterialIndicesName,
                SourceName,
                DirectionName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            IList<int> chosen;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !ModelFindVertexBounds.TryVertices(
                    context.Params,
                    model,
                    context.Screen.Pick(ElementKinds.Vertex, model.Vertex.Count),
                    out chosen,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int? source;
            V3 direction;
            if (!TrySource(context, operation, model.Vertex.Count, out source, out code, out message)
                || !TryDirection(context, operation, out direction, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            V2 taken = source.HasValue ? model.Vertex[source.Value].UV : null;
            V3 across = direction == null ? null : Across(direction);
            V3 down = direction == null ? null : Vectors.Perpendicular(direction, across);
            int changed = 0;
            foreach (int at in chosen)
            {
                IPXVertex vertex = model.Vertex[at];
                V2 written = Written(operation, vertex, taken, across, down);
                if (written.X != vertex.UV.X || written.Y != vertex.UV.Y)
                {
                    vertex.UV = written;
                    changed++;
                }
            }

            return ComposedEditResult.CompleteRewriting(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                },
                new[] { ElementKinds.Vertex });
        }

        private static V2 Written(
            string operation, IPXVertex vertex, V2 taken, V3 across, V3 down)
        {
            switch (operation)
            {
                case FlipU:
                    return new V2(FlipFrom - vertex.UV.X, vertex.UV.Y);

                case FlipV:
                    return new V2(vertex.UV.X, FlipFrom - vertex.UV.Y);

                case FlipUV:
                    return new V2(FlipFrom - vertex.UV.X, FlipFrom - vertex.UV.Y);

                case Copy:
                    return new V2(taken.X, taken.Y);

                default:
                    return new V2(
                        Vectors.Dot(vertex.Position, across), Vectors.Dot(vertex.Position, down));
            }
        }

        private static V3 Across(V3 direction)
        {
            V3 up = new V3(0f, 1f, 0f);
            V3 across = Vectors.Perpendicular(up, direction);

            return Vectors.HasLength(across)
                ? across
                : Vectors.Perpendicular(new V3(0f, 0f, 1f), direction);
        }

        private static bool TrySource(
            McpMethodContext context,
            string operation,
            int count,
            out int? source,
            out string code,
            out string message)
        {
            source = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            bool pointed = context.Params.TryGetValue(SourceName, out given);
            if (!string.Equals(operation, Copy, StringComparison.Ordinal))
            {
                if (pointed)
                {
                    message = SourceName + " を渡せるのは " + Copy + " のときだけである。";

                    return false;
                }

                code = null;

                return true;
            }

            int taken;
            if (!PositionInput.TryOne(
                given,
                SourceName,
                SourceName + " は " + Copy + " のときに渡す整数である。",
                count,
                out taken,
                out code,
                out message))
            {
                return false;
            }

            source = taken;
            code = null;

            return true;
        }

        private static bool TryDirection(
            McpMethodContext context,
            string operation,
            out V3 direction,
            out string code,
            out string message)
        {
            direction = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            bool pointed = context.Params.ContainsKey(DirectionName);
            if (!string.Equals(operation, ProjectFromView, StringComparison.Ordinal))
            {
                if (pointed)
                {
                    message = DirectionName + " を渡せるのは " + ProjectFromView + " のときだけである。";

                    return false;
                }

                code = null;

                return true;
            }

            return ComposedInput.TryDirection(context, DirectionName, out direction, out code, out message);
        }
    }
}
