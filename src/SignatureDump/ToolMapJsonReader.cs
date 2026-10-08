using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>能力対応表の正本を読む。</summary>
    public static class ToolMapJsonReader
    {
        private const string RowsName = "rows";

        private const string ToolSetupsName = "toolSetups";

        private const string SignatureKeyName = "signatureKey";

        private const string EditKindName = "editKind";

        private const string UpdateSpecName = "updateSpec";

        private const string BasisName = "basis";

        private const string ToolName = "tool";

        private const string PostconditionName = "postcondition";

        private const string EventTypeName = "eventType";

        private const string EmbeddedInName = "embeddedIn";

        private const string UpdateName = "update";

        private const string RefreshName = "refresh";

        private const string EffectTypeName = "effectType";

        private const string EffectKeyName = "effectKey";

        private const string KindName = "kind";

        private const string ObserverToolName = "observerTool";

        private const string ObserverArgsName = "observerArgs";

        private const string ValuePathName = "valuePath";

        private const string ComparisonName = "comparison";

        private const string ExpectedName = "expected";

        private const string SetupName = "setup";

        private const string ArgumentTypesName = "argumentTypes";

        private const string TagName = "tag";

        private const string ElementTypeName = "elementType";

        private const string ArgsName = "args";

        private const string OutName = "out";

        private static readonly JsonForm Setup = JsonForm.Array(
            JsonForm.Object(
                JsonForm.Member(TagName, JsonForm.Text()),
                JsonForm.Optional(ElementTypeName, JsonForm.Text()),
                JsonForm.Optional(ToolName, JsonForm.Text()),
                JsonForm.Optional(ArgsName, JsonForm.Map(JsonForm.Any(), allowEmpty: true)),
                JsonForm.Optional(OutName, JsonForm.Text())));

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                RowsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(SignatureKeyName, JsonForm.Text()),
                        JsonForm.Member(EditKindName, JsonForm.Text()),
                        JsonForm.Member(BasisName, JsonForm.Text()),
                        JsonForm.Optional(
                            UpdateSpecName,
                            JsonForm.Object(
                                JsonForm.Member(
                                    RefreshName, JsonForm.Array(JsonForm.Text(), allowEmpty: true)),
                                JsonForm.Optional(UpdateName, JsonForm.Text()))),
                        JsonForm.Optional(
                            PostconditionName,
                            JsonForm.Array(
                                JsonForm.Object(
                                    JsonForm.Member(EffectTypeName, JsonForm.Text()),
                                    JsonForm.Member(EffectKeyName, JsonForm.TextOrEmpty()),
                                    JsonForm.Member(KindName, JsonForm.Text()),
                                    JsonForm.Member(ComparisonName, JsonForm.Text()),
                                    JsonForm.Optional(ObserverToolName, JsonForm.Text()),
                                    JsonForm.Optional(
                                        ObserverArgsName,
                                        JsonForm.Map(JsonForm.Text(), allowEmpty: true)),
                                    JsonForm.Optional(ValuePathName, JsonForm.TextOrEmpty()),
                                    JsonForm.Optional(ExpectedName, JsonForm.Any()),
                                    JsonForm.Optional(SetupName, Setup)))),
                        JsonForm.Optional(EventTypeName, JsonForm.Text()),
                        JsonForm.Optional(EmbeddedInName, JsonForm.Array(JsonForm.Text())),
                        JsonForm.Optional(SetupName, Setup),
                        JsonForm.Optional(ArgumentTypesName, JsonForm.Map(JsonForm.Text()))),
                    SignatureKeyName,
                    allowEmpty: true)),
            JsonForm.Optional(
                ToolSetupsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(ToolName, JsonForm.Text()),
                        JsonForm.Member(SetupName, Setup)),
                    ToolName,
                    allowEmpty: true)));

        private static readonly Regex SnakeCaseName = new Regex(
            "^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        private static readonly Regex MemberName = new Regex(
            "^[a-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);

        private static readonly Regex EnumeratorName = new Regex(
            "^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);

        /// <summary>SDKの引数の名前。名前を決めるのはSDKの側なので、識別子の形までとする。</summary>
        private static readonly Regex SdkArgumentName = new Regex(
            "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        /// <summary>参照元の射影。組の配列を受け取る引数の内側だけを指せる。</summary>
        private static readonly Regex ArgumentReference = new Regex(
            "^[a-z][A-Za-z0-9]*(\\[\\]\\.[a-z][A-Za-z0-9]*(\\[\\])?)?$",
            RegexOptions.CultureInvariant);

        /// <summary>比べる値の位置。応答の値そのもの・直下の項目・一覧からの配列射影の3つ。</summary>
        private static readonly Regex ValuePath = new Regex(
            "^(|[a-z][A-Za-z0-9]*|items\\[\\]\\.[a-z][A-Za-z0-9]*)$", RegexOptions.CultureInvariant);

        /// <summary>
        /// 用意の操作が取れるサンプル値への参照。型名は公開API列挙の表記なので、総称型の山括弧と
        /// 配列の角括弧を含む。
        /// </summary>
        private static readonly Regex SampleReference = new Regex(
            "^sample2?:[A-Za-z][A-Za-z0-9_.+<>,\\[\\]]*$", RegexOptions.CultureInvariant);

        /// <summary>編集の流れの綴り。正本が持つ綴りをそのまま返す。</summary>
        public static string SpellingOf(ToolMapEditKind editKind)
        {
            foreach (KeyValuePair<string, ToolMapEditKind> pair in EditKinds)
            {
                if (pair.Value == editKind)
                {
                    return pair.Key;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(editKind), editKind, "綴りを持たない編集の流れ。");
        }

        private static readonly Dictionary<string, ToolMapEditKind> EditKinds =
            new Dictionary<string, ToolMapEditKind>(StringComparer.Ordinal)
            {
                { "duplicateEdit", ToolMapEditKind.DuplicateEdit },
                { "directChange", ToolMapEditKind.DirectChange },
                { "viewSession", ToolMapEditKind.ViewSession },
                { "read", ToolMapEditKind.Read },
            };

        private static readonly Dictionary<string, RefreshTarget> RefreshTargets =
            new Dictionary<string, RefreshTarget>(StringComparer.Ordinal)
            {
                { "model", RefreshTarget.Model },
                { "list", RefreshTarget.List },
                { "view", RefreshTarget.View },
            };

        private static readonly Dictionary<string, EffectType> EffectTypes =
            new Dictionary<string, EffectType>(StringComparer.Ordinal)
            {
                { "fileWritten", EffectType.FileWritten },
                { "handleConsumed", EffectType.HandleConsumed },
                { "handleCascaded", EffectType.HandleCascaded },
                { "handleCreated", EffectType.HandleCreated },
                { "countChanged", EffectType.CountChanged },
                { "stateWritten", EffectType.StateWritten },
                { "observableChange", EffectType.ObservableChange },
                { "valueRead", EffectType.ValueRead },
                { "shutdownRequested", EffectType.ShutdownRequested },
                { "none", EffectType.None },
            };

        private static readonly Dictionary<string, EffectCheckKind> CheckKinds =
            new Dictionary<string, EffectCheckKind>(StringComparer.Ordinal)
            {
                { "readback", EffectCheckKind.Readback },
                { "file", EffectCheckKind.File },
                { "handle", EffectCheckKind.Handle },
                { "callLogOnly", EffectCheckKind.CallLogOnly },
            };

        private static readonly Dictionary<string, EffectComparison> Comparisons =
            new Dictionary<string, EffectComparison>(StringComparer.Ordinal)
            {
                { "equals", EffectComparison.Equals },
                { "exists", EffectComparison.Exists },
                { "invalidated", EffectComparison.Invalidated },
                { "deltaEquals", EffectComparison.DeltaEquals },
                { "anyChanged", EffectComparison.AnyChanged },
            };

        private static readonly Dictionary<string, SetupTag> SetupTags =
            new Dictionary<string, SetupTag>(StringComparer.Ordinal)
            {
                { "initPmx", SetupTag.InitPmx },
                { "addElement", SetupTag.AddElement },
                { "callTool", SetupTag.CallTool },
            };

        /// <summary>参照元の名前空間。接頭辞で見分ける。</summary>
        private static readonly Dictionary<string, Func<string, bool>> ReferenceSpaces =
            new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
            {
                { ReferenceSpace.Arg, rest => ArgumentReference.IsMatch(rest) },
                { ReferenceSpace.SdkArg, rest => SdkArgumentName.IsMatch(rest) },
                { ReferenceSpace.Result, rest => ValuePath.IsMatch(rest) },
                { ReferenceSpace.SetupOut, rest => MemberName.IsMatch(rest) },
                { ReferenceSpace.Receiver, rest => rest.Length == 0 },
            };

        /// <summary>
        /// 行を書かれた順に返す。行キーが序数の昇順に重複なく並ぶことと、行だけで決まる項目の
        /// 要否——複製編集型の行が反映の指定を持つこと——を求める。種別ごとの要否は行の外の材料が
        /// 要るので照合が見る。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static ToolMap Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            List<ToolMapRow> rows = Items(root[RowsName]).Select(ReadRow).ToList();

            return new ToolMap(rows, ReadToolSetups(root));
        }

        /// <summary>ツールの名前から、そのツールを呼ぶ前の段取りへ。無ければ空。</summary>
        private static IDictionary<string, IList<SetupOperation>> ReadToolSetups(
            IDictionary<string, object> root)
        {
            Dictionary<string, IList<SetupOperation>> setups =
                new Dictionary<string, IList<SetupOperation>>(StringComparer.Ordinal);
            object items;
            if (!root.TryGetValue(ToolSetupsName, out items))
            {
                return setups;
            }

            foreach (IDictionary<string, object> members in Items(items))
            {
                setups[Name((string)members[ToolName], ToolName)] = ReadSetup(members[SetupName]);
            }

            return setups;
        }

        private static ToolMapRow ReadRow(IDictionary<string, object> members)
        {
            ToolMapEditKind editKind = Lookup(EditKinds, (string)members[EditKindName], EditKindName);
            RequirePresence(members, UpdateSpecName, editKind == ToolMapEditKind.DuplicateEdit);

            return new ToolMapRow(
                (string)members[SignatureKeyName],
                editKind,
                members.ContainsKey(UpdateSpecName)
                    ? ReadUpdateSpec((IDictionary<string, object>)members[UpdateSpecName])
                    : null,
                (string)members[BasisName],
                members.ContainsKey(PostconditionName)
                    ? ReadPostcondition(members[PostconditionName])
                    : null,
                members.ContainsKey(EventTypeName) ? (string)members[EventTypeName] : null,
                members.ContainsKey(EmbeddedInName) ? ReadEmbeddedIn(members[EmbeddedInName]) : null,
                members.ContainsKey(SetupName) ? ReadSetup(members[SetupName]) : null,
                members.ContainsKey(ArgumentTypesName)
                    ? ReadArgumentTypes((IDictionary<string, object>)members[ArgumentTypesName])
                    : null);
        }

        private static void RequirePresence(
            IDictionary<string, object> members, string name, bool required)
        {
            if (required && !members.ContainsKey(name))
            {
                throw new FormatException("項目が無い: " + name);
            }

            if (!required && members.ContainsKey(name))
            {
                throw new FormatException("この行が持てない項目がある: " + name);
            }
        }

        private static IList<string> ReadEmbeddedIn(object value)
        {
            List<string> names = new List<string>();
            foreach (string name in ((object[])value).Cast<string>())
            {
                if (names.Contains(name, StringComparer.Ordinal))
                {
                    throw new FormatException("同じ埋め込み先が二度現れる: " + name);
                }

                names.Add(name);
            }

            return names;
        }

        private static UpdateSpec ReadUpdateSpec(IDictionary<string, object> members)
        {
            List<RefreshTarget> refresh = new List<RefreshTarget>();
            foreach (string item in ((object[])members[RefreshName]).Cast<string>())
            {
                RefreshTarget target = Lookup(RefreshTargets, item, RefreshName);
                if (refresh.Contains(target))
                {
                    throw new FormatException("同じ表示更新が二度現れる: " + target);
                }

                refresh.Add(target);
            }

            string update = null;
            if (members.ContainsKey(UpdateName))
            {
                update = (string)members[UpdateName];
                if (!EnumeratorName.IsMatch(update))
                {
                    throw new FormatException("反映の指定が列挙子の名前でない: " + update);
                }
            }

            return new UpdateSpec(update, refresh);
        }

        private static IList<Postcondition> ReadPostcondition(object value)
        {
            List<Postcondition> judgements = new List<Postcondition>();
            List<string> seen = new List<string>();
            foreach (IDictionary<string, object> item in Items(value))
            {
                Postcondition judgement = ReadJudgement(item);
                if (seen.Contains(judgement.EffectId, StringComparer.Ordinal))
                {
                    throw new FormatException("同じ効果が二度現れる: " + judgement.EffectId);
                }

                seen.Add(judgement.EffectId);
                judgements.Add(judgement);
            }

            return judgements;
        }

        private static Postcondition ReadJudgement(IDictionary<string, object> members)
        {
            EffectType effectType =
                Lookup(EffectTypes, (string)members[EffectTypeName], EffectTypeName);
            EffectCheckKind kind = Lookup(CheckKinds, (string)members[KindName], KindName);
            string effectKey = (string)members[EffectKeyName];
            if (kind == EffectCheckKind.File && effectKey.Length == 0)
            {
                throw new FormatException(
                    "ファイルの生成を見る判定は、確かめるパスを取る引数を " + EffectKeyName
                        + " で指さなければならない。");
            }

            EffectComparison comparison = Lookup(
                Comparisons, (string)members[ComparisonName], ComparisonName);

            bool observed = (kind == EffectCheckKind.Readback || kind == EffectCheckKind.Handle)
                && comparison != EffectComparison.AnyChanged;

            // 呼ぶ前と後で読み比べる判定も、どの一覧を読むかは述べる。渡すものは述べない——
            // 一覧の全体を読むので、指す対象が無い。
            bool compared = kind == EffectCheckKind.Readback
                && comparison == EffectComparison.AnyChanged;
            RequirePresence(members, ObserverToolName, observed || compared);
            RequirePresence(members, ObserverArgsName, observed);
            RequirePresence(
                members,
                ValuePathName,
                kind == EffectCheckKind.Readback && comparison != EffectComparison.AnyChanged);
            RequirePresence(
                members,
                ExpectedName,
                comparison == EffectComparison.Equals || comparison == EffectComparison.DeltaEquals);
            RequirePresence(
                members,
                SetupName,
                effectType == EffectType.ObservableChange || effectType == EffectType.ValueRead);

            object expected = null;
            if (members.ContainsKey(ExpectedName))
            {
                expected = ReadExpected(members[ExpectedName], comparison);
            }

            Postcondition judgement = new Postcondition(
                effectType,
                effectKey,
                kind,
                members.ContainsKey(ObserverToolName)
                    ? Name((string)members[ObserverToolName], ObserverToolName)
                    : null,
                members.ContainsKey(ObserverArgsName)
                    ? ReadObserverArgs((IDictionary<string, object>)members[ObserverArgsName])
                    : null,
                members.ContainsKey(ValuePathName)
                    ? Path((string)members[ValuePathName], ValuePathName)
                    : null,
                comparison,
                expected,
                members.ContainsKey(ExpectedName),
                members.ContainsKey(SetupName) ? ReadSetup(members[SetupName]) : null);
            RequireOutputsExist(judgement);

            return judgement;
        }

        /// <summary>
        /// 用意の操作が出した値への参照が、同じ判定の操作が出した名前を指すことを求める。名前で引く
        /// ので、出していない名前を指すと束縛が決まらない。操作の引数が指せるのは、その操作より前が
        /// 出した名前に限る——列は順に実行するので、後で出す名前は先の操作から引けない。
        /// </summary>
        private static void RequireOutputsExist(Postcondition judgement)
        {
            List<string> produced = new List<string>();
            foreach (SetupOperation operation in judgement.Setup ?? new SetupOperation[0])
            {
                if (operation.Args != null)
                {
                    RequireProduced(operation.Args.Values.OfType<string>(), produced);
                }

                if (operation.Out != null)
                {
                    produced.Add(operation.Out);
                }
            }

            RequireProduced(judgement.Bound, produced);
        }

        private static void RequireProduced(IEnumerable<string> bound, IList<string> produced)
        {
            foreach (string reference in bound
                .Where(r => r.StartsWith(ReferenceSpace.SetupOut, StringComparison.Ordinal)))
            {
                string name = reference.Substring(ReferenceSpace.SetupOut.Length);
                if (!produced.Contains(name, StringComparer.Ordinal))
                {
                    throw new FormatException("用意の操作が出していない名前を指している: " + reference);
                }
            }
        }

        /// <summary>期待はJSONのリテラルか参照元1つ。</summary>
        private static object ReadExpected(object value, EffectComparison comparison)
        {
            string text = value as string;
            if (text != null && ReferenceSpaces.Keys.Any(p => text.StartsWith(p, StringComparison.Ordinal)))
            {
                RequireReference(text, ExpectedName);
                return text;
            }

            if (comparison == EffectComparison.DeltaEquals && !(value is int || value is long
                || value is double || value is decimal))
            {
                throw new FormatException("差の期待は数値でなければならない。");
            }

            if (value is Dictionary<string, object> || value is object[])
            {
                RequireLiteral(value);
            }

            return value;
        }

        /// <summary>
        /// 呼ぶツールへ渡す値1つ。参照の綴りは入れ子の中でも参照として見る——組や配列の中へ
        /// 書いた参照を素通りさせると、綴りを誤ったものが検査に掛からないまま呼び先へ渡る。
        /// </summary>
        private static void RequireGiven(object value)
        {
            object[] items = value as object[];
            if (items != null)
            {
                foreach (object item in items)
                {
                    RequireGiven(item);
                }

                return;
            }

            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members != null)
            {
                foreach (KeyValuePair<string, object> pair in members)
                {
                    if (!MemberName.IsMatch(pair.Key))
                    {
                        throw new FormatException("呼ぶツールの引数の名前でない: " + pair.Key);
                    }

                    RequireGiven(pair.Value);
                }

                return;
            }

            string text = value as string;
            if (text == null)
            {
                return;
            }

            if (ReferenceSpaces.Keys.Any(p => text.StartsWith(p, StringComparison.Ordinal)))
            {
                RequireReference(text, ArgsName);

                return;
            }

            if (text.StartsWith("sample", StringComparison.Ordinal)
                && !SampleReference.IsMatch(text))
            {
                throw new FormatException("サンプル値への参照の形でない: " + text);
            }
        }

        /// <summary>期待のリテラルは値だけで、演算も合成も持たない。入れ子の中も同じとする。</summary>
        private static void RequireLiteral(object value)
        {
            object[] items = value as object[];
            if (items != null)
            {
                foreach (object item in items)
                {
                    RequireLiteral(item);
                }

                return;
            }

            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in members)
            {
                if (!MemberName.IsMatch(pair.Key))
                {
                    throw new FormatException("期待の項目名が値の項目の名前でない: " + pair.Key);
                }

                RequireLiteral(pair.Value);
            }
        }

        private static IDictionary<string, string> ReadObserverArgs(
            IDictionary<string, object> members)
        {
            Dictionary<string, string> bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in members)
            {
                if (!MemberName.IsMatch(pair.Key))
                {
                    throw new FormatException("観測ツールの引数の名前でない: " + pair.Key);
                }

                string reference = (string)pair.Value;
                RequireReference(reference, ObserverArgsName);
                bindings.Add(pair.Key, reference);
            }

            return bindings;
        }

        private static IList<SetupOperation> ReadSetup(object value)
        {
            List<SetupOperation> operations = new List<SetupOperation>();
            List<string> names = new List<string>();
            foreach (IDictionary<string, object> item in Items(value))
            {
                SetupOperation operation = ReadSetupOperation(item);
                if (operation.Out != null)
                {
                    if (names.Contains(operation.Out, StringComparer.Ordinal))
                    {
                        throw new FormatException("用意の操作が同じ名前を二度出す: " + operation.Out);
                    }

                    names.Add(operation.Out);
                }

                operations.Add(operation);
            }

            return operations;
        }

        /// <summary>引数の名前から、そこへ渡す相手の型へ。</summary>
        private static IDictionary<string, string> ReadArgumentTypes(
            IDictionary<string, object> members)
        {
            Dictionary<string, string> types =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> one in members)
            {
                types[Name(one.Key, ArgumentTypesName)] = (string)one.Value;
            }

            return types;
        }

        private static SetupOperation ReadSetupOperation(IDictionary<string, object> members)
        {
            SetupTag tag = Lookup(SetupTags, (string)members[TagName], TagName);
            RequirePresence(members, ElementTypeName, tag == SetupTag.AddElement);
            RequirePresence(members, ToolName, tag == SetupTag.CallTool);
            RequirePresence(members, ArgsName, tag == SetupTag.CallTool);
            if (tag == SetupTag.InitPmx && members.ContainsKey(OutName))
            {
                throw new FormatException("値を出さない用意の操作が " + OutName + " を持つ。");
            }

            string outName = members.ContainsKey(OutName) ? (string)members[OutName] : null;
            if (outName != null && !MemberName.IsMatch(outName))
            {
                throw new FormatException("用意の操作が出す値の名前でない: " + outName);
            }

            switch (tag)
            {
                case SetupTag.InitPmx:
                    return SetupOperation.InitPmx();
                case SetupTag.AddElement:
                    return SetupOperation.AddElement(
                        Name((string)members[ElementTypeName], ElementTypeName), outName);
                default:
                    return SetupOperation.CallTool(
                        Name((string)members[ToolName], ToolName),
                        ReadSetupArgs((IDictionary<string, object>)members[ArgsName]),
                        outName);
            }
        }

        /// <summary>
        /// 呼ぶときの束縛。参照元とJSONのリテラルに加えて、型ごとに定めたサンプル値を指せる。
        /// </summary>
        private static IDictionary<string, object> ReadSetupArgs(IDictionary<string, object> members)
        {
            Dictionary<string, object> args = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in members)
            {
                if (!MemberName.IsMatch(pair.Key))
                {
                    throw new FormatException("呼ぶツールの引数の名前でない: " + pair.Key);
                }

                RequireGiven(pair.Value);

                args.Add(pair.Key, pair.Value);
            }

            return args;
        }

        private static void RequireReference(string text, string name)
        {
            foreach (KeyValuePair<string, Func<string, bool>> space in ReferenceSpaces)
            {
                if (!text.StartsWith(space.Key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!space.Value(text.Substring(space.Key.Length)))
                {
                    throw new FormatException("参照元の形でない: " + text);
                }

                return;
            }

            throw new FormatException(name + " は参照元でなければならない: " + text);
        }

        private static string Path(string text, string name)
        {
            if (!ValuePath.IsMatch(text))
            {
                throw new FormatException(name + " が比べる値の位置の形でない。");
            }

            return text;
        }

        private static string Name(string text, string name)
        {
            if (!SnakeCaseName.IsMatch(text))
            {
                throw new FormatException(
                    name + " は小文字で始まり、小文字と数字と下線だけからなる語でなければならない: "
                        + text);
            }

            return text;
        }

        private static TValue Lookup<TValue>(
            Dictionary<string, TValue> table, string text, string name)
        {
            TValue found;
            if (!table.TryGetValue(text, out found))
            {
                throw new FormatException("知らない " + name + ": " + text);
            }

            return found;
        }

        private static IEnumerable<IDictionary<string, object>> Items(object value)
        {
            return ((object[])value).Cast<IDictionary<string, object>>();
        }
    }
}
