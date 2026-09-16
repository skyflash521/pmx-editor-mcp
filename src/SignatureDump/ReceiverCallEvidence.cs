using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 呼び出しの受け手をハンドルで要るツールへ、そのハンドルを得るまでに順に呼ぶツールの列を
    /// 与える。列は根——受け手を渡さずに呼べるツール——から始まり、次の段は前の段が出した
    /// ハンドルを受け取る。段数に上限は置かない。
    /// 同じ相手へ届く列が2つ以上あるときは、段数が少ないものを採り、段数が並ぶときはツールの
    /// 名前を先頭から綴りの順で比べて先のものを採る——どれを採るかを決めないと、入力の並び
    /// しだいで組み立てる検査が変わる。
    /// </summary>
    public static class ReceiverCallEvidence
    {
        /// <summary>
        /// 型の名前から、その型の実体を1つ得るまでに順に呼ぶツールの列へ。派生型へ至る列は、
        /// その型が継承・実装する型の名前からも引ける——基底型の受け手には派生型の実体を渡せる。
        /// 根から辿り着けない型は持たない。
        /// </summary>
        public static IDictionary<string, IList<string>> ByType(
            InventoryRecord inventory,
            ToolMap map,
            IDictionary<string, string> toolsByRow,
            ToolSchemaTable schemas)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 受け手をハンドルで要るツールの名前から、そのハンドルを得るまでに順に呼ぶツールの列へ。
        /// 自分の行を持たない集約のツールも、並べる要素を所有する型のハンドルを要るので同じ列で
        /// 引ける。受け手を要さないツールと、受け手へ至る列の無いツールは持たない。
        /// <paramref name="roles"/> は担当群を解いた型役割表である。
        /// </summary>
        public static IDictionary<string, IList<string>> ByTool(
            InventoryRecord inventory,
            ToolMap map,
            TypeRoleTable roles,
            IDictionary<string, string> toolsByRow,
            ToolSchemaTable schemas)
        {
            throw new NotImplementedException();
        }
    }
}
