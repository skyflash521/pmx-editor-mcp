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
        /// <summary>
        /// 引数・UIスレッドへの委譲・応答サイズ予算・セッションが保つ台帳とキューを与えて生成する。
        /// </summary>
        public McpMethodContext(
            IDictionary<string, object> parameters,
            IUiInvoker ui,
            int budgetChars,
            HandleLedger handles,
            EventQueue events)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (ui == null)
            {
                throw new ArgumentNullException(nameof(ui));
            }

            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            Params = parameters;
            Ui = ui;
            BudgetChars = budgetChars;
            Handles = handles;
            Events = events;
        }

        /// <summary>要求の引数。省略されていたときは空。</summary>
        public IDictionary<string, object> Params { get; }

        /// <summary>UIスレッドへの委譲。PEPlugin API の呼び出しはすべてこれを通す。</summary>
        public IUiInvoker Ui { get; }

        /// <summary>ホストが読んだ応答サイズ予算の文字数。結果の量を抑える判定に用いる。</summary>
        public int BudgetChars { get; }

        /// <summary>このセッションが保つ長寿命オブジェクトの台帳。</summary>
        public HandleLedger Handles { get; }

        /// <summary>このセッションが溜めている購読中のイベント。</summary>
        public EventQueue Events { get; }
    }

    /// <summary>ホストが公開する処理。戻り値がそのまま応答の result になる。</summary>
    public delegate object McpMethod(McpMethodContext context);

    /// <summary>メソッド名から処理を引く表。</summary>
    public sealed class McpMethodTable
    {
        private readonly Dictionary<string, McpMethod> _methods = new Dictionary<string, McpMethod>(StringComparer.Ordinal);

        /// <summary>
        /// 処理を登録する。同じ名前を二度登録することと、接続自身が受け持つ基盤メソッドの名前を
        /// 登録することは、いずれも黙って無視されるのを避けるため拒む。
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
        private const string EndSessionMethodName = "end_session";

        /// <summary>接続自身が受け持つ基盤メソッドの名前。</summary>
        public static readonly ReadOnlyCollection<string> BaseMethodNames =
            Array.AsReadOnly(new[] { HandshakeMethodName, PingMethodName, EndSessionMethodName });

        /// <summary>要求1件の処理に許す時間の既定。</summary>
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(120);

        private readonly HostLog _log;
        private readonly McpMethodTable _methods;
        private readonly string _hostVersion;
        private readonly int _budgetChars;
        private readonly TimeSpan _requestTimeout;
        private readonly int _maxMessageBytes;
        private readonly HandleIdIssuer _handleIds = new HandleIdIssuer();
        private readonly EventSequenceIssuer _eventSequence = new EventSequenceIssuer();
        private readonly SessionStore _sessions;
        private readonly ClientProcessOpener _openClient;

        /// <summary>
        /// 要求の処理を直列化する錠。複数の接続を同時に受けるので、SDKを呼んでいる区間が重ならない
        /// ことと、要求1件が発行したハンドルがその要求のものに定まることを、ここで保証する
        /// ——SDKのスレッドセーフティは仮定しない。
        /// </summary>
        private readonly object _requestGate = new object();

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
            : this(
                log,
                methods,
                hostVersion,
                budgetChars,
                requestTimeout,
                maxMessageBytes,
                PipeClientProcess.TryOpen)
        {
        }

        /// <summary>
        /// 接続元のプロセスの開き方も指定して生成する。テストから差し替えるための引数で、
        /// 通常は名前付きパイプから開くものを用いる。
        /// </summary>
        public JsonRpcConnection(
            HostLog log,
            McpMethodTable methods,
            string hostVersion,
            int budgetChars,
            TimeSpan requestTimeout,
            int maxMessageBytes,
            ClientProcessOpener openClient)
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

            if (openClient == null)
            {
                throw new ArgumentNullException(nameof(openClient));
            }

            _log = log;
            _methods = methods;
            _hostVersion = hostVersion;
            _budgetChars = budgetChars;
            _requestTimeout = requestTimeout;
            _maxMessageBytes = maxMessageBytes;
            _openClient = openClient;
            _sessions = new SessionStore(log, _handleIds, _eventSequence, _requestGate);
        }

        /// <summary>ホストが持つセッションの集まり。</summary>
        public SessionStore Sessions
        {
            get { return _sessions; }
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

            // エラー応答の数はコードごとに数え、記録は最初の1回と、接続が切れたときの合計に限る。
            // 同じ記録の反復でローテーションが有用な履歴を押し流すのを避けるため。数え上げは接続ごとに
            // 独立させたいので、稼働世代やインスタンスでなくこの呼び出しのローカルに持つ。
            ErrorResponseCounter errors = new ErrorResponseCounter();

            ClientProcess client;
            bool opened = _openClient(stream, out client);

            // 所有権が移ったかどうかは、この呼び出しがどう終わるかと切り離して覚える。戻り値で
            // 表すと、例外で抜けた回に、セッションが所有しているハンドルを閉じてしまう。
            ClientHandover handover = new ClientHandover();

            try
            {
                HandleRequests(channel, errors, ui, opened ? client : null, handover);
            }
            finally
            {
                try
                {
                    // セッションが所有者として保つのは自分が受け取ったハンドルだけなので、
                    // 繋ぎ直しで戻った接続が開いたぶんはここで閉じる。
                    if (opened && !handover.Taken)
                    {
                        client.Dispose();
                    }
                }
                finally
                {
                    errors.WriteSummary(_log);
                }
            }
        }

        /// <summary>接続元のプロセスの所有権がセッションへ移ったかどうか。</summary>
        private sealed class ClientHandover
        {
            public bool Taken { get; set; }
        }

        /// <summary>要求を処理する。</summary>
        private void HandleRequests(
            MessageChannel channel,
            ErrorResponseCounter errors,
            IUiInvoker ui,
            ClientProcess client,
            ClientHandover handover)
        {
            ConnectionScope scope = null;

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

                if (scope == null && !isHandshake)
                {
                    Respond(channel, errors, request.Id, JsonRpcErrorCodes.HandshakeRequired,
                        "接続後の最初の要求は handshake でなければならない。");
                    return;
                }

                // handshake の受け直しも断るので、どの分岐よりも先に見る。ここで通ったあと錠を
                // 待つ間に終わることもあるので、直列区間の中でもう一度見る。
                if (scope != null && scope.Session.IsEnded)
                {
                    Respond(channel, errors, request.Id, JsonRpcErrorCodes.SessionRefused,
                        "このセッションは終わっている。");
                    return;
                }

                if (isHandshake)
                {
                    Session session;
                    HandshakeOutcome outcome = CheckHandshake(
                        channel, errors, request, client, scope, handover, out session);
                    if (outcome == HandshakeOutcome.Refused)
                    {
                        return;
                    }

                    if (outcome == HandshakeOutcome.Accepted)
                    {
                        if (scope == null)
                        {
                            scope = new ConnectionScope(ui, session);
                        }

                        WriteResult(channel, errors, request.Id, BuildHandshakeResult(scope.Session));
                    }

                    continue;
                }

                bool isEndSession =
                    EndSessionMethodName.Equals(request.Method, StringComparison.Ordinal);

                McpMethod method = null;
                bool isPing = PingMethodName.Equals(request.Method, StringComparison.Ordinal);
                if (!isPing && !isEndSession && !_methods.TryGet(request.Method, out method))
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

                // 要求1件の処理をまるごと直列化する。境界の記録・実行・結果の確定か後始末までを
                // 分けずに囲むのは、別の接続が同じ台帳へ発行したハンドルを、こちらの後始末が
                // 巻き込まないようにするため。処理に許す時間を測り始めるのも錠を取ったあとで、
                // 待っている間は数えない。
                lock (_requestGate)
                {
                    // 錠を待つ間に別の接続が終わらせたかもしれない。終わったセッションの台帳と
                    // キューへ触らせないために、ここでもう一度見る。
                    if (scope.Session.IsEnded)
                    {
                        Respond(channel, errors, request.Id, JsonRpcErrorCodes.SessionRefused,
                            "このセッションは終わっている。");
                        return;
                    }

                    if (isEndSession)
                    {
                        _sessions.End(scope.Session.Id);
                        WriteResult(channel, errors, request.Id, scope.Session.Id);

                        return;
                    }

                    int issuedBefore = scope.Handles.LastIssuedId;
                    try
                    {
                        object result;
                        if (isPing)
                        {
                            result = "pong";
                        }
                        else if (!TryInvoke(
                            channel, errors, request, method, parameters, scope, out result))
                        {
                            DiscardHandles(scope, issuedBefore);
                            continue;
                        }

                        if (!WriteResult(channel, errors, request.Id, result))
                        {
                            DiscardHandles(scope, issuedBefore);
                        }
                    }
                    catch
                    {
                        // 書き出せずに抜ける経路でも、呼び出し側へ届かないIDを台帳に残さない。
                        // 台帳は切断を越えて生きるので、残すと誰も解放できないまま居座る。
                        DiscardHandles(scope, issuedBefore);
                        throw;
                    }
                }
            }
        }

        /// <summary>ハンドシェイクの検査の結末。引数の不備と、断る結末とで切断の要否が分かれる。</summary>
        private enum HandshakeOutcome
        {
            /// <summary>受理した。</summary>
            Accepted,

            /// <summary>引数が契約に合わない。応答は返したが接続は保つ。</summary>
            InvalidParams,

            /// <summary>断った。応答を返して切断する。</summary>
            Refused,
        }

        /// <summary>
        /// ハンドシェイクの引数を検査し、接続に結び付けるセッションを決める。受理できないときは
        /// 応答まで済ませる。済んだ接続で受け直したときは、同じセッションのまま同じ応答を返す。
        /// </summary>
        private HandshakeOutcome CheckHandshake(
            MessageChannel channel,
            ErrorResponseCounter errors,
            JsonRpcRequest request,
            ClientProcess client,
            ConnectionScope scope,
            ClientHandover handover,
            out Session session)
        {
            session = null;

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
                return HandshakeOutcome.Refused;
            }

            string presented;
            if (!TryReadSessionId(parameters, out presented))
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.InvalidParams,
                    "handshake の session は文字列でなければならない。");
                return HandshakeOutcome.InvalidParams;
            }

            if (scope != null)
            {
                session = scope.Session;
                return HandshakeOutcome.Accepted;
            }

            // 開けたかどうかだけでは終わったかを判じられない。終わったプロセスでも、そのプロセス
            // オブジェクトへのハンドルが残っている間は開けて、開いた直後から合図済みになる。
            if (client == null || client.HasExited)
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.SessionRefused,
                    "接続元のプロセスを所有者にできない。");
                return HandshakeOutcome.Refused;
            }

            if (!_sessions.TryResolve(presented, client, out session))
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.SessionRefused,
                    "提示された session は別のプロセスのものである。");
                return HandshakeOutcome.Refused;
            }

            handover.Taken = ReferenceEquals(session.Client, client);

            return HandshakeOutcome.Accepted;
        }

        /// <summary>
        /// 提示された識別子を読む。書かれていなければ null を入れて真。文字列でなければ偽。
        /// </summary>
        private static bool TryReadSessionId(IDictionary<string, object> parameters, out string id)
        {
            id = null;

            object value;
            if (!parameters.TryGetValue("session", out value) || value == null)
            {
                return true;
            }

            id = value as string;

            return id != null;
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

        private IDictionary<string, object> BuildHandshakeResult(Session session)
        {
            return new Dictionary<string, object>
            {
                { "protocol", Protocol },
                { "hostVersion", _hostVersion },
                { "budgetChars", _budgetChars },
                { "session", session.Id },
            };
        }

        private bool TryInvoke(
            MessageChannel channel,
            ErrorResponseCounter errors,
            JsonRpcRequest request,
            McpMethod method,
            IDictionary<string, object> parameters,
            ConnectionScope scope,
            out object result)
        {
            result = null;

            McpMethodContext context = new McpMethodContext(
                parameters, scope.Ui, _budgetChars, scope.Handles, scope.Events);
            Task<object> running = Task.Factory.StartNew(
                () => method(context), TaskCreationOptions.LongRunning);

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

        /// <summary>結果を書き出せたときは真。書き出せずに誤りへ寄せたときは偽。</summary>
        private bool WriteResult(
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
                return false;
            }

            if (MessageChannel.MeasureBytes(line) > channel.MaxMessageBytes)
            {
                Respond(channel, errors, id, JsonRpcErrorCodes.ResponseTooLarge,
                    "応答のメッセージが上限のバイト数を超えた。");
                return false;
            }

            channel.Write(line);

            return true;
        }

        /// <summary>結果が呼び出し側へ届かなかったときに、その処理が発行したハンドルを失効させる。</summary>
        private void DiscardHandles(ConnectionScope scope, int issuedBefore)
        {
            HandleReleaseResult discarded = scope.Handles.ReleaseIssuedAfter(issuedBefore);
            if (discarded.Invalidated.Count > 0)
            {
                _log.Write(
                    "届かなかった結果のハンドルの解放: 件数=" + discarded.Invalidated.Count
                        + " 失敗=" + discarded.Failed.Count);
            }
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
        /// 接続1本が結び付いた相手。要求ごとの文脈はここから作る。ハンドルもイベントもセッションの
        /// 持ち物なので、切断を越えて生き、繋ぎ直した接続が同じものを見る。
        /// </summary>
        private sealed class ConnectionScope
        {
            public ConnectionScope(IUiInvoker ui, Session session)
            {
                Ui = ui;
                Session = session;
            }

            public IUiInvoker Ui { get; }

            public Session Session { get; }

            public HandleLedger Handles
            {
                get { return Session.Handles; }
            }

            public EventQueue Events
            {
                get { return Session.Events; }
            }
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
