using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 剛体とJointへ、指している相手の名前や位置を写すツール。
    /// </summary>
    public static class ModelCopyFromReference
    {
        public const string ToolName = "model_copy_from_reference";

        public const string BodyNameFromBone = "bodyNameFromBone";

        public const string JointNameFromBodyA = "jointNameFromBodyA";

        public const string JointNameFromBodyB = "jointNameFromBodyB";

        public const string JointPositionFromBone = "jointPositionFromBone";

        public const string JointPositionFromSameNameBone = "jointPositionFromSameNameBone";

        public const string JointRotationFromBodyA = "jointRotationFromBodyA";

        public const string JointRotationFromBodyB = "jointRotationFromBodyB";

        public const string JointRotationBetweenBodies = "jointRotationBetweenBodies";

        public const string ChangedName = "changed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[]
                {
                    BodyNameFromBone,
                    JointNameFromBodyA,
                    JointNameFromBodyB,
                    JointPositionFromBone,
                    JointPositionFromSameNameBone,
                    JointRotationFromBodyA,
                    JointRotationFromBodyB,
                    JointRotationBetweenBodies,
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
                TargetNames.Element.Ranges,
                TargetNames.Element.All,
                TargetNames.Element.Selected,
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
                    context, Operations, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            bool bodies = string.Equals(operation, BodyNameFromBone, StringComparison.Ordinal);
            if (!TargetInput.TryPositions(
                    context.Params,
                    TargetNames.Element,
                    bodies ? model.Body.Count : model.Joint.Count,
                    out chosen,
                    out code,
                    out message,
                    context.Screen.Pick(
                        bodies ? ElementKinds.Body : ElementKinds.Joint,
                        bodies ? model.Body.Count : model.Joint.Count)))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int changed = bodies
                ? chosen.Select(at => model.Body[at]).Count(Named)
                : chosen.Select(at => model.Joint[at]).Count(joint => Taken(model, joint, operation));

            return ComposedEditResult.CompleteRewriting(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                },
                new[] { bodies ? ElementKinds.Body : ElementKinds.Joint });
        }

        /// <summary>剛体の名前を、指すボーンの名前にする。変えたなら真を返す。</summary>
        private static bool Named(IPXBody body)
        {
            if (body.Bone == null || string.Equals(body.Name, body.Bone.Name, StringComparison.Ordinal))
            {
                return false;
            }

            body.Name = body.Bone.Name;

            return true;
        }

        private static bool Taken(IPXPmx model, IPXJoint joint, string operation)
        {
            if (string.Equals(operation, JointRotationFromBodyA, StringComparison.Ordinal))
            {
                return joint.BodyA != null && Turned(joint, joint.BodyA.Rotation);
            }

            if (string.Equals(operation, JointRotationFromBodyB, StringComparison.Ordinal))
            {
                return joint.BodyB != null && Turned(joint, joint.BodyB.Rotation);
            }

            if (string.Equals(operation, JointRotationBetweenBodies, StringComparison.Ordinal))
            {
                V3 halfway = joint.BodyA == null || joint.BodyB == null
                    ? null
                    : Halfway(joint.BodyA.Rotation, joint.BodyB.Rotation);

                return halfway != null && Turned(joint, halfway);
            }

            if (string.Equals(operation, JointPositionFromSameNameBone, StringComparison.Ordinal))
            {
                IPXBone named = model.Bone.FirstOrDefault(
                    bone => string.Equals(bone.Name, joint.Name, StringComparison.Ordinal));
                if (named == null || Vectors.Same(joint.Position, named.Position))
                {
                    return false;
                }

                joint.Position = Vectors.Copied(named.Position);

                return true;
            }

            IPXBody held = string.Equals(operation, JointNameFromBodyB, StringComparison.Ordinal)
                ? joint.BodyB
                : joint.BodyA;
            if (held == null)
            {
                return false;
            }

            if (!string.Equals(operation, JointPositionFromBone, StringComparison.Ordinal))
            {
                if (string.Equals(joint.Name, held.Name, StringComparison.Ordinal))
                {
                    return false;
                }

                joint.Name = held.Name;

                return true;
            }

            if (held.Bone == null || Vectors.Same(joint.Position, held.Bone.Position))
            {
                return false;
            }

            joint.Position = Vectors.Copied(held.Bone.Position);

            return true;
        }

        private static bool Turned(IPXJoint joint, V3 rotation)
        {
            if (Vectors.Same(joint.Rotation, rotation))
            {
                return false;
            }

            joint.Rotation = Vectors.Copied(rotation);

            return true;
        }

        /// <summary>
        /// 2つの回転の角が表す向きを結ぶ最短の弧の中点の角。2つが正反対で中点が一つに決まらなければ空。
        /// </summary>
        private static V3 Halfway(V3 first, V3 second)
        {
            if (Vectors.Same(first, second))
            {
                return Vectors.Copied(first);
            }

            double[] one = Quaternion(first);
            double[] two = Quaternion(second);
            double dot = Enumerable.Range(0, 4).Sum(at => one[at] * two[at]);
            if (dot == 0d)
            {
                return null;
            }

            double sign = dot < 0d ? -1d : 1d;
            double[] sum = Enumerable.Range(0, 4).Select(at => one[at] + (sign * two[at])).ToArray();
            double length = Math.Sqrt(sum.Sum(part => part * part));
            double[] middle = sum.Select(part => part / length).ToArray();
            V3 halfway = RowMatrix.Rows(
                    Rotated(middle, new[] { 1d, 0d, 0d }),
                    Rotated(middle, new[] { 0d, 1d, 0d }),
                    Rotated(middle, new[] { 0d, 0d, 1d }))
                .ToEulerZxy();

            return Vectors.Finite(halfway) ? halfway : null;
        }

        /// <summary>
        /// 剛体とJointの回転の角の四元数 (w, x, y, z)。角は Z・X・Y の軸の順に回す。
        /// </summary>
        private static double[] Quaternion(V3 rotation)
        {
            double[] aroundZ = { Math.Cos(rotation.Z / 2d), 0d, 0d, Math.Sin(rotation.Z / 2d) };
            double[] aroundX = { Math.Cos(rotation.X / 2d), Math.Sin(rotation.X / 2d), 0d, 0d };
            double[] aroundY = { Math.Cos(rotation.Y / 2d), 0d, Math.Sin(rotation.Y / 2d), 0d };

            return Product(aroundY, Product(aroundX, aroundZ));
        }

        private static double[] Product(double[] left, double[] right)
        {
            return new[]
            {
                (left[0] * right[0]) - (left[1] * right[1]) - (left[2] * right[2]) - (left[3] * right[3]),
                (left[0] * right[1]) + (left[1] * right[0]) + (left[2] * right[3]) - (left[3] * right[2]),
                (left[0] * right[2]) - (left[1] * right[3]) + (left[2] * right[0]) + (left[3] * right[1]),
                (left[0] * right[3]) + (left[1] * right[2]) - (left[2] * right[1]) + (left[3] * right[0]),
            };
        }

        private static double[] Rotated(double[] turn, double[] vector)
        {
            double[] conjugate = { turn[0], -turn[1], -turn[2], -turn[3] };
            double[] moved = Product(Product(turn, new[] { 0d, vector[0], vector[1], vector[2] }), conjugate);

            return new[] { moved[1], moved[2], moved[3] };
        }
    }
}
