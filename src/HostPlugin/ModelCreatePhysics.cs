using System;
using System.Collections.Generic;

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

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { BodyFollowBone, BodyPhysics, Joint, BodyAndJoint, BodyAtVertices };
            }
        }

        /// <summary>剛体の当たりの形を受け取る入力の名前。</summary>
        public const string ShapeName = "shape";

        /// <summary>球。</summary>
        public const string Sphere = "sphere";

        /// <summary>箱。</summary>
        public const string Box = "box";

        /// <summary>カプセル。</summary>
        public const string Capsule = "capsule";

        /// <summary>受け取れる当たりの形。スキーマが並べる順。</summary>
        public static IList<string> Shapes
        {
            get
            {
                return new[] { Sphere, Box, Capsule };
            }
        }

        /// <summary>足した剛体の位置を返す項目の名前。</summary>
        public const string AddedBodiesName = "addedBodies";

        /// <summary>足したJointの位置を返す項目の名前。</summary>
        public const string AddedJointsName = "addedJoints";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            throw new NotImplementedException();
        }
    }
}
