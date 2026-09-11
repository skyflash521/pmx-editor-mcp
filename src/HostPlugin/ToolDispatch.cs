using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace PmxEditorMcp
{
    /// <summary>
    /// ツールの要求を、結び付きの表が指す行へ振り分ける。呼ぶ先も受け手も引数の型もビルド時に
    /// 決まっているので、ここには名前で型やメンバーを引く経路が無い。
    /// SDKへ届く呼び出しはどれもUIスレッドへ委譲した中で行い、複製編集型はその中で反映まで閉じる。
    /// </summary>
    public sealed class ToolDispatch
    {
        /// <summary>危険操作の確認を受け取る共通引数の名前。</summary>
        public const string ConfirmName = "confirm";

        /// <summary>返す項目を選ぶ共通引数の名前。</summary>
        public const string FieldsName = "fields";

        /// <summary>一覧が切り出す位置を受け取る共通引数の名前。</summary>
        public const string OffsetName = "offset";

        /// <summary>一覧が切り出す件数を受け取る共通引数の名前。</summary>
        public const string LimitName = "limit";

        /// <summary>更新が受け取る値の組の名前。</summary>
        public const string ValueName = "value";

        /// <summary>範囲の組が持つ始まりの名前。</summary>
        public const string StartName = "start";

        /// <summary>範囲の組が持つ件数の名前。</summary>
        public const string CountName = "count";

        /// <summary>一覧の応答が持つ総数の名前。</summary>
        public const string TotalName = "total";

        /// <summary>一覧の応答が持つ切り出した並びの名前。</summary>
        public const string ItemsName = "items";

        /// <summary>一覧の応答が持つ続きの位置の名前。</summary>
        public const string NextOffsetName = "nextOffset";

        /// <summary>更新が受け取る、対象ごとの値の組の並びの名前。</summary>
        public const string ValuesName = "values";

        /// <summary>要素のメソッドが受け取る、全件へ配る引数の組の名前。</summary>
        public const string ArgsName = "args";

        /// <summary>要素のメソッドが受け取る、対象ごとの引数の組の並びの名前。</summary>
        public const string ArgsListName = "argsList";

        /// <summary>加えるツールが受け取る、親ごとの組の並びの名前。</summary>
        public const string AssignmentsName = "assignments";

        /// <summary>親を指す位置の名前。一覧の各項目と、親ごとの組がこれを持つ。</summary>
        public const string ParentIndexName = "parentIndex";

        /// <summary>親を指すハンドルの名前。親をハンドルで指した呼び出しがこれを持つ。</summary>
        public const string ParentHandleName = "parentHandle";

        /// <summary>一覧の各項目が持つ、親の中の位置の名前。</summary>
        public const string IndexInParentName = "indexInParent";

        /// <summary>要素の実行時の型を表す項目の名前。</summary>
        public const string ItemTypeName = "itemType";

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly SdkRelayTable _relay;

        private readonly IDictionary<string, SdkReceiver> _receivers;

        private readonly IDictionary<string, SdkList> _lists;

        private readonly ResidentConnection _connection;

        private readonly PmxSession _pmx;

        private ToolDispatch(
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            IDictionary<string, SdkList> lists,
            ResidentConnection connection,
            PmxSession pmx)
        {
            _relay = relay;
            _receivers = receivers;
            _lists = lists;
            _connection = connection;
            _pmx = pmx;
        }

        /// <summary>結び付きの表が持つツールをすべて登録する。</summary>
        public static void AddTo(
            McpMethodTable methods,
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            IDictionary<string, SdkList> lists,
            ResidentConnection connection,
            PmxSession pmx,
            IDictionary<string, ToolCall> calls,
            IDictionary<string, ToolFields> aggregations,
            IDictionary<string, ToolElements> elements)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (relay == null)
            {
                throw new ArgumentNullException(nameof(relay));
            }

            if (receivers == null)
            {
                throw new ArgumentNullException(nameof(receivers));
            }

            if (lists == null)
            {
                throw new ArgumentNullException(nameof(lists));
            }

            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (pmx == null)
            {
                throw new ArgumentNullException(nameof(pmx));
            }

            if (calls == null)
            {
                throw new ArgumentNullException(nameof(calls));
            }

            if (aggregations == null)
            {
                throw new ArgumentNullException(nameof(aggregations));
            }

            if (elements == null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            ToolDispatch dispatch = new ToolDispatch(relay, receivers, lists, connection, pmx);
            foreach (KeyValuePair<string, ToolCall> call in calls)
            {
                ToolCall bound = call.Value;
                methods.Add(call.Key, context => dispatch.Invoke(context, bound));
            }

            foreach (KeyValuePair<string, ToolFields> aggregation in aggregations)
            {
                ToolFields bound = aggregation.Value;
                methods.Add(
                    aggregation.Key,
                    context => bound.Writes
                        ? dispatch.Write(context, bound)
                        : dispatch.Read(context, bound));
            }

            foreach (KeyValuePair<string, ToolElements> element in elements)
            {
                ToolElements bound = element.Value;
                methods.Add(
                    element.Key,
                    context => bound.Removes
                        ? dispatch.Remove(context, bound)
                        : dispatch.Add(context, bound));
            }
        }

        /// <summary>SDKのメンバーを呼ぶ。要素を相手にする呼び出しは対象の全件へ及ぶ。</summary>
        private object Invoke(McpMethodContext context, ToolCall call)
        {
            return call.Access.Kind == ToolAccessKind.Element
                ? InvokeEach(context, call)
                : InvokeOnce(context, call);
        }

        /// <summary>SDKのメンバーを、受け手そのものへ1度呼ぶ。</summary>
        private object InvokeOnce(McpMethodContext context, ToolCall call)
        {
            string code;
            string message;
            bool confirm;
            long? handle;
            List<string> known = call.Arguments.Select(a => a.Name).ToList();
            if (call.Danger != DangerKind.None)
            {
                known.Add(ConfirmName);
            }

            if (!TryOnlyKnown(context, Known(known, call.Receiver), out code, out message)
                || !TryConfirm(context, out confirm, out code, out message)
                || !TryPmxHandle(context, call.Receiver, out handle, out code, out message)
                || !TryPassDanger(call, handle, confirm, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            object[] arguments = new object[call.Arguments.Count];
            for (int at = 0; at < call.Arguments.Count; at++)
            {
                object value;
                if (!TryValue(context, call.Arguments[at], out value, out code, out message))
                {
                    return ToolEnvelope.Failure(code, message);
                }

                arguments[at] = value;
            }

            object result = null;
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                IList<Spot> column;
                if (!TryTake(context, call.Receiver, handle, false, out target, out refused)
                    || !TryColumn(
                        context,
                        call.Access,
                        call.Receiver,
                        target,
                        Pointed.None,
                        Accepted(call.Access, null, false),
                        out column,
                        out refused))
                {
                    return;
                }

                stage = Changing(call.Receiver, target);
                object value;
                SdkRelayRefusal refusal;
                if (!_relay.TryInvoke(
                    call.RowKey, column[0].Item, arguments, out value, out refusal))
                {
                    refused = Refusal.Of(call.RowKey, refusal);

                    return;
                }

                result = value;
                stage = Reflecting(call.Receiver, target, stage);
                refused = Commit(call.Receiver, target);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            if (refused != null)
            {
                return refused.Envelope;
            }

            if (call.Outputs.Count != 0)
            {
                return Written(call, result);
            }

            return call.Result == null
                ? ToolEnvelope.Success(null)
                : Written(call.Result, result);
        }

        /// <summary>
        /// 要素のメソッドを、解いた対象の全件へ呼ぶ。引数は全件へ同じ組を配るか、対象ごとの組の
        /// 並びで受け取る。
        /// </summary>
        private object InvokeEach(McpMethodContext context, ToolCall call)
        {
            string code;
            string message;
            bool confirm;
            long? handle;
            Pointed pointed;
            List<string> known = new List<string>();
            if (call.Arguments.Count != 0)
            {
                known.Add(ArgsName);
                known.Add(ArgsListName);
            }

            if (call.Danger != DangerKind.None)
            {
                known.Add(ConfirmName);
            }

            known.AddRange(Pointing(call.Access, true));
            if (!TryOnlyKnown(context, Known(known, call.Receiver), out code, out message)
                || !TryConfirm(context, out confirm, out code, out message)
                || !TryPmxHandle(context, call.Receiver, out handle, out code, out message)
                || !TryPassDanger(call, handle, confirm, out code, out message)
                || !TryPointed(context, call.Access, true, handle, out pointed, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            IList<object[]> passing;
            bool spread;
            if (!TryPassed(context, call, out passing, out spread, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            int invoked = 0;
            List<object> results = new List<object>();
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                IList<Spot> column;
                if (!TryTake(context, call.Receiver, handle, pointed.Held, out target, out refused)
                    || !TryColumn(
                        context,
                        call.Access,
                        call.Receiver,
                        target,
                        pointed,
                        Accepted(call.Access, null, false),
                        out column,
                        out refused)
                    || !TryOfItemType(column, call.Access, null, out refused))
                {
                    return;
                }

                if (!spread && passing.Count != column.Count)
                {
                    refused = new Refusal(ToolEnvelope.Failure(
                        ToolEnvelope.InvalidArgument,
                        ArgsListName + " の件数が対象の件数と合わない。"));

                    return;
                }

                stage = Changing(call.Receiver, target);
                for (int at = 0; at < column.Count; at++)
                {
                    object value;
                    SdkRelayRefusal refusal;
                    if (!_relay.TryInvoke(
                        call.RowKey,
                        column[at].Item,
                        spread ? passing[0] : passing[at],
                        out value,
                        out refusal))
                    {
                        refused = Refusal.Of(call.RowKey, refusal);

                        return;
                    }

                    results.Add(value);
                }

                invoked = column.Count;
                stage = Reflecting(call.Receiver, target, stage);
                refused = Commit(call.Receiver, target);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            if (refused != null)
            {
                return refused.Envelope;
            }

            if (call.Result == null && call.Outputs.Count == 0)
            {
                return ToolEnvelope.Success(SetResponse.Invoked(invoked));
            }

            List<object> written = new List<object>(results.Count);
            List<string> warnings = new List<string>();
            foreach (object one in results)
            {
                IDictionary<string, object> envelope = call.Outputs.Count != 0
                    ? Written(call, one)
                    : Written(call.Result, one);
                object value;
                if (!envelope.TryGetValue("value", out value))
                {
                    return envelope;
                }

                written.Add(value);
                object noted;
                if (envelope.TryGetValue("warnings", out noted))
                {
                    warnings.AddRange(((object[])noted).Cast<string>());
                }
            }

            return ToolEnvelope.Success(SetResponse.PerTarget(written, invoked), warnings);
        }

        /// <summary>要素のメソッドへ渡す引数。全件へ同じ組を配るか、対象ごとの組の並びを取る。</summary>
        private static bool TryPassed(
            McpMethodContext context,
            ToolCall call,
            out IList<object[]> passing,
            out bool spread,
            out string code,
            out string message)
        {
            passing = null;
            spread = true;
            code = null;
            message = null;
            object single;
            object many = null;
            bool hasSingle = context.Params.TryGetValue(ArgsName, out single);
            bool hasMany = context.Params.TryGetValue(ArgsListName, out many);
            if (call.Arguments.Count == 0)
            {
                if (hasSingle || hasMany)
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = "引数を取らない呼び出しは " + ArgsName + " も " + ArgsListName
                        + " も取らない。";

                    return false;
                }

                passing = new[] { new object[0] };

                return true;
            }

            if (hasSingle == hasMany)
            {
                code = ToolEnvelope.InvalidArgument;
                message = ArgsName + " と " + ArgsListName + " のどちらか一方を渡す。";

                return false;
            }

            if (hasSingle)
            {
                object[] taken;
                if (!TryPass(call, single, out taken, out code, out message))
                {
                    return false;
                }

                passing = new[] { taken };

                return true;
            }

            object[] items = many as object[];
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = ArgsListName + " は引数の組の配列でなければならない。";

                return false;
            }

            List<object[]> built = new List<object[]>();
            foreach (object item in items)
            {
                object[] taken;
                if (!TryPass(call, item, out taken, out code, out message))
                {
                    return false;
                }

                built.Add(taken);
            }

            passing = built;
            spread = false;

            return true;
        }

        /// <summary>引数の組1つを、シグネチャの並びの値へ直す。</summary>
        private static bool TryPass(
            ToolCall call, object given, out object[] passed, out string code, out string message)
        {
            passed = null;
            code = null;
            message = null;
            IDictionary<string, object> members = given as IDictionary<string, object>;
            if (members == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = ArgsName + " は引数の組でなければならない。";

                return false;
            }

            string unknown = members.Keys
                .Where(n => !call.Arguments.Any(
                    a => string.Equals(a.Name, n, StringComparison.Ordinal)))
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = "知らない項目を渡している: " + unknown;

                return false;
            }

            object[] taken = new object[call.Arguments.Count];
            for (int at = 0; at < call.Arguments.Count; at++)
            {
                ToolArgument argument = call.Arguments[at];
                object value;
                if (!members.TryGetValue(argument.Name, out value))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = "引数が足りない: " + argument.Name;

                    return false;
                }

                object typed;
                if (!ValueInput.TryFromJson(argument.Type, value, out typed, out code, out message))
                {
                    code = code ?? ToolEnvelope.NotApplicable;
                    message = message ?? (argument.Name + " は値として受け取れない型を取る。");

                    return false;
                }

                taken[at] = typed;
            }

            passed = taken;

            return true;
        }

        /// <summary>その型の項目をまとめて読む。</summary>
        private object Read(McpMethodContext context, ToolFields tool)
        {
            string code;
            string message;
            IList<string> requested;
            IList<string> selected;
            long? handle;
            int offset;
            int limit;
            Pointed pointed;
            List<string> known = new List<string> { FieldsName };
            if (tool.Listing)
            {
                known.Add(OffsetName);
                known.Add(LimitName);
            }

            known.AddRange(Pointing(tool.Access, true));
            bool divided = Divided(tool);
            if (!TryOnlyKnown(context, Known(known, tool.Receiver), out code, out message)
                || !TryPmxHandle(context, tool.Receiver, out handle, out code, out message)
                || !TryCount(context, OffsetName, 0, 0, out offset, out code, out message)
                || !TryCount(context, LimitName, int.MaxValue, 1, out limit, out code, out message)
                || !TryFields(context, out requested, out code, out message)
                || !TryPointed(context, tool.Access, true, handle, out pointed, out code, out message)
                || !FieldSelection.TryResolve(
                    requested,
                    tool.Fields.Select(f => f.Name).ToList(),
                    Composed(tool.Access, pointed),
                    out selected,
                    out code,
                    out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            int total = 0;
            List<Spot> taken = new List<Spot>();
            List<string> types = new List<string>();
            List<IList<ToolField>> reading = new List<IList<ToolField>>();
            List<object[]> values = new List<object[]>();
            Refusal refused = null;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                IList<Spot> column;
                if (!TryTake(context, tool.Receiver, handle, pointed.Held, out target, out refused)
                    || !TryColumn(
                        context,
                        tool.Access,
                        tool.Receiver,
                        target,
                        pointed,
                        Accepted(tool.Access, null, divided),
                        out column,
                        out refused)
                    || !TryOfDeclaredType(column, tool.Access, divided, out refused))
                {
                    return;
                }

                total = column.Count;
                taken.AddRange(tool.Listing ? column.Skip(offset).Take(limit) : column);
                foreach (Spot spot in taken)
                {
                    string itemType = tool.Access.Items.Count == 0
                        ? null
                        : Named(tool.Access, spot.Item);
                    IList<ToolField> fields = Reading(tool, divided ? itemType : null, selected);
                    object[] read = new object[fields.Count];
                    for (int at = 0; at < fields.Count; at++)
                    {
                        object value;
                        SdkRelayRefusal refusal;
                        if (!_relay.TryInvoke(
                            fields[at].RowKey, spot.Item, new object[0], out value, out refusal))
                        {
                            refused = Refusal.Of(fields[at].RowKey, refusal);

                            return;
                        }

                        read[at] = value;
                    }

                    types.Add(itemType);
                    reading.Add(fields);
                    values.Add(read);
                }
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, EditStage.BeforeCommit);
            }

            if (refused != null)
            {
                return refused.Envelope;
            }

            List<IDictionary<string, object>> items =
                new List<IDictionary<string, object>>(taken.Count);
            List<string> warnings = new List<string>();
            for (int spot = 0; spot < taken.Count; spot++)
            {
                Dictionary<string, object> item =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (string name in Composed(tool.Access, pointed))
                {
                    item.Add(name, Composed(name, taken[spot], types[spot]));
                }

                for (int at = 0; at < reading[spot].Count; at++)
                {
                    object json;
                    IList<string> written;
                    if (!ValueShape.TryToJson(
                        reading[spot][at].Type,
                        values[spot][at],
                        ImageTransfer.DefaultMaxLongSide,
                        out json,
                        out written,
                        out code,
                        out message))
                    {
                        return Unwritable(reading[spot][at].Type, code, message);
                    }

                    item.Add(reading[spot][at].Name, json);
                    warnings.AddRange(written);
                }

                items.Add(item);
            }

            return tool.Listing
                ? Listed(context, items, total, offset, limit, warnings)
                : ToolEnvelope.Success(items[0], warnings);
        }

        /// <summary>その要素から読む項目。選んだ名前のうち、その実行時の型が持つものを並びの順で採る。</summary>
        private static IList<ToolField> Reading(
            ToolFields tool, string itemType, IList<string> selected)
        {
            ToolFieldSet set = SetOf(tool, itemType);
            if (set == null)
            {
                return new ToolField[0];
            }

            return selected
                .Where(n => set.Fields.Any(f => string.Equals(f.Name, n, StringComparison.Ordinal)))
                .Select(n => set.Fields.First(
                    f => string.Equals(f.Name, n, StringComparison.Ordinal)))
                .ToList();
        }

        /// <summary>合成の項目1件の値。</summary>
        private static object Composed(string name, Spot spot, string itemType)
        {
            if (string.Equals(name, ItemTypeName, StringComparison.Ordinal))
            {
                return itemType;
            }

            if (string.Equals(name, ParentHandleName, StringComparison.Ordinal))
            {
                return spot.ParentHandle;
            }

            return string.Equals(name, ParentIndexName, StringComparison.Ordinal)
                ? (object)spot.ParentIndex
                : spot.IndexInParent;
        }

        /// <summary>その型の項目を書く。</summary>
        private object Write(McpMethodContext context, ToolFields tool)
        {
            return tool.Listing ? WriteValue(context, tool) : WriteOne(context, tool);
        }

        /// <summary>
        /// コネクタの項目を1つ書く。1度に書けるのを1つに限るのは、直に書き込む先を戻す手立てが
        /// SDKの側に無く、2つ以上を続けて書くと途中まで書けた状態が残りうるためである。
        /// </summary>
        private object WriteOne(McpMethodContext context, ToolFields tool)
        {
            string code;
            string message;
            if (!TryOnlyKnown(
                context,
                Known(tool.Sets[0].Fields.Select(f => f.Name).ToList(), tool.Receiver),
                out code,
                out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            List<ToolField> writing = tool.Sets[0].Fields
                .Where(f => context.Params.ContainsKey(f.Name))
                .ToList();
            if (writing.Count != 1)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument, "書き込む項目をちょうど1つ選ぶ。");
            }

            ToolField field = writing[0];
            object value;
            if (!TryValue(
                context, new ToolArgument(field.Name, field.Type), out value, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            Refusal refused = null;
            EditStage stage = Changing(tool.Receiver, null);
            Exception failure;
            if (!Run(context, () =>
            {
                object ignored;
                SdkRelayRefusal refusal;
                if (!_relay.TryInvoke(
                    field.RowKey,
                    Receiver(tool.Receiver, null),
                    new[] { value },
                    out ignored,
                    out refusal))
                {
                    refused = Refusal.Of(field.RowKey, refusal);
                }
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            return refused != null
                ? refused.Envelope
                : ToolEnvelope.Success(SetResponse.Updated(1));
        }

        /// <summary>
        /// 項目を、値の組で受け取って書く。複製編集型は複製へ書いてまとめて反映するので、
        /// 途中まで書けた状態がエディタへ残らない。
        /// </summary>
        private object WriteValue(McpMethodContext context, ToolFields tool)
        {
            string code;
            string message;
            long? handle;
            string itemType;
            Pointed pointed;
            List<string> known = new List<string> { ValueName };
            if (tool.Access.Kind == ToolAccessKind.Element)
            {
                known.Add(ValuesName);
            }

            if (Divided(tool))
            {
                known.Add(ItemTypeName);
            }

            known.AddRange(Pointing(tool.Access, true));
            if (!TryOnlyKnown(context, Known(known, tool.Receiver), out code, out message)
                || !TryPmxHandle(context, tool.Receiver, out handle, out code, out message)
                || !TryItemType(context, tool, out itemType, out code, out message)
                || !TryPointed(context, tool.Access, true, handle, out pointed, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            ToolFieldSet set = SetOf(tool, itemType);
            if (set == null || set.Fields.Count == 0)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable, "書き込める項目を持たない型を指している。");
            }

            IList<Change> writing;
            bool spread;
            if (!TryChanges(context, tool, set, out writing, out spread, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            int updated = 0;
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                IList<Spot> column;
                if (!TryTake(context, tool.Receiver, handle, pointed.Held, out target, out refused)
                    || !TryColumn(
                        context,
                        tool.Access,
                        tool.Receiver,
                        target,
                        pointed,
                        Accepted(tool.Access, itemType, false),
                        out column,
                        out refused)
                    || !TryOfItemType(column, tool.Access, itemType, out refused))
                {
                    return;
                }

                if (!spread && writing.Count != column.Count)
                {
                    refused = new Refusal(ToolEnvelope.Failure(
                        ToolEnvelope.InvalidArgument,
                        ValuesName + " の件数が対象の件数と合わない。"));

                    return;
                }

                stage = Changing(tool.Receiver, target);
                for (int at = 0; at < column.Count; at++)
                {
                    Change one = spread ? writing[0] : writing[at];
                    for (int field = 0; field < one.Fields.Count; field++)
                    {
                        object ignored;
                        SdkRelayRefusal refusal;
                        if (!_relay.TryInvoke(
                            one.Fields[field].RowKey,
                            column[at].Item,
                            new[] { one.Values[field] },
                            out ignored,
                            out refusal))
                        {
                            refused = Refusal.Of(one.Fields[field].RowKey, refusal);

                            return;
                        }
                    }
                }

                updated = column.Count;
                stage = Reflecting(tool.Receiver, target, stage);
                refused = Commit(tool.Receiver, target);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            return refused != null
                ? refused.Envelope
                : ToolEnvelope.Success(SetResponse.Updated(updated));
        }

        /// <summary>
        /// 書き込む相手の実行時の型。型で分かれるツールだけが受け取り、分かれないツールでは
        /// 名前を持たない1つの組を指す。
        /// </summary>
        private static bool TryItemType(
            McpMethodContext context,
            ToolFields tool,
            out string itemType,
            out string code,
            out string message)
        {
            itemType = null;
            code = null;
            message = null;
            if (!Divided(tool))
            {
                return true;
            }

            object value;
            string given = context.Params.TryGetValue(ItemTypeName, out value)
                ? value as string
                : null;
            if (given == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = ItemTypeName + " は要素の実行時の型の名前でなければならない。";

                return false;
            }

            if (!tool.Access.Items.Any(
                i => string.Equals(i.ItemType, given, StringComparison.Ordinal)))
            {
                code = ToolEnvelope.InvalidArgument;
                message = "このリストが並べない型を指している: " + given;

                return false;
            }

            itemType = given;

            return true;
        }

        /// <summary>所有するリストの末尾へ、ハンドルが指す要素を加える。</summary>
        private object Add(McpMethodContext context, ToolElements tool)
        {
            return tool.Access.Parents.Count == 0
                ? AddToRoot(context, tool)
                : AddToParents(context, tool);
        }

        /// <summary>PMXが直に持つリストの末尾へ加える。</summary>
        private object AddToRoot(McpMethodContext context, ToolElements tool)
        {
            string code;
            string message;
            IList<long> given;
            ResolvedTargets resolved;
            if (!TryOnlyKnown(
                context,
                new List<string> { TargetNames.Element.Handles },
                out code,
                out message)
                || !TryHandles(context, TargetNames.Element, out given, out code, out message)
                || !TargetSelection.TryResolve(
                    new TargetRequest(null, null, null, null, given),
                    TargetForm.Handles,
                    0,
                    id => Held(context, Accepted(tool.Access, null, true), id) != null,
                    out resolved,
                    out code,
                    out message,
                    TargetNames.Element))
            {
                return ToolEnvelope.Failure(code, message);
            }

            object[] items = resolved.Handles
                .Select(id => Held(context, Accepted(tool.Access, null, true), id))
                .ToArray();
            int[] indices = new int[items.Length];
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                SdkList list;
                if (!TryTake(context, tool.Receiver, null, false, out target, out refused)
                    || !TryList(tool.Access.RowKey, out list, out refused))
                {
                    return;
                }

                stage = Changing(tool.Receiver, target);
                for (int at = 0; at < items.Length; at++)
                {
                    list.Add(target.Pmx, items[at]);
                    indices[at] = list.Count(target.Pmx) - 1;
                }

                stage = Reflecting(tool.Receiver, target, stage);
                refused = Commit(tool.Receiver, target);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            if (refused != null)
            {
                return refused.Envelope;
            }

            Release(context, resolved.Handles);

            return ToolEnvelope.Success(SetResponse.Added(indices));
        }

        /// <summary>親ごとの組で、親が持つリストの末尾へ加える。</summary>
        private object AddToParents(McpMethodContext context, ToolElements tool)
        {
            string code;
            string message;
            IList<Assignment> assignments;
            bool byHandle;
            if (!TryOnlyKnown(context, new List<string> { AssignmentsName }, out code, out message)
                || !TryAssignments(context, tool, out assignments, out byHandle, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            List<int> indices = new List<int>();
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                SdkList list;
                IList<object> owners = null;
                if (!TryTake(context, tool.Receiver, null, byHandle, out target, out refused)
                    || !TryList(tool.Access.RowKey, out list, out refused)
                    || (!byHandle && !TryOwners(tool.Access, target, out owners, out refused)))
                {
                    return;
                }

                foreach (Assignment assignment in assignments)
                {
                    if (!byHandle
                        && (assignment.Parent < 0 || assignment.Parent >= owners.Count))
                    {
                        refused = new Refusal(ToolEnvelope.Failure(
                            ToolEnvelope.IndexOutOfRange,
                            ParentIndexName + " が親の列の外を指している: " + assignment.Parent));

                        return;
                    }
                }

                stage = Changing(tool.Receiver, target);
                foreach (Assignment assignment in assignments)
                {
                    object owner = byHandle ? assignment.Owner : owners[assignment.Parent];
                    foreach (object item in assignment.Items)
                    {
                        list.Add(owner, item);
                        indices.Add(list.Count(owner) - 1);
                    }
                }

                stage = Reflecting(tool.Receiver, target, stage);
                refused = Commit(tool.Receiver, target);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            if (refused != null)
            {
                return refused.Envelope;
            }

            Release(context, assignments.SelectMany(a => a.Handles).ToList());

            return ToolEnvelope.Success(SetResponse.Added(indices.ToArray()));
        }

        /// <summary>所有するリストから、指した位置の要素を取り除く。</summary>
        private object Remove(McpMethodContext context, ToolElements tool)
        {
            string code;
            string message;
            long? handle;
            Pointed pointed;
            List<string> known = new List<string>(Pointing(tool.Access, false));
            if (!TryOnlyKnown(context, Known(known, tool.Receiver), out code, out message)
                || !TryPmxHandle(context, tool.Receiver, out handle, out code, out message)
                || !TryPointed(context, tool.Access, false, handle, out pointed, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            int removed = 0;
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                SdkList list;
                IList<Spot> column;
                if (!TryTake(context, tool.Receiver, handle, pointed.Held, out target, out refused)
                    || !TryList(tool.Access.RowKey, out list, out refused)
                    || !TryColumn(
                        context,
                        tool.Access,
                        tool.Receiver,
                        target,
                        pointed,
                        Accepted(tool.Access, null, false),
                        out column,
                        out refused)
                    || !TryOfItemType(column, tool.Access, null, out refused))
                {
                    return;
                }

                stage = Changing(tool.Receiver, target);
                foreach (Spot spot in column
                    .OrderByDescending(s => s.IndexInParent)
                    .ToList())
                {
                    list.RemoveAt(spot.Owner, spot.IndexInParent);
                }

                removed = column.Count;
                stage = Reflecting(tool.Receiver, target, stage);
                refused = Commit(tool.Receiver, target);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            return refused != null
                ? refused.Envelope
                : ToolEnvelope.Success(SetResponse.Removed(removed));
        }

        /// <summary>加え終えたハンドルを解く。</summary>
        private static void Release(McpMethodContext context, IEnumerable<long> handles)
        {
            foreach (long id in handles)
            {
                HandleReleaseResult released;
                context.Handles.TryRelease((int)id, out released);
            }
        }

        /// <summary>
        /// 切り出した並びを一覧の形にする。<paramref name="items"/> は位置と件数で切り出した後の
        /// 並び、<paramref name="total"/> は切り出す前の総数で、続きの位置はその2つから決まる。
        /// </summary>
        private static object Listed(
            McpMethodContext context,
            IList<IDictionary<string, object>> items,
            int total,
            int offset,
            int limit,
            IList<string> warnings)
        {
            Page<IDictionary<string, object>> page;
            if (!Paging.TryTake(
                items,
                0,
                limit,
                ResponseSize.ValueChars(context.BudgetChars),
                taken => Serializer.Serialize(taken).Length,
                out page))
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            int next = offset + page.Items.Count;
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { TotalName, total },
                { ItemsName, page.Items.ToArray() },
            };
            if (next < total)
            {
                value.Add(NextOffsetName, next);
            }

            return ToolEnvelope.Success(value, warnings.Concat(page.Warnings).ToList());
        }

        /// <summary>その道が受け取る対象の指し方の名前。要素を相手にしない道は何も受け取らない。</summary>
        private static IEnumerable<string> Pointing(ToolAccess access, bool handles)
        {
            if (access.Kind != ToolAccessKind.Element)
            {
                yield break;
            }

            yield return TargetNames.Element.Indices;
            yield return TargetNames.Element.Range;
            yield return TargetNames.Element.All;
            if (handles)
            {
                yield return TargetNames.Element.Handles;
            }

            if (access.Parents.Count == 0)
            {
                yield break;
            }

            yield return TargetNames.Parent.Indices;
            yield return TargetNames.Parent.Range;
            yield return TargetNames.Parent.All;
            if (access.Owner != null)
            {
                yield return TargetNames.Parent.Handles;
            }
        }

        /// <summary>
        /// 一覧の各項目が常に持つ合成の項目。親を辿る道の、位置で指した呼び出しだけが持つ
        /// ——ハンドルで指した対象はまだどの親にも属していない。
        /// </summary>
        private static IList<string> Composed(ToolAccess access, Pointed pointed)
        {
            List<string> composed = new List<string>();
            if (access.Items.Count != 0)
            {
                composed.Add(ItemTypeName);
            }

            if (access.Kind == ToolAccessKind.Element
                && access.Parents.Count != 0
                && !pointed.ByHandle)
            {
                composed.Add(pointed.ParentByHandle ? ParentHandleName : ParentIndexName);
                composed.Add(IndexInParentName);
            }

            return composed;
        }

        /// <summary>要素と親の指し方を読み取る。</summary>
        private static bool TryPointed(
            McpMethodContext context,
            ToolAccess access,
            bool handles,
            long? pmxHandle,
            out Pointed pointed,
            out string code,
            out string message)
        {
            pointed = null;
            code = null;
            message = null;
            if (access.Kind != ToolAccessKind.Element)
            {
                pointed = new Pointed(null, null, false);

                return true;
            }

            TargetRequest elements;
            TargetRequest parents = null;
            if (!TryTargets(context, TargetNames.Element, handles, out elements, out code, out message))
            {
                return false;
            }

            if (access.Parents.Count != 0
                && !TryTargets(
                    context,
                    TargetNames.Parent,
                    access.Owner != null,
                    out parents,
                    out code,
                    out message))
            {
                return false;
            }

            bool byHandle = elements.Handles != null;
            if (byHandle && parents != null && Points(parents))
            {
                code = ToolEnvelope.InvalidArgument;
                message = "対象をハンドルで指した呼び出しは親の指し方を取らない。";

                return false;
            }

            bool parentByHandle = parents != null && parents.Handles != null;
            if ((byHandle || parentByHandle) && pmxHandle.HasValue)
            {
                code = ToolEnvelope.InvalidArgument;
                message = "対象をハンドルで指した呼び出しは " + PmxSession.HandleName + " を取らない。";

                return false;
            }

            pointed = new Pointed(elements, parents, byHandle, parentByHandle);

            return true;
        }

        /// <summary>その指定が何かを指しているか。</summary>
        private static bool Points(TargetRequest request)
        {
            return request.Indices != null
                || request.RangeStart.HasValue
                || request.RangeCount.HasValue
                || request.All.HasValue
                || request.Handles != null;
        }

        /// <summary>
        /// 要求が指した対象の列。受け手そのものを相手にする道とPMXの子を相手にする道では1件、
        /// 要素を相手にする道では解いた対象をその順で並べる。
        /// </summary>
        private bool TryColumn(
            McpMethodContext context,
            ToolAccess access,
            ToolReceiver receiver,
            PmxTarget target,
            Pointed pointed,
            IList<Type> accepted,
            out IList<Spot> column,
            out Refusal refused)
        {
            column = null;
            refused = null;
            if (access.Kind == ToolAccessKind.Whole)
            {
                column = new[] { new Spot(null, 0, -1, -1, Receiver(receiver, target)) };

                return true;
            }

            if (access.Kind == ToolAccessKind.Element && pointed.ByHandle)
            {
                return TryHeld(context, access, accepted, pointed, out column, out refused);
            }

            IList<object> owners;
            IList<long> ids = null;
            if (pointed.ParentByHandle)
            {
                if (!TryHeldOwners(context, access, pointed, out owners, out ids, out refused))
                {
                    return false;
                }
            }
            else if (!TryOwners(access, target, out owners, out refused))
            {
                return false;
            }

            if (access.Kind == ToolAccessKind.Child)
            {
                List<object> child = new List<object>();
                if (owners.Count != 0 && !TryStep(Step(access), owners[0], child, out refused))
                {
                    return false;
                }

                column = child.Select(one => new Spot(null, 0, -1, -1, one)).ToList();

                return true;
            }

            IList<int> chosen;
            if (pointed.ParentByHandle)
            {
                chosen = Enumerable.Range(0, owners.Count).ToArray();
            }
            else if (!TryChosen(pointed.Parents, owners.Count, out chosen, out refused))
            {
                return false;
            }

            List<Spot> spots = new List<Spot>();
            foreach (int parent in chosen)
            {
                List<object> reached = new List<object>();
                if (!TryStep(Step(access), owners[parent], reached, out refused))
                {
                    return false;
                }

                for (int at = 0; at < reached.Count; at++)
                {
                    spots.Add(new Spot(
                        owners[parent],
                        spots.Count,
                        ids == null ? parent : -1,
                        access.Listed ? at : 0,
                        reached[at],
                        ids == null ? (long?)null : ids[parent]));
                }
            }

            ResolvedTargets resolved;
            string code;
            string message;
            if (!TargetSelection.TryResolve(
                pointed.Elements,
                TargetForm.Indices | TargetForm.Range | TargetForm.All,
                spots.Count,
                id => false,
                out resolved,
                out code,
                out message,
                TargetNames.Element))
            {
                refused = new Refusal(ToolEnvelope.Failure(code, message));

                return false;
            }

            column = resolved.Indices.Select(i => spots[i]).ToList();

            return true;
        }

        /// <summary>
        /// そのツールが実行時の型で分かれるか。型ごとの組を持つツールだけが、要求と応答で型を扱う。
        /// </summary>
        private static bool Divided(ToolFields tool)
        {
            return tool.Sets.Any(s => s.ItemType != null);
        }

        /// <summary>その実行時の型の項目の組。持たない型では null。</summary>
        private static ToolFieldSet SetOf(ToolFields tool, string itemType)
        {
            return tool.Sets.FirstOrDefault(
                s => string.Equals(s.ItemType, itemType, StringComparison.Ordinal));
        }

        /// <summary>その実体の実行時の型の名前。どの型にも当たらなければ分からないと綴る。</summary>
        private static string Named(ToolAccess access, object item)
        {
            foreach (ToolItem one in access.Items)
            {
                if (one.IsItem(item))
                {
                    return one.ItemType;
                }
            }

            return "知らない型";
        }

        /// <summary>
        /// 型で分かれないツールの対象が、そのツールの宣言型であることを求める。分かれるツールは
        /// 要素ごとに型が違ってよいので、ここでは何も求めない。
        /// </summary>
        private static bool TryOfDeclaredType(
            IList<Spot> column, ToolAccess access, bool divided, out Refusal refused)
        {
            refused = null;

            return access.Kind != ToolAccessKind.Element || divided
                || TryOfItemType(column, access, null, out refused);
        }

        /// <summary>
        /// 対象が、書き込む相手として指した実行時の型であることを求める。型で分かれないツールでは
        /// 宣言型であることを求める。
        /// </summary>
        private static bool TryOfItemType(
            IList<Spot> column, ToolAccess access, string itemType, out Refusal refused)
        {
            refused = null;
            if (access.Kind != ToolAccessKind.Element)
            {
                return true;
            }

            // 型を指さない呼び出し——要素のメソッドと、型で分かれないツール——は宣言型を求める。
            if (itemType == null)
            {
                return TryOfType(
                    column,
                    access,
                    access.IsElement,
                    access.ItemType ?? access.Element.FullName,
                    out refused);
            }

            ToolItem wanted = access.Items.First(
                i => string.Equals(i.ItemType, itemType, StringComparison.Ordinal));

            return TryOfType(column, access, wanted.IsItem, wanted.ItemType, out refused);
        }

        /// <summary>
        /// 解いた対象の実行時の型が求める型と合うことを求める。合わない位置は適用できないとして
        /// 断り、その位置と実行時の型を説明に含める——抽象の型を並べるリストでは、位置で解いた
        /// 対象に別の具象の型が混じりうる。
        /// </summary>
        private static bool TryOfType(
            IList<Spot> column,
            ToolAccess access,
            Func<object, bool> isItem,
            string wanted,
            out Refusal refused)
        {
            refused = null;
            foreach (Spot spot in column)
            {
                if (isItem(spot.Item))
                {
                    continue;
                }

                refused = new Refusal(ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable,
                    "対象の実行時の型が合わない: 位置 " + spot.Position + " は "
                        + Named(access, spot.Item) + " で、求めるのは " + wanted + "。"));

                return false;
            }

            return true;
        }

        private static ToolHop Step(ToolAccess access)
        {
            return new ToolHop(access.RowKey, access.Listed);
        }

        /// <summary>
        /// ハンドルで指した対象の列。まだリストへ加えていない生成物を指す。台帳は発行したときの型で
        /// 覚えているので、そのツールが受け付ける型のどれかで引く。
        /// </summary>
        private static bool TryHeld(
            McpMethodContext context,
            ToolAccess access,
            IList<Type> accepted,
            Pointed pointed,
            out IList<Spot> column,
            out Refusal refused)
        {
            column = null;
            refused = null;
            ResolvedTargets resolved;
            string code;
            string message;
            if (!TargetSelection.TryResolve(
                pointed.Elements,
                TargetForm.Handles,
                0,
                id => Held(context, accepted, id) != null,
                out resolved,
                out code,
                out message,
                TargetNames.Element))
            {
                refused = new Refusal(ToolEnvelope.Failure(code, message));

                return false;
            }

            column = resolved.Handles
                .Select((id, at) => new Spot(null, at, -1, -1, Held(context, accepted, id)))
                .ToList();

            return true;
        }

        /// <summary>
        /// ハンドルで指した親の列。要求に現れた順に並べ、同じ並びのハンドルも返す。親がまだ
        /// どのPMXにも属していないので、所有の経路は辿らない。
        /// </summary>
        private static bool TryHeldOwners(
            McpMethodContext context,
            ToolAccess access,
            Pointed pointed,
            out IList<object> owners,
            out IList<long> ids,
            out Refusal refused)
        {
            owners = null;
            ids = null;
            refused = null;
            ResolvedTargets resolved;
            string code;
            string message;
            if (!TargetSelection.TryResolve(
                pointed.Parents,
                TargetForm.Handles,
                0,
                id => Held(context, access.Owner, id) != null,
                out resolved,
                out code,
                out message,
                TargetNames.Parent))
            {
                refused = new Refusal(ToolEnvelope.Failure(code, message));

                return false;
            }

            ids = resolved.Handles;
            owners = resolved.Handles.Select(id => Held(context, access.Owner, id)).ToList();

            return true;
        }

        /// <summary>
        /// そのツールが受け付ける型。実行時の型で分かれるツールでは、読む側はどの具象の型でもよく、
        /// 書く側は選んだ型に限る。分かれないツールと要素のメソッドは、その宣言型に限る。
        /// </summary>
        private static IList<Type> Accepted(ToolAccess access, string itemType, bool anyItem)
        {
            if (access.Kind != ToolAccessKind.Element)
            {
                return new Type[0];
            }

            if (anyItem && access.Items.Count != 0)
            {
                return access.Items.Select(i => i.Element).ToList();
            }

            return itemType == null
                ? new[] { access.Element }
                : new[]
                {
                    access.Items
                        .First(i => string.Equals(i.ItemType, itemType, StringComparison.Ordinal))
                        .Element,
                };
        }

        /// <summary>そのハンドルが指す、受け付ける型のどれかの実体。指していなければ null。</summary>
        private static object Held(McpMethodContext context, IList<Type> accepted, long id)
        {
            foreach (Type type in accepted)
            {
                object held = Held(context, type, id);
                if (held != null)
                {
                    return held;
                }
            }

            return null;
        }

        /// <summary>親の列の中から、要求が指した親の位置。親を辿らない道では先頭の1件。</summary>
        private static bool TryChosen(
            TargetRequest parents, int count, out IList<int> chosen, out Refusal refused)
        {
            chosen = null;
            refused = null;
            if (parents == null)
            {
                chosen = new[] { 0 };

                return true;
            }

            ResolvedTargets resolved;
            string code;
            string message;
            if (!TargetSelection.TryResolve(
                parents,
                TargetForm.Indices | TargetForm.Range | TargetForm.All,
                count,
                id => false,
                out resolved,
                out code,
                out message,
                TargetNames.Parent))
            {
                refused = new Refusal(ToolEnvelope.Failure(code, message));

                return false;
            }

            chosen = resolved.Indices;

            return true;
        }

        /// <summary>
        /// 要素を並べるリストを持つ実体の列。親を辿らない道ではPMXそのもの1件で、辿る道では
        /// 所有の経路を先頭から辿って平らに並べる。値を持たない段はそこで打ち切る。
        /// </summary>
        private bool TryOwners(
            ToolAccess access, PmxTarget target, out IList<object> owners, out Refusal refused)
        {
            owners = null;
            refused = null;
            List<object> column = new List<object> { target.Pmx };
            foreach (ToolHop hop in access.Parents)
            {
                List<object> next = new List<object>();
                foreach (object owner in column)
                {
                    if (!TryStep(hop, owner, next, out refused))
                    {
                        return false;
                    }
                }

                column = next;
            }

            owners = column;

            return true;
        }

        /// <summary>その一歩の先を <paramref name="next"/> へ並べる。</summary>
        private bool TryStep(ToolHop hop, object owner, IList<object> next, out Refusal refused)
        {
            refused = null;
            if (hop.Listed)
            {
                SdkList list;
                if (!TryList(hop.RowKey, out list, out refused))
                {
                    return false;
                }

                int count = list.Count(owner);
                for (int at = 0; at < count; at++)
                {
                    object item = list.At(owner, at);
                    if (item != null)
                    {
                        next.Add(item);
                    }
                }

                return true;
            }

            object child;
            SdkRelayRefusal refusal;
            if (!_relay.TryInvoke(hop.RowKey, owner, new object[0], out child, out refusal))
            {
                refused = Refusal.Of(hop.RowKey, refusal);

                return false;
            }

            if (child != null)
            {
                next.Add(child);
            }

            return true;
        }

        /// <summary>
        /// 更新が受け取る値の組。全件へ同じ組を配る指定と、対象ごとの組の並びのどちらかを取る。
        /// 値の検証は対象を解く前にすべて済ませる。
        /// </summary>
        private static bool TryChanges(
            McpMethodContext context,
            ToolFields tool,
            ToolFieldSet set,
            out IList<Change> changes,
            out bool spread,
            out string code,
            out string message)
        {
            changes = null;
            spread = false;
            code = null;
            message = null;
            object single;
            object many = null;
            bool hasSingle = context.Params.TryGetValue(ValueName, out single);
            bool hasMany = tool.Access.Kind == ToolAccessKind.Element
                && context.Params.TryGetValue(ValuesName, out many);
            if (hasSingle == hasMany)
            {
                code = ToolEnvelope.InvalidArgument;
                message = tool.Access.Kind == ToolAccessKind.Element
                    ? ValueName + " と " + ValuesName + " のどちらか一方を渡す。"
                    : ValueName + " を渡す。";

                return false;
            }

            List<Change> taken = new List<Change>();
            if (hasSingle)
            {
                Change one;
                if (!TryChange(set, single, out one, out code, out message))
                {
                    return false;
                }

                taken.Add(one);
                spread = true;
            }
            else
            {
                object[] items = many as object[];
                if (items == null)
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = ValuesName + " は値の組の配列でなければならない。";

                    return false;
                }

                foreach (object item in items)
                {
                    Change one;
                    if (!TryChange(set, item, out one, out code, out message))
                    {
                        return false;
                    }

                    taken.Add(one);
                }
            }

            changes = taken;

            return true;
        }

        /// <summary>値の組1つを、書き込む項目と値へ直す。</summary>
        private static bool TryChange(
            ToolFieldSet set, object given, out Change change, out string code, out string message)
        {
            change = null;
            code = null;
            message = null;
            IDictionary<string, object> members = given as IDictionary<string, object>;
            if (members == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = ValueName + " は項目の組でなければならない。";

                return false;
            }

            string unknown = members.Keys
                .Where(n => !set.Fields.Any(f => string.Equals(f.Name, n, StringComparison.Ordinal)))
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = "知らない項目を渡している: " + unknown;

                return false;
            }

            List<ToolField> writing = set.Fields
                .Where(f => members.ContainsKey(f.Name))
                .ToList();
            List<object> values = new List<object>(writing.Count);
            foreach (ToolField field in writing)
            {
                object value;
                if (!ValueInput.TryFromJson(
                    field.Type, members[field.Name], out value, out code, out message))
                {
                    code = code ?? ToolEnvelope.NotApplicable;
                    message = message ?? (field.Name + " は値として受け取れない型を取る。");

                    return false;
                }

                values.Add(value);
            }

            change = new Change(writing, values);

            return true;
        }

        /// <summary>
        /// 親ごとの組の並び。同じ親を2つ以上の組へ書けず、ハンドルも組をまたいで重ねられない。
        /// </summary>
        private static bool TryAssignments(
            McpMethodContext context,
            ToolElements tool,
            out IList<Assignment> assignments,
            out bool byHandle,
            out string code,
            out string message)
        {
            assignments = null;
            byHandle = false;
            code = null;
            message = null;
            object given;
            object[] items = context.Params.TryGetValue(AssignmentsName, out given)
                ? given as object[]
                : null;
            if (items == null || items.Length == 0)
            {
                code = ToolEnvelope.InvalidArgument;
                message = AssignmentsName + " は1件以上の組の配列でなければならない。";

                return false;
            }

            HashSet<long> parents = new HashSet<long>();
            HashSet<long> used = new HashSet<long>();
            List<Assignment> built = new List<Assignment>();
            foreach (object item in items)
            {
                Assignment one;
                if (!TryAssignment(context, tool, item, parents, used, out one, out code, out message))
                {
                    return false;
                }

                built.Add(one);
            }

            if (built.Any(a => a.Owner != null) && built.Any(a => a.Owner == null))
            {
                code = ToolEnvelope.InvalidArgument;
                message = AssignmentsName + " が親の指し方を混ぜている。全部の組が "
                    + ParentIndexName + " を持つか、全部の組が " + ParentHandleName
                    + " を持つかのどちらかにする。";

                return false;
            }

            assignments = built;
            byHandle = built[0].Owner != null;

            return true;
        }

        /// <summary>親ごとの組1件。</summary>
        private static bool TryAssignment(
            McpMethodContext context,
            ToolElements tool,
            object given,
            ISet<long> parents,
            ISet<long> used,
            out Assignment assignment,
            out string code,
            out string message)
        {
            assignment = null;
            code = null;
            message = null;
            IDictionary<string, object> members = given as IDictionary<string, object>;
            if (members == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = AssignmentsName + " の組は項目の組でなければならない。";

                return false;
            }

            string unknown = members.Keys
                .Where(n => !string.Equals(n, ParentIndexName, StringComparison.Ordinal)
                    && !string.Equals(n, ParentHandleName, StringComparison.Ordinal)
                    && !string.Equals(n, TargetNames.Element.Handles, StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = "知らない項目を渡している: " + unknown;

                return false;
            }

            int position;
            object owner;
            long pointing;
            if (!TryParent(
                context, tool, members, out position, out owner, out pointing, out code, out message))
            {
                return false;
            }

            if (!parents.Add(pointing))
            {
                code = ToolEnvelope.InvalidArgument;
                message = "同じ親を2つ以上の組へ書いている: " + pointing;

                return false;
            }

            object handles;
            object[] listed = members.TryGetValue(TargetNames.Element.Handles, out handles)
                ? handles as object[]
                : null;
            if (listed == null || listed.Length == 0)
            {
                code = ToolEnvelope.InvalidArgument;
                message = AssignmentsName + " の組は1件以上の "
                    + TargetNames.Element.Handles + " を持つ。";

                return false;
            }

            List<long> ids = new List<long>();
            List<object> held = new List<object>();
            foreach (object one in listed)
            {
                long id;
                if (!TryInteger(one, out id))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = TargetNames.Element.Handles + " は整数の配列でなければならない。";

                    return false;
                }

                if (!used.Add(id))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = "同じハンドルを二度以上指している: " + id;

                    return false;
                }

                object item = Held(context, Accepted(tool.Access, null, true), id);
                if (item == null)
                {
                    code = ToolEnvelope.InvalidHandle;
                    message = "台帳に無いハンドルを指している: " + id;

                    return false;
                }

                ids.Add(id);
                held.Add(item);
            }

            assignment = new Assignment(position, owner, ids, held);

            return true;
        }

        /// <summary>
        /// 組が指す親。位置で指す組は親の列の中の位置を、ハンドルで指す組は台帳の実体を持つ。
        /// </summary>
        private static bool TryParent(
            McpMethodContext context,
            ToolElements tool,
            IDictionary<string, object> members,
            out int position,
            out object owner,
            out long pointing,
            out string code,
            out string message)
        {
            position = -1;
            owner = null;
            pointing = 0;
            code = null;
            message = null;
            object given;
            bool byIndex = members.TryGetValue(ParentIndexName, out given)
                && TryIndex(given, out position);
            object held;
            long id = 0;
            bool byHandle = members.TryGetValue(ParentHandleName, out held)
                && TryInteger(held, out id);
            if (byIndex == byHandle)
            {
                code = ToolEnvelope.InvalidArgument;
                message = AssignmentsName + " の組は " + ParentIndexName + " と "
                    + ParentHandleName + " のどちらか1つを持つ。";

                return false;
            }

            if (byIndex)
            {
                pointing = position;

                return true;
            }

            if (tool.Access.Owner == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = ParentHandleName
                    + " はこのツールでは指定できない。親の型がハンドルを発行しないためである。";

                return false;
            }

            owner = Held(context, tool.Access.Owner, id);
            if (owner == null)
            {
                code = ToolEnvelope.InvalidHandle;
                message = ParentHandleName + " が台帳に無いハンドルを指している: " + id;

                return false;
            }

            pointing = id;

            return true;
        }

        /// <summary>そのハンドルが指す、期待する型の実体。指していなければ null。</summary>
        private static object Held(McpMethodContext context, Type type, long id)
        {
            object held;

            return id >= int.MinValue && id <= int.MaxValue
                && context.Handles.TryGet((int)id, type.FullName, out held)
                    ? held
                    : null;
        }

        /// <summary>
        /// 相手にするPMX。PMXから得ない受け手では何も決めない。対象をハンドルで指した呼び出しは
        /// PMXを相手にしないので、複製も作らない。
        /// </summary>
        private bool TryTake(
            McpMethodContext context,
            ToolReceiver receiver,
            long? handle,
            bool held,
            out PmxTarget target,
            out Refusal refused)
        {
            target = null;
            refused = null;
            if (receiver.Kind != ToolReceiverKind.Pmx)
            {
                return true;
            }

            if (held)
            {
                target = new PmxTarget(null, false);

                return true;
            }

            string code;
            string message;
            if (_pmx.TryTake(handle, context.Handles, out target, out code, out message))
            {
                return true;
            }

            refused = new Refusal(ToolEnvelope.Failure(code, message));

            return false;
        }

        /// <summary>
        /// まとめて反映する呼び出しを持つのは、複製して編集する分類だけである。現在のPMXの複製を
        /// 相手にしていない呼び出しは、直に変える側なので反映を持たない。
        /// </summary>
        private static bool Reflects(ToolReceiver receiver, PmxTarget target)
        {
            return receiver.Kind == ToolReceiverKind.Pmx
                && receiver.Edit == EditKind.DuplicateEdit
                && target != null
                && target.Current;
        }

        /// <summary>
        /// SDKのメンバーを呼ぶ段の位置。複製を相手にする呼び出しはまとめて反映するまで確定せず、
        /// 読み取りは何も変えない。ハンドルが指すものへ直に働く呼び出しは、そのメンバーが返った
        /// ところで確定する。
        /// </summary>
        private static EditStage Changing(ToolReceiver receiver, PmxTarget target)
        {
            if (receiver.Edit == EditKind.Read)
            {
                return EditStage.BeforeCommit;
            }

            return target != null && target.Current
                ? EditStage.BeforeCommit
                : EditStage.AtCommit;
        }

        /// <summary>
        /// まとめて反映する段の位置。反映を持たない呼び出しでは、メンバーを呼ぶ段の位置のままである。
        /// </summary>
        private static EditStage Reflecting(ToolReceiver receiver, PmxTarget target, EditStage stage)
        {
            return Reflects(receiver, target) ? EditStage.AtCommit : stage;
        }

        /// <summary>複製編集型の呼び出しを、現在のPMXへ反映する。</summary>
        private Refusal Commit(ToolReceiver receiver, PmxTarget target)
        {
            if (!Reflects(receiver, target))
            {
                return null;
            }

            string code;
            string message;

            return _pmx.TryCommit(target, out code, out message)
                ? null
                : new Refusal(ToolEnvelope.Failure(code, message));
        }

        /// <summary>その行のリストの中継。持たなければ偽で、断る内容を渡す。</summary>
        private bool TryList(string rowKey, out SdkList list, out Refusal refused)
        {
            refused = null;
            if (_lists.TryGetValue(rowKey, out list))
            {
                return true;
            }

            refused = new Refusal(ToolEnvelope.Failure(
                ToolEnvelope.NotApplicable, "中継を持たないリスト: " + rowKey));

            return false;
        }

        /// <summary>
        /// 受け手。接続の道から得るものはビルド時に決めた道を辿り、PMXから得るものはその実体を
        /// そのまま渡す。静的なメンバーは相手を取らない。
        /// </summary>
        private object Receiver(ToolReceiver receiver, PmxTarget target)
        {
            if (receiver.Kind == ToolReceiverKind.Pmx)
            {
                return target == null ? null : target.Pmx;
            }

            if (receiver.TypeName == null)
            {
                return null;
            }

            SdkReceiver found;
            if (!_receivers.TryGetValue(receiver.TypeName, out found))
            {
                throw new InvalidOperationException("受け手を得る道が無い: " + receiver.TypeName);
            }

            return found(_connection);
        }

        /// <summary>
        /// SDKへの呼び出しをUIスレッドで行う。行えなければ偽を返し、落ちたときは
        /// <paramref name="failure"/> にその例外を持たせる。
        /// </summary>
        private static bool Run(McpMethodContext context, Action action, out Exception failure)
        {
            Exception caught = null;
            bool ran = context.Ui.TryInvokeOnUi(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });
            failure = caught;

            return ran;
        }

        /// <summary>そのツールが受け取る名前。PMXから受け手を得るものは切り替えも受け取る。</summary>
        private static IList<string> Known(IList<string> names, ToolReceiver receiver)
        {
            List<string> known = new List<string>(names);
            if (receiver.Kind == ToolReceiverKind.Pmx)
            {
                known.Add(PmxSession.HandleName);
            }

            return known;
        }

        /// <summary>知らない名前の引数を渡す要求を断る。名前の検証は適用より先に済ませる。</summary>
        private static bool TryOnlyKnown(
            McpMethodContext context,
            IList<string> known,
            out string code,
            out string message)
        {
            string unknown = context.Params.Keys
                .Where(n => !known.Contains(n, StringComparer.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = "知らない引数を渡している: " + unknown;

                return false;
            }

            code = null;
            message = null;

            return true;
        }

        /// <summary>
        /// 確認の要否。開いているPMXを空にする呼び出しだけが対象で要否の分かれる初期化に当たり、
        /// 対象を指定した呼び出しは空にするのがメモリの上の生成物なので確認を要さない。
        /// </summary>
        private static bool TryPassDanger(
            ToolCall call, long? handle, bool confirm, out string code, out string message)
        {
            if (call.Danger == DangerKind.Reset
                && call.Receiver.Kind == ToolReceiverKind.Pmx)
            {
                return ConfirmGate.TryPassClear(!handle.HasValue, confirm, out code, out message);
            }

            return ConfirmGate.TryPass(call.Danger, confirm, out code, out message);
        }

        private static bool TryConfirm(
            McpMethodContext context, out bool confirm, out string code, out string message)
        {
            code = null;
            message = null;
            confirm = false;
            object value;
            if (!context.Params.TryGetValue(ConfirmName, out value))
            {
                return true;
            }

            if (!(value is bool))
            {
                code = ToolEnvelope.InvalidArgument;
                message = ConfirmName + " は真偽でなければならない。";

                return false;
            }

            confirm = (bool)value;

            return true;
        }

        /// <summary>どのPMXを見るかの切り替え。PMXから受け手を得ない呼び出しは受け取らない。</summary>
        private static bool TryPmxHandle(
            McpMethodContext context,
            ToolReceiver receiver,
            out long? handle,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            handle = null;
            object value;
            if (receiver.Kind != ToolReceiverKind.Pmx
                || !context.Params.TryGetValue(PmxSession.HandleName, out value))
            {
                return true;
            }

            long taken;
            if (!TryInteger(value, out taken))
            {
                code = ToolEnvelope.InvalidArgument;
                message = PmxSession.HandleName + " は整数でなければならない。";

                return false;
            }

            handle = taken;

            return true;
        }

        /// <summary>位置と件数。渡していなければ既定を使う。</summary>
        private static bool TryCount(
            McpMethodContext context,
            string name,
            int fallback,
            int least,
            out int taken,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            taken = fallback;
            object value;
            if (!context.Params.TryGetValue(name, out value))
            {
                return true;
            }

            long number;
            if (!TryInteger(value, out number) || number < least || number > int.MaxValue)
            {
                code = ToolEnvelope.InvalidArgument;
                message = name + " は " + least.ToString(CultureInfo.InvariantCulture)
                    + " 以上の整数でなければならない。";

                return false;
            }

            taken = (int)number;

            return true;
        }

        /// <summary>返す項目の頼み方。渡していなければ選んでいないものとする。</summary>
        private static bool TryFields(
            McpMethodContext context, out IList<string> requested, out string code, out string message)
        {
            code = null;
            message = null;
            requested = null;
            object value;
            if (!context.Params.TryGetValue(FieldsName, out value))
            {
                return true;
            }

            object[] items = value as object[];
            if (items == null || items.Any(i => !(i is string)))
            {
                code = ToolEnvelope.InvalidArgument;
                message = FieldsName + " は項目名の配列でなければならない。";

                return false;
            }

            requested = items.Cast<string>().ToList();

            return true;
        }

        /// <summary>対象を指すハンドルの並び。</summary>
        private static bool TryHandles(
            McpMethodContext context,
            TargetNames names,
            out IList<long> handles,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            handles = null;
            object value;
            if (!context.Params.TryGetValue(names.Handles, out value))
            {
                return true;
            }

            object[] items = value as object[];
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Handles + " はハンドルの配列でなければならない。";

                return false;
            }

            List<long> taken = new List<long>();
            foreach (object item in items)
            {
                long number;
                if (!TryInteger(item, out number))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = names.Handles + " は整数の配列でなければならない。";

                    return false;
                }

                taken.Add(number);
            }

            handles = taken;

            return true;
        }

        /// <summary>1つの集合の指定。持っている指し方だけを載せる。</summary>
        private static bool TryTargets(
            McpMethodContext context,
            TargetNames names,
            bool handles,
            out TargetRequest request,
            out string code,
            out string message)
        {
            request = null;
            IList<int> indices;
            int? start;
            int? count;
            bool? all;
            IList<long> held = null;
            if (!TryIndices(context, names, out indices, out code, out message)
                || !TryRange(context, names, out start, out count, out code, out message)
                || !TryAll(context, names, out all, out code, out message)
                || (handles && !TryHandles(context, names, out held, out code, out message)))
            {
                return false;
            }

            request = new TargetRequest(indices, start, count, all, held);

            return true;
        }

        private static bool TryIndices(
            McpMethodContext context,
            TargetNames names,
            out IList<int> indices,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            indices = null;
            object value;
            if (!context.Params.TryGetValue(names.Indices, out value))
            {
                return true;
            }

            object[] items = value as object[];
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Indices + " は位置の配列でなければならない。";

                return false;
            }

            List<int> taken = new List<int>();
            foreach (object item in items)
            {
                int number;
                if (!TryIndex(item, out number))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = names.Indices + " は整数の配列でなければならない。";

                    return false;
                }

                taken.Add(number);
            }

            indices = taken;

            return true;
        }

        private static bool TryRange(
            McpMethodContext context,
            TargetNames names,
            out int? start,
            out int? count,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            start = null;
            count = null;
            object value;
            if (!context.Params.TryGetValue(names.Range, out value))
            {
                return true;
            }

            IDictionary<string, object> members = value as IDictionary<string, object>;
            object given;
            int taken;
            if (members == null
                || !members.TryGetValue(StartName, out given)
                || !TryIndex(given, out taken))
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Range + " は " + StartName + " と " + CountName
                    + " の組でなければならない。";

                return false;
            }

            start = taken;
            if (!members.TryGetValue(CountName, out given) || !TryIndex(given, out taken))
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Range + " は " + StartName + " と " + CountName
                    + " の組でなければならない。";

                return false;
            }

            count = taken;

            return true;
        }

        private static bool TryAll(
            McpMethodContext context,
            TargetNames names,
            out bool? all,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            all = null;
            object value;
            if (!context.Params.TryGetValue(names.All, out value))
            {
                return true;
            }

            if (!(value is bool))
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.All + " は真偽でなければならない。";

                return false;
            }

            all = (bool)value;

            return true;
        }

        private static bool TryIndex(object value, out int number)
        {
            number = 0;
            long taken;
            if (!TryInteger(value, out taken) || taken < int.MinValue || taken > int.MaxValue)
            {
                return false;
            }

            number = (int)taken;

            return true;
        }

        private static bool TryInteger(object value, out long number)
        {
            number = 0;
            if (!ValueInput.IsNumber(value))
            {
                return false;
            }

            double written = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (written != Math.Floor(written) || written < long.MinValue || written > long.MaxValue)
            {
                return false;
            }

            number = (long)written;

            return true;
        }

        private static bool TryValue(
            McpMethodContext context,
            ToolArgument argument,
            out object value,
            out string code,
            out string message)
        {
            value = null;
            object json;
            if (!context.Params.TryGetValue(argument.Name, out json))
            {
                code = ToolEnvelope.InvalidArgument;
                message = argument.Name + " を渡していない。";

                return false;
            }

            if (ValueInput.TryFromJson(argument.Type, json, out value, out code, out message))
            {
                return true;
            }

            if (code == null)
            {
                code = ToolEnvelope.NotApplicable;
                message = argument.Name + " は値として受け取れない型を取る。";
            }

            return false;
        }

        /// <summary>
        /// 出力に現れる引数を持つ呼び出しの応答。呼び出しが返した並びを、引数の名前の組へ直す。
        /// </summary>
        private static IDictionary<string, object> Written(ToolCall call, object result)
        {
            object[] values = result as object[];
            if (values == null || values.Length != call.Outputs.Count)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable, "出力の引数の数が呼び出しの結果と合わない。");
            }

            Dictionary<string, object> members =
                new Dictionary<string, object>(StringComparer.Ordinal);
            List<string> warnings = new List<string>();
            for (int at = 0; at < call.Outputs.Count; at++)
            {
                object json;
                IList<string> written;
                string code;
                string message;
                if (!ValueShape.TryToJson(
                    call.Outputs[at].Type,
                    values[at],
                    ImageTransfer.DefaultMaxLongSide,
                    out json,
                    out written,
                    out code,
                    out message))
                {
                    return Unwritable(call.Outputs[at].Type, code, message);
                }

                members.Add(call.Outputs[at].Name, json);
                warnings.AddRange(written);
            }

            return ToolEnvelope.Success(members, warnings);
        }

        private static IDictionary<string, object> Written(Type declared, object value)
        {
            object json;
            IList<string> warnings;
            string code;
            string message;
            if (!ValueShape.TryToJson(
                declared,
                value,
                ImageTransfer.DefaultMaxLongSide,
                out json,
                out warnings,
                out code,
                out message))
            {
                return Unwritable(declared, code, message);
            }

            return ToolEnvelope.Success(json, warnings);
        }

        private static IDictionary<string, object> Unwritable(
            Type declared, string code, string message)
        {
            return ToolEnvelope.Failure(
                code ?? ToolEnvelope.NotApplicable,
                message ?? ("返す値を写せない型を取る: " + declared.FullName));
        }

        private static IDictionary<string, object> Unavailable()
        {
            return ToolEnvelope.Failure(
                ToolEnvelope.NotApplicable, "いまは要求を受け付けていない。");
        }

        /// <summary>
        /// SDKの呼び出しが落ちたことを返す。例外の内容だけでは、どこまで変わったのかを受け取った
        /// 先が判じられないので、失敗した位置から決まる状態を添える。
        /// </summary>
        private static IDictionary<string, object> Failed(Exception failure, EditStage stage)
        {
            return ToolEnvelope.Failure(
                ToolEnvelope.OperationFailed,
                failure.Message + " " + EditOutcome.Describe(EditOutcome.Resolve(stage)));
        }

        /// <summary>UIスレッドの中で決まった断り。包みは同じスレッドの外で返す。</summary>
        /// <summary>対象1件の居場所。</summary>
        private sealed class Spot
        {
            public Spot(
                object owner,
                int position,
                int parentIndex,
                int indexInParent,
                object item,
                long? parentHandle = null)
            {
                Owner = owner;
                Position = position;
                ParentIndex = parentIndex;
                IndexInParent = indexInParent;
                Item = item;
                ParentHandle = parentHandle;
            }

            /// <summary>親をまたいで平らにした列の中の位置。要求が対象を指す位置である。</summary>
            public int Position { get; }

            /// <summary>その対象を並べるリストを持つ実体。リストに属さない対象では null。</summary>
            public object Owner { get; }

            /// <summary>親の列の中の位置。親を辿らない道と、ハンドルで指した対象では -1。</summary>
            public int ParentIndex { get; }

            /// <summary>親の中の位置。ハンドルで指した対象では -1。</summary>
            public int IndexInParent { get; }

            /// <summary>親を指すハンドル。親を位置で指した呼び出しでは null。</summary>
            public long? ParentHandle { get; }

            /// <summary>対象そのもの。</summary>
            public object Item { get; }
        }

        /// <summary>要素と親の指し方の指定。要素を相手にしない道では、どちらも持たない。</summary>
        private sealed class Pointed
        {
            public Pointed(
                TargetRequest elements,
                TargetRequest parents,
                bool byHandle,
                bool parentByHandle = false)
            {
                Elements = elements;
                Parents = parents;
                ByHandle = byHandle;
                ParentByHandle = parentByHandle;
            }

            /// <summary>要素の指し方。要素を相手にしない道では null。</summary>
            public TargetRequest Elements { get; }

            /// <summary>親の指し方。親を辿らない道では null。</summary>
            public TargetRequest Parents { get; }

            /// <summary>要素をハンドルで指しているか。</summary>
            public bool ByHandle { get; }

            /// <summary>親をハンドルで指しているか。</summary>
            public bool ParentByHandle { get; }

            /// <summary>対象そのものをハンドルで指しているか。どのPMXを見るかを切り替えられない。</summary>
            public bool Held
            {
                get { return ByHandle || ParentByHandle; }
            }

            /// <summary>何も指していない指定。要素を相手にしない道で使う。</summary>
            public static Pointed None { get; } = new Pointed(null, null, false);
        }

        /// <summary>対象1件へ書き込む項目と値。</summary>
        private sealed class Change
        {
            public Change(IList<ToolField> fields, IList<object> values)
            {
                Fields = fields;
                Values = values;
            }

            /// <summary>書き込む項目。</summary>
            public IList<ToolField> Fields { get; }

            /// <summary>項目と同じ並びの、書き込む値。</summary>
            public IList<object> Values { get; }
        }

        /// <summary>親1件へ加える組。</summary>
        private sealed class Assignment
        {
            public Assignment(int parent, object owner, IList<long> handles, IList<object> items)
            {
                Parent = parent;
                Owner = owner;
                Handles = handles;
                Items = items;
            }

            /// <summary>親の列の中の位置。親をハンドルで指した組では -1。</summary>
            public int Parent { get; }

            /// <summary>ハンドルが指す親。親を位置で指した組では null。</summary>
            public object Owner { get; }

            /// <summary>加えるものを指すハンドル。</summary>
            public IList<long> Handles { get; }

            /// <summary>ハンドルと同じ並びの、加えるものの実体。</summary>
            public IList<object> Items { get; }
        }

        private sealed class Refusal
        {
            public Refusal(IDictionary<string, object> envelope)
            {
                Envelope = envelope;
            }

            public IDictionary<string, object> Envelope { get; }

            public static Refusal Of(string rowKey, SdkRelayRefusal refusal)
            {
                return new Refusal(
                    ToolEnvelope.Failure(ToolEnvelope.NotApplicable, Describe(rowKey, refusal)));
            }

            private static string Describe(string rowKey, SdkRelayRefusal refusal)
            {
                switch (refusal)
                {
                    case SdkRelayRefusal.Unknown:
                        return "中継を持たない呼び出し: " + rowKey;

                    case SdkRelayRefusal.Unresolved:
                        return "この版のSDKでは組み立てられなかった呼び出し: " + rowKey;

                    case SdkRelayRefusal.Disabled:
                        return "この版のSDKに無くなった呼び出し: " + rowKey;

                    default:
                        return "断られた呼び出し: " + rowKey;
                }
            }
        }
    }
}
