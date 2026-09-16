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

        private readonly IDictionary<string, ISet<string>> _unkeptMembers;

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
            IDictionary<string, ISet<string>> unkeptMembers,
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
            _unkeptMembers = unkeptMembers;
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

        /// <summary>
        /// 要素型の名前から、その要素を並びへ加えるツールの名前へ。事後条件の用意の操作が要素型で
        /// 指すので、その名前から呼ぶ先を引く。
        /// </summary>
        public IDictionary<string, string> ElementAdders(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return OwnedRoles(inventory).Types
                .Where(t => t.Group != CapabilityOwner.None
                    && !string.IsNullOrEmpty(t.ElementNoun)
                    && !string.IsNullOrEmpty(t.ElementNounPlural))
                .GroupBy(t => t.ElementNoun, StringComparer.Ordinal)
                .Where(g => g.Count() == 1)
                .ToDictionary(
                    g => g.Key,
                    g => ToolNameRule.OfRole(g.First(), ToolVerb.Add),
                    StringComparer.Ordinal);
        }

        /// <summary>
        /// 要素をリストから外すツールの名前から、その要素をリストへ加えるツールの名前へ。外す
        /// 相手は段取りが加えた要素である——新しく作った要素はまだ並びに無いので外せない。
        /// </summary>
        public IDictionary<string, string> ElementRemovers(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return OwnedRoles(inventory).Types
                .Where(t => t.Group != CapabilityOwner.None
                    && !string.IsNullOrEmpty(t.ElementNoun)
                    && !string.IsNullOrEmpty(t.ElementNounPlural))
                .GroupBy(t => ToolNameRule.OfRole(t, ToolVerb.Remove), StringComparer.Ordinal)
                .Where(g => g.Count() == 1)
                .ToDictionary(
                    g => g.Key,
                    g => ToolNameRule.OfRole(g.First(), ToolVerb.Add),
                    StringComparer.Ordinal);
        }

        /// <summary>
        /// 要素を並べるリストへ加えるツールの名前から、その要素を1つ作るツールの名前へ。作る
        /// ツールが1つに決まらない型は持たない——どれを使うかがここでは決められない。
        /// </summary>
        public IDictionary<string, string> ElementFactories(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            TypeRoleTable owned = OwnedRoles(inventory);
            IDictionary<string, string> tools = ToolsByRow(inventory);
            IDictionary<string, SignatureRecord> signatures = inventory.Signatures
                .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
            IDictionary<string, IList<string>> reached =
                ReceiverCallEvidence.ByType(inventory, Map, tools, Schemas);
            Dictionary<string, string> factories =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TypeRoleRecord role in owned.Types
                .Where(t => t.Group != CapabilityOwner.None
                    && !string.IsNullOrEmpty(t.ElementNoun)
                    && !string.IsNullOrEmpty(t.ElementNounPlural)))
            {
                string[] making = tools
                    .Where(t => Makes(signatures, t.Key, role.TypeName))
                    .Select(t => t.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                IList<string> path;
                if (making.Length == 1)
                {
                    factories[ToolNameRule.OfRole(role, ToolVerb.Add)] = making[0];
                }
                else if (reached.TryGetValue(role.TypeName, out path) && path.Count == 1)
                {
                    factories[ToolNameRule.OfRole(role, ToolVerb.Add)] = path[0];
                }
            }

            return factories;
        }

        /// <summary>
        /// 受け手をハンドルで要るツールの名前から、その受け手を得るまでに順に呼ぶツールの列へ。
        /// 受け手へ至る列を持たないツールは持たない。受け手の型は行の宣言型から取る——スキーマの
        /// 側の型は呼び出しの引数だけを写すので、受け手は載らない。
        /// </summary>
        public IDictionary<string, IList<string>> ReceiverPaths(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return ReceiverCallEvidence.ByTool(
                inventory, Map, OwnedRoles(inventory), ToolsByRow(inventory), Schemas);
        }

        /// <summary>その行が、その型の実体を引数無しで1つ作るか。</summary>
        private static bool Makes(
            IDictionary<string, SignatureRecord> signatures, string rowKey, string typeName)
        {
            SignatureRecord signature;

            return signatures.TryGetValue(rowKey, out signature)
                && signature.Parameters.Count == 0
                && signature.MemberKind == MemberKind.Method
                && string.Equals(
                    TypeDefinitionName.Of(signature.ValueType), typeName, StringComparison.Ordinal);
        }

        /// <summary>
        /// 値を書き換えるツールの名前から、同じ型を読むツールの名前へ。書いた値を読み返す検査が
        /// 相手を決めるのに使う。
        /// </summary>
        public IDictionary<string, string> Readers(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return AggregationToolRule.Readers(OwnedRoles(inventory).Types);
        }

        /// <summary>
        /// いま選ばれている対象を相手にする行の行キー。呼ぶ前に確かめることの規則がこれらを分けて
        /// いる——選ばれているものが無いと、エディタが人の応答を待つ表示を出す。
        /// </summary>
        public ISet<string> PickingRows(InventoryRecord inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            HashSet<string> picking = new HashSet<string>(StringComparer.Ordinal);
            foreach (SignatureRecord signature in inventory.Signatures)
            {
                PreconditionKind kind;
                if (PreconditionRule.TryClassify(signature, out kind)
                    && kind == PreconditionKind.PickedObjects)
                {
                    picking.Add(signature.Key);
                }
            }

            return picking;
        }

        /// <summary>値をハンドルの番号で写す型の名前。</summary>
        public ISet<string> HandledTypes()
        {
            return new HashSet<string>(
                _roles.Types
                    .Where(t => t.Role == TypeRole.HandleTarget)
                    .Select(t => t.TypeName),
                StringComparer.Ordinal);
        }

        /// <summary>値を要素の位置で写す型の名前。</summary>
        public ISet<string> PositionedTypes()
        {
            return new HashSet<string>(
                _roles.Types
                    .Where(t => t.Role == TypeRole.OperationTarget)
                    .Select(t => t.TypeName),
                StringComparer.Ordinal);
        }

        /// <summary>ビューの画像を返すツールの名前から、そのビューの名前へ。</summary>
        public IDictionary<string, string> ViewImages
        {
            get { return _viewImages; }
        }

        /// <summary>
        /// 値を書き換えるツールの名前から、書いてもモデルが持ち続けない項目の名前へ。
        /// </summary>
        public IDictionary<string, ISet<string>> UnkeptMembers
        {
            get { return _unkeptMembers; }
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
                contract.UnkeptMembers,
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

        /// <summary>
        /// 画像を返すツールの名前。ビューを名指しされたツールがそれで、名指しと画像を返すことの
        /// 一致は[写像の規則](ToolMappingGate)が見る。
        /// </summary>
        public ISet<string> DrawingTools()
        {
            return new HashSet<string>(_viewImages.Keys, StringComparer.Ordinal);
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
