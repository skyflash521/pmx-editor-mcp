using System;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 行がどの種別を採るかを、行の外の材料から導く。書き手が種別を選ぶのではなく、シグネチャの
    /// 種類と特別規則の表から決まるので、書かれた種別と突き合わせて取り違えを落とす。
    /// </summary>
    public static class RowKindRule
    {
        /// <summary>その行が採る種別。</summary>
        public static ToolMapRowKind Of(MemberKind memberKind, bool assigned)
        {
            if (assigned)
            {
                return ToolMapRowKind.CommonContract;
            }

            switch (memberKind)
            {
                case MemberKind.Event:
                    return ToolMapRowKind.EventBranch;

                case MemberKind.Property:
                case MemberKind.Field:
                    return ToolMapRowKind.SchemaEmbedded;

                case MemberKind.Method:
                case MemberKind.Constructor:
                    return ToolMapRowKind.DirectDispatch;

                default:
                    throw new InvalidOperationException(
                        "種別を導けないメンバーの種類: " + memberKind);
            }
        }
    }
}
