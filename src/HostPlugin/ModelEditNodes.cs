using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 表示枠への一括登録と、表情枠の正規化を行うツール。
    /// </summary>
    public static class ModelEditNodes
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_nodes";

        /// <summary>どの枠にも載っていないボーンを、指した枠の末尾へ足す。</summary>
        public const string RegisterUnlistedBones = "registerUnlistedBones";

        /// <summary>どの枠にも載っていないモーフを、指した枠の末尾へ足す。</summary>
        public const string RegisterUnlistedMorphs = "registerUnlistedMorphs";

        /// <summary>表情の枠の中身を、モーフの並びの順にそろえる。</summary>
        public const string NormalizeExpressionNode = "normalizeExpressionNode";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { RegisterUnlistedBones, RegisterUnlistedMorphs, NormalizeExpressionNode };
            }
        }

        /// <summary>足した枠の中身の数を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>変えた枠の数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit)
        {
            throw new NotImplementedException();
        }
    }
}
