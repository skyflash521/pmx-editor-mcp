using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 提供対象のシグネチャと能力対応表の行が、群をまたいで過不足なく対応することを確かめる。行の
    /// 中身を問う照合は別に在り、ここが見るのは母集合との突き合わせだけである。
    /// </summary>
    public static class MapCoverageGate
    {
        /// <summary>過不足があれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ISet<string> provided,
            IDictionary<string, SignatureRecord> signatures,
            ISet<string> embeddedTypes,
            ToolMap map)
        {
            if (provided == null)
            {
                throw new ArgumentNullException(nameof(provided));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (embeddedTypes == null)
            {
                throw new ArgumentNullException(nameof(embeddedTypes));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            HashSet<string> rows = new HashSet<string>(
                map.Rows.Select(r => r.SignatureKey), StringComparer.Ordinal);
            Require(
                "行を持たない提供対象がある",
                Needing(provided, signatures, embeddedTypes).Where(k => !rows.Contains(k)));
            Require(
                "提供対象でない行がある",
                rows.Where(k => !provided.Contains(k) && !Embedded(signatures, embeddedTypes, k)));
        }

        /// <summary>
        /// 独立したツールを持たない役割の型が、入出力へ埋め込む項目か。この型は提供対象の引数と
        /// して現れるだけなので、その項目の行は提供対象に無い。
        /// </summary>
        private static bool Embedded(
            IDictionary<string, SignatureRecord> signatures,
            ISet<string> embeddedTypes,
            string key)
        {
            SignatureRecord signature;

            return signatures.TryGetValue(key, out signature)
                && embeddedTypes.Contains(TypeDefinitionName.OfElement(signature.DeclaringType))
                && (signature.MemberKind == MemberKind.Property
                    || signature.MemberKind == MemberKind.Field);
        }

        /// <summary>
        /// 行を持たなければならない提供対象。次の2つは数えない。独立したツールを持たない役割の型で
        /// 行になるのは入出力へ埋め込む項目だけなので、その型のメソッドとコンストラクタは数えない。
        /// 値の型が型引数そのもののメンバーは、実引数が決まるまで写す表現が決まらないので数えない。
        /// </summary>
        private static IEnumerable<string> Needing(
            ISet<string> provided,
            IDictionary<string, SignatureRecord> signatures,
            ISet<string> embeddedTypes)
        {
            foreach (string key in provided)
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(key, out signature))
                {
                    yield return key;
                    continue;
                }

                if (embeddedTypes.Contains(TypeDefinitionName.OfElement(signature.DeclaringType))
                    && signature.MemberKind != MemberKind.Property
                    && signature.MemberKind != MemberKind.Field)
                {
                    continue;
                }

                if (signature.ValueTypeIsTypeArgument)
                {
                    continue;
                }

                yield return key;
            }
        }

        private static void Require(string what, IEnumerable<string> keys)
        {
            string[] found = keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            if (found.Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                what + "(" + found.Length + "件): " + string.Join("・", found.Take(20))
                    + (found.Length > 20 ? "・ほか" : string.Empty));
        }
    }
}
