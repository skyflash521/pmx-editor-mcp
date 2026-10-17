using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    public sealed class HostConnection
    {
        public HostConnection(Stream stream, string pipeName)
        {
            Stream = stream;
            PipeName = pipeName;
        }

        public Stream Stream { get; }

        public string PipeName { get; }
    }

    public sealed class HostCallResult
    {
        public HostCallResult(JsonNode result, string targetNotice)
        {
            Result = result;
            TargetNotice = targetNotice;
        }

        public JsonNode Result { get; }

        /// <summary>この応答を返した相手を伝える一行。応答と一緒に確定させる。</summary>
        public string TargetNotice { get; }
    }

    public interface IHostConnector
    {
        /// <summary>
        /// ホストへの接続を開く。<paramref name="selectedPipeName"/> が null でなければそのパイプ
        /// だけを相手にする。接続先を決められないときと確立に失敗したときは
        /// <see cref="BridgeException"/> を投げる。
        /// </summary>
        Task<HostConnection> ConnectAsync(string selectedPipeName, CancellationToken cancellationToken);
    }

    public sealed class NamedPipeHostConnector : IHostConnector
    {
        public static readonly TimeSpan ConnectWaitLimit = TimeSpan.FromSeconds(5);

        private readonly Func<string, string> _resolvePipeName;
        private readonly Func<string, CancellationToken, Task<Stream>> _openPipe;
        private readonly TimeSpan _waitLimit;

        public NamedPipeHostConnector()
            : this(PipeTargetResolver.ResolveFromRunningHosts, OpenNamedPipeAsync)
        {
        }

        internal NamedPipeHostConnector(
            Func<string, string> resolvePipeName,
            Func<string, CancellationToken, Task<Stream>> openPipe)
            : this(resolvePipeName, openPipe, ConnectWaitLimit)
        {
        }

        internal NamedPipeHostConnector(Func<string, string> resolvePipeName, TimeSpan waitLimit)
            : this(
                resolvePipeName,
                (name, cancellationToken) => OpenNamedPipeAsync(name, cancellationToken, waitLimit),
                waitLimit)
        {
        }

        private NamedPipeHostConnector(
            Func<string, string> resolvePipeName,
            Func<string, CancellationToken, Task<Stream>> openPipe,
            TimeSpan waitLimit)
        {
            _resolvePipeName = resolvePipeName;
            _openPipe = openPipe;
            _waitLimit = waitLimit;
        }

        /// <summary>
        /// 接続のたびに接続先を決め直してから開く。選んだ接続先があれば、それが待ち受けているかを
        /// 確かめるだけで、ほかの待受へは移らない。
        /// </summary>
        public async Task<HostConnection> ConnectAsync(
            string selectedPipeName, CancellationToken cancellationToken)
        {
            string pipeName = _resolvePipeName(selectedPipeName);

            try
            {
                Stream stream = await _openPipe(pipeName, cancellationToken).ConfigureAwait(false);
                return new HostConnection(stream, pipeName);
            }
            catch (TimeoutException)
            {
                throw new BridgeException(
                    BridgeErrorCodes.ConnectFailed,
                    "ホストのパイプ " + pipeName + " へ " + Describe(_waitLimit)
                        + "以内に接続できなかった。接続先のエディタが終了している、またはエディタで"
                        + "ホストが停止している可能性がある。");
            }
            catch (IOException error)
            {
                throw ConnectFailed(pipeName, error);
            }
            catch (UnauthorizedAccessException error)
            {
                throw ConnectFailed(pipeName, error);
            }
            catch (ArgumentException error)
            {
                throw ConnectFailed(pipeName, error);
            }
        }

        private static string Describe(TimeSpan value)
        {
            return value.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " 秒";
        }

        private static BridgeException ConnectFailed(string pipeName, Exception error)
        {
            return new BridgeException(
                BridgeErrorCodes.ConnectFailed,
                "ホストのパイプ " + pipeName + " へ接続できない: " + error.Message);
        }

        internal static Task<Stream> OpenNamedPipeAsync(
            string pipeName, CancellationToken cancellationToken)
        {
            return OpenNamedPipeAsync(pipeName, cancellationToken, ConnectWaitLimit);
        }

        internal static async Task<Stream> OpenNamedPipeAsync(
            string pipeName, CancellationToken cancellationToken, TimeSpan waitLimit)
        {
            NamedPipeClientStream pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync((int)waitLimit.TotalMilliseconds, cancellationToken)
                    .ConfigureAwait(false);
                return pipe;
            }
            catch
            {
                pipe.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// ホストへ要求を中継する。ツール呼び出しのたびに接続を開くのではなく、最初の呼び出しで
    /// 接続して handshake し、以後はその接続を再利用する。応答の不正・切断・予算の不一致は
    /// いずれも接続を捨て、次の呼び出しで新しい接続からやり直す。
    /// </summary>
    public sealed class HostIpcClient : IDisposable
    {
        /// <summary>
        /// 接続・handshake・ホストの要求処理を待つ上限。ホストの処理タイムアウトへ往復の余裕を
        /// 足した値。
        /// </summary>
        public static readonly TimeSpan DefaultWaitLimit = TimeSpan.FromSeconds(125);

        private readonly IHostConnector _connector;
        private readonly HostRequestQueue _queue = new HostRequestQueue();

        /// <summary>パイプ名ごとの、そのホストが handshake で渡したセッションの識別子。</summary>
        private readonly Dictionary<string, string> _sessionsByPipeName =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private Stream _stream;
        private BridgeMessageChannel _channel;
        private int _lastRequestId;
        private string _connectedPipeName;
        private string _connectedHostVersion;
        private string _reportedPipeName;
        private string _selectedPipeName;

        public HostIpcClient(IHostConnector connector, int budgetChars)
            : this(connector, budgetChars, DefaultWaitLimit)
        {
        }

        internal HostIpcClient(IHostConnector connector, int budgetChars, TimeSpan waitLimit)
        {
            if (connector == null)
            {
                throw new ArgumentNullException(nameof(connector));
            }

            _connector = connector;
            BudgetChars = budgetChars;
            WaitLimit = waitLimit;
        }

        /// <summary>ホストと一致していなければならない応答サイズ予算の文字数。</summary>
        public int BudgetChars { get; }

        public TimeSpan WaitLimit { get; }

        public bool IsConnected => _channel != null;

        /// <summary>
        /// ホストのメソッドを1件呼び、成功応答の結果と接続先の知らせを返す。未接続なら接続して
        /// handshake を済ませてから送る。失敗は <see cref="BridgeException"/> で返す。
        /// </summary>
        public Task<HostCallResult> CallAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            return InTurnAsync(
                limited => CallCoreAsync(method, parameters, limited), cancellationToken);
        }

        /// <summary>
        /// ホストのメソッドを1件呼び、成功応答を <paramref name="read"/> で写した値を返す。写すのは
        /// 順番の中で行う。<paramref name="read"/> が <see cref="FormatException"/> を投げたら、応答が
        /// 契約から外れているとして、順番を譲る前に接続を捨てて <c>BRIDGE_PROTOCOL_ERROR</c> にする。
        /// </summary>
        public Task<T> CallAsync<T>(
            string method,
            JsonObject parameters,
            Func<HostCallResult, T> read,
            CancellationToken cancellationToken)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }

            return InTurnAsync(
                async limited =>
                {
                    HostCallResult response = await CallCoreAsync(method, parameters, limited)
                        .ConfigureAwait(false);
                    try
                    {
                        return read(response);
                    }
                    catch (FormatException broken)
                    {
                        throw FailAndClose(BridgeErrorCodes.ProtocolError, broken.Message);
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// 接続先に選んだパイプ。選んでいなければ null。選ぶと、以後の接続はこのパイプへだけ開く。
        /// </summary>
        public string SelectedPipeName => _selectedPipeName;

        /// <summary>いま繋いでいる相手のパイプ名。繋いでいなければ null。</summary>
        public string ConnectedPipeName => _connectedPipeName;

        /// <summary>
        /// 接続先を <paramref name="pipeName"/> に選び、そこへ繋いで handshake を済ませてから、接続先の
        /// 知らせを返す。そのパイプへ接続を開けなければ、選び直さず、いまの接続も保ったまま
        /// <see cref="BridgeException"/> を投げる。
        /// </summary>
        public Task<HostCallResult> SelectAsync(string pipeName, CancellationToken cancellationToken)
        {
            if (pipeName == null)
            {
                throw new ArgumentNullException(nameof(pipeName));
            }

            return InTurnAsync(
                limited => SelectCoreAsync(pipeName, limited), cancellationToken, false);
        }

        private async Task<HostCallResult> SelectCoreAsync(
            string pipeName, CancellationToken cancellationToken)
        {
            if (IsConnected && string.Equals(_connectedPipeName, pipeName, StringComparison.Ordinal))
            {
                _selectedPipeName = pipeName;
                return new HostCallResult(null, TakeTargetNotice());
            }

            HostConnection connection = await _connector
                .ConnectAsync(pipeName, cancellationToken)
                .ConfigureAwait(false);

            Stream keptStream = _stream;
            BridgeMessageChannel keptChannel = _channel;
            string keptPipeName = _connectedPipeName;
            string keptHostVersion = _connectedHostVersion;
            _stream = null;
            _channel = null;
            _connectedPipeName = null;
            _connectedHostVersion = null;
            try
            {
                await HandshakeAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                Close();
                _stream = keptStream;
                _channel = keptChannel;
                _connectedPipeName = keptPipeName;
                _connectedHostVersion = keptHostVersion;
                throw;
            }

            keptStream?.Dispose();
            _selectedPipeName = pipeName;
            return new HostCallResult(null, TakeTargetNotice());
        }

        private Task<T> InTurnAsync<T>(
            Func<CancellationToken, Task<T>> body, CancellationToken cancellationToken)
        {
            return InTurnAsync(body, cancellationToken, true);
        }

        /// <summary>
        /// 順番を取り、待つ上限を掛けて <paramref name="body"/> を走らせる。
        /// <paramref name="closesOnAbandon"/> が真なら、打ち切りと取り消しでは接続を捨てる。
        /// </summary>
        private async Task<T> InTurnAsync<T>(
            Func<CancellationToken, Task<T>> body,
            CancellationToken cancellationToken,
            bool closesOnAbandon)
        {
            await _queue.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using CancellationTokenSource limit =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                limit.CancelAfter(WaitLimit);

                try
                {
                    return await body(limit.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    string message = "ホストからの応答が " + Describe(WaitLimit) + " 以内に返らなかった。";
                    throw closesOnAbandon
                        ? FailAndClose(BridgeErrorCodes.Timeout, message)
                        : new BridgeException(BridgeErrorCodes.Timeout, message);
                }
                catch (OperationCanceledException)
                {
                    if (closesOnAbandon)
                    {
                        Close();
                    }

                    throw;
                }
            }
            finally
            {
                _queue.Leave();
            }
        }

        private async Task<HostCallResult> CallCoreAsync(
            string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            if (!IsConnected)
            {
                await ConnectAndHandshakeAsync(cancellationToken).ConfigureAwait(false);
            }

            HostResponse response = await ExchangeAsync(method, parameters, false, cancellationToken)
                .ConfigureAwait(false);
            if (response.IsError)
            {
                string code = BridgeErrorCodes.ForHostError(response.ErrorCode);
                if (HostDisconnectsAfter(response.ErrorCode))
                {
                    throw FailAndClose(code, response.ErrorMessage);
                }

                throw new BridgeException(code, response.ErrorMessage);
            }

            return new HostCallResult(response.Result, TakeTargetNotice());
        }

        /// <summary>
        /// 結果の先頭へ置く接続先の知らせを作り、名乗った相手として控える。前に名乗った相手と
        /// 違えば、変わった事実と前の相手も添える。
        /// </summary>
        private string TakeTargetNotice()
        {
            string previous = _reportedPipeName;
            _reportedPipeName = _connectedPipeName;

            if (previous == null || previous == _connectedPipeName)
            {
                return DescribeTarget(_connectedPipeName, _connectedHostVersion);
            }

            return DescribeChangedTarget(previous, _connectedPipeName, _connectedHostVersion);
        }

        private static string DescribeTarget(string pipeName, string hostVersion)
        {
            return "接続先: " + DescribeHost(pipeName, hostVersion);
        }

        private static string DescribeChangedTarget(
            string previousPipeName, string pipeName, string hostVersion)
        {
            return "接続先が変わった: " + previousPipeName + " から " + DescribeHost(pipeName, hostVersion)
                + " へ。以前の応答は別のエディタのものである。";
        }

        private static string DescribeHost(string pipeName, string hostVersion)
        {
            return pipeName + "(ホスト " + hostVersion + ")";
        }

        public void Dispose()
        {
            Close();
        }

        private async Task ConnectAndHandshakeAsync(CancellationToken cancellationToken)
        {
            HostConnection connection = await _connector
                .ConnectAsync(_selectedPipeName, cancellationToken)
                .ConfigureAwait(false);
            await HandshakeAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        private async Task HandshakeAsync(HostConnection connection, CancellationToken cancellationToken)
        {
            _stream = connection.Stream;
            _connectedPipeName = connection.PipeName;
            _channel = new BridgeMessageChannel(connection.Stream);

            JsonObject parameters = new JsonObject { ["protocol"] = HostProtocol.Number };
            string presented;
            if (connection.PipeName != null
                && _sessionsByPipeName.TryGetValue(connection.PipeName, out presented))
            {
                parameters["session"] = presented;
            }

            HostResponse response = await ExchangeAsync("handshake", parameters, true, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsError)
            {
                throw FailAndClose(
                    BridgeErrorCodes.HandshakeMismatch,
                    "ホストが handshake を拒んだ("
                        + BridgeErrorCodes.ForHostError(response.ErrorCode) + "): " + response.ErrorMessage);
            }

            string hostVersion;
            int hostBudgetChars;
            string hostToolMapDigest;
            string session;
            string invalidReason;
            if (!TryReadHandshake(
                response.Result,
                out hostVersion,
                out hostBudgetChars,
                out hostToolMapDigest,
                out session,
                out invalidReason))
            {
                throw FailAndClose(BridgeErrorCodes.HandshakeMismatch, invalidReason);
            }

            if (!string.Equals(
                hostToolMapDigest, GeneratedToolDefinitions.ToolMapDigest, StringComparison.Ordinal))
            {
                throw FailAndClose(
                    BridgeErrorCodes.ToolDefinitionMismatch,
                    "ホストの中継とブリッジのツール定義が別の能力対応表から作られている。"
                        + "ホストの指紋は " + hostToolMapDigest + " で、ブリッジの指紋は "
                        + GeneratedToolDefinitions.ToolMapDigest + " である。"
                        + "同じバージョンのホストDLLとブリッジを使う。");
            }

            if (hostBudgetChars != BudgetChars)
            {
                throw FailAndClose(
                    BridgeErrorCodes.BudgetMismatch,
                    "ホストの応答サイズ予算は " + Describe(hostBudgetChars) + " 文字で、ブリッジの "
                        + Describe(BudgetChars) + " 文字と一致しない。両者へ同じ値を設定する。");
            }

            if (session != null && connection.PipeName != null)
            {
                _sessionsByPipeName[connection.PipeName] = session;
            }

            _connectedHostVersion = hostVersion;
        }

        private async Task<HostResponse> ExchangeAsync(
            string method, JsonObject parameters, bool duringHandshake, CancellationToken cancellationToken)
        {
            int requestId = ++_lastRequestId;
            string request = BridgeJsonRpc.SerializeRequest(requestId, method, parameters);

            int requestBytes = BridgeMessageChannel.MeasureBytes(request);
            if (requestBytes > _channel.MaxMessageBytes)
            {
                throw new BridgeException(
                    BridgeErrorCodes.RequestTooLarge,
                    "要求が " + Describe(requestBytes) + " バイトで、上限の "
                        + Describe(_channel.MaxMessageBytes) + " バイトを超えている。");
            }

            BridgeMessageRead read;
            try
            {
                await _channel.WriteAsync(request, cancellationToken).ConfigureAwait(false);
                read = await _channel.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException error)
            {
                throw FailAndClose(BridgeErrorCodes.ConnectionLost, "ホストとの送受信に失敗した: " + error.Message);
            }
            catch (ObjectDisposedException error)
            {
                throw FailAndClose(BridgeErrorCodes.ConnectionLost, "ホストとの接続が閉じられた: " + error.Message);
            }

            if (read.Outcome == BridgeMessageOutcome.EndOfStream)
            {
                throw FailAndClose(BridgeErrorCodes.ConnectionLost, "応答を受け取る前にホストが切断した。");
            }

            if (read.Outcome == BridgeMessageOutcome.TooLarge)
            {
                throw FailAndClose(
                    ResponseErrorCode(duringHandshake), "ホストの応答が上限のバイト数を超えている。");
            }

            if (read.Outcome == BridgeMessageOutcome.InvalidEncoding)
            {
                throw FailAndClose(
                    ResponseErrorCode(duringHandshake), "ホストの応答がUTF-8として解釈できない。");
            }

            HostResponseParseResult parsed = BridgeJsonRpc.ParseResponse(read.Message, requestId);
            if (!parsed.IsValid)
            {
                throw FailAndClose(ResponseErrorCode(duringHandshake), parsed.InvalidReason);
            }

            return parsed.Response;
        }

        /// <summary>
        /// ホストが応答を返したあとに切断する契約のエラーコードかどうか。
        /// </summary>
        private static bool HostDisconnectsAfter(int hostErrorCode)
        {
            return hostErrorCode == JsonRpcErrorCodes.ParseError
                || hostErrorCode == JsonRpcErrorCodes.ProtocolMismatch
                || hostErrorCode == JsonRpcErrorCodes.HandshakeRequired
                || hostErrorCode == JsonRpcErrorCodes.RequestTooLarge
                || hostErrorCode == JsonRpcErrorCodes.SessionRefused;
        }

        private static string ResponseErrorCode(bool duringHandshake)
        {
            return duringHandshake ? BridgeErrorCodes.HandshakeMismatch : BridgeErrorCodes.ProtocolError;
        }

        private static bool TryReadHandshake(
            JsonNode result,
            out string hostVersion,
            out int budgetChars,
            out string toolMapDigest,
            out string session,
            out string invalidReason)
        {
            hostVersion = null;
            budgetChars = 0;
            toolMapDigest = null;
            session = null;
            invalidReason = null;

            JsonObject handshake = result as JsonObject;
            if (handshake == null)
            {
                invalidReason = "handshake の結果がオブジェクトでない。";
                return false;
            }

            JsonNode protocolNode;
            int protocol;
            if (!handshake.TryGetPropertyValue("protocol", out protocolNode)
                || !BridgeJsonRpc.TryGetInt32(protocolNode, out protocol))
            {
                invalidReason = "handshake の結果の protocol が整数でない。";
                return false;
            }

            if (protocol != HostProtocol.Number)
            {
                invalidReason = "ホストのプロトコル番号 " + Describe(protocol) + " は、ブリッジの "
                    + Describe(HostProtocol.Number) + " と一致しない。";
                return false;
            }

            JsonNode hostVersionNode;
            if (!handshake.TryGetPropertyValue("hostVersion", out hostVersionNode)
                || !BridgeJsonRpc.TryGetString(hostVersionNode, out hostVersion))
            {
                invalidReason = "handshake の結果の hostVersion が文字列でない。";
                return false;
            }

            JsonNode budgetNode;
            if (!handshake.TryGetPropertyValue("budgetChars", out budgetNode)
                || !BridgeJsonRpc.TryGetInt32(budgetNode, out budgetChars))
            {
                invalidReason = "handshake の結果の budgetChars が整数でない。";
                return false;
            }

            JsonNode digestNode;
            if (!handshake.TryGetPropertyValue("toolMapDigest", out digestNode)
                || !BridgeJsonRpc.TryGetString(digestNode, out toolMapDigest))
            {
                invalidReason = "handshake の結果の toolMapDigest が文字列でない。";
                return false;
            }

            JsonNode sessionNode;
            if (handshake.TryGetPropertyValue("session", out sessionNode)
                && (sessionNode == null || !BridgeJsonRpc.TryGetString(sessionNode, out session)))
            {
                invalidReason = "handshake の結果の session が文字列でない。";
                return false;
            }

            return true;
        }

        private static string Describe(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Describe(TimeSpan value)
        {
            return value.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " 秒";
        }

        private BridgeException FailAndClose(string code, string message)
        {
            Close();
            return new BridgeException(code, message);
        }

        private void Close()
        {
            _channel = null;

            _connectedPipeName = null;
            _connectedHostVersion = null;

            Stream stream = _stream;
            _stream = null;
            stream?.Dispose();
        }
    }
}
