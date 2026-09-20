using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 視点の回転の中心を、指した要素から決めるツール。
    /// </summary>
    public static class ViewSetCameraRotateCenter
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_set_camera_rotate_center";

        /// <summary>選んだ頂点の重心を中心にする。</summary>
        public const string Vertices = "vertices";

        /// <summary>選んだボーンの重心を中心にする。</summary>
        public const string Bones = "bones";

        /// <summary>選んだ面の重心を中心にする。</summary>
        public const string Face = "face";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Vertices, Bones, Face };
            }
        }

        /// <summary>決めた中心の座標を返す項目の名前。</summary>
        public const string CentreName = "centre";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen)
        {
            throw new NotImplementedException();
        }
    }
}
