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
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_copy_from_reference";

        /// <summary>剛体の名前を、その剛体が指すボーンの名前にする。</summary>
        public const string BodyNameFromBone = "bodyNameFromBone";

        /// <summary>Jointの名前を、そのJointが繋ぐ1つめの剛体の名前にする。</summary>
        public const string JointNameFromBodyA = "jointNameFromBodyA";

        /// <summary>Jointの名前を、そのJointが繋ぐ2つめの剛体の名前にする。</summary>
        public const string JointNameFromBodyB = "jointNameFromBodyB";

        /// <summary>Jointの位置を、繋ぐ剛体が指すボーンの位置にする。</summary>
        public const string JointPositionFromBone = "jointPositionFromBone";

        public const string JointPositionFromSameNameBone = "jointPositionFromSameNameBone";

        /// <summary>変えた要素の数を返す項目の名前。</summary>
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
                };
            }
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
                    out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            int changed = bodies
                ? chosen.Select(at => model.Body[at]).Count(Named)
                : chosen.Select(at => model.Joint[at]).Count(joint => Taken(model, joint, operation));

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ChangedName, changed },
                });
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
    }
}
