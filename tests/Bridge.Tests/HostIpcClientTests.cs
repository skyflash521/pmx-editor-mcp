using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class HostIpcClientTests
    {
        private const int BudgetChars = 100000;

        /// <summary>テストが待つ上限。製品側が待つ上限を掛け忘れても有限時間で失敗させる。</summary>
        private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(30);

        [Fact]
        public void ハンドシェイクのプロトコル番号は契約で定めた値である()
        {
            Assert.Equal(1, HostIpcClient.Protocol);
        }

        [Fact]
        public async Task 最初の呼び出しで接続しハンドシェイクを済ませてから要求を送る()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            using HostIpcClient client = Connect(host);

            JsonNode result = await client.CallAsync("ping", null, CancellationToken.None);

            Assert.Equal("pong", (string)result);
            Assert.Equal(2, host.Requests.Count);

            JsonObject handshake = JsonNode.Parse(host.Requests[0]).AsObject();
            Assert.Equal("handshake", (string)handshake["method"]);
            Assert.Equal(HostIpcClient.Protocol, (int)handshake["params"]["protocol"]);

            JsonObject call = JsonNode.Parse(host.Requests[1]).AsObject();
            Assert.Equal("ping", (string)call["method"]);
        }

        [Fact]
        public async Task 続けての呼び出しは同じ接続を使いハンドシェイクをやり直さない()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "1"))
                .Reply(request => Result(request, "2"))
                .Start();
            FakeHostConnector connector = new FakeHostConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            await client.CallAsync("first", null, CancellationToken.None);
            await client.CallAsync("second", null, CancellationToken.None);

            Assert.Equal(1, connector.ConnectCount);
            Assert.Equal(3, host.Requests.Count);
        }

        [Fact]
        public async Task 引数を与えた呼び出しはそのまま要求へ載せる()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "null"))
                .Start();
            using HostIpcClient client = Connect(host);

            await client.CallAsync(
                "move", new JsonObject { ["index"] = 7 }, CancellationToken.None);

            JsonObject call = JsonNode.Parse(host.Requests[1]).AsObject();
            Assert.Equal(7, (int)call["params"]["index"]);
        }

        [Theory]
        // プロトコル番号の不一致は、ホストが切断を伴うエラーとして返す。
        [InlineData(-32001, "プロトコル番号が一致しない")]
        [InlineData(-32602, "引数が不正")]
        public async Task ハンドシェイクでホストがエラーを返すと不成立として接続を閉じる(
            int hostErrorCode, string hostMessage)
        {
            using FakeHost host = new FakeHost()
                .Reply(request => Error(request, hostErrorCode, hostMessage))
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.HandshakeMismatch, error.Code);
            Assert.False(client.IsConnected);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("\"ok\"")]
        [InlineData("[1]")]
        [InlineData("{\"protocol\":2,\"hostVersion\":\"1.0.0.0\",\"budgetChars\":100000}")]
        [InlineData("{\"protocol\":\"1\",\"hostVersion\":\"1.0.0.0\",\"budgetChars\":100000}")]
        [InlineData("{\"hostVersion\":\"1.0.0.0\",\"budgetChars\":100000}")]
        [InlineData("{\"protocol\":1,\"budgetChars\":100000}")]
        [InlineData("{\"protocol\":1,\"hostVersion\":5,\"budgetChars\":100000}")]
        [InlineData("{\"protocol\":1,\"hostVersion\":\"1.0.0.0\"}")]
        [InlineData("{\"protocol\":1,\"hostVersion\":\"1.0.0.0\",\"budgetChars\":\"100000\"}")]
        public async Task ハンドシェイクの成功応答が契約に沿わなければ不成立として接続を閉じる(string result)
        {
            using FakeHost host = new FakeHost()
                .Reply(request => Result(request, result))
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.HandshakeMismatch, error.Code);
            Assert.False(client.IsConnected);
        }

        [Theory]
        // 別の要求の識別子・版の不一致・解析できない本文。
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":OTHER_ID,\"result\":{\"protocol\":1,\"hostVersion\":\"1.0.0.0\",\"budgetChars\":100000}}")]
        [InlineData("{\"jsonrpc\":\"1.0\",\"id\":ID,\"result\":{\"protocol\":1,\"hostVersion\":\"1.0.0.0\",\"budgetChars\":100000}}")]
        [InlineData("これはJSONではない")]
        public async Task ハンドシェイクの応答の包絡が契約に沿わなければ不成立として接続を閉じる(string response)
        {
            using FakeHost host = new FakeHost().Reply(WithRequestId(response)).Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.HandshakeMismatch, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task ハンドシェイクの応答が不正なUTF8なら不成立として接続を閉じる()
        {
            // 同じ不正でも、handshakeが成立する前なら不成立として区分する。
            using FakeHost host = new FakeHost()
                .ReplyBytes(new byte[] { 0x82, 0xA0, (byte)'\n' })
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.HandshakeMismatch, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task ハンドシェイクの応答が上限を超えたら不成立として接続を閉じる()
        {
            using FakeHost host = new FakeHost().ReplyBytes(OversizedResponse()).Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.HandshakeMismatch, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task ハンドシェイクの応答を待っている間の切断は切断として返す()
        {
            // 応答を受け取る前に相手が消えただけなので、版やプロトコルの食い違いを示唆する
            // 不成立ではなく切断として区分する。
            using FakeHost host = new FakeHost().Disconnect().Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ConnectionLost, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task 応答サイズ予算が一致しなければ両方の値を示して接続を閉じる()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(200000))
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.BudgetMismatch, error.Code);
            Assert.Contains("200000", error.Message);
            Assert.Contains("100000", error.Message);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task 応答サイズ予算が一致するホストへ繋ぎ直せば通常の動作へ戻る()
        {
            // 予算の不一致でプロセスを終えないので、設定を直したホストへ繋ぎ直せば回復する。
            using FakeHost mismatched = new FakeHost().Reply(HandshakeResultOf(200000)).Start();
            using FakeHost matched = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            SwitchingConnector connector = new SwitchingConnector(mismatched.PipeName, matched.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));
            Assert.Equal(BridgeErrorCodes.BudgetMismatch, error.Code);

            JsonNode result = await client.CallAsync("ping", null, CancellationToken.None);

            Assert.Equal("pong", (string)result);
        }

        [Fact]
        public async Task ハンドシェイクが不成立でも次の呼び出しは新しい接続からやり直す()
        {
            using FakeHost host = new FakeHost()
                .Reply(request => Error(request, -32001, "プロトコル番号が一致しない"))
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            FakeHostConnector connector = new FakeHostConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
            Assert.Equal(2, connector.ConnectCount);
        }

        [Fact]
        public async Task ハンドシェイク後のホストのエラーはホスト由来のコードで返し接続を保つ()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Error(request, -32601, "未知のメソッド"))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("missing", null, CancellationToken.None));

            Assert.Equal("HOST_-32601", error.Code);
            Assert.Equal("未知のメソッド", error.Message);
            Assert.True(client.IsConnected);

            // 接続を保っているので、続けて呼べる。
            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
        }

        [Theory]
        // IPC仕様書のエラー表で、ホストが応答のあと切断すると定めているコード。
        [InlineData(-32700)]
        [InlineData(-32001)]
        [InlineData(-32003)]
        [InlineData(-32004)]
        public async Task ホストが切断を伴うエラーを返したら接続を捨てる(int hostErrorCode)
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Error(request, hostErrorCode, "切断を伴うエラー"))
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            FakeHostConnector connector = new FakeHostConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("boom", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ForHostError(hostErrorCode), error.Code);
            Assert.False(client.IsConnected);

            // 捨てた接続を引きずらないので、次の呼び出しは新しい接続からやり直せる。
            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
            Assert.Equal(2, connector.ConnectCount);
        }

        [Fact]
        public async Task 応答を待っている間に切断されたら切断として返す()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Disconnect()
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ConnectionLost, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task 切断されたあとの呼び出しは新しい接続からやり直す()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Disconnect()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            FakeHostConnector connector = new FakeHostConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
            Assert.Equal(2, connector.ConnectCount);
        }

        [Theory]
        // 別の要求の識別子・結果とエラーの同居・どちらも無い・解析できない本文。識別子の照合
        // だけで落ちないよう、不一致を見るケース以外は要求の識別子に合わせる。
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":OTHER_ID,\"result\":1}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":ID,\"result\":1,\"error\":{\"code\":-1,\"message\":\"x\"}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":ID}")]
        [InlineData("これはJSONではない")]
        public async Task ハンドシェイク後の応答が契約に沿わなければ通信規約の違反として接続を閉じる(string response)
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(WithRequestId(response))
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ProtocolError, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task ハンドシェイク後の不正なUTF8は通信規約の違反として接続を閉じる()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .ReplyBytes(new byte[] { 0x82, 0xA0, (byte)'\n' })
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ProtocolError, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task ハンドシェイク後の応答が上限を超えたら通信規約の違反として接続を閉じる()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .ReplyBytes(OversizedResponse())
                .Start();
            using HostIpcClient client = Connect(host);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ProtocolError, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task 通信規約の違反のあとの呼び出しは新しい接続からやり直す()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply("これはJSONではない")
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            FakeHostConnector connector = new FakeHostConnector(host.PipeName);
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
            Assert.Equal(2, connector.ConnectCount);
        }

        [Fact]
        public async Task 上限を超える要求は送らずに知らせて接続を保つ()
        {
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();
            using HostIpcClient client = Connect(host);

            JsonObject oversized = new JsonObject
            {
                ["text"] = new string('a', BridgeMessageChannel.DefaultMaxMessageBytes),
            };

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("store", oversized, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.RequestTooLarge, error.Code);
            Assert.True(client.IsConnected);

            // 送られていないので、ホストが見た要求は handshake だけである。
            Assert.Single(host.Requests);

            // 接続を保っているので、続けて呼べる。
            Assert.Equal("pong", (string)await client.CallAsync("ping", null, CancellationToken.None));
        }

        [Fact]
        public async Task 破棄すると保っていた接続を手放す()
        {
            // パイプの同時接続は1本なので、破棄が接続を手放していなければ次の接続は成立しない。
            using FakeHost host = new FakeHost()
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Reply(HandshakeResultOf(BudgetChars))
                .Reply(request => Result(request, "\"pong\""))
                .Start();

            HostIpcClient first = Connect(host);
            await first.CallAsync("ping", null, CancellationToken.None);
            first.Dispose();

            Assert.False(first.IsConnected);

            using HostIpcClient second = Connect(host);
            Assert.Equal("pong", (string)await second.CallAsync("ping", null, CancellationToken.None));
        }

        [Fact]
        public async Task 接続役が返した失敗はそのまま要求元へ返す()
        {
            // 接続を確立できたかどうかを判断するのは接続役で、こちらはその結果を包み直さない。
            RefusingConnector connector = new RefusingConnector();
            using HostIpcClient client = new HostIpcClient(connector, BudgetChars);

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => client.CallAsync("ping", null, CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ConnectFailed, error.Code);
            Assert.False(client.IsConnected);
        }

        [Fact]
        public async Task 待ち受けていないパイプは待ち続けずに接続の失敗として返す()
        {
            // 接続先を決めた時点でそのパイプは待ち受けていたので、開けないのは待って解決する
            // 話ではない。待ち続けると要求全体の上限まで使い、原因も分からなくなる。
            Assert.Equal(TimeSpan.FromSeconds(5), NamedPipeHostConnector.ConnectWaitLimit);

            // 接続先の決定だけを固定し、パイプを開く処理は製品と同じものを通す。
            string absent = "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N");
            NamedPipeHostConnector connector = new NamedPipeHostConnector(
                () => absent, NamedPipeHostConnector.OpenNamedPipeAsync);

            Stopwatch elapsed = Stopwatch.StartNew();
            BridgeException error = await ThrowsWithin<BridgeException>(
                () => connector.ConnectAsync(CancellationToken.None));
            elapsed.Stop();

            Assert.Equal(BridgeErrorCodes.ConnectFailed, error.Code);
            Assert.Contains(absent, error.Message);

            // パイプ名だけを検査すると、原因を1つに断定する本文へ戻しても通ってしまう。
            // 観測した事実と、原因が1つに絞られていないことの両方を固定する。
            // 語ごとに検査すると、同じ候補を断定文で並べ直した本文も通ってしまう。挙げた
            // 候補以外の原因もありうるので、観測した事実から候補の列挙を経て断定していない
            // ことを示す語尾までを、一続きの文として固定する。
            Assert.Contains(
                ConnectWaitLimitSeconds + " 秒以内に接続できなかった。接続先のエディタが終了している、"
                    + "エディタでホストが停止している、または別の接続がパイプを使用中である可能性がある。",
                error.Message);

            // 打ち切りは公開した上限で決まる。上下から挟まないと、上限を名乗りながら実際には
            // ずっと短い値で諦める作りも、上限と無関係に長く待つ作りも通ってしまう。
            Assert.InRange(
                elapsed.Elapsed,
                NamedPipeHostConnector.ConnectWaitLimit - TimeSpan.FromMilliseconds(500),
                NamedPipeHostConnector.ConnectWaitLimit + TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task OSが受け付けない名前を指していたら接続の失敗として返す()
        {
            // 明示指定は黙って自動発見へ落とさないので、空の名前もそのまま接続先になる。
            // パイプを開く処理は製品と同じものを通し、OSの拒否がどう表れるかまで見る。
            NamedPipeHostConnector connector = new NamedPipeHostConnector(
                () => string.Empty, NamedPipeHostConnector.OpenNamedPipeAsync);

            BridgeException error = await ThrowsWithin<BridgeException>(
                () => connector.ConnectAsync(CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ConnectFailed, error.Code);
        }

        [Fact]
        public async Task 接続中に取り消されたら接続の失敗ではなく取り消しとして返す()
        {
            // 上限による打ち切りと呼び出し側の取り消しは、どちらも同じ種類の例外で表れる。
            // 取り消しまで接続の失敗へ変えると、呼び出し側が自分で止めたことが分からなくなる。
            string absent = "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N");
            NamedPipeHostConnector connector = new NamedPipeHostConnector(
                () => absent, NamedPipeHostConnector.OpenNamedPipeAsync);

            using CancellationTokenSource connecting = new CancellationTokenSource();
            Task<Stream> opening = connector.ConnectAsync(connecting.Token);

            await Task.Delay(TimeSpan.FromMilliseconds(300));
            connecting.Cancel();

            await WithinTestWait(Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening));
        }

        [Fact]
        public async Task 少し遅れて現れたパイプは上限のあいだ待って受け入れる()
        {
            // 決めてから開くまでの短い隙にパイプが入れ替わる場合まで落とさない。即座に諦める
            // 作りだと、ホストが繋ぎ直しの合間にいるだけで理由もなく失敗する。
            string pipeName = "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N");
            NamedPipeHostConnector connector = new NamedPipeHostConnector(
                () => pipeName, NamedPipeHostConnector.OpenNamedPipeAsync);

            Task<Stream> connecting = connector.ConnectAsync(CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(300));
            using NamedPipeServerStream listening = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            Task accepting = listening.WaitForConnectionAsync();

            using Stream opened = await WithinTestWait(connecting);

            await WithinTestWait(accepting);
            Assert.True(listening.IsConnected);
        }

        [Theory]
        [InlineData(typeof(IOException))]
        [InlineData(typeof(UnauthorizedAccessException))]
        [InlineData(typeof(ArgumentException))]
        public async Task パイプを開けなかった失敗は接続の失敗として返す(Type failure)
        {
            // 接続先の決定も差し替える。実行環境に待ち受けているホストが無い・複数あると、パイプを
            // 開く手前の分岐で終わってしまい、確かめたい変換へ届かない。
            NamedPipeHostConnector connector = new NamedPipeHostConnector(
                () => "pmx-editor-mcp-0",
                (pipeName, cancellationToken) =>
                    Task.FromException<Stream>((Exception)Activator.CreateInstance(failure)));

            BridgeException error = await Assert.ThrowsAsync<BridgeException>(
                () => connector.ConnectAsync(CancellationToken.None));

            Assert.Equal(BridgeErrorCodes.ConnectFailed, error.Code);
        }

        [Fact]
        public async Task 接続のたびに接続先を決め直して開く()
        {
            // エディタを起動し直すとパイプ名は変わる。決め直さずに握り続けると、繋ぎ直しが
            // 消えたエディタを指したままになる。
            int resolved = 0;
            List<string> opened = new List<string>();

            NamedPipeHostConnector connector = new NamedPipeHostConnector(
                () => "pmx-editor-mcp-" + (++resolved).ToString(CultureInfo.InvariantCulture),
                (pipeName, cancellationToken) =>
                {
                    opened.Add(pipeName);
                    return Task.FromException<Stream>(new IOException());
                });

            await Assert.ThrowsAsync<BridgeException>(
                () => connector.ConnectAsync(CancellationToken.None));
            await Assert.ThrowsAsync<BridgeException>(
                () => connector.ConnectAsync(CancellationToken.None));

            Assert.Equal(new string[] { "pmx-editor-mcp-1", "pmx-editor-mcp-2" }, opened);
        }

        /// <summary>接続の待機上限を、本文に現れるのと同じ表記で得る。</summary>
        private static string ConnectWaitLimitSeconds
        {
            get
            {
                return NamedPipeHostConnector.ConnectWaitLimit.TotalSeconds
                    .ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>上限付きで例外を待つ。製品側が待つ上限を掛け忘れても有限時間で失敗する。</summary>
        private static async Task<TException> ThrowsWithin<TException>(Func<Task> action)
            where TException : Exception
        {
            Task<TException> throwing = Assert.ThrowsAnyAsync<TException>(action);

            return await WithinTestWait(throwing);
        }

        private static async Task<T> WithinTestWait<T>(Task<T> pending)
        {
            await WithinTestWait((Task)pending);
            return await pending;
        }

        private static async Task WithinTestWait(Task pending)
        {
            Task finished = await Task.WhenAny(pending, Task.Delay(TestWait));

            Assert.True(ReferenceEquals(finished, pending), "待機上限内に終わらなかった。");
            await pending;
        }

        private static HostIpcClient Connect(FakeHost host)
        {
            return new HostIpcClient(new FakeHostConnector(host.PipeName), BudgetChars);
        }

        /// <summary>ハンドシェイクの成功応答を、受け取った要求の識別子に合わせて組み立てる。</summary>
        private static Func<string, string> HandshakeResultOf(int budgetChars)
        {
            return request => Result(
                request,
                "{\"protocol\":1,\"hostVersion\":\"1.0.0.0\",\"budgetChars\":" + budgetChars + "}");
        }

        /// <summary>
        /// 応答の雛形の中の ID を受け取った要求の識別子へ、OTHER_ID をそれとは別の識別子へ
        /// 置き換える。識別子の決め方は実装の裁量なので、テストが特定の値を当て込まない。
        /// </summary>
        private static Func<string, string> WithRequestId(string template)
        {
            return request => template
                .Replace("OTHER_ID", (IdOf(request) + 1).ToString(CultureInfo.InvariantCulture))
                .Replace("ID", IdOf(request).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>本文の上限を1バイト超える応答を作る。</summary>
        private static byte[] OversizedResponse()
        {
            byte[] payload = new byte[BridgeMessageChannel.DefaultMaxMessageBytes + 2];
            Array.Fill(payload, (byte)'a');
            payload[payload.Length - 1] = (byte)'\n';
            return payload;
        }

        private static string Result(string request, string result)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + IdOf(request) + ",\"result\":" + result + "}";
        }

        private static string Error(string request, int code, string message)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + IdOf(request)
                + ",\"error\":{\"code\":" + code + ",\"message\":\"" + message + "\"}}";
        }

        private static int IdOf(string request)
        {
            return (int)JsonNode.Parse(request).AsObject()["id"];
        }

        /// <summary>1回目と2回目以降で別のホストへ繋ぐ接続役。</summary>
        private sealed class SwitchingConnector : IHostConnector
        {
            private readonly FakeHostConnector _first;
            private readonly FakeHostConnector _rest;

            private int _opened;

            public SwitchingConnector(string firstPipeName, string restPipeName)
            {
                _first = new FakeHostConnector(firstPipeName);
                _rest = new FakeHostConnector(restPipeName);
            }

            public Task<Stream> ConnectAsync(CancellationToken cancellationToken)
            {
                _opened++;
                return _opened == 1
                    ? _first.ConnectAsync(cancellationToken)
                    : _rest.ConnectAsync(cancellationToken);
            }
        }

        /// <summary>接続の確立に失敗したことを知らせる接続役。</summary>
        private sealed class RefusingConnector : IHostConnector
        {
            public Task<Stream> ConnectAsync(CancellationToken cancellationToken)
            {
                throw new BridgeException(BridgeErrorCodes.ConnectFailed, "接続を確立できない。");
            }
        }
    }
}
