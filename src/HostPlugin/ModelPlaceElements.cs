using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class ModelPlaceElements
    {
        public const string ToolName = "model_place_elements";

        public const string AlignTo = "alignTo";

        public const string TranslateBy = "translateBy";

        public const string OffsetName = "offset";

        public static IList<string> Operations
        {
            get { return new[] { AlignTo, TranslateBy }; }
        }

        public const string TargetsName = "targets";

        public const string PositionName = "position";

        public const string AxesName = "axes";

        public const string AllAxes = "xyz";

        public static IList<string> Axes
        {
            get
            {
                return new[]
                {
                    AllAxes,
                    ModelEditVertices.AxisX,
                    ModelEditVertices.AxisY,
                    ModelEditVertices.AxisZ,
                };
            }
        }

        public static IList<string> Kinds
        {
            get
            {
                return new[]
                {
                    ElementKinds.Vertex,
                    ElementKinds.Bone,
                    ElementKinds.Body,
                    ElementKinds.Joint,
                };
            }
        }

        public const string ChangedName = "changed";

        public const string KindName = "kind";

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
                TargetsName,
                PositionName,
                AxesName,
                OffsetName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            string axes = null;
            V3 spot = null;
            V3 offset = null;
            IList<KeyValuePair<string, IList<int>>> targets;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            bool aligning = string.Equals(operation, AlignTo, StringComparison.Ordinal);
            if (!TryOnlyFor(context, operation, aligning, out code, out message)
                || (aligning
                    && (!ComposedInput.TryChoice(
                            context, AxesName, Axes, out axes, out code, out message)
                        || !TryPoint(context, PositionName, out spot, out code, out message)))
                || (!aligning && !TryPoint(context, OffsetName, out offset, out code, out message))
                || !TryTargets(context, model, out targets, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<object> picked = targets
                .SelectMany(t => t.Value.Select(at => Held(model, t.Key)[at]))
                .ToList();
            if (!aligning && picked.Any(item => !Vectors.Finite(Moved(Spot(item), offset))))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    OffsetName + " だけずらすと、有限の数で表せない座標になる要素がある。");
            }

            int changed = 0;
            foreach (object item in picked)
            {
                bool moved = aligning ? Placed(item, spot, axes) : Shifted(item, offset);
                changed += moved ? 1 : 0;
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                });
        }

        private static bool TryTargets(
            McpMethodContext context,
            IPXPmx model,
            out IList<KeyValuePair<string, IList<int>>> targets,
            out string code,
            out string message)
        {
            targets = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            context.Params.TryGetValue(TargetsName, out given);
            object[] items = given as object[];
            if (items == null || items.Length == 0)
            {
                message = TargetsName + " は、そろえる要素を種類ごとに指した組を1つ以上"
                    + "並べたものでなければならない。";

                return false;
            }

            List<KeyValuePair<string, IList<int>>> made =
                new List<KeyValuePair<string, IList<int>>>();
            foreach (object item in items)
            {
                IDictionary<string, object> held = item as IDictionary<string, object>;
                object name;
                string kind = null;
                if (held != null && held.TryGetValue(KindName, out name))
                {
                    kind = name as string;
                }

                if (kind == null || !Kinds.Contains(kind, StringComparer.Ordinal))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = TargetsName + " の " + KindName + " は次のどれかでなければならない: "
                        + string.Join("・", Kinds.ToArray());

                    return false;
                }

                if (made.Any(t => string.Equals(t.Key, kind, StringComparison.Ordinal)))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = TargetsName + " へ同じ種類を二度並べている: " + kind;

                    return false;
                }

                IList<int> positions;
                int count = Held(model, kind).Count;
                if (!TargetInput.TryPositions(
                    held,
                    TargetNames.Element,
                    count,
                    out positions,
                    out code,
                    out message,
                    context.Screen.Pick(kind, count)))
                {
                    return false;
                }

                made.Add(new KeyValuePair<string, IList<int>>(kind, positions));
            }

            code = null;
            targets = made;

            return true;
        }

        private static bool TryOnlyFor(
            McpMethodContext context,
            string operation,
            bool aligning,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            string[] foreign = aligning ? new[] { OffsetName } : new[] { PositionName, AxesName };
            string given = foreign.FirstOrDefault(context.Params.ContainsKey);
            if (given == null)
            {
                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            message = given + " は " + operation + " では渡せない。";

            return false;
        }

        private static bool TryPoint(
            McpMethodContext context, string name, out V3 spot, out string code, out string message)
        {
            spot = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            context.Params.TryGetValue(name, out given);
            object[] items = given as object[];
            float[] taken = new float[3];
            if (items == null || items.Length != taken.Length)
            {
                message = name + " は3つの数の並びでなければならない。";

                return false;
            }

            for (int at = 0; at < taken.Length; at++)
            {
                if (!ValueInput.TrySingle(items[at], out taken[at]))
                {
                    message = name + " は3つの有限の数の並びでなければならない。";

                    return false;
                }
            }

            code = null;
            spot = new V3(taken[0], taken[1], taken[2]);

            return true;
        }

        private static IList<object> Held(IPXPmx model, string kind)
        {
            switch (kind)
            {
                case ElementKinds.Vertex:
                    return model.Vertex.Cast<object>().ToList();

                case ElementKinds.Bone:
                    return model.Bone.Cast<object>().ToList();

                case ElementKinds.Body:
                    return model.Body.Cast<object>().ToList();

                default:
                    return model.Joint.Cast<object>().ToList();
            }
        }

        /// <summary>動いたなら真を返す。</summary>
        private static bool Placed(object item, V3 spot, string axes)
        {
            V3 before = Spot(item);
            V3 after = new V3(
                Taken(axes, ModelEditVertices.AxisX) ? spot.X : before.X,
                Taken(axes, ModelEditVertices.AxisY) ? spot.Y : before.Y,
                Taken(axes, ModelEditVertices.AxisZ) ? spot.Z : before.Z);
            if (Vectors.Same(before, after))
            {
                return false;
            }

            Put(item, after);

            return true;
        }

        private static bool Shifted(object item, V3 offset)
        {
            V3 before = Spot(item);
            V3 after = Moved(before, offset);
            if (Vectors.Same(before, after))
            {
                return false;
            }

            Put(item, after);

            return true;
        }

        private static V3 Moved(V3 before, V3 offset)
        {
            return new V3(before.X + offset.X, before.Y + offset.Y, before.Z + offset.Z);
        }

        private static bool Taken(string axes, string axis)
        {
            return string.Equals(axes, AllAxes, StringComparison.Ordinal)
                || string.Equals(axes, axis, StringComparison.Ordinal);
        }

        private static V3 Spot(object item)
        {
            IPXVertex vertex = item as IPXVertex;
            if (vertex != null)
            {
                return vertex.Position;
            }

            IPXBone bone = item as IPXBone;
            if (bone != null)
            {
                return bone.Position;
            }

            IPXBody body = item as IPXBody;

            return body != null ? body.Position : ((IPXJoint)item).Position;
        }

        private static void Put(object item, V3 spot)
        {
            IPXVertex vertex = item as IPXVertex;
            if (vertex != null)
            {
                vertex.Position = spot;

                return;
            }

            IPXBone bone = item as IPXBone;
            if (bone != null)
            {
                bone.Position = spot;

                return;
            }

            IPXBody body = item as IPXBody;
            if (body != null)
            {
                body.Position = spot;

                return;
            }

            ((IPXJoint)item).Position = spot;
        }
    }
}
