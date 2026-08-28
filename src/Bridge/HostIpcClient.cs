using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge
{
    /// <summary>開いたホストへの接続と、その接続先。</summary>
    public sealed class HostConnection
    {
        /// <summary>開いた通信路と、その相手のパイプ名を与えて生成する。</summary>
        public HostConnection(Stream stream, string pipeName)
        {
            Stream = stream;
            PipeName = pipeName;
        }

        /// <summary>ホストとの通信路。</summary>
        public Stream Stream { get; }

        /// <summary>繋いだ相手のパイプ名。</summary>
        public string PipeName { get; }
    }

    /// <summary>ホストの応答と、それを返した接続先の知らせ。</summary>
    public sealed class HostCallResult
    {
        /// <summary>結果と接続先の知らせを与えて生成する。</summary>
        public HostCallResult(JsonNode result, string targetNotice)
        {
            Result = result;
            TargetNotice = targetNotice;
        }

        /// <summary>ホストが返した結果。</summary>
        public JsonNode Result { get; }

        /// <summary>
        /// この応答を返した相手を伝える一行。応答と一緒に確定させる——別々に取りに行くと、
        /// 間に入った呼び出しが繋ぎ直したときに、応答と知らせの相手が食い違う。
        /// </summary>
        public string TargetNotice { get; }
    }

    /// <summary>ホストへの接続を開く役。</summary>
    public interface IHostConnector
    {
        /// <summary>
        /// ホストへの接続を開く。接続先を決められないときと確立に失敗したときは
        /// <see cref="BridgeException"/> を投げる。
        /// </summary>
        Task<HostConnection> ConnectAsync(CancellationToken cancellationToken);
    }

    /// <summary>待ち受けているホストから決めた名前付きパイプへ接続する。</summary>
    public sealed class NamedPipeHostConnector : IHostConnector
    {
        /// <summary>
        /// パイプが開くのを待つ上限。接続先を決めた時点でそのパイプは待ち受けていたので、開けない
        /// のは待って解決する話ではない。決めてから開くまでの短い隙だけを見込んだ値にし、待ち
        /// 続けずに接続の失敗として返す。
        /// </summary>
        public static readonly TimeSpan ConnectWaitLimit = TimeSpan.FromSeconds(5);

        private readonly Func<string> _resolvePipeName;
        private readonly Func<string, CancellationToken, Task<Stream>> _openPipe;

        /// <summary>
        /// 待ち受けているホストから接続先を決め、名前付きパイプを開く既定の処理で生成する。
        /// </summary>
        public NamedPipeHostConnector()
            : this(PipeTargetResolver.ResolveFromRunningHosts, OpenNamedPipeAsync)
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
        public async Task<HostConnection> ConnectAsync(CancellationToken cancellationToken)
        {
            string pipeName = _resolvePipeName();

            try
            {
                Stream stream = await _openPipe(pipeName, cancellationToken).ConfigureAwait(false);
                return new HostConnection(stream, pipeName);
            }
            catch (TimeoutException)
            {
                // 待ち受けていないときのほか、ホストは同時接続を1本に限るので別の接続が使用中でも
                // ここへ来る。区別できないので、事実だけを述べて考えられる原因を並べる。
                throw new BridgeException(
                    BridgeErrorCodes.ConnectFailed,
                    "ホストのパイプ " + pipeName + " へ " + Describe(ConnectWaitLimit)
                        + "以内に接続できなかった。接続先のエディタが終了している、エディタでホストが"
                        + "停止している、または別の接続がパイプを使用中である可能性がある。");
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
                // 明示指定は黙って自動発見へ落とさないので、OSが名前として受け付けない値も
                // そのまま接続先になる。指定の誤りであって異常ではないため、結果として返す。
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

        /// <summary>
        /// 名前付きパイプを実際に開く既定の処理。接続先の決定だけを差し替えて、この経路を
        /// そのまま通すために内部へ開けている。
        /// </summary>
        internal static async Task<Stream> OpenNamedPipeAsync(string pipeName, CancellationToken cancellationToken)
        {
            NamedPipeClientStream pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                // 上限を付けずに待つと、パイプが無いだけの場合まで要求全体の上限まで待ってしまう。
                await pipe.ConnectAsync((int)ConnectWaitLimit.TotalMilliseconds, cancellationToken)
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
        private readonly HostRequestQueue _queue = new HostRequestQueue();

        private Stream _stream;
        private BridgeMessageChannel _channel;
        private int _lastRequestId;
        private string _connectedPipeName;
        private string _reportedPipeName;

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
        /// ホストのメソッドを1件呼び、成功応答の結果と接続先の知らせを返す。未接続なら接続して
        /// handshake を済ませてから送る。失敗は <see cref="BridgeException"/> で返す。
        /// </summary>
        public async Task<HostCallResult> CallAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            await _queue.EnterAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 順番を取った直後の取り消しは、まだ何も送っていないので接続に触れない。
                cancellationToken.ThrowIfCancellationRequested();

                using CancellationTokenSource limit =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                limit.CancelAfter(WaitLimit);

                try
                {
                    return await CallCoreAsync(method, parameters, limit.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // 呼び出し側ではなく待つ上限で打ち切った。遅れて届く応答を次の要求の応答と
                    // 取り違えないよう、接続を捨てる。
                    throw FailAndClose(
                        BridgeErrorCodes.Timeout,
                        "ホストからの応答が " + Describe(WaitLimit) + " 以内に返らなかった。");
                }
                catch (OperationCanceledException)
                {
                    // 呼び出し側の取り消し。実行されたか分からない要求を残すので接続を捨て、
                    // 同じ要求を送り直さない。
                    Close();
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
                    // ホストはこの応答のあと切断する契約なので、接続を保つと次の呼び出しが
                    // 死んだ接続を使って必ず落ちる。ここで捨てて繋ぎ直せるようにする。
                    throw FailAndClose(code, response.ErrorMessage);
                }

                // ホストが接続を保つ契約のエラーなので、こちらも保ったままコードで知らせる。
                throw new BridgeException(code, response.ErrorMessage);
            }

            // 知らせを確定させるのはこの直列区間の中に限る。応答を返した後で別に取りに行くと、
            // 間に入った呼び出しが繋ぎ直したときに、応答と知らせの相手が食い違う。
            return new HostCallResult(response.Result, TakeTargetNotice());
        }

        /// <summary>
        /// 結果の先頭へ置く接続先の知らせを作り、名乗った相手として控える。前に名乗った相手と
        /// 違えば、変わった事実と前の相手も添える——黙って繋ぎ替えると、呼び出し元は前の応答で
        /// 作った前提のまま別のエディタを操作する。
        /// </summary>
        private string TakeTargetNotice()
        {
            string previous = _reportedPipeName;
            _reportedPipeName = _connectedPipeName;

            if (previous == null || previous == _connectedPipeName)
            {
                return DescribeTarget(_connectedPipeName);
            }

            return DescribeChangedTarget(previous, _connectedPipeName);
        }

        private static string DescribeTarget(string pipeName)
        {
            return "接続先: " + pipeName;
        }

        private static string DescribeChangedTarget(string previousPipeName, string pipeName)
        {
            return "接続先が変わった: " + previousPipeName + " から " + pipeName
                + " へ。以前の応答は別のエディタのものである。";
        }

        /// <summary>保っている接続を閉じる。</summary>
        public void Dispose()
        {
            Close();
        }

        private async Task ConnectAndHandshakeAsync(CancellationToken cancellationToken)
        {
            HostConnection connection = await _connector.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _stream = connection.Stream;
            _connectedPipeName = connection.PipeName;
            _channel = new BridgeMessageChannel(connection.Stream);

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

            // 名乗った相手は繋ぎ直しをまたいで覚えておく——忘れると、別のエディタへ移っても
            // 初めての知らせに見えて、変わった事実が伝わらない。
            _connectedPipeName = null;

            Stream stream = _stream;
            _stream = null;
            stream?.Dispose();
        }
    }
}
