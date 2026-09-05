using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>能力対応表の正本を読む。</summary>
    public static class ToolMapJsonReader
    {
        private const string RowsName = "rows";

        private const string SignatureKeyName = "signatureKey";

        private const string CapabilityIdsName = "capabilityIds";

        private const string RowKindName = "rowKind";

        private const string EditKindName = "editKind";

        private const string DirectionName = "direction";

        private const string DangerKindName = "dangerKind";

        private const string UpdateSpecName = "updateSpec";

        private const string NoteName = "note";

        private const string BasisName = "basis";

        private const string ToolName = "tool";

        private const string PostconditionName = "postcondition";

        private const string AssignmentName = "assignment";

        private const string TargetName = "target";

        private const string SlotBindingName = "slotBinding";

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

        private const string TagName = "tag";

        private const string ElementTypeName = "elementType";

        private const string ArgsName = "args";

        private const string OutName = "out";

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

        private static readonly Dictionary<string, ToolMapRowKind> RowKinds =
            new Dictionary<string, ToolMapRowKind>(StringComparer.Ordinal)
            {
                { "commonContract", ToolMapRowKind.CommonContract },
                { "eventBranch", ToolMapRowKind.EventBranch },
                { "schemaEmbedded", ToolMapRowKind.SchemaEmbedded },
                { "directDispatch", ToolMapRowKind.DirectDispatch },
            };

        private static readonly Dictionary<string, ToolMapEditKind> EditKinds =
            new Dictionary<string, ToolMapEditKind>(StringComparer.Ordinal)
            {
                { "duplicateEdit", ToolMapEditKind.DuplicateEdit },
                { "directChange", ToolMapEditKind.DirectChange },
                { "viewSession", ToolMapEditKind.ViewSession },
                { "read", ToolMapEditKind.Read },
            };

        private static readonly Dictionary<string, OperationDirection> Directions =
            new Dictionary<string, OperationDirection>(StringComparer.Ordinal)
            {
                { "read", OperationDirection.Read },
                { "write", OperationDirection.Write },
            };

        private static readonly Dictionary<string, DangerKind> DangerKinds =
            new Dictionary<string, DangerKind>(StringComparer.Ordinal)
            {
                { "shutdown", DangerKind.Shutdown },
                { "overwrite", DangerKind.Overwrite },
                { "reset", DangerKind.Reset },
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
            };

        /// <summary>
        /// 行を書かれた順に返す。行キーが序数の昇順に重複なく並ぶことと、行の種別ごとの項目が
        /// そろっていることを求める。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static ToolMap Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object parsed;
            try
            {
                parsed = new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("JSONとして読めない。", exception);
            }

            List<ToolMapRow> rows = new List<ToolMapRow>();
            string previous = null;
            foreach (object item in Array(Members(parsed, RowsName)[RowsName], RowsName))
            {
                ToolMapRow row = ReadRow(item);
                RequireAscending(previous, row.SignatureKey);
                previous = row.SignatureKey;
                rows.Add(row);
            }

            return new ToolMap(rows);
        }

        private static ToolMapRow ReadRow(object item)
        {
            Dictionary<string, object> members = Members(
                item,
                new[]
                {
                    SignatureKeyName, CapabilityIdsName, RowKindName, EditKindName,
                    DirectionName, BasisName,
                },
                new[]
                {
                    DangerKindName, UpdateSpecName, NoteName, ToolName, PostconditionName,
                    AssignmentName, TargetName, SlotBindingName, EventTypeName, EmbeddedInName,
                });

            ToolMapRowKind rowKind = Lookup(RowKinds, members[RowKindName], RowKindName);
            ToolMapEditKind editKind = Lookup(EditKinds, members[EditKindName], EditKindName);
            CommonAssignmentKind? assignment = members.ContainsKey(AssignmentName)
                ? CommonAssignmentJsonReader.ReadAssignmentKind(members[AssignmentName])
                : (CommonAssignmentKind?)null;
            RequirePresence(members, UpdateSpecName, editKind == ToolMapEditKind.DuplicateEdit);
            foreach (KeyValuePair<string, bool> pair in FieldsOf(rowKind))
            {
                RequirePresence(members, pair.Key, pair.Value);
            }

            return new ToolMapRow(
                Text(members[SignatureKeyName], SignatureKeyName),
                ReadCapabilityIds(members[CapabilityIdsName]),
                rowKind,
                editKind,
                Lookup(Directions, members[DirectionName], DirectionName),
                members.ContainsKey(DangerKindName)
                    ? Lookup(DangerKinds, members[DangerKindName], DangerKindName)
                    : (DangerKind?)null,
                members.ContainsKey(UpdateSpecName) ? ReadUpdateSpec(members[UpdateSpecName]) : null,
                members.ContainsKey(NoteName) ? Text(members[NoteName], NoteName) : null,
                Text(members[BasisName], BasisName),
                members.ContainsKey(ToolName) ? Name(members[ToolName], ToolName) : null,
                members.ContainsKey(PostconditionName)
                    ? ReadPostcondition(members[PostconditionName])
                    : null,
                assignment,
                members.ContainsKey(TargetName)
                    ? CommonAssignmentJsonReader.ReadAssignmentTarget(
                        members[TargetName], assignment.Value)
                    : null,
                members.ContainsKey(SlotBindingName)
                    ? CommonAssignmentJsonReader.ReadSlotBinding(members[SlotBindingName])
                    : null,
                members.ContainsKey(EventTypeName) ? Text(members[EventTypeName], EventTypeName) : null,
                members.ContainsKey(EmbeddedInName) ? ReadEmbeddedIn(members[EmbeddedInName]) : null);
        }

        /// <summary>行の種別ごとに、持たなければならない項目と持ってはならない項目。</summary>
        private static IEnumerable<KeyValuePair<string, bool>> FieldsOf(ToolMapRowKind rowKind)
        {
            bool hasTool = rowKind == ToolMapRowKind.DirectDispatch;
            yield return new KeyValuePair<string, bool>(ToolName, hasTool);
            yield return new KeyValuePair<string, bool>(PostconditionName, hasTool);
            bool isCommon = rowKind == ToolMapRowKind.CommonContract;
            yield return new KeyValuePair<string, bool>(AssignmentName, isCommon);
            yield return new KeyValuePair<string, bool>(TargetName, isCommon);
            yield return new KeyValuePair<string, bool>(SlotBindingName, isCommon);
            yield return new KeyValuePair<string, bool>(
                EventTypeName, rowKind == ToolMapRowKind.EventBranch);
            yield return new KeyValuePair<string, bool>(
                EmbeddedInName, rowKind == ToolMapRowKind.SchemaEmbedded);
        }

        private static void RequirePresence(
            Dictionary<string, object> members, string name, bool required)
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

        private static IList<string> ReadCapabilityIds(object value)
        {
            List<string> ids = new List<string>();
            foreach (object item in Array(value, CapabilityIdsName))
            {
                string id = Text(item, CapabilityIdsName);
                if (ids.Contains(id, StringComparer.Ordinal))
                {
                    throw new FormatException("同じ提供能力のIDが二度現れる: " + id);
                }

                ids.Add(id);
            }

            if (ids.Count == 0)
            {
                throw new FormatException(CapabilityIdsName + " は1件以上でなければならない。");
            }

            return ids;
        }

        private static IList<string> ReadEmbeddedIn(object value)
        {
            List<string> names = new List<string>();
            foreach (object item in Array(value, EmbeddedInName))
            {
                string name = Text(item, EmbeddedInName);
                if (names.Contains(name, StringComparer.Ordinal))
                {
                    throw new FormatException("同じ埋め込み先が二度現れる: " + name);
                }

                names.Add(name);
            }

            if (names.Count == 0)
            {
                throw new FormatException(EmbeddedInName + " は1件以上でなければならない。");
            }

            return names;
        }

        private static UpdateSpec ReadUpdateSpec(object value)
        {
            Dictionary<string, object> members = Members(
                value, new[] { RefreshName }, new[] { UpdateName });
            List<RefreshTarget> refresh = new List<RefreshTarget>();
            foreach (object item in Array(members[RefreshName], RefreshName))
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
                update = Text(members[UpdateName], UpdateName);
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
            foreach (object item in Array(value, PostconditionName))
            {
                Postcondition judgement = ReadJudgement(item);
                if (seen.Contains(judgement.EffectId, StringComparer.Ordinal))
                {
                    throw new FormatException("同じ効果が二度現れる: " + judgement.EffectId);
                }

                seen.Add(judgement.EffectId);
                judgements.Add(judgement);
            }

            if (judgements.Count == 0)
            {
                throw new FormatException(PostconditionName + " は1件以上でなければならない。");
            }

            return judgements;
        }

        private static Postcondition ReadJudgement(object item)
        {
            Dictionary<string, object> members = Members(
                item,
                new[] { EffectTypeName, EffectKeyName, KindName, ComparisonName },
                new[] { ObserverToolName, ObserverArgsName, ValuePathName, ExpectedName, SetupName });

            EffectType effectType = Lookup(EffectTypes, members[EffectTypeName], EffectTypeName);
            EffectCheckKind kind = Lookup(CheckKinds, members[KindName], KindName);
            string effectKey = EffectKey(members[EffectKeyName]);
            if (kind == EffectCheckKind.File && effectKey.Length == 0)
            {
                throw new FormatException(
                    "ファイルの生成を見る判定は、確かめるパスを取る引数を " + EffectKeyName
                        + " で指さなければならない。");
            }

            EffectComparison comparison = Lookup(
                Comparisons, members[ComparisonName], ComparisonName);

            bool observed = (kind == EffectCheckKind.Readback || kind == EffectCheckKind.Handle)
                && comparison != EffectComparison.AnyChanged;
            RequirePresence(members, ObserverToolName, observed);
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
                    ? Name(members[ObserverToolName], ObserverToolName)
                    : null,
                members.ContainsKey(ObserverArgsName)
                    ? ReadObserverArgs(members[ObserverArgsName])
                    : null,
                members.ContainsKey(ValuePathName)
                    ? Path(members[ValuePathName], ValuePathName)
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

        private static IDictionary<string, string> ReadObserverArgs(object value)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException(ObserverArgsName + " は項目の組でなければならない。");
            }

            Dictionary<string, string> bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in members)
            {
                if (!MemberName.IsMatch(pair.Key))
                {
                    throw new FormatException("観測ツールの引数の名前でない: " + pair.Key);
                }

                string reference = Text(pair.Value, ObserverArgsName);
                RequireReference(reference, ObserverArgsName);
                bindings.Add(pair.Key, reference);
            }

            return bindings;
        }

        private static IList<SetupOperation> ReadSetup(object value)
        {
            List<SetupOperation> operations = new List<SetupOperation>();
            List<string> names = new List<string>();
            foreach (object item in Array(value, SetupName))
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

            if (operations.Count == 0)
            {
                throw new FormatException(SetupName + " は1件以上でなければならない。");
            }

            return operations;
        }

        private static SetupOperation ReadSetupOperation(object item)
        {
            Dictionary<string, object> members = Members(
                item,
                new[] { TagName },
                new[] { ElementTypeName, ToolName, ArgsName, OutName });

            SetupTag tag = Lookup(SetupTags, members[TagName], TagName);
            RequirePresence(members, ElementTypeName, tag == SetupTag.AddElement);
            RequirePresence(members, ToolName, tag == SetupTag.CallTool);
            RequirePresence(members, ArgsName, tag == SetupTag.CallTool);
            if (tag == SetupTag.InitPmx && members.ContainsKey(OutName))
            {
                throw new FormatException("値を出さない用意の操作が " + OutName + " を持つ。");
            }

            string outName = members.ContainsKey(OutName) ? Text(members[OutName], OutName) : null;
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
                        Name(members[ElementTypeName], ElementTypeName), outName);
                default:
                    return SetupOperation.CallTool(
                        Name(members[ToolName], ToolName), ReadSetupArgs(members[ArgsName]), outName);
            }
        }

        /// <summary>
        /// 呼ぶときの束縛。参照元とJSONのリテラルに加えて、型ごとに定めたサンプル値を指せる。
        /// </summary>
        private static IDictionary<string, object> ReadSetupArgs(object value)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException(ArgsName + " は項目の組でなければならない。");
            }

            Dictionary<string, object> args = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in members)
            {
                if (!MemberName.IsMatch(pair.Key))
                {
                    throw new FormatException("呼ぶツールの引数の名前でない: " + pair.Key);
                }

                string text = pair.Value as string;
                if (text != null && ReferenceSpaces.Keys.Any(p => text.StartsWith(p, StringComparison.Ordinal)))
                {
                    RequireReference(text, ArgsName);
                }
                else if (text != null && text.StartsWith("sample", StringComparison.Ordinal)
                    && !SampleReference.IsMatch(text))
                {
                    throw new FormatException("サンプル値への参照の形でない: " + text);
                }
                else
                {
                    RequireLiteral(pair.Value);
                }

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

        private static string EffectKey(object value)
        {
            string text = value as string;
            if (text == null)
            {
                throw new FormatException(EffectKeyName + " は文字列でなければならない。");
            }

            return text;
        }

        private static string Path(object value, string name)
        {
            string text = value as string;
            if (text == null || !ValuePath.IsMatch(text))
            {
                throw new FormatException(name + " が比べる値の位置の形でない。");
            }

            return text;
        }

        private static string Name(object value, string name)
        {
            string text = Text(value, name);
            if (!SnakeCaseName.IsMatch(text))
            {
                throw new FormatException(
                    name + " は小文字で始まり、小文字と数字と下線だけからなる語でなければならない: "
                        + text);
            }

            return text;
        }

        private static TValue Lookup<TValue>(
            Dictionary<string, TValue> table, object value, string name)
        {
            string text = Text(value, name);
            TValue found;
            if (!table.TryGetValue(text, out found))
            {
                throw new FormatException("知らない " + name + ": " + text);
            }

            return found;
        }

        private static void RequireAscending(string previous, string current)
        {
            if (previous == null)
            {
                return;
            }

            int order = string.CompareOrdinal(previous, current);
            if (order == 0)
            {
                throw new FormatException("同じ行キーが二度現れる: " + current);
            }

            if (order > 0)
            {
                throw new FormatException("序数の昇順で並んでいない: " + current);
            }
        }

        private static object[] Array(object value, string name)
        {
            object[] items = value as object[];
            if (items == null)
            {
                throw new FormatException(name + " は項目の並びでなければならない。");
            }

            return items;
        }

        private static Dictionary<string, object> Members(object value, params string[] names)
        {
            return Members(value, names, new string[0]);
        }

        /// <summary>
        /// 求める項目と持てる項目だけを持つ対象として読む。余分な項目を黙って捨てると、正本の形が
        /// 崩れても気づけない。
        /// </summary>
        private static Dictionary<string, object> Members(
            object value, string[] names, string[] optional)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            foreach (string name in names)
            {
                if (!members.ContainsKey(name))
                {
                    throw new FormatException("項目が無い: " + name);
                }
            }

            foreach (string name in members.Keys)
            {
                if (!names.Contains(name, StringComparer.Ordinal)
                    && !optional.Contains(name, StringComparer.Ordinal))
                {
                    throw new FormatException("知らない項目がある: " + name);
                }
            }

            return members;
        }

        private static string Text(object value, string name)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                throw new FormatException(
                    name + " は空でない文字列でなければならない(空白だけも不可)。");
            }

            return text;
        }
    }
}
