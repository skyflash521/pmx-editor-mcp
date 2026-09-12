using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>ツールの結び付きを組み立てた結果。</summary>
    public sealed class ToolBindingSource
    {
        public ToolBindingSource(
            string text, IList<string> calls, IList<string> aggregations, IList<string> elements)
        {
            Text = text;
            Calls = calls;
            Aggregations = aggregations;
            Elements = elements;
        }

        /// <summary>ホストへ組み込むC#の本文。</summary>
        public string Text { get; }

        /// <summary>SDKのメンバーへ中継するツールの名前。</summary>
        public IList<string> Calls { get; }

        /// <summary>項目を集めるツールの名前。</summary>
        public IList<string> Aggregations { get; }

        /// <summary>所有するリストへ加える・から取り除くツールの名前。</summary>
        public IList<string> Elements { get; }
    }

    /// <summary>
    /// ツールの名前から、呼ぶ行・受け手の得方・引数の型・確認の要否を引く表と、所有するリストを
    /// 読み書きする中継をC#として組み立てる。どれも行と型役割から導けるので、ホストは名前で引く
    /// 経路を持たずに振り分けられる。
    /// </summary>
    public static class ToolBindingSourceBuilder
    {
        private const string Indent = "            ";

        private const string VoidTypeName = "System.Void";

        private const string FlagTypeName = "System.Boolean";

        /// <summary>Undoの抑止を受け取る共通引数の対象名。</summary>
        private const string SuppressUndoArgument = "suppressUndo";

        /// <summary>Undoの記録を止めるメンバーの名前。</summary>
        private const string StopUndoMember = "LockUndo";

        /// <summary>止めたUndoの記録を戻すメンバーの名前。</summary>
        private const string ResumeUndoMember = "UnlockUndo";

        private const string PmxTypeName = "PEPlugin.Pmx.IPXPmx";

        /// <summary>Cプラグイン連携の橋渡しの型。ここから得る受け手は、そちらの流れで扱う。</summary>
        private const string BridgeTypeName = "PXCPlugin.PXCBridge";

        /// <summary>
        /// 表を組み立てる。<paramref name="toolNames"/> は行キーからツールの名前へ、
        /// <paramref name="roles"/> は担当群を解いた型役割表、<paramref name="assignments"/> は
        /// 共通契約割当の正本。
        /// </summary>
        public static ToolBindingSource Build(
            ToolMap map,
            TypeRoleTable roles,
            InventoryRecord inventory,
            IDictionary<string, string> toolNames,
            CommonAssignmentTable assignments)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (toolNames == null)
            {
                throw new ArgumentNullException(nameof(toolNames));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            IDictionary<string, SignatureRecord> signatures = inventory.Signatures.ToDictionary(
                s => s.Key, s => s, StringComparer.Ordinal);
            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);
            IDictionary<string, DangerKind> dangerous = DangerousOperationRule.Classify(
                signatures.Values);
            IDictionary<string, TypeRoleRecord> owned = ElementToolRule.Elements(
                map, signatures, roles);
            IDictionary<string, AccessPath> paths = ElementPathEvidence.Resolve(inventory, roles);
            ISet<string> issued = ElementPathEvidence.Issued(inventory, roles);
            ISet<string> bridged = Bridged(inventory);
            IDictionary<string, TypeRole> roleOf = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t.Role, StringComparer.Ordinal);
            IDictionary<string, IList<string>> concrete =
                ElementCollectionEvidence.ConcreteTypes(inventory, roleOf);
            ISet<string> built = Built(inventory, bridged, concrete);
            IDictionary<string, IList<string>> ownerPaths = roles.Collections
                .Where(c => c.Owns && c.OwnerPath.Count != 0)
                .ToDictionary(
                    c => c.SignatureKey,
                    c => (IList<string>)c.OwnerPath.Take(c.OwnerPath.Count - 1).ToList(),
                    StringComparer.Ordinal);

            SortedDictionary<string, List<string>> calls =
                new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            SortedDictionary<string, SortedDictionary<string, List<string>>> fields =
                new SortedDictionary<string, SortedDictionary<string, List<string>>>(
                    StringComparer.Ordinal);
            SortedDictionary<string, string> aggregated =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            SortedDictionary<string, string> elements =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            SortedDictionary<string, string> lists =
                new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (ToolMapRow row in map.Rows.OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    continue;
                }

                AccessPath path = Path(paths, signature);
                string tool;
                if (toolNames.TryGetValue(row.SignatureKey, out tool))
                {
                    List<string> overloads;
                    if (!calls.TryGetValue(tool, out overloads))
                    {
                        overloads = new List<string>();
                        calls.Add(tool, overloads);
                    }

                    overloads.Add(
                        Call(
                            row,
                            signature,
                            path,
                            dangerous,
                            signatures,
                            concrete,
                            byType,
                            paths,
                            bridged));
                    Listing(lists, path, signatures, byType);
                    continue;
                }

                TypeRoleRecord element;
                if (owned.TryGetValue(row.SignatureKey, out element))
                {
                    IList<string> owning = Owner(ownerPaths, row.SignatureKey);
                    AccessPath listed = new AccessPath(
                        AccessPathKind.Element,
                        row.SignatureKey,
                        owning,
                        true,
                        element.TypeName,
                        ElementPathEvidence.Owner(
                            signatures, issued, row.SignatureKey, owning.Count));
                    foreach (string named in ElementToolRule.Of(element))
                    {
                        elements.Add(
                            named,
                            Elements(
                                row, signature, element, named, listed, signatures, concrete,
                                byType, built));
                    }

                    Listing(lists, listed, signatures, byType);
                    continue;
                }

                foreach (Embedding target in Aggregations(row, signature, byType, concrete))
                {
                    AccessPath owning = Path(paths, target.OwnerType);
                    SortedDictionary<string, List<string>> sets;
                    if (!fields.TryGetValue(target.Tool, out sets))
                    {
                        sets = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
                        fields.Add(target.Tool, sets);
                        aggregated.Add(
                            target.Tool,
                            Aggregation(
                                row, byType[target.OwnerType], target.Updates, owning,
                                signatures, concrete, byType));
                        Listing(lists, owning, signatures, byType);
                    }

                    List<string> members;
                    string kind = target.ItemType ?? string.Empty;
                    if (!sets.TryGetValue(kind, out members))
                    {
                        members = new List<string>();
                        sets.Add(kind, members);
                    }

                    members.Add(Field(signature));
                }
            }

            return new ToolBindingSource(
                Compose(
                    calls,
                    fields,
                    aggregated,
                    elements,
                    lists,
                    Flows(assignments, signatures),
                    signatures,
                    assignments),
                calls.Keys.ToList(),
                fields.Keys.ToList(),
                elements.Keys.ToList());
        }

        /// <summary>
        /// その行が項目を持ち込む取得と更新のツール。名前と、そのツールが書き込む側かどうかを返す。
        /// 埋め込み先が取得と更新のどちらでもないものは、そのツールの側が項目を持つので返さない。
        /// </summary>
        private static IEnumerable<Embedding> Aggregations(
            ToolMapRow row,
            SignatureRecord signature,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, IList<string>> concrete)
        {
            string declaring = TypeDefinitionName.OfElement(signature.DeclaringType);
            TypeRoleRecord owner;
            if (row.EmbeddedIn == null || !byType.TryGetValue(declaring, out owner))
            {
                yield break;
            }

            foreach (string embedded in row.EmbeddedIn)
            {
                foreach (TypeRoleRecord holder in Holders(owner, declaring, byType, concrete))
                {
                    if (!AggregationToolRule.Of(holder).Contains(embedded, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    bool updates = string.Equals(
                        embedded,
                        ToolNameRule.OfRole(holder, ToolVerb.Update),
                        StringComparison.Ordinal);
                    if (updates ? !signature.CanWrite : !signature.CanRead)
                    {
                        throw new InvalidOperationException(
                            (updates ? "書き込めない項目が更新へ持ち込まれている: "
                                : "読み取れない項目が取得へ持ち込まれている: ") + signature.Key);
                    }

                    yield return new Embedding(
                        embedded,
                        updates,
                        TypeDefinitionName.OfElement(holder.TypeName),
                        ReferenceEquals(holder, owner) ? null : owner.ElementNoun);
                }
            }
        }

        /// <summary>
        /// その行の項目を集めうる型。宣言型そのものと、宣言型を具象として並べる抽象の型である
        /// ——抽象の型を並べるリストでは、具象の型の項目はそのリストのツールへ集まる。
        /// </summary>
        private static IEnumerable<TypeRoleRecord> Holders(
            TypeRoleRecord owner,
            string declaring,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, IList<string>> concrete)
        {
            yield return owner;
            foreach (KeyValuePair<string, IList<string>> listed in concrete
                .Where(c => c.Value.Contains(declaring, StringComparer.Ordinal))
                .OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                TypeRoleRecord holder;
                if (byType.TryGetValue(listed.Key, out holder))
                {
                    yield return holder;
                }
            }
        }

        /// <summary>その行の宣言型の受け手が、PMXのどこに居るか。辿り着けない型では null。</summary>
        private static AccessPath Path(
            IDictionary<string, AccessPath> paths, SignatureRecord signature)
        {
            return Path(paths, TypeDefinitionName.OfElement(signature.DeclaringType));
        }

        /// <summary>その型の受け手が、PMXのどこに居るか。辿り着けない型では null。</summary>
        private static AccessPath Path(IDictionary<string, AccessPath> paths, string typeName)
        {
            AccessPath path;

            return paths.TryGetValue(typeName, out path) ? path : null;
        }

        /// <summary>行が項目を持ち込む先1件。</summary>
        private sealed class Embedding
        {
            public Embedding(string tool, bool updates, string ownerType, string itemType)
            {
                Tool = tool;
                Updates = updates;
                OwnerType = ownerType;
                ItemType = itemType;
            }

            /// <summary>持ち込む先のツールの名前。</summary>
            public string Tool { get; }

            /// <summary>そのツールが書き込む側か。</summary>
            public bool Updates { get; }

            /// <summary>そのツールの名前を導く型。</summary>
            public string OwnerType { get; }

            /// <summary>持ち込む項目を持つ要素の実行時の型。宣言型そのもののツールでは null。</summary>
            public string ItemType { get; }
        }

        /// <summary>その行が並べる要素の、所有の経路の親の側。</summary>
        private static IList<string> Owner(
            IDictionary<string, IList<string>> ownerPaths, string rowKey)
        {
            IList<string> parents;

            return ownerPaths.TryGetValue(rowKey, out parents) ? parents : new string[0];
        }

        /// <summary>
        /// その道が辿るリストの中継を、まだ持っていなければ足す。要素を並べるリストも、親の列を
        /// 作る途中のリストも、同じ引き当てで辿る。
        /// </summary>
        private static void Listing(
            IDictionary<string, string> lists,
            AccessPath path,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, TypeRoleRecord> byType)
        {
            if (path == null || path.Kind != AccessPathKind.Element)
            {
                return;
            }

            foreach (string rowKey in path.Parents.Concat(new[] { path.RowKey }))
            {
                SignatureRecord signature;
                string element;
                if (lists.ContainsKey(rowKey)
                    || !signatures.TryGetValue(rowKey, out signature)
                    || !ValueTypeName.TryElement(signature.ValueType, out element))
                {
                    continue;
                }

                lists.Add(rowKey, List(signature, element));
            }
        }

        private static string Call(
            ToolMapRow row,
            SignatureRecord signature,
            AccessPath path,
            IDictionary<string, DangerKind> dangerous,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, AccessPath> paths,
            ISet<string> bridged)
        {
            DangerKind kind;
            string danger = dangerous.TryGetValue(signature.Key, out kind)
                ? "DangerKind." + kind
                : "DangerKind.None";
            string[] arguments = signature.Parameters
                .Where(p => p.Direction != ParameterDirection.Out)
                .Select(p => Argument(p, signatures, concrete, byType, paths))
                .ToArray();
            string[] outputs = signature.Parameters
                .Where(p => p.Direction != ParameterDirection.In)
                .Select(p => "new ToolArgument(" + Literal(p.Name) + ", " + TypeOf(p.TypeName) + ")")
                .ToArray();

            string issues = Issues(row, signature) ? ", " + TypeOf(signature.ValueType) : string.Empty;

            return "new ToolCall(" + Literal(signature.Key) + ", "
                + Receiver(row, signature, path, null, Bridges(signature, path, bridged)) + ", "
                + Access(path, signatures, concrete, byType) + ", "
                + danger + ", new ToolArgument[] { " + string.Join(", ", arguments) + " }, "
                + "new ToolArgument[] { " + string.Join(", ", outputs) + " }, "
                + (string.Equals(signature.ValueType, VoidTypeName, StringComparison.Ordinal)
                    ? "null"
                    : TypeOf(signature.ValueType))
                + issues + ")";
        }

        /// <summary>
        /// その行が返す値を台帳へ預けるか。返り値がどのリストにも居ない新しい実体なのかは列挙からは
        /// 決まらないので、能力対応表がその行へ判じた効果から採る。
        /// </summary>
        private static bool Issues(ToolMapRow row, SignatureRecord signature)
        {
            return !string.Equals(signature.ValueType, VoidTypeName, StringComparison.Ordinal)
                && row.Postcondition != null
                && row.Postcondition.Any(p => p.EffectType == EffectType.HandleCreated);
        }

        /// <summary>
        /// 引数1件をC#の式にする。相手にするPMXを取る引数はホストが入れ、ほかの操作対象型を取る
        /// 引数はその実体を並べるリストの中の位置で受け取る。
        /// </summary>
        private static string Argument(
            ParameterRecord parameter,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, AccessPath> paths)
        {
            string written = "new ToolArgument(" + Literal(parameter.Name) + ", "
                + TypeOf(parameter.TypeName);
            string typeName = TypeDefinitionName.OfElement(parameter.TypeName);
            if (string.Equals(typeName, PmxTypeName, StringComparison.Ordinal))
            {
                return written + ", true)";
            }

            TypeRoleRecord role;
            AccessPath listed;
            if (!byType.TryGetValue(typeName, out role)
                || role.Role != TypeRole.OperationTarget
                || !paths.TryGetValue(typeName, out listed))
            {
                return written + ")";
            }

            return written + ", false, " + Access(listed, signatures, concrete, byType) + ")";
        }

        /// <summary>その行の受け手を、橋渡しから得るか。所有の根から得る受け手は当たらない。</summary>
        private static bool Bridges(
            SignatureRecord signature, AccessPath path, ISet<string> bridged)
        {
            string declaring = TypeDefinitionName.OfElement(signature.DeclaringType);

            return !string.Equals(declaring, PmxTypeName, StringComparison.Ordinal)
                && (path == null || path.Kind == AccessPathKind.Whole)
                && bridged.Contains(declaring);
        }

        private static string Aggregation(
            ToolMapRow row,
            TypeRoleRecord owner,
            bool writes,
            AccessPath path,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType)
        {
            return "new ToolFields(" + (writes ? "true" : "false") + ", "
                + (owner.Role == TypeRole.Connector ? "false" : "true") + ", "
                + Receiver(owner.TypeName, false, path, writes ? row.EditKind : ToolMapEditKind.Read)
                + ", " + Access(path, signatures, concrete, byType) + ", new ToolFieldSet[]";
        }

        private static string Elements(
            ToolMapRow row,
            SignatureRecord signature,
            TypeRoleRecord element,
            string tool,
            AccessPath path,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType,
            ISet<string> built)
        {
            bool removes = string.Equals(
                tool, ToolNameRule.OfRole(element, ToolVerb.Remove), StringComparison.Ordinal);

            return "new ToolElements(" + (removes ? "true" : "false") + ", "
                + Receiver(
                    row,
                    signature,
                    path,
                    ToolMapEditKind.DuplicateEdit,
                    !removes && built.Contains(TypeDefinitionName.OfElement(element.TypeName)))
                + ", " + Access(path, signatures, concrete, byType) + ")";
        }

        /// <summary>
        /// Cプラグイン連携の橋渡しが直に返す型。そこから得た受け手は、複製と反映もそちらの流れで
        /// 行う——片方の流れで作った中身は、もう片方の流れでは反映できない。
        /// </summary>
        private static ISet<string> Bridged(InventoryRecord inventory)
        {
            return new HashSet<string>(
                inventory.Signatures
                    .Where(s => string.Equals(
                        TypeDefinitionName.OfElement(s.DeclaringType),
                        BridgeTypeName,
                        StringComparison.Ordinal))
                    .Select(s => TypeDefinitionName.OfElement(s.ValueType)),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// 橋渡しから得る窓口が作る型。この型の実体は、作った側の流れでしか反映できない。
        /// </summary>
        private static ISet<string> Built(
            InventoryRecord inventory,
            ISet<string> bridged,
            IDictionary<string, IList<string>> concrete)
        {
            HashSet<string> built = new HashSet<string>(
                inventory.Signatures
                    .Where(s => s.MemberKind == MemberKind.Method
                        && bridged.Contains(TypeDefinitionName.OfElement(s.DeclaringType)))
                    .Select(s => TypeDefinitionName.OfElement(s.ValueType)),
                StringComparer.Ordinal);

            foreach (KeyValuePair<string, IList<string>> listed in concrete)
            {
                if (listed.Value.Any(built.Contains))
                {
                    built.Add(listed.Key);
                }
            }

            return built;
        }

        /// <summary>相手にするPMXから受け手へ至る道をC#の式にする。</summary>
        private static string Access(
            AccessPath path,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType)
        {
            if (path == null || path.Kind == AccessPathKind.Whole)
            {
                return "ToolAccess.Whole()";
            }

            string[] hops = path.Parents
                .Select(p => "new ToolHop(" + Literal(p) + ", "
                    + (ElementPathEvidence.Listed(signatures, p) ? "true" : "false") + ")")
                .ToArray();
            string walked = "new ToolHop[] { " + string.Join(", ", hops) + " }, "
                + (path.Listed ? "true" : "false");
            if (path.Kind == AccessPathKind.Child)
            {
                return "new ToolAccess(ToolAccessKind.Child, " + Literal(path.RowKey)
                    + ", " + walked + ", null, null)";
            }

            TypeRoleRecord element;
            string noun = byType.TryGetValue(
                TypeDefinitionName.OfElement(path.ElementType), out element)
                    ? Literal(element.ElementNoun)
                    : "null";

            return "new ToolAccess(ToolAccessKind.Element, " + Literal(path.RowKey)
                + ", " + walked + ", "
                + TypeOf(path.ElementType) + ", item => item is " + Code(path.ElementType) + ", "
                + noun + ", " + Items(path, signatures, concrete, byType) + ", "
                + (path.OwnerType == null ? "null" : TypeOf(path.OwnerType)) + ")";
        }

        /// <summary>
        /// そのリストが並べうる具象の型。要素の型が抽象で実体が複数の型に分かれるリストだけが持つ。
        /// </summary>
        private static string Items(
            AccessPath path,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType)
        {
            SignatureRecord signature;
            IList<string> listed;
            if (!signatures.TryGetValue(path.RowKey, out signature)
                || !concrete.TryGetValue(
                    TypeDefinitionName.OfElement(ValueTypeName.Contained(signature.ValueType)),
                    out listed))
            {
                return "null";
            }

            string[] items = listed
                .Where(byType.ContainsKey)
                .Select(t => "new ToolItem(" + Literal(byType[t].ElementNoun) + ", "
                    + TypeOf(t) + ", item => item is " + Code(t) + ")")
                .ToArray();

            return items.Length == 0
                ? "null"
                : "new ToolItem[] { " + string.Join(", ", items) + " }";
        }

        private static string List(SignatureRecord signature, string element)
        {
            string owner = "((" + Code(signature.DeclaringType) + ")owner)." + signature.MemberName;

            return "new SdkList("
                + "owner => " + owner + ".Count, "
                + "(owner, index) => " + owner + "[index], "
                + "(owner, item) => " + owner + ".Add((" + Code(element) + ")item), "
                + "(owner, index) => " + owner + ".RemoveAt(index))";
        }

        private static string Field(SignatureRecord signature)
        {
            return "new ToolField(" + Literal(SdkShapeEvidence.MemberNameOf(signature.MemberName))
                + ", " + Literal(signature.Key) + ", " + TypeOf(signature.ValueType) + ")";
        }

        /// <summary>
        /// 受け手の得方。所有の根の型はどのPMXを見るかの切り替えで選び、ほかは接続の道から得る。
        /// 静的なメンバーは相手を取らない。
        /// </summary>
        private static string Receiver(
            ToolMapRow row,
            SignatureRecord signature,
            AccessPath path,
            ToolMapEditKind? edit = null,
            bool bridged = false)
        {
            return Receiver(
                signature.DeclaringType, signature.IsStatic, path, edit ?? row.EditKind, bridged);
        }

        /// <summary>受け手の得方を、宣言型と道から決める。</summary>
        private static string Receiver(
            string declaringType,
            bool isStatic,
            AccessPath path,
            ToolMapEditKind edit,
            bool bridged = false)
        {
            string declaring = TypeDefinitionName.OfElement(declaringType);
            bool rooted = string.Equals(declaring, PmxTypeName, StringComparison.Ordinal)
                || (path != null && path.Kind != AccessPathKind.Whole);
            string type = isStatic || rooted ? "null" : Literal(declaring);

            return "new ToolReceiver(ToolReceiverKind."
                + (rooted ? "Pmx" : "Connection") + ", " + type + ", EditKind."
                + Edit(edit) + (bridged ? ", true" : string.Empty) + ")";
        }

        private static string Edit(ToolMapEditKind kind)
        {
            switch (kind)
            {
                case ToolMapEditKind.DuplicateEdit:
                    return "DuplicateEdit";

                case ToolMapEditKind.DirectChange:
                    return "DirectChange";

                case ToolMapEditKind.ViewSession:
                    return "ViewSession";

                default:
                    return "Read";
            }
        }

        /// <summary>
        /// 複製編集の流れが通る行キー。状態取得は引数を取らないもの、反映は複製だけを取るものを
        /// 採る。ほかの形は反映する範囲を別に受け取るので、流れの既定にはしない。
        /// </summary>
        private static IList<string> Flows(
            CommonAssignmentTable assignments, IDictionary<string, SignatureRecord> signatures)
        {
            return new[]
            {
                Flow(assignments, signatures, "stateRead", p => p.Count == 0),
                Flow(
                    assignments,
                    signatures,
                    "duplicateEdit",
                    p => p.Count == 1
                        && string.Equals(p[0].TypeName, PmxTypeName, StringComparison.Ordinal)),
                Flow(
                    assignments,
                    signatures,
                    "stateRead",
                    p => p.Count == 1 && IsConnector(p[0])),
                Flow(
                    assignments,
                    signatures,
                    "duplicateEdit",
                    p => p.Count == 3 && IsConnector(p[0])),
            };
        }

        /// <summary>その引数が、ホストが入れる常駐コネクタを取るか。</summary>
        private static bool IsConnector(ParameterRecord parameter)
        {
            return string.Equals(
                parameter.TypeName, TypeRoleEvidence.InjectedConnector, StringComparison.Ordinal);
        }

        /// <summary>その行が取る引数の置き場。並びは引数の並びと同じ。</summary>
        private static string Slots(
            IDictionary<string, SignatureRecord> signatures, string rowKey)
        {
            string[] slots = signatures[rowKey].Parameters
                .Select(p => "FlowSlot." + (IsConnector(p)
                    ? "Connector"
                    : string.Equals(p.TypeName, PmxTypeName, StringComparison.Ordinal)
                        ? "Pmx"
                        : "UndoLock"))
                .ToArray();

            return "new FlowSlot[] { " + string.Join(", ", slots) + " }";
        }

        /// <summary>複製編集の流れ1つをC#の式にする。</summary>
        private static string FlowText(
            CommonAssignmentTable assignments,
            IDictionary<string, SignatureRecord> signatures,
            string read,
            string commit,
            bool received)
        {
            string paired = Suppressing(signatures, commit)
                ? string.Empty
                : ", " + Literal(UndoRow(assignments, signatures, commit, StopUndoMember))
                    + ", " + Literal(UndoRow(assignments, signatures, commit, ResumeUndoMember));

            return "new PmxFlow(" + Literal(read) + ", " + Literal(commit) + ", "
                + (received ? Literal(DeclaringTypeOf(read)) : "null") + ", "
                + Slots(signatures, read) + ", " + Slots(signatures, commit) + paired + ")";
        }

        /// <summary>その反映が、Undoの記録を止めるかどうかを引数で取るか。</summary>
        private static bool Suppressing(
            IDictionary<string, SignatureRecord> signatures, string commit)
        {
            return signatures[commit].Parameters.Any(
                p => string.Equals(p.TypeName, FlagTypeName, StringComparison.Ordinal));
        }

        /// <summary>
        /// 反映と同じ受け手が持つ、Undoの記録を動かす行。抑止の共通引数へ割り当てた行のうち、
        /// その名前のものを採る。1件に決まらなければ組み立てを止める。
        /// </summary>
        private static string UndoRow(
            CommonAssignmentTable assignments,
            IDictionary<string, SignatureRecord> signatures,
            string commit,
            string memberName)
        {
            string owner = DeclaringTypeOf(commit);
            string[] found = assignments.Assignments
                .Where(a => a.Assignment == CommonAssignmentKind.CommonArg
                    && string.Equals(a.Target, SuppressUndoArgument, StringComparison.Ordinal)
                    && signatures.ContainsKey(a.SignatureKey)
                    && string.Equals(
                        signatures[a.SignatureKey].DeclaringType, owner, StringComparison.Ordinal)
                    && string.Equals(
                        signatures[a.SignatureKey].MemberName, memberName, StringComparison.Ordinal))
                .Select(a => a.SignatureKey)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();
            if (found.Length != 1)
            {
                throw new InvalidOperationException(
                    "Undoの記録を動かす行が1件に決まらない: " + memberName
                        + "(" + found.Length + " 件)");
            }

            return found[0];
        }

        private static string Flow(
            CommonAssignmentTable assignments,
            IDictionary<string, SignatureRecord> signatures,
            string target,
            Func<IList<ParameterRecord>, bool> shape)
        {
            string[] found = assignments.Assignments
                .Where(a => a.Assignment == CommonAssignmentKind.InternalFlow
                    && string.Equals(a.Target, target, StringComparison.Ordinal)
                    && signatures.ContainsKey(a.SignatureKey)
                    && shape(signatures[a.SignatureKey].Parameters))
                .Select(a => a.SignatureKey)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();
            if (found.Length != 1)
            {
                throw new InvalidOperationException(
                    "内部フローの行が1件に決まらない: " + target + "(" + found.Length + " 件)");
            }

            return found[0];
        }

        /// <summary>行キーが指すメンバーの宣言型。</summary>
        private static string DeclaringTypeOf(string rowKey)
        {
            int open = rowKey.IndexOf('(');
            string head = open < 0 ? rowKey : rowKey.Substring(0, open);

            return head.Substring(0, head.LastIndexOf('.'));
        }

        /// <summary>列挙が書く型名から、その型の綴り。</summary>
        private static string Code(string typeName)
        {
            return "global::" + typeName.Replace('+', '.');
        }

        /// <summary>列挙が書く型名から、その型そのものを渡す式。</summary>
        private static string TypeOf(string typeName)
        {
            return "typeof(" + Code(typeName) + ")";
        }

        private static string Literal(string text)
        {
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Compose(
            IDictionary<string, List<string>> calls,
            IDictionary<string, SortedDictionary<string, List<string>>> fields,
            IDictionary<string, string> aggregated,
            IDictionary<string, string> elements,
            IDictionary<string, string> lists,
            IList<string> flows,
            IDictionary<string, SignatureRecord> signatures,
            CommonAssignmentTable assignments)
        {
            StringBuilder text = new StringBuilder();
            text.Append("// この本文はビルドのたびに作り直す。手で直さない。\n");
            text.Append("using System;\n");
            text.Append("using System.Collections.Generic;\n");
            text.Append("\n");
            text.Append("namespace PmxEditorMcp\n");
            text.Append("{\n");
            text.Append("    internal static class GeneratedTools\n");
            text.Append("    {\n");
            text.Append("        /// <summary>SDKのメンバーへ中継するツール。</summary>\n");
            text.Append(
                "        internal static Dictionary<string, IList<ToolCall>> Calls()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, IList<ToolCall>> calls =\n");
            text.Append(
                "                new Dictionary<string, IList<ToolCall>>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, List<string>> call in calls)
            {
                text.Append(Indent).Append("calls.Add(").Append(Literal(call.Key))
                    .Append(", new ToolCall[] { ").Append(string.Join(", ", call.Value))
                    .Append(" });\n");
            }

            text.Append("\n");
            text.Append("            return calls;\n");
            text.Append("        }\n");
            text.Append("\n");
            text.Append("        /// <summary>項目を集めるツール。</summary>\n");
            text.Append("        internal static Dictionary<string, ToolFields> Aggregations()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, ToolFields> aggregations =\n");
            text.Append("                new Dictionary<string, ToolFields>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, SortedDictionary<string, List<string>>> tool in fields)
            {
                text.Append(Indent).Append("aggregations.Add(").Append(Literal(tool.Key))
                    .Append(", ").Append(aggregated[tool.Key]).Append("\n");
                text.Append(Indent).Append("{\n");
                foreach (KeyValuePair<string, List<string>> set in tool.Value)
                {
                    text.Append(Indent).Append("    new ToolFieldSet(")
                        .Append(set.Key.Length == 0 ? "null" : Literal(set.Key))
                        .Append(", new ToolField[]\n");
                    text.Append(Indent).Append("    {\n");
                    foreach (string field in set.Value)
                    {
                        text.Append(Indent).Append("        ").Append(field).Append(",\n");
                    }

                    text.Append(Indent).Append("    }),\n");
                }

                text.Append(Indent).Append("}));\n");
            }

            text.Append("\n");
            text.Append("            return aggregations;\n");
            text.Append("        }\n");
            text.Append("\n");
            text.Append("        /// <summary>所有するリストへ加える・から取り除くツール。</summary>\n");
            text.Append("        internal static Dictionary<string, ToolElements> Elements()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, ToolElements> elements =\n");
            text.Append("                new Dictionary<string, ToolElements>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, string> element in elements)
            {
                text.Append(Indent).Append("elements.Add(").Append(Literal(element.Key))
                    .Append(", ").Append(element.Value).Append(");\n");
            }

            text.Append("\n");
            text.Append("            return elements;\n");
            text.Append("        }\n");
            text.Append("    }\n");
            text.Append("\n");
            text.Append("    internal static class GeneratedSdkLists\n");
            text.Append("    {\n");
            text.Append("        /// <summary>行キーから、所有するリストを読み書きする中継を引く。</summary>\n");
            text.Append("        internal static Dictionary<string, SdkList> Create()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, SdkList> lists =\n");
            text.Append("                new Dictionary<string, SdkList>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, string> list in lists)
            {
                text.Append(Indent).Append("lists.Add(").Append(Literal(list.Key)).Append(",\n");
                text.Append(Indent).Append("    ").Append(list.Value).Append(");\n");
            }

            text.Append("\n");
            text.Append("            return lists;\n");
            text.Append("        }\n");
            text.Append("    }\n");
            text.Append("\n");
            text.Append("    internal static class GeneratedSdkFlows\n");
            text.Append("    {\n");
            text.Append("        /// <summary>PMXのコネクタが受け持つ、複製編集の流れ。</summary>\n");
            text.Append("        internal static readonly PmxFlow Current =\n");
            text.Append("            ")
                .Append(FlowText(assignments, signatures, flows[0], flows[1], true))
                .Append(";\n");
            text.Append("\n");
            text.Append(
                "        /// <summary>Cプラグイン連携の橋渡しが受け持つ、複製編集の流れ。</summary>\n");
            text.Append("        internal static readonly PmxFlow Bridge =\n");
            text.Append("            ")
                .Append(FlowText(assignments, signatures, flows[2], flows[3], false))
                .Append(";\n");
            text.Append("\n");
            text.Append("        /// <summary>PMXの実体の型。ハンドルの型を見分けるのに使う。</summary>\n");
            text.Append("        internal static readonly Type Pmx = ")
                .Append(TypeOf(PmxTypeName)).Append(";\n");
            text.Append("    }\n");
            text.Append("}\n");

            return text.ToString();
        }
    }
}
