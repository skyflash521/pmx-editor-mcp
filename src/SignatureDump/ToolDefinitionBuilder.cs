using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>MCPクライアントへ載せるツール定義の1件。</summary>
    public sealed class ToolDefinition
    {
        public ToolDefinition(
            string name, string description, string inputSchema, bool returnsImage)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            ReturnsImage = returnsImage;
        }

        public string Name { get; }

        public string Description { get; }

        /// <summary>入力の形をJSON Schemaで綴ったもの。</summary>
        public string InputSchema { get; }

        /// <summary>
        /// 値が画像かどうか。ブリッジはこれを見て、結果を文字列でなく画像の本文として返す——画像を
        /// 文字列で返すと、MCPクライアントは中身を見られない。
        /// </summary>
        public bool ReturnsImage { get; }
    }

    /// <summary>
    /// スキーマ正本と説明文から、MCPクライアントへ載せるツール定義を組み立てる。一覧の件数の既定と
    /// 要素数の上限は正本に書かず、[逆算の規則](ListingLimitRule)と[要素数の規則](ElementLimitRule)が
    /// 導いた値をここで入れる——予算を変えれば動く値なので、書き写せば必ず食い違う。
    /// </summary>
    public static class ToolDefinitionBuilder
    {
        /// <summary>一覧が何件返すかを受け取る入力の名前。</summary>
        public const string LimitName = "limit";

        /// <summary>一覧が切り出す前の総数を返す項目の名前。</summary>
        public const string TotalName = ListingLimitRule.TotalName;

        /// <summary>ハンドルをいくつ発行するかを受け取る入力の名前。</summary>
        public const string CountName = "count";

        /// <summary>危険操作の確認を受け取る共通引数の名前。</summary>
        public const string ConfirmName = "confirm";

        /// <summary>Undoの記録を止めることを頼む共通引数の名前。</summary>
        public const string SuppressName = "suppressUndo";

        /// <summary>どのPMXを見るかを切り替える共通引数の名前。</summary>
        public const string PmxHandleName = "pmxHandle";

        private const string ObjectType = "object";

        private const string ArrayType = "array";

        private const string IntegerType = "integer";

        private const string NumberType = "number";

        private const string StringType = "string";

        private const string BooleanType = "boolean";

        private const string OffsetName = "offset";

        private const string NameContainsName = "nameContains";

        private const string FieldsName = "fields";

        private const string AssignmentsName = "assignments";

        private const string ParentIndexName = "parentIndex";

        private const string ParentHandleName = "parentHandle";

        private const string StartName = "start";

        private const string Base64Spelling = "base64";

        private const string SingleMost = "3.4028234663852886E+38";

        // System.Text.Json の既定の書き出しは文字列中の加算記号を + へ置き換える。
        private const string Base64Pattern =
            "^(?:[A-Za-z0-9\\x2B/]{4})*(?:[A-Za-z0-9\\x2B/]{2}==|[A-Za-z0-9\\x2B/]{3}=)?$";

        private const string VersionPattern =
            "^(?:0|[1-9][0-9]{0,8})(?:\\.(?:0|[1-9][0-9]{0,8})){1,3}$";

        private static readonly string[] PmxHandleNames = { PmxHandleName, "basePmxHandle" };

        private static readonly string[] PositionListNames =
            { "indices", "modelIndices", "parentIndices" };

        private static readonly string[] HandleListNames = { "handles", "parentHandles" };

        private static readonly string[] RangeNames = { "range", "parentRange" };

        private static readonly string[] WholeNames = { "all", "parentAll", "selected" };

        private static readonly IDictionary<string, string> ScalarTypes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "boolean", BooleanType },
                { "number", NumberType },
                { "text", StringType },
                { "base64", StringType },
                { "enum_name", StringType },
                { "image", StringType },
                { "null_value", "null" },
            };

        /// <summary>成分の数が綴りで決まる並び。最小と最大をそのまま入れる。</summary>
        private static readonly IDictionary<string, int[]> FixedArrays =
            new Dictionary<string, int[]>(StringComparer.Ordinal)
            {
                { "color", new[] { 3, 4 } },
                { "size", new[] { 2, 2 } },
                { "point", new[] { 2, 2 } },
                { "rectangle", new[] { 4, 4 } },
                { "brush", new[] { 3, 4 } },
            };

        /// <summary>
        /// ツール定義を綴りの順に組み立てる。<paramref name="descriptions"/> はツール名から説明文へ、
        /// <paramref name="valueChars"/> は応答の値の枠、<paramref name="requestBudgetBytes"/> は
        /// 要求サイズ予算、<paramref name="tokenLimit"/> はIPCの構造トークンの上限、
        /// <paramref name="sdkShapes"/> はSDKに由来する項目の綴り、<paramref name="dangerous"/> は
        /// 確認を要するツールの名前、<paramref name="heldOnly"/> はハンドルで指したPMXだけを空にする
        /// ツールの名前。<paramref name="rules"/> を渡すと、入力をホストが受け取る値まで狭める。
        /// </summary>
        public static IList<ToolDefinition> Build(
            ToolSchemaTable schemas,
            IDictionary<string, string> descriptions,
            AssumedLength lengths,
            int valueChars,
            int requestBudgetBytes,
            int tokenLimit,
            IDictionary<SchemaItem, string> sdkShapes,
            ISet<string> dangerous,
            ISet<string> heldOnly,
            ISet<string> suppressing,
            ISet<string> drawing,
            InputRules rules = null)
        {
            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (descriptions == null)
            {
                throw new ArgumentNullException(nameof(descriptions));
            }

            if (lengths == null)
            {
                throw new ArgumentNullException(nameof(lengths));
            }

            if (sdkShapes == null)
            {
                throw new ArgumentNullException(nameof(sdkShapes));
            }

            if (dangerous == null)
            {
                throw new ArgumentNullException(nameof(dangerous));
            }

            if (heldOnly == null)
            {
                throw new ArgumentNullException(nameof(heldOnly));
            }

            if (suppressing == null)
            {
                throw new ArgumentNullException(nameof(suppressing));
            }

            if (drawing == null)
            {
                throw new ArgumentNullException(nameof(drawing));
            }

            List<ToolDefinition> definitions = new List<ToolDefinition>();
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                string description;
                if (!descriptions.TryGetValue(schema.Tool, out description))
                {
                    throw new InvalidOperationException("説明文が無いツール: " + schema.Tool);
                }

                Writing writing = new Writing(
                    schema,
                    ListingLimitRule.IsListing(schema)
                        ? ListingLimitRule.Derive(schema, lengths, valueChars)
                        : null,
                    Readable(schema),
                    sdkShapes,
                    rules);
                definitions.Add(new ToolDefinition(
                    schema.Tool,
                    description,
                    InputSchema(
                        writing,
                        lengths,
                        valueChars,
                        requestBudgetBytes,
                        tokenLimit,
                        heldOnly.Contains(schema.Tool),
                        dangerous.Contains(schema.Tool),
                        suppressing.Contains(schema.Tool)),
                    drawing.Contains(schema.Tool)));
            }

            return definitions;
        }

        private static string InputSchema(
            Writing writing,
            AssumedLength lengths,
            int valueChars,
            int requestBudgetBytes,
            int tokenLimit,
            bool heldOnly,
            bool confirms,
            bool suppresses)
        {
            List<Writing> written = writing.Schema.Branches
                .Select(b => Limited(writing.For(b), lengths, valueChars, requestBudgetBytes, tokenLimit))
                .ToList();
            Aligned(written);
            List<BranchShape> branches = written
                .Select(w => Branch(w, heldOnly, confirms && !heldOnly, suppresses))
                .ToList();

            return branches.Count == 1
                ? Compose(branches[0], new HashSet<string>(StringComparer.Ordinal))
                : Merge(branches);
        }

        /// <summary>分岐1つ分の、まとめる前の構成要素。</summary>
        private sealed class BranchShape
        {
            public BranchShape(
                List<KeyValuePair<string, string>> properties,
                List<string> required,
                List<string> rules,
                string selectorName,
                string selectorValue)
            {
                Properties = properties;
                Required = required;
                Rules = rules;
                SelectorName = selectorName;
                SelectorValue = selectorValue;
            }

            /// <summary>引数の名前と、その形を綴ったもの。</summary>
            public List<KeyValuePair<string, string>> Properties { get; }

            public List<string> Required { get; }

            /// <summary>どれか1つだけ渡す、といった引数どうしの決まり。</summary>
            public List<string> Rules { get; }

            public string SelectorName { get; }

            public string SelectorValue { get; }
        }

        private sealed class Writing
        {
            public Writing(
                ToolSchema schema,
                ListingLimits listing,
                IList<string> readable,
                IDictionary<SchemaItem, string> sdkShapes,
                InputRules rules)
            {
                Schema = schema;
                Listing = listing;
                Readable = readable;
                SdkShapes = sdkShapes;
                Rules = rules;
            }

            public ToolSchema Schema { get; }

            public ListingLimits Listing { get; }

            public IList<string> Readable { get; }

            public IDictionary<SchemaItem, string> SdkShapes { get; }

            public InputRules Rules { get; }

            public SchemaBranch Branch { get; private set; }

            public IDictionary<SchemaItem, int> Limits { get; set; }

            public int? Issued { get; set; }

            public int? Issuable { get; set; }

            public IDictionary<SchemaItem, string> ItemPaths { get; set; }

            public bool Dispatched
            {
                get { return Rules != null && Rules.Dispatched.Contains(Schema.Tool); }
            }

            public Writing For(SchemaBranch branch)
            {
                Writing one = (Writing)MemberwiseClone();
                one.Branch = branch;

                return one;
            }
        }

        private sealed class Place
        {
            private Place(bool top, string container, bool inArray)
            {
                Top = top;
                Container = container;
                InArray = inArray;
            }

            public static readonly Place Input = new Place(true, null, false);

            public bool Top { get; }

            public string Container { get; }

            public bool InArray { get; }

            public bool UnderTop { get; private set; }

            public Place MemberOf(string container)
            {
                return new Place(false, container, false) { UnderTop = Top };
            }

            public Place ElementOf(string container)
            {
                return new Place(false, container, true) { UnderTop = Top };
            }
        }

        private static Writing Limited(
            Writing writing,
            AssumedLength lengths,
            int valueChars,
            int requestBudgetBytes,
            int tokenLimit)
        {
            writing.ItemPaths = Paths(writing.Branch);
            IDictionary<SchemaItem, int> limits = ElementLimitRule.Request(
                writing.Branch, lengths, requestBudgetBytes, tokenLimit, item => Declared(writing, item));

            // 応答が並びを返すなら、要求で受ける件数も応答で返せる件数より多くできない。
            SchemaItem returned = writing.Schema.Output == null ? null : writing.Schema.Output.Element;
            writing.Issued = returned == null
                ? (int?)null
                : ElementLimitRule.Response(returned, lengths, valueChars);
            if (writing.Issued.HasValue)
            {
                foreach (SchemaItem array in limits.Keys.ToList())
                {
                    limits[array] = ElementLimitRule.Bounded(limits[array], writing.Issued.Value);
                }
            }

            writing.Limits = limits;

            return writing;
        }

        private static int? Declared(Writing writing, SchemaItem item)
        {
            HostListLength length;
            if (!TryComposed(writing, item, writing.Rules?.ComposedReads.Lists, out length))
            {
                TryComposed(writing, item, writing.Rules?.ComposedTexts.Lists, out length);
            }

            return length == null ? null : length.Most;
        }

        private static void Aligned(IList<Writing> branches)
        {
            Dictionary<string, int> least = new Dictionary<string, int>(StringComparer.Ordinal);
            List<IDictionary<SchemaItem, string>> paths = new List<IDictionary<SchemaItem, string>>();
            foreach (Writing branch in branches)
            {
                IDictionary<SchemaItem, string> path = Paths(branch.Branch);
                paths.Add(path);
                foreach (KeyValuePair<SchemaItem, int> limit in branch.Limits)
                {
                    int found;
                    string at = path[limit.Key];
                    least[at] = least.TryGetValue(at, out found) ? Math.Min(found, limit.Value) : limit.Value;
                }
            }

            for (int at = 0; at < branches.Count; at++)
            {
                Writing branch = branches[at];
                IDictionary<SchemaItem, string> path = paths[at];
                branch.Limits = branch.Limits.Keys.ToDictionary(k => k, k => least[path[k]]);
                branch.Issuable = branch.Issued.HasValue
                    ? ElementLimitRule.Issued(branch.Limits.Values, branch.Issued.Value)
                    : (int?)null;
            }
        }

        private static IDictionary<SchemaItem, string> Paths(SchemaBranch branch)
        {
            Dictionary<SchemaItem, string> paths = new Dictionary<SchemaItem, string>();
            foreach (SchemaItem input in branch.Inputs)
            {
                Walk(input, input.Name, paths);
            }

            return paths;
        }

        private static void Walk(SchemaItem item, string path, IDictionary<SchemaItem, string> paths)
        {
            paths[item] = path;
            foreach (SchemaItem member in item.Members ?? new SchemaItem[0])
            {
                Walk(member, path + "." + member.Name, paths);
            }

            if (item.Element != null)
            {
                Walk(item.Element, path + "[]", paths);
            }
        }

        private static BranchShape Branch(
            Writing writing,
            bool heldOnly,
            bool confirms,
            bool suppresses)
        {
            SchemaBranch branch = writing.Branch;
            List<KeyValuePair<string, string>> properties =
                new List<KeyValuePair<string, string>>();
            List<string> required = new List<string>();
            List<string> rules = new List<string>();

            // ホストが自分で入れる引数は、呼び出す側へ現れない。
            foreach (SchemaItem input in branch.Inputs.Where(i => !i.Injected))
            {
                properties.Add(new KeyValuePair<string, string>(
                    input.Name, Item(writing, input, Place.Input)));
                if ((input.Required.HasValue && input.Required.Value)
                    || (heldOnly && string.Equals(input.Name, PmxHandleName, StringComparison.Ordinal)))
                {
                    required.Add(input.Name);
                }
            }

            // 確認の共通引数は、どのシグネチャが危険操作に当たるかの決め方が導くので正本に書かない。
            // 要らないツールには現れない。
            if (confirms)
            {
                properties.Add(new KeyValuePair<string, string>(
                    ConfirmName, new JsonObjectText().AddText("type", BooleanType).Text));
                required.Add(ConfirmName);
            }

            // 抑止の共通引数も、どの行が複製編集型かの決め方が導くので正本に書かない。まとめて
            // 反映する呼び出しにだけ現れ、渡さなければ止めない。
            if (suppresses)
            {
                properties.Add(new KeyValuePair<string, string>(
                    SuppressName, new JsonObjectText().AddText("type", BooleanType).Text));
            }

            if (properties.Any(p => string.Equals(p.Key, SuppressName, StringComparison.Ordinal))
                && properties.Any(p => string.Equals(p.Key, PmxHandleName, StringComparison.Ordinal)))
            {
                rules.Add(SuppressingOnlyTheOpenPmx());
            }

            bool nameNarrowingStandsIn = TakesNameContains(branch);
            rules.InsertRange(
                0,
                (branch.Choices ?? new SchemaChoice[0]).Select(c => Choice(c, nameNarrowingStandsIn)));

            return new BranchShape(
                properties,
                required,
                rules,
                branch.SelectorName,
                branch.SelectorName == null ? null : Value(branch.SelectorValue));
        }

        /// <summary>分岐1つ分を、MCPのツール定義が読む形へ綴る。</summary>
        private static string Compose(BranchShape shape, ISet<string> shared)
        {
            JsonObjectText properties = new JsonObjectText();
            foreach (KeyValuePair<string, string> property in shape.Properties)
            {
                properties.Add(property.Key, shared.Contains(property.Key) ? "{}" : property.Value);
            }

            JsonObjectText body = new JsonObjectText().AddText("type", ObjectType);
            body.Add("properties", properties.Text);
            if (shape.Required.Count > 0)
            {
                body.Add("required", JsonWriter.TextArray(shape.Required));
            }

            body.AddBoolean("additionalProperties", false);

            if (shape.Rules.Count > 0)
            {
                body.Add("allOf", JsonWriter.Array(shape.Rules));
            }

            return body.Text;
        }

        /// <summary>いくつもの分岐を1つの組へまとめる。</summary>
        private static string Merge(List<BranchShape> branches)
        {
            List<string> names = new List<string>();
            IDictionary<string, List<string>> forms =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (BranchShape branch in branches)
            {
                foreach (KeyValuePair<string, string> property in branch.Properties)
                {
                    List<string> found;
                    if (!forms.TryGetValue(property.Key, out found))
                    {
                        found = new List<string>();
                        forms.Add(property.Key, found);
                        names.Add(property.Key);
                    }

                    if (!found.Contains(property.Value, StringComparer.Ordinal))
                    {
                        found.Add(property.Value);
                    }
                }
            }

            JsonObjectText properties = new JsonObjectText();
            foreach (string name in names)
            {
                List<string> found = forms[name];
                properties.Add(
                    name,
                    found.Count == 1
                        ? found[0]
                        : new JsonObjectText().Add("anyOf", JsonWriter.Array(found)).Text);
            }

            HashSet<string> shared = new HashSet<string>(
                names.Where(n => forms[n].Count == 1), StringComparer.Ordinal);
            List<string> required = branches[0].Required
                .Where(n => branches.All(b => b.Required.Contains(n, StringComparer.Ordinal)))
                .ToList();

            JsonObjectText body = new JsonObjectText().AddText("type", ObjectType);
            body.Add("properties", properties.Text);
            if (required.Count > 0)
            {
                body.Add("required", JsonWriter.TextArray(required));
            }

            body.AddBoolean("additionalProperties", false);
            string selector = branches[0].SelectorName;
            if (selector == null
                || !required.Contains(selector, StringComparer.Ordinal)
                || branches.Any(b => !string.Equals(b.SelectorName, selector, StringComparison.Ordinal)))
            {
                body.Add("anyOf", JsonWriter.Array(branches.Select(b => Compose(b, shared))));

                return body.Text;
            }

            List<string> chosen = new List<string>();
            foreach (IGrouping<string, BranchShape> same in branches.GroupBy(
                b => b.SelectorValue, StringComparer.Ordinal))
            {
                JsonObjectText when = new JsonObjectText()
                    .Add("required", JsonWriter.TextArray(new[] { selector }))
                    .Add(
                        "properties",
                        new JsonObjectText()
                            .Add(selector, new JsonObjectText().Add("const", same.Key).Text)
                            .Text);
                JsonObjectText alike = new JsonObjectText();
                HashSet<string> held = new HashSet<string>(shared, StringComparer.Ordinal);
                foreach (string name in names.Where(n => !shared.Contains(n)))
                {
                    List<string> within = same
                        .SelectMany(b => b.Properties.Where(p => string.Equals(p.Key, name, StringComparison.Ordinal)))
                        .Select(p => p.Value)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (within.Count == 1)
                    {
                        alike.Add(name, within[0]);
                        held.Add(name);
                    }
                }

                JsonObjectText then = new JsonObjectText();
                if (!alike.IsEmpty)
                {
                    then.Add("properties", alike.Text);
                }

                then.Add("anyOf", JsonWriter.Array(same.Select(b => Compose(b, held))));
                chosen.Add(new JsonObjectText().Add("if", when.Text).Add("then", then.Text).Text);
            }

            body.Add("allOf", JsonWriter.Array(chosen));

            return body.Text;
        }

        private static string SuppressingOnlyTheOpenPmx()
        {
            JsonObjectText both = new JsonObjectText()
                .Add("required", JsonWriter.TextArray(new[] { SuppressName, PmxHandleName }))
                .Add(
                    "properties",
                    new JsonObjectText()
                        .Add(SuppressName, new JsonObjectText().Add("const", "true").Text)
                        .Text);

            return new JsonObjectText().Add("not", both.Text).Text;
        }

        private static string Choice(SchemaChoice choice, bool nameNarrowingStandsIn)
        {
            List<string> cases = choice.Names
                .Select(n => new JsonObjectText().Add("required", JsonWriter.TextArray(new[] { n })).Text)
                .ToList();

            if (choice.Required && nameNarrowingStandsIn)
            {
                string pointing = new JsonObjectText().Add("anyOf", JsonWriter.Array(cases)).Text;
                cases.Add(new JsonObjectText()
                    .Add("required", JsonWriter.TextArray(new[] { NameContainsName }))
                    .Add("not", pointing)
                    .Text);
            }

            if (!choice.Required)
            {
                cases.Add(new JsonObjectText()
                    .Add("not", new JsonObjectText().Add("anyOf", JsonWriter.Array(cases)).Text)
                    .Text);
            }

            return new JsonObjectText().Add("oneOf", JsonWriter.Array(cases)).Text;
        }

        private static bool TakesNameContains(SchemaBranch branch)
        {
            return branch.Inputs.Any(
                i => !i.Injected && string.Equals(i.Name, NameContainsName, StringComparison.Ordinal));
        }

        private static string Item(
            Writing writing, SchemaItem item, Place place, bool distributed = false)
        {
            JsonObjectText written = Shape(
                writing,
                item,
                place,
                distributed || string.Equals(
                    item.Name, ElementLimitRule.DistributedName, StringComparison.Ordinal));

            if (item.HasDefault)
            {
                written.Add("default", Value(item.Default));
            }

            // 値で分かれる呼び分けは、その項目の値そのものが分岐を選ぶ。名前が同じだけの入れ子の
            // 項目まで縛らないよう、分岐が直に受け取る入力に限る。
            SchemaBranch branch = writing.Branch;
            if (branch.SelectorName != null
                && place.Top
                && branch.Inputs.Contains(item)
                && string.Equals(branch.SelectorName, item.Name, StringComparison.Ordinal))
            {
                written.Add("const", Value(branch.SelectorValue));
            }

            return written.Text;
        }

        private static JsonObjectText Shape(
            Writing writing, SchemaItem item, Place place, bool distributed)
        {
            if (place.Top
                && string.Equals(item.Name, AssignmentsName, StringComparison.Ordinal)
                && item.Element != null
                && item.Element.Members != null
                && item.Element.Members.Any(m => string.Equals(m.Name, ParentIndexName, StringComparison.Ordinal))
                && item.Element.Members.Any(m => string.Equals(m.Name, ParentHandleName, StringComparison.Ordinal)))
            {
                return Assignments(writing, item, place);
            }

            if (item.Members != null)
            {
                return Group(writing, item, place, item.Members, distributed);
            }

            if (item.Element != null)
            {
                return Listed(writing, item, place, Item(writing, item.Element, place.ElementOf(item.Name), distributed), distributed);
            }

            string sdkType;
            if (item.Origin == null
                && writing.Rules != null
                && writing.Rules.SdkTypes.TryGetValue(item, out sdkType))
            {
                JsonObjectText sdk = SdkValue(writing, item, sdkType);
                if (sdk != null)
                {
                    return sdk;
                }
            }

            string spelling = item.Shape;
            if (spelling == null && !writing.SdkShapes.TryGetValue(item, out spelling))
            {
                throw new InvalidOperationException(
                    "形を持たない項目: " + writing.Schema.Tool + "." + item.Name);
            }

            return Scalar(writing, item, place, spelling);
        }

        private static JsonObjectText Group(
            Writing writing,
            SchemaItem item,
            Place place,
            IList<SchemaItem> members,
            bool distributed)
        {
            JsonObjectText written = new JsonObjectText();
            List<string> required = new List<string>();
            foreach (SchemaItem member in members)
            {
                written.Add(
                    member.Name,
                    Item(writing, member, place.MemberOf(item.Name), distributed));
                if ((member.Required.HasValue && member.Required.Value)
                    || (item.Origin == null && writing.Rules != null))
                {
                    required.Add(member.Name);
                }
            }

            JsonObjectText body = new JsonObjectText()
                .Add("type", TypeOf(ObjectType, item.Nullable));
            body.Add("properties", written.Text);
            if (required.Count > 0)
            {
                body.Add("required", JsonWriter.TextArray(required));
            }

            return body.AddBoolean("additionalProperties", false);
        }

        private static JsonObjectText Listed(
            Writing writing, SchemaItem item, Place place, string element, bool distributed)
        {
            JsonObjectText body = new JsonObjectText()
                .Add("type", TypeOf(ArrayType, item.Nullable));
            body.Add("items", element);
            bool fields = place.Top
                && string.Equals(item.Name, FieldsName, StringComparison.Ordinal)
                && writing.Readable.Count > 0;
            HostListLength length;
            if (!TryComposed(writing, item, writing.Rules?.ComposedReads.Lists, out length))
            {
                TryComposed(writing, item, writing.Rules?.ComposedTexts.Lists, out length);
            }
            int least = NonEmptyArrayRule.NonEmpty(item) || fields ? 1 : 0;
            if (length != null && length.Least.HasValue)
            {
                least = Math.Max(least, length.Least.Value);
            }

            if (least > 0)
            {
                body.AddNumber("minItems", least);
            }

            if (item.UniqueItems
                || (place.Top
                    && (fields
                        || PositionListNames.Contains(item.Name, StringComparer.Ordinal)
                        || string.Equals(item.Name, AssignmentsName, StringComparison.Ordinal)))
                || HandleListNames.Contains(item.Name, StringComparer.Ordinal))
            {
                body.AddBoolean("uniqueItems", true);
            }

            int maxItems;
            if (item.MaxItems.HasValue)
            {
                maxItems = item.MaxItems.Value;
            }
            else if (distributed)
            {
                return length != null && length.Most.HasValue
                    ? body.AddNumber("maxItems", length.Most.Value)
                    : body;
            }
            else if (!writing.Limits.TryGetValue(item, out maxItems))
            {
                throw new InvalidOperationException(
                    "要素数の上限を導けない並び: " + writing.Schema.Tool + "." + item.Name);
            }

            if (length != null && length.Most.HasValue)
            {
                maxItems = Math.Min(maxItems, length.Most.Value);
            }

            return body.AddNumber("maxItems", maxItems);
        }

        private static JsonObjectText Assignments(Writing writing, SchemaItem item, Place place)
        {
            string[] ways = { ParentIndexName, ParentHandleName };
            List<string> alternatives = new List<string>();
            foreach (string way in ways)
            {
                string other = ways.Single(w => !string.Equals(w, way, StringComparison.Ordinal));
                SchemaItem element = item.Element;
                JsonObjectText members = new JsonObjectText();
                List<string> required = new List<string>();
                foreach (SchemaItem member in element.Members
                    .Where(m => !string.Equals(m.Name, other, StringComparison.Ordinal)))
                {
                    members.Add(
                        member.Name,
                        Item(writing, member, place.ElementOf(item.Name).MemberOf(item.Name)));
                    if ((member.Required.HasValue && member.Required.Value)
                        || string.Equals(member.Name, way, StringComparison.Ordinal))
                    {
                        required.Add(member.Name);
                    }
                }

                JsonObjectText one = new JsonObjectText().AddText("type", ObjectType);
                one.Add("properties", members.Text);
                one.Add("required", JsonWriter.TextArray(required));
                one.AddBoolean("additionalProperties", false);
                alternatives.Add(Listed(writing, item, place, one.Text, false).Text);
            }

            return new JsonObjectText().Add("anyOf", JsonWriter.Array(alternatives));
        }

        private static JsonObjectText Scalar(
            Writing writing, SchemaItem item, Place place, string spelling)
        {
            int[] fixedArray;
            if (FixedArrays.TryGetValue(spelling, out fixedArray))
            {
                return new JsonObjectText()
                    .Add("type", TypeOf(ArrayType, item.Nullable))
                    .Add("items", new JsonObjectText().AddText("type", NumberType).Text)
                    .AddNumber("minItems", fixedArray[0])
                    .AddNumber("maxItems", fixedArray[1]);
            }

            if (string.Equals(spelling, "number_array", StringComparison.Ordinal))
            {
                return new JsonObjectText()
                    .Add("type", TypeOf(ArrayType, item.Nullable))
                    .Add("items", new JsonObjectText().AddText("type", NumberType).Text);
            }

            if (string.Equals(spelling, "font", StringComparison.Ordinal))
            {
                return Font(item, null);
            }

            if (string.Equals(spelling, "json", StringComparison.Ordinal))
            {
                return new JsonObjectText();
            }

            string type;
            if (!ScalarTypes.TryGetValue(spelling, out type))
            {
                throw new InvalidOperationException(
                    "組み立て方を持たない綴り: " + spelling + "(" + writing.Schema.Tool + ")");
            }

            if (string.Equals(type, NumberType, StringComparison.Ordinal))
            {
                return Numeric(item, HostNumber(writing, item, place));
            }

            JsonObjectText written = new JsonObjectText().Add("type", TypeOf(type, item.Nullable));
            HostTextRead text;
            if (string.Equals(type, StringType, StringComparison.Ordinal)
                && TryComposed(writing, item, writing.Rules?.ComposedTexts.Inputs, out text))
            {
                if (text.Choices != null)
                {
                    List<string> choices = text.Choices.Select(JsonText.Quote).ToList();
                    if (item.Nullable.HasValue && item.Nullable.Value)
                    {
                        choices.Add("null");
                    }

                    return written.Add("enum", JsonWriter.Array(choices));
                }

                if (text.MinLength.HasValue)
                {
                    return written.AddNumber("minLength", text.MinLength.Value);
                }
            }

            if (place.InArray
                && place.UnderTop
                && string.Equals(place.Container, FieldsName, StringComparison.Ordinal)
                && string.Equals(type, StringType, StringComparison.Ordinal)
                && writing.Readable.Count > 0)
            {
                return written.Add("enum", JsonWriter.TextArray(writing.Readable));
            }

            if (place.InArray
                && place.UnderTop
                && string.Equals(place.Container, NameContainsName, StringComparison.Ordinal)
                && string.Equals(type, StringType, StringComparison.Ordinal))
            {
                return written.AddNumber("minLength", 1);
            }

            if (!place.Top)
            {
                return written;
            }

            if (string.Equals(type, BooleanType, StringComparison.Ordinal)
                && WholeNames.Contains(item.Name, StringComparer.Ordinal))
            {
                return written.Add("const", "true");
            }

            return written;
        }

        private static NumberRange HostNumber(Writing writing, SchemaItem item, Place place)
        {
            NumberRange range = new NumberRange();
            if (place.InArray)
            {
                if (HandleListNames.Contains(place.Container, StringComparer.Ordinal))
                {
                    range = NumberRange.Handle();
                }
                else if (place.UnderTop
                    && PositionListNames.Contains(place.Container, StringComparer.Ordinal))
                {
                    range = NumberRange.Position();
                }
            }
            else if (string.Equals(item.Name, ParentHandleName, StringComparison.Ordinal))
            {
                range = NumberRange.Handle();
            }
            else if (string.Equals(item.Name, ParentIndexName, StringComparison.Ordinal))
            {
                range = NumberRange.Position();
            }
            else if (!place.Top)
            {
                if (place.UnderTop && RangeNames.Contains(place.Container, StringComparer.Ordinal))
                {
                    range = string.Equals(item.Name, StartName, StringComparison.Ordinal)
                        ? NumberRange.Position()
                        : NumberRange.Counted();
                }
            }
            else if (PmxHandleNames.Contains(item.Name, StringComparer.Ordinal))
            {
                range = NumberRange.Handle();
            }
            else if (string.Equals(item.Name, OffsetName, StringComparison.Ordinal))
            {
                range = NumberRange.Position();
            }
            else if (string.Equals(item.Name, LimitName, StringComparison.Ordinal)
                || string.Equals(item.Name, CountName, StringComparison.Ordinal))
            {
                range = writing.Dispatched ? NumberRange.Counted() : new NumberRange();

                // 一覧の件数と発行する数は、応答で返せる件数から導く値なので正本に書かない。
                if (writing.Listing != null
                    && string.Equals(item.Name, LimitName, StringComparison.Ordinal))
                {
                    range = range.Within(NumberRange.Counted().Real())
                        .WithDefault(writing.Listing.LimitDefault);
                }

                if (writing.Issuable.HasValue
                    && string.Equals(item.Name, CountName, StringComparison.Ordinal))
                {
                    range = range.Within(new NumberRange(false, Bound.Of(1), Bound.Of(writing.Issuable.Value)));
                }
            }

            HostNumberRead read;
            if (TryComposed(writing, item, writing.Rules?.ComposedReads.Numbers, out read))
            {
                range = range.Within(new NumberRange(
                    read.Integer, Bound.Of(read.Least, read.LeastExcluded), Bound.Of(read.Most)));
            }

            return range.Within(item.Bounds);
        }

        private static bool TryComposed<T>(
            Writing writing, SchemaItem item, IDictionary<string, T> table, out T found)
        {
            found = default(T);
            string path;

            return table != null
                && writing.ItemPaths.TryGetValue(item, out path)
                && table.TryGetValue(writing.Schema.Tool + " " + path, out found);
        }

        private static JsonObjectText Numeric(SchemaItem item, NumberRange range)
        {
            JsonObjectText written = new JsonObjectText()
                .Add("type", TypeOf(range.Integer ? IntegerType : NumberType, item.Nullable));
            if (range.Least != null)
            {
                written.Add(range.Least.Excluded ? "exclusiveMinimum" : "minimum", range.Least.Text);
            }

            if (range.Most != null)
            {
                written.Add("maximum", range.Most.Text);
            }

            if (range.Default.HasValue)
            {
                written.AddNumber("default", range.Default.Value);
            }

            return written;
        }

        private static JsonObjectText SdkValue(Writing writing, SchemaItem item, string type)
        {
            InputRules rules = writing.Rules;
            IList<string> names;
            if (rules.EnumNames.TryGetValue(type, out names) && names.Count > 0)
            {
                JsonObjectText named = new JsonObjectText().Add("type", TypeOf(StringType, item.Nullable));
                if (!rules.FlagEnums.Contains(type))
                {
                    return named.Add("enum", JsonWriter.TextArray(names));
                }

                string any = "(?:" + string.Join("|", names) + ")";

                return named.AddText("pattern", "^" + any + "(?:, " + any + ")*$");
            }

            int count;
            if (rules.Components.TryGetValue(type, out count))
            {
                return Components(item, Numeric(Plain, NumberRange.Single()).Text, count, count);
            }

            if (rules.HandledTypes.Contains(type))
            {
                return Numeric(item, NumberRange.Handle().Within(item.Bounds));
            }

            if (rules.PositionedTypes.Contains(type))
            {
                return Numeric(item, NumberRange.Position().Within(item.Bounds));
            }

            switch (type)
            {
                case "System.Boolean":
                    return new JsonObjectText().Add("type", TypeOf(BooleanType, item.Nullable));

                case "System.Byte":
                    return Numeric(
                        item,
                        new NumberRange(true, Bound.Of(byte.MinValue), Bound.Of(byte.MaxValue)).Within(item.Bounds));

                case "System.Int32":
                    return Numeric(item, NumberRange.Int32().Within(item.Bounds));

                case "System.Single":
                    return Numeric(item, NumberRange.Single().Within(item.Bounds));

                case "System.Double":
                    return Numeric(item, new NumberRange().Within(item.Bounds));

                case "System.String":
                    return new JsonObjectText().Add("type", TypeOf(StringType, item.Nullable));

                case "System.Version":
                    return new JsonObjectText()
                        .Add("type", TypeOf(StringType, item.Nullable))
                        .AddText("pattern", VersionPattern);

                case "System.Object":
                    return new JsonObjectText();

                case Base64Spelling:
                case "System.Drawing.Bitmap":
                    return new JsonObjectText()
                        .Add("type", TypeOf(StringType, item.Nullable))
                        .AddText("pattern", Base64Pattern);

                case "System.Drawing.Color":
                case "System.Drawing.Brush":
                    return Components(
                        item,
                        Numeric(Plain, new NumberRange(false, Bound.Of(0), Bound.Of(1))).Text,
                        3,
                        4);

                case "System.Drawing.Point":
                case "System.Drawing.Size":
                    return Components(item, Numeric(Plain, NumberRange.Int32()).Text, 2, 2);

                case "System.Drawing.Rectangle":
                    return Components(item, Numeric(Plain, NumberRange.Int32()).Text, 4, 4);

                case "System.Drawing.Font":
                    IList<string> styles;
                    return Font(
                        item,
                        rules.EnumNames.TryGetValue("System.Drawing.FontStyle", out styles) ? styles : null);

                default:
                    return null;
            }
        }

        private static readonly SchemaItem Plain = new SchemaItem(
            NumberType, null, null, null, null, null, null, false, null, null, null, false, null);

        private static JsonObjectText Components(SchemaItem item, string element, int least, int most)
        {
            return new JsonObjectText()
                .Add("type", TypeOf(ArrayType, item.Nullable))
                .Add("items", element)
                .AddNumber("minItems", least)
                .AddNumber("maxItems", most);
        }

        private static JsonObjectText Font(SchemaItem item, IList<string> styles)
        {
            JsonObjectText family = new JsonObjectText().AddText("type", StringType);
            JsonObjectText size = new JsonObjectText().AddText("type", NumberType);
            JsonObjectText style = new JsonObjectText().AddText("type", StringType);
            if (styles != null && styles.Count > 0)
            {
                family.AddNumber("minLength", 1);
                size.AddNumber("exclusiveMinimum", 0).Add("maximum", SingleMost);
                string any = "(?:" + string.Join("|", styles) + ")";
                style.AddText("pattern", "^" + any + "(?:, " + any + ")*$");
            }

            JsonObjectText members = new JsonObjectText()
                .Add("family", family.Text)
                .Add("size", size.Text)
                .Add("style", style.Text);

            return new JsonObjectText()
                .Add("type", TypeOf(ObjectType, item.Nullable))
                .Add("properties", members.Text)
                .Add("required", JsonWriter.TextArray(new[] { "family", "size", "style" }))
                .AddBoolean("additionalProperties", false);
        }

        /// <summary>
        /// 形の綴り。値を持たないことを許す項目は、その形と null の両方を受け取る。
        /// </summary>
        private static string TypeOf(string type, bool? nullable)
        {
            return nullable.HasValue && nullable.Value
                ? JsonWriter.TextArray(new[] { type, "null" })
                : JsonText.Quote(type);
        }

        private static string Value(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            if (value is string)
            {
                return JsonText.Quote((string)value);
            }

            return JsonWriter.Number(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }

        private static IList<string> Readable(ToolSchema schema)
        {
            SchemaItem answer = ListingLimitRule.Answer(schema.Output);
            if (ListingLimitRule.IsListing(schema))
            {
                answer = answer.Members.First(
                    m => string.Equals(m.Name, ListingLimitRule.ItemsName, StringComparison.Ordinal))
                    .Element;
            }

            return answer.Members == null
                ? new string[0]
                : answer.Members.Select(m => m.Name).Where(n => n != null).ToList();
        }

        private sealed class Bound
        {
            private const double LongReach = 9007199254740992d;

            private Bound(double value, string text, bool excluded)
            {
                Value = value;
                Text = text;
                Excluded = excluded;
            }

            public double Value { get; }

            public string Text { get; }

            /// <summary>その値そのものを含まない下限なら真。</summary>
            public bool Excluded { get; }

            public static Bound Of(double value, bool excluded = false)
            {
                return new Bound(
                    value,
                    Math.Abs(value) < LongReach
                        ? JsonWriter.Number(value)
                        : value.ToString("R", CultureInfo.InvariantCulture),
                    excluded);
            }

            public static Bound Written(double value, string text)
            {
                return new Bound(value, text, false);
            }
        }

        private sealed class NumberRange
        {
            public NumberRange()
            {
            }

            public NumberRange(bool integer, Bound least, Bound most)
            {
                Integer = integer;
                Least = least;
                Most = most;
            }

            public bool Integer { get; private set; }

            public Bound Least { get; private set; }

            public Bound Most { get; private set; }

            public int? Default { get; private set; }

            public static NumberRange Handle()
            {
                return new NumberRange(true, Bound.Of(1), Bound.Of(int.MaxValue));
            }

            public static NumberRange Position()
            {
                return new NumberRange(true, Bound.Of(0), Bound.Of(int.MaxValue));
            }

            public static NumberRange Counted()
            {
                return new NumberRange(true, Bound.Of(1), Bound.Of(int.MaxValue));
            }

            public static NumberRange Int32()
            {
                return new NumberRange(true, Bound.Of(int.MinValue), Bound.Of(int.MaxValue));
            }

            public static NumberRange Single()
            {
                return new NumberRange(
                    false,
                    Bound.Written(-float.MaxValue, "-" + SingleMost),
                    Bound.Written(float.MaxValue, SingleMost));
            }

            public NumberRange Real()
            {
                return new NumberRange(false, Least, Most);
            }

            public NumberRange Within(NumberRange other)
            {
                return new NumberRange(
                    Integer || other.Integer,
                    Larger(Least, other.Least),
                    Smaller(Most, other.Most))
                {
                    Default = Default ?? other.Default,
                };
            }

            public NumberRange Within(ValueBounds bounds)
            {
                if (bounds == null)
                {
                    return this;
                }

                return Within(new NumberRange(
                    false,
                    bounds.Minimum.HasValue ? Bound.Of(bounds.Minimum.Value) : null,
                    bounds.Maximum.HasValue ? Bound.Of(bounds.Maximum.Value) : null));
            }

            public NumberRange WithDefault(int value)
            {
                NumberRange made = Within(new NumberRange());
                made.Default = value;

                return made;
            }

            private static Bound Larger(Bound one, Bound other)
            {
                if (one == null)
                {
                    return other;
                }

                if (other == null || one.Value > other.Value)
                {
                    return one;
                }

                return one.Value == other.Value && one.Excluded ? one : other;
            }

            private static Bound Smaller(Bound one, Bound other)
            {
                if (one == null)
                {
                    return other;
                }

                return other == null || one.Value <= other.Value ? one : other;
            }
        }
    }
}
