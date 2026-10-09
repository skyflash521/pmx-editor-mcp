using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class ModelEditWeights
    {
        public const string ToolName = "model_edit_weights";

        public const string Average = "average";

        public const string Smooth = "smooth";

        public const string FromNearestBonePosition = "fromNearestBonePosition";

        public const string FromNearestBoneAxis = "fromNearestBoneAxis";

        public const string FromMirror = "fromMirror";

        public const string Normalize = "normalize";

        public const string RepairMissingBone = "repairMissingBone";

        public const string ReplaceBone = "replaceBone";

        public const string FromSurface = "fromSurface";

        public const string SmoothSpatial = "smoothSpatial";

        public const string AverageNear = "averageNear";

        public const string RampToward = "rampToward";

        public const string TargetIndicesName = "targetIndices";

        public const string FromBoneName = "fromBone";

        public const string ToBoneName = "toBone";

        public const string StrengthName = "strength";

        public const string AxisName = "axis";

        public const string ProjectionName = "projection";

        public const string ProjectionNearest = "nearest";

        public const string ProjectionRay = "ray";

        public const string ExcludeBonesName = "excludeBones";

        public const string MaxBonesName = "maxBones";

        public const string MinWeightName = "minWeight";

        public const string FalloffName = "falloff";

        public const string FalloffNearName = "near";

        public const string FalloffFadeName = "fade";

        public const string SourceSmoothRadiusName = "sourceSmoothRadius";

        public const string FalloffSmoothRadiusName = "falloffSmoothRadius";

        public const string RadiusName = "radius";

        public const string IterationsName = "iterations";

        public const string ThresholdName = "threshold";

        private const int DefaultMaxBones = 4;

        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    Average,
                    Smooth,
                    FromNearestBonePosition,
                    FromNearestBoneAxis,
                    FromMirror,
                    Normalize,
                    RepairMissingBone,
                    ReplaceBone,
                    FromSurface,
                    SmoothSpatial,
                    AverageNear,
                    RampToward,
                };
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
                StrengthName,
                AxisName,
                FromBoneName,
                ToBoneName,
                ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                ProjectionName,
                ExcludeBonesName,
                MaxBonesName,
                MinWeightName,
                FalloffName,
                SourceSmoothRadiusName,
                FalloffSmoothRadiusName,
                RadiusName,
                IterationsName,
                ThresholdName,
                TargetIndicesName,
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

            float strength;
            string axis;
            if (!ComposedInput.TryFloat(
                    context,
                    StrengthName,
                    operation,
                    new[] { Smooth },
                    0f,
                    1f,
                    out strength,
                    out code,
                    out message)
                || !ComposedInput.TryChoice(
                    context,
                    AxisName,
                    operation,
                    new[] { FromMirror },
                    ModelEditVertices.Axes,
                    out axis,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int fromBone;
            int toBone;
            if (!ComposedInput.TryPosition(
                    context,
                    FromBoneName,
                    operation,
                    new[] { ReplaceBone },
                    model.Bone.Count,
                    out fromBone,
                    out code,
                    out message)
                || !ComposedInput.TryPosition(
                    context,
                    ToBoneName,
                    operation,
                    new[] { ReplaceBone },
                    model.Bone.Count,
                    out toBone,
                    out code,
                    out message)
                || !TryBones(operation, fromBone, toBone, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            float radius;
            int iterations;
            float threshold;
            if (!TrySpatial(context, operation, out radius, out iterations, out threshold, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<int> targets;
            if (!ComposedInput.TryIndices(
                    context,
                    TargetIndicesName,
                    operation,
                    new[] { RampToward },
                    model.Vertex.Count,
                    out targets,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            SurfaceInput surface;
            if (!TrySurface(context, model, operation, out surface, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IList<IPXVertex> picked = chosen.Select(at => model.Vertex[at]).ToList();
            if (string.Equals(operation, ReplaceBone, StringComparison.Ordinal))
            {
                picked = Holding(picked, model.Bone[fromBone]);
            }

            IList<KeyValuePair<IPXVertex, IList<KeyValuePair<IPXBone, float>>>> ramp = null;
            if (string.Equals(operation, RampToward, StringComparison.Ordinal))
            {
                ramp = RampPlan(picked, targets.Select(at => model.Vertex[at]).ToList(), radius);
                picked = ramp.Select(one => one.Key).ToList();
            }

            IList<IList<KeyValuePair<IPXBone, float>>> before =
                picked.Select(VertexWeights.All).ToList();
            IList<V3[]> sdefBefore = picked.Select(Sdef).ToList();
            switch (operation)
            {
                case Average:
                    Aim(picked, Averaged(picked));
                    break;

                case Smooth:
                    Smoothed(model, picked, strength);
                    break;

                case FromNearestBonePosition:
                    Nearest(model, picked, false);
                    break;

                case FromNearestBoneAxis:
                    Nearest(model, picked, true);
                    break;

                case FromMirror:
                    Mirrored(model, picked, axis);
                    break;

                case ReplaceBone:
                    Replaced(picked, model.Bone[fromBone], model.Bone[toBone]);
                    break;

                case FromSurface:
                    Surfaced(picked, surface);
                    break;

                case SmoothSpatial:
                    SmoothedInSpace(picked, radius, iterations);
                    break;

                case AverageNear:
                    foreach (IList<IPXVertex> group in VertexClusters.Near(picked, threshold))
                    {
                        Aim(group, Averaged(group));
                    }

                    break;

                case RampToward:
                    foreach (KeyValuePair<IPXVertex, IList<KeyValuePair<IPXBone, float>>> one in ramp)
                    {
                        VertexWeights.Write(one.Key, one.Value);
                    }

                    break;

                default:
                    break;
            }

            ReferenceCleanup.RepairWeights(model, picked);
            foreach (IPXVertex vertex in picked)
            {
                VertexWeights.Write(
                    vertex, VertexWeights.Settled(VertexWeights.Read(vertex)));
                if (vertex.SDEF
                    && (vertex.Bone1 == null
                        || vertex.Bone2 == null
                        || vertex.Bone3 != null
                        || vertex.Bone4 != null))
                {
                    vertex.SDEF = false;
                }
            }

            int changed = 0;
            for (int at = 0; at < picked.Count; at++)
            {
                changed += VertexWeights.Same(picked[at], before[at])
                    && SameSdef(picked[at], sdefBefore[at]) ? 0 : 1;
            }

            return ComposedEditResult.CompleteRewriting(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                },
                new[] { ScreenRefresh.WeightKind });
        }

        private static V3[] Sdef(IPXVertex vertex)
        {
            return vertex.SDEF
                ? new[]
                {
                    Vectors.Copied(vertex.SDEF_C),
                    Vectors.Copied(vertex.SDEF_R0),
                    Vectors.Copied(vertex.SDEF_R1),
                }
                : null;
        }

        private static bool SameSdef(IPXVertex vertex, V3[] held)
        {
            V3[] now = Sdef(vertex);
            if (now == null || held == null)
            {
                return now == held;
            }

            return Enumerable.Range(0, now.Length).All(at => Vectors.Same(now[at], held[at]));
        }

        private static bool TrySpatial(
            McpMethodContext context,
            string operation,
            out float radius,
            out int iterations,
            out float threshold,
            out string code,
            out string message)
        {
            iterations = 0;
            threshold = 0f;
            if (!ComposedInput.TryFloat(
                    context,
                    RadiusName,
                    operation,
                    new[] { SmoothSpatial, RampToward },
                    0f,
                    ComposedInput.NoCeiling,
                    out radius,
                    out code,
                    out message)
                || !ComposedInput.TryCount(
                    context,
                    IterationsName,
                    operation,
                    new[] { SmoothSpatial },
                    1,
                    out iterations,
                    out code,
                    out message)
                || !ComposedInput.TryFloat(
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
                return false;
            }

            if ((string.Equals(operation, SmoothSpatial, StringComparison.Ordinal)
                    || string.Equals(operation, RampToward, StringComparison.Ordinal))
                && !(radius > 0f))
            {
                code = ToolEnvelope.InvalidArgument;
                message = RadiusName + " は 0 より大きくなければならない。";

                return false;
            }

            return true;
        }

        private sealed class SurfaceInput
        {
            public SurfaceGeometry.SurfaceTree Tree { get; set; }

            public bool Ray { get; set; }

            public ISet<IPXBone> Excluded { get; set; }

            public int MaxBones { get; set; }

            public float MinWeight { get; set; }

            public bool Fades { get; set; }

            public double Near { get; set; }

            public double Fade { get; set; }

            public float SourceSmooth { get; set; }

            public float FalloffSmooth { get; set; }
        }

        private sealed class Plan
        {
            public IPXVertex Vertex { get; set; }

            public SdefSource Sdef { get; set; }

            public double Fade { get; set; }

            public IList<KeyValuePair<IPXBone, float>> Copied { get; set; }
        }

        private sealed class SdefSource
        {
            public IPXBone Bone1 { get; set; }

            public IPXBone Bone2 { get; set; }

            public V3 C { get; set; }

            public V3 R0 { get; set; }

            public V3 R1 { get; set; }
        }

        private static bool TrySurface(
            McpMethodContext context,
            IPXPmx model,
            string operation,
            out SurfaceInput input,
            out string code,
            out string message)
        {
            input = null;
            code = null;
            message = null;
            string[] names =
            {
                ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                ProjectionName,
                ExcludeBonesName,
                MaxBonesName,
                MinWeightName,
                FalloffName,
                SourceSmoothRadiusName,
                FalloffSmoothRadiusName,
            };
            if (!string.Equals(operation, FromSurface, StringComparison.Ordinal))
            {
                string stray = names.FirstOrDefault(name => context.Params.ContainsKey(name));
                if (stray == null)
                {
                    return true;
                }

                code = ToolEnvelope.InvalidArgument;
                message = stray + " を渡せるのは " + FromSurface + " のときだけである。";

                return false;
            }

            List<int> materials;
            string projection = ProjectionNearest;
            int maxBones = DefaultMaxBones;
            float minWeight = 0f;
            float sourceSmooth = 0f;
            float falloffSmooth = 0f;
            ISet<IPXBone> excluded = new HashSet<IPXBone>(ReferenceComparer<IPXBone>.Instance);
            input = new SurfaceInput();
            if (!ModelFindSurfaceDistances.TryMaterials(
                    context,
                    model,
                    ModelFindSurfaceDistances.SurfaceMaterialIndicesName,
                    out materials,
                    out code,
                    out message))
            {
                input = null;

                return false;
            }

            if ((context.Params.ContainsKey(ProjectionName)
                    && !ComposedInput.TryChoice(
                        context,
                        ProjectionName,
                        operation,
                        new[] { FromSurface },
                        new[] { ProjectionNearest, ProjectionRay },
                        out projection,
                        out code,
                        out message))
                || (context.Params.ContainsKey(MaxBonesName)
                    && !TryMaxBones(context, operation, out maxBones, out code, out message))
                || (context.Params.ContainsKey(MinWeightName)
                    && !ComposedInput.TryFloat(
                        context,
                        MinWeightName,
                        operation,
                        new[] { FromSurface },
                        0f,
                        ComposedInput.NoCeiling,
                        out minWeight,
                        out code,
                        out message))
                || !TrySmoothRadius(context, operation, SourceSmoothRadiusName, out sourceSmooth, out code, out message)
                || !TrySmoothRadius(context, operation, FalloffSmoothRadiusName, out falloffSmooth, out code, out message)
                || !TryExcluded(context, model, excluded, out code, out message)
                || !TryFalloff(context, input, out message))
            {
                code = code ?? ToolEnvelope.InvalidArgument;
                input = null;

                return false;
            }

            code = null;
            message = null;
            input.Tree = SurfaceGeometry.SurfaceTree.Of(model, materials);
            if (input.Tree == null)
            {
                code = ToolEnvelope.NotApplicable;
                message = ModelFindSurfaceDistances.SurfaceMaterialIndicesName
                    + " の材質に面積のある面が1つも無い。";
                input = null;

                return false;
            }

            input.Ray = string.Equals(projection, ProjectionRay, StringComparison.Ordinal);
            input.Excluded = excluded;
            input.MaxBones = maxBones;
            input.MinWeight = minWeight;
            input.SourceSmooth = sourceSmooth;
            input.FalloffSmooth = falloffSmooth;

            return true;
        }

        private static bool TrySmoothRadius(
            McpMethodContext context,
            string operation,
            string name,
            out float radius,
            out string code,
            out string message)
        {
            radius = 0f;
            code = null;
            message = null;
            if (!context.Params.ContainsKey(name))
            {
                return true;
            }

            if (!ComposedInput.TryFloat(
                    context,
                    name,
                    operation,
                    new[] { FromSurface },
                    0f,
                    ComposedInput.NoCeiling,
                    out radius,
                    out code,
                    out message))
            {
                return false;
            }

            if (radius > 0f)
            {
                return true;
            }

            radius = 0f;
            code = ToolEnvelope.InvalidArgument;
            message = name + " は 0 より大きくなければならない。";

            return false;
        }

        private static bool TryMaxBones(
            McpMethodContext context,
            string operation,
            out int maxBones,
            out string code,
            out string message)
        {
            if (!ComposedInput.TryCount(
                    context,
                    MaxBonesName,
                    operation,
                    new[] { FromSurface },
                    1,
                    out maxBones,
                    out code,
                    out message))
            {
                return false;
            }

            if (maxBones <= VertexWeights.Slots)
            {
                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            message = MaxBonesName + " は 1 以上 "
                + VertexWeights.Slots.ToString(CultureInfo.InvariantCulture) + " 以下でなければならない。";

            return false;
        }

        private static bool TryExcluded(
            McpMethodContext context,
            IPXPmx model,
            ISet<IPXBone> excluded,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(ExcludeBonesName, out given))
            {
                return true;
            }

            List<int> taken;
            if (!PositionInput.TryMany(
                given,
                ExcludeBonesName,
                ExcludeBonesName + " はボーンの位置を並べた並びでなければならない。",
                model.Bone.Count,
                true,
                out taken,
                out code,
                out message))
            {
                return false;
            }

            HashSet<IPXBone> roots = new HashSet<IPXBone>(
                taken.Select(at => model.Bone[at]), ReferenceComparer<IPXBone>.Instance);

            foreach (IPXBone bone in model.Bone)
            {
                IPXBone up = bone;
                for (int steps = 0; up != null && steps <= model.Bone.Count; steps++)
                {
                    if (roots.Contains(up))
                    {
                        excluded.Add(bone);
                        break;
                    }

                    up = up.Parent;
                }
            }

            return true;
        }

        private static bool TryFalloff(McpMethodContext context, SurfaceInput input, out string message)
        {
            message = null;
            object given;
            if (!context.Params.TryGetValue(FalloffName, out given))
            {
                return true;
            }

            IDictionary<string, object> members = given as IDictionary<string, object>;
            object near;
            object fade;
            float nearRead;
            float fadeRead;
            if (members == null
                || members.Keys.Any(name => name != FalloffNearName && name != FalloffFadeName)
                || !members.TryGetValue(FalloffNearName, out near)
                || !members.TryGetValue(FalloffFadeName, out fade)
                || !ValueInput.TrySingle(near, out nearRead)
                || !ValueInput.TrySingle(fade, out fadeRead)
                || !(nearRead >= 0f)
                || !(fadeRead >= 0f)
                || float.IsInfinity(nearRead)
                || float.IsInfinity(fadeRead))
            {
                message = FalloffName + " は " + FalloffNearName + " と " + FalloffFadeName
                    + " の両方を0以上の有限の数で持つ組でなければならない。";

                return false;
            }

            input.Fades = true;
            input.Near = nearRead;
            input.Fade = fadeRead;

            return true;
        }

        private static void Surfaced(IEnumerable<IPXVertex> picked, SurfaceInput input)
        {
            List<Plan> plans = new List<Plan>();
            foreach (IPXVertex vertex in picked)
            {
                Plan plan = Planned(vertex, input);
                if (plan != null)
                {
                    plans.Add(plan);
                }
            }

            if (input.SourceSmooth > 0f)
            {
                SmoothedCopies(plans, input);
            }

            if (input.FalloffSmooth > 0f && input.Fades)
            {
                SmoothedFades(plans, input.FalloffSmooth);
            }

            foreach (Plan plan in plans)
            {
                Applied(plan);
            }
        }

        private static Plan Planned(IPXVertex vertex, SurfaceInput input)
        {
            if (!Vectors.Finite(vertex.Position))
            {
                return null;
            }

            SurfaceGeometry.Vec at = SurfaceGeometry.Vec.Of(vertex.Position);
            SurfaceGeometry.Hit hit = Landing(input, at, vertex.Normal);
            if (hit == null || hit.Corners == null)
            {
                return null;
            }

            return new Plan
            {
                Vertex = vertex,
                Sdef = Sdefed(hit),
                Fade = Faded(input, (hit.Point - at).Length),
                Copied = Copied(hit, input),
            };
        }

        private static void Applied(Plan plan)
        {
            IPXVertex vertex = plan.Vertex;
            double fade = plan.Fade;
            IList<KeyValuePair<IPXBone, float>> copied = plan.Copied;
            if (fade >= 1d || copied.Count == 0)
            {
                return;
            }

            if (fade == 0d && TrySdef(vertex, plan.Sdef, copied))
            {
                return;
            }

            vertex.SDEF = false;
            VertexWeights.Write(
                vertex,
                fade == 0d
                    ? copied
                    : VertexWeights.Settled(
                        Weighted(copied, (float)(1d - fade))
                            .Concat(Weighted(VertexWeights.Read(vertex), (float)fade))));
        }

        private static void SmoothedCopies(IList<Plan> plans, SurfaceInput input)
        {
            IList<Plan> held = plans.Where(plan => plan.Copied.Count > 0).ToList();
            IList<int>[] near;
            double[][] pull;
            Gaussian(held.Select(plan => plan.Vertex).ToList(), input.SourceSmooth, out near, out pull);
            IList<KeyValuePair<IPXBone, float>>[] next = held.Select(plan => plan.Copied).ToArray();
            for (int at = 0; at < held.Count; at++)
            {
                if (near[at].Count == 0)
                {
                    continue;
                }

                List<KeyValuePair<IPXBone, float>> mixed =
                    new List<KeyValuePair<IPXBone, float>>(held[at].Copied);
                for (int slot = 0; slot < near[at].Count; slot++)
                {
                    mixed.AddRange(Weighted(held[near[at][slot]].Copied, (float)pull[at][slot]));
                }

                next[at] = Limited(mixed, input);
            }

            for (int at = 0; at < held.Count; at++)
            {
                held[at].Copied = next[at];
            }
        }

        private static void SmoothedFades(IList<Plan> plans, float radius)
        {
            IList<int>[] near;
            double[][] pull;
            Gaussian(plans.Select(plan => plan.Vertex).ToList(), radius, out near, out pull);
            double[] next = plans.Select(plan => plan.Fade).ToArray();
            for (int at = 0; at < plans.Count; at++)
            {
                double sum = plans[at].Fade;
                double whole = 1d;
                for (int slot = 0; slot < near[at].Count; slot++)
                {
                    sum += pull[at][slot] * plans[near[at][slot]].Fade;
                    whole += pull[at][slot];
                }

                next[at] = sum / whole;
            }

            for (int at = 0; at < plans.Count; at++)
            {
                plans[at].Fade = next[at];
            }
        }

        private static SurfaceGeometry.Hit Landing(SurfaceInput input, SurfaceGeometry.Vec at, V3 normal)
        {
            if (input.Ray && Vectors.Finite(normal))
            {
                SurfaceGeometry.Vec along = SurfaceGeometry.Vec.Of(normal);
                double length = along.Length;
                if (length > 0d && !double.IsInfinity(length))
                {
                    along = along * (1d / length);
                    SurfaceGeometry.Hit front = input.Tree.Cast(at, along);
                    SurfaceGeometry.Hit back = input.Tree.Cast(at, along * -1d);
                    if (front != null && (back == null || front.Signed <= back.Signed))
                    {
                        return front;
                    }

                    if (back != null)
                    {
                        return back;
                    }
                }
            }

            return input.Tree.Nearest(at, double.PositiveInfinity);
        }

        private static double Faded(SurfaceInput input, double apart)
        {
            if (!input.Fades || apart <= input.Near)
            {
                return 0d;
            }

            if (input.Fade <= 0d || apart >= input.Near + input.Fade)
            {
                return 1d;
            }

            double t = (apart - input.Near) / input.Fade;

            return t * t * (3d - (2d * t));
        }

        private static IList<KeyValuePair<IPXBone, float>> Copied(SurfaceGeometry.Hit hit, SurfaceInput input)
        {
            List<KeyValuePair<IPXBone, float>> mixed = new List<KeyValuePair<IPXBone, float>>();
            for (int corner = 0; corner < hit.Corners.Length; corner++)
            {
                double by = hit.Barycentric[corner];
                if (!(by > 0d))
                {
                    continue;
                }

                foreach (KeyValuePair<IPXBone, float> share in VertexWeights.Read(hit.Corners[corner]))
                {
                    if (share.Value > 0f && !input.Excluded.Contains(share.Key))
                    {
                        mixed.Add(new KeyValuePair<IPXBone, float>(share.Key, (float)(share.Value * by)));
                    }
                }
            }

            return Limited(mixed, input);
        }

        private static IList<KeyValuePair<IPXBone, float>> Limited(
            IEnumerable<KeyValuePair<IPXBone, float>> mixed, SurfaceInput input)
        {
            IList<KeyValuePair<IPXBone, float>> settled = VertexWeights.Settled(mixed);
            settled = VertexWeights.Settled(settled.Take(input.MaxBones).ToList());

            return VertexWeights.Settled(
                settled.Where((share, at) => at == 0 || share.Value >= input.MinWeight).ToList());
        }

        private static SdefSource Sdefed(SurfaceGeometry.Hit hit)
        {
            IPXVertex first = hit.Corners[0];
            if (!hit.Corners.All(corner => corner.SDEF
                    && corner.Bone1 != null
                    && corner.Bone2 != null
                    && ReferenceEquals(corner.Bone1, first.Bone1)
                    && ReferenceEquals(corner.Bone2, first.Bone2))
                || ReferenceEquals(first.Bone1, first.Bone2))
            {
                return null;
            }

            double[] by = hit.Barycentric;

            return new SdefSource
            {
                Bone1 = first.Bone1,
                Bone2 = first.Bone2,
                C = Blended(hit.Corners, by, corner => corner.SDEF_C),
                R0 = Blended(hit.Corners, by, corner => corner.SDEF_R0),
                R1 = Blended(hit.Corners, by, corner => corner.SDEF_R1),
            };
        }

        private static bool TrySdef(
            IPXVertex vertex, SdefSource source, IList<KeyValuePair<IPXBone, float>> copied)
        {
            if (source == null
                || copied.Count != 2
                || !copied.Any(share => ReferenceEquals(share.Key, source.Bone1))
                || !copied.Any(share => ReferenceEquals(share.Key, source.Bone2)))
            {
                return false;
            }

            vertex.SDEF = true;
            vertex.Bone1 = source.Bone1;
            vertex.Bone2 = source.Bone2;
            vertex.Bone3 = null;
            vertex.Bone4 = null;
            vertex.Weight1 = copied.First(share => ReferenceEquals(share.Key, source.Bone1)).Value;
            vertex.Weight2 = copied.First(share => ReferenceEquals(share.Key, source.Bone2)).Value;
            vertex.Weight3 = 0f;
            vertex.Weight4 = 0f;
            vertex.SDEF_C = source.C;
            vertex.SDEF_R0 = source.R0;
            vertex.SDEF_R1 = source.R1;

            return true;
        }

        private static V3 Blended(IPXVertex[] corners, double[] by, Func<IPXVertex, V3> member)
        {
            double x = 0d;
            double y = 0d;
            double z = 0d;
            for (int at = 0; at < corners.Length; at++)
            {
                V3 held = member(corners[at]);
                x += held.X * by[at];
                y += held.Y * by[at];
                z += held.Z * by[at];
            }

            return new V3((float)x, (float)y, (float)z);
        }

        private static bool TryBones(
            string operation,
            int fromBone,
            int toBone,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            if (!string.Equals(operation, ReplaceBone, StringComparison.Ordinal))
            {
                return true;
            }

            code = ToolEnvelope.InvalidArgument;

            if (fromBone == toBone)
            {
                message = FromBoneName + " と " + ToBoneName + " は別のボーンを指さなければならない。";

                return false;
            }

            code = null;

            return true;
        }

        private static IList<IPXVertex> Holding(IEnumerable<IPXVertex> picked, IPXBone from)
        {
            return picked
                .Where(vertex => VertexWeights.Read(vertex)
                    .Any(share => share.Value > 0f && ReferenceEquals(share.Key, from)))
                .ToList();
        }

        private static void Replaced(IEnumerable<IPXVertex> held, IPXBone from, IPXBone to)
        {
            foreach (IPXVertex vertex in held)
            {
                if (vertex.SDEF)
                {
                    ReplacedInSlots(vertex, from, to);
                    continue;
                }

                VertexWeights.Write(
                    vertex,
                    VertexWeights.Settled(VertexWeights.Read(vertex).Select(share =>
                        ReferenceEquals(share.Key, from)
                            ? new KeyValuePair<IPXBone, float>(to, share.Value)
                            : share)));
            }
        }

        private static void ReplacedInSlots(IPXVertex vertex, IPXBone from, IPXBone to)
        {
            vertex.Bone1 = ReferenceEquals(vertex.Bone1, from) ? to : vertex.Bone1;
            vertex.Bone2 = ReferenceEquals(vertex.Bone2, from) ? to : vertex.Bone2;
            if (vertex.Bone1 == null || !ReferenceEquals(vertex.Bone1, vertex.Bone2))
            {
                return;
            }

            vertex.Weight1 += vertex.Weight2;
            vertex.Bone2 = null;
            vertex.Weight2 = 0f;
            vertex.SDEF = false;
        }

        private static IList<KeyValuePair<IPXBone, float>> Averaged(IList<IPXVertex> picked)
        {
            return VertexWeights.Settled(picked.SelectMany(VertexWeights.Read));
        }

        private static void Aim(
            IEnumerable<IPXVertex> picked, IList<KeyValuePair<IPXBone, float>> shares)
        {
            foreach (IPXVertex vertex in picked)
            {
                VertexWeights.Write(vertex, shares.ToList());
            }
        }

        private static void Smoothed(IPXPmx model, IList<IPXVertex> picked, float strength)
        {
            IDictionary<IPXVertex, ISet<IPXVertex>> around = Around(model);
            Dictionary<IPXVertex, IList<KeyValuePair<IPXBone, float>>> made =
                new Dictionary<IPXVertex, IList<KeyValuePair<IPXBone, float>>>(
                    ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXVertex vertex in picked)
            {
                ISet<IPXVertex> neighbours;
                if (!around.TryGetValue(vertex, out neighbours) || neighbours.Count == 0)
                {
                    continue;
                }

                IList<KeyValuePair<IPXBone, float>> theirs =
                    VertexWeights.Settled(neighbours.SelectMany(VertexWeights.Read));
                made[vertex] = VertexWeights.Settled(
                    Weighted(VertexWeights.Read(vertex), 1f - strength)
                        .Concat(Weighted(theirs, strength)));
            }

            foreach (KeyValuePair<IPXVertex, IList<KeyValuePair<IPXBone, float>>> made1 in made)
            {
                VertexWeights.Write(made1.Key, made1.Value);
            }
        }

        private static void Gaussian(
            IList<IPXVertex> picked, float radius, out IList<int>[] near, out double[][] pull)
        {
            near = SurfaceGeometry.Within(
                picked.Select(vertex => SurfaceGeometry.Vec.Of(vertex.Position)).ToList(), radius);
            double variance = radius / 2d * (radius / 2d);
            pull = new double[picked.Count][];
            for (int at = 0; at < picked.Count; at++)
            {
                SurfaceGeometry.Vec here = SurfaceGeometry.Vec.Of(picked[at].Position);
                pull[at] = near[at]
                    .Select(other =>
                    {
                        double apart = (SurfaceGeometry.Vec.Of(picked[other].Position) - here).Length;

                        return Math.Exp(-apart * apart / (2d * variance));
                    })
                    .ToArray();
            }
        }

        private static void SmoothedInSpace(IList<IPXVertex> picked, float radius, int iterations)
        {
            IList<int>[] near;
            double[][] pull;
            Gaussian(picked, radius, out near, out pull);
            IList<KeyValuePair<IPXBone, float>>[] current =
                picked.Select(VertexWeights.Read).ToArray();
            for (int round = 0; round < iterations; round++)
            {
                IList<KeyValuePair<IPXBone, float>>[] next = current.ToArray();
                for (int at = 0; at < picked.Count; at++)
                {
                    if (near[at].Count == 0)
                    {
                        continue;
                    }

                    List<KeyValuePair<IPXBone, float>> mixed =
                        new List<KeyValuePair<IPXBone, float>>(current[at]);
                    for (int slot = 0; slot < near[at].Count; slot++)
                    {
                        mixed.AddRange(Weighted(current[near[at][slot]], (float)pull[at][slot]));
                    }

                    next[at] = VertexWeights.Settled(mixed);
                }

                current = next;
            }

            for (int at = 0; at < picked.Count; at++)
            {
                if (near[at].Count > 0)
                {
                    VertexWeights.Write(picked[at], current[at]);
                }
            }
        }

        private static IList<KeyValuePair<IPXVertex, IList<KeyValuePair<IPXBone, float>>>> RampPlan(
            IList<IPXVertex> picked, IList<IPXVertex> targets, float radius)
        {
            IList<SurfaceGeometry.Vec> goals = targets
                .Where(target => Vectors.Finite(target.Position))
                .Select(target => SurfaceGeometry.Vec.Of(target.Position))
                .ToList();
            IList<KeyValuePair<IPXBone, float>>[] reached = targets
                .Where(target => Vectors.Finite(target.Position))
                .Select(VertexWeights.Read)
                .ToArray();
            List<KeyValuePair<IPXVertex, IList<KeyValuePair<IPXBone, float>>>> made =
                new List<KeyValuePair<IPXVertex, IList<KeyValuePair<IPXBone, float>>>>();
            foreach (IPXVertex vertex in picked)
            {
                if (!Vectors.Finite(vertex.Position))
                {
                    continue;
                }

                SurfaceGeometry.Vec here = SurfaceGeometry.Vec.Of(vertex.Position);
                int nearest = -1;
                double best = double.PositiveInfinity;
                for (int goal = 0; goal < goals.Count; goal++)
                {
                    double apart = (goals[goal] - here).Length;
                    if (apart < best)
                    {
                        best = apart;
                        nearest = goal;
                    }
                }

                double ratio = nearest < 0 ? 0d : 1d - (best / radius);
                if (!(ratio > 0d))
                {
                    continue;
                }

                made.Add(new KeyValuePair<IPXVertex, IList<KeyValuePair<IPXBone, float>>>(
                    vertex,
                    VertexWeights.Settled(
                        Weighted(VertexWeights.Read(vertex), (float)(1d - ratio))
                            .Concat(Weighted(reached[nearest], (float)ratio)))));
            }

            return made;
        }

        private static IEnumerable<KeyValuePair<IPXBone, float>> Weighted(
            IEnumerable<KeyValuePair<IPXBone, float>> shares, float by)
        {
            return shares.Select(
                share => new KeyValuePair<IPXBone, float>(share.Key, share.Value * by));
        }

        /// <summary>同じ面に載っている頂点の表。自分自身は入らない。</summary>
        private static IDictionary<IPXVertex, ISet<IPXVertex>> Around(IPXPmx model)
        {
            Dictionary<IPXVertex, ISet<IPXVertex>> around =
                new Dictionary<IPXVertex, ISet<IPXVertex>>(ReferenceComparer<IPXVertex>.Instance);
            foreach (IPXMaterial material in model.Material)
            {
                foreach (IPXFace face in material.Faces)
                {
                    IPXVertex[] corners =
                        { face.Vertex1, face.Vertex2, face.Vertex3 };
                    foreach (IPXVertex corner in corners.Where(held => held != null))
                    {
                        foreach (IPXVertex other in corners.Where(held => held != null))
                        {
                            if (!ReferenceEquals(corner, other))
                            {
                                Met(around, corner).Add(other);
                            }
                        }
                    }
                }
            }

            return around;
        }

        private static ISet<IPXVertex> Met(
            IDictionary<IPXVertex, ISet<IPXVertex>> around, IPXVertex corner)
        {
            ISet<IPXVertex> held;
            if (!around.TryGetValue(corner, out held))
            {
                held = new HashSet<IPXVertex>(ReferenceComparer<IPXVertex>.Instance);
                around[corner] = held;
            }

            return held;
        }

        private static void Nearest(IPXPmx model, IList<IPXVertex> picked, bool alongTheBone)
        {
            if (model.Bone.Count == 0)
            {
                return;
            }

            IList<IPXBone> bones = model.Bone;
            NearestBones table = bones.All(bone => Vectors.Finite(bone.Position)
                    && (!alongTheBone || Vectors.Finite(Tip(bone))))
                ? (alongTheBone ? NearestBones.BySegment(bones, Tip) : NearestBones.ByPosition(bones))
                : null;
            foreach (IPXVertex vertex in picked)
            {
                IPXBone closest = table != null && Vectors.Finite(vertex.Position)
                    ? table.Closest(vertex.Position)
                    : Scanned(bones, vertex, alongTheBone);
                VertexWeights.Write(
                    vertex, new[] { new KeyValuePair<IPXBone, float>(closest, 1f) });
            }
        }

        /// <summary>全ボーンを順に比べて最も近いものを選ぶ。距離が等しければ先のものを残す。</summary>
        private static IPXBone Scanned(IList<IPXBone> bones, IPXVertex vertex, bool alongTheBone)
        {
            IPXBone closest = null;
            float best = 0f;
            foreach (IPXBone bone in bones)
            {
                float apart = alongTheBone
                    ? Vectors.DistanceToSegment(vertex.Position, bone.Position, Tip(bone))
                    : Vectors.Distance(vertex.Position, bone.Position);
                if (closest != null && apart >= best)
                {
                    continue;
                }

                closest = bone;
                best = apart;
            }

            return closest;
        }

        /// <summary>そのボーンの表示先の点。表示先を持たないボーンでは根元と同じ点になる。</summary>
        private static V3 Tip(IPXBone bone)
        {
            return bone.ToBone != null
                ? bone.ToBone.Position
                : Vectors.Add(bone.Position, bone.ToOffset);
        }

        private static void Mirrored(IPXPmx model, IList<IPXVertex> picked, string axis)
        {
            Dictionary<string, IPXVertex> spots =
                new Dictionary<string, IPXVertex>(StringComparer.Ordinal);
            foreach (IPXVertex vertex in model.Vertex)
            {
                spots[Spot(vertex.Position)] = vertex;
            }

            List<KeyValuePair<IPXVertex, IPXVertex>> pairs =
                new List<KeyValuePair<IPXVertex, IPXVertex>>();
            foreach (IPXVertex vertex in picked)
            {
                IPXVertex twin;
                if (!spots.TryGetValue(Spot(MirrorPartners.Across(vertex.Position, axis)), out twin)
                    || ReferenceEquals(twin, vertex))
                {
                    continue;
                }

                pairs.Add(new KeyValuePair<IPXVertex, IPXVertex>(vertex, (IPXVertex)twin.Clone()));
            }

            foreach (KeyValuePair<IPXVertex, IPXVertex> pair in pairs)
            {
                IPXVertex vertex = pair.Key;
                IPXVertex twin = pair.Value;
                vertex.SDEF = false;
                VertexWeights.Write(
                    vertex,
                    VertexWeights.Read(twin)
                        .Select(share => new KeyValuePair<IPXBone, float>(
                            MirrorPartners.OfBone(model, share.Key) ?? share.Key, share.Value))
                        .ToList());
                vertex.SDEF = twin.SDEF;
                VertexWeights.MirrorSdef(vertex, twin, point => MirrorPartners.Across(point, axis));
            }
        }

        /// <summary>同じ点を同じ綴りで表す鍵。</summary>
        private static string Spot(V3 given)
        {
            return string.Join(
                "/",
                given.X.ToString("R", CultureInfo.InvariantCulture),
                given.Y.ToString("R", CultureInfo.InvariantCulture),
                given.Z.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
