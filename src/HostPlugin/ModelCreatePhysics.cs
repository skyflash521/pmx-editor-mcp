using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指したボーンや頂点から、剛体とJointを作るツール。
    /// </summary>
    public static class ModelCreatePhysics
    {
        public const string ToolName = "model_create_physics";

        public const string BodyFollowBone = "bodyFollowBone";

        public const string BodyPhysics = "bodyPhysics";

        public const string Joint = "joint";

        public const string BodyAndJoint = "bodyAndJoint";

        public const string BodyAtVertices = "bodyAtVertices";

        public const string SplitBodies = "splitBodies";

        public const string FitBodiesToChains = "fitBodiesToChains";

        public const string ChainsName = "chains";

        public const string ClosedName = "closed";

        public const string CoverName = "cover";

        public const string ThicknessName = "thickness";

        public const string ChangedName = "changed";

        public const string ShapeName = "shape";

        public const string Sphere = "sphere";

        public const string Box = "box";

        public const string Capsule = "capsule";

        public const string AddedBodiesName = "addedBodies";

        public const string AddedJointsName = "addedJoints";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    BodyFollowBone, BodyPhysics, Joint, BodyAndJoint, BodyAtVertices, SplitBodies, FitBodiesToChains,
                };
            }
        }

        /// <summary>受け取れる当たりの形。スキーマが並べる順。</summary>
        public static IList<string> Shapes
        {
            get
            {
                return new[] { Sphere, Box, Capsule };
            }
        }

        /// <summary><paramref name="builder"/> は新しい要素を作る相手を返す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            List<string> known = new List<string>
            {
                ComposedOperation.OperationName,
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.Ranges,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
                ShapeName,
                ChainsName,
                ClosedName,
                CoverName,
                ThicknessName,
            };
            methods.Add(
                ToolName, edit.Method(known, (context, pmx) => Run(context, pmx, builder)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, object pmx, Func<object> builder)
        {
            IPXPmx model = (IPXPmx)pmx;
            string operation;
            string code;
            string message;
            IList<int> chosen;
            if (!ComposedOperation.TryTake(
                    context, Operations, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (string.Equals(operation, FitBodiesToChains, StringComparison.Ordinal))
            {
                return Fitted(context, model);
            }

            float unused;
            if (!TryNotChained(context, out code, out message)
                || !ComposedInput.TryFloat(
                    context,
                    CoverName,
                    operation,
                    new[] { FitBodiesToChains },
                    0f,
                    ComposedInput.NoCeiling,
                    out unused,
                    out code,
                    out message)
                || !ComposedInput.TryFloat(
                    context,
                    ThicknessName,
                    operation,
                    new[] { FitBodiesToChains },
                    0f,
                    ComposedInput.NoCeiling,
                    out unused,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            string shape;
            if (!TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    Counted(model, operation),
                    out chosen,
                    out code,
                    out message,
                    context.Screen.Pick(Pointed(operation), Counted(model, operation)))
                || !ComposedInput.TryChoice(
                    context,
                    ShapeName,
                    operation,
                    new[] { BodyFollowBone, BodyPhysics, BodyAndJoint, BodyAtVertices },
                    Shapes,
                    out shape,
                    out code,
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            IPXPmxBuilder made = (IPXPmxBuilder)builder();
            switch (operation)
            {
                case Joint:
                    return Tied(model, made, chosen.Select(at => model.Body[at]).ToList());

                case BodyAtVertices:
                    return Wrapped(model, made, chosen.Select(at => model.Vertex[at]).ToList(), shape);

                case SplitBodies:
                    return Split(model, made, chosen.Select(at => model.Bone[at]).ToList());

                default:
                    return Attached(
                        model, made, chosen.Select(at => model.Bone[at]).ToList(), operation, shape);
            }
        }

        private static string Pointed(string operation)
        {
            switch (operation)
            {
                case Joint:
                    return ElementKinds.Body;

                case BodyAtVertices:
                    return ElementKinds.Vertex;

                default:
                    return ElementKinds.Bone;
            }
        }

        private static int Counted(IPXPmx model, string operation)
        {
            switch (operation)
            {
                case Joint:
                    return model.Body.Count;

                case BodyAtVertices:
                    return model.Vertex.Count;

                default:
                    return model.Bone.Count;
            }
        }

        private static ComposedEditResult Attached(
            IPXPmx model,
            IPXPmxBuilder builder,
            IList<IPXBone> picked,
            string operation,
            string shape)
        {
            bool following = string.Equals(operation, BodyFollowBone, StringComparison.Ordinal);
            bool tying = string.Equals(operation, BodyAndJoint, StringComparison.Ordinal);
            List<int> bodies = new List<int>();
            List<int> joints = new List<int>();
            foreach (IPXBone bone in picked)
            {
                IPXBody body = builder.Body();
                body.Name = bone.Name;
                body.NameE = string.Empty;
                body.Bone = bone;
                body.Position = Vectors.Copied(bone.Position);
                body.BoxKind = Kind(shape);
                body.BoxSize = new V3(Thickness, Thickness, Thickness);
                body.Rotation = new V3(0f, 0f, 0f);
                body.Mode = following ? BodyMode.Static : BodyMode.Dynamic;
                model.Body.Add(body);
                bodies.Add(model.Body.Count - 1);
                IPXBody above = tying ? Holding(model, bone.Parent) : null;
                if (above == null)
                {
                    continue;
                }

                model.Joint.Add(Tie(builder, above, body));
                joints.Add(model.Joint.Count - 1);
            }

            return Answer(bodies, joints);
        }

        private static ComposedEditResult Tied(
            IPXPmx model, IPXPmxBuilder builder, IList<IPXBody> picked)
        {
            List<int> joints = new List<int>();
            for (int at = 1; at < picked.Count; at++)
            {
                model.Joint.Add(Tie(builder, picked[at - 1], picked[at]));
                joints.Add(model.Joint.Count - 1);
            }

            return Answer(new int[0], joints);
        }

        private static ComposedEditResult Wrapped(
            IPXPmx model, IPXPmxBuilder builder, IList<IPXVertex> picked, string shape)
        {
            if (picked.Count == 0)
            {
                return Answer(new int[0], new int[0]);
            }

            V3 least = Corner(picked, Math.Min);
            V3 most = Corner(picked, Math.Max);
            V3 middle = Vectors.Between(least, most);
            V3 size;
            if (!TrySized(shape, least, most, middle, picked, out size))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "指した頂点を包む当たりの大きさが、単精度で持てる範囲を超える。");
            }

            IPXBody body = builder.Body();
            body.Name = string.Empty;
            body.NameE = string.Empty;
            body.Position = middle;
            body.BoxKind = Kind(shape);
            body.BoxSize = size;
            body.Mode = BodyMode.Static;
            model.Body.Add(body);

            return Answer(new[] { model.Body.Count - 1 }, new int[0]);
        }

        private static ComposedEditResult Split(
            IPXPmx model, IPXPmxBuilder builder, IList<IPXBone> picked)
        {
            Dictionary<IPXBody, IPXBody> halves =
                new Dictionary<IPXBody, IPXBody>(ReferenceComparer<IPXBody>.Instance);
            Dictionary<IPXBody, V3> was = new Dictionary<IPXBody, V3>(ReferenceComparer<IPXBody>.Instance);
            foreach (IPXBone middle in picked.Distinct(ReferenceComparer<IPXBone>.Instance))
            {
                IPXBone root = middle.Parent;
                if (root == null)
                {
                    continue;
                }

                V3 tip = middle.ToBone != null
                    ? middle.ToBone.Position
                    : Vectors.Add(middle.Position, middle.ToOffset);
                V3 along = Vectors.Toward(tip, root.Position);
                float whole = Vectors.Dot(Vectors.Apart(tip, root.Position, 1f), along);
                float first = Vectors.Dot(Vectors.Apart(middle.Position, root.Position, 1f), along);
                if (!(first > 0f) || !(first < whole))
                {
                    continue;
                }

                float ratio = first / whole;
                foreach (IPXBody body in model.Body
                    .Where(held => ReferenceEquals(held.Bone, root) && !halves.ContainsKey(held))
                    .ToList())
                {
                    V3 offset = Vectors.Apart(body.Position, root.Position, 1f);
                    float at = Vectors.Dot(offset, along);
                    V3 aside = Vectors.Add(root.Position, Vectors.Add(offset, Vectors.Scale(along, -at)));
                    IPXBody half = (IPXBody)body.Clone();
                    half.Name = middle.Name;
                    half.Bone = middle;
                    half.Position = Vectors.Add(aside, Vectors.Scale(along, first + (at * (1f - ratio))));
                    half.BoxSize = Shortened(body, along, 1f - ratio);
                    was[body] = Vectors.Copied(body.Position);
                    body.Position = Vectors.Add(aside, Vectors.Scale(along, at * ratio));
                    body.BoxSize = Shortened(body, along, ratio);
                    model.Body.Insert(model.Body.IndexOf(body) + 1, half);
                    halves[body] = half;
                }
            }

            List<IPXJoint> added = new List<IPXJoint>();
            foreach (IPXJoint joint in model.Joint.ToList())
            {
                IPXBody first = joint.BodyA;
                IPXBody second = joint.BodyB;
                bool firstSplit = first != null && halves.ContainsKey(first);
                bool secondSplit = second != null && halves.ContainsKey(second);
                if (firstSplit && secondSplit && !Below(second, first.Bone) && !Below(first, second.Bone))
                {
                    V3 shift = Vectors.Apart(joint.Position, Vectors.Between(was[first], was[second]), 1f);
                    IPXJoint copy = (IPXJoint)joint.Clone();
                    copy.BodyA = halves[first];
                    copy.BodyB = halves[second];
                    copy.Position = Vectors.Add(Vectors.Between(copy.BodyA.Position, copy.BodyB.Position), shift);
                    joint.Position = Vectors.Add(Vectors.Between(first.Position, second.Position), shift);
                    added.Add(copy);
                    continue;
                }

                if (firstSplit && Below(second, first.Bone))
                {
                    joint.BodyA = halves[first];
                }

                if (secondSplit && Below(first, second.Bone))
                {
                    joint.BodyB = halves[second];
                }
            }

            foreach (KeyValuePair<IPXBody, IPXBody> split in halves)
            {
                IPXJoint template = model.Joint.FirstOrDefault(joint =>
                    (ReferenceEquals(joint.BodyB, split.Key) && Below(split.Key, joint.BodyA == null ? null : joint.BodyA.Bone))
                    || (ReferenceEquals(joint.BodyA, split.Key) && Below(split.Key, joint.BodyB == null ? null : joint.BodyB.Bone)));
                IPXJoint seam = template == null
                    ? Tie(builder, split.Key, split.Value)
                    : (IPXJoint)template.Clone();
                bool reversed = template != null && ReferenceEquals(template.BodyA, split.Key);
                seam.Name = split.Value.Name;
                seam.BodyA = reversed ? split.Value : split.Key;
                seam.BodyB = reversed ? split.Key : split.Value;
                seam.Position = Vectors.Copied(split.Value.Bone.Position);
                added.Add(seam);
            }

            foreach (IPXJoint joint in added)
            {
                model.Joint.Add(joint);
            }

            return Answer(
                halves.Values.Select(half => model.Body.IndexOf(half)).ToList(),
                added.Select(joint => model.Joint.IndexOf(joint)).ToList());
        }

        private static bool TryNotChained(McpMethodContext context, out string code, out string message)
        {
            code = null;
            message = null;
            foreach (string name in new[] { ChainsName, ClosedName })
            {
                if (context.Params.ContainsKey(name))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = name + " を渡せるのは " + FitBodiesToChains + " のときだけである。";

                    return false;
                }
            }

            return true;
        }

        private static ComposedEditResult Fitted(McpMethodContext context, IPXPmx model)
        {
            string[] targets =
            {
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.Ranges,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
            };
            if (targets.Any(context.Params.ContainsKey))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    FitBodiesToChains + " は " + ChainsName + " でボーンを指し、"
                        + string.Join("・", targets) + " は渡さない。");
            }

            if (context.Params.ContainsKey(ShapeName))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    ShapeName + " を " + FitBodiesToChains + " へ渡せない。剛体は箱にする。");
            }

            string code;
            string message;
            IList<IList<IPXBone>> chains;
            bool closed;
            float cover;
            float thickness;
            if (!TryChains(context, model, out chains, out code, out message)
                || !TryClosed(context, out closed, out code, out message)
                || !TryPositive(context, CoverName, 1f, out cover, out code, out message)
                || !TryPositive(context, ThicknessName, null, out thickness, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            double[][][] middles = chains
                .Select(chain => Sections(chain).Select(section => Halfway(section[0], section[1])).ToArray())
                .ToArray();
            int changed = 0;
            for (int at = 0; at < chains.Count; at++)
            {
                V3[][] sections = Sections(chains[at]);
                for (int step = 0; step < sections.Length; step++)
                {
                    IPXBody body = Holding(model, chains[at][step]);
                    double[] previous = Neighbour(middles, at - 1, step, closed);
                    double[] next = Neighbour(middles, at + 1, step, closed);
                    if (body != null
                        && Fit(body, sections[step][0], sections[step][1], previous, next, cover, thickness))
                    {
                        changed++;
                    }
                }
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { AddedBodiesName, new object[0] },
                    { AddedJointsName, new object[0] },
                    { ChangedName, changed },
                });
        }

        private static bool TryChains(
            McpMethodContext context,
            IPXPmx model,
            out IList<IList<IPXBone>> chains,
            out string code,
            out string message)
        {
            chains = null;
            string shape = ChainsName + " は、ボーンの位置を根元から先端の順に並べた並びを、隣り合う順に2つ以上並べた並びでなければならない。";
            object given;
            context.Params.TryGetValue(ChainsName, out given);
            object[] items = given as object[];
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = shape;

                return false;
            }

            List<IList<IPXBone>> taken = new List<IList<IPXBone>>();
            foreach (object item in items)
            {
                List<int> positions;
                if (!PositionInput.TryMany(
                    item, ChainsName, shape, model.Bone.Count, false, out positions, out code, out message))
                {
                    return false;
                }

                taken.Add(positions.Select(at => model.Bone[at]).ToList());
            }

            code = ToolEnvelope.InvalidArgument;
            if (taken.Count < 2)
            {
                message = shape;

                return false;
            }

            if (taken.SelectMany(chain => chain).Distinct(ReferenceComparer<IPXBone>.Instance).Count()
                != taken.Sum(chain => chain.Count))
            {
                message = ChainsName + " に同じボーンが2度現れる。";

                return false;
            }

            code = null;
            message = null;
            chains = taken;

            return true;
        }

        private static bool TryClosed(McpMethodContext context, out bool closed, out string code, out string message)
        {
            closed = false;
            code = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(ClosedName, out given) || given == null)
            {
                return true;
            }

            if (!(given is bool))
            {
                code = ToolEnvelope.InvalidArgument;
                message = ClosedName + " は真か偽でなければならない。";

                return false;
            }

            closed = (bool)given;

            return true;
        }

        /// <summary><paramref name="fallback"/> が空なら必須にする。</summary>
        private static bool TryPositive(
            McpMethodContext context,
            string name,
            float? fallback,
            out float number,
            out string code,
            out string message)
        {
            number = fallback ?? 0f;
            code = null;
            message = null;
            object given;
            if ((!context.Params.TryGetValue(name, out given) || given == null) && fallback != null)
            {
                return true;
            }

            if (ValueInput.TrySingle(given, out number) && number > 0f)
            {
                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            message = name + " は " + FitBodiesToChains + " のときに渡す、正の有限の数である。";

            return false;
        }

        private static V3[][] Sections(IList<IPXBone> chain)
        {
            return Enumerable.Range(0, chain.Count)
                .Select(at => new[]
                {
                    chain[at].Position,
                    at + 1 < chain.Count ? chain[at + 1].Position : Tip(chain[at]),
                })
                .ToArray();
        }

        private static V3 Tip(IPXBone bone)
        {
            return bone.ToBone != null
                ? bone.ToBone.Position
                : Vectors.Add(bone.Position, bone.ToOffset);
        }

        /// <summary>隣のチェーンの同じ段の中点。チェーンもその段も無いか、有限でなければ空。</summary>
        private static double[] Neighbour(double[][][] middles, int at, int step, bool closed)
        {
            if (closed)
            {
                at = (at + middles.Length) % middles.Length;
            }

            if (at < 0 || at >= middles.Length || step >= middles[at].Length)
            {
                return null;
            }

            double[] middle = middles[at][step];

            return middle.All(value => !double.IsNaN(value) && !double.IsInfinity(value)) ? middle : null;
        }

        private static bool Fit(
            IPXBody body, V3 start, V3 end, double[] previous, double[] next, float cover, float thickness)
        {
            if (!Vectors.Finite(start) || !Vectors.Finite(end) || (previous == null && next == null))
            {
                return false;
            }

            double[] middle = Halfway(start, end);
            double[] along = Difference(Spot(start), Spot(end));
            double length = Length(along);
            double[] across = Difference(next ?? middle, previous ?? middle);
            double[] apart = Cross(across, along);
            if (!(length > 0d) || apart.All(value => value == 0d))
            {
                return false;
            }

            double[] up = Scaled(along, 1d / length);
            double[] side = Cross(along, apart);
            side = Scaled(side, 1d / Length(side));
            double[] normal = Cross(side, up);
            double reach = new[] { previous, next }
                .Where(spot => spot != null)
                .Max(spot => Length(Difference(spot, middle)));
            V3 rotation = RowMatrix.Rows(side, up, normal).ToEulerZxy();
            V3 position = new V3((float)middle[0], (float)middle[1], (float)middle[2]);
            V3 size = new V3((float)(reach / 2d * cover), (float)(length / 2d * cover), thickness);
            if (!Vectors.Finite(rotation) || !Vectors.Finite(position) || !Vectors.Finite(size))
            {
                return false;
            }

            bool same = body.BoxKind == BodyBoxKind.Box
                && Vectors.Same(body.Position, position)
                && Vectors.Same(body.Rotation, rotation)
                && Vectors.Same(body.BoxSize, size);
            body.BoxKind = BodyBoxKind.Box;
            body.Position = position;
            body.Rotation = rotation;
            body.BoxSize = size;

            return !same;
        }

        private static double[] Spot(V3 given)
        {
            return new double[] { given.X, given.Y, given.Z };
        }

        private static double[] Halfway(V3 first, V3 second)
        {
            return new[]
            {
                ((double)first.X + second.X) / 2d,
                ((double)first.Y + second.Y) / 2d,
                ((double)first.Z + second.Z) / 2d,
            };
        }

        private static double[] Difference(double[] to, double[] from)
        {
            return new[] { to[0] - from[0], to[1] - from[1], to[2] - from[2] };
        }

        private static double[] Scaled(double[] given, double by)
        {
            return new[] { given[0] * by, given[1] * by, given[2] * by };
        }

        private static double[] Cross(double[] left, double[] right)
        {
            return new[]
            {
                (left[1] * right[2]) - (left[2] * right[1]),
                (left[2] * right[0]) - (left[0] * right[2]),
                (left[0] * right[1]) - (left[1] * right[0]),
            };
        }

        private static double Dot(double[] left, double[] right)
        {
            return (left[0] * right[0]) + (left[1] * right[1]) + (left[2] * right[2]);
        }

        private static double Length(double[] given)
        {
            return Math.Sqrt(Dot(given, given));
        }

        private static bool Below(IPXBody body, IPXBone root)
        {
            if (body == null || root == null || ReferenceEquals(body.Bone, root))
            {
                return false;
            }

            HashSet<IPXBone> met = new HashSet<IPXBone>(ReferenceComparer<IPXBone>.Instance);
            for (IPXBone bone = body.Bone; bone != null && met.Add(bone); bone = bone.Parent)
            {
                if (ReferenceEquals(bone, root))
                {
                    return true;
                }
            }

            return false;
        }

        private static V3 Shortened(IPXBody body, V3 along, float ratio)
        {
            V3 size = Vectors.Copied(body.BoxSize);
            if (body.BoxKind == BodyBoxKind.Sphere)
            {
                return size;
            }

            RowMatrix frame = RowMatrix.YawPitchRoll(body.Rotation.Y, body.Rotation.X, body.Rotation.Z);
            float[] lined = new[]
            {
                Math.Abs(Vectors.Dot(frame.Transform(new V3(1f, 0f, 0f)), along)),
                Math.Abs(Vectors.Dot(frame.Transform(new V3(0f, 1f, 0f)), along)),
                Math.Abs(Vectors.Dot(frame.Transform(new V3(0f, 0f, 1f)), along)),
            };
            int axis = Array.IndexOf(lined, lined.Max());
            if (body.BoxKind == BodyBoxKind.Capsule && axis != 1)
            {
                return size;
            }

            return new V3(
                axis == 0 ? size.X * ratio : size.X,
                axis == 1 ? size.Y * ratio : size.Y,
                axis == 2 ? size.Z * ratio : size.Z);
        }

        /// <summary>そのボーンを指している剛体のうち先頭。無ければ空。</summary>
        private static IPXBody Holding(IPXPmx model, IPXBone bone)
        {
            return bone == null
                ? null
                : model.Body.FirstOrDefault(body => ReferenceEquals(body.Bone, bone));
        }

        private static IPXJoint Tie(IPXPmxBuilder builder, IPXBody first, IPXBody second)
        {
            IPXJoint joint = builder.Joint();
            joint.Name = second.Name;
            joint.NameE = string.Empty;
            joint.BodyA = first;
            joint.BodyB = second;
            joint.Position = Vectors.Between(first.Position, second.Position);

            return joint;
        }

        /// <summary>
        /// 指した頂点をすべて包む当たりの大きさ。PMXの剛体は、この3つの成分を形ごとに読み分け、
        /// 球は半径を、箱は各軸の半幅を、カプセルは半径と中心線の長さを見る。どの大きさも、単精度へ
        /// 丸めたあとの中心から測る。カプセルの半径は、
        /// 中心線のいちばん近い点から各頂点までの隔たりのうち、いちばん大きいものとする。要る長さを
        /// 単精度へ丸めるときは、下回らない側へ寄せる。どれかの成分が単精度で持てなければ偽を返す。
        /// </summary>
        private static bool TrySized(
            string shape, V3 least, V3 most, V3 middle, IList<IPXVertex> picked, out V3 size)
        {
            if (string.Equals(shape, Box, StringComparison.Ordinal))
            {
                size = new V3(
                    Enough(Reach(least.X, most.X, middle.X)),
                    Enough(Reach(least.Y, most.Y, middle.Y)),
                    Enough(Reach(least.Z, most.Z, middle.Z)));

                return Held(size);
            }

            if (string.Equals(shape, Sphere, StringComparison.Ordinal))
            {
                float radius = Enough(picked.Max(vertex => Apart(middle, vertex.Position, 0d)));
                size = new V3(radius, radius, radius);

                return Held(size);
            }

            double span = (double)most.Y - least.Y;
            float tall = span > float.MaxValue ? float.MaxValue : Enough(span);
            float round = Enough(picked.Max(
                vertex => Apart(middle, vertex.Position, tall / 2d)));
            size = new V3(round, tall, round);

            return Held(size);
        }

        private static double Reach(float least, float most, float middle)
        {
            return Math.Max(
                Math.Abs((double)most - middle), Math.Abs((double)middle - least));
        }

        /// <summary>
        /// 中心の点から頂点までの隔たり。<paramref name="reach"/> を渡すと、その長さだけ軸へ伸ばした
        /// 中心線のいちばん近い点からの隔たりになる。
        /// </summary>
        private static double Apart(V3 middle, V3 spot, double reach)
        {
            double above = Math.Abs((double)spot.Y - middle.Y) - reach;
            double along = above > 0d ? above : 0d;
            double across = (double)spot.X - middle.X;
            double aside = (double)spot.Z - middle.Z;

            return Math.Sqrt((across * across) + (aside * aside) + (along * along));
        }

        /// <summary>
        /// その長さを下回らない、いちばん小さい単精度の値。単精度で持てない長さでは無限大を返す。
        /// </summary>
        private static float Enough(double given)
        {
            float made = (float)given;
            if (made >= given || float.IsInfinity(made))
            {
                return made;
            }

            return BitConverter.ToSingle(
                BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(made), 0) + 1), 0);
        }

        private static bool Held(V3 size)
        {
            return !float.IsInfinity(size.X)
                && !float.IsInfinity(size.Y)
                && !float.IsInfinity(size.Z);
        }

        private static V3 Corner(IList<IPXVertex> picked, Func<float, float, float> pick)
        {
            V3 found = Vectors.Copied(picked[0].Position);
            foreach (IPXVertex vertex in picked)
            {
                found = new V3(
                    pick(found.X, vertex.Position.X),
                    pick(found.Y, vertex.Position.Y),
                    pick(found.Z, vertex.Position.Z));
            }

            return found;
        }

        private static BodyBoxKind Kind(string shape)
        {
            switch (shape)
            {
                case Box:
                    return BodyBoxKind.Box;

                case Capsule:
                    return BodyBoxKind.Capsule;

                default:
                    return BodyBoxKind.Sphere;
            }
        }

        private const float Thickness = 0.5f;

        private static ComposedEditResult Answer(IList<int> bodies, IList<int> joints)
        {
            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { AddedBodiesName, bodies.Cast<object>().ToArray() },
                    { AddedJointsName, joints.Cast<object>().ToArray() },
                });
        }
    }
}
