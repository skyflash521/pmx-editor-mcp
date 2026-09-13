using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表とスキーマ正本から、実機のエディタへ投げる検査を組み立てる。行ごとに書き下ろさず
    /// ここで導くので、行が増えれば検査も増える。
    /// </summary>
    public static class E2eCaseBuilder
    {
        /// <summary>危険操作が確認を求めるときに使う共通引数の名前。</summary>
        public const string ConfirmName = "confirm";

        /// <summary>危険操作の確認が無いことを断る綴り。</summary>
        public const string ConfirmRequired = "TOOL_CONFIRM_REQUIRED";

        /// <summary>台帳に無いハンドルを断る綴り。</summary>
        public const string InvalidHandle = "TOOL_INVALID_HANDLE";

        /// <summary>引数の値が不正であることを断る綴り。</summary>
        public const string InvalidArgument = "TOOL_INVALID_ARGUMENT";

        /// <summary>範囲の外の位置を断る綴り。</summary>
        public const string IndexOutOfRange = "TOOL_INDEX_OUT_OF_RANGE";

        /// <summary>一覧が何件返すかを受け取る入力の名前。</summary>
        public const string LimitName = "limit";

        /// <summary>ハンドルの並びを受け取る入力の名前。</summary>
        public const string HandlesName = "handles";

        /// <summary>対象を全件にする入力の名前。</summary>
        private const string WholeName = "all";

        /// <summary>親を全件にする入力の名前。</summary>
        private const string ParentWholeName = "parentAll";

        /// <summary>値の組を受け取る入力の名前。</summary>
        private const string ValueName = "value";

        /// <summary>値の組の並びを受け取る入力の名前。</summary>
        private const string ValuesName = "values";

        /// <summary>並べたものを載せる応答の項目の名前。</summary>
        private const string ItemsName = "items";

        /// <summary>
        /// 位置で指す項目へ渡す、どのリストにも無い位置。負でない整数の上限なので、要素の数が
        /// これに届くことはない。
        /// </summary>
        private const int UnknownPosition = int.MaxValue;

        /// <summary>
        /// 対象を指す項目の名前。対象が決まっている呼び出しでは、この組を埋めない——ハンドルで
        /// 指した対象は、位置でも親でも指し直せない。
        /// </summary>
        private static readonly string[] PointingNames =
        {
            "indices", "range", "all", "handles",
            "parentIndices", "parentRange", "parentAll", "parentHandles",
        };

        /// <summary>
        /// 台帳に無いハンドルとして渡す値。ホストの発行器がこの値を決して発行しないので、どの
        /// 台帳にも在り得ない。
        /// </summary>
        private const int UnknownHandle = int.MaxValue;

        /// <summary>
        /// 検査を組み立てる。<paramref name="connectionPaths"/> は型から接続の経路へ、
        /// <paramref name="dangerous"/> は確認を要する行キーの集合。断る理由の綴りはここが名指し
        /// する——どの綴りがどの断り方を指すかは機械では導けない。
        /// 名指しが実装とずれていれば、その検査が実機で落ちて分かる。
        /// </summary>
        public static IList<E2eCase> Build(
            ToolMap map,
            ToolSchemaTable schemas,
            IDictionary<string, string> toolsByRow,
            IDictionary<string, string> connectionPaths,
            ISet<string> dangerous,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, string> sdkTypes = null,
            SampleValueTable samples = null,
            IDictionary<string, string> viewImages = null,
            ISet<string> positioned = null,
            IDictionary<string, string> factories = null)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (schemas == null)
            {
                throw new ArgumentNullException(nameof(schemas));
            }

            if (toolsByRow == null)
            {
                throw new ArgumentNullException(nameof(toolsByRow));
            }

            if (connectionPaths == null)
            {
                throw new ArgumentNullException(nameof(connectionPaths));
            }

            if (dangerous == null)
            {
                throw new ArgumentNullException(nameof(dangerous));
            }

            if (sdkShapes == null)
            {
                throw new ArgumentNullException(nameof(sdkShapes));
            }

            // 母集団はスキーマ正本が持つツールである。行から導く名前を持たない共通契約のツールも
            // 検査の相手なので、行の側を母集団にすると落ちる。
            Dictionary<string, ToolMapRow> byTool = new Dictionary<string, ToolMapRow>(
                StringComparer.Ordinal);
            Dictionary<string, ToolMapRow> rows = map.Rows.ToDictionary(
                r => r.SignatureKey, r => r, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> named in toolsByRow)
            {
                ToolMapRow row;
                if (rows.TryGetValue(named.Key, out row))
                {
                    byTool[named.Value] = row;
                }
            }

            List<E2eCase> cases = new List<E2eCase>(SetupCases(schemas, factories));
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                ToolMapRow row;
                byTool.TryGetValue(schema.Tool, out row);
                cases.AddRange(Cases(
                    row,
                    schema,
                    connectionPaths,
                    dangerous,
                    sdkShapes,
                    Sampled(sdkTypes, samples)));
                cases.AddRange(ImageCases(row, schema, connectionPaths, viewImages));
                cases.AddRange(ReadingCases(row, schema, connectionPaths));
                cases.AddRange(PositionCases(
                    row, schema, connectionPaths, sdkTypes, positioned, dangerous));
            }

            return cases;
        }

        /// <summary>
        /// 先に流す段取り。要素を1つ作って並びへ加える。中身の無い並びでは、項目を読む検査が
        /// 一度も項目を読まないまま通ってしまう。
        /// </summary>
        private static IEnumerable<E2eCase> SetupCases(
            ToolSchemaTable schemas, IDictionary<string, string> factories)
        {
            if (factories == null)
            {
                yield break;
            }

            ISet<string> named = new HashSet<string>(
                schemas.Tools.Select(t => t.Tool), StringComparer.Ordinal);
            ISet<string> handed = new HashSet<string>(
                schemas.Tools
                    .Where(t => t.Branches.Any(b => b.Inputs.All(
                        i => i.Injected
                            || string.Equals(i.Name, HandlesName, StringComparison.Ordinal))))
                    .Select(t => t.Tool),
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in factories
                .Where(f => handed.Contains(f.Key) && named.Contains(f.Value))
                .OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                yield return new E2eCase(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    pair.Value,
                    "並びへ加える要素を1つ作れること",
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    E2eExpectation.Success,
                    null,
                    null,
                    pair.Key);
                yield return new E2eCase(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    pair.Key,
                    "作った要素を並びへ加えられること",
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    E2eExpectation.Success,
                    null,
                    null,
                    null,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { HandlesName, pair.Key },
                    });
            }
        }

        /// <summary>
        /// ビューの絵を返すツールの検査。写し取れるのは窓を持つビューだけなので、その1枚を相手に
        /// 全系統を見比べ、そのビューを返す行だけが合うことを確かめる。
        /// </summary>
        private static IEnumerable<E2eCase> ImageCases(
            ToolMapRow row,
            ToolSchema schema,
            IDictionary<string, string> connectionPaths,
            IDictionary<string, string> viewImages)
        {
            string view;
            if (viewImages == null || !viewImages.TryGetValue(schema.Tool, out view))
            {
                yield break;
            }

            string rowKey = row == null ? string.Empty : row.SignatureKey;
            yield return new E2eCase(
                rowKey,
                row == null ? string.Empty : ToolMapJsonReader.SpellingOf(row.EditKind),
                row == null ? string.Empty : ConnectionPath(rowKey, connectionPaths),
                schema.Tool,
                "返す絵が写し取ったビューの姿と合うこと",
                new Dictionary<string, object>(StringComparer.Ordinal),
                E2eExpectation.ViewImage,
                null,
                view);
        }

        private static IEnumerable<E2eCase> Cases(
            ToolMapRow row,
            ToolSchema schema,
            IDictionary<string, string> connectionPaths,
            ISet<string> dangerous,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled)
        {
            // 行から導く名前を持たないツールは、行の値も接続の経路も持たない。
            string rowKey = row == null ? string.Empty : row.SignatureKey;
            string tool = schema.Tool;
            string path = row == null ? string.Empty : ConnectionPath(rowKey, connectionPaths);
            string editKind = row == null ? string.Empty : ToolMapJsonReader.SpellingOf(row.EditKind);
            bool confirmed = row != null && dangerous.Contains(rowKey);

            yield return new E2eCase(
                rowKey,
                editKind,
                path,
                tool,
                "未知のメソッドとして断られないこと",
                new Dictionary<string, object>(StringComparer.Ordinal),
                E2eExpectation.Dispatched,
                null);

            if (confirmed)
            {
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    "確認を渡さない呼び出しを断ること",
                    Arguments(false),
                    E2eExpectation.Refusal,
                    ConfirmRequired);
            }

            foreach (SchemaItem handles in HandleInputs(schema))
            {
                IDictionary<string, object> arguments =
                    Single(handles.Name, new object[] { UnknownHandle }, confirmed);
                if (!TryFill(schema, sdkShapes, sampled, arguments))
                {
                    continue;
                }

                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    "台帳に無いハンドルを渡す呼び出しを断ること",
                    arguments,
                    E2eExpectation.Refusal,
                    InvalidHandle);
            }

            foreach (SchemaItem limit in LimitInputs(schema))
            {
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    "件数に0を渡す呼び出しを断ること",
                    Single(limit.Name, 0, confirmed),
                    E2eExpectation.Refusal,
                    InvalidArgument);
            }
        }

        /// <summary>
        /// その呼び分けで、対象を指す組のほかに必ず要る組を、最小の値で埋める。埋められない形が
        /// 在れば偽——確かめたい断り方ではなく、埋め忘れを断ることになるからである。
        /// </summary>
        private static bool TryFill(
            ToolSchema schema,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            IDictionary<string, object> arguments)
        {
            foreach (SchemaBranch branch in schema.Branches)
            {
                foreach (SchemaChoice choice in branch.Choices)
                {
                    if (!choice.Required
                        || choice.Names.Any(n => PointingNames.Contains(n, StringComparer.Ordinal)))
                    {
                        continue;
                    }

                    SchemaItem item = branch.Inputs.FirstOrDefault(
                        i => string.Equals(i.Name, choice.Names[0], StringComparison.Ordinal));
                    object value;
                    if (item == null || !TryMinimal(item, sdkShapes, sampled, out value))
                    {
                        return false;
                    }

                    arguments[item.Name] = value;
                }

                foreach (SchemaItem item in branch.Inputs
                    .Where(i => i.Required == true
                        && !i.Injected
                        && !arguments.ContainsKey(i.Name)
                        && !PointingNames.Contains(i.Name, StringComparer.Ordinal)))
                {
                    // 分岐を選ぶ項目は、その分岐が選ばれる値でなければ届かない。
                    object value = string.Equals(
                        branch.SelectorName, item.Name, StringComparison.Ordinal)
                            ? branch.SelectorValue
                            : null;
                    if (value == null && !TryMinimal(item, sdkShapes, sampled, out value))
                    {
                        return false;
                    }

                    arguments[item.Name] = value;
                }
            }

            return true;
        }

        /// <summary>
        /// その項目の最小の値。組は必ず要る項目だけを埋めた組、配列は要素1つの並び、綴りは
        /// その綴りが受け取る最も短い値とする。綴りから値を決められなければ偽。
        /// </summary>
        private static bool TryMinimal(
            SchemaItem item,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            out object value)
        {
            value = null;
            if (item.Members != null)
            {
                Dictionary<string, object> members =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (SchemaItem member in item.Members.Where(m => m.Required == true))
                {
                    object one;
                    if (!TryMinimal(member, sdkShapes, sampled, out one))
                    {
                        return false;
                    }

                    members[member.Name] = one;
                }

                value = members;

                return true;
            }

            if (item.Element != null)
            {
                object one;
                if (!TryMinimal(item.Element, sdkShapes, sampled, out one))
                {
                    return false;
                }

                value = new[] { one };

                return true;
            }

            string shape;
            if (!sdkShapes.TryGetValue(item, out shape))
            {
                shape = item.Shape;
            }

            object sample;
            if (sampled.TryGetValue(item, out sample))
            {
                value = sample;

                return true;
            }

            switch (shape)
            {
                case "number":
                    value = 0;
                    return true;

                case "text":
                case "base64":
                    value = string.Empty;
                    return true;

                case "boolean":
                    value = false;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>その型のサンプル値を持つ項目から、その値へ。</summary>
        private static IDictionary<SchemaItem, object> Sampled(
            IDictionary<SchemaItem, string> sdkTypes, SampleValueTable samples)
        {
            Dictionary<SchemaItem, object> sampled = new Dictionary<SchemaItem, object>();
            if (sdkTypes == null || samples == null)
            {
                return sampled;
            }

            Dictionary<string, object> byType =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (SampleValueRow row in samples.Types)
            {
                byType[row.TypeName] = row.First;
            }

            foreach (KeyValuePair<SchemaItem, string> one in sdkTypes)
            {
                object value;
                if (byType.TryGetValue(one.Value, out value))
                {
                    sampled[one.Key] = value;
                }
            }

            return sampled;
        }

        /// <summary>対象を全件にする入力。位置で指す呼び分けだけが持つ。</summary>
        private static IEnumerable<SchemaItem> WholeInputs(SchemaBranch branch)
        {
            return branch.Inputs.Where(i => !i.Injected
                && (string.Equals(i.Name, WholeName, StringComparison.Ordinal)
                    || string.Equals(i.Name, ParentWholeName, StringComparison.Ordinal)));
        }

        /// <summary>
        /// 全件を指すだけで呼べる呼び分けか。対象を選ぶ必須の組が、どれも全件の指定で満たせる
        /// ものをいう——ハンドルや位置を要る呼び分けは、何を渡すかがここでは決まらない。
        /// </summary>
        private static bool Wholly(SchemaBranch branch)
        {
            ISet<string> whole = new HashSet<string>(
                WholeInputs(branch).Select(i => i.Name), StringComparer.Ordinal);
            if (whole.Count == 0 || branch.Inputs.Any(i => i.Required == true && !i.Injected
                && !whole.Contains(i.Name)))
            {
                return false;
            }

            return branch.Choices
                .Where(c => c.Required)
                .All(c => c.Names.Any(whole.Contains)
                    || c.Names.Any(n => string.Equals(n, ValueName, StringComparison.Ordinal)));
        }

        /// <summary>
        /// 数えて並べる呼び分け。件数と項目の並びを返す形を持ち、値を書き込む組を受け取らない
        /// ものがこれに当たる。
        /// </summary>
        private static SchemaBranch Listing(ToolSchema schema)
        {
            if (schema.Output == null
                || schema.Output.Members == null
                || !schema.Output.Members.Any(
                    m => string.Equals(m.Name, ItemsName, StringComparison.Ordinal)))
            {
                return null;
            }

            return schema.Branches.FirstOrDefault(
                b => Wholly(b)
                    && !b.Inputs.Any(i => string.Equals(i.Name, ValueName, StringComparison.Ordinal)
                        || string.Equals(i.Name, ValuesName, StringComparison.Ordinal)));
        }

        /// <summary>ハンドルの並びを受け取る入力。型役割がハンドルの型を指す並びである。</summary>
        private static IEnumerable<SchemaItem> HandleInputs(ToolSchema schema)
        {
            return schema.Branches
                .SelectMany(b => b.Inputs)
                .Where(i => !i.Injected
                    && i.Element != null
                    && string.Equals(i.Name, HandlesName, StringComparison.Ordinal));
        }

        private static IEnumerable<SchemaItem> LimitInputs(ToolSchema schema)
        {
            return schema.Branches
                .SelectMany(b => b.Inputs)
                .Where(i => !i.Injected
                    && string.Equals(i.Name, LimitName, StringComparison.Ordinal));
        }

        /// <summary>
        /// 並べたものを全件そのまま読む検査。項目を選ばずに呼ぶので、公開した項目のどれか1つでも
        /// 値として写せなければ落ちる。
        /// </summary>
        private static IEnumerable<E2eCase> ReadingCases(
            ToolMapRow row,
            ToolSchema schema,
            IDictionary<string, string> connectionPaths)
        {
            SchemaBranch listing = Listing(schema);
            if (listing == null)
            {
                yield break;
            }

            IDictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (SchemaItem whole in WholeInputs(listing))
            {
                arguments[whole.Name] = true;
            }

            string rowKey = row == null ? string.Empty : row.SignatureKey;
            yield return new E2eCase(
                rowKey,
                row == null ? string.Empty : ToolMapJsonReader.SpellingOf(row.EditKind),
                row == null ? string.Empty : ConnectionPath(rowKey, connectionPaths),
                schema.Tool,
                "並べたものを項目を選ばずに読めること",
                arguments,
                E2eExpectation.Success,
                null);
        }

        /// <summary>
        /// 位置で指す項目へ、どのリストにも無い位置を書く検査。位置から実体を解く経路と、その
        /// 範囲の検査が働いていなければ落ちる。
        /// </summary>
        private static IEnumerable<E2eCase> PositionCases(
            ToolMapRow row,
            ToolSchema schema,
            IDictionary<string, string> connectionPaths,
            IDictionary<SchemaItem, string> sdkTypes,
            ISet<string> positioned,
            ISet<string> dangerous)
        {
            string rowKey = row == null ? string.Empty : row.SignatureKey;
            if (sdkTypes == null || positioned == null || dangerous.Contains(rowKey))
            {
                yield break;
            }

            foreach (SchemaBranch branch in schema.Branches)
            {
                SchemaItem group = branch.Inputs.FirstOrDefault(
                    i => string.Equals(i.Name, ValueName, StringComparison.Ordinal));
                if (group == null || group.Members == null || !Wholly(branch))
                {
                    continue;
                }

                foreach (SchemaItem member in group.Members)
                {
                    string typeName;
                    if (!sdkTypes.TryGetValue(member, out typeName)
                        || !positioned.Contains(typeName))
                    {
                        continue;
                    }

                    IDictionary<string, object> arguments =
                        new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (SchemaItem whole in WholeInputs(branch))
                    {
                        arguments[whole.Name] = true;
                    }

                    if (branch.SelectorName != null)
                    {
                        arguments[branch.SelectorName] = branch.SelectorValue;
                    }

                    arguments[ValueName] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { member.Name, UnknownPosition },
                    };
                    yield return new E2eCase(
                        rowKey,
                        row == null ? string.Empty : ToolMapJsonReader.SpellingOf(row.EditKind),
                        row == null ? string.Empty : ConnectionPath(rowKey, connectionPaths),
                        schema.Tool,
                        "どのリストにも無い位置を指す書き込みを断ること",
                        arguments,
                        E2eExpectation.Refusal,
                        IndexOutOfRange);
                }
            }
        }

        /// <summary>その1件だけを渡す引数。確認を要する行では確認も渡す。</summary>
        private static IDictionary<string, object> Single(string name, object value, bool confirmed)
        {
            IDictionary<string, object> arguments = Arguments(confirmed);
            arguments[name] = value;

            return arguments;
        }

        private static IDictionary<string, object> Arguments(bool confirmed)
        {
            Dictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            if (confirmed)
            {
                arguments[ConfirmName] = true;
            }

            return arguments;
        }

        /// <summary>受け手の型へ至る接続の経路。辿り着けない型では空。</summary>
        private static string ConnectionPath(
            string rowKey, IDictionary<string, string> connectionPaths)
        {
            int open = rowKey.IndexOf('(');
            string head = open < 0 ? rowKey : rowKey.Substring(0, open);
            int dot = head.LastIndexOf('.');
            if (dot < 0)
            {
                return string.Empty;
            }

            string path;

            return connectionPaths.TryGetValue(head.Substring(0, dot), out path)
                ? path
                : string.Empty;
        }
    }
}
