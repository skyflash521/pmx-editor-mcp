using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指したモーフの結合・まとめ・分離と、いまの材質の値からの作成を行うツール。
    /// </summary>
    public static class ModelEditMorphs
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_edit_morphs";

        /// <summary>同じ名前のモーフを先頭の1つへまとめる。</summary>
        public const string MergeSameName = "mergeSameName";

        /// <summary>同じ種類のモーフを先頭の1つへまとめる。</summary>
        public const string MergeSameKind = "mergeSameKind";

        /// <summary>同じ種類のモーフを、同じ相手へのオフセットを足しながら1つへまとめる。</summary>
        public const string MergeSameKindAdd = "mergeSameKindAdd";

        /// <summary>指したモーフを呼ぶグループモーフを1つ足す。</summary>
        public const string GroupInto = "groupInto";

        /// <summary>指したモーフを切り替えるフリップモーフを1つ足す。</summary>
        public const string FlipInto = "flipInto";

        /// <summary>頂点モーフを、動く頂点のまとまりごとの別々のモーフへ分ける。</summary>
        public const string SplitVertices = "splitVertices";

        /// <summary>いまの材質の値を写した材質モーフを1つ足す。</summary>
        public const string MaterialFromCurrent = "materialFromCurrent";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get
            {
                return new[] { MergeSameName, MergeSameKind, MergeSameKindAdd, GroupInto, FlipInto, SplitVertices, MaterialFromCurrent };
            }
        }

        /// <summary>足したモーフの名前を受け取る入力の名前。</summary>
        public const string NameName = "name";

        /// <summary>足したモーフの位置を返す項目の名前。</summary>
        public const string AddedName = "added";

        /// <summary>変えたモーフの数を返す項目の名前。</summary>
        public const string ChangedName = "changed";

        /// <summary>消えたモーフの数を返す項目の名前。</summary>
        public const string RemovedName = "removed";

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            throw new NotImplementedException();
        }
    }
}
