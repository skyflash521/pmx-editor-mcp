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
            ISet<string> dangerous)
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
                cases.AddRange(Cases(row, schema, connectionPaths, dangerous));
            }

            return cases;
        }

        private static IEnumerable<E2eCase> Cases(
            ToolMapRow row,
            ToolSchema schema,
            IDictionary<string, string> connectionPaths,
            ISet<string> dangerous)
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
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    "台帳に無いハンドルを渡す呼び出しを断ること",
                    Single(handles.Name, new object[] { UnknownHandle }, confirmed),
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
