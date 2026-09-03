using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 所有の経路が、列挙のメンバーの並びとしてつながっていることを検査する。段の意味は列挙から
    /// 決まるので、書いた経路が実在の辿り方かどうかはここで確かめられる。
    /// </summary>
    public static class OwnerPathGate
    {
        /// <summary>
        /// 所有の経路が次を満たすことを求める。最初の段の宣言型が所有の根であること。各段の宣言型が
        /// 一つ前の段が指す型であること。リストの段が表の所有するリストであること。単数の段が指す型が
        /// どの所有リストの要素でもないこと。最後の段がその所有リスト自身であること。あわせて所有の根が
        /// どの所有リストの要素でもないことを求める。満たさなければ
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        public static void Require(
            IList<ElementCollectionRecord> records,
            IDictionary<string, SignatureRecord> signatures,
            IEnumerable<string> ownershipRoots)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (ownershipRoots == null)
            {
                throw new ArgumentNullException(nameof(ownershipRoots));
            }

            IList<ElementCollectionRecord> owning = records.Where(r => r.Owns).ToList();
            ISet<string> owned = new HashSet<string>(
                owning.Select(r => ElementTypeName(Stage(signatures, r.SignatureKey))),
                StringComparer.Ordinal);
            ISet<string> lists = new HashSet<string>(
                owning.Select(r => r.SignatureKey), StringComparer.Ordinal);
            ISet<string> roots = new HashSet<string>(ownershipRoots, StringComparer.Ordinal);
            foreach (string root in roots.Where(owned.Contains)
                .OrderBy(n => n, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("所有の根が所有される型である: " + root);
            }

            foreach (ElementCollectionRecord record in owning)
            {
                RequireChain(record, signatures, owned, lists, roots);
            }
        }

        private static void RequireChain(
            ElementCollectionRecord record,
            IDictionary<string, SignatureRecord> signatures,
            ISet<string> owned,
            ISet<string> lists,
            ISet<string> roots)
        {
            IList<string> path = record.OwnerPath;
            if (!string.Equals(path[path.Count - 1], record.SignatureKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "所有の経路がその所有リストで終わっていない: " + record.SignatureKey
                        + "(末尾: " + path[path.Count - 1] + ")");
            }

            string above = null;
            foreach (string stage in path)
            {
                SignatureRecord signature = Stage(signatures, stage);
                string declaring = TypeDefinitionName.Of(signature.DeclaringType);
                if (above == null)
                {
                    if (!roots.Contains(declaring))
                    {
                        throw new InvalidOperationException(
                            "所有の経路が所有の根から始まっていない: " + record.SignatureKey
                                + "(" + declaring + ")");
                    }
                }
                else if (!string.Equals(above, declaring, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "所有の経路の段がつながっていない: " + record.SignatureKey
                            + "(" + above + " のあとに " + stage + ")");
                }

                RequireStageOwns(record, signature, stage, owned, lists);
                above = ElementTypeName(signature);
            }
        }

        /// <summary>
        /// 段が所有の一歩であることを求める。リストの段は表が所有するリストでなければならず、単数の段は
        /// どの所有リストの要素でもない型を指さなければならない。
        /// </summary>
        private static void RequireStageOwns(
            ElementCollectionRecord record,
            SignatureRecord signature,
            string stage,
            ISet<string> owned,
            ISet<string> lists)
        {
            if (IsList(signature))
            {
                if (!lists.Contains(stage))
                {
                    throw new InvalidOperationException(
                        "所有しないリストの段が在る: " + record.SignatureKey + "(" + stage + ")");
                }

                return;
            }

            string value = ElementTypeName(signature);
            if (owned.Contains(value))
            {
                throw new InvalidOperationException(
                    "所有される型を指す単数の段が在る: " + record.SignatureKey
                        + "(" + stage + " が " + value + " を指す)");
            }
        }

        private static SignatureRecord Stage(
            IDictionary<string, SignatureRecord> signatures, string key)
        {
            SignatureRecord signature;
            if (!signatures.TryGetValue(key, out signature))
            {
                throw new InvalidOperationException("提供対象に無い段が在る: " + key);
            }

            if (signature.MemberKind != MemberKind.Property
                || !signature.CanRead
                || signature.Parameters.Count != 0)
            {
                throw new InvalidOperationException(
                    "引数の無い取得プロパティでない段が在る: " + key);
            }

            return signature;
        }

        private static bool IsList(SignatureRecord signature)
        {
            return signature.ValueType.StartsWith(ListHead, StringComparison.Ordinal)
                && signature.ValueType.EndsWith(">", StringComparison.Ordinal);
        }

        /// <summary>その段が指す型。リストなら要素の型、そうでなければ値の型。</summary>
        private static string ElementTypeName(SignatureRecord signature)
        {
            string value = signature.ValueType;

            return TypeDefinitionName.Of(
                IsList(signature)
                    ? value.Substring(ListHead.Length, value.Length - ListHead.Length - 1)
                    : value);
        }

        private const string ListHead = "System.Collections.Generic.IList<";
    }
}
