using System;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 列挙結果をJSONへ書き出す。同じ入力からは常に同じバイト列になり、配列の要素は1行ずつに
    /// 分かれるので、行単位の差分で変化を追える。
    /// </summary>
    public static class InventoryJson
    {
        /// <summary>末尾に改行を1つ置く。</summary>
        public static string Write(InventoryRecord inventory)
        {
            throw new NotImplementedException();
        }
    }
}
