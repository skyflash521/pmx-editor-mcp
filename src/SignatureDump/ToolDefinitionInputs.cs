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

        private readonly IDictionary<string, string> _viewImages;

        private readonly IDictionary<string, string> _methodNotes;

        private readonly IDictionary<string, string> _propertyNotes;

        private readonly IDictionary<string, string> _shapesByType;

        private ToolDefinitionInputs(
            IList<CapabilityRecord> ledger,
            TypeRoleTable roles,
            IList<PropertyNameRecord> names,
            CommonAssignmentTable assignments,
            IDictionary<string, ComposedTool> composedTools,
            IDictionary<string, string> viewImages,
            IDictionary<string, string> methodNotes,
            IDictionary<string, string> propertyNotes,
            IDictionary<string, string> shapesByType,
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
            _viewImages = viewImages;
            _methodNotes = methodNotes;
            _propertyNotes = propertyNotes;
            _shapesByType = shapesByType;
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

        /// <summary>型ごとの役割と、発行と並びの判定。</summary>
        public TypeRoleTable Roles
        {
            get { return _roles; }
        }

        /// <summary>共通契約が受け持つ行の割当。</summary>
        public CommonAssignmentTable Assignments
        {
            get { return _assignments; }
        }

        /// <summary>1つのSDKメンバーへ1対1で写らないツール。行を持たない。</summary>
        public IDictionary<string, ComposedTool> ComposedTools
        {
            get { return _composedTools; }
        }

        /// <summary>ビューの絵を返すツールの名前から、そのビューの名前へ。</summary>
        public IDictionary<string, string> ViewImages
        {
            get { return _viewImages; }
        }

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

            string map = ReadFile(args[6], "能力対応表の正本");
            CommonContractTable contract =
                CommonContractJsonReader.Read(ReadFile(args[2], "共通契約の正本"));
            string document = ReadFile(
                SdkAssemblyLocator.GetDocumentPath(editorDirectory), "ドキュメントXML");

            return new ToolDefinitionInputs(
                LedgerJsonReader.Read(ReadFile(args[1], "能力台帳")),
                TypeRoleTableJsonReader.ReadTypeRoles(ReadFile(args[3], "型役割表の正本")),
                PropertyNameJsonReader.ReadPropertyNames(ReadFile(args[4], "日本語名の正本")),
                CommonAssignmentJsonReader.Read(ReadFile(args[5], "共通契約割当の正本")),
                contract.ComposedTools,
                contract.ViewImages,
                DocumentNoteReader.ReadMethods(document),
                DocumentNoteReader.Read(document),
                ShapesByType(contract),
                ToolMapJsonReader.Read(map),
                ToolMapDigest.Of(map),
                ToolSchemaJsonReader.Read(ReadFile(args[7], "スキーマ正本")),
                contract.AssumedChars(),
                contract.Budgets.ResponseDefaultChars,
                contract.Budgets.WarningRoomChars,
                contract.Budgets.RequestBytes,
                contract.Budgets.StructureTokenLimit);
        }

        /// <summary>操作対象型を指す位置の綴り。位置は0から数える整数である。</summary>
        private const string PositionShape = "number";

        /// <summary>型から値の表現の綴りへ。綴りが1つに決まらない包む型は持たない。</summary>
        private static IDictionary<string, string> ShapesByType(CommonContractTable contract)
        {
            Dictionary<string, string> shapes =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ValueShapeRow row in contract.Types.Where(r => r.Shape != null))
            {
                shapes[row.TypeName] = row.Shape;
            }

            return shapes;
        }

        /// <summary>担当群を解いた型役割表。名前を決める側と同じ解き方をここ1つに置く。</summary>
        public TypeRoleTable OwnedRoles(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return TypeGroupRule.Resolve(
                _roles, TypeGroupEvidence.OwnersByType(_ledger, inventory));
        }

        /// <summary>SDKに由来する項目から、その項目が写す型の名前へ。</summary>
        public IDictionary<SchemaItem, string> SdkTypes(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            Dictionary<string, string> itself =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in Positioned().Keys)
            {
                itself[name] = name;
            }

            return SdkShapeEvidence.Resolve(
                Schemas,
                Map,
                inventory.Signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal),
                ToolsByRow(inventory),
                itself,
                Positioned());
        }

        /// <summary>SDKに由来する項目から表現の綴りへ。正本が綴りを書かない項目をここで補う。</summary>
        public IDictionary<SchemaItem, string> SdkShapes(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return SdkShapeEvidence.Resolve(
                Schemas,
                Map,
                inventory.Signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal),
                ToolsByRow(inventory),
                Positioned(),
                Positioned());
        }

        /// <summary>
        /// 型から値の表現の綴りへ。操作対象型は要素の位置で、ハンドル操作型はハンドルの番号で写すので、
        /// 型役割表からその綴りを足す。どちらも数で写るが、指すものは位置と番号で別である。
        /// </summary>
        private IDictionary<string, string> Positioned()
        {
            Dictionary<string, string> shapes =
                new Dictionary<string, string>(_shapesByType, StringComparer.Ordinal);
            foreach (TypeRoleRecord role in _roles.Types
                .Where(t => t.Role == TypeRole.OperationTarget || t.Role == TypeRole.HandleTarget))
            {
                shapes[role.TypeName] = PositionShape;
            }

            return shapes;
        }

        /// <summary>確認を要するツールの名前。行の側の判定をツールの名前へ写す。</summary>
        public ISet<string> DangerousTools(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            IDictionary<string, string> tools = ToolsByRow(inventory);

            return new HashSet<string>(
                Dangerous(inventory).Where(tools.ContainsKey).Select(k => tools[k]),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// Undoの記録を止めることを頼めるツールの名前。止めても効くのはまとめて反映するときの
        /// 登録だけなので、複製編集型の行を持つツールに限る。
        /// </summary>
        public ISet<string> SuppressingTools(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            IDictionary<string, string> tools = ToolsByRow(inventory);

            return new HashSet<string>(
                Map.Rows
                    .Where(r => r.EditKind == ToolMapEditKind.DuplicateEdit
                        && tools.ContainsKey(r.SignatureKey))
                    .Select(r => tools[r.SignatureKey]),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// 確認の要否が呼ぶ対象で分かれるツールの名前。所有の根そのものを空にする初期化だけが
        /// これに当たり、対象を指定した呼び出しはメモリの上の生成物を空にするので確認を要さない。
        /// </summary>
        public ISet<string> ConditionalDangerousTools(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            IDictionary<string, string> tools = ToolsByRow(inventory);
            IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);

            return new HashSet<string>(
                DangerousOperationRule.Classify(inventory.Signatures)
                    .Where(d => d.Value == DangerKind.Reset
                        && tools.ContainsKey(d.Key)
                        && ElementCollectionEvidence.OwnershipRoots.Contains(
                            TypeDefinitionName.OfElement(signatures[d.Key].DeclaringType),
                            StringComparer.Ordinal))
                    .Select(d => tools[d.Key]),
                StringComparer.Ordinal);
        }

        /// <summary>ツール名から説明文へ。合成ツールは受け持つことをそのまま使う。</summary>
        public IDictionary<string, string> Descriptions(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            TypeRoleTable owned = OwnedRoles(inventory);
            IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
            IList<ToolDescriptionMaterial> materials = ToolDescriptionEvidence.Collect(
                Map,
                owned,
                _names,
                inventory,
                ToolNameEvidence.Resolve(Map, owned, _assignments, inventory),
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

        /// <summary>行キーからツールの名前へ。</summary>
        public IDictionary<string, string> ToolsByRow(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            TypeRoleTable owned = OwnedRoles(inventory);

            return ToolNameEvidence.Resolve(Map, owned, _assignments, inventory);
        }

        /// <summary>確認を要する行キー。名前で決まるので列挙から判じる。</summary>
        public ISet<string> Dangerous(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return new HashSet<string>(
                DangerousOperationRule.Classify(inventory.Signatures).Keys, StringComparer.Ordinal);
        }

        /// <summary>型から接続の根へ至る経路。辿り着けない型は持たない。</summary>
        public IDictionary<string, string> ConnectionPaths(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return TypeRoleEvidence.ReachableFromRoots(inventory, TypeRoleEvidence.ConnectionRoots);
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
