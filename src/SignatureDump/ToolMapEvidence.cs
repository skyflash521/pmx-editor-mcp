using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表と突き合わせる側を、台帳・除外一覧・公開API列挙・型役割表から導いたもの。
    /// </summary>
    public sealed class ToolMapEvidence
    {
        /// <summary>台帳の備考のうち、契約注記の本文が始まる位置を示す固定の接頭辞。</summary>
        private const string NotePrefix = "契約注記:";

        /// <summary>反映を一部に限るときの指定が引く列挙型。</summary>
        private const string UpdateKindType = "PEPlugin.Pmx.PmxUpdateObject";

        public ToolMapEvidence(
            ISet<string> provided,
            IDictionary<string, ISet<string>> owners,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, DangerKind> dangers,
            IDictionary<string, string> notes,
            ISet<string> updateKinds,
            ISet<string> elementNouns,
            ISet<string> typeNames)
        {
            if (provided == null)
            {
                throw new ArgumentNullException(nameof(provided));
            }

            if (owners == null)
            {
                throw new ArgumentNullException(nameof(owners));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (dangers == null)
            {
                throw new ArgumentNullException(nameof(dangers));
            }

            if (notes == null)
            {
                throw new ArgumentNullException(nameof(notes));
            }

            if (updateKinds == null)
            {
                throw new ArgumentNullException(nameof(updateKinds));
            }

            if (elementNouns == null)
            {
                throw new ArgumentNullException(nameof(elementNouns));
            }

            if (typeNames == null)
            {
                throw new ArgumentNullException(nameof(typeNames));
            }

            Provided = provided;
            Owners = new ReadOnlyDictionary<string, ISet<string>>(
                new Dictionary<string, ISet<string>>(owners, StringComparer.Ordinal));
            Signatures = new ReadOnlyDictionary<string, SignatureRecord>(
                new Dictionary<string, SignatureRecord>(signatures, StringComparer.Ordinal));
            Dangers = new ReadOnlyDictionary<string, DangerKind>(
                new Dictionary<string, DangerKind>(dangers, StringComparer.Ordinal));
            Notes = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(notes, StringComparer.Ordinal));
            UpdateKinds = updateKinds;
            ElementNouns = elementNouns;
            TypeNames = typeNames;
        }

        /// <summary>提供対象のシグネチャの行キー。</summary>
        public ISet<string> Provided { get; }

        /// <summary>行キーから、それを指す提供能力のIDを引く表。</summary>
        public IDictionary<string, ISet<string>> Owners { get; }

        /// <summary>行キーから公開API列挙の記録を引く表。</summary>
        public IDictionary<string, SignatureRecord> Signatures { get; }

        /// <summary>危険操作に当たる行キーと、その種別。</summary>
        public IDictionary<string, DangerKind> Dangers { get; }

        /// <summary>契約注記を持つ提供能力のIDと、その注記の本文。</summary>
        public IDictionary<string, string> Notes { get; }

        /// <summary>反映を一部に限るときに指せる列挙子の名前。</summary>
        public ISet<string> UpdateKinds { get; }

        /// <summary>用意の操作が足せる要素型の名前。</summary>
        public ISet<string> ElementNouns { get; }

        /// <summary>サンプル値を引ける型の名前。</summary>
        public ISet<string> TypeNames { get; }

        /// <summary>導けないものがあれば <see cref="InvalidOperationException"/>。</summary>
        public static ToolMapEvidence Collect(
            IList<CapabilityRecord> ledger,
            IList<ExcludedSignatureRecord> excluded,
            InventoryRecord inventory,
            TypeRoleTable roles)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (excluded == null)
            {
                throw new ArgumentNullException(nameof(excluded));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            IEnumerable<TypeRecord> types = inventory.Types.Concat(inventory.ReferencedTypes);
            TypeRecord updateKind = types.FirstOrDefault(
                t => string.Equals(t.Name, UpdateKindType, StringComparison.Ordinal));
            if (updateKind == null)
            {
                throw new InvalidOperationException(
                    "反映の指定の列挙型が公開API列挙に無い: " + UpdateKindType);
            }

            return new ToolMapEvidence(
                TypeRolePopulation.Resolve(ledger, inventory, excluded).Signatures,
                ProvidedOwners(LedgerPopulation.Resolve(ledger, inventory).Owners, ledger),
                inventory.Signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal),
                DangerousOperationRule.Classify(inventory.Signatures),
                ContractNotes(ledger),
                new HashSet<string>(updateKind.EnumMembers, StringComparer.Ordinal),
                new HashSet<string>(
                    roles.Types.Where(r => r.ElementNoun != null).Select(r => r.ElementNoun),
                    StringComparer.Ordinal),
                new HashSet<string>(types.Select(t => t.Name), StringComparer.Ordinal));
        }

        /// <summary>
        /// 行キーから、それを指す提供能力のIDを引く表。台帳は非対応の行も同じシグネチャを指しうるので、
        /// 分類が提供の能力だけに絞る。
        /// </summary>
        public static IDictionary<string, ISet<string>> ProvidedOwners(
            IDictionary<string, ISet<string>> owners, IEnumerable<CapabilityRecord> ledger)
        {
            if (owners == null)
            {
                throw new ArgumentNullException(nameof(owners));
            }

            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            HashSet<string> capabilities = new HashSet<string>(
                ledger.Where(c => c.Status == CapabilityStatus.Provided).Select(c => c.Id),
                StringComparer.Ordinal);
            return owners.ToDictionary(
                pair => pair.Key,
                pair => (ISet<string>)new HashSet<string>(
                    pair.Value.Where(capabilities.Contains), StringComparer.Ordinal),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// 契約注記を持つ提供能力のIDから、その注記の本文を引く表。本文は固定の接頭辞から備考の
        /// 末尾までとする。接頭辞が二度現れる備考と、本文を伴わない接頭辞は、書いたつもりで検査を
        /// 素通りするので不合格にする。
        /// </summary>
        public static IDictionary<string, string> ContractNotes(IEnumerable<CapabilityRecord> ledger)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            Dictionary<string, string> notes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CapabilityRecord capability in ledger)
            {
                string remarks = capability.Remarks ?? string.Empty;
                int at = remarks.IndexOf(NotePrefix, StringComparison.Ordinal);
                if (at < 0)
                {
                    continue;
                }

                if (remarks.IndexOf(NotePrefix, at + NotePrefix.Length, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException(
                        capability.Id + " の契約注記の接頭辞が二度現れる: " + remarks);
                }

                string body = remarks.Substring(at + NotePrefix.Length).TrimStart(' ');
                if (body.Length == 0)
                {
                    throw new InvalidOperationException(
                        capability.Id + " の契約注記に本文が無い: " + remarks);
                }

                notes.Add(capability.Id, body);
            }

            return notes;
        }
    }
}
