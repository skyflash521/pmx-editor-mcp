using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型ごとの担当群を決める。台帳がその型へ担当を一つだけ与えているなら台帳が決め、与えていない
    /// ——担当が複数ある型と、担当を一つも与えられていない型——ときだけ表の値が決める。
    /// </summary>
    public static class TypeGroupRule
    {
        /// <summary>
        /// 表の担当群を解決した表。台帳が決める型に書いた値が在るか、決めない型に値が無ければ
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        public static TypeRoleTable Resolve(
            TypeRoleTable table, IDictionary<string, ISet<CapabilityOwner>> ledgerOwners)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (ledgerOwners == null)
            {
                throw new ArgumentNullException(nameof(ledgerOwners));
            }

            List<TypeRoleRecord> resolved = new List<TypeRoleRecord>();
            foreach (TypeRoleRecord record in table.Types)
            {
                resolved.Add(TypeRoleRecord.HasIndependentTool(record.Role)
                    ? WithGroup(record, Decided(record.TypeName, ledgerOwners))
                    : record);
            }

            return new TypeRoleTable(resolved, table.Issuances, table.Collections);
        }

        /// <summary>台帳がその型へ一つだけ与えている担当。与えていなければ null。</summary>
        private static CapabilityOwner? Decided(
            string typeName, IDictionary<string, ISet<CapabilityOwner>> ledgerOwners)
        {
            ISet<CapabilityOwner> owners;

            return ledgerOwners.TryGetValue(typeName, out owners) && owners.Count == 1
                ? owners.First()
                : (CapabilityOwner?)null;
        }

        private static TypeRoleRecord WithGroup(TypeRoleRecord record, CapabilityOwner? decided)
        {
            if (decided.HasValue)
            {
                if (record.Group != CapabilityOwner.None)
                {
                    throw new InvalidOperationException(
                        "台帳が担当を決める型は担当群を書かない: " + record.TypeName);
                }

                return Copy(record, decided.Value);
            }

            if (record.Group == CapabilityOwner.None)
            {
                throw new InvalidOperationException(
                    "台帳が担当を決めない型は担当群を書く: " + record.TypeName);
            }

            return record;
        }

        private static TypeRoleRecord Copy(TypeRoleRecord record, CapabilityOwner group)
        {
            return new TypeRoleRecord(
                record.TypeName,
                record.Role,
                record.Basis,
                record.ElementNoun,
                record.ElementNounPlural,
                group);
        }
    }
}
