using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>MCPクライアントへ載せるツール定義の1件。</summary>
    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, string description, string inputSchema)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
        }

        public string Name { get; }

        public string Description { get; }

        /// <summary>入力の形をJSON Schemaで綴ったもの。</summary>
        public string InputSchema { get; }
    }

    /// <summary>
    /// スキーマ正本と説明文から、MCPクライアントへ載せるツール定義を組み立てる。件数と要素数の
    /// 上限は正本に書かず、[逆算の規則](ListingLimitRule)と[要素数の規則](ElementLimitRule)が
    /// 導いた値をここで入れる——予算を変えれば動く値なので、書き写せば必ず食い違う。
    /// </summary>
    public static class ToolDefinitionBuilder
    {
        /// <summary>一覧が何件返すかを受け取る入力の名前。</summary>
        public const string LimitName = "limit";

        /// <summary>一覧が切り出す前の総数を返す項目の名前。</summary>
        public const string TotalName = "total";

        /// <summary>ハンドルをいくつ発行するかを受け取る入力の名前。</summary>
        public const string CountName = "count";

        private const string ObjectType = "object";

        private const string ArrayType = "array";

        private static readonly IDictionary<string, string> ScalarTypes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "boolean", "boolean" },
                { "number", "number" },
                { "text", "string" },
                { "base64", "string" },
                { "enum_name", "string" },
                { "image", "string" },
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

        /// <summary>危険操作の確認を受け取る共通引数の名前。</summary>
        public const string ConfirmName = "confirm";

        /// <summary>Undoの記録を止めることを頼む共通引数の名前。</summary>
        public const string SuppressName = "suppressUndo";

        /// <summary>どのPMXを見るかを切り替える共通引数の名前。</summary>
        public const string PmxHandleName = "pmxHandle";

        /// <summary>
        /// ツール定義を綴りの順に組み立てる。<paramref name="descriptions"/> はツール名から説明文へ、
        /// <paramref name="valueChars"/> は応答の値の枠、<paramref name="requestBudgetBytes"/> は
        /// 要求サイズ予算、<paramref name="tokenLimit"/> はIPCの構造トークンの上限、
        /// <paramref name="sdkShapes"/> はSDKに由来する項目の綴り、<paramref name="dangerous"/> は
        /// 確認を要するツールの名前。
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
            ISet<string> conditional,
            ISet<string> suppressing)
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

            if (conditional == null)
            {
                throw new ArgumentNullException(nameof(conditional));
            }

            if (suppressing == null)
            {
                throw new ArgumentNullException(nameof(suppressing));
            }

            List<ToolDefinition> definitions = new List<ToolDefinition>();
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                string description;
                if (!descriptions.TryGetValue(schema.Tool, out description))
                {
                    throw new InvalidOperationException("説明文が無いツール: " + schema.Tool);
                }

                definitions.Add(new ToolDefinition(
                    schema.Tool,
                    description,
                    InputSchema(
                        schema,
                        lengths,
                        valueChars,
                        requestBudgetBytes,
                        tokenLimit,
                        sdkShapes,
                        dangerous.Contains(schema.Tool),
                        conditional.Contains(schema.Tool),
                        suppressing.Contains(schema.Tool))));
            }

            return definitions;
        }

        private static string InputSchema(
            ToolSchema schema,
            AssumedLength lengths,
            int valueChars,
            int requestBudgetBytes,
            int tokenLimit,
            IDictionary<SchemaItem, string> sdkShapes,
            bool confirms,
            bool conditional,
            bool suppresses)
        {
            ListingLimits listing = IsListing(schema)
                ? ListingLimitRule.Derive(schema, lengths, valueChars)
                : null;

            List<BranchShape> branches = schema.Branches
                .Select(b => Branch(
                    schema,
                    b,
                    listing,
                    lengths,
                    valueChars,
                    requestBudgetBytes,
                    tokenLimit,
                    sdkShapes,
                    confirms,
                    conditional,
                    suppresses))
                .ToList();

            return Compose(branches.Count == 1 ? branches[0] : Merge(branches));
        }

        /// <summary>分岐1つ分の、まとめる前の構成要素。</summary>
        private sealed class BranchShape
        {
            public BranchShape(
                List<KeyValuePair<string, string>> properties,
                List<string> required,
                List<string> rules)
            {
                Properties = properties;
                Required = required;
                Rules = rules;
            }

            /// <summary>引数の名前と、その形を綴ったもの。</summary>
            public List<KeyValuePair<string, string>> Properties { get; }

            public List<string> Required { get; }

            /// <summary>どれか1つだけ渡す、といった引数どうしの決まり。</summary>
            public List<string> Rules { get; }
        }

        private static BranchShape Branch(
            ToolSchema schema,
            SchemaBranch branch,
            ListingLimits listing,
            AssumedLength lengths,
            int valueChars,
            int requestBudgetBytes,
            int tokenLimit,
            IDictionary<SchemaItem, string> sdkShapes,
            bool confirms,
            bool conditional,
            bool suppresses)
        {
            IDictionary<SchemaItem, int> limits =
                ElementLimitRule.Request(branch, lengths, requestBudgetBytes, tokenLimit);

            // 応答が並びを返すなら、要求で受ける件数も応答で返せる件数より多くできない。
            SchemaItem returned = schema.Output == null ? null : schema.Output.Element;
            int? issued = returned == null
                ? (int?)null
                : ElementLimitRule.Response(returned, lengths, valueChars);
            if (issued.HasValue)
            {
                foreach (SchemaItem array in limits.Keys.ToList())
                {
                    limits[array] = ElementLimitRule.Bounded(limits[array], issued.Value);
                }
            }

            List<KeyValuePair<string, string>> properties =
                new List<KeyValuePair<string, string>>();
            List<string> required = new List<string>();

            // ホストが自分で入れる引数は、呼び出す側へ現れない。
            foreach (SchemaItem input in branch.Inputs.Where(i => !i.Injected))
            {
                properties.Add(new KeyValuePair<string, string>(
                    input.Name, Item(schema, branch, input, listing, limits, issued, sdkShapes)));
                if (input.Required.HasValue && input.Required.Value)
                {
                    required.Add(input.Name);
                }
            }

            // 確認の共通引数は、どのシグネチャが危険操作に当たるかの決め方が導くので正本に書かない。
            // 要らないツールには現れない。対象で要否が分かれる呼び出しでは、必ず渡す側へ入れずに
            // 下の決まりで表す。
            if (confirms)
            {
                properties.Add(new KeyValuePair<string, string>(
                    ConfirmName, new JsonObjectText().AddText("type", "boolean").Text));
                if (!conditional)
                {
                    required.Add(ConfirmName);
                }
            }

            // 抑止の共通引数も、どの行が複製編集型かの決め方が導くので正本に書かない。まとめて
            // 反映する呼び出しにだけ現れ、渡さなければ止めない。
            if (suppresses)
            {
                properties.Add(new KeyValuePair<string, string>(
                    SuppressName, new JsonObjectText().AddText("type", "boolean").Text));
            }

            List<string> rules = (branch.Choices ?? new SchemaChoice[0]).Select(Choice).ToList();
            if (confirms && conditional)
            {
                rules.Add(Conditional());
            }

            return new BranchShape(properties, required, rules);
        }

        /// <summary>分岐1つ分を、MCPのツール定義が読む形へ綴る。</summary>
        private static string Compose(BranchShape shape)
        {
            JsonObjectText properties = new JsonObjectText();
            foreach (KeyValuePair<string, string> property in shape.Properties)
            {
                properties.Add(property.Key, property.Value);
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
        private static BranchShape Merge(List<BranchShape> branches)
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

            List<KeyValuePair<string, string>> properties =
                new List<KeyValuePair<string, string>>();
            foreach (string name in names)
            {
                List<string> found = forms[name];
                properties.Add(new KeyValuePair<string, string>(
                    name,
                    found.Count == 1
                        ? found[0]
                        : new JsonObjectText().Add("anyOf", JsonWriter.Array(found)).Text));
            }

            List<string> required = branches[0].Required
                .Where(n => branches.All(b => b.Required.Contains(n, StringComparer.Ordinal)))
                .ToList();

            return new BranchShape(properties, required, new List<string>());
        }

        /// <summary>
        /// 対象で要否が分かれる確認の決まりを綴る。どのPMXを見るかを切り替えていれば確認は要らず、
        /// 切り替えていなければ開いているPMXが相手になるので確認を要る。
        /// </summary>
        private static string Conditional()
        {
            string[] cases =
            {
                new JsonObjectText()
                    .Add("required", JsonWriter.TextArray(new[] { PmxHandleName })).Text,
                new JsonObjectText()
                    .Add("required", JsonWriter.TextArray(new[] { ConfirmName })).Text,
            };

            return new JsonObjectText().Add("anyOf", JsonWriter.Array(cases)).Text;
        }

        /// <summary>
        /// まとまりのうち1つだけを受け取る決まりを綴る。必ず要るまとまりはどれか1つを要り、
        /// 要らないまとまりはどれも無い形も許す。
        /// </summary>
        private static string Choice(SchemaChoice choice)
        {
            List<string> cases = choice.Names
                .Select(n => new JsonObjectText().Add("required", JsonWriter.TextArray(new[] { n })).Text)
                .ToList();

            if (!choice.Required)
            {
                cases.Add(new JsonObjectText()
                    .Add("not", new JsonObjectText().Add("anyOf", JsonWriter.Array(cases)).Text)
                    .Text);
            }

            return new JsonObjectText().Add("oneOf", JsonWriter.Array(cases)).Text;
        }

        private static string Item(
            ToolSchema schema,
            SchemaBranch branch,
            SchemaItem item,
            ListingLimits listing,
            IDictionary<SchemaItem, int> limits,
            int? issued,
            IDictionary<SchemaItem, string> sdkShapes,
            bool distributed = false)
        {
            JsonObjectText written = Shape(
                schema,
                branch,
                item,
                listing,
                limits,
                issued,
                sdkShapes,
                distributed || string.Equals(
                    item.Name, ElementLimitRule.DistributedName, StringComparison.Ordinal));

            if (item.Bounds != null)
            {
                if (item.Bounds.Minimum.HasValue)
                {
                    written.AddNumber("minimum", item.Bounds.Minimum.Value);
                }

                if (item.Bounds.Maximum.HasValue)
                {
                    written.AddNumber("maximum", item.Bounds.Maximum.Value);
                }
            }

            if (item.HasDefault)
            {
                written.Add("default", Value(item.Default));
            }

            // 値で分かれる呼び分けは、その項目の値そのものが分岐を選ぶ。名前が同じだけの入れ子の
            // 項目まで縛らないよう、分岐が直に受け取る入力に限る。
            if (branch.SelectorName != null
                && branch.Inputs.Contains(item)
                && string.Equals(branch.SelectorName, item.Name, StringComparison.Ordinal))
            {
                written.Add("const", Value(branch.SelectorValue));
            }

            return written.Text;
        }

        private static JsonObjectText Shape(
            ToolSchema schema,
            SchemaBranch branch,
            SchemaItem item,
            ListingLimits listing,
            IDictionary<SchemaItem, int> limits,
            int? issued,
            IDictionary<SchemaItem, string> sdkShapes,
            bool distributed)
        {
            if (item.Members != null)
            {
                JsonObjectText members = new JsonObjectText();
                List<string> required = new List<string>();
                foreach (SchemaItem member in item.Members)
                {
                    members.Add(
                        member.Name,
                        Item(
                            schema, branch, member, listing, limits, issued, sdkShapes,
                            distributed));
                    if (member.Required.HasValue && member.Required.Value)
                    {
                        required.Add(member.Name);
                    }
                }

                JsonObjectText body = new JsonObjectText()
                    .Add("type", TypeOf(ObjectType, item.Nullable));
                body.Add("properties", members.Text);
                if (required.Count > 0)
                {
                    body.Add("required", JsonWriter.TextArray(required));
                }

                return body.AddBoolean("additionalProperties", false);
            }

            if (item.Element != null)
            {
                JsonObjectText body = new JsonObjectText()
                    .Add("type", TypeOf(ArrayType, item.Nullable));
                body.Add(
                    "items",
                    Item(
                        schema, branch, item.Element, listing, limits, issued, sdkShapes,
                        distributed));
                if (NonEmptyArrayRule.NonEmpty(item))
                {
                    body.AddNumber("minItems", 1);
                }

                int maxItems;
                if (item.MaxItems.HasValue)
                {
                    maxItems = item.MaxItems.Value;
                }
                else if (distributed)
                {
                    return body;
                }
                else if (!limits.TryGetValue(item, out maxItems))
                {
                    throw new InvalidOperationException(
                        "要素数の上限を導けない並び: " + schema.Tool + "." + item.Name);
                }

                return body.AddNumber("maxItems", maxItems);
            }

            string spelling = item.Shape;
            if (spelling == null && !sdkShapes.TryGetValue(item, out spelling))
            {
                throw new InvalidOperationException(
                    "形を持たない項目: " + schema.Tool + "." + item.Name);
            }

            return Scalar(schema, branch, item, listing, issued, spelling);
        }

        private static JsonObjectText Scalar(
            ToolSchema schema,
            SchemaBranch branch,
            SchemaItem item,
            ListingLimits listing,
            int? issued,
            string spelling)
        {
            int[] fixedArray;
            if (FixedArrays.TryGetValue(spelling, out fixedArray))
            {
                return new JsonObjectText()
                    .Add("type", TypeOf(ArrayType, item.Nullable))
                    .Add("items", new JsonObjectText().AddText("type", "number").Text)
                    .AddNumber("minItems", fixedArray[0])
                    .AddNumber("maxItems", fixedArray[1]);
            }

            if (string.Equals(spelling, "number_array", StringComparison.Ordinal))
            {
                return new JsonObjectText()
                    .Add("type", TypeOf(ArrayType, item.Nullable))
                    .Add("items", new JsonObjectText().AddText("type", "number").Text);
            }

            if (string.Equals(spelling, "font", StringComparison.Ordinal))
            {
                JsonObjectText members = new JsonObjectText()
                    .Add("family", new JsonObjectText().AddText("type", "string").Text)
                    .Add("size", new JsonObjectText().AddText("type", "number").Text)
                    .Add("style", new JsonObjectText().AddText("type", "string").Text);

                return new JsonObjectText()
                    .Add("type", TypeOf(ObjectType, item.Nullable))
                    .Add("properties", members.Text)
                    .Add("required", JsonWriter.TextArray(new[] { "family", "size", "style" }))
                    .AddBoolean("additionalProperties", false);
            }

            if (string.Equals(spelling, "json", StringComparison.Ordinal))
            {
                return new JsonObjectText();
            }

            string type;
            if (!ScalarTypes.TryGetValue(spelling, out type))
            {
                throw new InvalidOperationException(
                    "組み立て方を持たない綴り: " + spelling + "(" + schema.Tool + ")");
            }

            JsonObjectText written = new JsonObjectText().Add("type", TypeOf(type, item.Nullable));

            // 一覧の件数は予算から導く値なので、正本ではなくここで入れる。
            if (listing != null
                && string.Equals(item.Name, LimitName, StringComparison.Ordinal)
                && branch.Inputs.Contains(item))
            {
                written.AddNumber("minimum", 1);
                written.AddNumber("maximum", listing.LimitMaximum);
                written.AddNumber("default", listing.LimitDefault);
            }

            // 発行する数も、応答で返せる件数から導く値なので正本に書かない。
            if (issued.HasValue
                && string.Equals(item.Name, CountName, StringComparison.Ordinal)
                && branch.Inputs.Contains(item))
            {
                written.AddNumber("minimum", 1);
                written.AddNumber("maximum", issued.Value);
            }

            return written;
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

            return JsonWriter.Number(Convert.ToDouble(value));
        }

        /// <summary>応答が総数と切り出した並びを返す形か。一覧を返すツールはこの形を取る。</summary>
        private static bool IsListing(ToolSchema schema)
        {
            IList<SchemaItem> members = schema.Output == null ? null : schema.Output.Members;

            return members != null
                && members.Any(m => string.Equals(m.Name, TotalName, StringComparison.Ordinal))
                && members.Any(
                    m => string.Equals(m.Name, ListingLimitRule.ItemsName, StringComparison.Ordinal)
                        && m.Element != null);
        }
    }
}
