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

        /// <summary>一覧が何件返すかを受け取る入力の名前。</summary>
        public const string LimitName = "limit";

        /// <summary>ハンドルの並びを受け取る入力の名前。</summary>
        public const string HandlesName = "handles";

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
        /// する——どの綴りがどの断り方を指すかは仕様書の説明文にしか無く、機械では導けない。
        /// 名指しが実装とずれていれば、その検査が実機で落ちて分かる。
        /// </summary>
        public static IList<E2eCase> Build(
            ToolMap map,
            ToolSchemaTable schemas,
            IDictionary<string, string> toolsByRow,
            IDictionary<string, string> connectionPaths,
            ISet<string> dangerous,
            IDictionary<SchemaItem, string> sdkShapes)
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

            List<E2eCase> cases = new List<E2eCase>();
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                ToolMapRow row;
                byTool.TryGetValue(schema.Tool, out row);
                cases.AddRange(Cases(row, schema, connectionPaths, dangerous, sdkShapes));
            }

            return cases;
        }

        private static IEnumerable<E2eCase> Cases(
            ToolMapRow row,
            ToolSchema schema,
            IDictionary<string, string> connectionPaths,
            ISet<string> dangerous,
            IDictionary<SchemaItem, string> sdkShapes)
        {
            // 行から導く名前を持たないツールは、行の値も接続の経路も持たない。
            string rowKey = row == null ? string.Empty : row.SignatureKey;
            string tool = schema.Tool;
            string path = row == null ? string.Empty : ConnectionPath(rowKey, connectionPaths);
            string editKind = row == null ? string.Empty : ToolMapJsonReader.SpellingOf(row.EditKind);
            bool confirmed = row != null && dangerous.Contains(rowKey);

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
                if (!TryFill(schema, sdkShapes, arguments))
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
                    if (item == null || !TryMinimal(item, sdkShapes, out value))
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
                    if (value == null && !TryMinimal(item, sdkShapes, out value))
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
            SchemaItem item, IDictionary<SchemaItem, string> sdkShapes, out object value)
        {
            value = null;
            if (item.Members != null)
            {
                Dictionary<string, object> members =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (SchemaItem member in item.Members.Where(m => m.Required == true))
                {
                    object one;
                    if (!TryMinimal(member, sdkShapes, out one))
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
                if (!TryMinimal(item.Element, sdkShapes, out one))
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
