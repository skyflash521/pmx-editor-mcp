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

        public const string RotateBy = "rotateBy";

        public const string ScaleBy = "scaleBy";

        public const string RotationName = "rotation";

        public const string ScaleName = "scale";

        public const string CenterName = "center";

        public static IList<string> Operations
        {
            get { return new[] { AlignTo, TranslateBy, RotateBy, ScaleBy }; }
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
                RotationName,
                ScaleName,
                CenterName,
            };
            methods.Add(ToolName, edit.Method(known, Run));
        }

        private static ComposedEditResult Run(McpMethodContext context, object pmx)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            Func<object, Change> plan;
            IList<KeyValuePair<string, IList<int>>> targets;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message)
                || !TryOnlyFor(context, operation, out code, out message)
                || !TryPlan(context, operation, out plan, out code, out message)
                || !TryTargets(context, model, out targets, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            List<Change> changes = new List<Change>();
            foreach (KeyValuePair<string, IList<int>> target in targets)
            {
                IList<object> held = Held(model, target.Key);
                changes.AddRange(target.Value.Select(at => plan(held[at])));
            }
            if (!changes.All(c => c.Finite))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    operation + " で動かすと、有限の数で表せない値になる要素がある。");
            }

            return ComposedEditResult.CompleteRewriting(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changes.Count(c => c.Apply()) },
                },
                targets.Select(t => t.Key).ToList());
        }

        private static bool TryPlan(
            McpMethodContext context,
            string operation,
            out Func<object, Change> plan,
            out string code,
            out string message)
        {
            plan = null;
            V3 center = new V3(0f, 0f, 0f);
            V3 given;
            switch (operation)
            {
                case AlignTo:
                    string axes;
                    if (!ComposedInput.TryChoice(
                            context, AxesName, Axes, out axes, out code, out message)
                        || !TryPoint(context, PositionName, out given, out code, out message))
                    {
                        return false;
                    }

                    plan = item => Change.Of(item, Aligned(Spot(item), given, axes));

                    return true;

                case TranslateBy:
                    if (!TryPoint(context, OffsetName, out given, out code, out message))
                    {
                        return false;
                    }

                    plan = item => Change.Of(item, Vectors.Add(Spot(item), given));

                    return true;

                case RotateBy:
                    if (!TryPoint(context, RotationName, out given, out code, out message)
                        || !TryCenter(context, ref center, out code, out message))
                    {
                        return false;
                    }

                    RowMatrix turn = RowMatrix.YawPitchRoll(
                        Radians(given.Y), Radians(given.X), Radians(given.Z));
                    plan = item => Turned(item, turn, center);

                    return true;

                default:
                    if (!TryPoint(context, ScaleName, out given, out code, out message)
                        || !TryCenter(context, ref center, out code, out message))
                    {
                        return false;
                    }

                    RowMatrix stretch = RowMatrix.Diagonal(given.X, given.Y, given.Z);
                    float? even = given.X == given.Y && given.Y == given.Z
                        ? given.X
                        : (float?)null;
                    plan = item => Stretched(item, stretch, center, even);

                    return true;
            }
        }

        private static bool TryCenter(
            McpMethodContext context, ref V3 center, out string code, out string message)
        {
            code = null;
            message = null;

            return !context.Params.ContainsKey(CenterName)
                || TryPoint(context, CenterName, out center, out code, out message);
        }

        private static Change Turned(object item, RowMatrix turn, V3 center)
        {
            Change made = Change.Of(item, turn.TransformAbout(Spot(item), center));
            IPXVertex vertex = item as IPXVertex;
            if (vertex != null)
            {
                made.Normal = turn.Transform(vertex.Normal);
            }

            V3 rotation = Rotation(item);
            if (rotation != null)
            {
                made.Rotation = RowMatrix
                    .YawPitchRoll(rotation.Y, rotation.X, rotation.Z)
                    .Times(turn)
                    .ToEulerZxy();
            }

            return made;
        }

        private static Change Stretched(object item, RowMatrix stretch, V3 center, float? even)
        {
            Change made = Change.Of(item, stretch.TransformAbout(Spot(item), center));
            IPXBody body = item as IPXBody;
            if (body != null && even.HasValue)
            {
                made.Size = Vectors.Scale(body.BoxSize, even.Value);
            }

            return made;
        }

        private static V3 Aligned(V3 before, V3 spot, string axes)
        {
            return new V3(
                Taken(axes, ModelEditVertices.AxisX) ? spot.X : before.X,
                Taken(axes, ModelEditVertices.AxisY) ? spot.Y : before.Y,
                Taken(axes, ModelEditVertices.AxisZ) ? spot.Z : before.Z);
        }

        private static double Radians(float degrees)
        {
            return degrees * Math.PI / 180d;
        }

        private static V3 Rotation(object item)
        {
            IPXBody body = item as IPXBody;
            if (body != null)
            {
                return body.Rotation;
            }

            IPXJoint joint = item as IPXJoint;

            return joint == null ? null : joint.Rotation;
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
            McpMethodContext context, string operation, out string code, out string message)
        {
            code = null;
            message = null;
            string[] taken;
            switch (operation)
            {
                case AlignTo:
                    taken = new[] { PositionName, AxesName };
                    break;

                case TranslateBy:
                    taken = new[] { OffsetName };
                    break;

                case RotateBy:
                    taken = new[] { RotationName, CenterName };
                    break;

                default:
                    taken = new[] { ScaleName, CenterName };
                    break;
            }

            string given = new[] { PositionName, AxesName, OffsetName, RotationName, ScaleName, CenterName }
                .Where(n => !taken.Contains(n, StringComparer.Ordinal))
                .FirstOrDefault(context.Params.ContainsKey);
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

        private sealed class Change
        {
            private readonly object _item;

            private readonly V3 _position;

            private Change(object item, V3 position)
            {
                _item = item;
                _position = position;
            }

            public V3 Normal { get; set; }

            public V3 Rotation { get; set; }

            public V3 Size { get; set; }

            public bool Finite
            {
                get
                {
                    return new[] { _position, Normal, Rotation, Size }
                        .Where(v => v != null)
                        .All(Vectors.Finite);
                }
            }

            public static Change Of(object item, V3 position)
            {
                return new Change(item, position);
            }

            /// <summary>書いた値のどれかが前と違っていれば真。</summary>
            public bool Apply()
            {
                bool changed = false;
                if (!Vectors.Same(Spot(_item), _position))
                {
                    Put(_item, _position);
                    changed = true;
                }

                IPXVertex vertex = _item as IPXVertex;
                if (Normal != null && vertex != null && !Vectors.Same(vertex.Normal, Normal))
                {
                    vertex.Normal = Normal;
                    changed = true;
                }

                IPXBody body = _item as IPXBody;
                IPXJoint joint = _item as IPXJoint;
                if (Rotation != null && body != null && !Vectors.Same(body.Rotation, Rotation))
                {
                    body.Rotation = Rotation;
                    changed = true;
                }

                if (Rotation != null && joint != null && !Vectors.Same(joint.Rotation, Rotation))
                {
                    joint.Rotation = Rotation;
                    changed = true;
                }

                if (Size != null && body != null && !Vectors.Same(body.BoxSize, Size))
                {
                    body.BoxSize = Size;
                    changed = true;
                }

                return changed;
            }
        }
    }
}
