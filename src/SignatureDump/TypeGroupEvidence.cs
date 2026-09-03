using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型の担当群の機械的な根拠。台帳の行がどの型とどのシグネチャを担当するかを解決し、その行の担当を
    /// 型ごとに集める。
    /// </summary>
    public static class TypeGroupEvidence
    {
        /// <summary>
        /// 型の名前から、その型を担当する行の担当を引ける表。数えるのは、その型を名前で指す行と、
        /// その型が宣言するシグネチャを担当する行である。型の名前は型役割表と同じ、型引数の数を書く形に
        /// そろえる。担当を持たない行——分類が提供でない行——は数えないので、そういう行だけが担当する型は
        /// 空の集合を持つ。どちらでも担当されない型は表に現れない。
        /// </summary>
        public static IDictionary<string, ISet<CapabilityOwner>> OwnersByType(
            IList<CapabilityRecord> ledger, InventoryRecord inventory)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            Dictionary<string, CapabilityOwner> byId =
                new Dictionary<string, CapabilityOwner>(StringComparer.Ordinal);
            foreach (CapabilityRecord row in ledger)
            {
                byId[row.Id] = row.Owner;
            }

            LedgerPopulation population = LedgerPopulation.Resolve(ledger, inventory);
            Dictionary<string, ISet<CapabilityOwner>> owners =
                new Dictionary<string, ISet<CapabilityOwner>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ISet<string>> pair in population.NamedTypes)
            {
                Add(owners, byId, pair.Key, pair.Value);
            }

            foreach (SignatureRecord signature in inventory.Signatures)
            {
                ISet<string> ids;
                if (population.Owners.TryGetValue(signature.Key, out ids))
                {
                    Add(owners, byId, signature.DeclaringType, ids);
                }
            }

            return new ReadOnlyDictionary<string, ISet<CapabilityOwner>>(owners);
        }

        private static void Add(
            IDictionary<string, ISet<CapabilityOwner>> owners,
            IDictionary<string, CapabilityOwner> byId,
            string typeName,
            IEnumerable<string> ids)
        {
            string name = TypeDefinitionName.Of(typeName);
            ISet<CapabilityOwner> found;
            if (!owners.TryGetValue(name, out found))
            {
                found = new HashSet<CapabilityOwner>();
                owners[name] = found;
            }

            foreach (string id in ids)
            {
                CapabilityOwner owner = byId[id];
                if (owner != CapabilityOwner.None)
                {
                    found.Add(owner);
                }
            }
        }
    }
}
