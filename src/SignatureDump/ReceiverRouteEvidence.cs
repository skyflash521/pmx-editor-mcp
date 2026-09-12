using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 自分自身へ至る道を持たない型が、受け手を得るのに通る型の候補。台帳がその型のメンバーを、
    /// それを実装する型の名前で指しているとき、その実装する型が候補になる——実装する型の実体は、
    /// 実装される型としても受け取れる。メンバーを名指しした行だけを根拠にするのは、その行こそが
    /// そのメンバーをどの型のものとして扱うかを述べているからで、型の名前だけを書く行は
    /// 継ぐメンバーを覆うだけで、どの型を通るかを述べていない。
    /// </summary>
    public static class ReceiverRouteEvidence
    {
        /// <summary>
        /// 型の名前から、その型が宣言するメンバーを名前で指す行が頭に書いた、その型を実装する型。
        /// 自分自身を書いた行は入らない。どれを通るかは道が要るときに決まるので、ここでは候補のまま
        /// 返す。
        /// </summary>
        public static IDictionary<string, ISet<string>> Candidates(
            InventoryRecord inventory, IList<CapabilityRecord> ledger)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            LedgerPopulation population = LedgerPopulation.Resolve(ledger, inventory);
            IDictionary<string, IList<string>> bases = TypeRoleEvidence.BaseTypes(inventory);
            Dictionary<string, ISet<string>> candidates =
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (SignatureRecord signature in inventory.Signatures)
            {
                ISet<string> written;
                if (!population.WrittenOwners.TryGetValue(signature.Key, out written))
                {
                    continue;
                }

                string declaring = TypeDefinitionName.Of(signature.DeclaringType);
                foreach (string name in written.Select(TypeDefinitionName.Of)
                    .Where(n => !string.Equals(n, declaring, StringComparison.Ordinal)))
                {
                    IList<string> inherited;
                    if (bases.TryGetValue(name, out inherited)
                        && inherited.Contains(declaring, StringComparer.Ordinal))
                    {
                        Add(candidates, declaring, name);
                    }
                }
            }

            return new ReadOnlyDictionary<string, ISet<string>>(candidates);
        }

        private static void Add(
            IDictionary<string, ISet<string>> candidates, string type, string name)
        {
            ISet<string> found;
            if (!candidates.TryGetValue(type, out found))
            {
                found = new HashSet<string>(StringComparer.Ordinal);
                candidates[type] = found;
            }

            found.Add(name);
        }
    }
}
