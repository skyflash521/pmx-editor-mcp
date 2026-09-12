using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 行がどの種別を採るかを、行の外の材料から導く。種別はシグネチャの種類と特別規則の表から
    /// 決まるので、行は種別を書かない。
    /// </summary>
    public static class RowKindRule
    {
        /// <summary>
        /// 行キーから、その行が採る種別を引く表。公開API列挙に無い行キーは、実在するかどうかを
        /// 能力対応表の照合が見るので、この表には載せない。
        /// </summary>
        public static IDictionary<string, ToolMapRowKind> Resolve(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            ISet<string> embeddedTypes,
            ISet<string> assigned,
            ISet<string> independentTypes,
            ISet<string> handleTypes,
            ISet<string> collections)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (embeddedTypes == null)
            {
                throw new ArgumentNullException(nameof(embeddedTypes));
            }

            if (assigned == null)
            {
                throw new ArgumentNullException(nameof(assigned));
            }

            if (independentTypes == null)
            {
                throw new ArgumentNullException(nameof(independentTypes));
            }

            if (handleTypes == null)
            {
                throw new ArgumentNullException(nameof(handleTypes));
            }

            if (collections == null)
            {
                throw new ArgumentNullException(nameof(collections));
            }

            Dictionary<string, ToolMapRowKind> kinds =
                new Dictionary<string, ToolMapRowKind>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows)
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    continue;
                }

                string value = TypeDefinitionName.OfElement(
                    ValueTypeName.Contained(signature.ValueType));
                kinds.Add(row.SignatureKey, Of(
                    signature.MemberKind,
                    assigned.Contains(row.SignatureKey),
                    embeddedTypes.Contains(
                        TypeDefinitionName.OfElement(signature.DeclaringType)),
                    independentTypes.Contains(value),
                    handleTypes.Contains(value) && !collections.Contains(row.SignatureKey)));
            }

            return new ReadOnlyDictionary<string, ToolMapRowKind>(kinds);
        }

        /// <summary>
        /// その行が採る種別。<paramref name="embedded"/> は、宣言型が独立したツールを持たない役割
        /// (イベント引数型・DTO型)かどうか。<paramref name="reaches"/> は、値の型が並びの印を
        /// 外した先で独立したツールを持つ役割の型かどうか。<paramref name="handed"/> は、値が
        /// ハンドルで指す型の実体で、かつ要素を並べるリストではないかどうか。
        /// </summary>
        public static ToolMapRowKind Of(
            MemberKind memberKind, bool assigned, bool embedded, bool reaches, bool handed)
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
                    if (!reaches)
                    {
                        return ToolMapRowKind.SchemaEmbedded;
                    }

                    return handed
                        ? ToolMapRowKind.DirectDispatch
                        : ToolMapRowKind.RoleAccess;

                case MemberKind.Constructor:
                    return embedded
                        ? ToolMapRowKind.SchemaEmbedded
                        : ToolMapRowKind.DirectDispatch;

                case MemberKind.Method:
                    return ToolMapRowKind.DirectDispatch;

                default:
                    throw new InvalidOperationException(
                        "種別を導けないメンバーの種類: " + memberKind);
            }
        }
    }
}
