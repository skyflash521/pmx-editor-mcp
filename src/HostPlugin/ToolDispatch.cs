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

        /// <summary>SDKのメンバーを1度呼ぶ。</summary>
        private object Invoke(McpMethodContext context, ToolCall call)
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
                if (!TryTake(context, call.Receiver, handle, out target, out refused))
                {
                    return;
                }

                stage = Changing(call.Receiver, target);
                object value;
                SdkRelayRefusal refusal;
                if (!_relay.TryInvoke(
                    call.RowKey, Receiver(call.Receiver, target), arguments, out value, out refusal))
                {
                    refused = Refusal.Of(call.RowKey, refusal);

                    return;
                }

                result = value;
                stage = Reflecting(call.Receiver, stage);
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

            return call.Result == null
                ? ToolEnvelope.Success(null)
                : Written(call.Result, result);
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
            List<string> known = new List<string> { FieldsName };
            if (tool.Listing)
            {
                known.Add(OffsetName);
                known.Add(LimitName);
            }

            if (!TryOnlyKnown(context, Known(known, tool.Receiver), out code, out message)
                || !TryPmxHandle(context, tool.Receiver, out handle, out code, out message)
                || !TryCount(context, OffsetName, 0, 0, out offset, out code, out message)
                || !TryCount(context, LimitName, int.MaxValue, 1, out limit, out code, out message)
                || !TryFields(context, out requested, out code, out message)
                || !FieldSelection.TryResolve(
                    requested,
                    tool.Fields.Select(f => f.Name).ToList(),
                    new string[0],
                    out selected,
                    out code,
                    out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            IList<ToolField> reading = selected
                .Select(n => tool.Fields.First(f => string.Equals(f.Name, n, StringComparison.Ordinal)))
                .ToList();
            object[] values = new object[reading.Count];
            Refusal refused = null;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                if (!TryTake(context, tool.Receiver, handle, out target, out refused))
                {
                    return;
                }

                for (int at = 0; at < reading.Count; at++)
                {
                    object value;
                    SdkRelayRefusal refusal;
                    if (!_relay.TryInvoke(
                        reading[at].RowKey,
                        Receiver(tool.Receiver, target),
                        new object[0],
                        out value,
                        out refusal))
                    {
                        refused = Refusal.Of(reading[at].RowKey, refusal);

                        return;
                    }

                    values[at] = value;
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

            Dictionary<string, object> item =
                new Dictionary<string, object>(StringComparer.Ordinal);
            List<string> warnings = new List<string>();
            for (int at = 0; at < reading.Count; at++)
            {
                object json;
                IList<string> written;
                if (!ValueShape.TryToJson(
                    reading[at].Type,
                    values[at],
                    ImageTransfer.DefaultMaxLongSide,
                    out json,
                    out written,
                    out code,
                    out message))
                {
                    return Unwritable(reading[at].Type, code, message);
                }

                item.Add(reading[at].Name, json);
                warnings.AddRange(written);
            }

            return tool.Listing
                ? Listed(context, item, offset, limit, warnings)
                : ToolEnvelope.Success(item, warnings);
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
                Known(tool.Fields.Select(f => f.Name).ToList(), tool.Receiver),
                out code,
                out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            List<ToolField> writing = tool.Fields
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
        /// PMXの項目を、値の組で受け取って書く。複製編集型は複製へ書いてまとめて反映するので、
        /// 途中まで書けた状態がエディタへ残らない。
        /// </summary>
        private object WriteValue(McpMethodContext context, ToolFields tool)
        {
            string code;
            string message;
            long? handle;
            if (!TryOnlyKnown(
                context, Known(new List<string> { ValueName }, tool.Receiver), out code, out message)
                || !TryPmxHandle(context, tool.Receiver, out handle, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            object given;
            IDictionary<string, object> members = context.Params.TryGetValue(ValueName, out given)
                ? given as IDictionary<string, object>
                : null;
            if (members == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument, ValueName + " は項目の組でなければならない。");
            }

            string unknown = members.Keys
                .Where(n => !tool.Fields.Any(f => string.Equals(f.Name, n, StringComparison.Ordinal)))
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidArgument, "知らない項目を渡している: " + unknown);
            }

            List<ToolField> writing = tool.Fields
                .Where(f => members.ContainsKey(f.Name))
                .ToList();
            object[] values = new object[writing.Count];
            for (int at = 0; at < writing.Count; at++)
            {
                object value;
                if (!ValueInput.TryFromJson(
                    writing[at].Type, members[writing[at].Name], out value, out code, out message))
                {
                    return ToolEnvelope.Failure(
                        code ?? ToolEnvelope.NotApplicable,
                        message ?? (writing[at].Name + " は値として受け取れない型を取る。"));
                }

                values[at] = value;
            }

            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                if (!TryTake(context, tool.Receiver, handle, out target, out refused))
                {
                    return;
                }

                stage = Changing(tool.Receiver, target);
                for (int at = 0; at < writing.Count; at++)
                {
                    object ignored;
                    SdkRelayRefusal refusal;
                    if (!_relay.TryInvoke(
                        writing[at].RowKey,
                        Receiver(tool.Receiver, target),
                        new[] { values[at] },
                        out ignored,
                        out refusal))
                    {
                        refused = Refusal.Of(writing[at].RowKey, refusal);

                        return;
                    }
                }

                stage = Reflecting(tool.Receiver, stage);
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
                : ToolEnvelope.Success(SetResponse.Updated(1));
        }

        /// <summary>所有するリストの末尾へ、ハンドルが指す要素を加える。</summary>
        private object Add(McpMethodContext context, ToolElements tool)
        {
            string code;
            string message;
            long? handle;
            IList<long> given;
            ResolvedTargets resolved;

            handle = null;
            if (!TryOnlyKnown(
                context,
                new List<string> { TargetNames.Element.Handles },
                out code,
                out message)
                || !TryHandles(context, out given, out code, out message)
                || !TargetSelection.TryResolve(
                    new TargetRequest(null, null, null, null, given),
                    TargetForm.Handles,
                    0,
                    id => Held(context, tool.Element, id) != null,
                    out resolved,
                    out code,
                    out message,
                    TargetNames.Element))
            {
                return ToolEnvelope.Failure(code, message);
            }

            object[] items = resolved.Handles.Select(id => Held(context, tool.Element, id)).ToArray();
            int[] indices = new int[items.Length];
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                SdkList list;
                if (!TryTake(context, tool.Receiver, handle, out target, out refused)
                    || !TryList(tool, out list, out refused))
                {
                    return;
                }

                stage = Changing(tool.Receiver, target);
                for (int at = 0; at < items.Length; at++)
                {
                    list.Add(target.Pmx, items[at]);
                    indices[at] = list.Count(target.Pmx) - 1;
                }

                stage = Reflecting(tool.Receiver, stage);
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

            foreach (long id in resolved.Handles)
            {
                HandleReleaseResult released;
                context.Handles.TryRelease((int)id, out released);
            }

            return ToolEnvelope.Success(SetResponse.Added(indices));
        }

        /// <summary>所有するリストから、指した位置の要素を取り除く。</summary>
        private object Remove(McpMethodContext context, ToolElements tool)
        {
            string code;
            string message;
            long? handle;
            TargetRequest request;
            if (!TryOnlyKnown(
                context,
                Known(
                    new List<string>
                    {
                        TargetNames.Element.Indices,
                        TargetNames.Element.Range,
                        TargetNames.Element.All,
                    },
                    tool.Receiver),
                out code,
                out message)
                || !TryPmxHandle(context, tool.Receiver, out handle, out code, out message)
                || !TryTargets(context, out request, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            int removed = 0;
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            string refusedCode = null;
            string refusedMessage = null;
            Exception failure;
            if (!Run(context, () =>
            {
                PmxTarget target;
                SdkList list;
                if (!TryTake(context, tool.Receiver, handle, out target, out refused)
                    || !TryList(tool, out list, out refused))
                {
                    return;
                }

                ResolvedTargets resolved;
                if (!TargetSelection.TryResolve(
                    request,
                    TargetForm.Indices | TargetForm.Range | TargetForm.All,
                    list.Count(target.Pmx),
                    id => false,
                    out resolved,
                    out refusedCode,
                    out refusedMessage,
                    TargetNames.Element))
                {
                    return;
                }

                stage = Changing(tool.Receiver, target);
                foreach (int index in resolved.Indices.OrderByDescending(i => i))
                {
                    list.RemoveAt(target.Pmx, index);
                }

                removed = resolved.Indices.Count;
                stage = Reflecting(tool.Receiver, stage);
                refused = Commit(tool.Receiver, target);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            if (refusedCode != null)
            {
                return ToolEnvelope.Failure(refusedCode, refusedMessage);
            }

            return refused != null
                ? refused.Envelope
                : ToolEnvelope.Success(SetResponse.Removed(removed));
        }

        /// <summary>一覧の形で1件を返す。位置と件数はこの1件へ働く。</summary>
        private static object Listed(
            McpMethodContext context,
            IDictionary<string, object> item,
            int offset,
            int limit,
            IList<string> warnings)
        {
            Page<IDictionary<string, object>> page;
            if (!Paging.TryTake(
                new[] { item },
                offset,
                limit,
                ResponseSize.ValueChars(context.BudgetChars),
                taken => Serializer.Serialize(taken).Length,
                out page))
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.ResponseTooLarge, "値の枠に1件も収まらない。");
            }

            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { TotalName, page.Total },
                { ItemsName, page.Items.ToArray() },
            };
            if (page.NextOffset.HasValue)
            {
                value.Add(NextOffsetName, page.NextOffset.Value);
            }

            return ToolEnvelope.Success(value, warnings.Concat(page.Warnings).ToList());
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

        /// <summary>相手にするPMX。PMXから得ない受け手では何も決めない。</summary>
        private bool TryTake(
            McpMethodContext context,
            ToolReceiver receiver,
            long? handle,
            out PmxTarget target,
            out Refusal refused)
        {
            target = null;
            refused = null;
            if (receiver.Kind != ToolReceiverKind.Pmx)
            {
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

        /// <summary>まとめて反映する呼び出しを持つのは、複製して編集する分類だけである。</summary>
        private static bool Reflects(ToolReceiver receiver)
        {
            return receiver.Kind == ToolReceiverKind.Pmx
                && receiver.Edit == EditKind.DuplicateEdit;
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
        private static EditStage Reflecting(ToolReceiver receiver, EditStage stage)
        {
            return Reflects(receiver) ? EditStage.AtCommit : stage;
        }

        /// <summary>複製編集型の呼び出しを、現在のPMXへ反映する。</summary>
        private Refusal Commit(ToolReceiver receiver, PmxTarget target)
        {
            if (!Reflects(receiver))
            {
                return null;
            }

            string code;
            string message;

            return _pmx.TryCommit(target, out code, out message)
                ? null
                : new Refusal(ToolEnvelope.Failure(code, message));
        }

        private bool TryList(ToolElements tool, out SdkList list, out Refusal refused)
        {
            refused = null;
            if (_lists.TryGetValue(tool.ListRowKey, out list))
            {
                return true;
            }

            refused = new Refusal(ToolEnvelope.Failure(
                ToolEnvelope.NotApplicable, "中継を持たないリスト: " + tool.ListRowKey));

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

        /// <summary>加える対象を指すハンドルの並び。</summary>
        private static bool TryHandles(
            McpMethodContext context, out IList<long> handles, out string code, out string message)
        {
            code = null;
            message = null;
            handles = null;
            object value;
            object[] items = context.Params.TryGetValue(TargetNames.Element.Handles, out value)
                ? value as object[]
                : null;
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = TargetNames.Element.Handles + " はハンドルの配列でなければならない。";

                return false;
            }

            List<long> taken = new List<long>();
            foreach (object item in items)
            {
                long number;
                if (!TryInteger(item, out number))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = TargetNames.Element.Handles + " は整数の配列でなければならない。";

                    return false;
                }

                taken.Add(number);
            }

            handles = taken;

            return true;
        }

        /// <summary>取り除く対象の指定。持っている指し方だけを載せる。</summary>
        private static bool TryTargets(
            McpMethodContext context, out TargetRequest request, out string code, out string message)
        {
            request = null;
            IList<int> indices;
            int? start;
            int? count;
            bool? all;
            if (!TryIndices(context, out indices, out code, out message)
                || !TryRange(context, out start, out count, out code, out message)
                || !TryAll(context, out all, out code, out message))
            {
                return false;
            }

            request = new TargetRequest(indices, start, count, all, null);

            return true;
        }

        private static bool TryIndices(
            McpMethodContext context, out IList<int> indices, out string code, out string message)
        {
            code = null;
            message = null;
            indices = null;
            object value;
            if (!context.Params.TryGetValue(TargetNames.Element.Indices, out value))
            {
                return true;
            }

            object[] items = value as object[];
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = TargetNames.Element.Indices + " は位置の配列でなければならない。";

                return false;
            }

            List<int> taken = new List<int>();
            foreach (object item in items)
            {
                int number;
                if (!TryIndex(item, out number))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = TargetNames.Element.Indices + " は整数の配列でなければならない。";

                    return false;
                }

                taken.Add(number);
            }

            indices = taken;

            return true;
        }

        private static bool TryRange(
            McpMethodContext context,
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
            if (!context.Params.TryGetValue(TargetNames.Element.Range, out value))
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
                message = TargetNames.Element.Range + " は " + StartName + " と " + CountName
                    + " の組でなければならない。";

                return false;
            }

            start = taken;
            if (!members.TryGetValue(CountName, out given) || !TryIndex(given, out taken))
            {
                code = ToolEnvelope.InvalidArgument;
                message = TargetNames.Element.Range + " は " + StartName + " と " + CountName
                    + " の組でなければならない。";

                return false;
            }

            count = taken;

            return true;
        }

        private static bool TryAll(
            McpMethodContext context, out bool? all, out string code, out string message)
        {
            code = null;
            message = null;
            all = null;
            object value;
            if (!context.Params.TryGetValue(TargetNames.Element.All, out value))
            {
                return true;
            }

            if (!(value is bool))
            {
                code = ToolEnvelope.InvalidArgument;
                message = TargetNames.Element.All + " は真偽でなければならない。";

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
