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
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_create_physics";

        /// <summary>ボーンに追従する剛体を、ボーンごとに足す。</summary>
        public const string BodyFollowBone = "bodyFollowBone";

        /// <summary>物理で動く剛体を、ボーンごとに足す。</summary>
        public const string BodyPhysics = "bodyPhysics";

        /// <summary>指した剛体どうしを繋ぐJointを足す。</summary>
        public const string Joint = "joint";

        /// <summary>物理で動く剛体と、親の剛体へ繋ぐJointを足す。</summary>
        public const string BodyAndJoint = "bodyAndJoint";

        /// <summary>指した頂点を包む大きさの剛体を1つ足す。</summary>
        public const string BodyAtVertices = "bodyAtVertices";

        /// <summary>剛体の当たりの形を受け取る入力の名前。</summary>
        public const string ShapeName = "shape";

        /// <summary>球。</summary>
        public const string Sphere = "sphere";

        /// <summary>箱。</summary>
        public const string Box = "box";

        /// <summary>カプセル。</summary>
        public const string Capsule = "capsule";

        /// <summary>足した剛体の位置を返す項目の名前。</summary>
        public const string AddedBodiesName = "addedBodies";

        /// <summary>足したJointの位置を返す項目の名前。</summary>
        public const string AddedJointsName = "addedJoints";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { BodyFollowBone, BodyPhysics, Joint, BodyAndJoint, BodyAtVertices };
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

        /// <summary>ツールを表へ足す。<paramref name="builder"/> は新しい要素を作る相手を返す。</summary>
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
                TargetNames.Element.All,
                ShapeName,
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

            string shape;
            if (!TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    Counted(model, operation),
                    out chosen,
                    out code,
                    out message)
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

                default:
                    return Attached(
                        model, made, chosen.Select(at => model.Bone[at]).ToList(), operation, shape);
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

        /// <summary>その軸で、剛体の中心から遠い側の端までの隔たり。</summary>
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

        /// <summary>その大きさの3つの成分が、どれも単精度で持てる値か。</summary>
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
