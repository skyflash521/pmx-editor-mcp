using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表とスキーマ正本が、写像の規則に合うことを確かめる。埋め込み先も呼び分けの
    /// 見分けも機械で決まるので、書き手が別のものを書けばここで落ちる。
    /// </summary>
    public static class ToolMappingGate
    {
        /// <summary>画像として写す値の綴り。共通契約の正本が定める。</summary>
        private const string ImageShape = "image";

        /// <summary>値の組を受け取る入力の名前。</summary>
        private const string ValueName = "value";

        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolMap map,
            TypeRoleTable roles,
            IDictionary<string, SignatureRecord> signatures,
            ToolSchemaTable schemas,
            IDictionary<string, string> toolNames,
            IDictionary<string, ComposedTool> composedTools,
            IDictionary<string, IList<string>> concrete,
            IDictionary<string, string> viewImages,
            IDictionary<string, string> shapesByType,
            IDictionary<string, ISet<string>> unkeptMembers,
            IDictionary<string, ISet<string>> targetedMembers)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (toolNames == null)
            {
                throw new ArgumentNullException(nameof(toolNames));
            }

            if (composedTools == null)
            {
                throw new ArgumentNullException(nameof(composedTools));
            }

            if (concrete == null)
            {
                throw new ArgumentNullException(nameof(concrete));
            }

            if (viewImages == null)
            {
                throw new ArgumentNullException(nameof(viewImages));
            }

            if (shapesByType == null)
            {
                throw new ArgumentNullException(nameof(shapesByType));
            }

            if (unkeptMembers == null)
            {
                throw new ArgumentNullException(nameof(unkeptMembers));
            }

            if (targetedMembers == null)
            {
                throw new ArgumentNullException(nameof(targetedMembers));
            }

            RequireViewImages(map, signatures, toolNames, viewImages, shapesByType);
            RequireNamedMembers(schemas, unkeptMembers, "持ち続けない項目");
            RequireNamedMembers(schemas, targetedMembers, "指す先を埋める項目");

            IDictionary<string, TypeRoleRecord> byType = roles.Types.ToDictionary(
                t => TypeDefinitionName.OfElement(t.TypeName), t => t, StringComparer.Ordinal);

            HashSet<string> derived = new HashSet<string>(
                Aggregations(map, signatures, byType, concrete), StringComparer.Ordinal);
            derived.UnionWith(ElementToolRule.Names(map, signatures, roles));
            derived.UnionWith(ElementToolRule.HoldingNames(
                map,
                signatures,
                roles,
                HandleIssuanceEvidence.Made(map, signatures, concrete),
                ElementPathEvidence.Issued(signatures, concrete, roles)));

            RequireNoComposedName(toolNames, composedTools);
            RequireSameTools(schemas, map, toolNames, composedTools, derived);
            RequireTellableBranches(schemas);

            foreach (ToolMapRow row in map.Rows
                .Where(r => r.EmbeddedIn != null)
                .OrderBy(r => r.SignatureKey, StringComparer.Ordinal))
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature))
                {
                    throw new InvalidOperationException(
                        "行キーのシグネチャが公開APIの列挙に無い: " + row.SignatureKey);
                }

                foreach (string embedded in row.EmbeddedIn)
                {
                    RequireEmbedded(embedded, signature, byType, map, toolNames, concrete);
                }
            }
        }

        /// <summary>
        /// 名指しされた項目が、そのツールが書き換える項目に実在することを確かめる。名指しが
        /// 実在しなくなると、その名指しに掛かっている検査だけが黙って減る——持ち続けない項目
        /// では読み返して確かめる検査が、指す先を埋める項目では加える前に埋める段が消える。
        /// </summary>
        private static void RequireNamedMembers(
            ToolSchemaTable schemas,
            IDictionary<string, ISet<string>> named,
            string about)
        {
            IDictionary<string, ToolSchema> byTool = schemas.Tools.ToDictionary(
                t => t.Tool, t => t, StringComparer.Ordinal);
            foreach (KeyValuePair<string, ISet<string>> one in named
                .OrderBy(u => u.Key, StringComparer.Ordinal))
            {
                ToolSchema schema;
                if (!byTool.TryGetValue(one.Key, out schema))
                {
                    throw new InvalidOperationException(
                        about + "の名指しが、スキーマ正本に無いツールを指している: "
                            + one.Key);
                }

                ISet<string> written = new HashSet<string>(
                    schema.Branches
                        .SelectMany(b => b.Inputs)
                        .Where(i => string.Equals(i.Name, ValueName, StringComparison.Ordinal)
                            && i.Members != null)
                        .SelectMany(i => i.Members)
                        .Select(m => m.Name),
                    StringComparer.Ordinal);
                string missing = one.Value
                    .Where(m => !written.Contains(m))
                    .OrderBy(m => m, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (missing != null)
                {
                    throw new InvalidOperationException(
                        about + "の名指しが、そのツールが書き換えない項目を指している: "
                            + one.Key + "." + missing);
                }
            }
        }

        /// <summary>
        /// 画像を返す行のツールと、ビューの名前を引く表が一対一で対応することを確かめる。対応が
        /// 欠けると、その画像がどのビューのものかを確かめる検査だけが黙って減る。
        /// </summary>
        private static void RequireViewImages(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, string> toolNames,
            IDictionary<string, string> viewImages,
            IDictionary<string, string> shapesByType)
        {
            HashSet<string> drawing = new HashSet<string>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows)
            {
                SignatureRecord signature;
                string shape;
                string tool;
                if (signatures.TryGetValue(row.SignatureKey, out signature)
                    && shapesByType.TryGetValue(
                        TypeDefinitionName.OfElement(
                            ValueTypeName.Contained(signature.ValueType)), out shape)
                    && string.Equals(shape, ImageShape, StringComparison.Ordinal)
                    && toolNames.TryGetValue(row.SignatureKey, out tool))
                {
                    drawing.Add(tool);
                }
            }

            string[] unnamed = drawing.Where(t => !viewImages.ContainsKey(t))
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToArray();
            if (unnamed.Length != 0)
            {
                throw new InvalidOperationException(
                    "画像を返すのにビューを名指しされていないツールがある: "
                        + string.Join("・", unnamed));
            }

            string[] extra = viewImages.Keys.Where(t => !drawing.Contains(t))
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToArray();
            if (extra.Length != 0)
            {
                throw new InvalidOperationException(
                    "画像を返さないツールがビューを名指しされている: " + string.Join("・", extra));
            }
        }

        /// <summary>
        /// 導いた名前が合成ツールの名前にならないことを求める。合成ツールは行を持たないので、
        /// 同じ名前になると1つのツールが行と合成ツールの表の両方から現れる。
        /// </summary>
        private static void RequireNoComposedName(
            IDictionary<string, string> toolNames, IDictionary<string, ComposedTool> composedTools)
        {
            string named = toolNames.Values.Where(composedTools.ContainsKey)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (named != null)
            {
                throw new InvalidOperationException(
                    "導いた名前が合成ツールと同じになる行がある: " + named);
            }
        }

        /// <summary>
        /// 項目を集める取得と更新のツールの名前。これらのツールは行を持たないので、埋め込み先として
        /// 名指しされたものを母集合へ入れる。
        /// </summary>
        private static ISet<string> Aggregations(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, IList<string>> concrete)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows.Where(r => r.EmbeddedIn != null))
            {
                SignatureRecord signature;
                TypeRoleRecord owner;
                string declaring = signatures.TryGetValue(row.SignatureKey, out signature)
                    ? TypeDefinitionName.OfElement(signature.DeclaringType)
                    : null;
                if (declaring == null || !byType.TryGetValue(declaring, out owner))
                {
                    continue;
                }

                ISet<string> aggregations = AggregationToolRule.Names(
                    Holders(owner, declaring, byType, concrete));
                names.UnionWith(row.EmbeddedIn.Where(aggregations.Contains));
            }

            return names;
        }

        /// <summary>
        /// その行の項目を集めうる型。宣言型そのものと、宣言型を具象として並べる抽象の型である。
        /// </summary>
        private static IEnumerable<TypeRoleRecord> Holders(
            TypeRoleRecord owner,
            string declaring,
            IDictionary<string, TypeRoleRecord> byType,
            IDictionary<string, IList<string>> concrete)
        {
            return new[] { owner }.Concat(concrete
                .Where(c => c.Value.Contains(declaring, StringComparer.Ordinal))
                .Select(c => c.Key)
                .Where(byType.ContainsKey)
                .Select(t => byType[t]));
        }

        /// <summary>
        /// 行から導いた名前と合成ツールに入出力の形が在ること、およびスキーマ正本が持つツールが
        /// そのどちらかに在ることを求める。分岐を持つ合成ツールの形は、その分岐の出どころで
        /// あるイベント行が無ければ書けないので、イベント行が在るときだけ求める。
        /// </summary>
        private static void RequireSameTools(
            ToolSchemaTable schemas,
            ToolMap map,
            IDictionary<string, string> toolNames,
            IDictionary<string, ComposedTool> composedTools,
            ISet<string> derived)
        {
            HashSet<string> assigned = new HashSet<string>(
                toolNames.Values, StringComparer.Ordinal);
            HashSet<string> described = new HashSet<string>(
                schemas.Tools.Select(t => t.Tool), StringComparer.Ordinal);
            bool hasEvents = map.Rows.Any(r => r.EventType != null);

            HashSet<string> wanted = new HashSet<string>(assigned, StringComparer.Ordinal);
            wanted.UnionWith(derived);
            wanted.UnionWith(
                composedTools.Where(t => hasEvents || !t.Value.Branching).Select(t => t.Key));
            string missing = wanted.Except(described, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("入出力の形が無いツールがある: " + missing);
            }

            HashSet<string> allowed = new HashSet<string>(assigned, StringComparer.Ordinal);
            allowed.UnionWith(derived);
            allowed.UnionWith(composedTools.Keys);
            string extra = described.Except(allowed, StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException(
                    "どの行の名前にもならないツールの形がある: " + extra);
            }
        }

        /// <summary>
        /// 同じツールへ集めた呼び分けが、相互に見分けられることを求める。見分けられない呼び分けを
        /// 残すと、どちらの呼び出しなのかがホストの側で決まらない。
        /// </summary>
        private static void RequireTellableBranches(ToolSchemaTable schemas)
        {
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                for (int first = 0; first < schema.Branches.Count; first++)
                {
                    for (int second = first + 1; second < schema.Branches.Count; second++)
                    {
                        if (Tellable(schema.Branches[first], schema.Branches[second]))
                        {
                            continue;
                        }

                        throw new InvalidOperationException(
                            "入力で判別できない呼び分けがある: " + schema.Tool
                                + "(" + schema.Branches[first].Branch + " と "
                                + schema.Branches[second].Branch + ")");
                    }
                }
            }
        }

        /// <summary>
        /// 2つの呼び分けを見分けられるか。見分けられるのは、分岐を選ぶ項目が同じ名前で違う値を選ぶ
        /// とき、片方が必ず渡す名前をもう片方がどの入力にも持たないとき、必ず1つを渡すまとまりが
        /// 共通の名前を持たないときのいずれかで、入れ子の組の中にも同じ規則を当てる。
        /// </summary>
        private static bool Tellable(SchemaBranch first, SchemaBranch second)
        {
            if (first.SelectorName != null
                && string.Equals(first.SelectorName, second.SelectorName, StringComparison.Ordinal)
                && !string.Equals(Written(first.SelectorValue), Written(second.SelectorValue),
                    StringComparison.Ordinal))
            {
                return true;
            }

            return Missing(first.Inputs, second.Inputs)
                || Missing(second.Inputs, first.Inputs)
                || Apart(first, second)
                || Inside(first.Inputs, second.Inputs);
        }

        /// <summary>必ず渡す名前のうち、相手がどの入力にも持たないものがあるか。</summary>
        private static bool Missing(IEnumerable<SchemaItem> from, IEnumerable<SchemaItem> other)
        {
            return from.Where(i => i.Required.HasValue && i.Required.Value)
                .Any(i => !other.Any(
                    o => string.Equals(o.Name, i.Name, StringComparison.Ordinal)));
        }

        /// <summary>必ず1つを渡すまとまりで、共通の名前を持たない組があるか。</summary>
        private static bool Apart(SchemaBranch first, SchemaBranch second)
        {
            return first.Choices.Where(c => c.Required).Any(
                one => second.Choices.Where(c => c.Required).Any(
                    other => !one.Names.Intersect(other.Names, StringComparer.Ordinal).Any()));
        }

        /// <summary>両方が持つ同じ名前の組の中で、必ず渡す名前が食い違うか。</summary>
        private static bool Inside(IList<SchemaItem> first, IList<SchemaItem> second)
        {
            foreach (SchemaItem item in first.Where(i => Grouped(i) != null))
            {
                SchemaItem twin = second.FirstOrDefault(
                    o => string.Equals(o.Name, item.Name, StringComparison.Ordinal)
                        && Grouped(o) != null);
                if (twin == null)
                {
                    continue;
                }

                if (Missing(Grouped(item), Grouped(twin)) || Missing(Grouped(twin), Grouped(item)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// その項目が持つ組の中身。組と、空にできない組の配列が持つ。空にできる配列は、空の要求が
        /// どちらの呼び分けにも当てはまるので見分けに使えない。
        /// </summary>
        private static IList<SchemaItem> Grouped(SchemaItem item)
        {
            if (item.Members != null)
            {
                return item.Members;
            }

            return NonEmptyArrayRule.NonEmpty(item) ? item.Element.Members : null;
        }

        /// <summary>分岐を選ぶ値を、JSONの形と型を保った文字列にしたもの。</summary>
        private static string Written(object value)
        {
            if (value == null)
            {
                return "null";
            }

            IDictionary<string, object> members = value as IDictionary<string, object>;
            if (members != null)
            {
                return "{" + string.Join(
                    ",",
                    members.OrderBy(m => m.Key, StringComparer.Ordinal)
                        .Select(m => Written(m.Key) + ":" + Written(m.Value))
                        .ToArray()) + "}";
            }

            IEnumerable<object> items = value as IEnumerable<object>;
            if (items != null)
            {
                return "[" + string.Join(",", items.Select(Written).ToArray()) + "]";
            }

            return value.GetType().Name + ":"
                + Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 埋め込み先が、宣言型の役割に応じた先であることを求める。埋め込み先は名前でしか指せない
        /// ので、綴りの取り違えはここでしか出ない。
        /// </summary>
        private static void RequireEmbedded(
            string embedded,
            SignatureRecord signature,
            IDictionary<string, TypeRoleRecord> byType,
            ToolMap map,
            IDictionary<string, string> toolNames,
            IDictionary<string, IList<string>> concrete)
        {
            string declaring = TypeDefinitionName.OfElement(signature.DeclaringType);
            TypeRoleRecord owner;
            if (!byType.TryGetValue(declaring, out owner))
            {
                throw new InvalidOperationException(
                    "ツールの名前を導く型が型役割表に無い: " + signature.DeclaringType);
            }

            bool branch = map.Rows.Any(
                r => string.Equals(r.EventType, embedded, StringComparison.Ordinal));
            if (owner.Role == TypeRole.EventArgs)
            {
                if (!branch)
                {
                    throw new InvalidOperationException(
                        "イベント引数型の埋め込み先がイベントの分岐に無い: " + embedded);
                }

                return;
            }

            if (owner.Role == TypeRole.Dto)
            {
                if (!branch
                    && !toolNames.Values.Contains(embedded, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        "DTO型の埋め込み先が表のツールにもイベントの分岐にも無い: " + embedded);
                }

                return;
            }

            // 抽象の型を並べるリストでは、具象の型の項目はそのリストのツールへ集まる。
            if (!Holders(owner, declaring, byType, concrete).Any(h => AggregationToolRule.Of(h).Contains(embedded, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "埋め込み先が宣言型の取得と更新のツールにも、"
                        + "その型を具象として並べるリストのツールにも無い: " + embedded);
            }
        }
    }
}
