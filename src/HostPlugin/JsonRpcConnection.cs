using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp
{
    /// <summary>メソッドへ渡された引数が契約に合わないことを表す。応答では不正な引数として扱う。</summary>
    public sealed class InvalidParamsException : Exception
    {
        public InvalidParamsException(string message)
            : base(message)
        {
        }
    }

    public sealed class McpMethodContext
    {
        public McpMethodContext(
            IDictionary<string, object> parameters,
            IUiInvoker ui,
            int budgetChars,
            HandleLedger handles,
            EventQueue events,
            ScreenTargets screen = null)
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
            Screen = screen ?? ScreenTargets.None;
        }

        /// <summary>要求の引数。省略されていたときは空。</summary>
        public IDictionary<string, object> Params { get; }

        /// <summary>UIスレッドへの委譲。PEPlugin API の呼び出しはすべてこれを通す。</summary>
        public IUiInvoker Ui { get; }

        public int BudgetChars { get; }

        public HandleLedger Handles { get; }

        public EventQueue Events { get; }

        public ScreenTargets Screen { get; }

        /// <summary>
        /// この呼び出しが変えた中身を、エディタの画面へ映せなかったか。映す段が置き、呼び出しを
        /// 包む側が知らせへ移す。
        /// </summary>
        public bool NotShown { get; set; }

        /// <summary>
        /// この呼び出しの応答へ添える警告。UIスレッドの中の段が置き、呼び出しを包む側が応答へ移す。
        /// </summary>
        public IList<string> Notices { get; } = new List<string>();
    }

    /// <summary>ホストが公開する処理。戻り値がそのまま応答の result になる。</summary>
    public delegate object McpMethod(McpMethodContext context);

    public sealed class McpMethodTable
    {
        private readonly Dictionary<string, McpMethod> _methods = new Dictionary<string, McpMethod>(StringComparer.Ordinal);

        /// <summary>
        /// 処理を登録する。同じ名前を二度登録することと、接続自身が受け持つ基盤メソッドの名前を
        /// 登録することは拒む。
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

        /// <summary>登録されている処理の名前。綴りの順に並ぶ。</summary>
        public IList<string> Names
        {
            get
            {
                List<string> names = new List<string>(_methods.Keys);
                names.Sort(StringComparer.Ordinal);

                return names;
            }
        }

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
        private const string HandshakeMethodName = "handshake";
        private const string PingMethodName = "ping";
        private const string EndSessionMethodName = "end_session";
        private const string SdkStatusMethodName = "sdk_status";

        public static readonly ReadOnlyCollection<string> BaseMethodNames =
            Array.AsReadOnly(new[]
            {
                HandshakeMethodName, PingMethodName, EndSessionMethodName, SdkStatusMethodName,
            });

        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(120);

        /// <summary>要求の処理を直列化する錠を待つ間に、相手の切断を見に行く間隔の既定。</summary>
        public static readonly TimeSpan DefaultGatePollInterval = TimeSpan.FromMilliseconds(50);

        private static readonly Stopwatch SinceLoaded = Stopwatch.StartNew();

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
        private readonly SdkRelayTable _relays;
        private readonly string _sdkVersion;

        private readonly UndoRecovery _recovery;

        private readonly ScreenTargets _screen;

        private readonly Func<TimeSpan> _clock;

        private readonly TimeSpan _gatePollInterval;

        /// <summary>
        /// 要求の処理を直列化する錠。SDKを呼んでいる区間が重ならないことと、要求1件が発行した
        /// ハンドルがその要求のものに定まることを、ここで保証する。
        /// </summary>
        private readonly object _requestGate = new object();

        public JsonRpcConnection(HostLog log, McpMethodTable methods, string hostVersion, int budgetChars)
            : this(log, methods, hostVersion, budgetChars, DefaultRequestTimeout, MessageChannel.DefaultMaxMessageBytes)
        {
        }

        /// <summary>
        /// 行キーからSDKへの中継と、読み込まれているSDKのバージョン、いまの稼働世代を返すもの、
        /// Undoの記録を戻す窓口も与えて生成する。窓口を渡さない接続は、戻しにいく機会を持たない。
        /// </summary>
        public JsonRpcConnection(
            HostLog log,
            McpMethodTable methods,
            string hostVersion,
            int budgetChars,
            SdkRelayTable relays,
            string sdkVersion,
            Func<IUiInvoker> currentUi,
            UndoRecovery recovery = null,
            ScreenTargets screen = null)
            : this(
                log,
                methods,
                hostVersion,
                budgetChars,
                DefaultRequestTimeout,
                MessageChannel.DefaultMaxMessageBytes,
                PipeClientProcess.TryOpen,
                relays,
                sdkVersion,
                recovery,
                screen,
                currentUi: currentUi ?? throw new ArgumentNullException(nameof(currentUi)))
        {
        }

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

        public JsonRpcConnection(
            HostLog log,
            McpMethodTable methods,
            string hostVersion,
            int budgetChars,
            TimeSpan requestTimeout,
            int maxMessageBytes,
            ClientProcessOpener openClient)
            : this(
                log,
                methods,
                hostVersion,
                budgetChars,
                requestTimeout,
                maxMessageBytes,
                openClient,
                new SdkRelayTable(
                    string.Empty, string.Empty, new Dictionary<string, SdkCall>(), new string[0]),
                string.Empty)
        {
        }

        /// <summary>
        /// すべてを指定して生成する。<paramref name="clock"/> は単調に進む時刻で、省くとホストの読み込みから数えた
        /// 経過時間を用いる。<paramref name="gatePollInterval"/> は要求の処理を直列化する錠を待つ間に相手の切断を
        /// 見に行く間隔で、省くと <see cref="DefaultGatePollInterval"/> を用いる。<paramref name="currentUi"/> は
        /// セッションを終わらせるときにハンドルを手放す稼働世代を返すもので、省くと常に断る窓口を用いる。
        /// </summary>
        public JsonRpcConnection(
            HostLog log,
            McpMethodTable methods,
            string hostVersion,
            int budgetChars,
            TimeSpan requestTimeout,
            int maxMessageBytes,
            ClientProcessOpener openClient,
            SdkRelayTable relays,
            string sdkVersion,
            UndoRecovery recovery = null,
            ScreenTargets screen = null,
            Func<TimeSpan> clock = null,
            TimeSpan? gatePollInterval = null,
            Func<IUiInvoker> currentUi = null)
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

            if (relays == null)
            {
                throw new ArgumentNullException(nameof(relays));
            }

            if (sdkVersion == null)
            {
                throw new ArgumentNullException(nameof(sdkVersion));
            }

            _log = log;
            _methods = methods;
            _hostVersion = hostVersion;
            _budgetChars = budgetChars;
            _requestTimeout = requestTimeout;
            _maxMessageBytes = maxMessageBytes;
            _openClient = openClient;
            _relays = relays;
            _sdkVersion = sdkVersion;
            _recovery = recovery;
            _screen = screen ?? ScreenTargets.None;
            _clock = clock ?? (() => SinceLoaded.Elapsed);
            _gatePollInterval = gatePollInterval ?? DefaultGatePollInterval;
            _sessions = new SessionStore(
                log,
                _handleIds,
                _eventSequence,
                _requestGate,
                currentUi ?? (() => DeclinedUiInvoker.Instance));
        }

        public SessionStore Sessions
        {
            get { return _sessions; }
        }

        public SdkRelayTable Relays
        {
            get { return _relays; }
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

            ErrorResponseCounter errors = new ErrorResponseCounter();

            ClientProcess client;
            bool opened = _openClient(stream, out client);

            ClientHandover handover = new ClientHandover();

            try
            {
                HandleRequests(channel, errors, ui, opened ? client : null, handover);
            }
            finally
            {
                try
                {
                    if (opened && !handover.Taken)
                    {
                        client.Dispose();
                    }
                }
                finally
                {
                    try
                    {
                        Recover(ui);
                    }
                    finally
                    {
                        errors.WriteSummary(_log);
                    }
                }
            }
        }

        /// <summary>
        /// 止めたままのUndoの記録を戻しにいく。窓口を持たない接続では何もしない。戻せたことの
        /// 知らせは次の応答に載る。
        /// </summary>
        private void Recover(IUiInvoker ui)
        {
            if (_recovery != null)
            {
                _recovery.TryRecover(ui);
            }
        }

        private sealed class ClientHandover
        {
            public bool Taken { get; set; }
        }

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

                TimeSpan received = _clock();
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
                        Recover(ui);
                    }

                    continue;
                }

                bool isEndSession =
                    EndSessionMethodName.Equals(request.Method, StringComparison.Ordinal);
                bool isSdkStatus =
                    SdkStatusMethodName.Equals(request.Method, StringComparison.Ordinal);

                McpMethod method = null;
                bool isPing = PingMethodName.Equals(request.Method, StringComparison.Ordinal);
                if (!isPing && !isEndSession && !isSdkStatus
                    && !_methods.TryGet(request.Method, out method))
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

                GateEntry entry = EnterGate(channel, received);
                if (entry == GateEntry.PeerGone)
                {
                    _log.Write("錠を待つ間に相手が切断した: " + request.Method);
                    return;
                }

                if (entry == GateEntry.TimedOut)
                {
                    _log.Write("処理タイムアウト: " + request.Method);
                    Respond(channel, errors, request.Id, JsonRpcErrorCodes.RequestTimeout,
                        "処理が上限の時間を超えた。");
                    continue;
                }

                try
                {
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
                        else if (isSdkStatus)
                        {
                            result = BuildSdkStatusResult();
                        }
                        else if (!TryInvoke(
                            channel, errors, request, method, parameters, scope, received, out result))
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
                        DiscardHandles(scope, issuedBefore);
                        throw;
                    }
                }
                finally
                {
                    Monitor.Exit(_requestGate);
                }
            }
        }

        private enum GateEntry
        {
            Entered,

            /// <summary>錠を取る前に、要求に許す時間を過ぎた。錠は取っていない。</summary>
            TimedOut,

            /// <summary>錠を取る前に、応答を書き出す先の相手が切断した。錠は取っていない。</summary>
            PeerGone,
        }

        /// <summary>
        /// 要求の処理を直列化する錠を取る。要求に許す時間を過ぎるか、相手の切断が分かった時点で、
        /// 錠を取らずに戻る。錠を取れた後でも、相手の切断が分かっているか時間が残っていなければ、錠を返して
        /// その結末とする。
        /// </summary>
        private GateEntry EnterGate(MessageChannel channel, TimeSpan received)
        {
            while (true)
            {
                TimeSpan remaining = _requestTimeout - (_clock() - received);
                if (remaining <= TimeSpan.Zero)
                {
                    return GateEntry.TimedOut;
                }

                if (Monitor.TryEnter(_requestGate, remaining < _gatePollInterval ? remaining : _gatePollInterval))
                {
                    if (channel.IsPeerGone)
                    {
                        Monitor.Exit(_requestGate);

                        return GateEntry.PeerGone;
                    }

                    if (_clock() - received < _requestTimeout)
                    {
                        return GateEntry.Entered;
                    }

                    Monitor.Exit(_requestGate);

                    return GateEntry.TimedOut;
                }

                channel.ReadAhead();
                if (channel.IsPeerGone)
                {
                    return GateEntry.PeerGone;
                }
            }
        }

        /// <summary>ハンドシェイクの検査の結末。引数の不備と、断る結末とで切断の要否が分かれる。</summary>
        private enum HandshakeOutcome
        {
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
            if (!parameters.TryGetValue("protocol", out protocol) || !ValueInput.IsNumber(protocol))
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.InvalidParams,
                    "handshake には数値の protocol が要る。");
                return HandshakeOutcome.InvalidParams;
            }

            if (!MatchesProtocol(protocol))
            {
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.ProtocolMismatch,
                    "プロトコル番号が合わない。このホストは "
                    + HostProtocol.Number.ToString(CultureInfo.InvariantCulture) + " を用いる。");
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

            // 終わったプロセスでも、そのプロセスオブジェクトへのハンドルが残っている間は開ける。
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

        private static bool MatchesProtocol(object value)
        {
            if (value is int)
            {
                return (int)value == HostProtocol.Number;
            }

            if (value is long)
            {
                return (long)value == HostProtocol.Number;
            }

            if (value is decimal)
            {
                return (decimal)value == HostProtocol.Number;
            }

            return value is double && (double)value == HostProtocol.Number;
        }

        private static bool IsSerializeFailure(Exception exception)
        {
            return exception is ArgumentException
                || exception is InvalidOperationException
                || exception is NotSupportedException;
        }

        /// <summary>答える名前に基盤メソッドは入らない。</summary>
        private IDictionary<string, object> BuildSdkStatusResult()
        {
            return new Dictionary<string, object>
            {
                { "runningSdkVersion", _sdkVersion },
                { "generatedSdkVersion", _relays.GeneratedSdkVersion },
                { "unresolvedRows", _relays.Unresolved },
                { "disabledRows", _relays.Disabled },
                { "refusedTools", _relays.RefusedTools },
                { "toolNames", _methods.Names },
            };
        }

        private IDictionary<string, object> BuildHandshakeResult(Session session)
        {
            return new Dictionary<string, object>
            {
                { "protocol", HostProtocol.Number },
                { "hostVersion", _hostVersion },
                { "budgetChars", _budgetChars },
                { "toolMapDigest", _relays.ToolMapDigest },
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
            TimeSpan received,
            out object result)
        {
            result = null;

            TimeSpan remaining = _requestTimeout - (_clock() - received);
            if (remaining <= TimeSpan.Zero)
            {
                _log.Write("処理タイムアウト: " + request.Method);
                Respond(channel, errors, request.Id, JsonRpcErrorCodes.RequestTimeout, "処理が上限の時間を超えた。");

                return false;
            }

            McpMethodContext context = new McpMethodContext(
                parameters, scope.Ui, _budgetChars, scope.Handles, scope.Events, _screen);
            Task<object> running = Task.Factory.StartNew(
                () => method(context), TaskCreationOptions.LongRunning);

            try
            {
                // 処理が例外で終わったときは、待ち合わせ自体がその例外を運んでくる。
                if (!running.Wait(remaining))
                {
                    try
                    {
                        _log.Write("処理タイムアウト: " + request.Method);
                        Respond(channel, errors, request.Id, JsonRpcErrorCodes.RequestTimeout,
                            "処理が上限の時間を超えた。");
                    }
                    finally
                    {
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
            HandleReleaseResult discarded = scope.Handles.ReleaseIssuedAfter(issuedBefore, scope.Ui);
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
                line = JsonRpcCodec.SerializeError(null, code, message);
            }

            if (MessageChannel.MeasureBytes(line) > channel.MaxMessageBytes)
            {
                line = JsonRpcCodec.SerializeError(null, code, string.Empty);
            }

            channel.Write(line);
            errors.Note(_log, code);
        }

        /// <summary>
        /// 接続1本が結び付いた相手。要求ごとの文脈はここから作る。ハンドルとイベントはセッションの
        /// 持ち物で、切断を越えて生きる。
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
