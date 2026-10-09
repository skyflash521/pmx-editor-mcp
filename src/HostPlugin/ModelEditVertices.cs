using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

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

        public const string FollowGuide = "followGuide";

        public const string MirrorDisplacement = "mirrorDisplacement";

        public const string SweepBand = "sweepBand";

        public const string LateralScale = "lateralScale";

        public const string CopyFromBase = "copyFromBase";

        public const string GuideIndicesName = "guideIndices";

        public const string RootIndicesName = "rootIndices";

        public const string LayerToleranceName = "layerTolerance";

        public const string OffsetsName = "offsets";

        public const string FixedEndsName = "fixedEnds";

        public const string OffsetDistanceName = "t";

        public const string OffsetMoveName = "move";

        public const string FromName = "from";

        public const string ToName = "to";

        public const string AcrossName = "across";

        public const string DirectionName = "direction";

        public const string KnotsName = "knots";

        public const string KnotPositionName = "s";

        public const string KnotScaleName = "scale";

        public const string ModeName = "mode";

        public const string RigidMode = "rigid";

        public const string InterpolateMode = "interpolate";

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

        private static readonly KeyValuePair<string, string[]>[] ScopedInputs =
        {
            new KeyValuePair<string, string[]>(
                ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                new[] { ProjectOntoSurface, PushOutOfSurface }),
            new KeyValuePair<string, string[]>(OffsetName, new[] { ProjectOntoSurface }),
            new KeyValuePair<string, string[]>(DirectionName, new[] { ProjectOntoSurface }),
            new KeyValuePair<string, string[]>(
                ModelFindSurfaceDistances.DistanceLimitName, new[] { ProjectOntoSurface }),
            new KeyValuePair<string, string[]>(MarginName, new[] { PushOutOfSurface }),
            new KeyValuePair<string, string[]>(SpreadRadiusName, new[] { PushOutOfSurface }),
            new KeyValuePair<string, string[]>(GuideIndicesName, new[] { FollowGuide }),
            new KeyValuePair<string, string[]>(
                ModelMorphFromMoved.BasePmxHandleName,
                new[] { FollowGuide, MirrorDisplacement, CopyFromBase }),
            new KeyValuePair<string, string[]>(RootIndicesName, new[] { SweepBand }),
            new KeyValuePair<string, string[]>(OffsetsName, new[] { SweepBand }),
            new KeyValuePair<string, string[]>(FromName, new[] { LateralScale }),
            new KeyValuePair<string, string[]>(ToName, new[] { LateralScale }),
            new KeyValuePair<string, string[]>(AcrossName, new[] { LateralScale }),
            new KeyValuePair<string, string[]>(KnotsName, new[] { LateralScale }),
        };

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get { return new[] { Weld, WeldNear, Align, MirrorCopy, MirrorModel, ProjectOntoSurface, PushOutOfSurface, FollowGuide, MirrorDisplacement, SweepBand, LateralScale, CopyFromBase }; }
        }

        public static IList<string> Modes
        {
            get { return new[] { RigidMode, InterpolateMode }; }
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
                DirectionName,
                ModelFindSurfaceDistances.DistanceLimitName,
                MarginName,
                SpreadRadiusName,
                GuideIndicesName,
                ModeName,
                ModelMorphFromMoved.BasePmxHandleName,
                RootIndicesName,
                LayerToleranceName,
                OffsetsName,
                FixedEndsName,
                FromName,
                ToName,
                AcrossName,
                KnotsName,
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
            float tolerance;
            int fixedEnds = 0;
            string axis;
            string mode;
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
                || !ComposedInput.TryFloat(
                    context,
                    LayerToleranceName,
                    operation,
                    new[] { SweepBand },
                    0f,
                    ComposedInput.NoCeiling,
                    out tolerance,
                    out code,
                    out message)
                || (context.Params.ContainsKey(FixedEndsName)
                    && !ComposedInput.TryCount(
                        context,
                        FixedEndsName,
                        operation,
                        new[] { SweepBand },
                        0,
                        out fixedEnds,
                        out code,
                        out message))
                || !ComposedInput.TryChoice(
                    context,
                    AxisName,
                    operation,
                    new[] { Align, MirrorCopy, MirrorModel, MirrorDisplacement },
                    Axes,
                    out axis,
                    out code,
                    out message)
                || !ComposedInput.TryChoice(
                    context,
                    ModeName,
                    operation,
                    new[] { FollowGuide },
                    Modes,
                    out mode,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            KeyValuePair<string, string[]> unwanted = ScopedInputs.FirstOrDefault(
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

            if (string.Equals(operation, FollowGuide, StringComparison.Ordinal))
            {
                return Followed(context, model, chosen, mode);
            }

            if (string.Equals(operation, MirrorDisplacement, StringComparison.Ordinal))
            {
                return Displaced(context, model, chosen, axis);
            }

            if (string.Equals(operation, CopyFromBase, StringComparison.Ordinal))
            {
                return Copied(context, model, chosen);
            }

            if (string.Equals(operation, SweepBand, StringComparison.Ordinal))
            {
                return Swept(context, model, chosen, tolerance, fixedEnds);
            }

            if (string.Equals(operation, LateralScale, StringComparison.Ordinal))
            {
                return ScaledAcross(context, model, chosen);
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
            V3 direction = null;
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
                || !ModelFindSurfaceDistances.TryLimit(context, out limit, out message)
                || (context.Params.ContainsKey(DirectionName)
                    && !ComposedInput.TryDirection(
                        context, DirectionName, out direction, out code, out message)))
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
                SurfaceGeometry.Vec at = SurfaceGeometry.Vec.Of(vertex.Position);
                SurfaceGeometry.Hit hit = direction == null
                    ? tree.Nearest(at, reach)
                    : tree.Nearest(at, 0) ?? tree.Cast(at, SurfaceGeometry.Vec.Of(direction));
                if (hit == null || (direction != null && hit.Signed > reach))
                {
                    continue;
                }

                SurfaceGeometry.Vec moved = hit.Point + (hit.Front * offset);
                moves.Add(new KeyValuePair<IPXVertex, V3>(
                    vertex, new V3((float)moved.X, (float)moved.Y, (float)moved.Z)));
            }

            int changed = 0;
            foreach (KeyValuePair<IPXVertex, V3> move in moves)
            {
                V3 position = move.Key.Position;
                V3 center = move.Key.SDEF_C;
                move.Key.Position = move.Value;
                VertexWeights.ProjectSdefCenter(move.Key);
                if (!Vectors.Same(position, move.Key.Position) || !Vectors.Same(center, move.Key.SDEF_C))
                {
                    changed++;
                }
            }

            return Answer(changed, 0, 0, None(model), new[] { ElementKinds.Vertex });
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
                    VertexWeights.ProjectSdefCenter(each[at]);
                    changed++;
                }

                remaining += Cleared(tree, SurfaceGeometry.Vec.Of(now), margin) ? 0 : 1;
            }

            return Answer(changed, 0, 0, None(model), new[] { ElementKinds.Vertex }, remaining);
        }

        private static ComposedEditResult Followed(
            McpMethodContext context, IPXPmx model, IList<int> chosen, string mode)
        {
            IPXPmx based;
            string code;
            string message;
            List<int> guides;
            if (!ModelMorphFromMoved.TryBase(context, out based, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            string differs = ModelCompareShape.Differs(based, model);
            if (differs != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, differs);
            }

            if (!ModelFindSurfaceDistances.TryPositions(
                context, GuideIndicesName, "頂点", model.Vertex.Count, out guides, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            guides = guides.Distinct().ToList();
            int[] each = chosen.Distinct().ToArray();
            if (guides.Intersect(each).Any())
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    GuideIndicesName + " の頂点は、動かす頂点に含められない。");
            }

            Vec[] before = ModelCompareShape.Positions(based);
            Vec[] now = ModelCompareShape.Positions(model);
            Func<int, Vec> placed;
            if (string.Equals(mode, RigidMode, StringComparison.Ordinal))
            {
                RigidFit fit = RigidFit.Of(
                    guides.Select(at => before[at]).ToList(), guides.Select(at => now[at]).ToList());
                if (fit == null)
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument,
                        GuideIndicesName + " は、複製でも今でも一直線に並ばない3つ以上の頂点を指さなければならない。");
                }

                placed = at => fit.Point(before[at]);
            }
            else
            {
                placed = at => before[at] + Pulled(before[at], guides, before, now);
            }

            int changed = 0;
            foreach (int at in each)
            {
                Vec moved = placed(at);
                V3 was = model.Vertex[at].Position;
                V3 made = new V3((float)moved.X, (float)moved.Y, (float)moved.Z);
                if (made.X != was.X || made.Y != was.Y || made.Z != was.Z)
                {
                    model.Vertex[at].Position = made;
                    VertexWeights.ProjectSdefCenter(model.Vertex[at]);
                    changed++;
                }
            }

            return Answer(changed, 0, 0, None(model), new[] { ElementKinds.Vertex });
        }

        private static ComposedEditResult Copied(
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

            int changed = 0;
            foreach (int at in chosen.Distinct())
            {
                IPXVertex source = based.Vertex[at];
                IPXVertex vertex = model.Vertex[at];
                if (Vectors.Same(vertex.Position, source.Position)
                    && Vectors.Same(vertex.Normal, source.Normal)
                    && Vectors.Same(vertex.SDEF_C, source.SDEF_C)
                    && Vectors.Same(vertex.SDEF_R0, source.SDEF_R0)
                    && Vectors.Same(vertex.SDEF_R1, source.SDEF_R1))
                {
                    continue;
                }

                vertex.Position = Vectors.Copied(source.Position);
                vertex.Normal = Vectors.Copied(source.Normal);
                vertex.SDEF_C = Vectors.Copied(source.SDEF_C);
                vertex.SDEF_R0 = Vectors.Copied(source.SDEF_R0);
                vertex.SDEF_R1 = Vectors.Copied(source.SDEF_R1);
                changed++;
            }

            return Answer(changed, 0, 0, None(model), new[] { ElementKinds.Vertex });
        }

        private static ComposedEditResult Displaced(
            McpMethodContext context, IPXPmx model, IList<int> chosen, string axis)
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

            int along = MirrorPartners.AxisIndex(axis);
            Vec[] before = ModelCompareShape.Positions(based);
            Vec[] now = ModelCompareShape.Positions(model);
            IDictionary<int, int> partners = MirrorPartners.OfVertices(based, along, chosen);
            Dictionary<int, KeyValuePair<V3, V3>> made = new Dictionary<int, KeyValuePair<V3, V3>>();
            foreach (int at in chosen.Distinct())
            {
                int partner;
                if (!partners.TryGetValue(at, out partner))
                {
                    continue;
                }

                Vec moved = now[at] - before[at];
                V3 given = model.Vertex[at].Normal;
                if (partner != at)
                {
                    made[partner] = new KeyValuePair<V3, V3>(
                        (now[partner] + MirrorPartners.Flip(moved, along)).ToV3(),
                        MirrorPartners.Flip(Vec.Of(given), along).ToV3());
                    continue;
                }

                Vec level = MirrorPartners.Level(Vec.Of(given), along);
                made[at] = new KeyValuePair<V3, V3>(
                    (before[at] + MirrorPartners.Level(moved, along)).ToV3(),
                    level.Length > 0d ? (level * (1d / level.Length)).ToV3() : given);
            }

            int changed = 0;
            foreach (KeyValuePair<int, KeyValuePair<V3, V3>> each in made)
            {
                IPXVertex vertex = model.Vertex[each.Key];
                if (Vectors.Same(vertex.Position, each.Value.Key)
                    && Vectors.Same(vertex.Normal, each.Value.Value))
                {
                    continue;
                }

                vertex.Position = each.Value.Key;
                vertex.Normal = each.Value.Value;
                VertexWeights.ProjectSdefCenter(vertex);
                changed++;
            }

            return Answer(changed, 0, 0, None(model), new[] { ElementKinds.Vertex });
        }

        private static ComposedEditResult Swept(
            McpMethodContext context,
            IPXPmx model,
            IList<int> chosen,
            double tolerance,
            int fixedEnds)
        {
            string code;
            string message;
            List<int> roots;
            IList<KeyValuePair<double, Vec>> offsets;
            if (!ModelFindSurfaceDistances.TryPositions(
                    context, RootIndicesName, "頂点", model.Vertex.Count, out roots, out code, out message)
                || !TryOffsets(context, out offsets, out message))
            {
                return ComposedEditResult.Refuse(code ?? ToolEnvelope.InvalidArgument, message);
            }

            int[] band = chosen.Distinct().ToArray();
            roots = roots.Distinct().ToList();
            if (roots.Except(band).Any())
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    RootIndicesName + " の頂点は、" + TargetNames.Element.Indices + " に含まれなければならない。");
            }

            Vec[] spots = ModelCompareShape.Positions(model);
            double[] reach = BandSweep.Distances(
                band,
                roots,
                SurfaceGeometry.Neighbours(model, Enumerable.Range(0, model.Material.Count)),
                spots);
            if (band.Any(at => double.IsInfinity(reach[at])))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    TargetNames.Element.Indices + " の頂点に、面の辺で " + RootIndicesName
                        + " からたどり着けないものがある。");
            }

            int changed = 0;
            foreach (KeyValuePair<int, Vec> each in BandSweep.Place(
                BandSweep.Layers(band, reach, tolerance), reach, spots, offsets, fixedEnds))
            {
                V3 was = model.Vertex[each.Key].Position;
                V3 made = each.Value.ToV3();
                if (made.X != was.X || made.Y != was.Y || made.Z != was.Z)
                {
                    model.Vertex[each.Key].Position = made;
                    VertexWeights.ProjectSdefCenter(model.Vertex[each.Key]);
                    changed++;
                }
            }

            return Answer(changed, 0, 0, None(model), new[] { ElementKinds.Vertex });
        }

        private static ComposedEditResult ScaledAcross(
            McpMethodContext context,
            IPXPmx model,
            IList<int> chosen)
        {
            V3 from;
            V3 to;
            V3 across;
            IList<KeyValuePair<double, double>> knots;
            LateralScaling scaling;
            string code;
            string message;
            if (!ComposedInput.TrySpot(context, FromName, out from, out code, out message)
                || !ComposedInput.TrySpot(context, ToName, out to, out code, out message)
                || !ComposedInput.TrySpot(context, AcrossName, out across, out code, out message)
                || !TryKnots(context, out knots, out message)
                || !LateralScaling.TryCreate(
                    Vec.Of(from), Vec.Of(to), Vec.Of(across), knots, out scaling, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            Dictionary<int, V3> made = new Dictionary<int, V3>();
            foreach (int at in chosen.Distinct())
            {
                V3 was = model.Vertex[at].Position;
                V3 moved = scaling.Scaled(Vec.Of(was)).ToV3();
                if (float.IsNaN(moved.X) || float.IsInfinity(moved.X)
                    || float.IsNaN(moved.Y) || float.IsInfinity(moved.Y)
                    || float.IsNaN(moved.Z) || float.IsInfinity(moved.Z))
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.InvalidArgument,
                        KnotsName + " の倍率を掛けると、単精度で持てない位置になる頂点がある。");
                }

                if (moved.X != was.X || moved.Y != was.Y || moved.Z != was.Z)
                {
                    made[at] = moved;
                }
            }

            foreach (KeyValuePair<int, V3> each in made)
            {
                model.Vertex[each.Key].Position = each.Value;
                VertexWeights.ProjectSdefCenter(model.Vertex[each.Key]);
            }

            return Answer(made.Count, 0, 0, None(model), new[] { ElementKinds.Vertex });
        }

        private static bool TryKnots(
            McpMethodContext context,
            out IList<KeyValuePair<double, double>> knots,
            out string message)
        {
            knots = null;
            message = KnotsName + " は、" + KnotPositionName + "(位置)の昇順に、"
                + KnotScaleName + "(有限の数の倍率)との組を1つ以上並べたものでなければならない。";
            object given;
            context.Params.TryGetValue(KnotsName, out given);
            object[] items = given as object[];
            if (items == null || items.Length == 0)
            {
                return false;
            }

            List<KeyValuePair<double, double>> made = new List<KeyValuePair<double, double>>();
            foreach (object item in items)
            {
                IDictionary<string, object> held = item as IDictionary<string, object>;
                object position;
                object scale;
                float along;
                float by;
                if (held == null
                    || !held.TryGetValue(KnotPositionName, out position)
                    || !held.TryGetValue(KnotScaleName, out scale)
                    || !ValueInput.TrySingle(position, out along)
                    || !ValueInput.TrySingle(scale, out by)
                    || (made.Count > 0 && !(along > made[made.Count - 1].Key)))
                {
                    return false;
                }

                made.Add(new KeyValuePair<double, double>(along, by));
            }

            message = null;
            knots = made;

            return true;
        }

        private static bool TryOffsets(
            McpMethodContext context,
            out IList<KeyValuePair<double, Vec>> offsets,
            out string message)
        {
            offsets = null;
            message = OffsetsName + " は、" + OffsetDistanceName + "(網目に沿った距離)の昇順に、"
                + OffsetMoveName + "(3つの有限の数の並び)との組を1つ以上並べたものでなければならない。";
            object given;
            context.Params.TryGetValue(OffsetsName, out given);
            object[] items = given as object[];
            if (items == null || items.Length == 0)
            {
                return false;
            }

            List<KeyValuePair<double, Vec>> made = new List<KeyValuePair<double, Vec>>();
            foreach (object item in items)
            {
                IDictionary<string, object> held = item as IDictionary<string, object>;
                object distance;
                object move;
                float along;
                object[] parts;
                if (held == null
                    || !held.TryGetValue(OffsetDistanceName, out distance)
                    || !held.TryGetValue(OffsetMoveName, out move)
                    || !ValueInput.TrySingle(distance, out along)
                    || (parts = move as object[]) == null
                    || parts.Length != 3
                    || (made.Count > 0 && !(along > made[made.Count - 1].Key)))
                {
                    return false;
                }

                float[] axes = new float[parts.Length];
                for (int at = 0; at < parts.Length; at++)
                {
                    if (!ValueInput.TrySingle(parts[at], out axes[at]))
                    {
                        return false;
                    }
                }

                made.Add(new KeyValuePair<double, Vec>(along, new Vec(axes[0], axes[1], axes[2])));
            }

            message = null;
            offsets = made;

            return true;
        }

        private static Vec Pulled(Vec at, IList<int> guides, Vec[] before, Vec[] now)
        {
            Vec weighted = default(Vec);
            Vec coincident = default(Vec);
            double total = 0d;
            int onGuide = 0;
            foreach (int guide in guides)
            {
                Vec gap = at - before[guide];
                double squared = gap.Dot(gap);
                Vec shift = now[guide] - before[guide];
                if (squared == 0d)
                {
                    coincident = coincident + shift;
                    onGuide++;
                    continue;
                }

                weighted = weighted + (shift * (1d / squared));
                total += 1d / squared;
            }

            return onGuide > 0 ? coincident * (1d / onGuide) : weighted * (1d / total);
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
            int changed = 0;
            foreach (IPXVertex vertex in picked)
            {
                V3 placed = Written(vertex.Position, axis, middle);
                if (!Vectors.Same(vertex.Position, placed))
                {
                    vertex.Position = placed;
                    VertexWeights.ProjectSdefCenter(vertex);
                    changed++;
                }
            }

            return Answer(changed, 0, 0, None(model), new[] { ElementKinds.Vertex });
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
            int changed = 0;
            foreach (IPXVertex vertex in picked)
            {
                V3 position = vertex.Position;
                V3 normal = vertex.Normal;
                V3 center = vertex.SDEF_C;
                Flip(vertex, axis);
                if (!Vectors.Same(position, vertex.Position) || !Vectors.Same(normal, vertex.Normal)
                    || !Vectors.Same(center, vertex.SDEF_C))
                {
                    changed++;
                }
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

            return Answer(changed, 0, 0, None(model));
        }

        private static void Flip(IPXVertex vertex, string axis)
        {
            vertex.Position = Written(
                vertex.Position, axis, -Component(vertex.Position, axis));
            vertex.Normal = Written(vertex.Normal, axis, -Component(vertex.Normal, axis));
            VertexWeights.ProjectSdefCenter(vertex);
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
