using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// ツールの要求を、結び付きの表が指す行へ振り分ける。呼ぶ先も受け手も引数の型もビルド時に
    /// 決まっているので、ここには名前で型やメンバーを引く経路が無い。
    /// </summary>
    public sealed class ToolDispatch
    {
        /// <summary>危険操作の確認を受け取る共通引数の名前。</summary>
        public const string ConfirmName = "confirm";

        /// <summary>返す項目を選ぶ共通引数の名前。</summary>
        public const string FieldsName = "fields";

        private readonly SdkRelayTable _relay;

        private readonly IDictionary<string, SdkReceiver> _receivers;

        private readonly ResidentConnection _connection;

        private ToolDispatch(
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            ResidentConnection connection)
        {
            _relay = relay;
            _receivers = receivers;
            _connection = connection;
        }

        /// <summary>結び付きの表が持つツールをすべて登録する。</summary>
        public static void AddTo(
            McpMethodTable methods,
            SdkRelayTable relay,
            IDictionary<string, SdkReceiver> receivers,
            ResidentConnection connection,
            IDictionary<string, ToolCall> calls,
            IDictionary<string, ToolFields> aggregations)
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

            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (calls == null)
            {
                throw new ArgumentNullException(nameof(calls));
            }

            if (aggregations == null)
            {
                throw new ArgumentNullException(nameof(aggregations));
            }

            ToolDispatch dispatch = new ToolDispatch(relay, receivers, connection);
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
        }

        /// <summary>SDKのメンバーを1度呼ぶ。</summary>
        private object Invoke(McpMethodContext context, ToolCall call)
        {
            string code;
            string message;
            bool confirm;
            List<string> known = call.Arguments.Select(a => a.Name).ToList();
            if (call.Danger != DangerKind.None)
            {
                known.Add(ConfirmName);
            }

            if (!TryOnlyKnown(context, known, out code, out message)
                || !TryConfirm(context, out confirm, out code, out message)
                || !ConfirmGate.TryPass(call.Danger, confirm, out code, out message))
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
            SdkRelayRefusal refusal = SdkRelayRefusal.None;
            bool relayed = false;
            Exception failure;
            if (!Run(context, () =>
            {
                relayed = _relay.TryInvoke(
                    call.RowKey, Receiver(call.ReceiverType), arguments, out result, out refusal);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure);
            }

            if (!relayed)
            {
                return Refused(call.RowKey, refusal);
            }

            if (call.Result == null)
            {
                return ToolEnvelope.Success(null);
            }

            return Written(call.Result, result);
        }

        /// <summary>その型の項目をまとめて読む。</summary>
        private object Read(McpMethodContext context, ToolFields tool)
        {
            string code;
            string message;
            IList<string> requested;
            IList<string> selected;
            if (!TryOnlyKnown(context, new[] { FieldsName }, out code, out message)
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
            string refusedKey = null;
            SdkRelayRefusal refusal = SdkRelayRefusal.None;
            Exception failure;
            if (!Run(context, () =>
            {
                for (int at = 0; at < reading.Count && refusedKey == null; at++)
                {
                    object value;
                    if (_relay.TryInvoke(
                        reading[at].RowKey,
                        Receiver(reading[at].ReceiverType),
                        new object[0],
                        out value,
                        out refusal))
                    {
                        values[at] = value;
                        continue;
                    }

                    refusedKey = reading[at].RowKey;
                }
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure);
            }

            if (refusedKey != null)
            {
                return Refused(refusedKey, refusal);
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

            return ToolEnvelope.Success(item, warnings);
        }

        /// <summary>
        /// その型の項目を1つ書く。1度に書けるのを1つに限るのは、直に書き込む先を戻す手立てが
        /// SDKの側に無く、2つ以上を続けて書くと途中まで書けた状態が残りうるためである。
        /// </summary>
        private object Write(McpMethodContext context, ToolFields tool)
        {
            string code;
            string message;
            if (!TryOnlyKnown(context, tool.Fields.Select(f => f.Name).ToList(), out code, out message))
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

            SdkRelayRefusal refusal = SdkRelayRefusal.None;
            bool relayed = false;
            Exception failure;
            if (!Run(context, () =>
            {
                object ignored;
                relayed = _relay.TryInvoke(
                    field.RowKey,
                    Receiver(field.ReceiverType),
                    new[] { value },
                    out ignored,
                    out refusal);
            }, out failure))
            {
                return Unavailable();
            }

            if (failure != null)
            {
                return Failed(failure);
            }

            if (!relayed)
            {
                return Refused(field.RowKey, refusal);
            }

            return ToolEnvelope.Success(SetResponse.Updated(1));
        }

        /// <summary>
        /// 受け手。接続の根から辿って得る道はビルド時に決めてあり、静的なメンバーは相手を取らない。
        /// </summary>
        private object Receiver(string receiverType)
        {
            if (receiverType == null)
            {
                return null;
            }

            SdkReceiver receiver;
            if (!_receivers.TryGetValue(receiverType, out receiver))
            {
                throw new InvalidOperationException("受け手を得る道が無い: " + receiverType);
            }

            return receiver(_connection);
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

        /// <summary>知らない名前の引数を渡す要求を断る。名前の検証は適用より先に済ませる。</summary>
        private static bool TryOnlyKnown(
            McpMethodContext context,
            IList<string> known,
            out string code,
            out string message)
        {
            foreach (string name in context.Params.Keys
                .Where(n => !known.Contains(n, StringComparer.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal))
            {
                code = ToolEnvelope.InvalidArgument;
                message = "知らない引数を渡している: " + name;

                return false;
            }

            code = null;
            message = null;

            return true;
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

        private static IDictionary<string, object> Failed(Exception failure)
        {
            return ToolEnvelope.Failure(ToolEnvelope.OperationFailed, failure.Message);
        }

        private static IDictionary<string, object> Refused(string rowKey, SdkRelayRefusal refusal)
        {
            return ToolEnvelope.Failure(ToolEnvelope.NotApplicable, Describe(rowKey, refusal));
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
