using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    public static class ModelEditNormals
    {
        public const string ToolName = "model_edit_normals";

        public const string Average = "average";

        public const string AverageNear = "averageNear";

        public const string FromFaces = "fromFaces";

        public const string Normalize = "normalize";

        public const string Flip = "flip";

        public const string Rotate = "rotate";

        public const string RotateFromBase = "rotateFromBase";

        public const string RotationAxisName = "rotationAxis";

        public const string RotationAngleName = "rotationAngle";

        public const string ThresholdName = "threshold";

        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Average, AverageNear, FromFaces, Normalize, Flip, Rotate, RotateFromBase };
            }
        }

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
                ThresholdName,
                RotationAxisName,
                RotationAngleName,
                ModelMorphFromMoved.BasePmxHandleName,
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

            if (!string.Equals(operation, RotateFromBase, StringComparison.Ordinal)
                && context.Params.ContainsKey(ModelMorphFromMoved.BasePmxHandleName))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    ModelMorphFromMoved.BasePmxHandleName + " を渡せるのは " + RotateFromBase + " のときだけである。");
            }

            float threshold;
            if (!ComposedInput.TryFloat(
                    context,
                    ThresholdName,
                    operation,
                    new[] { AverageNear },
                    0f,
                    ComposedInput.NoCeiling,
                    out threshold,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            RowMatrix turn;
            if (!TryTurn(context, operation, out turn, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (string.Equals(operation, RotateFromBase, StringComparison.Ordinal))
            {
                return RotatedFromBase(context, model, chosen);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            IList<V3> before = picked.Select(vertex => Vectors.Copied(vertex.Normal)).ToList();
            switch (operation)
            {
                case Average:
                    Aim(picked, Shared(picked));
                    break;

                case AverageNear:
                    foreach (IList<IPXVertex> group in VertexClusters.Near(picked, threshold))
                    {
                        Aim(group, Shared(group));
                    }

                    break;

                case FromFaces:
                    FromTheFaces(model, picked);
                    break;

                case Normalize:
                    foreach (IPXVertex vertex in picked)
                    {
                        vertex.Normal = Vectors.Normalized(vertex.Normal);
                    }

                    break;

                case Rotate:
                    foreach (IPXVertex vertex in picked)
                    {
                        vertex.Normal = turn.Transform(vertex.Normal);
                    }

                    break;

                default:
                    foreach (IPXVertex vertex in picked)
                    {
                        vertex.Normal = Vectors.Scale(vertex.Normal, -1f);
                    }

                    break;
            }

            int changed = 0;
            for (int at = 0; at < picked.Count; at++)
            {
                changed += Vectors.Same(before[at], picked[at].Normal) ? 0 : 1;
            }

            return ComposedEditResult.CompleteRewriting(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                },
                new[] { ElementKinds.Vertex });
        }

        private static ComposedEditResult RotatedFromBase(
            McpMethodContext context, IPXPmx model, IList<int> chosen)
        {
            IPXPmx based;
            string code;
            string message;
            if (!ModelMorphFromMoved.TryBase(context, out based, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            string differs = ModelCompareShape.Differs(based, model);
            if (differs != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, differs);
            }

            bool[] wanted = new bool[model.Vertex.Count];
            foreach (int at in chosen)
            {
                wanted[at] = true;
            }

            IDictionary<IPXVertex, int> places = ModelCleanFaces.Places(model);
            double[][] sums = new double[model.Vertex.Count][];
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    if (!ReferenceCleanup.IsSoundFace(face))
                    {
                        continue;
                    }

                    int[] corners = new int[3];
                    if (!places.TryGetValue(face.Vertex1, out corners[0])
                        || !places.TryGetValue(face.Vertex2, out corners[1])
                        || !places.TryGetValue(face.Vertex3, out corners[2])
                        || !corners.Any(at => wanted[at]))
                    {
                        continue;
                    }

                    double area;
                    double[] turn = FaceTurn(based, model, corners, out area);
                    if (turn == null)
                    {
                        continue;
                    }

                    foreach (int at in corners.Where(at => wanted[at]))
                    {
                        Accumulate(ref sums[at], turn, area);
                    }
                }
            }

            int changed = 0;
            foreach (int at in chosen.Distinct())
            {
                double[] sum = sums[at];
                double length = sum == null
                    ? 0d
                    : Math.Sqrt((sum[0] * sum[0]) + (sum[1] * sum[1]) + (sum[2] * sum[2]) + (sum[3] * sum[3]));
                if (!(length > 0d))
                {
                    continue;
                }

                IPXVertex vertex = model.Vertex[at];
                V3 turned = Rotated(sum, length, vertex.Normal);
                changed += Vectors.Same(vertex.Normal, turned) ? 0 : 1;
                vertex.Normal = turned;
            }

            return ComposedEditResult.CompleteRewriting(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                },
                new[] { ElementKinds.Vertex });
        }

        private static double[] FaceTurn(IPXPmx based, IPXPmx model, int[] corners, out double area)
        {
            double basedArea;
            double[][] before = Frame(based, corners, out basedArea);
            double[][] now = Frame(model, corners, out area);
            if (before == null || now == null)
            {
                return null;
            }

            double[,] turn = new double[3, 3];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        turn[row, column] += now[axis][row] * before[axis][column];
                    }
                }
            }

            return Quaternion(turn);
        }

        private static double[][] Frame(IPXPmx model, int[] corners, out double area)
        {
            area = 0d;
            Vec first = Vec.Of(model.Vertex[corners[0]].Position);
            Vec edge = Vec.Of(model.Vertex[corners[1]].Position) - first;
            Vec other = Vec.Of(model.Vertex[corners[2]].Position) - first;
            Vec across = edge.Cross(other);
            double length = across.Length;
            if (!(length > 0d))
            {
                return null;
            }

            Vec along = edge * (1d / edge.Length);
            Vec normal = across * (1d / length);
            Vec side = normal.Cross(along);
            area = length / 2d;

            return new[]
            {
                new[] { along.X, along.Y, along.Z },
                new[] { side.X, side.Y, side.Z },
                new[] { normal.X, normal.Y, normal.Z },
            };
        }

        private static double[] Quaternion(double[,] r)
        {
            double trace = r[0, 0] + r[1, 1] + r[2, 2];
            if (trace > 0d)
            {
                double s = Math.Sqrt(trace + 1d) * 2d;

                return new[] { s / 4d, (r[2, 1] - r[1, 2]) / s, (r[0, 2] - r[2, 0]) / s, (r[1, 0] - r[0, 1]) / s };
            }

            if (r[0, 0] >= r[1, 1] && r[0, 0] >= r[2, 2])
            {
                double s = Math.Sqrt(1d + r[0, 0] - r[1, 1] - r[2, 2]) * 2d;

                return new[] { (r[2, 1] - r[1, 2]) / s, s / 4d, (r[0, 1] + r[1, 0]) / s, (r[0, 2] + r[2, 0]) / s };
            }

            if (r[1, 1] >= r[2, 2])
            {
                double s = Math.Sqrt(1d + r[1, 1] - r[0, 0] - r[2, 2]) * 2d;

                return new[] { (r[0, 2] - r[2, 0]) / s, (r[0, 1] + r[1, 0]) / s, s / 4d, (r[1, 2] + r[2, 1]) / s };
            }

            double last = Math.Sqrt(1d + r[2, 2] - r[0, 0] - r[1, 1]) * 2d;

            return new[] { (r[1, 0] - r[0, 1]) / last, (r[0, 2] + r[2, 0]) / last, (r[1, 2] + r[2, 1]) / last, last / 4d };
        }

        private static void Accumulate(ref double[] sum, double[] turn, double weight)
        {
            if (sum == null)
            {
                sum = new double[8];
                Array.Copy(turn, 0, sum, 4, 4);
            }

            double sign = (turn[0] * sum[4]) + (turn[1] * sum[5]) + (turn[2] * sum[6]) + (turn[3] * sum[7]) < 0d
                ? -1d
                : 1d;
            for (int at = 0; at < 4; at++)
            {
                sum[at] += sign * weight * turn[at];
            }
        }

        private static V3 Rotated(double[] sum, double length, V3 normal)
        {
            double w = sum[0] / length;
            double x = sum[1] / length;
            double y = sum[2] / length;
            double z = sum[3] / length;
            double cx = (y * normal.Z) - (z * normal.Y);
            double cy = (z * normal.X) - (x * normal.Z);
            double cz = (x * normal.Y) - (y * normal.X);
            double dx = (y * cz) - (z * cy);
            double dy = (z * cx) - (x * cz);
            double dz = (x * cy) - (y * cx);

            return new V3(
                (float)(normal.X + (2d * ((w * cx) + dx))),
                (float)(normal.Y + (2d * ((w * cy) + dy))),
                (float)(normal.Z + (2d * ((w * cz) + dz))));
        }

        private static bool TryTurn(
            McpMethodContext context, string operation, out RowMatrix turn, out string code, out string message)
        {
            turn = null;
            float angle;
            if (!ComposedInput.TryFloat(
                    context,
                    RotationAngleName,
                    operation,
                    new[] { Rotate },
                    ComposedInput.NoFloor,
                    ComposedInput.NoCeiling,
                    out angle,
                    out code,
                    out message))
            {
                return false;
            }

            if (!string.Equals(operation, Rotate, StringComparison.Ordinal))
            {
                if (!context.Params.ContainsKey(RotationAxisName))
                {
                    return true;
                }

                code = ToolEnvelope.InvalidArgument;
                message = RotationAxisName + " を渡せるのは " + Rotate + " のときだけである。";

                return false;
            }

            V3 axis;
            if (!ComposedInput.TryDirection(context, RotationAxisName, out axis, out code, out message))
            {
                return false;
            }

            turn = RowMatrix.AroundAxis(axis.X, axis.Y, axis.Z, angle * Math.PI / 180d);

            return true;
        }

        private static V3 Shared(IEnumerable<IPXVertex> picked)
        {
            return Vectors.NormalizedSum(picked.Select(vertex => vertex.Normal));
        }

        private static void Aim(IEnumerable<IPXVertex> picked, V3 direction)
        {
            foreach (IPXVertex vertex in picked)
            {
                vertex.Normal = new V3(direction.X, direction.Y, direction.Z);
            }
        }

        private static void FromTheFaces(IPXPmx model, IList<IPXVertex> picked)
        {
            Dictionary<IPXVertex, IList<V3>> facing =
                new Dictionary<IPXVertex, IList<V3>>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXVertex vertex in picked)
            {
                facing[vertex] = new List<V3>();
            }

            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    if (!ReferenceCleanup.IsSoundFace(face))
                    {
                        continue;
                    }

                    V3 made = Vectors.PerpendicularTo(
                        face.Vertex1.Position, face.Vertex2.Position, face.Vertex3.Position);
                    foreach (IPXVertex corner in
                        new[] { face.Vertex1, face.Vertex2, face.Vertex3 })
                    {
                        IList<V3> held;
                        if (facing.TryGetValue(corner, out held))
                        {
                            held.Add(made);
                        }
                    }
                }
            }

            foreach (IPXVertex vertex in picked)
            {
                V3 made = Vectors.NormalizedSum(facing[vertex]);
                if (Vectors.HasLength(made))
                {
                    vertex.Normal = made;
                }
            }
        }
    }
}
