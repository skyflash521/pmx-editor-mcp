using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ツールを持つ行のシグネチャと、そのツールの入出力の形が対応することを確かめる。引数と受け手と
    /// 戻り値は呼び出しの成立に要るので、スキーマの側に行き先が無ければその行は呼べない。
    /// </summary>
    public static class SchemaCorrespondenceGate
    {
        /// <summary>値を返さないことを表す型の名前。</summary>
        private const string VoidTypeName = "System.Void";

        /// <summary>値を返さないことを表す綴り。</summary>
        private const string NullSpelling = "null_value";

        /// <summary>[対象の集合]が定める指し方。受け手を集合で受け取る入力の名前。</summary>
        private static readonly string[] TargetSelectors = { "all", "handles", "indices", "range" };

        /// <summary>ハンドルで操作する型の受け手の入力の名前。</summary>
        private const string HandleSelector = "handles";

        /// <summary>発行する数を受け取る入力の名前。</summary>
        private const string CountName = "count";

        private const string IndicesName = "indices";

        /// <summary>切り出す行の入力と応答の項目の名前。</summary>
        private static readonly string[] PagedInputs = { "offset", "limit" };

        private static readonly string[] PagedOutputs = { "total", "items", "nextOffset" };

        private const string PagedItemsName = "items";

        private const string RunsName = "runs";

        private const string ItemRunsName = "itemRuns";

        private const string NumbersType = "System.Int32[]";

        private const string IndicesParameters = "(System.Int32[])";

        private const string WriterPrefix = ".Set";

        private const string ReaderPrefix = ".Get";

        /// <summary>
        /// ホストが入れる引数の型。呼び出す側は持てないので、入力として受け取らない。接続の道から
        /// 得る受け手を取る引数も同じで、そちらは型役割から分かる。
        /// </summary>
        private static readonly string[] HostSupplied =
        {
            ElementPathEvidence.PmxTypeName, TypeRoleEvidence.InjectedConnector,
        };

        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolMap map,
            ToolSchemaTable schemas,
            TypeRoleTable roles,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames,
            IDictionary<string, AccessPath> paths)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (toolNames == null)
            {
                throw new ArgumentNullException(nameof(toolNames));
            }

            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            IDictionary<string, ToolSchema> byTool = schemas.Tools.ToDictionary(
                t => t.Tool, t => t, StringComparer.Ordinal);
            HashSet<string> issuing = new HashSet<string>(
                roles.Issuances.Where(i => i.Issues).Select(i => i.SignatureKey),
                StringComparer.Ordinal);
            IDictionary<string, TypeRole> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t.Role, StringComparer.Ordinal);

            HashSet<string> batching = new HashSet<string>(
                map.Rows
                    .Where(r => toolNames.ContainsKey(r.SignatureKey)
                        && HandleIssuanceEvidence.Batches(r, signatures[r.SignatureKey], byType))
                    .Select(r => toolNames[r.SignatureKey]),
                StringComparer.Ordinal);

            foreach (ToolMapRow row in map.Rows
                .Where(r => toolNames.ContainsKey(r.SignatureKey))
                .OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature = signatures[row.SignatureKey];
                string tool = toolNames[row.SignatureKey];
                ToolSchema schema;
                if (!byTool.TryGetValue(tool, out schema))
                {
                    throw new InvalidOperationException(
                        "行が持つツールの入出力の形が無い: " + tool);
                }

                RequireArguments(signature, schema);
                RequireInjection(signature, schema, byType);
                RequireReceiver(signature, schema, byType, paths);
                RequireOutput(signature, schema);
                if (PagedCallRule.Pages(row, signature))
                {
                    RequirePaged(schema, signature);
                }

                if (issuing.Contains(row.SignatureKey))
                {
                    RequireDerivedIssuanceLimit(schema);
                }

                if (batching.Contains(tool))
                {
                    RequireBatchedIssuance(schema);
                }
            }

            RequireSetupTools(map, byTool);
            RequireWritersTakeEmpty(map, byTool, toolNames);
        }

        private static void RequireWritersTakeEmpty(
            ToolMap map,
            IDictionary<string, ToolSchema> byTool,
            IDictionary<string, string> toolNames)
        {
            HashSet<string> keys = new HashSet<string>(
                map.Rows.Select(r => r.SignatureKey), StringComparer.Ordinal);
            foreach (string key in keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                string reader = ReaderKey(key);
                string tool;
                ToolSchema schema;
                if (reader == null
                    || !keys.Contains(reader)
                    || !toolNames.TryGetValue(key, out tool)
                    || !byTool.TryGetValue(tool, out schema))
                {
                    continue;
                }

                bool refuses = schema.Branches
                    .SelectMany(b => b.Inputs)
                    .SelectMany(i => i.WithNested)
                    .Any(i => string.Equals(i.Name, IndicesName, StringComparison.Ordinal)
                        && i.Origin == null
                        && i.Element != null
                        && !i.EmptyAllowed);
                if (refuses)
                {
                    throw new InvalidOperationException(
                        "読んだ並びを書き戻せない: " + tool + "(" + IndicesName + " が空を受け取らない)");
                }
            }
        }

        /// <summary>組にならなければ null。</summary>
        private static string ReaderKey(string key)
        {
            if (!key.EndsWith(IndicesParameters, StringComparison.Ordinal))
            {
                return null;
            }

            int at = key.LastIndexOf(WriterPrefix, StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }

            int from = at + WriterPrefix.Length;
            string member = key.Substring(from, key.Length - from - IndicesParameters.Length);

            return key.Substring(0, at) + ReaderPrefix + member + "()";
        }

        /// <summary>
        /// 事後条件の用意が呼ぶツールが、スキーマ正本に在ることを求める。無い名前を書いた用意は
        /// 実行できない。
        /// </summary>
        private static void RequireSetupTools(
            ToolMap map, IDictionary<string, ToolSchema> byTool)
        {
            foreach (SetupOperation operation in map.Rows
                .OrderBy(r => r.SignatureKey, StringComparer.Ordinal)
                .SelectMany(ToolMapGate.Setups)
                .Where(o => o.Tag == SetupTag.CallTool))
            {
                if (!byTool.ContainsKey(operation.ToolName))
                {
                    throw new InvalidOperationException(
                        "用意が呼ぶツールがスキーマ正本に無い: " + operation.ToolName);
                }
            }
        }

        /// <summary>
        /// ホストが入れる引数の入力だけが、受け取らない印を持つことを求める。器の内側へ置いた入力も
        /// 同じ——印がずれると、呼ぶ側は渡すよう求められた値をホストに捨てられるか、渡せない値を
        /// 求められる。
        /// </summary>
        private static void RequireInjection(
            SignatureRecord signature,
            ToolSchema schema,
            IDictionary<string, TypeRole> byType)
        {
            foreach (ParameterRecord parameter in signature.Parameters)
            {
                string typeName = TypeDefinitionName.OfElement(parameter.TypeName);
                TypeRole role;
                bool host = HostSupplied.Contains(typeName, StringComparer.Ordinal)
                    || (byType.TryGetValue(typeName, out role) && role == TypeRole.Connector);
                foreach (SchemaItem input in schema.Branches
                    .SelectMany(b => b.Inputs)
                    .SelectMany(i => i.WithNested)
                    .Where(i => string.Equals(i.Name, parameter.Name, StringComparison.Ordinal))
                    .Where(i => i.Injected != host))
                {
                    throw new InvalidOperationException(
                        (host
                            ? "ホストが入れる引数の入力が受け取る形になっている: "
                            : "呼ぶ側が渡す引数の入力がホストの入れる形になっている: ")
                            + schema.Tool + "(" + input.Name + ")");
                }
            }
        }

        /// <summary>
        /// 入力に現れる引数がいずれかの呼び分けの入力に、出力に現れる引数が応答に、同じ名前で
        /// 在ることを求める。入力の項目は組と配列で入れ子になるので内側まで見る。
        /// </summary>
        private static void RequireArguments(SignatureRecord signature, ToolSchema schema)
        {
            foreach (ParameterRecord parameter in signature.Parameters)
            {
                if (parameter.Direction != ParameterDirection.Out
                    && !schema.Branches.Any(b => HasInput(b, parameter.Name)))
                {
                    throw new InvalidOperationException(
                        "引数に対応する入力が無い: " + schema.Tool + "(" + parameter.Name + ")");
                }

                if (parameter.Direction != ParameterDirection.In
                    && !Named(schema.Output.WithNested, parameter.Name))
                {
                    throw new InvalidOperationException(
                        "引数に対応する応答の項目が無い: " + schema.Tool
                            + "(" + parameter.Name + ")");
                }
            }
        }

        /// <summary>受け手の入力が、宣言型の役割から決まる形であることを求める。</summary>
        private static void RequireReceiver(
            SignatureRecord signature,
            ToolSchema schema,
            IDictionary<string, TypeRole> byType,
            IDictionary<string, AccessPath> paths)
        {
            TypeRole role;
            if (signature.IsStatic
                || signature.MemberKind == MemberKind.Constructor
                || !byType.TryGetValue(
                    TypeDefinitionName.OfElement(signature.DeclaringType), out role))
            {
                return;
            }

            // 対象の集合を持つのは、リストの中の1件を相手にする受け手だけである。所有の根そのものも、
            // 根から1つに決まる子も、どのPMXを見るかの切り替えだけで相手が決まる。
            if (role == TypeRole.OperationTarget
                && Listed(paths, signature.DeclaringType)
                && !schema.Branches.All(b => TargetSelectors.Any(n => HasDirectInput(b, n))))
            {
                throw new InvalidOperationException(
                    "操作対象型の受け手を指す入力が無い呼び分けがある: " + schema.Tool);
            }

            if (role == TypeRole.HandleTarget
                && !schema.Branches.All(b => HasDirectInput(b, HandleSelector)))
            {
                throw new InvalidOperationException(
                    "ハンドル操作型の受け手を指す入力が無い呼び分けがある: " + schema.Tool);
            }

            if (role == TypeRole.Connector
                && schema.Branches.Any(b => TargetSelectors.Any(n => HasDirectInput(b, n))))
            {
                throw new InvalidOperationException(
                    "コネクタ型なのに受け手を指す入力がある: " + schema.Tool);
            }
        }

        /// <summary>その型の受け手が、リストの中の1件として指されるか。</summary>
        private static bool Listed(IDictionary<string, AccessPath> paths, string declaringType)
        {
            AccessPath path;

            return paths.TryGetValue(TypeDefinitionName.OfElement(declaringType), out path)
                && path.Kind == AccessPathKind.Element;
        }

        /// <summary>
        /// ハンドルを発行するツールの `count` が、上限を書いていないことを求める。上限は要素数の
        /// 上限の規則が分岐ごとに導く値なので、書けば導き直しを忘れたときにずれが残る。
        /// </summary>
        private static void RequireDerivedIssuanceLimit(ToolSchema schema)
        {
            foreach (SchemaBranch branch in schema.Branches)
            {
                SchemaItem count = branch.Inputs.FirstOrDefault(
                    i => string.Equals(i.Name, CountName, StringComparison.Ordinal));
                if (count == null || count.Bounds == null || count.Bounds.Maximum == null)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "発行する数の上限は導く値なので書かない: " + schema.Tool);
            }
        }

        /// <summary>
        /// 頼まれた数だけ発行できる行のツールが、発行する数をどの呼び分けでも受け取り、ハンドルを
        /// 並びで返すことを求める。数を受け取らなければ、要素の数だけ呼び出しの往復が要る。並びで
        /// 返さなければ、数を渡したときに2個目から先を受け取る場所が無い。
        /// </summary>
        private static void RequireBatchedIssuance(ToolSchema schema)
        {
            foreach (SchemaBranch branch in schema.Branches)
            {
                if (!HasDirectInput(branch, CountName))
                {
                    throw new InvalidOperationException(
                        "発行する数を受け取らない呼び分けがある: " + schema.Tool);
                }
            }

            if (schema.Output == null || schema.Output.Element == null)
            {
                throw new InvalidOperationException(
                    "発行したハンドルを並びで返さない: " + schema.Tool);
            }
        }

        /// <summary>
        /// 値を返すシグネチャのツールが値を返す形を持ち、値を返さないシグネチャのツールの応答が
        /// ホストの決める応答であることを求める。後者は導く先の戻り値を持たない。
        /// </summary>
        private static void RequireOutput(SignatureRecord signature, ToolSchema schema)
        {
            if (string.Equals(signature.ValueType, VoidTypeName, StringComparison.Ordinal))
            {
                if (schema.Output.Origin == null)
                {
                    throw new InvalidOperationException(
                        "値を返さないシグネチャなのに応答が出所を書いていない: " + schema.Tool);
                }

                return;
            }

            if (string.Equals(schema.Output.Shape, NullSpelling, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "値を返すシグネチャなのに応答が値を持たない: " + schema.Tool);
            }
        }

        /// <summary>
        /// 並びを丸ごと返す行のツールが、どの呼び分けでも offset と limit を受け取り、総数と
        /// 切り出した並びと続きの位置を返すことを求める。対象ごとに返すツールでは対象ごとの応答が
        /// その形を持つ。切り出した並びの要素は行の戻り値から導く。
        /// </summary>
        private static void RequirePaged(ToolSchema schema, SignatureRecord signature)
        {
            SchemaItem answer = schema.Output;
            while (answer.Members == null
                && answer.Origin == ItemOrigin.HostOutput
                && answer.Element != null
                && answer.Element.Origin == ItemOrigin.HostOutput)
            {
                answer = answer.Element;
            }

            SchemaItem[] members = answer.Members == null
                ? new SchemaItem[0]
                : answer.Members.ToArray();
            SchemaItem items = members.FirstOrDefault(
                m => string.Equals(m.Name, PagedItemsName, StringComparison.Ordinal));
            bool taken = schema.Branches.All(b => PagedInputs.All(n => b.Inputs.Any(
                i => i.Origin == ItemOrigin.HostInput
                    && string.Equals(i.Name, n, StringComparison.Ordinal))));
            bool answered = answer.Origin == ItemOrigin.HostOutput
                && PagedOutputs.All(n => members.Any(
                    m => string.Equals(m.Name, n, StringComparison.Ordinal)))
                && items.Element != null
                && items.Element.Origin == null;
            if (!taken || !answered)
            {
                throw new InvalidOperationException(
                    "並びを丸ごと返す行のツールが位置と件数で切り出す形を持たない: " + schema.Tool);
            }

            if (!string.Equals(signature.ValueType, NumbersType, StringComparison.Ordinal))
            {
                return;
            }

            bool joins = schema.Branches.All(b => b.Inputs.Any(
                i => i.Origin == ItemOrigin.HostInput
                    && string.Equals(i.Name, RunsName, StringComparison.Ordinal)));
            SchemaItem runs = members.FirstOrDefault(
                m => string.Equals(m.Name, ItemRunsName, StringComparison.Ordinal));
            if (!joins || runs == null || runs.Element == null || runs.Element.Members == null)
            {
                throw new InvalidOperationException(
                    "番号の並びを返す行のツールが、連なった区間へまとめて返す形を持たない: " + schema.Tool);
            }
        }

        /// <summary>入れ子の内側まで含めて、その名前の入力を持つか。</summary>
        private static bool HasInput(SchemaBranch branch, string name)
        {
            return Named(branch.Inputs.SelectMany(i => i.WithNested), name);
        }

        /// <summary>呼び分けの直下に、その名前でホストが決める入力を持つか。</summary>
        private static bool HasDirectInput(SchemaBranch branch, string name)
        {
            return Named(
                branch.Inputs.Where(i => i.Origin == ItemOrigin.HostInput), name);
        }

        private static bool Named(IEnumerable<SchemaItem> items, string name)
        {
            return items.Any(i => string.Equals(i.Name, name, StringComparison.Ordinal));
        }
    }
}
