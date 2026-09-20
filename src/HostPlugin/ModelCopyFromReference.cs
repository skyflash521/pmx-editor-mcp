using System;
using System.Collections.Generic;

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

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { BodyNameFromBone, JointNameFromBodyA, JointNameFromBodyB, JointPositionFromBone };
            }
        }

        /// <summary>変えた要素の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
