using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class ModelEditVertices
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_vertices";

        /// <summary>指した頂点を1つへまとめ、面の参照を付け替える。</summary>
        public const string Weld = "weld";

        /// <summary>指した頂点のうち、しきい値より近いものどうしをまとめる。</summary>
        public const string WeldNear = "weldNear";

        /// <summary>指した頂点の、指した軸の値を、指した頂点の平均へそろえる。</summary>
        public const string Align = "align";

        /// <summary>指した頂点を、指した軸の鏡像として複製する。</summary>
        public const string MirrorCopy = "mirrorCopy";

        /// <summary>指した頂点を、指した軸の鏡像へ移す。</summary>
        public const string MirrorModel = "mirrorModel";

        public const string ProjectOntoSurface = "projectOntoSurface";

        public const string PushOutOfSurface = "pushOutOfSurface";

        public const string MarginName = "margin";

        public const string SpreadRadiusName = "spreadRadius";

        public const string RemainingName = "remaining";

        /// <summary>まとめる距離のしきい値を受け取る入力の名前。</summary>
        public const string ThresholdName = "threshold";

        /// <summary>軸を受け取る入力の名前。</summary>
        public const string AxisName = "axis";

        /// <summary>X軸。</summary>
        public const string AxisX = "x";

        /// <summary>Y軸。</summary>
        public const string AxisY = "y";

        /// <summary>Z軸。</summary>
        public const string AxisZ = "z";

        public const string OffsetName = "surfaceOffset";

        /// <summary>変えた頂点の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えた頂点の数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>3つの頂点が揃わなくなって落ちた面の数を返す項目の名前。</summary>
        public const string RemovedFacesName = "removedFaces";

        /// <summary>
        /// 足した頂点の位置を、先頭と件数の組で返す項目の名前。足した頂点は並びの末尾に連なる。
        /// 足していなければ件数は0で、先頭は頂点の数になる。
        /// </summary>
        public const string AddedName = "added";

        private const double Tolerance = 1e-9;

        private const double RoundingSlack = 1d / (1 << 22);

        private static readonly KeyValuePair<string, string[]>[] SurfaceInputs =
        {
            new KeyValuePair<string, string[]>(
                ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                new[] { ProjectOntoSurface, PushOutOfSurface }),
            new KeyValuePair<string, string[]>(OffsetName, new[] { ProjectOntoSurface }),
            new KeyValuePair<string, string[]>(
                ModelFindSurfaceDistances.DistanceLimitName, new[] { ProjectOntoSurface }),
            new KeyValuePair<string, string[]>(MarginName, new[] { PushOutOfSurface }),
            new KeyValuePair<string, string[]>(SpreadRadiusName, new[] { PushOutOfSurface }),
        };

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get { return new[] { Weld, WeldNear, Align, MirrorCopy, MirrorModel, ProjectOntoSurface, PushOutOfSurface }; }
        }

        /// <summary>受け取れる軸。スキーマが並べる順。</summary>
        public static IList<string> Axes
        {
            get { return new[] { AxisX, AxisY, AxisZ }; }
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
                ThresholdName,
                AxisName,
                ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                OffsetName,
                ModelFindSurfaceDistances.DistanceLimitName,
                MarginName,
                SpreadRadiusName,
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

            float threshold;
            string axis;
            if (!ComposedInput.TryFloat(
                    context,
                    ThresholdName,
                    operation,
                    new[] { WeldNear },
                    0f,
                    ComposedInput.NoCeiling,
                    out threshold,
                    out code,
                    out message)
                || !ComposedInput.TryChoice(
                    context,
                    AxisName,
                    operation,
                    new[] { Align, MirrorCopy, MirrorModel },
                    Axes,
                    out axis,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            KeyValuePair<string, string[]> unwanted = SurfaceInputs.FirstOrDefault(
                input => context.Params.ContainsKey(input.Key)
                    && !input.Value.Contains(operation, StringComparer.Ordinal));
            if (unwanted.Key != null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    unwanted.Key + " は " + string.Join("・", unwanted.Value) + " のときだけ渡せる。");
            }

            if (string.Equals(operation, PushOutOfSurface, StringComparison.Ordinal))
            {
                return PushedOut(context, model, picked);
            }

            if (string.Equals(operation, ProjectOntoSurface, StringComparison.Ordinal))
            {
                return Projected(context, model, picked);
            }

            switch (operation)
            {
                case Weld:
                    return Joined(model, new[] { picked });

                case WeldNear:
                    return Joined(model, VertexClusters.Near(picked, threshold));

                case Align:
                    return Aligned(model, picked, axis);

                case MirrorCopy:
                    return Copied(model, picked, axis);

                default:
                    return Mirrored(model, picked, axis);
            }
        }

        private static ComposedEditResult Joined(
            IPXPmx model, IEnumerable<IList<IPXVertex>> groups)
        {
            Dictionary<IPXVertex, IPXVertex> moved =
                new Dictionary<IPXVertex, IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IList<IPXVertex> group in groups)
            {
                foreach (IPXVertex dropped in group.Skip(1))
                {
                    moved[dropped] = group[0];
                }
            }

            ReferenceCleanup.Repoint(model, moved);
            foreach (IPXVertex dropped in moved.Keys)
            {
                model.Vertex.Remove(dropped);
            }

            int faces = ReferenceCleanup.DropUnsoundFaces(model);

            return Answer(moved.Count, moved.Count, faces, None(model));
        }

        private static ComposedEditResult Projected(
            McpMethodContext context, IPXPmx model, IList<IPXVertex> picked)
        {
            string code;
            string message;
            List<int> surface;
            float offset = 0f;
            float? limit;
            if (!ModelFindSurfaceDistances.TryMaterials(
                    context,
                    model,
                    ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                    out surface,
                    out code,
                    out message)
                || (context.Params.ContainsKey(OffsetName)
                    && !ComposedInput.TryFloat(
                        context,
                        OffsetName,
                        ProjectOntoSurface,
                        new[] { ProjectOntoSurface },
                        ComposedInput.NoFloor,
                        ComposedInput.NoCeiling,
                        out offset,
                        out code,
                        out message))
                || !ModelFindSurfaceDistances.TryLimit(context, out limit, out message))
            {
                return ComposedEditResult.Refuse(code ?? ToolEnvelope.InvalidArgument, message);
            }

            SurfaceGeometry.SurfaceTree tree =
                SurfaceGeometry.SurfaceTree.Of(model, surface);
            if (tree == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    ModelFindSurfaceDistances.SurfaceMaterialIndicesName
                        + " の材質に面積のある面が1つも無い。");
            }

            double reach = limit.HasValue ? limit.Value : double.PositiveInfinity;
            List<KeyValuePair<IPXVertex, V3>> moves = new List<KeyValuePair<IPXVertex, V3>>();
            foreach (IPXVertex vertex in picked)
            {
                SurfaceGeometry.Hit hit = tree.Nearest(
                    SurfaceGeometry.Vec.Of(vertex.Position), reach);
                if (hit == null)
                {
                    continue;
                }

                SurfaceGeometry.Vec moved = hit.Point + (hit.Front * offset);
                moves.Add(new KeyValuePair<IPXVertex, V3>(
                    vertex, new V3((float)moved.X, (float)moved.Y, (float)moved.Z)));
            }

            foreach (KeyValuePair<IPXVertex, V3> move in moves)
            {
                move.Key.Position = move.Value;
            }

            return Answer(moves.Count, 0, 0, None(model), new[] { ElementKinds.Vertex });
        }

        private static ComposedEditResult PushedOut(
            McpMethodContext context, IPXPmx model, IList<IPXVertex> picked)
        {
            string code;
            string message;
            List<int> surface;
            float margin = 0f;
            float spreadRadius = 0f;
            if (!ModelFindSurfaceDistances.TryMaterials(
                    context,
                    model,
                    ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                    out surface,
                    out code,
                    out message)
                || (context.Params.ContainsKey(MarginName)
                    && !ComposedInput.TryFloat(
                        context,
                        MarginName,
                        PushOutOfSurface,
                        new[] { PushOutOfSurface },
                        0f,
                        ComposedInput.NoCeiling,
                        out margin,
                        out code,
                        out message))
                || (context.Params.ContainsKey(SpreadRadiusName)
                    && !ComposedInput.TryFloat(
                        context,
                        SpreadRadiusName,
                        PushOutOfSurface,
                        new[] { PushOutOfSurface },
                        0f,
                        ComposedInput.NoCeiling,
                        out spreadRadius,
                        out code,
                        out message)))
            {
                return ComposedEditResult.Refuse(code ?? ToolEnvelope.InvalidArgument, message);
            }

            SurfaceGeometry.SurfaceTree tree =
                SurfaceGeometry.SurfaceTree.Of(model, surface);
            if (tree == null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    ModelFindSurfaceDistances.SurfaceMaterialIndicesName
                        + " の材質に面積のある面が1つも無い。");
            }

            IList<IPXVertex> each = picked.Distinct(ReferenceComparer<IPXVertex>.Instance).ToList();
            SurfaceGeometry.Vec[] spots = each
                .Select(vertex => SurfaceGeometry.Vec.Of(vertex.Position))
                .ToArray();
            SurfaceGeometry.Vec[] away = new SurfaceGeometry.Vec[each.Count];
            double[] need = new double[each.Count];
            bool[] pushing = new bool[each.Count];
            bool[] fixedAt = new bool[each.Count];
            for (int at = 0; at < each.Count; at++)
            {
                SurfaceGeometry.Hit hit = tree.Nearest(spots[at], double.PositiveInfinity);
                if (hit == null)
                {
                    fixedAt[at] = true;
                    continue;
                }

                SurfaceGeometry.Vec toward = hit.Point - spots[at];
                double apart = toward.Length;
                if (apart <= Tolerance)
                {
                    fixedAt[at] = true;
                }
                else if (tree.IsInside(spots[at]))
                {
                    away[at] = toward * (1d / apart);
                    need[at] = apart + margin;
                    pushing[at] = true;
                }
                else if (apart < margin - Tolerance)
                {
                    away[at] = toward * (-1d / apart);
                    need[at] = margin - apart;
                    pushing[at] = true;
                }
            }

            SurfaceGeometry.Vec[] moves = new SurfaceGeometry.Vec[each.Count];
            if (spreadRadius > 0f)
            {
                double reach = spreadRadius;
                Func<double, double> window = gap =>
                    Math.Max(0d, (Math.Exp(-2d * gap * gap / (reach * reach)) - Math.Exp(-2d)) / (1d - Math.Exp(-2d)));
                double[] total = new double[each.Count];
                double[] others = new double[each.Count];
                SurfaceGeometry.Vec[] pull = new SurfaceGeometry.Vec[each.Count];
                for (int at = 0; at < each.Count; at++)
                {
                    if (fixedAt[at])
                    {
                        continue;
                    }

                    total[at] = 1d;
                    pull[at] = pushing[at] ? away[at] * need[at] : default(SurfaceGeometry.Vec);
                }

                SurfaceGeometry.ForEachNear(
                    spots,
                    spreadRadius,
                    (at, other) =>
                    {
                        if (fixedAt[at] || fixedAt[other])
                        {
                            return;
                        }

                        double weight = window((spots[at] - spots[other]).Length);
                        total[at] += weight;
                        if (pushing[other])
                        {
                            pull[at] = pull[at] + (away[other] * need[other] * weight);
                            others[at] += weight * need[other] * away[other].Dot(away[at]);
                        }
                    });

                double[] own = new double[each.Count];
                for (int at = 0; at < each.Count; at++)
                {
                    own[at] = pushing[at] && others[at] >= 0d
                        ? need[at] * total[at] / (need[at] + others[at])
                        : 0d;
                }

                double[] scale = own.ToArray();
                SurfaceGeometry.ForEachNear(
                    spots,
                    spreadRadius,
                    (at, other) => scale[at] = Math.Max(scale[at], own[other] * window((spots[at] - spots[other]).Length)));
                for (int at = 0; at < each.Count; at++)
                {
                    moves[at] = fixedAt[at] ? default(SurfaceGeometry.Vec) : pull[at] * (scale[at] / total[at]);
                }
            }
            else
            {
                for (int at = 0; at < each.Count; at++)
                {
                    moves[at] = away[at] * need[at];
                }
            }

            int changed = 0;
            int remaining = 0;
            for (int at = 0; at < each.Count; at++)
            {
                SurfaceGeometry.Vec moved = spots[at] + moves[at];
                V3 was = each[at].Position;
                V3 now = new V3((float)moved.X, (float)moved.Y, (float)moved.Z);
                if (now.X != was.X || now.Y != was.Y || now.Z != was.Z)
                {
                    each[at].Position = now;
                    changed++;
                }

                remaining += Cleared(tree, SurfaceGeometry.Vec.Of(now), margin) ? 0 : 1;
            }

            return Answer(changed, 0, 0, None(model), new[] { ElementKinds.Vertex }, remaining);
        }

        private static bool Cleared(SurfaceGeometry.SurfaceTree tree, SurfaceGeometry.Vec at, double margin)
        {
            SurfaceGeometry.Hit hit = tree.Nearest(at, double.PositiveInfinity);
            if (hit == null)
            {
                return true;
            }

            double slack = RoundingSlack * Math.Max(1d, Math.Max(Math.Abs(at.X), Math.Max(Math.Abs(at.Y), Math.Abs(at.Z))));
            double apart = (hit.Point - at).Length;
            if (apart <= slack)
            {
                return !(margin > 0d);
            }

            return !tree.IsInside(at) && apart >= margin - slack;
        }

        private static ComposedEditResult Aligned(
            IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            if (picked.Count == 0)
            {
                return Answer(0, 0, 0, None(model));
            }

            float middle = picked.Select(v => Component(v.Position, axis)).Sum() / picked.Count;
            foreach (IPXVertex vertex in picked)
            {
                vertex.Position = Written(vertex.Position, axis, middle);
            }

            return Answer(picked.Count, 0, 0, None(model), new[] { ElementKinds.Vertex });
        }

        private static ComposedEditResult Copied(
            IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            int start = model.Vertex.Count;
            foreach (IPXVertex vertex in picked)
            {
                IPXVertex made = (IPXVertex)vertex.Clone();
                Flip(made, axis);
                model.Vertex.Add(made);
            }

            return Answer(picked.Count, 0, 0, PositionRuns.Of(start, picked.Count));
        }

        private static ComposedEditResult Mirrored(
            IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            foreach (IPXVertex vertex in picked)
            {
                Flip(vertex, axis);
            }

            HashSet<IPXVertex> flipped =
                new HashSet<IPXVertex>(picked, ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    if (!flipped.Contains(face.Vertex1)
                        || !flipped.Contains(face.Vertex2)
                        || !flipped.Contains(face.Vertex3))
                    {
                        continue;
                    }

                    IPXVertex second = face.Vertex2;
                    face.Vertex2 = face.Vertex3;
                    face.Vertex3 = second;
                }
            }

            return Answer(picked.Count, 0, 0, None(model));
        }

        private static void Flip(IPXVertex vertex, string axis)
        {
            vertex.Position = Written(
                vertex.Position, axis, -Component(vertex.Position, axis));
            vertex.Normal = Written(vertex.Normal, axis, -Component(vertex.Normal, axis));
        }

        private static float Component(V3 given, string axis)
        {
            switch (axis)
            {
                case AxisX:
                    return given.X;

                case AxisY:
                    return given.Y;

                default:
                    return given.Z;
            }
        }

        private static V3 Written(V3 given, string axis, float value)
        {
            switch (axis)
            {
                case AxisX:
                    return new V3(value, given.Y, given.Z);

                case AxisY:
                    return new V3(given.X, value, given.Z);

                default:
                    return new V3(given.X, given.Y, value);
            }
        }

        /// <summary>
        /// 結末を作る。<paramref name="rewritten"/> は並びを変えずに中身だけを書き換えた種類で、
        /// 並びを変えうる操作では null。
        /// </summary>
        private static ComposedEditResult Answer(
            int changed,
            int removed,
            int faces,
            IDictionary<string, object> added,
            IList<string> rewritten = null,
            int? remaining = null)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ChangedName, changed },
                { RemovedName, removed },
                { RemovedFacesName, faces },
                { AddedName, added },
            };
            if (remaining.HasValue)
            {
                value[RemainingName] = remaining.Value;
            }

            return rewritten == null
                ? ComposedEditResult.Complete(value)
                : ComposedEditResult.CompleteRewriting(value, rewritten);
        }

        private static IDictionary<string, object> None(IPXPmx model)
        {
            return PositionRuns.Of(model.Vertex.Count, 0);
        }

    }
}
