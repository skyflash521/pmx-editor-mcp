using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace PmxEditorMcp
{
    /// <summary>メソッドへ渡された引数が契約に合わないことを表す。応答では不正な引数として扱う。</summary>
    public sealed class InvalidParamsException : Exception
    {
        /// <summary>要求元へ返す説明を添えて生成する。</summary>
        public InvalidParamsException(string message)
            : base(message)
        {
        }
    }

    /// <summary>メソッドを呼ぶときに渡す一式。</summary>
    public sealed class McpMethodContext
    {
        /// <summary>引数・UIスレッドへの委譲・応答サイズ予算を与えて生成する。</summary>
        public McpMethodContext(IDictionary<string, object> parameters, IUiInvoker ui, int budgetChars)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (ui == null)
            {
                throw new ArgumentNullException(nameof(ui));
            }

            Params = parameters;
            Ui = ui;
            BudgetChars = budgetChars;
        }

        /// <summary>要求の引数。省略されていたときは空。</summary>
        public IDictionary<string, object> Params { get; }

        /// <summary>UIスレッドへの委譲。PEPlugin API の呼び出しはすべてこれを通す。</summary>
        public IUiInvoker Ui { get; }

        /// <summary>ホストが読んだ応答サイズ予算の文字数。結果の量を抑える判定に用いる。</summary>
        public int BudgetChars { get; }
    }

    /// <summary>ホストが公開する処理。戻り値がそのまま応答の result になる。</summary>
    public delegate object McpMethod(McpMethodContext context);

    /// <summary>メソッド名から処理を引く表。</summary>
    public sealed class McpMethodTable
    {
        private readonly Dictionary<string, McpMethod> _methods = new Dictionary<string, McpMethod>(StringComparer.Ordinal);

        /// <summary>
        /// 処理を登録する。同じ名前を二度登録することと、接続自身が受け持つ基盤メソッドの名前
        /// (handshake・ping)を登録することは、いずれも黙って無視されるのを避けるため拒む。
        /// </summary>
        public void Add(string name, McpMethod method)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            if (JsonRpcConnection.BaseMethodNames.Contains(name))
            {
                throw new ArgumentException("基盤メソッドの名前は登録できない: " + name, nameof(name));
            }

            if (_methods.ContainsKey(name))
            {
                throw new ArgumentException("同じ名前の処理が登録済み: " + name, nameof(name));
            }

            _methods.Add(name, method);
        }

        /// <summary>名前に対応する処理を引く。</summary>
        public bool TryGet(string name, out McpMethod method)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _methods.TryGetValue(name, out method);
        }
    }

    /// <summary>
    /// 接続1本ぶんの処理。要求を1件ずつ読み、契約の順に判定して応答を1件返す。切断が要る
    /// エラーでは応答を書いてから戻り、待受ループが切断として扱う。ハンドシェイクの成否は
    /// <see cref="Handle"/> の呼び出しごとに独立していて、同じインスタンスで次の接続を
    /// 処理するときは持ち越さない。
    /// </summary>
    public sealed class JsonRpcConnection
    {
        /// <summary>ハンドシェイクで一致していなければならないプロトコル番号。</summary>
        public const int Protocol = 1;

        private const string HandshakeMethodName = "handshake";
        private const string PingMethodName = "ping";

        /// <summary>接続自身が受け持つ基盤メソッドの名前。</summary>
        public static readonly ReadOnlyCollection<string> BaseMethodNames =
            Array.AsReadOnly(new[] { HandshakeMethodName, PingMethodName });

        /// <summary>要求1件の処理に許す時間の既定。</summary>
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(120);

        private readonly HostLog _log;
        private readonly McpMethodTable _methods;
        private readonly string _hostVersion;
        private readonly int _budgetChars;
        private readonly TimeSpan _requestTimeout;
        private readonly int _maxMessageBytes;

        /// <summary>ログ・メソッド表・ハンドシェイク応答に載せる値を与えて生成する。</summary>
        public JsonRpcConnection(HostLog log, McpMethodTable methods, string hostVersion, int budgetChars)
            : this(log, methods, hostVersion, budgetChars, DefaultRequestTimeout, MessageChannel.DefaultMaxMessageBytes)
        {
        }

        /// <summary>
        /// 要求処理の時間の上限とメッセージの上限も指定して生成する。どちらもテストから
        /// 差し替えるための引数で、通常は既定を用いる。
        /// </summary>
        public JsonRpcConnection(
            HostLog log,
            McpMethodTable methods,
            string hostVersion,
            int budgetChars,
            TimeSpan requestTimeout,
            int maxMessageBytes)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (hostVersion == null)
            {
                throw new ArgumentNullException(nameof(hostVersion));
            }

            _log = log;
            _methods = methods;
            _hostVersion = hostVersion;
            _budgetChars = budgetChars;
            _requestTimeout = requestTimeout;
            _maxMessageBytes = maxMessageBytes;
        }

        /// <summary>
        /// 接続を処理する。相手が切断するか、切断が要るエラーを返すまで戻らない。
        /// </summary>
        public void Handle(Stream stream, IUiInvoker ui)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (ui == null)
            {
                throw new ArgumentNullException(nameof(ui));
            }

            MessageChannel channel = new MessageChannel(stream, _maxMessageBytes);

            // 接続ごとに独立させるため、この呼び出しのローカルに持つ。
            bool handshaked = false;

            // エラー応答の数はコードごとに数え、記録は最初の1回と、接続が切れたときの合計に限る。
            // 同じ記録の反復でローテーションが有用な履歴を押し流すのを避けるため。数え上げは接続ごとに
            // 独立させたいので、稼働世代やインスタンスでなくこの呼び出しのローカルに持つ。
            ErrorResponseCounter errors = new ErrorResponseCounter();

            try
            {
                HandleRequests(channel, errors, ui, handshaked);
            }
            finally
            {
                errors.WriteSummary(_log);
            }
        }

        private void HandleRequests(
            MessageChannel channel, ErrorResponseCounter errors, IUiInvoker ui, bool handshaked)
        {
            while (true)
            {
                string line;
                switch (channel.Read(out line))
                {
                    case MessageReadOutcome.EndOfStream:
                        return;

                    case MessageReadOutcome.TooLarge:
                        Respond(channel, errors, null, JsonRpcErrorCodes.RequestTooLarge,
                            "要求のメッセージが上限のバイト数を超えている。");
                        return;

                    case MessageReadOutcome.InvalidEncoding:
                        Respond(channel, errors, null, JsonRpcErrorCodes.ParseError,
                            "要求の本文がUTF-8として解釈できない。");
                        return;
                }

                JsonRpcParseResult parsed = JsonRpcCodec.ParseRequest(line);
                if (!parsed.IsValid)
                {
                    Respond(channel, errors, parsed.Id, parsed.ErrorCode, parsed.ErrorMessage);
                    if (parsed.ErrorCode == JsonRpcErrorCodes.ParseError
                        || parsed.ErrorCode == JsonRpcErrorCodes.RequestTooLarge)
                    {
                        return;
                    }

                    continue;
                }

                JsonRpcRequest request = parsed.Request;
                bool isHandshake = HandshakeMethodName.Equals(request.Method, StringComparison.Ordinal);

                if (!handshaked && !isHandshake)
                {
                    Respond(channel, errors, request.Id, JsonRpcErrorCodes.HandshakeRequired,
                        "接続後の最初の要求は handshake でなければならない。");
                    return;
                }

                if (isHandshake)
                {
                    HandshakeOutcome outcome = CheckHandshake(channel, errors, request);
                    if (outcome == HandshakeOutcome.Mismatched)
                    {
                        return;
                    }

                    if (outcome == HandshakeOutcome.Accepted)
                    {
                        handshaked = true;
                        WriteResult(channel, errors, request.Id, BuildHandshakeResult());
                    }

                    continue;
                }

                McpMethod method = null;
                bool isPing = PingMethodName.Equals(request.Method, StringComparison.Ordinal);
                if (!isPing && !_methods.TryGet(request.Method, out method))
                {
                    Respond(channel, errors, request.Id, JsonRpcErrorCodes.MethodNotFound,
                        "method に対応する処理が無い。");
                    continue;
                }

                IDictionary<string, object> parameters;
                if (!request.TryGetParams(out parameters))
                {
                    Respond(channel, errors, request.Id, JsonRpcErrorCodes.InvalidParams,
                        "params はオブジェクトでなければならない。");
                    continue;
                }

                object result;
                if (isPing)
                {
                    result = "pong";
                }
                else if (!TryInvoke(channel, errors, request, method, parameters, ui, out result))
                {
                    continue;
                }

                WriteResult(channel, errors, request.Id, result);
            }
        }

        /// <summary>ハンドシェイクの検査の結末。引数の不備と番号の不一致で切断の要否が分かれる。</summary>
        private enum HandshakeOutcome
        {
            /// <summary>受理した。</summary>
            Accepted,

            /// <summary>引数が契約に合わない。応答は返したが接続は保つ。</summary>
            InvalidParams,

            /// <summary>プロトコル番号が合わない。応答を返して切断する。</summary>
            Mismatched,
        }

        /// <summary>ハンドシェイクの引数を検査し、受理できないときは応答まで済ませる。</summary>
        private HandshakeOutcome CheckHandshake(
            MessageChannel channel, ErrorResponseCounter errors, JsonRpcRequest request)
        {
            IDictionary<string, object> parameters;
            if (!request.TryGetParams(out parameters))
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.InvalidParams,
                    "params はオブジェクトでなければならない。");
                return HandshakeOutcome.InvalidParams;
            }

            object protocol;
            if (!parameters.TryGetValue("protocol", out protocol) || !IsNumber(protocol))
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.InvalidParams,
                    "handshake には数値の protocol が要る。");
                return HandshakeOutcome.InvalidParams;
            }

            if (!MatchesProtocol(protocol))
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.ProtocolMismatch,
                    "プロトコル番号が合わない。このホストは "
                    + Protocol.ToString(CultureInfo.InvariantCulture) + " を用いる。");
                return HandshakeOutcome.Mismatched;
            }

            return HandshakeOutcome.Accepted;
        }

        private static bool IsNumber(object value)
        {
            return value is int || value is long || value is decimal || value is double;
        }

        /// <summary>
        /// プロトコル番号が一致するかを判定する。型ごとにそのまま比べるのは、桁あふれする値
        /// (シリアライザが decimal・double へ実体化した大きな数)で変換が例外になるのを避けるため。
        /// </summary>
        private static bool MatchesProtocol(object value)
        {
            if (value is int)
            {
                return (int)value == Protocol;
            }

            if (value is long)
            {
                return (long)value == Protocol;
            }

            if (value is decimal)
            {
                return (decimal)value == Protocol;
            }

            return value is double && (double)value == Protocol;
        }

        /// <summary>
        /// 応答を組み立てられなかったことを表す例外かどうかを判定する。シリアライザが投げる例外の
        /// 型を1つに賭けず、組み立ての失敗としてありうる型をまとめて内部エラーへ寄せる
        /// (メモリ不足などの続行できない失敗は通す)。
        /// </summary>
        private static bool IsSerializeFailure(Exception exception)
        {
            return exception is ArgumentException
                || exception is InvalidOperationException
                || exception is NotSupportedException;
        }

        private IDictionary<string, object> BuildHandshakeResult()
        {
            return new Dictionary<string, object>
            {
                { "protocol", Protocol },
                { "hostVersion", _hostVersion },
                { "budgetChars", _budgetChars },
            };
        }

        private bool TryInvoke(
            MessageChannel channel,
            ErrorResponseCounter errors,
            JsonRpcRequest request,
            McpMethod method,
            IDictionary<string, object> parameters,
            IUiInvoker ui,
            out object result)
        {
            result = null;

            McpMethodContext context = new McpMethodContext(parameters, ui, _budgetChars);
            Task<object> running = Task.Factory.StartNew(() => method(context), TaskCreationOptions.LongRunning);

            try
            {
                // 処理が例外で終わったときは、待ち合わせ自体がその例外を運んでくる。
                if (!running.Wait(_requestTimeout))
                {
                    try
                    {
                        _log.Write("処理タイムアウト: " + request.Method);
                        Respond(channel, errors, request.Id, JsonRpcErrorCodes.RequestTimeout,
                            "処理が上限の時間を超えた。");
                    }
                    finally
                    {
                        // 開始済みのUI処理は中断できないため、完了するまで次の要求を読み取らない。
                        // 完了後の結果は破棄し、二重に応答しない。応答を書けなかったときも、
                        // 処理を走らせたまま抜けると次の稼働世代の要求と並行してしまうので待つ。
                        try
                        {
                            running.Wait();
                        }
                        catch (AggregateException)
                        {
                        }
                    }

                    return false;
                }
            }
            catch (AggregateException aggregate)
            {
                RespondToFailure(channel, errors, request, aggregate);
                return false;
            }

            result = running.Result;
            return true;
        }

        private void RespondToFailure(
            MessageChannel channel, ErrorResponseCounter errors, JsonRpcRequest request,
            AggregateException aggregate)
        {
            Exception cause = aggregate.InnerException ?? aggregate;

            InvalidParamsException invalidParams = cause as InvalidParamsException;
            if (invalidParams != null)
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.InvalidParams, invalidParams.Message);
                return;
            }

            _log.WriteException("要求の処理で例外が起きた。", cause);
            Respond(channel, errors, request.Id, JsonRpcErrorCodes.InternalError, "要求の処理で例外が起きた。");
        }

        private void WriteResult(
            MessageChannel channel, ErrorResponseCounter errors, object id, object result)
        {
            string line;
            try
            {
                line = JsonRpcCodec.SerializeResult(id, result);
            }
            catch (Exception exception) when (IsSerializeFailure(exception))
            {
                _log.WriteException("応答の組み立てで例外が起きた。", exception);
                Respond(channel, errors, id, JsonRpcErrorCodes.InternalError, "応答を組み立てられなかった。");
                return;
            }

            if (MessageChannel.MeasureBytes(line) > channel.MaxMessageBytes)
            {
                Respond(channel, errors, id, JsonRpcErrorCodes.ResponseTooLarge,
                    "応答のメッセージが上限のバイト数を超えた。");
                return;
            }

            channel.Write(line);
        }

        private void Respond(
            MessageChannel channel, ErrorResponseCounter errors, object id, int code, string message)
        {
            string line = JsonRpcCodec.SerializeError(id, code, message);
            if (MessageChannel.MeasureBytes(line) > channel.MaxMessageBytes)
            {
                // 識別子まで載せると入らないときは、識別子を落としてでもエラーコードを返す。
                line = JsonRpcCodec.SerializeError(null, code, message);
            }

            if (MessageChannel.MeasureBytes(line) > channel.MaxMessageBytes)
            {
                // 説明も入らないときは、エラーコードだけを返す。ここで投げると、接続を保つはずの
                // エラーがすべて切断に化ける。
                line = JsonRpcCodec.SerializeError(null, code, string.Empty);
            }

            channel.Write(line);
            errors.Note(_log, code);
        }

        /// <summary>
        /// 1つの接続で返したエラー応答をコードごとに数える。記録するのはコードだけにする。
        /// 説明は不正な引数の値を指すことがあり、要求の内容を記録しない規則に触れる。
        /// </summary>
        private sealed class ErrorResponseCounter
        {
            private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

            /// <summary>返したエラー応答を数え、そのコードで最初の1回だけ記録する。</summary>
            public void Note(HostLog log, int code)
            {
                int count;
                _counts.TryGetValue(code, out count);
                _counts[code] = count + 1;

                if (count == 0)
                {
                    log.Write("エラー応答: code=" + code.ToString(CultureInfo.InvariantCulture));
                }
            }

            /// <summary>接続が切れたところで、繰り返したエラー応答の合計を記録する。</summary>
            public void WriteSummary(HostLog log)
            {
                foreach (KeyValuePair<int, int> entry in _counts)
                {
                    if (entry.Value > 1)
                    {
                        log.Write("エラー応答の反復: code="
                            + entry.Key.ToString(CultureInfo.InvariantCulture)
                            + " count=" + entry.Value.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
        }
    }
}
