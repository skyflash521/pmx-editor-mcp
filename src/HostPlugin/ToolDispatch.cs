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

        /// <summary>Undoの記録を止めることを頼む共通引数の名前。</summary>
        public const string SuppressName = "suppressUndo";

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

        private readonly EventBindingTable _events;

        private readonly IDictionary<string, SdkReceiver> _receivers;

        private readonly IDictionary<string, SdkList> _lists;

        private readonly ResidentConnection _connection;

        private readonly PmxSession _pmx;

        private readonly PmxSession _bridged;

        private readonly UndoRecovery _recovery;

        private readonly IModifierKeys _modifiers;

        private ToolDispatch(
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            IDictionary<string, SdkList> lists,
            ResidentConnection connection,
            PmxSession pmx,
            PmxSession bridged,
            UndoRecovery recovery,
            IModifierKeys modifiers,
            EventBindingTable events)
        {
            _events = events;
            _relay = relay;
            _receivers = receivers;
            _lists = lists;
            _connection = connection;
            _pmx = pmx;
            _bridged = bridged;
            _recovery = recovery;
            _modifiers = modifiers;
        }

        /// <summary>結び付きの表が持つツールをすべて登録する。</summary>
        public static void AddTo(
            McpMethodTable methods,
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            IDictionary<string, SdkList> lists,
            ResidentConnection connection,
            PmxSession pmx,
            PmxSession bridged,
            UndoRecovery recovery,
            IDictionary<string, IList<ToolCall>> calls,
            IDictionary<string, ToolFields> aggregations,
            IDictionary<string, ToolElements> elements,
            IDictionary<string, ToolPrecondition> preconditions,
            IModifierKeys modifiers,
            EventBindingTable events)
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

            if (bridged == null)
            {
                throw new ArgumentNullException(nameof(bridged));
            }

            if (recovery == null)
            {
                throw new ArgumentNullException(nameof(recovery));
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

            if (preconditions == null)
            {
                throw new ArgumentNullException(nameof(preconditions));
            }

            if (modifiers == null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            ToolDispatch dispatch = new ToolDispatch(
                relay, receivers, lists, connection, pmx, bridged, recovery, modifiers, events);
            foreach (KeyValuePair<string, IList<ToolCall>> call in calls)
            {
                IList<ToolCall> bound = call.Value;
                ResolvedPrecondition precondition = Required(preconditions, calls, call.Key);
                methods.Add(
                    call.Key,
                    dispatch.Guarded(
                        Edit(bound), context => dispatch.Invoke(context, bound, precondition)));
            }

            foreach (KeyValuePair<string, ToolFields> aggregation in aggregations)
            {
                ToolFields bound = aggregation.Value;
                RequireNoPrecondition(preconditions, aggregation.Key);
                methods.Add(
                    aggregation.Key,
                    dispatch.Guarded(
                        bound.Receiver.Edit,
                        context => bound.Writes
                            ? dispatch.Write(context, bound)
                            : dispatch.Read(context, bound)));
            }

            foreach (KeyValuePair<string, ToolElements> element in elements)
            {
                ToolElements bound = element.Value;
                RequireNoPrecondition(preconditions, element.Key);
                methods.Add(
                    element.Key,
                    dispatch.Guarded(
                        bound.Receiver.Edit,
                        context => bound.Removes
                            ? dispatch.Remove(context, bound)
                            : dispatch.Add(context, bound)));
            }
        }

        /// <summary>
        /// 呼ぶ前に確かめることを満たしているか。UIスレッドの中で呼ぶこと——確かめるのと本体を
        /// 呼ぶのが分かれていると、その間に人が選択やキーを変えられる。満たしていなければ偽で、
        /// <paramref name="refused"/> に断りを持たせる。
        /// </summary>
        private bool TryMet(
            McpMethodContext context,
            ResolvedPrecondition precondition,
            object receiver,
            out Refusal refused)
        {
            refused = null;
            if (precondition == null)
            {
                return true;
            }

            string message;
            if (PreconditionGate.TryAccept(
                precondition.Kind,
                Counted(context, precondition, receiver),
                _modifiers.AnyHeld(),
                out message))
            {
                return true;
            }

            refused = new Refusal(ToolEnvelope.Failure(ToolEnvelope.NotApplicable, message));

            return false;
        }

        /// <summary>
        /// 確かめる材料の数。別の受け手から読むものは並びの長さを足し合わせ、同じ受け手の上で読む
        /// ものはその値を数とする。1つでも読めなければ null。
        /// </summary>
        private int? Counted(
            McpMethodContext context, ResolvedPrecondition precondition, object receiver)
        {
            int? picked = Picked(context, precondition);
            if (picked == null)
            {
                return null;
            }

            int counted = picked.Value;
            foreach (string rowKey in precondition.Counting)
            {
                object value;
                SdkRelayRefusal refusal;
                if (!_relay.TryInvoke(rowKey, receiver, new object[0], out value, out refusal)
                    || !(value is int))
                {
                    return null;
                }

                counted += (int)value;
            }

            return counted;
        }

        /// <summary>
        /// いま選ばれているものの数。1つでも読めなければ null——読めなかったことと0件は別で、
        /// 前者を後者として扱うと、選び直しても直らない断り方になる。
        /// </summary>
        private int? Picked(McpMethodContext context, ResolvedPrecondition precondition)
        {
            int picked = 0;
            foreach (ToolCall reading in precondition.Reading)
            {
                PmxTarget target;
                IList<Spot> column;
                Refusal unreadable;
                if (!TryTake(context, reading.Receiver, false, null, false, out target, out unreadable)
                    || !TryColumn(
                        context,
                        reading.Access,
                        reading.Receiver,
                        target,
                        Pointed.None,
                        Accepted(reading.Access, null, false),
                        out column,
                        out unreadable))
                {
                    return null;
                }

                object value;
                SdkRelayRefusal refusal;
                if (!_relay.TryInvoke(
                    reading.RowKey, column[0].Item, new object[0], out value, out refusal))
                {
                    return null;
                }

                System.Collections.ICollection values = value as System.Collections.ICollection;
                if (values == null)
                {
                    return null;
                }

                picked += values.Count;
            }

            return picked;
        }

        /// <summary>
        /// そのツールが呼ぶ前に確かめること。持たなければ null。材料の名前はここで呼び出しへ解く。
        /// 解けない名前が在れば、確かめられないまま素通りさせずに組み立てで落とす。
        /// </summary>
        private static ResolvedPrecondition Required(
            IDictionary<string, ToolPrecondition> preconditions,
            IDictionary<string, IList<ToolCall>> calls,
            string tool)
        {
            ToolPrecondition precondition;
            if (!preconditions.TryGetValue(tool, out precondition))
            {
                return null;
            }

            List<ToolCall> reading = new List<ToolCall>();
            foreach (string name in precondition.Reading)
            {
                IList<ToolCall> found;
                if (!calls.TryGetValue(name, out found))
                {
                    throw new InvalidOperationException(
                        "確かめる材料を得るツールが無い: " + name);
                }

                reading.AddRange(found);
            }

            RequireMaterials(precondition, reading, tool);

            return new ResolvedPrecondition(precondition.Kind, reading, precondition.Counting);
        }

        /// <summary>
        /// 種別が要る材料を持っているか。種別ごとに読む先が違うので、数だけでなくどちらを持つかまで
        /// 見る——取り違えたまま通すと、別の意味の数を足し合わせて確かめたことにしてしまう。
        /// </summary>
        private static void RequireMaterials(
            ToolPrecondition precondition, IList<ToolCall> reading, string tool)
        {
            bool met;
            switch (precondition.Kind)
            {
                case PreconditionKind.PickedObjects:
                    met = reading.Count != 0 && precondition.Counting.Count == 0;
                    break;

                case PreconditionKind.SavedEdits:
                    met = reading.Count == 0 && precondition.Counting.Count != 0;
                    break;

                default:
                    met = false;
                    break;
            }

            if (!met)
            {
                throw new InvalidOperationException(
                    "呼ぶ前に確かめる材料がその種別に合わない: " + tool);
            }
        }

        /// <summary>
        /// 項目を集めるツールと要素を出し入れするツールは、呼ぶ前に確かめることを扱えない。持つ形が
        /// 現れたら、扱えないまま素通りさせずに組み立てで落とす。
        /// </summary>
        private static void RequireNoPrecondition(
            IDictionary<string, ToolPrecondition> preconditions, string tool)
        {
            if (preconditions.ContainsKey(tool))
            {
                throw new InvalidOperationException(
                    "呼ぶ前に確かめることを扱えないツールが持っている: " + tool);
            }
        }

        /// <summary>名前を呼び出しへ解いた後の、呼ぶ前に確かめること。</summary>
        private sealed class ResolvedPrecondition
        {
            public ResolvedPrecondition(
                PreconditionKind kind, IList<ToolCall> reading, IList<string> counting)
            {
                Kind = kind;
                Reading = reading;
                Counting = counting;
            }

            /// <summary>確かめることの種別。</summary>
            public PreconditionKind Kind { get; }

            /// <summary>確かめる材料を得る読み取りの呼び出し。</summary>
            public IList<ToolCall> Reading { get; }

            /// <summary>確かめる材料を得る、呼ぶ先と同じ受け手の上の行キー。</summary>
            public IList<string> Counting { get; }
        }

        /// <summary>呼び分けが揃って持つ編集の分類。揃っていなければ組み立てが誤っている。</summary>
        private static EditKind Edit(IList<ToolCall> calls)
        {
            EditKind[] kinds = calls.Select(c => c.Receiver.Edit).Distinct().ToArray();
            if (kinds.Length != 1)
            {
                throw new InvalidOperationException(
                    "呼び分けの編集の分類が揃っていない: " + calls[0].RowKey);
            }

            return kinds[0];
        }

        /// <summary>
        /// Undoの記録まわりの前置きを済ませてからツールを呼ぶ。止めることを頼めない分類が頼んで
        /// いれば断る。止めたまま戻せていないものがあれば、まず戻しにいき、戻らなければ分類ごとの
        /// 決まりで断るか警告を添える。
        /// </summary>
        private McpMethod Guarded(EditKind kind, McpMethod inner)
        {
            return context =>
            {
                bool suppress;
                string code;
                string message;
                if (!TrySuppress(context, out suppress, out code, out message)
                    || !UndoGate.TryAcceptSuppress(
                        kind,
                        context.Params.ContainsKey(PmxSession.HandleName),
                        suppress,
                        out code,
                        out message))
                {
                    return ToolEnvelope.Failure(code, message);
                }

                List<string> notices = new List<string>();
                string warning;
                if (!_recovery.TryRecover(context.Ui)
                    && !UndoGate.TryProceedWithLeftover(kind, out code, out message, out warning))
                {
                    return ToolEnvelope.Failure(code, message);
                }

                if (_recovery.TryTakeNotice())
                {
                    notices.Add(UndoGate.RecoveredWarning);
                }

                object answered = inner(context);
                if (_recovery.HasLeftover)
                {
                    notices.Add(UndoGate.LeftoverWarning);
                }

                return notices.Count == 0 ? answered : Noted(answered, notices);
            };
        }

        /// <summary>
        /// 包みへ知らせを載せる。成功した呼び出しには警告として足し、失敗した呼び出しには誤りの
        /// 説明へ足す——誤りだけを読む側にも、Undoの記録が止まったままであることが要るためである。
        /// </summary>
        private static object Noted(object answered, IList<string> notices)
        {
            IDictionary<string, object> envelope = answered as IDictionary<string, object>;
            if (envelope == null)
            {
                return answered;
            }

            Dictionary<string, object> written =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> member in envelope)
            {
                written.Add(member.Key, member.Value);
            }

            object failed;
            IDictionary<string, object> error =
                written.TryGetValue(ToolEnvelope.ErrorName, out failed)
                    ? failed as IDictionary<string, object>
                    : null;
            if (error != null)
            {
                Dictionary<string, object> explained =
                    new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> member in error)
                {
                    explained.Add(member.Key, member.Value);
                }

                object said;
                explained[ToolEnvelope.MessageName] =
                    (explained.TryGetValue(ToolEnvelope.MessageName, out said) ? (string)said : null)
                        + string.Concat(notices.Select(n => " " + n));
                written[ToolEnvelope.ErrorName] = explained;

                return written;
            }

            List<string> all = new List<string>();
            object listed;
            if (written.TryGetValue(ToolEnvelope.WarningsName, out listed) && listed is object[])
            {
                all.AddRange(((object[])listed).Select(w => (string)w));
            }

            all.AddRange(notices.Where(n => !all.Contains(n, StringComparer.Ordinal)));
            written[ToolEnvelope.WarningsName] = all.Cast<object>().ToArray();

            return written;
        }

        /// <summary>Undoの記録を止めることを頼んでいるか。真偽でなければ偽で、断る内容を渡す。</summary>
        private static bool TrySuppress(
            McpMethodContext context, out bool suppress, out string code, out string message)
        {
            code = null;
            message = null;
            suppress = false;
            object value;
            if (!context.Params.TryGetValue(SuppressName, out value))
            {
                return true;
            }

            if (!(value is bool))
            {
                code = ToolEnvelope.InvalidArgument;
                message = SuppressName + " は真偽でなければならない。";

                return false;
            }

            suppress = (bool)value;

            return true;
        }

        /// <summary>止めることを頼まれているか。値の検証は前置きで済んでいる。</summary>
        private static bool Suppressed(McpMethodContext context)
        {
            object value;

            return context.Params.TryGetValue(SuppressName, out value)
                && value is bool && (bool)value;
        }

        /// <summary>
        /// SDKのメンバーへ中継する。同じ名前のオーバーロードは1つのツールへ集まるので、まず
        /// 渡された引数の名前でどれを呼ぶかを決める。
        /// </summary>
        private object Invoke(
            McpMethodContext context, IList<ToolCall> calls, ResolvedPrecondition precondition)
        {
            ToolCall call;
            string code;
            string message;
            if (!TryOverload(context, calls, out call, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            return Invoke(context, call, precondition);
        }

        /// <summary>
        /// 渡された引数に合う呼び分け。1つしか持たないツールはそれを採る。2つ以上を持つツールは、
        /// 受け取る引数がすべて渡されていて、渡された値をその引数として受け取れるもののうち、
        /// 引数の多いものを採る——引数を足した呼び分けは、その一部だけを取る呼び分けを兼ねる。
        /// </summary>
        private static bool TryOverload(
            McpMethodContext context,
            IList<ToolCall> calls,
            out ToolCall chosen,
            out string code,
            out string message)
        {
            chosen = calls[0];
            code = null;
            message = null;
            if (calls.Count == 1)
            {
                return true;
            }

            IList<IDictionary<string, object>> sets = Sets(context, calls[0]);
            foreach (ToolCall call in calls.OrderByDescending(c => Given(c).Count))
            {
                if (sets.All(s => Given(call).All(a => Receivable(s, a))))
                {
                    chosen = call;

                    return true;
                }
            }

            code = ToolEnvelope.InvalidArgument;
            message = "渡された引数に合う呼び分けが無い。";

            return false;
        }

        /// <summary>
        /// 引数を探す組。対象の組へ及ぶ呼び出しは組を別に渡すので、渡された組ぜんぶを見る。
        /// </summary>
        private static IList<IDictionary<string, object>> Sets(
            McpMethodContext context, ToolCall call)
        {
            if (!Many(call.Access, call.Receiver))
            {
                return new[] { context.Params };
            }

            object given;
            if (context.Params.TryGetValue(ArgsName, out given))
            {
                return new[] { given as IDictionary<string, object> };
            }

            if (!context.Params.TryGetValue(ArgsListName, out given))
            {
                return new IDictionary<string, object>[0];
            }

            object[] items = given as object[];

            return items == null
                ? new IDictionary<string, object>[0]
                : items.Select(i => i as IDictionary<string, object>).ToList();
        }

        /// <summary>その引数として受け取れる値が組に入っているか。</summary>
        private static bool Receivable(IDictionary<string, object> set, ToolArgument argument)
        {
            object given;
            if (set == null || !set.TryGetValue(argument.Name, out given))
            {
                return false;
            }

            object taken;
            string code;
            string message;
            if (argument.Referenced != null)
            {
                return TryReference(argument, given, out taken, out code, out message);
            }

            if (argument.Held != null)
            {
                long id;

                return TryInteger(given, out id);
            }

            return ValueInput.TryFromJson(argument.Type, given, out taken, out code, out message);
        }

        /// <summary>SDKのメンバーを呼ぶ。対象の組へ及ぶ呼び出しは、その全件へ及ぶ。</summary>
        private object Invoke(
            McpMethodContext context, ToolCall call, ResolvedPrecondition precondition)
        {
            if (Many(call.Access, call.Receiver))
            {
                if (precondition != null)
                {
                    throw new InvalidOperationException(
                        "呼ぶ前に確かめることを扱えない呼び出しが持っている: " + call.RowKey);
                }

                return InvokeEach(context, call);
            }

            return InvokeOnce(context, call, precondition);
        }

        /// <summary>SDKのメンバーを、受け手そのものへ1度呼ぶ。</summary>
        private object InvokeOnce(
            McpMethodContext context, ToolCall call, ResolvedPrecondition precondition)
        {
            string code;
            string message;
            bool confirm;
            long? handle;
            List<string> known = Given(call).Select(a => a.Name).ToList();
            if (call.Danger != DangerKind.None)
            {
                known.Add(ConfirmName);
            }

            if (!TryOnlyKnown(context, Known(known, Accepts(call)), out code, out message)
                || !TryConfirm(context, out confirm, out code, out message)
                || !TryPmxHandle(context, Accepts(call), out handle, out code, out message)
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
            object called = null;
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            string unavailable;
            if (!Run(context, () =>
            {
                PmxTarget target;
                IList<Spot> column;
                if (!TryTake(context, call.Receiver, Targets(call), handle, false, out target, out refused)
                    || !TryColumn(
                        context,
                        call.Access,
                        call.Receiver,
                        target,
                        Pointed.None,
                        Accepted(call.Access, null, false),
                        out column,
                        out refused)
                    || !TryBound(call, target, new[] { arguments }, out refused)
                    || !TryMet(context, precondition, column[0].Item, out refused))
                {
                    return;
                }

                called = column[0].Item;
                stage = Changing(call.Receiver, target);
                object value;
                SdkRelayRefusal refusal;
                if (!_relay.TryInvoke(
                    call.RowKey, column[0].Item, arguments, out value, out refusal))
                {
                    refused = Refusal.Of(call.RowKey, refusal);

                    return;
                }

                if (!TryProjected(call, value, out result, out refused))
                {
                    return;
                }

                stage = Reflecting(call.Receiver, target, stage);
                refused = Commit(context, call.Receiver, target);
            }, out failure, out unavailable))
            {
                return Unavailable(unavailable);
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

            if (call.Issues != null)
            {
                return Issued(context, call, result, called);
            }

            if (call.Projected != null)
            {
                return ToolEnvelope.Success(result);
            }

            return call.Result == null
                ? ToolEnvelope.Success(null)
                : Written(call.Result, result);
        }

        /// <summary>
        /// 生成物を台帳へ預け、そのハンドルを返す。生成物はエディタの状態の外で生きるので、
        /// 解放するか、リストへ加えて消費するまで台帳が保つ。
        /// </summary>
        private object Issued(
            McpMethodContext context,
            ToolCall call,
            object result,
            object receiver,
            int at = 0,
            int? held = null)
        {
            IList<object> made;
            IDictionary<string, object> refused;
            if (!TryMade(call, result, out made, out refused))
            {
                return refused;
            }

            List<object> issued = new List<object>(made.Count);
            foreach (object one in made)
            {
                issued.Add(One(context, call, one, receiver, at, held));
            }

            return call.RespondsMany
                ? ToolEnvelope.Success(issued.ToArray())
                : ToolEnvelope.Success(issued[0]);
        }

        private static bool TryMade(
            ToolCall call,
            object result,
            out IList<object> made,
            out IDictionary<string, object> refused)
        {
            made = null;
            refused = null;
            if (result == null)
            {
                refused = ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable, "生成物を返さなかった: " + call.RowKey);

                return false;
            }

            if (!call.ReturnsMany)
            {
                made = new[] { result };

                return true;
            }

            System.Collections.IEnumerable row = result as System.Collections.IEnumerable;
            if (row == null)
            {
                refused = ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable, "生成物を並べて返さなかった: " + call.RowKey);

                return false;
            }

            List<object> each = new List<object>();
            foreach (object one in row)
            {
                if (one == null)
                {
                    refused = ToolEnvelope.Failure(
                        ToolEnvelope.NotApplicable, "生成物の並びに空きがある: " + call.RowKey);

                    return false;
                }

                each.Add(one);
            }

            made = each;

            return true;
        }

        /// <summary>生成物1件を台帳へ預け、そのハンドルを返す。</summary>
        private int One(
            McpMethodContext context,
            ToolCall call,
            object made,
            object receiver,
            int at,
            int? held)
        {
            Action detach = () => { };
            int id = context.Handles.Issue(
                call.Issues.FullName,
                made,
                () =>
                {
                    detach();
                    Releasing(context, call, made, receiver)();
                },
                Involved(context, call, Passed(context, call, at), held));
            detach = Listening(context, call.Issues.FullName, made, id);

            return id;
        }

        /// <summary>
        /// 預けた実体がリスナなら、その公開イベントへ受け手を掛ける。掛けた受け手は、ハンドルが
        /// 失効するときに外す。受け手はエディタのUIスレッドで走るので、溜め場へ入れるところで
        /// 起きた誤りは記録だけにして、エディタの側へ返さない。
        /// </summary>
        private Action Listening(
            McpMethodContext context, string typeName, object issued, int id)
        {
            EventAttach attach;
            if (!_events.Attachments.TryGetValue(typeName, out attach))
            {
                return () => { };
            }

            return attach(issued, (type, args) =>
            {
                context.Events.Enqueue(type, id, Read(type, args));
            });
        }

        /// <summary>
        /// イベント固有の値を組へ直す。写せなかった値は落として組を空にする——受け手はエディタの
        /// UIスレッドで走るので、ここで投げるとエディタの側へ抜ける。
        /// </summary>
        private object Read(string type, object args)
        {
            PayloadReader read;
            if (!_events.Payloads.TryGetValue(type, out read))
            {
                return null;
            }

            try
            {
                return read(args);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// その生成に関与したハンドル。ハンドルで受け取った引数と、ハンドルで指した受け手がこれに
        /// 当たり、生成物はそれらより先に解放される——生成物は関与した実体を持ち続けるので、先に
        /// 手放されると使えなくなる。
        /// </summary>
        private static IEnumerable<int> Involved(
            McpMethodContext context, ToolCall call, IDictionary<string, object> given, int? held)
        {
            List<int> involved = new List<int>();
            if (held.HasValue)
            {
                involved.Add(held.Value);
            }

            foreach (ToolArgument argument in call.Arguments.Where(a => a.Held != null))
            {
                object json;
                long id;
                if (given.TryGetValue(argument.Name, out json) && TryInteger(json, out id))
                {
                    involved.Add((int)id);
                }
            }

            return involved;
        }

        /// <summary>
        /// その位置の対象を指したハンドル。ハンドル以外で指した対象では null——位置で指した対象は
        /// リストが持つので、生成物より先に手放されることがない。
        /// </summary>
        private static int? Held(Pointed pointed, int at)
        {
            if (pointed == null || !pointed.ByHandle || pointed.Elements == null)
            {
                return null;
            }

            IList<long> handles = pointed.Elements.Handles;

            return handles != null && at < handles.Count ? (int?)handles[at] : null;
        }

        /// <summary>その呼び出しの引数を渡された組。器の中へ入れて渡す呼び出しはその器を見る。</summary>
        private static IDictionary<string, object> Passed(
            McpMethodContext context, ToolCall call, int at)
        {
            if (!Many(call.Access, call.Receiver))
            {
                return context.Params;
            }

            object single;
            if (context.Params.TryGetValue(ArgsName, out single))
            {
                return single as IDictionary<string, object>
                    ?? new Dictionary<string, object>(StringComparer.Ordinal);
            }

            object many;
            object[] items = context.Params.TryGetValue(ArgsListName, out many)
                ? many as object[]
                : null;

            return items != null && at < items.Length
                ? items[at] as IDictionary<string, object>
                    ?? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 預けた生成物を手放す手順。手放す行を持たない生成物では何もしない。手順を持つ生成物は
        /// SDKを呼ぶので、ほかの中継と同じくUIスレッドで行う。
        /// </summary>
        private Action Releasing(
            McpMethodContext context, ToolCall call, object result, object receiver)
        {
            if (call.Releases == null)
            {
                return () => { };
            }

            IUiInvoker invoker = context.Ui;
            object target = call.ReleasesIssued ? receiver : result;
            object[] arguments = call.ReleasesIssued ? new[] { result } : new object[0];

            return () =>
            {
                UiInvocation ran = invoker.TryInvokeOnUi(() =>
                {
                    object ignored;
                    SdkRelayRefusal refusal;
                    if (!_relay.TryInvoke(
                        call.Releases, target, arguments, out ignored, out refusal))
                    {
                        throw new InvalidOperationException(
                            "手放す呼び出しを断られた: " + call.Releases);
                    }
                });
                if (!ran.DidRun)
                {
                    throw new InvalidOperationException(
                        "手放す呼び出しをUIスレッドで行えなかった: " + call.Releases);
                }
            };
        }

        /// <summary>
        /// SDKのメンバーを、解いた対象の全件へ呼ぶ。引数は全件へ同じ組を配るか、対象ごとの組の
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
            if (Given(call).Count != 0)
            {
                known.Add(ArgsName);
                known.Add(ArgsListName);
            }

            if (call.Danger != DangerKind.None)
            {
                known.Add(ConfirmName);
            }

            known.AddRange(Pointing(call.Access, true, call.Receiver));
            if (!TryOnlyKnown(context, Known(known, Accepts(call)), out code, out message)
                || !TryConfirm(context, out confirm, out code, out message)
                || !TryPmxHandle(context, Accepts(call), out handle, out code, out message)
                || !TryPassDanger(call, handle, confirm, out code, out message)
                || !TryPointed(
                    context, call.Access, true, handle, out pointed, out code, out message,
                    call.Receiver))
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
            List<object> receivers = new List<object>();
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            string unavailable;
            if (!Run(context, () =>
            {
                PmxTarget target;
                IList<Spot> column;
                if (!TryTake(
                        context,
                        call.Receiver,
                        Targets(call),
                        handle,
                        pointed.Held && !Takes(call),
                        out target,
                        out refused)
                    || !TryColumn(
                        context,
                        call.Access,
                        call.Receiver,
                        target,
                        pointed,
                        Accepted(call.Access, null, false),
                        out column,
                        out refused)
                    || !TryOfItemType(column, call.Access, null, out refused)
                    || !TryBound(call, target, passing, out refused))
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

                    object projected;
                    if (!TryProjected(call, value, out projected, out refused))
                    {
                        return;
                    }

                    results.Add(projected);
                    receivers.Add(column[at].Item);
                }

                invoked = column.Count;
                stage = Reflecting(call.Receiver, target, stage);
                refused = Commit(context, call.Receiver, target);
            }, out failure, out unavailable))
            {
                return Unavailable(unavailable);
            }

            if (failure != null)
            {
                return Failed(failure, stage);
            }

            if (refused != null)
            {
                return refused.Envelope;
            }

            if (call.Issues != null)
            {
                return Handed(context, call, results, receivers, pointed, invoked);
            }

            if (call.Result == null && call.Outputs.Count == 0)
            {
                return ToolEnvelope.Success(SetResponse.Invoked(invoked));
            }

            List<object> written = new List<object>(results.Count);
            List<string> warnings = new List<string>();
            foreach (object one in results)
            {
                if (call.Projected != null)
                {
                    written.Add(one);

                    continue;
                }

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

        /// <summary>
        /// 対象ごとの生成物を台帳へ預け、そのハンドルを対象の並びで返す。対象の組へ及ぶ呼び出しが
        /// 生成物を返すときの応答で、1件ずつの発行は受け手ごとに行う。
        /// </summary>
        private object Handed(
            McpMethodContext context,
            ToolCall call,
            IList<object> results,
            IList<object> receivers,
            Pointed pointed,
            int invoked)
        {
            foreach (object one in results)
            {
                IList<object> made;
                IDictionary<string, object> refused;
                if (!TryMade(call, one, out made, out refused))
                {
                    return refused;
                }
            }

            List<object> handed = new List<object>(results.Count);
            for (int at = 0; at < results.Count; at++)
            {
                IDictionary<string, object> envelope = (IDictionary<string, object>)Issued(
                    context, call, results[at], receivers[at], at, Held(pointed, at));
                object value;
                if (!envelope.TryGetValue(ToolEnvelope.ValueName, out value))
                {
                    return envelope;
                }

                handed.Add(value);
            }

            return ToolEnvelope.Success(SetResponse.PerTarget(handed, invoked));
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
            if (Given(call).Count == 0)
            {
                if (hasSingle || hasMany)
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = "引数を取らない呼び出しは " + ArgsName + " も " + ArgsListName
                        + " も取らない。";

                    return false;
                }

                passing = new[] { new object[call.Arguments.Count] };

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
                if (!TryPass(context, call, single, out taken, out code, out message))
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
                if (!TryPass(context, call, item, out taken, out code, out message))
                {
                    return false;
                }

                built.Add(taken);
            }

            passing = built;
            spread = false;

            return true;
        }

        /// <summary>
        /// ホストが自分で入れる引数と、位置で受け取る引数を解く。位置が指すのは相手にするPMXの
        /// リストの中の要素で、関連が無いことは null で表す。
        /// </summary>
        private bool TryBound(
            ToolCall call, PmxTarget target, IList<object[]> passing, out Refusal refused)
        {
            refused = null;
            for (int at = 0; at < call.Arguments.Count; at++)
            {
                ToolArgument argument = call.Arguments[at];
                if (argument.Injected)
                {
                    object given = Injected(argument, target);
                    foreach (object[] one in passing)
                    {
                        one[at] = given;
                    }

                    continue;
                }

                if (argument.Referenced == null)
                {
                    continue;
                }

                IList<object> listed;
                if (!TryListed(argument.Referenced, target, out listed, out refused))
                {
                    return false;
                }

                foreach (object[] one in passing)
                {
                    if (one[at] == null)
                    {
                        continue;
                    }

                    int index = (int)one[at];
                    if (index < 0 || index >= listed.Count)
                    {
                        refused = new Refusal(ToolEnvelope.Failure(
                            ToolEnvelope.IndexOutOfRange,
                            argument.Name + " の位置が範囲の外にある: " + index
                                + "(リストの件数は " + listed.Count + ")"));

                        return false;
                    }

                    one[at] = listed[index];
                }
            }

            return true;
        }

        /// <summary>その道が指すリストの要素。位置で受け取る引数は、この列の中の位置で指す。</summary>
        private bool TryListed(
            ToolAccess access, PmxTarget target, out IList<object> listed, out Refusal refused)
        {
            listed = null;
            IList<object> owners;
            if (!TryOwners(access, target, out owners, out refused))
            {
                return false;
            }

            List<object> reached = new List<object>();
            foreach (object owner in owners)
            {
                if (!TryStep(Step(access), owner, reached, out refused))
                {
                    return false;
                }
            }

            listed = reached;

            return true;
        }

        /// <summary>呼び出す側が渡す引数。ホストが自分で入れる引数はこれに入らない。</summary>
        private static IList<ToolArgument> Given(ToolCall call)
        {
            return call.Arguments.Where(a => !a.Injected).ToList();
        }

        /// <summary>
        /// 位置で受け取る引数の指定。関連が無いことは null で表し、実体を解くのは相手にするPMXが
        /// 決まってからなので、ここでは位置のまま持つ。
        /// </summary>
        private static bool TryReference(
            ToolArgument argument,
            object given,
            out object position,
            out string code,
            out string message)
        {
            position = null;
            code = null;
            message = null;
            if (given == null)
            {
                return true;
            }

            int index;
            if (TryIndex(given, out index))
            {
                position = index;

                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            message = argument.Name + " は位置の整数でなければならない。";

            return false;
        }

        /// <summary>
        /// ハンドルで受け取る引数を、そのハンドルが指す実体へ直す。指していなければ断る。
        /// </summary>
        private static bool TryHeldArgument(
            McpMethodContext context,
            ToolArgument argument,
            object json,
            out object value,
            out string code,
            out string message)
        {
            value = null;
            code = null;
            message = null;
            long id;
            if (!TryInteger(json, out id))
            {
                code = ToolEnvelope.InvalidArgument;
                message = argument.Name + " はハンドルの番号でなければならない。";

                return false;
            }

            value = Held(context, new[] { argument.Held }, id);
            if (value != null)
            {
                return true;
            }

            code = ToolEnvelope.InvalidHandle;
            message = argument.Name + " が指すハンドルは、この呼び出しが相手にする実体を指していない。";

            return false;
        }

        /// <summary>
        /// 組で受け取る引数を、SDKへ渡す実体へ組み立てる。並びを取る引数は要素ごとに組み立てる。
        /// </summary>
        private static bool TryBuilt(
            ToolArgument argument,
            object json,
            out object value,
            out string code,
            out string message)
        {
            value = null;
            code = null;
            message = null;
            if (!argument.Type.IsArray)
            {
                return TryOne(argument, argument.Type, json, out value, out code, out message);
            }

            object[] items = json as object[];
            if (items == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = argument.Name + " は組の配列でなければならない。";

                return false;
            }

            Type element = argument.Type.GetElementType();
            Array built = Array.CreateInstance(element, items.Length);
            for (int at = 0; at < items.Length; at++)
            {
                object one;
                if (!TryOne(argument, element, items[at], out one, out code, out message))
                {
                    return false;
                }

                built.SetValue(one, at);
            }

            value = built;

            return true;
        }

        /// <summary>組1つを実体へ組み立てる。知らない項目と足りない項目は断る。</summary>
        private static bool TryOne(
            ToolArgument argument,
            Type type,
            object json,
            out object value,
            out string code,
            out string message)
        {
            value = null;
            code = null;
            message = null;
            IDictionary<string, object> members = json as IDictionary<string, object>;
            if (members == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = argument.Name + " は項目の組でなければならない。";

                return false;
            }

            string unknown = members.Keys
                .Where(n => !argument.Built.Members.Any(
                    m => string.Equals(m.Name, n, StringComparison.Ordinal)))
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = argument.Name + " が知らない項目を持っている: " + unknown;

                return false;
            }

            object made = argument.Built.Create();
            foreach (ToolValueMember member in argument.Built.Members)
            {
                object given;
                if (!members.TryGetValue(member.Name, out given))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = argument.Name + " の項目が足りない: " + member.Name;

                    return false;
                }

                object typed;
                if (!ValueInput.TryFromJson(member.Type, given, out typed, out code, out message))
                {
                    code = code ?? ToolEnvelope.NotApplicable;
                    message = message
                        ?? (member.Name + " は値として受け取れない型を取る。");

                    return false;
                }

                member.Write(made, typed);
            }

            value = made;

            return true;
        }

        /// <summary>引数の組1つを、シグネチャの並びの値へ直す。</summary>
        private static bool TryPass(
            McpMethodContext context,
            ToolCall call,
            object given,
            out object[] passed,
            out string code,
            out string message)
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
                .Where(n => !Given(call).Any(
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
                if (argument.Injected)
                {
                    continue;
                }

                object value;
                if (!members.TryGetValue(argument.Name, out value))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = "引数が足りない: " + argument.Name;

                    return false;
                }

                if (argument.Referenced != null)
                {
                    if (!TryReference(argument, value, out taken[at], out code, out message))
                    {
                        return false;
                    }

                    continue;
                }

                if (argument.Held != null)
                {
                    if (!TryHeldArgument(
                        context, argument, value, out taken[at], out code, out message))
                    {
                        return false;
                    }

                    continue;
                }

                if (argument.Built != null)
                {
                    if (!TryBuilt(argument, value, out taken[at], out code, out message))
                    {
                        return false;
                    }

                    continue;
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

            known.AddRange(Pointing(tool.Access, true, tool.Receiver));
            bool divided = Divided(tool);
            if (!TryOnlyKnown(context, Known(known, tool.Receiver.Kind == ToolReceiverKind.Pmx), out code, out message)
                || !TryPmxHandle(context, tool.Receiver.Kind == ToolReceiverKind.Pmx, out handle, out code, out message)
                || !TryCount(context, OffsetName, 0, 0, out offset, out code, out message)
                || !TryCount(context, LimitName, int.MaxValue, 1, out limit, out code, out message)
                || !TryFields(context, out requested, out code, out message)
                || !TryPointed(
                    context, tool.Access, true, handle, out pointed, out code, out message,
                    tool.Receiver)
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
            string unavailable;
            if (!Run(context, () =>
            {
                PmxTarget target;
                IList<Spot> column;
                if (!TryTake(context, tool.Receiver, tool.Receiver.Kind == ToolReceiverKind.Pmx, handle, pointed.Held, out target, out refused)
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
            }, out failure, out unavailable))
            {
                return Unavailable(unavailable);
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
                Known(tool.Sets[0].Fields.Select(f => f.Name).ToList(), tool.Receiver.Kind == ToolReceiverKind.Pmx),
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
            string unavailable;
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
            }, out failure, out unavailable))
            {
                return Unavailable(unavailable);
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
            if (Many(tool.Access, tool.Receiver))
            {
                known.Add(ValuesName);
            }

            if (Divided(tool))
            {
                known.Add(ItemTypeName);
            }

            known.AddRange(Pointing(tool.Access, true, tool.Receiver));
            if (!TryOnlyKnown(context, Known(known, tool.Receiver.Kind == ToolReceiverKind.Pmx), out code, out message)
                || !TryPmxHandle(context, tool.Receiver.Kind == ToolReceiverKind.Pmx, out handle, out code, out message)
                || !TryItemType(context, tool, out itemType, out code, out message)
                || !TryPointed(
                    context, tool.Access, true, handle, out pointed, out code, out message,
                    tool.Receiver))
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
            string unavailable;
            if (!Run(context, () =>
            {
                PmxTarget target;
                IList<Spot> column;
                if (!TryTake(context, tool.Receiver, tool.Receiver.Kind == ToolReceiverKind.Pmx, handle, pointed.Held, out target, out refused)
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
                refused = Commit(context, tool.Receiver, target);
            }, out failure, out unavailable))
            {
                return Unavailable(unavailable);
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
            return tool.Access.Parents.Count == 0 && tool.Access.Owner == null
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
            string unavailable;
            if (!Run(context, () =>
            {
                PmxTarget target;
                SdkList list;
                if (!TryTake(context, tool.Receiver, tool.Receiver.Kind == ToolReceiverKind.Pmx, null, false, out target, out refused)
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
                refused = Commit(context, tool.Receiver, target);
            }, out failure, out unavailable))
            {
                return Unavailable(unavailable);
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
            string unavailable;
            if (!Run(context, () =>
            {
                PmxTarget target;
                SdkList list;
                IList<object> owners = null;
                if (!TryTake(context, tool.Receiver, tool.Receiver.Kind == ToolReceiverKind.Pmx, null, byHandle, out target, out refused)
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
                refused = Commit(context, tool.Receiver, target);
            }, out failure, out unavailable))
            {
                return Unavailable(unavailable);
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
            List<string> known = new List<string>(Pointing(tool.Access, false, tool.Receiver));
            if (!TryOnlyKnown(context, Known(known, tool.Receiver.Kind == ToolReceiverKind.Pmx), out code, out message)
                || !TryPmxHandle(context, tool.Receiver.Kind == ToolReceiverKind.Pmx, out handle, out code, out message)
                || !TryPointed(
                    context, tool.Access, false, handle, out pointed, out code, out message,
                    tool.Receiver))
            {
                return ToolEnvelope.Failure(code, message);
            }

            int removed = 0;
            Refusal refused = null;
            EditStage stage = EditStage.BeforeCommit;
            Exception failure;
            string unavailable;
            if (!Run(context, () =>
            {
                PmxTarget target;
                SdkList list;
                IList<Spot> column;
                if (!TryTake(context, tool.Receiver, tool.Receiver.Kind == ToolReceiverKind.Pmx, handle, pointed.Held, out target, out refused)
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
                refused = Commit(context, tool.Receiver, target);
            }, out failure, out unavailable))
            {
                return Unavailable(unavailable);
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

        /// <summary>受け手をハンドルから得る呼び出しか。対象はハンドルでだけ指せる。</summary>
        private static bool Handled(ToolReceiver receiver)
        {
            return receiver.Kind == ToolReceiverKind.Handle;
        }

        /// <summary>その呼び出しが対象の組へ及ぶか。1つだけを相手にする呼び出しは当たらない。</summary>
        private static bool Many(ToolAccess access, ToolReceiver receiver)
        {
            return access.Kind == ToolAccessKind.Element || Handled(receiver);
        }

        /// <summary>その道が受け取る対象の指し方の名前。要素を相手にしない道は何も受け取らない。</summary>
        private static IEnumerable<string> Pointing(
            ToolAccess access, bool handles, ToolReceiver receiver = null)
        {
            if (receiver != null && Handled(receiver))
            {
                yield return TargetNames.Element.Handles;

                yield break;
            }

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

            if (access.Parents.Count != 0)
            {
                yield return TargetNames.Parent.Indices;
                yield return TargetNames.Parent.Range;
                yield return TargetNames.Parent.All;
            }

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
            out string message,
            ToolReceiver receiver = null)
        {
            pointed = null;
            code = null;
            message = null;
            if (receiver != null && Handled(receiver))
            {
                return TryHandled(context, out pointed, out code, out message);
            }

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

            if ((access.Parents.Count != 0 || access.Owner != null)
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

            if (access.Parents.Count == 0 && access.Owner != null && parents.Handles == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = TargetNames.Parent.Handles + " で親を指す。";

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

        /// <summary>
        /// ハンドルから受け手を得る呼び出しの指し方。ハンドル以外の指し方は、指す先のリストが
        /// 無いので取らない。
        /// </summary>
        private static bool TryHandled(
            McpMethodContext context, out Pointed pointed, out string code, out string message)
        {
            pointed = null;
            TargetRequest held;
            if (!TryTargets(context, TargetNames.Element, true, out held, out code, out message))
            {
                return false;
            }

            if (held.Handles == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = TargetNames.Element.Handles + " で対象を指す。";

                return false;
            }

            pointed = new Pointed(held, null, true);

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
            if (Handled(receiver))
            {
                return TryHeld(
                    id => Held(context, receiver.Accepts, id), pointed, out column, out refused);
            }

            if (access.Kind == ToolAccessKind.Whole)
            {
                column = new[] { new Spot(null, 0, -1, -1, Receiver(receiver, target)) };

                return true;
            }

            if (access.Kind == ToolAccessKind.Element && pointed.ByHandle)
            {
                return TryHeld(
                    id => Held(context, accepted, id), pointed, out column, out refused);
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
            Func<long, object> resolve,
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
                id => resolve(id) != null,
                out resolved,
                out code,
                out message,
                TargetNames.Element))
            {
                refused = new Refusal(ToolEnvelope.Failure(code, message));

                return false;
            }

            column = resolved.Handles
                .Select((id, at) => new Spot(null, at, -1, -1, resolve(id)))
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
            bool hasMany = Many(tool.Access, tool.Receiver)
                && context.Params.TryGetValue(ValuesName, out many);
            if (hasSingle == hasMany)
            {
                code = ToolEnvelope.InvalidArgument;
                message = Many(tool.Access, tool.Receiver)
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
                if (tool.Access.Parents.Count == 0)
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = ParentIndexName
                        + " はこのツールでは指定できない。親を位置で指す道が無いためである。";

                    return false;
                }

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
        /// そのハンドルが指す、受け手にできる実体。指していなければ null。実体の側で判ずるので、
        /// 派生した型のハンドルは、その基底の型を相手にする呼び出しでも通る。
        /// </summary>
        private static object Held(McpMethodContext context, Func<object, bool> accepts, long id)
        {
            object held;

            return id >= int.MinValue && id <= int.MaxValue
                && context.Handles.TryGet((int)id, out held)
                && accepts(held)
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
            bool targets,
            long? handle,
            bool held,
            out PmxTarget target,
            out Refusal refused)
        {
            target = null;
            refused = null;
            if (!targets)
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
            if (Session(receiver).TryTake(handle, context.Handles, out target, out code, out message))
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
            return receiver.Edit == EditKind.DuplicateEdit && target != null && target.Current;
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
        private Refusal Commit(
            McpMethodContext context, ToolReceiver receiver, PmxTarget target)
        {
            if (!Reflects(receiver, target))
            {
                return null;
            }

            string code;
            string message;

            return Session(receiver).TryCommit(target, Suppressed(context), out code, out message)
                ? null
                : new Refusal(ToolEnvelope.Failure(code, message));
        }

        /// <summary>その受け手の複製編集の流れ。橋渡しから得る受け手は、そちらの流れを使う。</summary>
        private PmxSession Session(ToolReceiver receiver)
        {
            return receiver.Bridged ? _bridged : _pmx;
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

        /// <summary>ホストが入れる引数の値。取る型で、どこから得るかが決まる。</summary>
        private object Injected(ToolArgument argument, PmxTarget target)
        {
            if (argument.Connector)
            {
                return _connection.Use();
            }

            if (argument.Resident != null)
            {
                SdkReceiver found;
                if (!_receivers.TryGetValue(argument.Resident, out found))
                {
                    throw new InvalidOperationException(
                        "受け手を得る道が無い: " + argument.Resident);
                }

                return found(_connection);
            }

            return target == null ? null : target.Pmx;
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
        /// <paramref name="failure"/> にその例外を、行えなかったときは
        /// <paramref name="unavailable"/> にその理由を持たせる。
        /// </summary>
        private static bool Run(
            McpMethodContext context, Action action, out Exception failure, out string unavailable)
        {
            Exception caught = null;
            UiInvocation invocation = context.Ui.TryInvokeOnUi(() =>
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
            unavailable = invocation.Unavailable;

            return invocation.DidRun;
        }

        /// <summary>そのツールが受け取る名前。PMXから受け手を得るものは切り替えも受け取る。</summary>
        private static IList<string> Known(IList<string> names, bool targets)
        {
            List<string> known = new List<string>(names) { SuppressName };
            if (targets)
            {
                known.Add(PmxSession.HandleName);
            }

            return known;
        }

        /// <summary>
        /// どのPMXを見るかの指定を受け取る呼び出しか。受け手をハンドルで指す呼び出しは受け取らない
        /// ——ハンドルが指す実体はどのPMXにも属さないので、指定しても相手は変わらない。
        /// </summary>
        private static bool Accepts(ToolCall call)
        {
            return Targets(call) && !Handled(call.Receiver);
        }

        /// <summary>
        /// 相手にするPMXそのものを引数として要る呼び出しか。受け手をハンドルで指していても、
        /// 引数へ入れるPMXはいま相手にしているものを採る。
        /// </summary>
        private static bool Takes(ToolCall call)
        {
            return call.Arguments.Any(
                a => (a.Injected && !a.Connector && a.Resident == null) || a.Referenced != null);
        }

        /// <summary>
        /// 相手にするPMXが要る呼び出しか。PMXから受け手を得る道と、PMXを引数へ入れる行が当たる。
        /// </summary>
        private static bool Targets(ToolCall call)
        {
            return call.Receiver.Kind == ToolReceiverKind.Pmx
                || call.Arguments.Any(a => a.Injected || a.Referenced != null);
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
            bool targets,
            out long? handle,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            handle = null;
            object value;
            if (!targets || !context.Params.TryGetValue(PmxSession.HandleName, out value))
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
            code = null;
            message = null;
            if (argument.Injected)
            {
                return true;
            }

            object json;
            if (!context.Params.TryGetValue(argument.Name, out json))
            {
                code = ToolEnvelope.InvalidArgument;
                message = argument.Name + " を渡していない。";

                return false;
            }

            if (argument.Referenced != null)
            {
                return TryReference(argument, json, out value, out code, out message);
            }

            if (argument.Held != null)
            {
                return TryHeldArgument(context, argument, json, out value, out code, out message);
            }

            if (argument.Built != null)
            {
                return TryBuilt(argument, json, out value, out code, out message);
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

        /// <summary>
        /// 返す値が値として写せない型なら、その中の項目を読んで組へ直す。写せる型ではそのまま返す。
        /// SDKを呼ぶので、呼び出しと同じUIスレッドの中で行う。読めない項目と写せない項目は、
        /// ほかの中継と同じ断り方で返す。
        /// </summary>
        private bool TryProjected(
            ToolCall call, object value, out object projected, out Refusal refused)
        {
            refused = null;
            if (call.Projected == null)
            {
                projected = value;

                return true;
            }

            if (!call.ReturnsMany)
            {
                IDictionary<string, object> members;
                if (!TryCarried(call.Projected, value, out members, out refused))
                {
                    projected = null;

                    return false;
                }

                projected = members;

                return true;
            }

            projected = null;
            if (value == null)
            {
                return true;
            }

            System.Collections.IEnumerable each = value as System.Collections.IEnumerable;
            if (each == null)
            {
                refused = new Refusal(ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable, "値を並べて返さなかった: " + call.RowKey));

                return false;
            }

            List<object> written = new List<object>();
            foreach (object one in each)
            {
                IDictionary<string, object> carried;
                if (!TryCarried(call.Projected, one, out carried, out refused))
                {
                    return false;
                }

                written.Add(carried);
            }

            projected = written.ToArray();

            return true;
        }

        /// <summary>運搬用の型の項目を読んで組へ直す。中がまた運搬用の型なら、その中も同じに扱う。</summary>
        private bool TryCarried(
            IList<ToolField> fields,
            object value,
            out IDictionary<string, object> carried,
            out Refusal refused)
        {
            carried = null;
            refused = null;
            if (value == null)
            {
                return true;
            }

            Dictionary<string, object> members =
                new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (ToolField field in fields)
            {
                object read;
                SdkRelayRefusal refusal;
                if (!_relay.TryInvoke(field.RowKey, value, new object[0], out read, out refusal))
                {
                    refused = Refusal.Of(field.RowKey, refusal);

                    return false;
                }

                if (field.Members != null)
                {
                    IDictionary<string, object> inner;
                    if (!TryCarried(field.Members, read, out inner, out refused))
                    {
                        return false;
                    }

                    members.Add(field.Name, inner);

                    continue;
                }

                object json;
                IList<string> ignored;
                string code;
                string message;
                if (!ValueShape.TryToJson(
                    field.Type,
                    read,
                    ImageTransfer.DefaultMaxLongSide,
                    out json,
                    out ignored,
                    out code,
                    out message))
                {
                    refused = new Refusal(Unwritable(field.Type, code, message));

                    return false;
                }

                members.Add(field.Name, json);
            }

            carried = members;

            return true;
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

        /// <summary>
        /// 呼び出しをUIスレッドで行えなかったことを返す。<paramref name="unavailable"/> に事情が
        /// 在るときはそれを説明とする——何が起きているかを知っているのは委譲した側である。
        /// </summary>
        private static IDictionary<string, object> Unavailable(string unavailable = null)
        {
            return ToolEnvelope.Failure(
                ToolEnvelope.NotApplicable, unavailable ?? "いまは要求を受け付けていない。");
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
