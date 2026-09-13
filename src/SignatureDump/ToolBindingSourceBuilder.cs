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

        private const string ReleaseToolName = "session_release_handle";

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
            CommonAssignmentTable assignments,
            ToolSchemaTable schemas,
            IDictionary<string, string> shapesByType)
        {
            if (shapesByType == null)
            {
                throw new ArgumentNullException(nameof(shapesByType));
            }

            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

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

            IDictionary<string, SchemaBranch> selected =
                SelectedBranches(schemas, map, signatures, toolNames, shapesByType);
            IDictionary<string, string> projections =
                Projections(map, signatures, byType, toolNames);
            ISet<string> responding = Responding(map, signatures, toolNames);

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

                    string carried;
                    overloads.Add(
                        Call(
                            Held(selected, row.SignatureKey),
                            row,
                            signature,
                            path,
                            dangerous,
                            signatures,
                            concrete,
                            byType,
                            paths,
                            bridged,
                            projections.TryGetValue(tool, out carried) ? carried : null,
                            assignments,
                            map,
                            tool,
                            responding.Contains(tool)));
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
                        ElementPathEvidence.Owner(signatures, issued, row.SignatureKey));
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

                    members.Add(Field(
                        signature,
                        null,
                        Positioning(signature, signatures, concrete, byType, paths)));
                }
            }

            SortedDictionary<string, string> preconditions =
                Preconditions(map, signatures, toolNames, calls);
            SortedDictionary<string, List<string>> listened = Listened(map, signatures);
            SortedDictionary<string, string> payloads = Payloads(map, signatures, schemas);

            return new ToolBindingSource(
                Compose(
                    calls,
                    fields,
                    aggregated,
                    elements,
                    lists,
                    preconditions,
                    listened,
                    payloads,
                    Flows(assignments, signatures),
                    signatures,
                    assignments),
                calls.Keys.ToList(),
                fields.Keys.ToList(),
                elements.Keys.ToList());
        }

        /// <summary>
        /// 応答をハンドルの並びで返すツールの名前。呼び分けのどれかが並びを預けるなら、どの
        /// 呼び分けでも並びで返す——選んだ呼び分けで応答の形が変わらないようにする。
        /// </summary>
        private static ISet<string> Responding(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames)
        {
            HashSet<string> responding = new HashSet<string>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows)
            {
                SignatureRecord signature;
                string tool;
                string element;
                if (signatures.TryGetValue(row.SignatureKey, out signature)
                    && toolNames.TryGetValue(row.SignatureKey, out tool)
                    && HandleIssuanceEvidence.Issues(row, signature)
                    && ValueTypeName.TryElement(signature.ValueType, out element))
                {
                    responding.Add(tool);
                }
            }

            return responding;
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
            if (row.EmbeddedIn == null
                || !byType.TryGetValue(declaring, out owner)
                || !TypeRoleRecord.HasIndependentTool(owner.Role))
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
            SchemaBranch selected,
            ToolMapRow row,
            SignatureRecord signature,
            AccessPath path,
            IDictionary<string, DangerKind> dangerous,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, AccessPath> paths,
            ISet<string> bridged,
            string projected,
            CommonAssignmentTable assignments,
            ToolMap map,
            string tool,
            bool responds)
        {
            DangerKind kind;
            string danger = dangerous.TryGetValue(signature.Key, out kind)
                ? "DangerKind." + kind
                : "DangerKind.None";
            string[] arguments = signature.Parameters
                .Where(p => p.Direction != ParameterDirection.Out)
                .Select(p => Argument(p, signatures, concrete, byType, paths, map, tool))
                .ToArray();
            string[] outputs = signature.Parameters
                .Where(p => p.Direction != ParameterDirection.In)
                .Select(p => "new ToolArgument(" + Literal(p.Name) + ", " + TypeOf(p.TypeName) + ")")
                .ToArray();

            bool issuing = HandleIssuanceEvidence.Issues(row, signature);
            string[] release = issuing
                ? Releases(signature, signatures, assignments)
                : new string[0];
            string made = ValueTypeName.Contained(signature.ValueType);
            bool many = (issuing || projected != null)
                && !string.Equals(made, signature.ValueType, StringComparison.Ordinal);
            string releases = release.Length == 0 ? null : release[0];
            string releasesIssued = release.Length == 0 ? null : release[1];
            string returnsMany = many ? "true" : responds ? "false" : null;
            if (returnsMany != null && releasesIssued == null)
            {
                releasesIssued = "false";
            }

            string tail = Tail(
                issuing ? TypeOf(made) : null,
                projected,
                releases,
                releasesIssued,
                returnsMany,
                responds ? "true" : null);

            string chosen = selected == null
                ? string.Empty
                : ", selectorName: " + Literal(selected.SelectorName)
                    + ", selectorValue: " + Literal((string)selected.SelectorValue);

            return "new ToolCall(" + Literal(signature.Key) + ", "
                + Receiver(
                    row,
                    signature,
                    path,
                    null,
                    Bridges(signature, path, bridged),
                    Held(signature.DeclaringType, byType)) + ", "
                + Access(path, signatures, concrete, byType) + ", "
                + danger + ", new ToolArgument[] { " + string.Join(", ", arguments) + " }, "
                + "new ToolArgument[] { " + string.Join(", ", outputs) + " }, "
                + (string.Equals(signature.ValueType, VoidTypeName, StringComparison.Ordinal)
                    ? "null"
                    : TypeOf(signature.ValueType))
                + tail + chosen + ")";
        }

        /// <summary>
        /// 行キーから、その行が呼ばれる呼び分けへ。分岐を選ぶ項目を持つツールだけが項目を持つ。
        /// </summary>
        private static IDictionary<string, SchemaBranch> SelectedBranches(
            ToolSchemaTable schemas,
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames,
            IDictionary<string, string> shapesByType)
        {
            Dictionary<string, SchemaBranch> selected =
                new Dictionary<string, SchemaBranch>(StringComparer.Ordinal);
            foreach (ToolSchema schema in schemas.Tools
                .Where(t => t.Branches.Any(b => b.SelectorName != null)))
            {
                IList<SignatureRecord> rows = map.Rows
                    .Where(r => toolNames.ContainsKey(r.SignatureKey)
                        && string.Equals(
                            toolNames[r.SignatureKey], schema.Tool, StringComparison.Ordinal)
                        && signatures.ContainsKey(r.SignatureKey))
                    .Select(r => signatures[r.SignatureKey])
                    .ToList();
                foreach (KeyValuePair<string, SchemaBranch> pair in
                    BranchRowRule.Resolve(schema, rows, shapesByType))
                {
                    selected.Add(pair.Key, pair.Value);
                }
            }

            return selected;
        }

        private static SchemaBranch Held(
            IDictionary<string, SchemaBranch> selected, string rowKey)
        {
            SchemaBranch branch;

            return selected.TryGetValue(rowKey, out branch) ? branch : null;
        }

        /// <summary>
        /// 既定を採る引数の並び。後ろから続く既定だけの並びは書かずに済ませる。
        /// </summary>
        private static string Tail(params string[] given)
        {
            int last = given.Length;
            while (last > 0 && given[last - 1] == null)
            {
                last--;
            }

            return string.Concat(given.Take(last).Select(g => ", " + (g ?? "null")));
        }

        /// <summary>
        /// その実体を手放す行のキーと、手放す呼び出しが生成物を引数に取るか。手順を持たない型では
        /// 空。解放のツールが受け持つと定めたメンバーのうち、その型が宣言する引数なしのものか、
        /// 預ける呼び出しと同じ型が宣言してその実体を1つだけ引数に取るものがこれに当たる。どちらの
        /// 形でも呼べないなら、預けても手放せないので <see cref="InvalidOperationException"/>。
        /// </summary>
        private static string[] Releases(
            SignatureRecord issuing,
            IDictionary<string, SignatureRecord> signatures,
            CommonAssignmentTable assignments)
        {
            string valueType = issuing.ValueType;
            string held = TypeDefinitionName.OfElement(valueType);
            SignatureRecord[] members = assignments.Assignments
                .Where(a => a.Assignment == CommonAssignmentKind.Tool
                    && string.Equals(a.Target, ReleaseToolName, StringComparison.Ordinal))
                .Select(a => a.SignatureKey)
                .Where(k => signatures.ContainsKey(k))
                .Select(k => signatures[k])
                .Where(r => Declares(r, held) || Takes(r, held))
                .OrderBy(r => r.Key, StringComparer.Ordinal)
                .ToArray();
            SignatureRecord[] callable = members
                .Where(r => Declares(r, held) && r.Parameters.Count == 0)
                .ToArray();
            SignatureRecord[] owned = members
                .Where(r => Declares(r, TypeDefinitionName.OfElement(issuing.DeclaringType))
                    && r.Parameters.Count == 1
                    && Takes(r, held))
                .ToArray();
            if (callable.Length + owned.Length > 1)
            {
                throw new InvalidOperationException(
                    "手放す行が2つ以上ある型を預けている: " + held);
            }

            if (callable.Length != 0)
            {
                return new[] { Literal(callable[0].Key), "false" };
            }

            if (owned.Length != 0)
            {
                return new[] { Literal(owned[0].Key), "true" };
            }

            if (members.Length != 0)
            {
                throw new InvalidOperationException(
                    "手放す手順を呼べない形の型を預けている: " + held + "(" + members[0].Key + ")");
            }

            return new string[0];
        }

        /// <summary>そのメンバーをその型が宣言するか。</summary>
        private static bool Declares(SignatureRecord signature, string typeName)
        {
            return string.Equals(
                TypeDefinitionName.OfElement(signature.DeclaringType),
                typeName,
                StringComparison.Ordinal);
        }

        /// <summary>そのメンバーがその型の実体を引数に取るか。</summary>
        private static bool Takes(SignatureRecord signature, string typeName)
        {
            return signature.Parameters.Any(
                p => string.Equals(
                    TypeDefinitionName.OfElement(p.TypeName), typeName, StringComparison.Ordinal));
        }

        /// <summary>
        /// 引数1件をC#の式にする。ホストが入れる引数はその印を持ち、操作対象型を取る引数はその実体を
        /// 並べるリストの中の位置で受け取る。
        /// </summary>
        private static string Argument(
            ParameterRecord parameter,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, AccessPath> paths,
            ToolMap map = null,
            string tool = null)
        {
            string written = "new ToolArgument(" + Literal(parameter.Name) + ", "
                + TypeOf(parameter.TypeName);
            string typeName = TypeDefinitionName.OfElement(parameter.TypeName);
            if (string.Equals(typeName, PmxTypeName, StringComparison.Ordinal))
            {
                return written + ", true)";
            }

            if (IsConnector(parameter))
            {
                return written + ", true, null, true)";
            }

            TypeRoleRecord role;
            AccessPath listed;
            if (byType.TryGetValue(typeName, out role) && role.Role == TypeRole.HandleTarget)
            {
                return written + ", false, null, false, " + TypeOf(typeName) + ")";
            }

            if (byType.TryGetValue(typeName, out role) && role.Role == TypeRole.Connector)
            {
                return written + ", true, null, false, null, " + Literal(typeName) + ")";
            }

            if (map != null && byType.TryGetValue(typeName, out role) && role.Role == TypeRole.Dto)
            {
                return written + ", false, null, false, null, null, "
                    + Building(typeName, tool, map, signatures) + ")";
            }

            if (!byType.TryGetValue(typeName, out role)
                || role.Role != TypeRole.OperationTarget
                || !paths.TryGetValue(typeName, out listed))
            {
                return written + ")";
            }

            return written + ", false, " + Access(listed, signatures, concrete, byType) + ")";
        }

        /// <summary>その型の受け手をハンドルから得るか。ハンドル操作型だけが当たる。</summary>
        private static bool Held(
            string declaringType, IDictionary<string, TypeRoleRecord> byType)
        {
            TypeRoleRecord role;

            return byType.TryGetValue(TypeDefinitionName.OfElement(declaringType), out role)
                && role.Role == TypeRole.HandleTarget;
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
                + Receiver(
                    owner.TypeName,
                    false,
                    path,
                    writes ? row.EditKind : ToolMapEditKind.Read,
                    false,
                    owner.Role == TypeRole.HandleTarget)
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
        /// 位置で指す型のリストが並べうる具象の型。
        /// </summary>
        private static string Items(
            AccessPath path,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType)
        {
            SignatureRecord signature;
            TypeRoleRecord element;
            IList<string> listed;
            if (!signatures.TryGetValue(path.RowKey, out signature))
            {
                return "null";
            }

            string value = TypeDefinitionName.OfElement(
                ValueTypeName.Contained(signature.ValueType));
            if (!byType.TryGetValue(value, out element)
                || element.Role != TypeRole.OperationTarget
                || !concrete.TryGetValue(value, out listed))
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

        private static string Field(
            SignatureRecord signature, string members = null, string referenced = null)
        {
            string written = "new ToolField("
                + Literal(SdkShapeEvidence.MemberNameOf(signature.MemberName))
                + ", " + Literal(signature.Key) + ", " + TypeOf(signature.ValueType);
            if (members == null && referenced == null)
            {
                return written + ")";
            }

            written += ", " + (members ?? "null");

            return (referenced == null ? written : written + ", " + referenced) + ")";
        }

        /// <summary>
        /// その項目の値が操作対象の実体なら、位置を数えるリストへの道。指さない項目では null。
        /// </summary>
        private static string Positioning(
            SignatureRecord signature,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, AccessPath> paths)
        {
            string value = TypeDefinitionName.Of(signature.ValueType);
            TypeRoleRecord role;
            AccessPath listed;
            if (!byType.TryGetValue(value, out role)
                || role.Role != TypeRole.OperationTarget
                || !paths.TryGetValue(value, out listed))
            {
                return null;
            }

            return Access(listed, signatures, concrete, byType);
        }

        /// <summary>
        /// ツールごとの、返す値の中の項目。運搬用の型を返す行だけが持ち、その型の項目を持ち込む
        /// 行から組み立てる。中がまた運搬用の型なら、その中も同じ規則で組み立てる。
        /// </summary>
        private static IDictionary<string, string> Projections(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, string> toolNames)
        {
            Dictionary<string, string> projected =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows)
            {
                SignatureRecord signature;
                string tool;
                if (!signatures.TryGetValue(row.SignatureKey, out signature)
                    || !toolNames.TryGetValue(row.SignatureKey, out tool)
                    || !Carried(signature.ValueType, byType))
                {
                    continue;
                }

                projected.Add(tool, Carrying(signature.ValueType, tool, map, signatures, byType));
            }

            return projected;
        }

        /// <summary>その型が、独立したツールを持たない運搬用の型か。</summary>
        private static bool Carried(
            string typeName, IDictionary<string, TypeRoleRecord> byType)
        {
            TypeRoleRecord record;

            return byType.TryGetValue(TypeDefinitionName.OfElement(typeName), out record)
                && record.Role == TypeRole.Dto;
        }

        /// <summary>
        /// 組で受け取る引数の組み立て方。書き込める項目をそのツールへ持ち込む行から採る。持ち込む
        /// 行が無ければ、渡された組から実体を作れないので <see cref="InvalidOperationException"/>。
        /// </summary>
        private static string Building(
            string typeName,
            string tool,
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures)
        {
            List<string> members = new List<string>();
            foreach (ToolMapRow row in map.Rows.OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                if (row.EmbeddedIn == null
                    || !row.EmbeddedIn.Contains(tool, StringComparer.Ordinal)
                    || !signatures.TryGetValue(row.SignatureKey, out signature)
                    || !signature.CanWrite
                    || !string.Equals(
                        TypeDefinitionName.OfElement(signature.DeclaringType),
                        typeName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                members.Add("new ToolValueMember("
                    + Literal(SdkShapeEvidence.MemberNameOf(signature.MemberName)) + ", "
                    + TypeOf(signature.ValueType) + ", (made, value) => ((" + Code(typeName)
                    + ")made)." + signature.MemberName + " = (" + Code(signature.ValueType)
                    + ")value)");
            }

            if (members.Count == 0)
            {
                throw new InvalidOperationException(
                    "組で受け取る引数の項目を持ち込む行が無い: " + tool + "(" + typeName + ")");
            }

            return "new ToolValueShape(() => new " + Code(typeName)
                + "(), new ToolValueMember[] { " + string.Join(", ", members) + " })";
        }

        /// <summary>その運搬用の型の項目を、そのツールへ持ち込む行から組み立てる。</summary>
        private static string Carrying(
            string typeName,
            string tool,
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, TypeRoleRecord> byType)
        {
            string carried = TypeDefinitionName.OfElement(typeName);
            List<string> fields = new List<string>();
            foreach (ToolMapRow row in map.Rows.OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                if (row.EmbeddedIn == null
                    || !row.EmbeddedIn.Contains(tool, StringComparer.Ordinal)
                    || !signatures.TryGetValue(row.SignatureKey, out signature)
                    || !signature.CanRead
                    || !string.Equals(
                        TypeDefinitionName.OfElement(signature.DeclaringType),
                        carried,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                fields.Add(Field(
                    signature,
                    Carried(signature.ValueType, byType)
                        ? Carrying(signature.ValueType, tool, map, signatures, byType)
                        : null));
            }

            if (fields.Count == 0)
            {
                throw new InvalidOperationException(
                    "返す運搬用の型の項目を持ち込む行が無い: " + tool + "(" + carried + ")");
            }

            return "new ToolField[] { " + string.Join(", ", fields) + " }";
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
            bool bridged = false,
            bool held = false)
        {
            return Receiver(
                signature.DeclaringType,
                signature.IsStatic,
                path,
                edit ?? row.EditKind,
                bridged,
                held);
        }

        /// <summary>受け手の得方を、宣言型と道から決める。</summary>
        private static string Receiver(
            string declaringType,
            bool isStatic,
            AccessPath path,
            ToolMapEditKind edit,
            bool bridged = false,
            bool held = false)
        {
            string declaring = TypeDefinitionName.OfElement(declaringType);
            bool rooted = string.Equals(declaring, PmxTypeName, StringComparison.Ordinal)
                || (path != null && path.Kind != AccessPathKind.Whole);
            string type = isStatic || rooted ? "null" : Literal(declaring);
            if (held)
            {
                return "new ToolReceiver(ToolReceiverKind.Handle, " + Literal(declaring)
                    + ", EditKind." + Edit(edit) + ", false, item => item is "
                    + Code(declaringType) + ")";
            }

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

        /// <summary>リスナの型ごとに、公開イベントへ受け手を掛ける文を書き出す。</summary>
        private static void Attachments(
            StringBuilder text, IDictionary<string, List<string>> listened)
        {
            text.Append("\n");
            text.Append("        /// <summary>リスナの公開イベントへ受け手を掛ける。</summary>\n");
            text.Append("        internal static Dictionary<string, EventAttach> Attachments()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, EventAttach> attachments =\n");
            text.Append(
                "                new Dictionary<string, EventAttach>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, List<string>> listener in listened)
            {
                text.Append(Indent).Append("attachments.Add(").Append(Literal(listener.Key))
                    .Append(", (listener, sink) =>\n");
                text.Append(Indent).Append("{\n");
                text.Append(Indent).Append("    ").Append(Code(listener.Key))
                    .Append(" typed = (").Append(Code(listener.Key)).Append(")listener;\n");
                for (int at = 0; at < listener.Value.Count; at++)
                {
                    string[] parts = listener.Value[at].Split('|');
                    text.Append(Indent).Append("    ").Append(parts[0]).Append(" held")
                        .Append(at).Append(" = (sender, e) => sink(").Append(parts[2])
                        .Append(", ").Append(parts[3]).Append(");\n");
                    text.Append(Indent).Append("    typed.").Append(parts[1]).Append(" += held")
                        .Append(at).Append(";\n");
                }

                text.Append(Indent).Append("    return () =>\n");
                text.Append(Indent).Append("    {\n");
                for (int at = 0; at < listener.Value.Count; at++)
                {
                    text.Append(Indent).Append("        typed.")
                        .Append(listener.Value[at].Split('|')[1]).Append(" -= held").Append(at)
                        .Append(";\n");
                }

                text.Append(Indent).Append("    };\n");
                text.Append(Indent).Append("});\n");
            }

            text.Append("\n");
            text.Append("            return attachments;\n");
            text.Append("        }\n");
        }

        /// <summary>イベント種別ごとに、値を組へ直す文を書き出す。</summary>
        private static void Payloads(StringBuilder text, IDictionary<string, string> payloads)
        {
            text.Append("\n");
            text.Append("        /// <summary>イベント固有の値を組へ直す。</summary>\n");
            text.Append("        internal static Dictionary<string, PayloadReader> Payloads()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, PayloadReader> payloads =\n");
            text.Append(
                "                new Dictionary<string, PayloadReader>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, string> payload in payloads)
            {
                string[] parts = payload.Value.Split('|');
                string[] members = parts[1].Length == 0 ? new string[0] : parts[1].Split(',');
                text.Append(Indent).Append("payloads.Add(").Append(Literal(payload.Key))
                    .Append(", args =>\n");
                text.Append(Indent).Append("{\n");
                text.Append(Indent).Append("    Dictionary<string, object> read =\n");
                text.Append(Indent)
                    .Append("        new Dictionary<string, object>(StringComparer.Ordinal);\n");
                if (members.Length != 0)
                {
                    text.Append(Indent).Append("    ").Append(Code(parts[0])).Append(" taken = (")
                        .Append(Code(parts[0])).Append(")args;\n");
                }

                foreach (string member in members)
                {
                    text.Append(Indent).Append("    read.Add(").Append(Literal(member))
                        .Append(", EventPayload.Of(taken.").Append(Declared(member))
                        .Append("));\n");
                }

                text.Append(Indent).Append("    return read;\n");
                text.Append(Indent).Append("});\n");
            }

            text.Append("\n");
            text.Append("            return payloads;\n");
            text.Append("        }\n");
        }

        /// <summary>応答の項目の名前から、それを持つメンバーの名前。頭を大文字へ戻す。</summary>
        private static string Declared(string name)
        {
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
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

        /// <summary>
        /// リスナの型ごとの、公開イベントへ受け手を掛ける文。掛けた受け手は同じ並びで外す。
        /// </summary>
        private static SortedDictionary<string, List<string>> Listened(
            ToolMap map, IDictionary<string, SignatureRecord> signatures)
        {
            SortedDictionary<string, List<string>> listened =
                new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows
                .Where(r => r.EventType != null && signatures.ContainsKey(r.SignatureKey))
                .OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature = signatures[row.SignatureKey];
                List<string> events;
                if (!listened.TryGetValue(signature.DeclaringType, out events))
                {
                    events = new List<string>();
                    listened.Add(signature.DeclaringType, events);
                }

                string carried = Carried(signature.ValueType);
                events.Add(
                    (carried == null
                        ? "EventHandler"
                        : "EventHandler<" + Code(carried) + ">")
                    + "|" + signature.MemberName + "|" + Literal(row.EventType)
                    + "|" + (carried == null ? "null" : "e"));
            }

            return listened;
        }

        /// <summary>そのイベントの受け手が受け取る値の型。値を持たないイベントでは null。</summary>
        private static string Carried(string handlerType)
        {
            int opened = handlerType.IndexOf('<');

            return opened < 0
                ? null
                : handlerType.Substring(opened + 1, handlerType.Length - opened - 2);
        }

        /// <summary>
        /// イベント種別ごとの、値を組へ直す文。項目の名前と綴りはスキーマ正本が持つ——取り出しは
        /// 1つのSDKメンバーへ写らない合成ツールなので、その形の正本はそちらにある。
        /// </summary>
        private static SortedDictionary<string, string> Payloads(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            ToolSchemaTable schemas)
        {
            IDictionary<string, SchemaPayload> described = schemas.Tools
                .Where(t => t.Payloads != null)
                .SelectMany(t => t.Payloads)
                .ToDictionary(p => p.Type, p => p, StringComparer.Ordinal);
            SortedDictionary<string, string> payloads =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows
                .Where(r => r.EventType != null && signatures.ContainsKey(r.SignatureKey))
                .OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                if (payloads.ContainsKey(row.EventType))
                {
                    continue;
                }

                SchemaPayload payload;
                if (!described.TryGetValue(row.EventType, out payload))
                {
                    throw new InvalidOperationException(
                        "イベント種別の値の形がスキーマ正本に無い: " + row.EventType);
                }

                payloads.Add(
                    row.EventType,
                    Carried(signatures[row.SignatureKey].ValueType) + "|"
                        + string.Join(
                            ",", payload.Members.Select(m => m.Name).ToArray()));
            }

            return payloads;
        }

        /// <summary>
        /// ツールの名前から、呼ぶ前に確かめることの組み立て文へ。確かめる材料は、別の受け手から読む
        /// ものは読み取りのツールの名前で、同じ受け手の上で読むものは行キーで添える。材料が揃って
        /// いるかを見るのは、これを読んで組み立てる側である。
        /// </summary>
        private static SortedDictionary<string, string> Preconditions(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames,
            IDictionary<string, List<string>> calls)
        {
            SortedDictionary<string, string> preconditions =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            string counting = PreconditionRule.Counting(signatures.Values);
            string[] picked = PreconditionRule.Picked(signatures.Values)
                .Where(toolNames.ContainsKey)
                .Select(k => toolNames[k])
                .Where(calls.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            foreach (ToolMapRow row in map.Rows)
            {
                SignatureRecord signature;
                PreconditionKind kind;
                string tool;
                if (!signatures.TryGetValue(row.SignatureKey, out signature)
                    || !PreconditionRule.TryClassify(signature, out kind)
                    || !toolNames.TryGetValue(row.SignatureKey, out tool))
                {
                    continue;
                }

                string[] tools = kind == PreconditionKind.PickedObjects ? picked : new string[0];
                string[] rows = kind == PreconditionKind.SavedEdits && counting != null
                    ? new[] { counting }
                    : new string[0];
                preconditions[tool] = "new ToolPrecondition(PreconditionKind." + kind
                    + ", new string[] { " + string.Join(", ", tools.Select(Literal))
                    + " }, new string[] { " + string.Join(", ", rows.Select(Literal)) + " })";
            }

            return preconditions;
        }

        private static string Compose(
            IDictionary<string, List<string>> calls,
            IDictionary<string, SortedDictionary<string, List<string>>> fields,
            IDictionary<string, string> aggregated,
            IDictionary<string, string> elements,
            IDictionary<string, string> lists,
            IDictionary<string, string> preconditions,
            IDictionary<string, List<string>> listened,
            IDictionary<string, string> payloads,
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
            text.Append("        /// <summary>呼ぶ前に確かめることを持つツール。</summary>\n");
            text.Append(
                "        internal static Dictionary<string, ToolPrecondition> Preconditions()\n");
            text.Append("        {\n");
            text.Append("            Dictionary<string, ToolPrecondition> preconditions =\n");
            text.Append(
                "                new Dictionary<string, ToolPrecondition>(StringComparer.Ordinal);\n");
            foreach (KeyValuePair<string, string> precondition in preconditions)
            {
                text.Append(Indent).Append("preconditions.Add(").Append(Literal(precondition.Key))
                    .Append(", ").Append(precondition.Value).Append(");\n");
            }

            text.Append("\n");
            text.Append("            return preconditions;\n");
            text.Append("        }\n");
            Attachments(text, listened);
            Payloads(text, payloads);
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
