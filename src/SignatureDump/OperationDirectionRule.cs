using System;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 操作の向き(読み取り/書き込み)を機械的に決める。読み取りになるのは、取得アクセサーを
    /// 持つプロパティと、戻り値を持ち出力引数を持たない取得の名前で始まるメソッドだけで、
    /// フィールドを含むそれ以外はすべて書き込みとする。書き込みを読み取りと誤って判定しても、
    /// 対になる書き込み行が見つからず効果が導出されなくなるだけで、誤って合格にはならない。
    /// </summary>
    public static class OperationDirectionRule
    {
        public static readonly ReadOnlyCollection<string> ReadMethodPrefixes =
            Array.AsReadOnly(new[] { "Get", "Is", "Has", "Can", "Find", "Search" });

        public static OperationDirection ForMethod(string memberName, string returnType, bool hasOutOrRefParameter)
        {
            throw new NotImplementedException();
        }

        public static OperationDirection ForProperty(bool hasPublicGetter)
        {
            throw new NotImplementedException();
        }

        public static OperationDirection ForOtherMember()
        {
            throw new NotImplementedException();
        }
    }
}
