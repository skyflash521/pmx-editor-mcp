using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>ツール定義の組み立てが読む入力をひとまとめにしたもの。</summary>
    public sealed class ToolDefinitionInputs
    {
        private readonly IList<CapabilityRecord> _ledger;

        private readonly TypeRoleTable _roles;

        private readonly IList<PropertyNameRecord> _names;

        private readonly CommonAssignmentTable _assignments;

        private readonly IDictionary<string, ComposedTool> _composedTools;

        private readonly IDictionary<string, string> _methodNotes;

        private readonly IDictionary<string, string> _propertyNotes;

        private ToolDefinitionInputs(
            IList<CapabilityRecord> ledger,
            TypeRoleTable roles,
            IList<PropertyNameRecord> names,
            CommonAssignmentTable assignments,
            IDictionary<string, ComposedTool> composedTools,
            IDictionary<string, string> methodNotes,
            IDictionary<string, string> propertyNotes,
            ToolMap map,
            string mapDigest,
            ToolSchemaTable schemas,
            IDictionary<string, int> lengths,
            int budgetChars,
            int warningChars,
            int requestBytes,
            int tokenLimit)
        {
            _ledger = ledger;
            _roles = roles;
            _names = names;
            _assignments = assignments;
            _composedTools = composedTools;
            _methodNotes = methodNotes;
            _propertyNotes = propertyNotes;
            Map = map;
            MapDigest = mapDigest;
            Schemas = schemas;
            Lengths = lengths;
            BudgetChars = budgetChars;
            WarningChars = warningChars;
            RequestBytes = requestBytes;
            TokenLimit = tokenLimit;
        }

        public ToolMap Map { get; }

        /// <summary>能力対応表の指紋。ホストの中継と同じ材料から作る。</summary>
        public string MapDigest { get; }

        public ToolSchemaTable Schemas { get; }

        public IDictionary<string, int> Lengths { get; }

        public int BudgetChars { get; }

        public int WarningChars { get; }

        public int RequestBytes { get; }

        public int TokenLimit { get; }

        /// <summary>読めない入力が1つでもあれば例外。</summary>
        public static ToolDefinitionInputs Read(string editorDirectory, string[] args)
        {
            if (editorDirectory == null)
            {
                throw new ArgumentNullException(nameof(editorDirectory));
            }

            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            string map = ReadFile(args[8], "能力対応表の正本");
            string contract = ReadFile(args[2], "共通契約仕様書");
            string ipc = ReadFile(args[3], "IPC仕様書");
            string architecture = ReadFile(args[4], "アーキテクチャ仕様書");
            string document = ReadFile(
                SdkAssemblyLocator.GetDocumentPath(editorDirectory), "ドキュメントXML");

            return new ToolDefinitionInputs(
                LedgerJsonReader.Read(ReadFile(args[1], "能力台帳")),
                TypeRoleTableJsonReader.ReadTypeRoles(ReadFile(args[5], "型役割表の正本")),
                PropertyNameJsonReader.ReadPropertyNames(ReadFile(args[6], "日本語名の正本")),
                CommonAssignmentJsonReader.Read(ReadFile(args[7], "共通契約割当の正本")),
                ComposedToolDocument.Read(contract),
                DocumentNoteReader.ReadMethods(document),
                DocumentNoteReader.Read(document),
                ToolMapJsonReader.Read(map),
                ToolMapDigest.Of(map),
                ToolSchemaJsonReader.Read(ReadFile(args[9], "スキーマ正本")),
                AssumedLengthDocument.Read(contract),
                BudgetDocument.ReadDefault(architecture),
                BudgetDocument.ReadWarningRoom(contract),
                BudgetDocument.ReadRequestBytes(contract),
                BudgetDocument.ReadTokenLimit(ipc));
        }

        /// <summary>ツール名から説明文へ。合成ツールは仕様書の受け持つことをそのまま使う。</summary>
        public IDictionary<string, string> Descriptions(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            TypeRoleTable owned = TypeGroupRule.Resolve(
                _roles, TypeGroupEvidence.OwnersByType(_ledger, inventory));
            IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
            IList<ToolDescriptionMaterial> materials = ToolDescriptionEvidence.Collect(
                Map,
                owned,
                _names,
                inventory,
                ToolNameEvidence.Resolve(Map, owned, _assignments, signatures),
                ToolMapEvidence.ContractNotesBySignature(
                    ToolMapEvidence.ProvidedOwners(
                        LedgerPopulation.Resolve(_ledger, inventory).Owners, _ledger),
                    ToolMapEvidence.ContractNotes(_ledger)),
                _methodNotes,
                _propertyNotes);

            Dictionary<string, string> descriptions =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ToolDescriptionMaterial material in materials)
            {
                descriptions[material.Tool] = ToolDescriptionRule.Compose(material).Text;
            }

            foreach (KeyValuePair<string, ComposedTool> composed in _composedTools)
            {
                descriptions[composed.Key] = composed.Value.Duty;
            }

            return descriptions;
        }

        private static string ReadFile(string path, string name)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(name + "が無い: " + path, path);
            }

            return File.ReadAllText(path);
        }
    }
}
