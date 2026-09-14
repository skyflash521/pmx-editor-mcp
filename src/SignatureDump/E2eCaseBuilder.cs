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

        /// <summary>親と要素の組を受け取る入力の名前。</summary>
        private const string AssignmentsName = "assignments";

        /// <summary>組の中で親を位置で指す項目の名前。</summary>
        private const string ParentIndexName = "parentIndex";

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
        /// 位置で指す項目へ渡す、並びの先頭。段取りがどの並びへも要素を1つ入れるので、この位置は
        /// どの並びにも在る。
        /// </summary>
        private const int FirstPosition = 0;

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
            IDictionary<string, string> factories = null,
            IDictionary<string, string> readers = null,
            IDictionary<string, ISet<string>> unkept = null,
            ISet<string> handled = null,
            ISet<string> picking = null)
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

            ISet<string> reading = new HashSet<string>(
                readers == null ? new string[0] : readers.Values.ToArray(),
                StringComparer.Ordinal);
            IDictionary<string, IDictionary<string, object>> given =
                Given(samples, toolsByRow, schemas);
            IDictionary<string, SampleCallRow> refused = samples == null
                ? new Dictionary<string, SampleCallRow>(StringComparer.Ordinal)
                : samples.Calls
                    .Where(c => c.Refused != null)
                    .ToDictionary(c => c.SignatureKey, c => c, StringComparer.Ordinal);
            List<E2eCase> cases = new List<E2eCase>(SetupCases(schemas, factories));

            // 直に呼ぶと状態が動く行は、その動きが後の検査の見るものを変える——取り消しは段取りが
            // 作った要素を消し、再生の開始はビューを動かし続ける。順に並べる中では避けられないので、
            // 最後へ回して、あとに続く検査を持たせない。
            List<E2eCase> trailing = new List<E2eCase>();

            // 選ばれている対象を相手にする呼び出しは、対象を選ぶ呼び出しより後でなければ、
            // 選ぶものが無いことを尋ねる表示が出る。名前の順ではそれが先に来るので、最後へ回す。
            List<E2eCase> picked = new List<E2eCase>();
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                ToolMapRow row;
                byTool.TryGetValue(schema.Tool, out row);
                List<E2eCase> held = cases;
                if (row != null && picking != null && picking.Contains(row.SignatureKey))
                {
                    held = picked;
                }
                else if (row != null && row.EditKind != ToolMapEditKind.Read)
                {
                    held = trailing;
                }
                held.AddRange(Cases(
                    row,
                    schema,
                    connectionPaths,
                    dangerous,
                    sdkShapes,
                    Sampled(sdkTypes, samples),
                    given,
                    refused,
                    Handles(schema, sdkTypes, handled)));
                cases.AddRange(ImageCases(row, schema, connectionPaths, viewImages));
                cases.AddRange(ReadingCases(row, schema, connectionPaths, reading));
                cases.AddRange(PositionCases(
                    row, schema, schemas, connectionPaths, sdkTypes, positioned, dangerous,
                    readers, unkept));
            }

            cases.AddRange(trailing);
            cases.AddRange(picked);

            return cases;
        }

        /// <summary>
        /// 先に流す段取り。要素を1つ作って並びへ加える。中身の無い並びでは、項目を読む検査が
        /// 一度も項目を読まないまま通ってしまう。親の並びへ入れる要素を先に流す——親の並びが
        /// 空のままでは、その中の並びへ入れる先を指せない。
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
            IDictionary<string, ToolSchema> byTool = schemas.Tools.ToDictionary(
                t => t.Tool, t => t, StringComparer.Ordinal);
            KeyValuePair<string, string>[] adding = factories
                .Where(f => named.Contains(f.Value) && byTool.ContainsKey(f.Key))
                .OrderBy(f => f.Key, StringComparer.Ordinal)
                .ToArray();
            foreach (KeyValuePair<string, string> pair in adding
                .Where(f => Handed(byTool[f.Key]))
                .Concat(adding.Where(f => !Handed(byTool[f.Key]) && Assigned(byTool[f.Key]))))
            {
                bool handed = Handed(byTool[pair.Key]);
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
                    handed
                        ? "作った要素を並びへ加えられること"
                        : "作った要素を親の並びの先頭へ加えられること",
                    handed ? Empty() : IntoFirstParent(),
                    E2eExpectation.Success,
                    null,
                    null,
                    null,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        {
                            handed ? HandlesName : AssignmentsName + "/0/" + HandlesName,
                            pair.Key
                        },
                    });
            }
        }

        private static IDictionary<string, object> Empty()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        /// <summary>親の並びの先頭へ、借りたハンドルを入れる組。ハンドルは借りる側が埋める。</summary>
        private static IDictionary<string, object> IntoFirstParent()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                {
                    AssignmentsName,
                    new object[]
                    {
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { ParentIndexName, FirstPosition },
                        },
                    }
                },
            };
        }

        /// <summary>作った要素をハンドルで渡すだけで呼べるツールか。</summary>
        private static bool Handed(ToolSchema schema)
        {
            return schema.Branches.Any(b => b.Inputs.All(
                i => i.Injected
                    || string.Equals(i.Name, HandlesName, StringComparison.Ordinal)));
        }

        /// <summary>
        /// 作った要素を、親と要素の組で渡して呼べるツールか。組が親を位置で指すものだけを採る
        /// ——ハンドルで親を指す組では、どの親を指すかがここでは決まらない。
        /// </summary>
        private static bool Assigned(ToolSchema schema)
        {
            return schema.Branches.Any(b => b.Inputs.All(i => i.Injected
                    || string.Equals(i.Name, AssignmentsName, StringComparison.Ordinal))
                && b.Inputs.Any(i => string.Equals(i.Name, AssignmentsName, StringComparison.Ordinal)
                    && i.Element != null
                    && i.Element.Members != null
                    && i.Element.Members.Any(m => string.Equals(
                        m.Name, ParentIndexName, StringComparison.Ordinal))
                    && i.Element.Members.Any(m => string.Equals(
                        m.Name, HandlesName, StringComparison.Ordinal))));
        }

        /// <summary>
        /// ビューの画像を返すツールの検査。写し取れるのは窓を持つビューだけなので、その1枚を相手に
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
                "返す画像が写し取ったビューの姿と合うこと",
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
            IDictionary<SchemaItem, object> sampled,
            IDictionary<string, IDictionary<string, object>> given,
            IDictionary<string, SampleCallRow> refused,
            IEnumerable<SchemaItem> handed)
        {
            // 行から導く名前を持たないツールは、行の値も接続の経路も持たない。
            string rowKey = row == null ? string.Empty : row.SignatureKey;
            string tool = schema.Tool;
            string path = row == null ? string.Empty : ConnectionPath(rowKey, connectionPaths);
            string editKind = row == null ? string.Empty : ToolMapJsonReader.SpellingOf(row.EditKind);
            bool confirmed = row != null && dangerous.Contains(rowKey);

            // 呼び先が在るだけでは、その行の振る舞いを一度も確かめない。確認を要さず、渡すものが
            // 決まる行は実際に呼ぶ。実際に呼ぶなら、呼び先が在ることはその呼び出しで分かるので、
            // 別に確かめない——同じ呼び出しを二度することになる。
            IDictionary<string, object> calling =
                new Dictionary<string, object>(StringComparer.Ordinal);
            bool calls = row != null && !confirmed
                && TryCalling(row, schema, sdkShapes, sampled, given, out calling);
            if (!calls)
            {
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    "未知のメソッドとして断られないこと",
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    E2eExpectation.Dispatched,
                    null);
            }

            SampleCallRow denied;
            bool denies = row != null && refused.TryGetValue(rowKey, out denied);
            if (calls)
            {
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    denies
                        ? "成り立たない値を渡す呼び出しを、その理由で断ること"
                        : row.EditKind == ToolMapEditKind.Read
                            ? "呼び出して値を返せること"
                            : "呼び出して成功すること",
                    calling,
                    denies ? E2eExpectation.Denied : E2eExpectation.Called,
                    denies ? refused[rowKey].Refused : null,
                    null,
                    null,
                    null,
                    null,
                    denies ? refused[rowKey].Says : null);
            }

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

            foreach (SchemaItem one in handed)
            {
                IDictionary<string, object> arguments =
                    Single(one.Name, UnknownHandle, confirmed);
                SchemaBranch holding = schema.Branches.First(
                    b => b.Inputs.Any(i => ReferenceEquals(i, one)));
                if (!TryFill(holding, sdkShapes, sampled, arguments))
                {
                    continue;
                }

                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    "台帳に無いハンドルを1つ渡す呼び出しを断ること",
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
        /// 渡す値がその呼び分けに収まるか。受け取らない項目を含まず、必ず要る組をどれも欠かさない
        /// ことをいう——どちらかが外れていると、呼び先まで届く前に断られる。
        /// </summary>
        private static bool Fits(SchemaBranch branch, IDictionary<string, object> arguments)
        {
            IDictionary<string, SchemaItem> taken = branch.Inputs
                .Where(i => !i.Injected)
                .GroupBy(i => i.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            return arguments.Keys.All(taken.ContainsKey)
                && branch.Inputs
                    .Where(i => i.Required == true && !i.Injected)
                    .All(i => arguments.ContainsKey(i.Name))
                && branch.Choices.All(c => !c.Required
                    || c.Names.Count(arguments.ContainsKey) == 1)
                && arguments.All(a => Shaped(taken[a.Key], a.Value));
        }

        /// <summary>
        /// その値が、項目の受け取る形をしているか。並びは要素まで、組は項目まで見る——外側だけを
        /// 見ると、中身の形が違う値も通ってしまう。綴りを持たない項目はSDKに由来する値なので、
        /// ここでは形を決められず真とする。
        /// </summary>
        private static bool Shaped(SchemaItem item, object value)
        {
            if (item.Element != null)
            {
                object[] items = value as object[];

                return items != null && items.All(one => Shaped(item.Element, one));
            }

            if (item.Members != null)
            {
                IDictionary<string, object> members = value as IDictionary<string, object>;
                if (members == null)
                {
                    return false;
                }

                IDictionary<string, SchemaItem> taken = item.Members
                    .GroupBy(m => m.Name, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

                return members.Keys.All(taken.ContainsKey)
                    && item.Members.Where(m => m.Required == true).All(
                        m => members.ContainsKey(m.Name))
                    && members.All(m => Shaped(taken[m.Key], m.Value));
            }

            if (item.Shape == null)
            {
                return true;
            }

            switch (item.Shape)
            {
                case "text":
                    return value is string;

                case "boolean":
                    return value is bool;

                case "number":
                    return value is int || value is double || value is float || value is long;

                default:
                    return true;
            }
        }

        /// <summary>
        /// その行を実際に呼ぶときの引数。読み取りの行は最小の値で埋めて呼べる——何を渡しても
        /// エディタは動かないので、値が意味を成さなくても呼び先までは届く。状態を動かす行は
        /// 引数を渡さずに呼べるものだけを呼ぶ——最小の値は、在りもしないファイルや範囲の外の
        /// 位置になり、確かめたい振る舞いではなくその断りを見ることになる。
        /// </summary>
        private static bool TryCalling(
            ToolMapRow row,
            ToolSchema schema,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            IDictionary<string, IDictionary<string, object>> given,
            out IDictionary<string, object> arguments)
        {
            if (row.EditKind == ToolMapEditKind.Read)
            {
                return TryReading(schema, sdkShapes, sampled, out arguments);
            }

            if (given != null && given.TryGetValue(row.SignatureKey, out arguments))
            {
                return true;
            }

            arguments = new Dictionary<string, object>(StringComparer.Ordinal);

            return Unchosen(schema) != null;
        }

        /// <summary>
        /// 行ごとに渡すと決めた値。書かれた行がその名前のツールを持ち、渡す項目がそのツールの
        /// 受け取る入力であることをここで確かめる——書いた値が届かないまま検査が増えたように
        /// 見えるのを防ぐ。
        /// </summary>
        private static IDictionary<string, IDictionary<string, object>> Given(
            SampleValueTable samples,
            IDictionary<string, string> toolsByRow,
            ToolSchemaTable schemas)
        {
            Dictionary<string, IDictionary<string, object>> given =
                new Dictionary<string, IDictionary<string, object>>(StringComparer.Ordinal);
            if (samples == null)
            {
                return given;
            }

            IDictionary<string, ToolSchema> byName = schemas.Tools.ToDictionary(
                t => t.Tool, t => t, StringComparer.Ordinal);
            foreach (SampleCallRow call in samples.Calls)
            {
                string tool;
                if (!toolsByRow.TryGetValue(call.SignatureKey, out tool))
                {
                    throw new InvalidOperationException(
                        "渡す値を書いた行が、自分の名前のツールを持っていない: " + call.SignatureKey);
                }

                if (!byName[tool].Branches.Any(b => Fits(b, call.Arguments)))
                {
                    throw new InvalidOperationException(
                        "渡す値が、どの呼び分けにも収まらない: " + tool + "("
                            + string.Join("・", call.Arguments.Keys.OrderBy(
                                n => n, StringComparer.Ordinal).ToArray()) + ")");
                }

                given[call.SignatureKey] = call.Arguments;
            }

            return given;
        }

        /// <summary>
        /// 読み取りの行を実際に呼ぶときの引数。対象を選ばずに呼べるなら空でよく、要る組を持つ
        /// 呼び分けが1つだけなら最小の値で埋める。対象を指す組が要る呼び分けは偽——何を指すかが
        /// ここでは決まらない。呼び分けが2つ以上あるときも偽で、どれを選ぶかが決まらない。
        /// </summary>
        private static bool TryReading(
            ToolSchema schema,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            out IDictionary<string, object> arguments)
        {
            arguments = new Dictionary<string, object>(StringComparer.Ordinal);
            if (Unchosen(schema) != null)
            {
                return true;
            }

            if (schema.Branches.Count != 1 || Pointed(schema.Branches[0]))
            {
                return false;
            }

            return TryFill(schema, sdkShapes, sampled, arguments);
        }

        /// <summary>その呼び分けが、対象を指す組を必ず要るか。</summary>
        private static bool Pointed(SchemaBranch branch)
        {
            return branch.Inputs.Any(i => i.Required == true
                    && !i.Injected
                    && PointingNames.Contains(i.Name, StringComparer.Ordinal))
                || branch.Choices.Any(c => c.Required
                    && c.Names.Any(n => PointingNames.Contains(n, StringComparer.Ordinal)));
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
                if (!TryFill(branch, sdkShapes, sampled, arguments))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>その呼び分け1つで、必ず要る組を最小の値で埋める。</summary>
        private static bool TryFill(
            SchemaBranch branch,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            IDictionary<string, object> arguments)
        {
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
        /// 対象を選ばずに呼べる呼び分け。要る組をどれも持たないものがこれに当たる——ハンドルで
        /// 対象を指す呼び分けは、何を渡すかがここでは決まらない。
        /// </summary>
        private static SchemaBranch Unchosen(ToolSchema schema)
        {
            return schema.Branches.FirstOrDefault(
                b => !b.Inputs.Any(i => i.Required == true && !i.Injected)
                    && !b.Choices.Any(c => c.Required));
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

        /// <summary>
        /// ハンドルを1つだけ受け取る入力。ハンドルで指す型を値に取る項目がこれに当たり、並びで
        /// 受け取る入力とは別に、1つだけ渡す形でも断ることを確かめる。
        /// </summary>
        private static IEnumerable<SchemaItem> Handles(
            ToolSchema schema,
            IDictionary<SchemaItem, string> sdkTypes,
            ISet<string> handled)
        {
            if (sdkTypes == null || handled == null)
            {
                return new SchemaItem[0];
            }

            return schema.Branches
                .SelectMany(b => b.Inputs)
                .Where(i => i.Required == true
                    && !i.Injected
                    && i.Element == null
                    && Handled(i, sdkTypes, handled))
                .GroupBy(i => i.Name, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToArray();
        }

        private static bool Handled(
            SchemaItem item, IDictionary<SchemaItem, string> sdkTypes, ISet<string> handled)
        {
            string typeName;

            return sdkTypes.TryGetValue(item, out typeName) && handled.Contains(typeName);
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
        /// その型の項目を集めて読むツールを、項目を選ばずに呼ぶ検査。公開した項目のどれか1つでも
        /// 値として写せなければ落ちる。要素を並べる型は全件を、自分1つを指す型はそれ自身を読む。
        /// </summary>
        private static IEnumerable<E2eCase> ReadingCases(
            ToolMapRow row,
            ToolSchema schema,
            IDictionary<string, string> connectionPaths,
            ISet<string> reading)
        {
            SchemaBranch listing = Listing(schema);
            if (!reading.Contains(schema.Tool)
                || (listing == null && Unchosen(schema) == null))
            {
                yield break;
            }

            IDictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (SchemaItem whole in listing == null
                ? new SchemaItem[0]
                : WholeInputs(listing).ToArray())
            {
                arguments[whole.Name] = true;
            }

            string rowKey = row == null ? string.Empty : row.SignatureKey;
            yield return new E2eCase(
                rowKey,
                row == null ? string.Empty : ToolMapJsonReader.SpellingOf(row.EditKind),
                row == null ? string.Empty : ConnectionPath(rowKey, connectionPaths),
                schema.Tool,
                listing == null
                    ? "その型の項目を選ばずに読めること"
                    : "並べたものを項目を選ばずに読めること",
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
            ToolSchemaTable schemas,
            IDictionary<string, string> connectionPaths,
            IDictionary<SchemaItem, string> sdkTypes,
            ISet<string> positioned,
            ISet<string> dangerous,
            IDictionary<string, string> readers,
            IDictionary<string, ISet<string>> unkept)
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

                    string editKind = row == null
                        ? string.Empty
                        : ToolMapJsonReader.SpellingOf(row.EditKind);
                    string path = row == null
                        ? string.Empty
                        : ConnectionPath(rowKey, connectionPaths);
                    yield return new E2eCase(
                        rowKey,
                        editKind,
                        path,
                        schema.Tool,
                        "どのリストにも無い位置を指す書き込みを断ること",
                        Pointing(arguments, member.Name, UnknownPosition),
                        E2eExpectation.Refusal,
                        IndexOutOfRange);

                    yield return new E2eCase(
                        rowKey,
                        editKind,
                        path,
                        schema.Tool,
                        "位置で指す項目へ関連が無いことを書けること",
                        Pointing(arguments, member.Name, null),
                        E2eExpectation.Success,
                        null);
                    yield return new E2eCase(
                        rowKey,
                        editKind,
                        path,
                        schema.Tool,
                        "位置で指す項目へ並びの先頭を書けること",
                        Pointing(arguments, member.Name, FirstPosition),
                        E2eExpectation.Success,
                        null);

                    E2eCase read = Kept(unkept, schema.Tool, member.Name)
                        ? ReadBackCase(
                            rowKey, editKind, path, schema.Tool, schemas, readers, member.Name)
                        : null;
                    if (read != null)
                    {
                        yield return read;
                    }
                }
            }
        }

        /// <summary>
        /// その項目へ書いた値を、モデルが持ち続けるか。持ち続けない項目は読み返して確かめられない
        /// ——どれがそれに当たるかは呼び先の型からは決まらないので、共通契約の正本が名指しする。
        /// </summary>
        private static bool Kept(
            IDictionary<string, ISet<string>> unkept, string tool, string member)
        {
            ISet<string> members;

            return unkept == null
                || !unkept.TryGetValue(tool, out members)
                || !members.Contains(member);
        }

        /// <summary>対象を指す組はそのままに、位置で指す項目へその値を渡す引数。</summary>
        private static IDictionary<string, object> Pointing(
            IDictionary<string, object> pointing, string member, object value)
        {
            IDictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> one in pointing)
            {
                arguments[one.Key] = one.Value;
            }

            arguments[ValueName] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { member, value },
            };

            return arguments;
        }

        /// <summary>
        /// 書いた値を読み返す検査。書き換えるツールと同じ型を読むツールへ、全件を指して問う。
        /// 読む相手が決まらないツールでは null——読み返せないことは、書けたことを疑う理由に
        /// ならない。
        /// 読み返す相手に並びの先頭を選ぶのは、関連が無いことを書けても持ち続けられない項目が
        /// あるからである——頂点の第1ウェイトのボーンは、モデルを整えるときに先頭のボーンへ
        /// 戻る。
        /// </summary>
        private static E2eCase ReadBackCase(
            string rowKey,
            string editKind,
            string path,
            string tool,
            ToolSchemaTable schemas,
            IDictionary<string, string> readers,
            string member)
        {
            string reader;
            if (readers == null || !readers.TryGetValue(tool, out reader))
            {
                return null;
            }

            ToolSchema found = schemas.Tools.FirstOrDefault(
                s => string.Equals(s.Tool, reader, StringComparison.Ordinal));
            SchemaBranch listing = found == null ? null : Listing(found);
            if (listing == null)
            {
                return null;
            }

            IDictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (SchemaItem whole in WholeInputs(listing))
            {
                arguments[whole.Name] = true;
            }

            return new E2eCase(
                rowKey,
                editKind,
                path,
                reader,
                "書いた位置を読み返せること",
                arguments,
                E2eExpectation.Reads,
                null,
                null,
                null,
                null,
                new E2eExpectedMember(member, FirstPosition));
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
