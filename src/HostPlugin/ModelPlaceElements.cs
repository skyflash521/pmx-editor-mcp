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

        public static IList<string> Operations
        {
            get { return new[] { AlignTo }; }
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
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            string axes;
            V3 spot;
            IList<KeyValuePair<string, IList<int>>> targets;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !ComposedInput.TryChoice(context, AxesName, Axes, out axes, out code, out message)
                || !TryPosition(context, out spot, out code, out message)
                || !TryTargets(context, model, out targets, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int changed = 0;
            foreach (KeyValuePair<string, IList<int>> target in targets)
            {
                IList<object> items = Held(model, target.Key);
                foreach (int at in target.Value)
                {
                    changed += Placed(items[at], spot, axes) ? 1 : 0;
                }
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
                if (!TargetInput.TryPositions(
                    held, TargetNames.Element, Held(model, kind).Count, out positions, out code, out message))
                {
                    return false;
                }

                made.Add(new KeyValuePair<string, IList<int>>(kind, positions));
            }

            code = null;
            targets = made;

            return true;
        }

        private static bool TryPosition(
            McpMethodContext context, out V3 spot, out string code, out string message)
        {
            spot = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            context.Params.TryGetValue(PositionName, out given);
            object[] items = given as object[];
            float[] taken = new float[3];
            if (items == null || items.Length != taken.Length)
            {
                message = PositionName + " は3つの数の並びでなければならない。";

                return false;
            }

            for (int at = 0; at < taken.Length; at++)
            {
                if (!ValueInput.TrySingle(items[at], out taken[at]))
                {
                    message = PositionName + " は3つの有限の数の並びでなければならない。";

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
