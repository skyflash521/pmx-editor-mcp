using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>並びの全体を相手にすると述べる入力の名前。</summary>
        public const string AllName = "all";

        /// <summary>ハンドルの並びを受け取る入力の名前。</summary>
        public const string HandlesName = "handles";

        /// <summary>ツールの名前の中で、担当群と残りを分ける文字。</summary>
        private const char Separator = '_';

        /// <summary>受け手を1つのハンドルで指す入力の名前。</summary>
        private const string AimName = "pmxHandle";

        /// <summary>書き先を受け取る入力の名前。</summary>
        private const string PathName = "path";

        /// <summary>相手を位置の並びで指す入力の名前。</summary>
        private const string IndicesName = "indices";

        /// <summary>親を位置の並びで指す入力の名前。</summary>
        private const string ParentIndicesName = "parentIndices";

        /// <summary>ハンドルを台帳から外すツールの名前。共通契約が名前を定める。</summary>
        private const string ReleaseToolName = "session_release_handle";

        /// <summary>いま開いているモデルを空へ戻すツールの名前。共通契約が名前を定める。</summary>
        private const string InitializeToolName = "session_initialize_pmx";

        private const string OpenWindowToolName = "editor_open_window";

        /// <summary>
        /// いま開いているモデルをPMDで書き出すツールの名前。共通契約が名前を定める。読み込める
        /// モデルを作れるのはエディタだけなので、読み込む中身が要るツールはこれが書いたものを読む。
        /// </summary>
        public const string SavingPmdToolName = "session_save_pmd_file";

        /// <summary>いま開いているモデルをPMXで書き出すツールの名前。共通契約が名前を定める。</summary>
        public const string SavingPmxToolName = "session_save_pmx_file";

        /// <summary>段取りが書き出すPMDの位置。実行器が持つ一時の置き場を名前で指す。</summary>
        public const string SavedPmdPath = "%PMX_EDITOR_MCP_E2E_TEMP%\\読み込み元.pmd";

        /// <summary>段取りが書き出すPMXの位置。実行器が持つ一時の置き場を名前で指す。</summary>
        public const string SavedPmxPath = "%PMX_EDITOR_MCP_E2E_TEMP%\\読み込み元.pmx";

        /// <summary>いまの表示の設定を書き出すツールの名前。共通契約が名前を定める。</summary>
        public const string SavingViewSettingToolName = "view_save_view_setting";

        /// <summary>段取りが書き出す表示の設定の位置。実行器が持つ一時の置き場を名前で指す。</summary>
        public const string SavedViewSettingPath =
            "%PMX_EDITOR_MCP_E2E_TEMP%\\読み込み元.xml";

        /// <summary>
        /// 覚えておく名前を行ごとに分ける区切り。読み比べる段は行ごとに同じツールを2度呼ぶので、
        /// 名前を分けないと別の行の覚えた値を借りる。
        /// </summary>
        private const string Scoped = "#";

        /// <summary>借りる値を差し込む先を、引数の中の道で指すときの区切り。</summary>
        private const string PathStep = "/";

        /// <summary>
        /// 呼ぶ前と後で読むときに受け取る件数。一覧は総数も返すので、1件だけ読めば総数の変化は
        /// 見える——全件を読むと、要素の多いモデルでは読むだけで時間の上限に届く。
        /// </summary>
        private const int ReadbackLimit = 1;

        /// <summary>台帳に無いハンドルを並びで渡す検査が確かめること。</summary>
        private const string ListedHandleRefusal = "台帳に無いハンドルを渡す呼び出しを断ること";

        /// <summary>台帳に無いハンドルを1つだけ渡す検査が確かめること。</summary>
        private const string LoneHandleRefusal = "台帳に無いハンドルを1つ渡す呼び出しを断ること";

        /// <summary>件数に0を渡す検査が確かめること。</summary>
        private const string CountRefusal = "件数に0を渡す呼び出しを断ること";

        /// <summary>確認を渡さない検査が確かめること。</summary>
        private const string ConfirmRefusal = "確認を渡さない呼び出しを断ること";

        /// <summary>
        /// 共通の入口が断ることを確かめる検査。どれもツールごとに違う振る舞いを見ておらず、同じ
        /// 経路を繰り返し通すだけなので、代表だけを実機へ投げる。断る経路そのものはホストの単体
        /// テストが固定している。並びは入口をどこまで進むかの順で、深いものが先に来る——1件しか
        /// 残せないツールには、深くまで進むものを残す。
        /// </summary>
        private static readonly string[] SharedRefusals =
        {
            ListedHandleRefusal, LoneHandleRefusal, CountRefusal, ConfirmRefusal,
        };

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

        /// <summary>返す項目を選ぶ入力の名前。</summary>
        private const string FieldsName = "fields";

        /// <summary>名前を載せる応答の項目の名前。</summary>
        private const string NameName = "name";

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
        /// そのツールが動かせる、いちばん手前の位置。並びの先頭に固定で並ぶ要素は取り除くことも
        /// 動かすこともできないので、その件数だけ後ろを指す。
        /// </summary>
        private static int Movable(string tool, IDictionary<string, int> fixedLeading)
        {
            int ahead;

            return fixedLeading != null && tool != null && fixedLeading.TryGetValue(tool, out ahead)
                ? FirstPosition + ahead
                : FirstPosition;
        }

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
            ISet<string> picking = null,
            IDictionary<string, IList<string>> makers = null,
            IDictionary<string, string> adders = null,
            IDictionary<string, string> removers = null,
            ISet<string> aimed = null,
            IDictionary<string, IList<string>> typeMakers = null,
            IDictionary<string, string> parents = null,
            IDictionary<string, string> addersByTool = null,
            IDictionary<string, string> addersByType = null,
            IDictionary<string, string> updaters = null,
            IDictionary<string, ISet<string>> targeted = null,
            IDictionary<string, ParentValues> parentValues = null,
            IDictionary<string, int> fixedLeading = null)
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

            IDictionary<SchemaItem, string> handleTargets = HandleTargets(sdkTypes, handled);

            // 母集団はスキーマ正本が持つツールである。行から導く名前を持たない共通契約のツールも
            // 検査の相手なので、行の側を母集団にすると落ちる。
            Dictionary<string, ToolMapRow> byTool = new Dictionary<string, ToolMapRow>(
                StringComparer.Ordinal);
            Dictionary<string, IList<SetupOperation>> stepSetups =
                new Dictionary<string, IList<SetupOperation>>(StringComparer.Ordinal);
            Dictionary<string, ToolMapRow> rows = map.Rows.ToDictionary(
                r => r.SignatureKey, r => r, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> named in toolsByRow)
            {
                ToolMapRow row;
                if (!rows.TryGetValue(named.Key, out row))
                {
                    continue;
                }

                byTool[named.Value] = row;
                if (row.Setup == null)
                {
                    continue;
                }

                // 同じツールへ写る行が2つ以上、呼ぶ前の段取りを持つと、道の段として呼ぶときに
                // どちらが流れるかが決まらない。決まらないまま片方だけを直すと、直したほうが
                // 黙って効かないままになる。
                if (stepSetups.ContainsKey(named.Value))
                {
                    throw new InvalidOperationException(
                        "同じツールへ写る行が2つ以上、呼ぶ前の段取りを持つ: " + named.Value);
                }

                stepSetups[named.Value] = row.Setup;
            }

            foreach (KeyValuePair<string, IList<SetupOperation>> named in map.ToolSetups)
            {
                if (stepSetups.ContainsKey(named.Key))
                {
                    throw new InvalidOperationException(
                        "ツールの名前で置いた段取りが、行の段取りと重なっている: " + named.Key);
                }

                if (Of(schemas, named.Key) == null)
                {
                    throw new InvalidOperationException(
                        "段取りを置いたツールが、スキーマ正本に無い: " + named.Key);
                }

                stepSetups[named.Key] = named.Value;
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
            ElementWiring wiring = new ElementWiring(
                factories, parents, addersByType, updaters,
                Aiming(schemas, sdkTypes, positioned, updaters, targeted),
                parentValues);
            List<E2eCase> cases = new List<E2eCase>(PreparingCases(schemas));
            cases.AddRange(SetupCases(schemas, factories, wiring));

            // 直に呼ぶと状態が動く行は、その動きが後の検査の見るものを変える——取り消しは段取りが
            // 作った要素を消し、再生の開始はビューを動かし続ける。順に並べる中では避けられないので、
            // 最後へ回して、あとに続く検査を持たせない。事後条件を実機で確かめる行だけは回さない
            // ——回すと、確かめる相手が既に使えなくなっている。
            List<E2eCase> trailing = new List<E2eCase>();

            // 選ばれている対象を相手にする呼び出しは、対象を選ぶ呼び出しより後でなければ、
            // 選ぶものが無いことを尋ねる表示が出る。名前の順ではそれが先に来るので、最後へ回す。
            List<E2eCase> picked = new List<E2eCase>();
            foreach (ToolSchema schema in schemas.Tools.OrderBy(t => t.Tool, StringComparer.Ordinal))
            {
                ToolMapRow row;
                byTool.TryGetValue(schema.Tool, out row);
                List<E2eCase> held = cases;
                if (row == null && removers != null && removers.ContainsKey(schema.Tool))
                {
                    held = trailing;
                }
                else if (row != null && picking != null && picking.Contains(row.SignatureKey))
                {
                    held = picked;
                }
                else if (row != null && !Verified(row)
                    && row.EditKind != ToolMapEditKind.Read)
                {
                    held = trailing;
                }
                List<E2eCase> group = Cases(
                    row,
                    schema,
                    schemas,
                    connectionPaths,
                    dangerous,
                    sdkShapes,
                    Sampled(sdkTypes, samples),
                    handleTargets,
                    given,
                    refused,
                    Handles(schema, sdkTypes, handled),
                    Maker(schema.Tool, makers),
                    adders,
                    factories,
                    reading,
                    removers,
                    aimed,
                    readers,
                    unkept,
                    sdkTypes,
                    positioned,
                    typeMakers,
                    parents,
                    stepSetups,
                    wiring,
                    fixedLeading).ToList();
                held.AddRange(
                    group.Any(c => string.Equals(c.Tool, OpenWindowToolName, StringComparison.Ordinal))
                        ? group.Select(c => c.RunAfterViews())
                        : group);
                cases.AddRange(ImageCases(row, schema, connectionPaths, viewImages));
                cases.AddRange(ReadingCases(row, schema, connectionPaths, reading));
                cases.AddRange(PositionCases(
                    row, schema, schemas, connectionPaths, sdkTypes, positioned, dangerous,
                    readers, unkept, factories, parents, addersByTool, addersByType, wiring));
            }

            cases.AddRange(trailing);
            cases.AddRange(picked);

            return Shrunk(cases);
        }

        /// <summary>
        /// 共通の入口が断ることを確かめる検査を代表へ縮めた並び。残すのは、種別ごとに並びの順で
        /// 最初に当たった1件だけである。同じ入口へ同じ断り方をもう一度通しても、分かることは
        /// 増えない。
        /// </summary>
        private static IList<E2eCase> Shrunk(IList<E2eCase> cases)
        {
            ISet<int> kept = new HashSet<int>();
            ISet<string> shown = new HashSet<string>(StringComparer.Ordinal);
            for (int at = 0; at < cases.Count; at++)
            {
                int kind = Shared(cases[at]);
                if (kind >= 0 && shown.Add(SharedRefusals[kind]))
                {
                    kept.Add(at);
                }
            }

            return cases
                .Where((one, at) => Shared(one) < 0 || kept.Contains(at))
                .ToList();
        }

        /// <summary>
        /// その検査が確かめるのが共通の入口の断りなら、その種別の位置。ほかの検査では負。
        /// </summary>
        private static int Shared(E2eCase one)
        {
            return one.Expectation != E2eExpectation.Refusal
                ? -1
                : Array.IndexOf(SharedRefusals, one.Purpose);
        }

        /// <summary>
        /// どの検査よりも先に流す段取り。読み込む中身が要るツールのために、いま開いているものを
        /// 一時の置き場へ書き出す。読み込めるものを作れるのはエディタだけなので、検査の中で作る。
        /// </summary>
        private static IEnumerable<E2eCase> PreparingCases(ToolSchemaTable schemas)
        {
            string[][] saving =
            {
                new[] { SavingPmdToolName, SavedPmdPath },
                new[] { SavingPmxToolName, SavedPmxPath },
                new[] { SavingViewSettingToolName, SavedViewSettingPath },
            };
            foreach (string[] one in saving.Where(o => Of(schemas, o[0]) != null))
            {
                yield return new E2eCase(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    one[0],
                    "読み込む元を書き出せること",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { PathName, one[1] },
                        { ConfirmName, true },
                    },
                    E2eExpectation.Success,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    PathName);
            }
        }

        /// <summary>
        /// 先に流す段取り。要素を1つ作って並びへ加える。中身の無い並びでは、項目を読む検査が
        /// 一度も項目を読まないまま通ってしまう。親の並びへ入れる要素を先に流す——親の並びが
        /// 空のままでは、その中の並びへ入れる先を指せない。
        /// </summary>
        private static IEnumerable<E2eCase> SetupCases(
            ToolSchemaTable schemas,
            IDictionary<string, string> factories,
            ElementWiring wiring)
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
                foreach (E2eCase one in Filling(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    schemas,
                    pair.Value,
                    pair.Key,
                    pair.Key,
                    "並びへ加える要素を1つ作れること",
                    Handed(byTool[pair.Key])
                        ? "作った要素を並びへ加えられること"
                        : "作った要素を親の並びの先頭へ加えられること",
                    wiring))
                {
                    yield return one;
                }
            }
        }

        /// <summary>借りたハンドルを1つ入れる空き。借りる側がこの位置を埋める。</summary>
        private static object[] Slot()
        {
            return new object[] { null };
        }

        /// <summary>その空きの中の、借りた値を置く位置までの道。</summary>
        private static string Leaf(string path)
        {
            return path + "/0";
        }

        /// <summary>
        /// 覚えた応答から、ハンドル1つを指す道。応答を並びで返すツールは、出たハンドルもその並びの
        /// 中へ入れるので、借りるのはその先頭である。並びを重ねて返すツールでは、重ねた数だけ下りる。
        /// </summary>
        private static string Borrowed(string name, ToolSchema making)
        {
            if (making == null || making.Output == null)
            {
                return name;
            }

            string path = name;
            for (SchemaItem at = making.Output.Element; at != null; at = at.Element)
            {
                path = Leaf(path);
            }

            return path;
        }

        /// <summary>その名前のツールの入出力の形。持たない名前では null。</summary>
        private static ToolSchema Of(ToolSchemaTable schemas, string tool)
        {
            return schemas.Tools.FirstOrDefault(
                t => string.Equals(t.Tool, tool, StringComparison.Ordinal));
        }

        /// <summary>ハンドルの並びだけを渡す引数。ハンドルは借りる側が埋める。</summary>
        private static IDictionary<string, object> Lent()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { HandlesName, Slot() },
            };
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
                            { HandlesName, Slot() },
                        },
                    }
                },
            };
        }

        /// <summary>
        /// 借りて渡す引数へ、正本が書いた値と確認を上書きで足したもの。正本が値を書いている
        /// 危険な行は、その値を渡して初めて呼べる——型ごとのサンプルで埋めた値では、書いた側が
        /// 選んだ渡し先にならない。
        /// </summary>
        private static IDictionary<string, object> Written(
            IDictionary<string, object> arguments,
            string rowKey,
            IDictionary<string, IDictionary<string, object>> given,
            bool confirmed)
        {
            IDictionary<string, object> named;
            if (given != null && given.TryGetValue(rowKey, out named))
            {
                foreach (KeyValuePair<string, object> one in named)
                {
                    arguments[one.Key] = one.Value;
                }
            }

            return confirmed ? Confirmed(arguments) : arguments;
        }

        /// <summary>
        /// 正本が書いた値へ、受け手のハンドルを補ったもの。受け手は呼ぶときに借りて渡すので、
        /// 正本には書けない——書けない値の不在で、書いた値の収まりを否まない。
        /// </summary>
        private static IDictionary<string, object> Receiving(
            SchemaBranch branch, IDictionary<string, object> arguments)
        {
            IDictionary<string, object> receiving =
                new Dictionary<string, object>(arguments, StringComparer.Ordinal);
            foreach (SchemaItem input in branch.Inputs.Where(
                i => !i.Injected && i.Required == true && !arguments.ContainsKey(i.Name)))
            {
                if (string.Equals(input.Name, HandlesName, StringComparison.Ordinal))
                {
                    receiving[input.Name] = new object[] { FirstPosition };
                }
                else if (string.Equals(input.Name, AimName, StringComparison.Ordinal))
                {
                    receiving[input.Name] = FirstPosition;
                }
            }

            return receiving;
        }

        /// <summary>
        /// 書き換えるツールへ渡す項目の組を、書いた直後に読み返せる項目ぶんだけ埋めたもの。埋めた
        /// 組はそのまま引数へ入る。値の組を受け取らないツールと、埋められる項目を1つも持たない
        /// ツールでは null——書いていないものは読み返せない。
        /// </summary>
        private static IDictionary<string, object> Filled(
            ToolSchema schema,
            IDictionary<string, object> arguments,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            IDictionary<SchemaItem, string> handleTargets,
            IDictionary<string, ISet<string>> unkept,
            string tool,
            IDictionary<SchemaItem, string> sdkTypes,
            ISet<string> positioned)
        {
            SchemaItem group = schema.Branches
                .Where(b => !Skipped(b, arguments) && Satisfied(b, arguments))
                .SelectMany(b => b.Inputs)
                .FirstOrDefault(i => !i.Injected
                    && i.Members != null
                    && string.Equals(i.Name, ValueName, StringComparison.Ordinal));
            if (group == null)
            {
                return null;
            }

            Dictionary<string, object> filled =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (SchemaItem member in group.Members.Where(
                m => Kept(unkept, tool, m.Name) && !Deferred(m, sdkTypes, positioned)))
            {
                object value;
                if (TryMinimal(
                        member, sdkShapes, sampled, handleTargets, null, member.Name, out value))
                {
                    filled[member.Name] = value;
                }
            }

            if (filled.Count == 0)
            {
                return null;
            }

            arguments[group.Name] = filled;

            return filled;
        }

        /// <summary>
        /// 書いた項目を読み返して確かめる検査。相手は書いたその要素で、呼び出しと同じハンドルを
        /// 借りる——並び全体を読むと、書いていない要素の値まで見てしまう。読み返す相手を持たない
        /// ツールでは null。
        /// </summary>
        private static E2eCase ReadingBack(
            string rowKey,
            string editKind,
            string path,
            ToolSchemaTable schemas,
            IDictionary<string, string> readers,
            string tool,
            IDictionary<string, string> borrowing,
            KeyValuePair<string, object> written)
        {
            string reader;
            if (readers == null || !readers.TryGetValue(tool, out reader))
            {
                return null;
            }

            ToolSchema found = Of(schemas, reader);
            if (found == null || !Holds(found))
            {
                return null;
            }

            IDictionary<string, object> arguments = Lent();
            if (Named(found))
            {
                arguments[FieldsName] = new object[] { written.Key };
            }

            return Satisfied(found, arguments)
                ? new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    reader,
                    "書いた項目を読み返せること",
                    arguments,
                    E2eExpectation.Reads,
                    null,
                    null,
                    null,
                    borrowing,
                    new E2eExpectedMember(written.Key, written.Value))
                : null;
        }

        /// <summary>
        /// その項目が、ハンドルで指した相手へ書いても預かりに回るか。要素の位置で指す項目は、
        /// 相手がまだどのPMXにも入っていない間は解けないので、書いた直後には読み返せない。
        /// </summary>
        private static bool Deferred(
            SchemaItem member,
            IDictionary<SchemaItem, string> sdkTypes,
            ISet<string> positioned)
        {
            string typeName;

            return sdkTypes != null
                && positioned != null
                && sdkTypes.TryGetValue(member, out typeName)
                && positioned.Contains(typeName);
        }

        /// <summary>受け手を1つのハンドルで指せるツールか。</summary>
        private static bool Aims(ToolSchema schema)
        {
            return schema.Branches.SelectMany(b => b.Inputs).Any(
                i => !i.Injected && string.Equals(i.Name, AimName, StringComparison.Ordinal));
        }

        /// <summary>受け手をハンドルの並びで受け取るツールか。渡し口が無ければ借りて渡せない。</summary>
        private static bool Holds(ToolSchema schema)
        {
            return schema.Branches.SelectMany(b => b.Inputs).Any(
                i => !i.Injected
                    && string.Equals(i.Name, HandlesName, StringComparison.Ordinal));
        }

        /// <summary>
        /// 段取りがその要素を並びへ加えるか。加えないなら、外す相手が並びに居ないので借りる名前も
        /// 出ない。
        /// </summary>
        private static bool Prepares(ToolSchema adding)
        {
            return adding != null && (Handed(adding) || Assigned(adding));
        }

        /// <summary>
        /// 並びの先頭を位置で指す引数。位置で指せないツールでは null——どの要素を相手にするかが
        /// ここでは決まらない。
        /// </summary>
        private static IDictionary<string, object> Pointed(
            ToolSchema schema,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            IDictionary<SchemaItem, string> handleTargets,
            IDictionary<string, int> fixedLeading)
        {
            int first = Movable(schema.Tool, fixedLeading);
            foreach (SchemaBranch branch in schema.Branches)
            {
                IDictionary<string, object> arguments =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (SchemaItem input in branch.Inputs.Where(
                    i => !i.Injected && Points(i.Name)))
                {
                    arguments[input.Name] = new object[] { first };
                }

                bool pointed = true;
                foreach (SchemaChoice choice in branch.Choices.Where(c => c.Required))
                {
                    string name = choice.Names.FirstOrDefault(Points);
                    if (name == null)
                    {
                        pointed = false;
                        break;
                    }

                    arguments[name] = new object[] { first };
                }

                if (pointed && arguments.Count != 0
                    && TryFill(branch, sdkShapes, sampled, handleTargets, null, arguments)
                    && Satisfied(branch, arguments))
                {
                    return arguments;
                }
            }

            return null;
        }

        /// <summary>
        /// ハンドルの並びだけを渡す引数に、借りる相手の種別を選ぶ値を足したもの。種別で呼び分ける
        /// ツールへ別の種別として渡すと、呼び先が断る。作ったものの種別は、作ったツールの名前の
        /// 担当群から後ろがそのまま表す。
        /// </summary>
        private static IDictionary<string, object> Chosen(ToolSchema schema, string made)
        {
            IDictionary<string, object> arguments = Lent();
            int at = made.IndexOf(Separator);
            string kind = at < 0 ? made : made.Substring(at + 1);
            SchemaBranch chosen = schema.Branches.FirstOrDefault(
                b => b.SelectorName != null
                    && string.Equals(b.SelectorValue as string, kind, StringComparison.Ordinal));
            if (chosen != null)
            {
                arguments[chosen.SelectorName] = chosen.SelectorValue;
            }

            return arguments;
        }

        /// <summary>
        /// 組み立てた引数が、そのツールのどれかの呼び分けの要る項目をすべて埋めているか。借りる
        /// 空きは値をまだ持たないので、値の形ではなく項目の名前で見る。
        /// </summary>
        private static bool Satisfied(ToolSchema schema, IDictionary<string, object> arguments)
        {
            return schema.Branches.Any(b => Satisfied(b, arguments));
        }

        /// <summary>その呼び分けの要る項目が、組み立てた引数にすべて在るか。</summary>
        private static bool Satisfied(
            SchemaBranch branch, IDictionary<string, object> arguments)
        {
            ISet<string> named = new HashSet<string>(
                branch.Inputs.Where(i => !i.Injected).Select(i => i.Name),
                StringComparer.Ordinal);

            return arguments.Keys.All(
                    k => named.Contains(k)
                        || string.Equals(k, ConfirmName, StringComparison.Ordinal))
                && branch.Inputs.All(
                    i => i.Injected || i.Required != true || arguments.ContainsKey(i.Name))
                && branch.Choices.All(
                    c => !c.Required || c.Names.Any(arguments.ContainsKey));
        }

        /// <summary>
        /// その呼び分けの必須の組を、相手を選ぶ値で埋める。全体を相手にできるならそれを、でき
        /// なければ並びの先頭を位置で指す。どちらもできない組が在れば偽——相手が決まらない。
        /// </summary>
        private static bool Chose(SchemaBranch branch, IDictionary<string, object> arguments)
        {
            foreach (SchemaChoice choice in branch.Choices.Where(c => c.Required))
            {
                if (choice.Names.Any(arguments.ContainsKey))
                {
                    continue;
                }

                string whole = choice.Names.FirstOrDefault(
                    n => string.Equals(n, WholeName, StringComparison.Ordinal));
                if (whole != null)
                {
                    arguments[whole] = true;
                    continue;
                }

                string pointed = choice.Names.FirstOrDefault(Points);
                if (pointed == null)
                {
                    return false;
                }

                arguments[pointed] = new object[] { FirstPosition };
            }

            return true;
        }

        /// <summary>その名前が、相手を位置の並びで指す入力か。</summary>
        private static bool Points(string name)
        {
            return string.Equals(name, IndicesName, StringComparison.Ordinal)
                || string.Equals(name, ParentIndicesName, StringComparison.Ordinal);
        }

        /// <summary>作った要素をハンドルで渡すだけで呼べるツールか。</summary>
        internal static bool Handed(ToolSchema schema)
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
        /// ビューの画像を返すツールの検査。行が名乗るビューを写し取り、返った画像がその姿と合うことを
        /// 確かめる。
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
            ToolSchemaTable schemas,
            IDictionary<string, string> connectionPaths,
            ISet<string> dangerous,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            IDictionary<SchemaItem, string> handleTargets,
            IDictionary<string, IDictionary<string, object>> given,
            IDictionary<string, SampleCallRow> refused,
            IEnumerable<SchemaItem> handed,
            IList<string> maker,
            IDictionary<string, string> adders,
            IDictionary<string, string> factories,
            ISet<string> reading,
            IDictionary<string, string> removers,
            ISet<string> aimed,
            IDictionary<string, string> readers,
            IDictionary<string, ISet<string>> unkept,
            IDictionary<SchemaItem, string> sdkTypes,
            ISet<string> positioned,
            IDictionary<string, IList<string>> typeMakers,
            IDictionary<string, string> parents,
            IDictionary<string, IList<SetupOperation>> stepSetups,
            ElementWiring wiring,
            IDictionary<string, int> fixedLeading)
        {
            // 行から導く名前を持たないツールは、行の値も接続の経路も持たない。
            string rowKey = row == null ? string.Empty : row.SignatureKey;
            string tool = schema.Tool;
            string path = row == null ? string.Empty : ConnectionPath(rowKey, connectionPaths);
            string editKind = row == null ? string.Empty : ToolMapJsonReader.SpellingOf(row.EditKind);
            bool confirmed = row != null && dangerous.Contains(rowKey);

            // 確認を要さず、渡すものが決まる行は実際に呼ぶ。呼べない行は呼び先まで届く検査を1つも
            // 持たないままになる——呼び先が在ることだけを見ても、その行の振る舞いは確かめられない。
            // 確認を要する行を呼ぶのは、渡す値が正本に書かれている行に限る。値を書くのは書く側の
            // 明示の選択なので、エディタを閉じる行やモデルを消す行が黙って呼ばれることがない。
            bool written = row != null && given != null && given.ContainsKey(rowKey);
            IDictionary<string, object> calling =
                new Dictionary<string, object>(StringComparer.Ordinal);
            bool calls = row != null && (!confirmed || written)
                && TryCalling(row, schema, sdkShapes, sampled, handleTargets, given, out calling)
                && Satisfied(schema, calling);

            // 行を持たないツールも、引数を要さないなら呼ぶ。呼べるのに呼ばないままだと、この
            // ツールが覆う行は呼び先まで届く検査を1つも持たず、網羅の判定で落ちる。項目を選ばず
            // に読む検査を別に持つツールだけは呼ばない——同じ引数で同じツールを二度呼ぶことになる。
            if (row == null && Unchosen(schema) != null && !reading.Contains(schema.Tool))
            {
                calls = true;
            }

            if (calls && confirmed)
            {
                calling = Confirmed(calling);
            }

            string making = string.IsNullOrEmpty(rowKey) ? tool : rowKey;
            IDictionary<string, string> borrowing = null;

            // 引数にハンドルで相手を取る呼び出しは、その相手も作ってから渡す。道ごとに作る列を
            // 分けて覚える——同じ型を2か所で取る呼び出しもあるが、渡すのは別の実体でよい。
            IDictionary<string, IList<string>> handing = null;
            if (!calls && maker != null && aimed != null && aimed.Contains(tool) && Aims(schema))
            {
                calling = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { AimName, null },
                };
                if (TryFill(schema, sdkShapes, sampled, handleTargets, null, calling) && Satisfied(schema, calling))
                {
                    calls = true;
                    borrowing = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        {
                            AimName,
                            Borrowed(
                                Step(making, maker.Count - 1),
                                Of(schemas, maker[maker.Count - 1]))
                        },
                    };
                }
            }

            if (!calls && (!confirmed || written) && maker != null && Holds(schema))
            {
                calling = Chosen(schema, maker[maker.Count - 1]);
                IDictionary<string, string> wanted =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                if (TryFill(schema, sdkShapes, sampled, handleTargets, wanted, calling)
                    && Satisfied(schema, calling = Written(calling, rowKey, given, confirmed)))
                {
                    handing = Handing(Narrowed(wanted, row), typeMakers);
                }

                if (handing != null)
                {
                    calls = true;
                    borrowing = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        {
                            Leaf(HandlesName),
                            Borrowed(
                                Step(making, maker.Count - 1),
                                Of(schemas, maker[maker.Count - 1]))
                        },
                    };
                    foreach (KeyValuePair<string, IList<string>> one in handing)
                    {
                        borrowing[one.Key] = Borrowed(
                            Step(Scoping(making, one.Key), one.Value.Count - 1),
                            Of(schemas, one.Value[one.Value.Count - 1]));
                    }
                }
            }
            string adder;
            string factory;

            // 並びから取り除くツールは、取り除く相手を自分で用意してから呼ぶ。並びの中身は、
            // 先に置いた要素がそのまま残るとは限らない——途中の検査がモデルを空へ戻すので、
            // 直前に1つ加えておかなければ、位置で指した先が無いまま呼ぶことになる。
            IList<string> filling = null;
            if (!calls && row == null && removers != null
                && removers.TryGetValue(tool, out adder)
                && factories != null && factories.TryGetValue(adder, out factory)
                && Of(schemas, factory) != null
                && Prepares(Of(schemas, adder))
                && Filling(adder, parents, factories, schemas) != null)
            {
                string made = Scoping(tool, adder);
                if (Holds(schema))
                {
                    calling = Lent();
                    if (TryFill(schema, sdkShapes, sampled, handleTargets, null, calling) && Satisfied(schema, calling))
                    {
                        calls = true;
                        borrowing = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            { Leaf(HandlesName), Borrowed(made, Of(schemas, factory)) },
                        };
                    }
                }
                else
                {
                    IDictionary<string, object> pointing =
                        Pointed(schema, sdkShapes, sampled, handleTargets, fixedLeading);
                    if (pointing != null)
                    {
                        calls = true;
                        calling = pointing;
                    }
                }

                if (calls)
                {
                    filling = Filling(adder, parents, factories, schemas);
                }
            }

            IDictionary<string, object> values = calls && row == null && borrowing != null
                ? Filled(
                    schema, calling, sdkShapes, sampled, handleTargets, unkept, tool, sdkTypes,
                    positioned)
                : null;

            SampleCallRow denied;
            bool denies = row != null && refused.TryGetValue(rowKey, out denied);

            // 観測の段を組み立てられるかは、呼び出しを組み立てるかに依らず確かめる。受け取らない
            // 引数を指す宣言は正本の誤りで、呼び出しの有無で見え隠れしてよいものではない。
            Postcondition[] drawn = Drawn(row).ToArray();
            foreach (Postcondition judgement in drawn)
            {
                Observed(judgement, schemas, rowKey);
            }

            Postcondition[] compared = Compared(row).ToArray();
            foreach (Postcondition judgement in compared)
            {
                Reader(judgement, schemas, rowKey);
            }

            string wrote = Wrote(row, schema, rowKey);

            // 断られることを確かめる呼び出しは、断られた時点でハンドルを出さない。その行の観測は
            // 借りるものを持たないので組み立てない。
            bool draws = calls && !denies && drawn.Length != 0;

            // 断られる呼び出しは何も動かさないので、読み比べても違いが出ない。確かめているのが
            // 行の効果でなく断りになるので、その行の読み比べは組み立てない。
            bool reads = calls && !denies && compared.Length != 0;

            string held = Borrowed(rowKey, schema);
            for (int at = 0; borrowing != null && maker != null && at < maker.Count; at++)
            {
                // 道の段として呼ぶ行も、その行が宣言した段取りを先に流す。段が作ったものをその
                // まま次の段へ渡すと、中身を持たないまま先へ進み、道の終わりで断られる。
                IList<SetupOperation> step;
                foreach (E2eCase one in Prepared(
                    stepSetups != null && stepSetups.TryGetValue(maker[at], out step)
                        ? step
                        : null,
                    schemas,
                    rowKey,
                    editKind,
                    path,
                    adders,
                    factories,
                    at == 0
                        ? null
                        : Borrowed(Step(making, at - 1), Of(schemas, maker[at - 1])),
                    wiring))
                {
                    yield return one;
                }

                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    maker[at],
                    "呼び出しの相手を1つ作れること",
                    at == 0 ? new Dictionary<string, object>(StringComparer.Ordinal) : Lent(),
                    E2eExpectation.Success,
                    null,
                    null,
                    Step(making, at),
                    at == 0
                        ? null
                        : new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            {
                                Leaf(HandlesName),
                                Borrowed(Step(making, at - 1), Of(schemas, maker[at - 1]))
                            },
                        });
            }

            foreach (string filler in filling ?? new string[0])
            {
                foreach (E2eCase one in Filling(
                    rowKey,
                    editKind,
                    path,
                    schemas,
                    factories[filler],
                    filler,
                    Scoping(tool, filler),
                    "呼び出しの前に並びへ加える要素を1つ作れること",
                    "作った要素を呼び出しの前に並びへ加えられること",
                    wiring))
                {
                    yield return one;
                }
            }

            foreach (KeyValuePair<string, IList<string>> one in handing
                ?? new Dictionary<string, IList<string>>(StringComparer.Ordinal))
            {
                string stepped = Scoping(making, one.Key);
                for (int at = 0; at < one.Value.Count; at++)
                {
                    yield return new E2eCase(
                        rowKey,
                        editKind,
                        path,
                        one.Value[at],
                        "引数に渡す相手を1つ作れること",
                        at == 0 ? new Dictionary<string, object>(StringComparer.Ordinal) : Lent(),
                        E2eExpectation.Success,
                        null,
                        null,
                        Step(stepped, at),
                        at == 0
                            ? null
                            : new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                {
                                    Leaf(HandlesName),
                                    Borrowed(
                                        Step(stepped, at - 1), Of(schemas, one.Value[at - 1]))
                                },
                            });
                }
            }

            // 行の段取りは、呼ぶ前の姿を覚えるより先に流す。間に挟むと、段取りが動かしたぶんが
            // 呼び出しの効果として数えられ、確かめているのが行の効果でなく段取りの効果になる。
            string received = borrowing != null && maker != null
                ? Borrowed(Step(making, maker.Count - 1), Of(schemas, maker[maker.Count - 1]))
                : null;
            IList<SetupOperation> tidying;
            foreach (E2eCase one in Prepared(
                calls && stepSetups != null && stepSetups.TryGetValue(tool, out tidying)
                    ? tidying
                    : null,
                schemas,
                rowKey,
                editKind,
                path,
                adders,
                factories,
                received,
                wiring))
            {
                yield return one;
            }

            foreach (Postcondition judgement in reads ? compared : new Postcondition[0])
            {
                foreach (E2eCase one in
                    Prepared(
                        judgement.Setup, schemas, rowKey, editKind, path, adders, factories,
                        received, wiring))
                {
                    yield return one;
                }

                yield return Recording(
                    judgement,
                    Reader(judgement, schemas, rowKey),
                    rowKey,
                    editKind,
                    path,
                    calling.ContainsKey(HandlesName) ? received : null);
            }

            if (calls)
            {
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    denies
                        ? "成り立たない値を渡す呼び出しを、その理由で断ること"
                        : row != null && row.EditKind == ToolMapEditKind.Read
                            ? "呼び出して値を返せること"
                            : "呼び出して成功すること",
                    calling,
                    denies ? E2eExpectation.Denied : E2eExpectation.Called,
                    denies ? refused[rowKey].Refused : null,
                    null,
                    draws ? rowKey : null,
                    borrowing,
                    null,
                    denies ? refused[rowKey].Says : null,
                    denies ? null : wrote,
                    null,
                    denies ? null : Written(row));

                foreach (KeyValuePair<string, object> one in values
                    ?? new Dictionary<string, object>(StringComparer.Ordinal))
                {
                    E2eCase back = ReadingBack(
                        rowKey, editKind, path, schemas, readers, tool, borrowing, one);
                    if (back != null)
                    {
                        IList<SetupOperation> ready;
                        foreach (E2eCase step in Prepared(
                            stepSetups != null
                                && stepSetups.TryGetValue(back.Tool, out ready)
                                ? ready
                                : null,
                            schemas,
                            rowKey,
                            editKind,
                            path,
                            adders,
                            factories,
                            received,
                            wiring))
                        {
                            yield return step;
                        }

                        yield return back;
                    }
                }

                foreach (Postcondition judgement in draws ? drawn : new Postcondition[0])
                {
                    yield return Drawing(judgement, schemas, rowKey, editKind, path, held);
                }

                foreach (Postcondition judgement in reads ? compared : new Postcondition[0])
                {
                    yield return Changing(
                        judgement,
                        Reader(judgement, schemas, rowKey),
                        rowKey,
                        editKind,
                        path,
                        calling.ContainsKey(HandlesName) ? received : null);
                }

                if (draws)
                {
                    yield return new E2eCase(
                        rowKey,
                        editKind,
                        path,
                        ReleaseToolName,
                        "観測に使ったハンドルを解放できること",
                        Lent(),
                        E2eExpectation.Success,
                        null,
                        null,
                        null,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            { Leaf(HandlesName), held },
                        });
                }
            }

            if (confirmed)
            {
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    ConfirmRefusal,
                    Arguments(false),
                    E2eExpectation.Refusal,
                    ConfirmRequired);
            }

            foreach (SchemaItem handles in HandleInputs(schema))
            {
                IDictionary<string, object> arguments =
                    Single(handles.Name, new object[] { UnknownHandle }, confirmed);
                if (!TryFill(schema, sdkShapes, sampled, handleTargets, null, arguments))
                {
                    continue;
                }

                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    ListedHandleRefusal,
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
                if (!TryFill(holding, sdkShapes, sampled, handleTargets, null, arguments))
                {
                    continue;
                }

                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    LoneHandleRefusal,
                    arguments,
                    E2eExpectation.Refusal,
                    InvalidHandle);
            }

            foreach (SchemaItem limit in LimitInputs(schema))
            {
                IDictionary<string, object> counting = calls
                    ? new Dictionary<string, object>(calling, StringComparer.Ordinal)
                    : Single(limit.Name, 0, confirmed);
                counting[limit.Name] = 0;
                if (!calls)
                {
                    Chose(
                        schema.Branches.First(b => b.Inputs.Any(i => ReferenceEquals(i, limit))),
                        counting);
                }

                if (!Satisfied(schema, counting))
                {
                    continue;
                }

                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    tool,
                    CountRefusal,
                    counting,
                    E2eExpectation.Refusal,
                    InvalidArgument,
                    null,
                    null,
                    calls ? borrowing : null);
            }
        }

        /// <summary>
        /// 並べたものが名前を載せる観測ツールか。在ることを見るだけなので返す項目は要らないが、
        /// 1つも選ばない頼み方は断られる。名前を載せるならそれ1つに絞る——項目を選ばずに頼むと
        /// 位置で指す項目まで返そうとして、まだどのPMXにも入っていない実体では解けずに断られる。
        /// 名前を載せないツールでは絞る相手が無いので、項目を選ばずに頼む。
        /// </summary>
        private static bool Named(ToolSchema observer)
        {
            SchemaItem items = observer.Output == null || observer.Output.Members == null
                ? null
                : observer.Output.Members.FirstOrDefault(
                    m => string.Equals(m.Name, ItemsName, StringComparison.Ordinal));
            SchemaItem element = items == null ? observer.Output : items.Element;

            return observer.Branches.SelectMany(b => b.Inputs).Any(
                    i => !i.Injected && string.Equals(i.Name, FieldsName, StringComparison.Ordinal))
                && element != null
                && element.Members != null
                && element.Members.Any(
                    m => string.Equals(m.Name, NameName, StringComparison.Ordinal));
        }

        /// <summary>
        /// 事後条件を実機で確かめる行か。確かめる行を最後へ回さないのは、直に呼ぶと状態が動く行の
        /// 中に、呼び先を使えなくするものがあるからである。そのあとで確かめても、見ているのは
        /// 宣言した効果ではなくその失敗になる。
        /// </summary>
        private static bool Verified(ToolMapRow row)
        {
            return row.Postcondition != null
                && row.Postcondition.Any(p => p.Kind == EffectCheckKind.File
                    || p.Kind == EffectCheckKind.Handle
                    || p.Kind == EffectCheckKind.Readback);
        }

        /// <summary>確認も渡す引数。渡された組は書き換えず、写しへ足す。</summary>
        private static IDictionary<string, object> Confirmed(IDictionary<string, object> arguments)
        {
            IDictionary<string, object> given =
                new Dictionary<string, object>(arguments, StringComparer.Ordinal);
            given[ConfirmName] = true;

            return given;
        }

        /// <summary>その行が書き先を確かめる判定の識別子。確かめない行では null。</summary>
        private static string Written(ToolMapRow row)
        {
            Postcondition writing = row == null || row.Postcondition == null
                ? null
                : row.Postcondition.FirstOrDefault(p => p.Kind == EffectCheckKind.File);

            return writing == null ? null : writing.EffectId;
        }

        /// <summary>
        /// その行がファイルを書くと宣言した先を渡す引数の名前。宣言しない行では null。指す引数を
        /// ツールが受け取らない宣言は、実機へ投げても在りもしない位置を見に行くだけなので、ここで
        /// 組み立てを止める。1回の呼び出しで確かめられる書き先は1つまでとする。
        /// </summary>
        private static string Wrote(ToolMapRow row, ToolSchema schema, string rowKey)
        {
            Postcondition[] writing = row == null || row.Postcondition == null
                ? new Postcondition[0]
                : row.Postcondition.Where(p => p.Kind == EffectCheckKind.File).ToArray();
            if (writing.Length == 0)
            {
                return null;
            }

            if (writing.Length > 1)
            {
                throw new InvalidOperationException(
                    "1回の呼び出しで2つ以上の書き先は確かめられない: " + rowKey);
            }

            string name = writing[0].EffectKey;
            if (!schema.Branches.SelectMany(b => b.Inputs).Any(
                i => !i.Injected && string.Equals(i.Name, name, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "書いた先を渡す引数を、ツールが受け取らない: " + schema.Tool + "(" + name + ")");
            }

            return name;
        }

        /// <summary>
        /// そのツールの受け手を得るまでに順に呼ぶツールの列。列を持たないツールでは null——渡す
        /// 相手が決まらないツールは呼べないままで、呼び先まで届く検査を1つも持たない。
        /// </summary>
        private static IList<string> Maker(
            string tool, IDictionary<string, IList<string>> makers)
        {
            IList<string> path;

            return makers != null && makers.TryGetValue(tool, out path) && path.Count != 0
                ? path
                : null;
        }

        /// <summary>
        /// 作った要素を並びへ加えるまでに要るものの引き当て。位置で指す項目を持つ要素は、指す先を
        /// 埋めてから加える——埋めずに加えると、指す先を持たない要素として書き戻しで捨てられる。
        /// </summary>
        private sealed class ElementWiring
        {
            public ElementWiring(
                IDictionary<string, string> factories,
                IDictionary<string, string> parents,
                IDictionary<string, string> addersByType,
                IDictionary<string, string> updaters,
                IDictionary<string, IDictionary<string, string>> aiming,
                IDictionary<string, ParentValues> parentValues)
            {
                Factories = factories;
                Parents = parents;
                AddersByType = addersByType;
                Updaters = updaters;
                Aiming = aiming;
                ParentValues = parentValues;
            }

            /// <summary>並びへ加えるツールの名前から、その要素を作るツールの名前へ。</summary>
            public IDictionary<string, string> Factories { get; }

            /// <summary>並びへ加えるツールの名前から、その親を加えるツールの名前へ。</summary>
            public IDictionary<string, string> Parents { get; }

            /// <summary>型の名前から、その型の要素を並びへ加えるツールの名前へ。</summary>
            public IDictionary<string, string> AddersByType { get; }

            /// <summary>並びへ加えるツールの名前から、その要素を書き換えるツールの名前へ。</summary>
            public IDictionary<string, string> Updaters { get; }

            /// <summary>
            /// 並びへ加えるツールの名前から、位置で指す項目の名前とその項目が指す型へ。位置で指す
            /// 項目を持たない要素は持たない。
            /// </summary>
            public IDictionary<string, IDictionary<string, string>> Aiming { get; }

            /// <summary>並びへ加えるツールの名前から、加える前に親へ揃える値へ。</summary>
            public IDictionary<string, ParentValues> ParentValues { get; }
        }

        /// <summary>
        /// 要素を1つ作って並びへ加える2段。加える形は加える側のツールで分かれる——作った要素を
        /// ハンドルで渡すだけで済むものと、親を位置で指す組で渡すものがある。差し込む先の道は
        /// その形ごとに違うので、道を選ぶところをここ1か所に持つ。
        /// </summary>
        private static IEnumerable<E2eCase> Filling(
            string rowKey,
            string editKind,
            string path,
            ToolSchemaTable schemas,
            string making,
            string adding,
            string held,
            string madePurpose,
            string addedPurpose,
            ElementWiring wiring)
        {
            string writing = Writing(wiring, adding);
            IDictionary<string, object> writes = writing == null
                ? null
                : Chosen(Of(schemas, writing), making);
            IDictionary<string, string> aiming = Aimed(wiring, adding, Of(schemas, writing), writes);

            // 指す先の並びが空だと、位置で指す項目を埋められない。指す先を先に用意する——その
            // 並びの要素は位置で指す項目を持たないものとして扱い、ここから先は辿らない。
            foreach (string adder in aiming.Values
                .Select(t => Added(wiring, t))
                .Where(a => a != null)
                .Distinct(StringComparer.Ordinal))
            {
                foreach (string filler in
                    Filling(adder, wiring.Parents, wiring.Factories, schemas)
                        ?? new string[0])
                {
                    foreach (E2eCase one in Filling(
                        rowKey,
                        editKind,
                        path,
                        schemas,
                        wiring.Factories[filler],
                        filler,
                        Scoping(held, filler),
                        "指す先にする要素を1つ作れること",
                        "指す先にする要素を並びへ加えられること",
                        null))
                    {
                        yield return one;
                    }
                }
            }

            ParentValues values = Aligned(wiring, adding);
            if (values != null)
            {
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    values.ParentTool,
                    "加える先の親を揃えられること",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { IndicesName, new object[] { FirstPosition } },
                        {
                            ValueName,
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                { values.Member, values.Value },
                            }
                        },
                    },
                    E2eExpectation.Success,
                    null);
            }

            bool listed = Handed(Of(schemas, adding));
            yield return new E2eCase(
                rowKey,
                editKind,
                path,
                making,
                madePurpose,
                new Dictionary<string, object>(StringComparer.Ordinal),
                E2eExpectation.Success,
                null,
                null,
                held);
            if (aiming.Count != 0)
            {
                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    writing,
                    "作った要素の位置で指す項目を並びの先頭へ向けられること",
                    Pointing(writes, aiming.Keys),
                    E2eExpectation.Success,
                    null,
                    null,
                    null,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { Leaf(HandlesName), Borrowed(held, Of(schemas, making)) },
                    });
            }

            yield return new E2eCase(
                rowKey,
                editKind,
                path,
                adding,
                addedPurpose,
                listed ? Lent() : IntoFirstParent(),
                E2eExpectation.Success,
                null,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    {
                        listed
                            ? Leaf(HandlesName)
                            : Leaf(AssignmentsName + "/0/" + HandlesName),
                        Borrowed(held, Of(schemas, making))
                    },
                });
        }

        /// <summary>
        /// その要素を並びへ加える前に埋める、位置で指す項目の名前とその項目が指す型。選んだ
        /// 呼び分けが受け取る項目だけを採る——種別で呼び分ける書き換えでは、呼び分けごとに
        /// 受け取る項目が違う。埋めるものが無ければ空。
        /// </summary>
        private static IDictionary<string, string> Aimed(
            ElementWiring wiring,
            string adding,
            ToolSchema writing,
            IDictionary<string, object> writes)
        {
            IDictionary<string, string> aiming;
            Dictionary<string, string> taken =
                new Dictionary<string, string>(StringComparer.Ordinal);
            if (wiring == null || writing == null || !wiring.Aiming.TryGetValue(adding, out aiming))
            {
                return taken;
            }

            foreach (SchemaItem member in writing.Branches
                .Where(b => !Skipped(b, writes))
                .SelectMany(b => b.Inputs)
                .Where(i => !i.Injected
                    && i.Members != null
                    && string.Equals(i.Name, ValueName, StringComparison.Ordinal))
                .SelectMany(i => i.Members))
            {
                string typeName;
                if (aiming.TryGetValue(member.Name, out typeName))
                {
                    taken[member.Name] = typeName;
                }
            }

            return taken;
        }

        /// <summary>その要素を書き換えるツール。書き換える手立てが無ければ null。</summary>
        private static string Writing(ElementWiring wiring, string adding)
        {
            string writing;

            return wiring != null && wiring.Updaters != null
                && wiring.Updaters.TryGetValue(adding, out writing)
                ? writing
                : null;
        }

        /// <summary>加える前に親へ揃える値。揃える必要が無ければ null。</summary>
        private static ParentValues Aligned(ElementWiring wiring, string adding)
        {
            ParentValues values;

            return wiring != null && wiring.ParentValues != null
                && wiring.ParentValues.TryGetValue(adding, out values)
                ? values
                : null;
        }

        /// <summary>その型の要素を並びへ加えるツール。加える手立てが無ければ null。</summary>
        private static string Added(ElementWiring wiring, string typeName)
        {
            string adding;

            return wiring.AddersByType != null
                && wiring.AddersByType.TryGetValue(typeName, out adding)
                ? adding
                : null;
        }

        /// <summary>
        /// 並びへ加えるツールごとの、位置で指す項目の名前とその項目が指す型。共通契約が挙げた
        /// 項目だけを採る——指す先を持たないまま加えると書き戻しで捨てられる要素はそこが決める。
        /// 捨てられない要素にまで指す先を用意すると、確かめるものが増えないまま実機の検査が
        /// 伸びる。
        /// </summary>
        private static IDictionary<string, IDictionary<string, string>> Aiming(
            ToolSchemaTable schemas,
            IDictionary<SchemaItem, string> sdkTypes,
            ISet<string> positioned,
            IDictionary<string, string> updaters,
            IDictionary<string, ISet<string>> targeted)
        {
            Dictionary<string, IDictionary<string, string>> aiming =
                new Dictionary<string, IDictionary<string, string>>(StringComparer.Ordinal);
            HashSet<string> reached = new HashSet<string>(StringComparer.Ordinal);
            if (updaters == null || sdkTypes == null || positioned == null || targeted == null)
            {
                return aiming;
            }

            foreach (KeyValuePair<string, string> one in updaters)
            {
                ToolSchema writing = Of(schemas, one.Value);
                ISet<string> wanted;
                if (writing == null || !targeted.TryGetValue(one.Value, out wanted))
                {
                    continue;
                }

                Dictionary<string, string> members =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (SchemaItem member in writing.Branches
                    .SelectMany(b => b.Inputs)
                    .Where(i => !i.Injected
                        && i.Members != null
                        && string.Equals(i.Name, ValueName, StringComparison.Ordinal))
                    .SelectMany(i => i.Members))
                {
                    string typeName;
                    if (wanted.Contains(member.Name)
                        && sdkTypes.TryGetValue(member, out typeName)
                        && positioned.Contains(typeName))
                    {
                        members[member.Name] = typeName;
                    }
                }

                if (members.Count != 0)
                {
                    aiming[one.Key] = members;
                    reached.Add(one.Value);
                }
            }

            // 名指しが埋める段を生まなくなったら落とす。名前が実在するだけでは、加える側との
            // 結び目が切れた回も、その項目が位置で数えられなくなった回も素通りし、指す先を
            // 持たない要素が書き戻しで捨てられる検査が、また合格として数えられる。
            string missed = targeted.Keys
                .Where(t => !reached.Contains(t))
                .OrderBy(t => t, StringComparer.Ordinal)
                .FirstOrDefault();
            if (missed != null)
            {
                throw new InvalidOperationException(
                    "指す先を埋める項目の名指しが、埋める段を1つも生んでいない: " + missed);
            }

            return aiming;
        }

        /// <summary>
        /// その名前が指す並びを用意するまでに、順に呼ぶ加える側のツールの列。加える側を引けない
        /// 名前と、用意しきれない並びでは null。
        /// </summary>
        private static IList<string> Filled(
            string name,
            IDictionary<string, string> adders,
            IDictionary<string, string> parents,
            IDictionary<string, string> factories,
            ToolSchemaTable schemas)
        {
            string adding;

            return adders != null && factories != null && adders.TryGetValue(name, out adding)
                ? Filling(adding, parents, factories, schemas)
                : null;
        }

        /// <summary>
        /// その要素を並びへ加えるまでに、順に呼ぶ加える側のツールの列。親の並びに1つも無い要素は
        /// 加えられないので、根に近い親から並べる。用意しきれない並びでは null を返す——どこかの
        /// 段に作る手立てか加える手立てが無いとき、列の先頭がまだ親を要するとき(親を辿れずに
        /// 終わったか、親を辿る先が巡って打ち切られたとき)である。通らない用意の段を組み立てると、
        /// 落ちた理由が別の系統として数えられ、原因の切り分けが後ろへ回る。
        /// </summary>
        private static IList<string> Filling(
            string adder,
            IDictionary<string, string> parents,
            IDictionary<string, string> factories,
            ToolSchemaTable schemas)
        {
            List<string> filling = new List<string>();
            HashSet<string> walked = new HashSet<string>(StringComparer.Ordinal);
            string at = adder;
            while (at != null && walked.Add(at))
            {
                string making;
                if (!factories.TryGetValue(at, out making) || Of(schemas, making) == null
                    || Of(schemas, at) == null || !Prepares(Of(schemas, at)))
                {
                    return null;
                }

                filling.Insert(0, at);
                string above;
                at = parents != null && parents.TryGetValue(at, out above) ? above : null;
            }

            return Handed(Of(schemas, filling[0])) ? filling : null;
        }

        /// <summary>
        /// 引数へ渡す相手の型を、行が挙げた型へ置き換える。挙げた名前の引数がこの呼び出しに
        /// 無ければ落とす。
        /// </summary>
        private static IDictionary<string, string> Narrowed(
            IDictionary<string, string> wanted, ToolMapRow row)
        {
            if (row == null || row.ArgumentTypes == null)
            {
                return wanted;
            }

            Dictionary<string, string> narrowed =
                new Dictionary<string, string>(wanted, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> one in row.ArgumentTypes)
            {
                IList<string> paths = wanted.Keys
                    .Where(p => string.Equals(Named(p), one.Key, StringComparison.Ordinal))
                    .ToList();
                if (paths.Count == 0)
                {
                    throw new InvalidOperationException(
                        "行が挙げた引数が、その呼び出しに無い: "
                            + row.SignatureKey + " の " + one.Key);
                }

                foreach (string path in paths)
                {
                    narrowed[path] = one.Value;
                }
            }

            return narrowed;
        }

        /// <summary>道の末の名前。</summary>
        private static string Named(string path)
        {
            int at = path.LastIndexOf(PathStep, StringComparison.Ordinal);

            return at < 0 ? path : path.Substring(at + PathStep.Length);
        }

        /// <summary>
        /// 引数へ渡す相手を、道ごとにどう作るか。作る列を引けない型が1つでもあれば null——
        /// 渡すものが揃わない呼び出しは組み立てない。取る相手が無ければ空の対応表になる。
        /// </summary>
        private static IDictionary<string, IList<string>> Handing(
            IDictionary<string, string> wanted,
            IDictionary<string, IList<string>> typeMakers)
        {
            Dictionary<string, IList<string>> handing =
                new Dictionary<string, IList<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> one in wanted)
            {
                IList<string> made;
                if (typeMakers == null || !typeMakers.TryGetValue(one.Value, out made)
                    || made.Count == 0)
                {
                    return null;
                }

                handing[one.Key] = made;
            }

            return handing;
        }

        /// <summary>
        /// 呼び出しより先に出す段が、出したハンドルを覚えておく名前。<paramref name="part"/> は
        /// その段が何のためのものかを分ける綴りで、引数の道でも、先に呼ぶツールの名前でもよい。
        /// 道の区切りは名前に残さない——借りる側は斜線で覚えた値の中を辿るので、名前に斜線が
        /// あると、名前の途中までを名前と読んでしまう。
        /// </summary>
        private static string Scoping(string making, string part)
        {
            return making + Scoped + part.Replace(PathStep, Scoped);
        }

        /// <summary>受け手を作る段が出したハンドルを覚えておく名前。段ごとに分ける。</summary>
        private static string Step(string name, int at)
        {
            return name + Scoped + at.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>出たハンドルを観測すると宣言した判定。宣言しない行では空。</summary>
        private static IEnumerable<Postcondition> Drawn(ToolMapRow row)
        {
            return row == null || row.Postcondition == null
                ? new Postcondition[0]
                : row.Postcondition.Where(p => p.Kind == EffectCheckKind.Handle);
        }

        /// <summary>呼ぶ前と後で読み比べる判定。</summary>
        private static IEnumerable<Postcondition> Compared(ToolMapRow row)
        {
            return row == null || row.Postcondition == null
                ? new Postcondition[0]
                : row.Postcondition.Where(p => p.Kind == EffectCheckKind.Readback
                    && p.Comparison == EffectComparison.AnyChanged);
        }

        /// <summary>
        /// 呼ぶ前と後で読むツール。宣言の無い判定と、並びの全体を読めないツールを指す判定は、
        /// 何と何を読み比べるのかが決まらないので、ここで組み立てを止める。
        /// </summary>
        private static ToolSchema Reader(
            Postcondition judgement, ToolSchemaTable schemas, string rowKey)
        {
            ToolSchema observer = schemas.Tools.FirstOrDefault(
                t => string.Equals(t.Tool, judgement.ObserverTool, StringComparison.Ordinal));
            if (observer == null)
            {
                throw new InvalidOperationException(
                    "読み比べる相手を宣言していない判定は組み立てられない: " + rowKey);
            }

            if (!Reads(observer.Tool))
            {
                throw new InvalidOperationException(
                    "読む働きを持たないツールとは読み比べられない: "
                        + judgement.ObserverTool + "(" + rowKey + ")");
            }

            if (Reading(observer, null) == null)
            {
                throw new InvalidOperationException(
                    "並びの全体を読めないツールとは読み比べられない: "
                        + judgement.ObserverTool + "(" + rowKey + ")");
            }

            return observer;
        }

        /// <summary>その行がその判定のために覚えておく名前。</summary>
        private static string Remembered(string rowKey, Postcondition judgement)
        {
            return rowKey + Scoped + judgement.ObserverTool;
        }

        /// <summary>その名前のツールが、読む働きを持つか。</summary>
        private static bool Reads(string tool)
        {
            string[] words = tool.Split('_');

            return words.Length > 1
                && new[] { ToolVerb.Get, ToolVerb.List }.Any(
                    v => string.Equals(
                        words[1], v.ToString().ToLowerInvariant(), StringComparison.Ordinal));
        }

        /// <summary>
        /// 呼ぶ前と後で読むときに渡す引数。相手をハンドルで受け取る観測へ受け手を渡したときは、
        /// そのハンドルの空きだけを持つ組を返す。渡さないときは並びの全体を指す組で、全体をどう
        /// 指すかは呼び分けごとに違うので、指せる呼び分けを探してその指し方で埋める。どの呼び分け
        /// でも指せなければ null。
        /// </summary>
        private static IDictionary<string, object> Reading(
            ToolSchema observer, string receiver)
        {
            if (receiver != null && Holds(observer))
            {
                return Lent();
            }

            foreach (SchemaBranch branch in observer.Branches)
            {
                IDictionary<string, object> arguments = Whole(branch);
                if (arguments != null && Fits(branch, arguments))
                {
                    return arguments;
                }
            }

            return null;
        }

        /// <summary>
        /// その呼び分けで並びの全体を指す引数。位置や範囲でしか指せない組を持つ呼び分けでは null
        /// ——どこを指すかがここでは決まらない。
        /// </summary>
        private static IDictionary<string, object> Whole(SchemaBranch branch)
        {
            IDictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            if (branch.Inputs.Any(
                i => string.Equals(i.Name, LimitName, StringComparison.Ordinal)))
            {
                arguments[LimitName] = ReadbackLimit;
            }
            foreach (SchemaChoice choice in branch.Choices.Where(c => c.Required))
            {
                string whole = choice.Names.FirstOrDefault(
                    n => n.EndsWith(AllName, StringComparison.OrdinalIgnoreCase));
                if (whole == null)
                {
                    return null;
                }

                arguments[whole] = true;
            }

            return arguments.Count == 0 ? null : arguments;
        }

        /// <summary>
        /// 呼ぶ前に整える段。行が持つ段取りと、判定が持つ読み比べの始まりの両方がここを通る。
        /// 読み比べの始まりを揃えずに読み比べると、呼ぶ前から同じ姿だった回に変わらないことが
        /// 起き、確かめているのが行の効果でなくその回の巡り合わせになる。
        /// </summary>
        private static IEnumerable<E2eCase> Prepared(
            IList<SetupOperation> setup,
            ToolSchemaTable schemas,
            string rowKey,
            string editKind,
            string path,
            IDictionary<string, string> adders,
            IDictionary<string, string> factories,
            string received,
            ElementWiring wiring)
        {
            IDictionary<string, string> produced =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (SetupOperation operation in setup ?? (IList<SetupOperation>)new SetupOperation[0])
            {
                if (operation.Tag == SetupTag.AddElement)
                {
                    foreach (E2eCase one in
                        Adding(
                            operation, schemas, rowKey, editKind, path, adders, factories,
                            wiring))
                    {
                        yield return one;
                    }

                    continue;
                }

                if (operation.Tag == SetupTag.InitPmx)
                {
                    yield return new E2eCase(
                        rowKey,
                        editKind,
                        path,
                        InitializeToolName,
                        "段取りがモデルを空へ揃えられること",
                        Confirmed(new Dictionary<string, object>(StringComparer.Ordinal)),
                        E2eExpectation.Success,
                        null);

                    continue;
                }

                IDictionary<string, string> taken;
                IDictionary<string, object> args =
                    Receiving(operation.Args, received, rowKey, produced, out taken);
                string made = operation.Out == null
                    ? null
                    : Scoping(rowKey, operation.Out);
                if (made != null)
                {
                    produced[operation.Out] =
                        Borrowed(made, Of(schemas, operation.ToolName));
                }

                yield return new E2eCase(
                    rowKey,
                    editKind,
                    path,
                    operation.ToolName,
                    "段取りの呼び出しが通ること",
                    args,
                    E2eExpectation.Success,
                    null,
                    null,
                    made,
                    taken);
            }
        }

        /// <summary>
        /// 段取りが呼ぶときの引数。その行の呼び出しが相手にするものを指す値は、借りる空きへ替えて
        /// 借りる先を覚える——段取りは呼び出しと同じ相手を整えるので、相手は借りて渡す。
        /// </summary>
        private static IDictionary<string, object> Receiving(
            IDictionary<string, object> args,
            string received,
            string rowKey,
            IDictionary<string, string> produced,
            out IDictionary<string, string> borrowed)
        {
            IDictionary<string, object> given =
                new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, string> taken =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> one in args
                ?? (IDictionary<string, object>)new Dictionary<string, object>(
                    StringComparer.Ordinal))
            {
                given[one.Key] = Received(
                    one.Value, one.Key, received, rowKey, taken, produced);
            }

            borrowed = taken.Count == 0 ? null : taken;

            return given;
        }

        /// <summary>
        /// 段取りが渡す値1つ。相手を指す値なら借りる空きにして道を覚え、ほかはそのまま返す。
        /// </summary>
        private static object Received(
            object value,
            string path,
            string received,
            string rowKey,
            IDictionary<string, string> taken,
            IDictionary<string, string> produced)
        {
            object[] items = value as object[];
            if (items != null)
            {
                object[] each = new object[items.Length];
                for (int at = 0; at < items.Length; at++)
                {
                    each[at] = Received(
                        items[at],
                        path + PathStep + at.ToString(CultureInfo.InvariantCulture),
                        received,
                        rowKey,
                        taken,
                        produced);
                }

                return each;
            }

            IDictionary<string, object> members = value as IDictionary<string, object>;
            if (members != null)
            {
                IDictionary<string, object> each =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> one in members)
                {
                    each[one.Key] = Received(
                        one.Value, path + PathStep + one.Key, received, rowKey, taken,
                        produced);
                }

                return each;
            }

            string text = value as string;
            if (text != null
                && text.StartsWith(ReferenceSpace.SetupOut, StringComparison.Ordinal))
            {
                string name = text.Substring(ReferenceSpace.SetupOut.Length);
                string key;
                if (produced == null || !produced.TryGetValue(name, out key))
                {
                    throw new InvalidOperationException(
                        "段取りが、まだ出していない値を指している: " + rowKey + " の " + name);
                }

                taken[path] = key;

                return null;
            }

            if (!string.Equals(
                value as string, ReferenceSpace.Receiver, StringComparison.Ordinal))
            {
                string other = new[]
                {
                    ReferenceSpace.Arg,
                    ReferenceSpace.SdkArg,
                    ReferenceSpace.Result,
                }.FirstOrDefault(
                    s => text != null && text.StartsWith(s, StringComparison.Ordinal));
                if (other != null)
                {
                    throw new InvalidOperationException(
                        "段取りが、置き換える先の無い参照を渡している: " + rowKey + " の " + text);
                }

                return value;
            }

            if (received == null)
            {
                throw new InvalidOperationException(
                    "段取りが呼び出しの相手を指しているが、その行は相手を借りない: " + rowKey);
            }

            taken[path] = received;

            return null;
        }

        /// <summary>
        /// 要素を1つ作って並びへ加える段。作る手立ての無い要素型を指す用意の操作は組み立てない。
        /// </summary>
        private static IEnumerable<E2eCase> Adding(
            SetupOperation operation,
            ToolSchemaTable schemas,
            string rowKey,
            string editKind,
            string path,
            IDictionary<string, string> adders,
            IDictionary<string, string> factories,
            ElementWiring wiring)
        {
            string adding;
            string making;
            if (adders == null
                || operation.ElementType == null
                || !adders.TryGetValue(operation.ElementType, out adding)
                || factories == null
                || !factories.TryGetValue(adding, out making)
                || Of(schemas, adding) == null)
            {
                throw new InvalidOperationException(
                    "並びへ加える手立ての無い要素型を用意の操作が指している: "
                        + operation.ElementType + "(" + rowKey + ")");
            }

            foreach (E2eCase one in Filling(
                rowKey,
                editKind,
                path,
                schemas,
                making,
                adding,
                Scoping(rowKey, adding),
                "段取りが要素を1つ作れること",
                "段取りが作った要素を並びへ加えられること",
                wiring))
            {
                yield return one;
            }
        }

        /// <summary>呼ぶ前の姿を読んで覚える検査。</summary>
        private static E2eCase Recording(
            Postcondition judgement,
            ToolSchema observer,
            string rowKey,
            string editKind,
            string path,
            string receiver)
        {
            return new E2eCase(
                rowKey,
                editKind,
                path,
                judgement.ObserverTool,
                "呼ぶ前の姿を読めること",
                Reading(observer, receiver),
                E2eExpectation.Success,
                null,
                null,
                Remembered(rowKey, judgement),
                Handed(observer, receiver));
        }

        /// <summary>相手をハンドルで読むときに借りる道。並びで読むときは null。</summary>
        private static IDictionary<string, string> Handed(
            ToolSchema observer, string receiver)
        {
            return receiver == null || !Holds(observer)
                ? null
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Leaf(HandlesName), receiver },
                };
        }

        /// <summary>呼んだ後の姿が、呼ぶ前の姿と違うことを確かめる検査。</summary>
        private static E2eCase Changing(
            Postcondition judgement,
            ToolSchema observer,
            string rowKey,
            string editKind,
            string path,
            string receiver)
        {
            return new E2eCase(
                rowKey,
                editKind,
                path,
                judgement.ObserverTool,
                "呼び出しの後に読めるものが、呼ぶ前と違うこと",
                Reading(observer, receiver),
                E2eExpectation.Changed,
                null,
                null,
                null,
                Handed(observer, receiver),
                null,
                null,
                null,
                Remembered(rowKey, judgement),
                judgement.EffectId);
        }

        /// <summary>
        /// 観測ツールが、判定の指す引数を受け取るか。受け取る項目を名前ごとに返す。どの呼び分けも
        /// 受け取らない引数を指す判定は、実機へ投げても断られるだけなので、ここで組み立てを止める。
        /// </summary>
        private static IDictionary<string, SchemaItem> Observed(
            Postcondition judgement, ToolSchemaTable schemas, string rowKey)
        {
            if (judgement.ObserverArgs == null)
            {
                throw new InvalidOperationException(
                    "出たハンドルを引く観測を宣言していない判定は組み立てられない: " + rowKey);
            }

            ToolSchema observer = schemas.Tools.FirstOrDefault(
                t => string.Equals(t.Tool, judgement.ObserverTool, StringComparison.Ordinal));
            Dictionary<string, SchemaItem> taken =
                new Dictionary<string, SchemaItem>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> bound in judgement.ObserverArgs)
            {
                if (!bound.Value.StartsWith(ReferenceSpace.Result, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "出たハンドル以外を観測へ渡す判定は組み立てられない: "
                            + judgement.ObserverTool + "(" + bound.Key + "=" + bound.Value + ")");
                }

                SchemaItem item = observer == null
                    ? null
                    : observer.Branches
                        .SelectMany(b => b.Inputs)
                        .FirstOrDefault(i => !i.Injected
                            && string.Equals(i.Name, bound.Key, StringComparison.Ordinal));
                if (item == null)
                {
                    throw new InvalidOperationException(
                        "観測ツールが、判定の指す引数をどの呼び分けでも受け取らない: "
                            + judgement.ObserverTool + "(" + bound.Key + ")");
                }

                taken[bound.Key] = item;
            }

            return taken;
        }

        /// <summary>
        /// 呼び出しが出したハンドルを観測ツールで引く検査。並びで受け取る引数へは空きを1つ置いて
        /// その中を借り、1つだけ受け取る引数へはその位置を借りる。
        /// </summary>
        private static E2eCase Drawing(
            Postcondition judgement,
            ToolSchemaTable schemas,
            string rowKey,
            string editKind,
            string path,
            string drawn)
        {
            IDictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            IDictionary<string, string> borrowed =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SchemaItem> taken in
                Observed(judgement, schemas, rowKey))
            {
                bool listed = taken.Value.Element != null;
                arguments[taken.Key] = listed ? Slot() : null;
                borrowed[listed ? Leaf(taken.Key) : taken.Key] = drawn;
            }

            ToolSchema observer = schemas.Tools.First(
                t => string.Equals(t.Tool, judgement.ObserverTool, StringComparison.Ordinal));
            if (Named(observer))
            {
                arguments[FieldsName] = new object[] { NameName };
            }

            return new E2eCase(
                rowKey,
                editKind,
                path,
                judgement.ObserverTool,
                "呼び出しが出したハンドルを観測ツールで引けること",
                arguments,
                E2eExpectation.Success,
                null,
                null,
                null,
                borrowed,
                null,
                null,
                null,
                null,
                judgement.EffectId);
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
            IDictionary<SchemaItem, string> handleTargets,
            IDictionary<string, IDictionary<string, object>> given,
            out IDictionary<string, object> arguments)
        {
            if (row.EditKind == ToolMapEditKind.Read)
            {
                return TryReading(schema, sdkShapes, sampled, handleTargets, out arguments);
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

                if (!byName[tool].Branches.Any(b => Fits(b, Receiving(b, call.Arguments))))
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
            IDictionary<SchemaItem, string> handleTargets,
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

            return TryFill(schema, sdkShapes, sampled, handleTargets, null, arguments);
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
            IDictionary<SchemaItem, string> handleTargets,
            IDictionary<string, string> borrows,
            IDictionary<string, object> arguments)
        {
            foreach (SchemaBranch branch in schema.Branches.Where(b => !Skipped(b, arguments)))
            {
                if (!TryFill(branch, sdkShapes, sampled, handleTargets, borrows, arguments))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// その呼び分けを飛ばすか。引数が既に別の呼び分けを選んでいれば飛ばす——選ばれていない
        /// 呼び分けの項目を埋めると、選んだ呼び分けが受け取らない項目を渡すことになり、呼び先
        /// まで届かない。
        /// </summary>
        private static bool Skipped(SchemaBranch branch, IDictionary<string, object> arguments)
        {
            object chosen;

            return branch.SelectorName != null
                && arguments.TryGetValue(branch.SelectorName, out chosen)
                && !Equals(chosen, branch.SelectorValue);
        }

        /// <summary>その呼び分け1つで、必ず要る組を最小の値で埋める。</summary>
        private static bool TryFill(
            SchemaBranch branch,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            IDictionary<SchemaItem, string> handleTargets,
            IDictionary<string, string> borrows,
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
                    if (item == null || !TryMinimal(
                        item, sdkShapes, sampled, handleTargets, borrows, item.Name, out value))
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
                    if (value == null && !TryMinimal(
                        item, sdkShapes, sampled, handleTargets, borrows, item.Name, out value))
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
        /// ハンドルで指す相手を取る項目は、最小の値では埋めない——番号を書けば綴りには合うが、
        /// 台帳が預かる相手を指さないので、呼び先まで届かないまま届いたことにしてしまう。この
        /// 項目の扱いは <paramref name="borrows"/> で分かれる。null なら偽——借りる先を持たない
        /// 呼び出しでは、渡すものが決まらない。渡してあれば、その項目の道をSDKの型名へ結んで
        /// <paramref name="borrows"/> へ置き、<paramref name="value"/> を null のまま真を返す。
        /// 値は、その道へ借りる側が入れる。
        /// </summary>
        private static bool TryMinimal(
            SchemaItem item,
            IDictionary<SchemaItem, string> sdkShapes,
            IDictionary<SchemaItem, object> sampled,
            IDictionary<SchemaItem, string> handleTargets,
            IDictionary<string, string> borrows,
            string path,
            out object value)
        {
            value = null;
            string wanted;
            if (handleTargets.TryGetValue(item, out wanted))
            {
                if (borrows == null)
                {
                    return false;
                }

                borrows[path] = wanted;

                return true;
            }

            if (item.Members != null)
            {
                Dictionary<string, object> members =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (SchemaItem member in item.Members.Where(m => m.Required == true))
                {
                    object one;
                    if (!TryMinimal(
                        member, sdkShapes, sampled, handleTargets, borrows,
                        path + PathStep + member.Name, out one))
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
                if (!TryMinimal(
                    item.Element, sdkShapes, sampled, handleTargets, borrows,
                    path + PathStep + FirstPosition.ToString(CultureInfo.InvariantCulture),
                    out one))
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
        internal static SchemaBranch Unchosen(ToolSchema schema)
        {
            if (schema == null)
            {
                return null;
            }

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

        /// <summary>
        /// ハンドルで指す相手を値に取る項目。番号で書くが、どの番号を書いても台帳が預かる相手を
        /// 指さないので、借りずに埋めることはできない。
        /// </summary>
        private static IDictionary<SchemaItem, string> HandleTargets(
            IDictionary<SchemaItem, string> sdkTypes, ISet<string> handled)
        {
            Dictionary<SchemaItem, string> items = new Dictionary<SchemaItem, string>();
            if (sdkTypes == null || handled == null)
            {
                return items;
            }

            foreach (KeyValuePair<SchemaItem, string> typed in sdkTypes)
            {
                if (handled.Contains(typed.Value))
                {
                    items[typed.Key] = typed.Value;
                }
            }

            return items;
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
            IDictionary<string, ISet<string>> unkept,
            IDictionary<string, string> factories,
            IDictionary<string, string> parents,
            IDictionary<string, string> addersByTool,
            IDictionary<string, string> addersByType,
            ElementWiring wiring)
        {
            string rowKey = row == null ? string.Empty : row.SignatureKey;
            if (sdkTypes == null || positioned == null || dangerous.Contains(rowKey))
            {
                yield break;
            }

            // 同じ並びを二度用意しない。用意の段はどの検査より先に置くので、出した相手を覚える。
            ISet<string> prepared = new HashSet<string>(StringComparer.Ordinal);

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
                    // 先頭を指して書くには、指す先の並びと書かれる側の並びの両方に1つでも
                    // 要る。書かれる側が空だと、全件を指しても1件も触らずに済んでしまい、位置が
                    // 範囲内かどうかを確かめたことにならない。読み返すのにも書かれる側が要る。
                    // 用意できない並びでは、その並びを要する検査を組み立てない——通らない検査を
                    // 出すと、落ちた理由が別の系統として数えられる。
                    IList<string> pointed = Filled(
                        typeName, addersByType, parents, factories, schemas);
                    IList<string> written = Filled(
                        schema.Tool, addersByTool, parents, factories, schemas);
                    bool writes = pointed != null && written != null;
                    E2eCase read = written == null || !Kept(unkept, schema.Tool, member.Name)
                        ? null
                        : ReadBackCase(
                            rowKey, editKind, path, schema.Tool, schemas, readers, member.Name);
                    foreach (string filler in (writes ? pointed : new string[0])
                        .Concat(writes || read != null ? written : new string[0]))
                    {
                        if (!prepared.Add(filler))
                        {
                            continue;
                        }

                        foreach (E2eCase one in Filling(
                            rowKey,
                            editKind,
                            path,
                            schemas,
                            factories[filler],
                            filler,
                            Scoping(schema.Tool, filler),
                            "位置で指す検査の前に並びへ加える要素を1つ作れること",
                            "作った要素を位置で指す検査の前に並びへ加えられること",
                            wiring))
                        {
                            yield return one;
                        }
                    }

                    if (writes)
                    {
                        yield return new E2eCase(
                            rowKey,
                            editKind,
                            path,
                            schema.Tool,
                            "位置で指す項目へ並びの先頭を書けること",
                            Pointing(arguments, member.Name, FirstPosition),
                            E2eExpectation.Success,
                            null);
                    }

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

        /// <summary>
        /// 対象を指す組はそのままに、位置で指す項目をどれも並びの先頭へ向ける引数。
        /// </summary>
        private static IDictionary<string, object> Pointing(
            IDictionary<string, object> pointing, IEnumerable<string> members)
        {
            IDictionary<string, object> arguments =
                new Dictionary<string, object>(pointing, StringComparer.Ordinal);
            IDictionary<string, object> value =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (string member in members)
            {
                value[member] = FirstPosition;
            }

            arguments[ValueName] = value;

            return arguments;
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
