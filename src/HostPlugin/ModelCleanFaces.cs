using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指した材質から、不正な面と重なった面を落とすツール。3つの頂点が揃っていない面を不正とし、
    /// 同じ3頂点を指す面が2つ以上あるとき、最初の1つだけを残す。
    /// </summary>
    public static class ModelCleanFaces
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_clean_faces";

        /// <summary>3つの頂点が揃っていない面を落とす。</summary>
        public const string Invalid = "invalid";

        /// <summary>モデル全体で同じ3頂点を指す面を、最初の1つだけ残して落とす。</summary>
        public const string Duplicate = "duplicate";

        /// <summary>材質ごとに同じ3頂点を指す面を、最初の1つだけ残して落とす。</summary>
        public const string DuplicateInMaterial = "duplicateInMaterial";

        /// <summary>落とした面の数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { Invalid, Duplicate, DuplicateInMaterial };
            }
        }

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
