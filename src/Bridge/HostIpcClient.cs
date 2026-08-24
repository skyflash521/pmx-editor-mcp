using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ホストへの接続を開く役。</summary>
    public interface IHostConnector
    {
        /// <summary>
        /// ホストへの接続を開く。接続先を決められないときと確立に失敗したときは
        /// <see cref="BridgeException"/> を投げる。
        /// </summary>
        Task<Stream> ConnectAsync(CancellationToken cancellationToken);
    }

    /// <summary>環境変数と起動中のPMXエディタから決めた名前付きパイプへ接続する。</summary>
    public sealed class NamedPipeHostConnector : IHostConnector
    {
        private readonly Func<string> _resolvePipeName;
        private readonly Func<string, CancellationToken, Task<Stream>> _openPipe;

        /// <summary>
        /// 環境変数と起動中のPMXエディタから接続先を決め、名前付きパイプを開く既定の処理で生成する。
        /// </summary>
        public NamedPipeHostConnector()
            : this(PipeTargetResolver.ResolveFromEnvironment, OpenNamedPipeAsync)
        {
        }

        /// <summary>
        /// 接続先を決める処理とパイプを開く処理を差し替えて生成する。入出力の失敗やアクセス拒否を
        /// エラーコードへ変換する経路は、実際にそれらを起こさないと通らないため、外から与える。
        /// 接続先の決定も差し替えるのは、実行環境のエディタの起動状況で手前の分岐へ逸れないようにするため。
        /// </summary>
        internal NamedPipeHostConnector(
            Func<string> resolvePipeName,
            Func<string, CancellationToken, Task<Stream>> openPipe)
        {
            _resolvePipeName = resolvePipeName;
            _openPipe = openPipe;
        }

        /// <summary>
        /// 接続のたびに接続先を決め直してから開く。エディタの起動・終了でパイプ名は変わるので、
        /// 一度決めた名前を握り続けると、繋ぎ直しが消えたエディタを指したままになる。
        /// </summary>
        public async Task<Stream> ConnectAsync(CancellationToken cancellationToken)
        {
            string pipeName = _resolvePipeName();

            try
            {
                return await _openPipe(pipeName, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException error)
            {
                throw ConnectFailed(pipeName, error);
            }
            catch (UnauthorizedAccessException error)
            {
                throw ConnectFailed(pipeName, error);
            }
        }

        private static BridgeException ConnectFailed(string pipeName, Exception error)
        {
            return new BridgeException(
                BridgeErrorCodes.ConnectFailed,
                "ホストのパイプ " + pipeName + " へ接続できない: " + error.Message);
        }

        private static async Task<Stream> OpenNamedPipeAsync(string pipeName, CancellationToken cancellationToken)
        {
            NamedPipeClientStream pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
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
        /// <summary>handshake で一致していなければならないプロトコル番号。</summary>
        public const int Protocol = 1;

        /// <summary>
        /// 接続・handshake・ホストの要求処理を待つ上限。ホストの処理タイムアウトへ往復の余裕を
        /// 足した値で、これより短いとホストが処理しきる要求まで打ち切ってしまう。
        /// </summary>
        public static readonly TimeSpan DefaultWaitLimit = TimeSpan.FromSeconds(125);

        private const int HostParseError = -32700;
        private const int HostProtocolMismatch = -32001;
        private const int HostHandshakeRequired = -32003;
        private const int HostInputTooLarge = -32004;

        private readonly IHostConnector _connector;

        private Stream _stream;
        private BridgeMessageChannel _channel;
        private int _lastRequestId;

        /// <summary>
        /// 接続の開き方と、ホストと一致していなければならない応答サイズ予算の文字数を与えて生成する。
        /// </summary>
        public HostIpcClient(IHostConnector connector, int budgetChars)
            : this(connector, budgetChars, DefaultWaitLimit)
        {
        }

        /// <summary>
        /// 待つ上限を差し替えて生成する。既定の上限は待ち切るのにテストが実時間を費やすため、
        /// 打ち切りの振る舞いを確かめるときだけ短くする。
        /// </summary>
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

        /// <summary>接続・handshake・ホストの要求処理を待つ上限。</summary>
        public TimeSpan WaitLimit { get; }

        /// <summary>ホストへの接続を保っているかどうか。</summary>
        public bool IsConnected => _channel != null;

        /// <summary>
        /// ホストのメソッドを1件呼び、成功応答の結果を返す。未接続なら接続して handshake を
        /// 済ませてから送る。失敗は <see cref="BridgeException"/> で返す。
        /// </summary>
        public async Task<JsonNode> CallAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
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
                    // ホストはこの応答のあと切断する契約なので、接続を保つと次の呼び出しが
                    // 死んだ接続を使って必ず落ちる。ここで捨てて繋ぎ直せるようにする。
                    throw FailAndClose(code, response.ErrorMessage);
                }

                // ホストが接続を保つ契約のエラーなので、こちらも保ったままコードで知らせる。
                throw new BridgeException(code, response.ErrorMessage);
            }

            return response.Result;
        }

        /// <summary>保っている接続を閉じる。</summary>
        public void Dispose()
        {
            Close();
        }

        private async Task ConnectAndHandshakeAsync(CancellationToken cancellationToken)
        {
            Stream stream = await _connector.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _stream = stream;
            _channel = new BridgeMessageChannel(stream);

            JsonObject parameters = new JsonObject { ["protocol"] = Protocol };
            HostResponse response = await ExchangeAsync("handshake", parameters, true, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsError)
            {
                throw FailAndClose(
                    BridgeErrorCodes.HandshakeMismatch,
                    "ホストが handshake を拒んだ("
                        + BridgeErrorCodes.ForHostError(response.ErrorCode) + "): " + response.ErrorMessage);
            }

            int hostBudgetChars;
            string invalidReason;
            if (!TryReadHandshakeBudget(response.Result, out hostBudgetChars, out invalidReason))
            {
                throw FailAndClose(BridgeErrorCodes.HandshakeMismatch, invalidReason);
            }

            if (hostBudgetChars != BudgetChars)
            {
                throw FailAndClose(
                    BridgeErrorCodes.BudgetMismatch,
                    "ホストの応答サイズ予算は " + Describe(hostBudgetChars) + " 文字で、ブリッジの "
                        + Describe(BudgetChars) + " 文字と一致しない。両者へ同じ値を設定する。");
            }
        }

        private async Task<HostResponse> ExchangeAsync(
            string method, JsonObject parameters, bool duringHandshake, CancellationToken cancellationToken)
        {
            int requestId = ++_lastRequestId;
            string request = BridgeJsonRpc.SerializeRequest(requestId, method, parameters);

            int requestBytes = BridgeMessageChannel.MeasureBytes(request);
            if (requestBytes > _channel.MaxMessageBytes)
            {
                // 送らないので接続は保つ。ホスト側の上限超過は切断を伴うので、そこへ持ち込まない。
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
        /// ホストが応答を返したあとに切断する契約のエラーコードかどうか。IPC仕様書のエラー表が
        /// コードごとに接続を切るか保つかを定めており、切る側をここで数える。
        /// </summary>
        private static bool HostDisconnectsAfter(int hostErrorCode)
        {
            return hostErrorCode == HostParseError
                || hostErrorCode == HostProtocolMismatch
                || hostErrorCode == HostHandshakeRequired
                || hostErrorCode == HostInputTooLarge;
        }

        /// <summary>
        /// 応答の不正をどのコードで返すかは、handshake が成立する前か後かで決まる。成立前の不正は
        /// 相手がホストとして噛み合っていないことを指し、成立後の不正は通信規約の違反を指す。
        /// </summary>
        private static string ResponseErrorCode(bool duringHandshake)
        {
            return duringHandshake ? BridgeErrorCodes.HandshakeMismatch : BridgeErrorCodes.ProtocolError;
        }

        private static bool TryReadHandshakeBudget(JsonNode result, out int budgetChars, out string invalidReason)
        {
            budgetChars = 0;
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

            if (protocol != Protocol)
            {
                invalidReason = "ホストのプロトコル番号 " + Describe(protocol) + " は、ブリッジの "
                    + Describe(Protocol) + " と一致しない。";
                return false;
            }

            JsonNode hostVersionNode;
            string hostVersion;
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

            return true;
        }

        private static string Describe(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private BridgeException FailAndClose(string code, string message)
        {
            Close();
            return new BridgeException(code, message);
        }

        private void Close()
        {
            _channel = null;

            Stream stream = _stream;
            _stream = null;
            stream?.Dispose();
        }
    }
}
