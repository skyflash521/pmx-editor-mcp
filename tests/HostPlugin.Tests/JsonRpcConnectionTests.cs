using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class JsonRpcConnectionTests : IDisposable
    {
        private const string HostVersion = "1.2.3.4";
        private const int BudgetChars = 100000;

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

        private readonly string _directory;
        private readonly HostLog _log;

        public JsonRpcConnectionTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _log = new HostLog(Path.Combine(_directory, "host.log"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string Handshake(int id, int protocol)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"handshake\",\"params\":{\"protocol\":" + protocol + "}}";
        }

        private static string Handshake()
        {
            return Handshake(1, 1);
        }

        private static string Request(int id, string method)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\"}";
        }

        private static string RequestWithArrayParams(int id, string method)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\",\"params\":[1,2]}";
        }

        private JsonRpcConnection CreateConnection(McpMethodTable methods)
        {
            return new JsonRpcConnection(_log, methods, HostVersion, BudgetChars);
        }

        private JsonRpcConnection CreateConnection(McpMethodTable methods, TimeSpan requestTimeout, int maxMessageBytes)
        {
            return new JsonRpcConnection(_log, methods, HostVersion, BudgetChars, requestTimeout, maxMessageBytes);
        }

        private static byte[] Lines(params string[] requests)
        {
            return Utf8WithoutBom.GetBytes(string.Join("\n", requests) + "\n");
        }

        /// <summary>要求の並びを流し込み、返った応答を解いて返す。</summary>
        private static IList<IDictionary<string, object>> Exchange(JsonRpcConnection connection, params string[] requests)
        {
            return ExchangeBytes(connection, Lines(requests));
        }

        private static IList<IDictionary<string, object>> ExchangeWith(
            JsonRpcConnection connection, IUiInvoker ui, params string[] requests)
        {
            using (ExchangeStream stream = new ExchangeStream(Lines(requests)))
            {
                connection.Handle(stream, ui);
                return stream.ReadResponses();
            }
        }

        private static IList<IDictionary<string, object>> ExchangeBytes(JsonRpcConnection connection, byte[] input)
        {
            using (ExchangeStream stream = new ExchangeStream(input))
            {
                connection.Handle(stream, new InlineInvoker());
                return stream.ReadResponses();
            }
        }

        private static int ErrorCodeOf(IDictionary<string, object> response)
        {
            object error;
            Assert.True(response.TryGetValue("error", out error), "エラーの応答ではない。");

            IDictionary<string, object> body = Assert.IsAssignableFrom<IDictionary<string, object>>(error);
            Assert.True(body.ContainsKey("message"));
            return Convert.ToInt32(body["code"]);
        }

        private static string ErrorMessageOf(IDictionary<string, object> response)
        {
            IDictionary<string, object> body =
                Assert.IsAssignableFrom<IDictionary<string, object>>(response["error"]);
            return (string)body["message"];
        }

        private static object ResultOf(IDictionary<string, object> response)
        {
            object result;
            Assert.True(response.TryGetValue("result", out result), "成功の応答ではない。");
            Assert.False(response.ContainsKey("error"));
            return result;
        }

        private static object IdOf(IDictionary<string, object> response)
        {
            Assert.Equal("2.0", response["jsonrpc"]);
            return response["id"];
        }

        [Fact]
        public void ハンドシェイクに成功すると契約の値を返す()
        {
            IList<IDictionary<string, object>> responses = Exchange(CreateConnection(new McpMethodTable()), Handshake());

            Assert.Single(responses);
            Assert.Equal(1, Convert.ToInt32(IdOf(responses[0])));

            IDictionary<string, object> result =
                Assert.IsAssignableFrom<IDictionary<string, object>>(ResultOf(responses[0]));
            Assert.Equal(JsonRpcConnection.Protocol, Convert.ToInt32(result["protocol"]));
            Assert.Equal(HostVersion, result["hostVersion"]);
            Assert.Equal(BudgetChars, Convert.ToInt32(result["budgetChars"]));
        }

        [Fact]
        public void プロトコル番号は1である()
        {
            Assert.Equal(1, JsonRpcConnection.Protocol);
        }

        [Fact]
        public void 要求処理の時間の上限の既定は120秒である()
        {
            Assert.Equal(TimeSpan.FromSeconds(120), JsonRpcConnection.DefaultRequestTimeout);
        }

        [Theory]
        [InlineData("ping")]
        [InlineData("unknown")]
        [InlineData("")]
        public void ハンドシェイクの前の他の要求は拒んで切断する(string method)
        {
            // メソッドの検索より先に判定するので、既知・未知・空のいずれでも同じ扱いになる。
            IList<IDictionary<string, object>> responses =
                Exchange(CreateConnection(new McpMethodTable()), Request(1, method), Request(2, "ping"));

            Assert.Single(responses);
            Assert.Equal(JsonRpcErrorCodes.HandshakeRequired, ErrorCodeOf(responses[0]));
            Assert.Equal(1, Convert.ToInt32(IdOf(responses[0])));
        }

        [Fact]
        public void ハンドシェイクの前の引数不正も引数の検査より先に拒んで切断する()
        {
            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(new McpMethodTable()), RequestWithArrayParams(1, "ping"), Request(2, "ping"));

            Assert.Single(responses);
            Assert.Equal(JsonRpcErrorCodes.HandshakeRequired, ErrorCodeOf(responses[0]));
        }

        [Fact]
        public void ハンドシェイクの前の構造不正は要求構造の判定が先で接続を保つ()
        {
            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(new McpMethodTable()), "{\"id\":1,\"method\":\"ping\"}", Handshake());

            // 構造不正は接続を保つので、続けて送ったハンドシェイクにも応答する。
            Assert.Equal(2, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, ErrorCodeOf(responses[0]));
            Assert.NotNull(ResultOf(responses[1]));
        }

        [Fact]
        public void プロトコル番号が合わないハンドシェイクは拒んで切断する()
        {
            IList<IDictionary<string, object>> responses =
                Exchange(CreateConnection(new McpMethodTable()), Handshake(1, 2), Request(2, "ping"));

            Assert.Single(responses);
            Assert.Equal(JsonRpcErrorCodes.ProtocolMismatch, ErrorCodeOf(responses[0]));
            Assert.Equal(1, Convert.ToInt32(IdOf(responses[0])));
        }

        [Theory]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\"}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\",\"params\":{}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\",\"params\":{\"protocol\":\"1\"}}")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\",\"params\":[1]}")]
        public void 数値のプロトコル番号を伴わないハンドシェイクは引数不正として接続を保つ(string request)
        {
            IList<IDictionary<string, object>> responses =
                Exchange(CreateConnection(new McpMethodTable()), request, Handshake(2, 1));

            Assert.Equal(2, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.InvalidParams, ErrorCodeOf(responses[0]));
            Assert.NotNull(ResultOf(responses[1]));
        }

        [Fact]
        public void ハンドシェイク済みの接続で再びハンドシェイクを受けても同じ内容を返す()
        {
            IList<IDictionary<string, object>> responses =
                Exchange(CreateConnection(new McpMethodTable()), Handshake(1, 1), Handshake(7, 1), Request(8, "ping"));

            Assert.Equal(3, responses.Count);

            IDictionary<string, object> second =
                Assert.IsAssignableFrom<IDictionary<string, object>>(ResultOf(responses[1]));
            Assert.Equal(7, Convert.ToInt32(IdOf(responses[1])));
            Assert.Equal(JsonRpcConnection.Protocol, Convert.ToInt32(second["protocol"]));
            Assert.Equal(HostVersion, second["hostVersion"]);
            Assert.Equal(BudgetChars, Convert.ToInt32(second["budgetChars"]));

            // 状態は変わらないので、続く要求が最初の要求として扱われることはない。
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void ハンドシェイクの成否は接続をまたいで持ち越さない()
        {
            JsonRpcConnection connection = CreateConnection(new McpMethodTable());

            IList<IDictionary<string, object>> first = Exchange(connection, Handshake(), Request(2, "ping"));
            Assert.Equal(2, first.Count);
            Assert.NotNull(ResultOf(first[0]));
            Assert.Equal("pong", ResultOf(first[1]));

            IList<IDictionary<string, object>> second = Exchange(connection, Request(1, "ping"));

            Assert.Single(second);
            Assert.Equal(JsonRpcErrorCodes.HandshakeRequired, ErrorCodeOf(second[0]));
        }

        [Fact]
        public void pingは決まった文字列を返す()
        {
            IList<IDictionary<string, object>> responses =
                Exchange(CreateConnection(new McpMethodTable()), Handshake(), Request(2, "ping"));

            Assert.Equal(2, responses.Count);
            Assert.Equal("pong", ResultOf(responses[1]));
            Assert.Equal(2, Convert.ToInt32(IdOf(responses[1])));
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("")]
        public void 対応する処理が無いメソッドは未知として接続を保つ(string method)
        {
            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(new McpMethodTable()), Handshake(), Request(2, method), Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.MethodNotFound, ErrorCodeOf(responses[1]));
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 未知のメソッドは引数の検査より先に判定する()
        {
            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(new McpMethodTable()), Handshake(), RequestWithArrayParams(2, "unknown"));

            Assert.Equal(2, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.MethodNotFound, ErrorCodeOf(responses[1]));
        }

        [Fact]
        public void 引数がオブジェクトでない要求は引数不正として接続を保つ()
        {
            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(new McpMethodTable()), Handshake(), RequestWithArrayParams(2, "ping"), Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.InvalidParams, ErrorCodeOf(responses[1]));
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 処理が引数不正を投げたら説明を添えて引数不正として返す()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("reject", context => throw new InvalidParamsException("index が足りない。"));

            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(methods), Handshake(), Request(2, "reject"), Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.InvalidParams, ErrorCodeOf(responses[1]));
            Assert.Contains("index が足りない。", ErrorMessageOf(responses[1]));
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 処理には引数とUIへの委譲と応答サイズ予算を渡す()
        {
            int observedBudget = 0;
            IUiInvoker observedUi = null;
            bool dispatched = false;

            McpMethodTable methods = new McpMethodTable();
            methods.Add("echo", context =>
            {
                observedBudget = context.BudgetChars;
                observedUi = context.Ui;
                dispatched = context.Ui.TryInvokeOnUi(() => { });
                return context.Params["value"];
            });

            InlineInvoker ui = new InlineInvoker();
            IList<IDictionary<string, object>> responses = ExchangeWith(
                CreateConnection(methods),
                ui,
                Handshake(),
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"echo\",\"params\":{\"value\":\"あ\"}}");

            Assert.Equal(2, responses.Count);
            Assert.Equal("あ", ResultOf(responses[1]));
            Assert.Equal(BudgetChars, observedBudget);
            Assert.Same(ui, observedUi);
            Assert.True(dispatched);
        }

        [Fact]
        public void 引数を省略した要求の引数は空になる()
        {
            IDictionary<string, object> observed = null;

            McpMethodTable methods = new McpMethodTable();
            methods.Add("peek", context =>
            {
                observed = context.Params;
                return "ok";
            });

            Exchange(CreateConnection(methods), Handshake(), Request(2, "peek"));

            Assert.NotNull(observed);
            Assert.Empty(observed);
        }

        [Fact]
        public void 処理が予期しない例外で終わったら内部エラーとして接続を保つ()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("boom", context => throw new InvalidOperationException("処理の失敗"));

            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(methods), Handshake(), Request(2, "boom"), Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.InternalError, ErrorCodeOf(responses[1]));
            Assert.Equal(2, Convert.ToInt32(IdOf(responses[1])));

            // ハンドシェイク済みの状態は保たれる。
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 処理の例外はスタックトレース付きで記録する()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("boom", context => throw new InvalidOperationException("記録される原因"));

            Exchange(CreateConnection(methods), Handshake(), Request(2, "boom"));

            string log = File.ReadAllText(_log.FilePath, Encoding.UTF8);
            Assert.Contains("記録される原因", log);
            Assert.Contains(nameof(InvalidOperationException), log);
            Assert.Contains(nameof(JsonRpcConnectionTests), log);
        }

        [Fact]
        public void 要求の内容は記録しない()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("boom", context => throw new InvalidOperationException("処理の失敗"));

            Exchange(
                CreateConnection(methods),
                Handshake(),
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"boom\",\"params\":{\"value\":\"モデルの秘密\"}}");

            string log = File.ReadAllText(_log.FilePath, Encoding.UTF8);
            // 記録しないのは要求の値であって、閉じた語彙であるメソッド名は含まない。
            Assert.DoesNotContain("モデルの秘密", log);
        }

        [Fact]
        public void 構文不正の本文は拒んで切断する()
        {
            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(new McpMethodTable()), Handshake(), "これはJSONではない", Request(3, "ping"));

            Assert.Equal(2, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.ParseError, ErrorCodeOf(responses[1]));
            Assert.Null(IdOf(responses[1]));
        }

        [Fact]
        public void UTF8として解釈できない本文は拒んで切断する()
        {
            List<byte> input = new List<byte>();
            input.AddRange(Lines(Handshake()));
            input.AddRange(new byte[] { 0x82, 0xA0, (byte)'\n' });
            input.AddRange(Lines(Request(3, "ping")));

            IList<IDictionary<string, object>> responses =
                ExchangeBytes(CreateConnection(new McpMethodTable()), input.ToArray());

            Assert.Equal(2, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.ParseError, ErrorCodeOf(responses[1]));
            Assert.Null(IdOf(responses[1]));
        }

        [Fact]
        public void 構造不正の要求は識別子を返して接続を保つ()
        {
            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(new McpMethodTable()),
                Handshake(),
                "{\"jsonrpc\":\"1.0\",\"id\":2,\"method\":\"ping\"}",
                Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, ErrorCodeOf(responses[1]));
            Assert.Equal(2, Convert.ToInt32(IdOf(responses[1])));
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 上限を超える入力は拒んで切断する()
        {
            // 区切りが来る前に上限を超えるので、識別子は判別できない。
            List<byte> input = new List<byte>();
            input.AddRange(Lines(Handshake()));
            input.AddRange(Utf8WithoutBom.GetBytes(new string('a', 4096)));

            IList<IDictionary<string, object>> responses = ExchangeBytes(
                CreateConnection(new McpMethodTable(), JsonRpcConnection.DefaultRequestTimeout, 256),
                input.ToArray());

            Assert.Equal(2, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.RequestTooLarge, ErrorCodeOf(responses[1]));
            Assert.Null(IdOf(responses[1]));
        }

        [Fact]
        public void 上限を超える応答は捨てて上限超過を返す()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("large", context => new string('a', 4096));

            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(methods, JsonRpcConnection.DefaultRequestTimeout, 1024),
                Handshake(),
                Request(2, "large"),
                Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.ResponseTooLarge, ErrorCodeOf(responses[1]));
            Assert.DoesNotContain("aaaa", ErrorMessageOf(responses[1]));
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 応答の上限は文字数でなくバイト数で測る()
        {
            // 「あ」はUTF-8で3バイト。400文字は400字だが1200バイトで、上限1024を超える。
            McpMethodTable methods = new McpMethodTable();
            methods.Add("wide", context => new string('あ', 400));

            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(methods, JsonRpcConnection.DefaultRequestTimeout, 1024),
                Handshake(),
                Request(2, "wide"),
                Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.ResponseTooLarge, ErrorCodeOf(responses[1]));
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 組み立てられない結果は上限超過でなく内部エラーになる()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("looped", context =>
            {
                Dictionary<string, object> looped = new Dictionary<string, object>();
                looped["self"] = looped;
                return looped;
            });

            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(methods), Handshake(), Request(2, "looped"), Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.InternalError, ErrorCodeOf(responses[1]));
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 時間切れの応答は処理の完了を待たずに返し完了まで次を読まない()
        {
            using (ManualResetEventSlim started = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            using (ExchangeStream stream = new ExchangeStream(
                Lines(Handshake(), Request(2, "slow"), Request(3, "ping"))))
            {
                McpMethodTable methods = new McpMethodTable();
                methods.Add("slow", context =>
                {
                    started.Set();
                    release.Wait(WaitLimit);
                    return "遅れて返る結果";
                });

                JsonRpcConnection connection = CreateConnection(
                    methods, TimeSpan.FromMilliseconds(200), MessageChannel.DefaultMaxMessageBytes);

                Thread worker = new Thread(() => connection.Handle(stream, new InlineInvoker()));
                worker.IsBackground = true;
                worker.Start();

                try
                {
                    Assert.True(started.Wait(WaitLimit));

                    // 処理が返るのを待たずに時間切れの応答が書き出される。
                    Assert.True(stream.WaitForMessages(2, WaitLimit));

                    // その後も処理が終わるまで次の要求は読まない。
                    Assert.False(worker.Join(TimeSpan.FromMilliseconds(300)));
                    Assert.Equal(2, stream.MessageCount);
                }
                finally
                {
                    // 待機中のワーカーがいる状態で破棄しないよう、必ず待機を解除してから抜ける。
                    release.Set();
                    worker.Join(WaitLimit);
                }

                IList<IDictionary<string, object>> responses = stream.ReadResponses();
                Assert.Equal(3, responses.Count);
                Assert.Equal(JsonRpcErrorCodes.RequestTimeout, ErrorCodeOf(responses[1]));
                Assert.Equal(2, Convert.ToInt32(IdOf(responses[1])));

                // 完了後の結果は破棄し、二重に応答しない。
                Assert.Equal("pong", ResultOf(responses[2]));

                // 超過した事実と、どの処理が超過したのかを記録する。
                string log = File.ReadAllText(_log.FilePath, Encoding.UTF8);
                Assert.Contains("処理タイムアウト", log);
                Assert.Contains("slow", log);
            }
        }

        [Fact]
        public void 時間切れの応答を書けなくても処理の完了を待ってから抜ける()
        {
            using (ManualResetEventSlim started = new ManualResetEventSlim())
            using (ManualResetEventSlim release = new ManualResetEventSlim())
            using (ExchangeStream stream = new ExchangeStream(Lines(Handshake(), Request(2, "slow"))))
            {
                bool completed = false;
                McpMethodTable methods = new McpMethodTable();
                methods.Add("slow", context =>
                {
                    started.Set();
                    release.Wait(WaitLimit);
                    completed = true;
                    return "遅れて返る結果";
                });

                // ハンドシェイクの応答だけ通し、時間切れの応答は書けなくする。
                stream.FailWritesAfter(1);

                JsonRpcConnection connection = CreateConnection(
                    methods, TimeSpan.FromMilliseconds(200), MessageChannel.DefaultMaxMessageBytes);

                Thread worker = new Thread(() =>
                {
                    try
                    {
                        connection.Handle(stream, new InlineInvoker());
                    }
                    catch (IOException)
                    {
                    }
                });
                worker.IsBackground = true;
                worker.Start();

                try
                {
                    Assert.True(started.Wait(WaitLimit));

                    // 応答を書けなくても、処理を走らせたまま抜けない。
                    Assert.False(worker.Join(TimeSpan.FromMilliseconds(400)));
                }
                finally
                {
                    release.Set();
                    worker.Join(WaitLimit);
                }

                Assert.True(completed);
            }
        }

        [Theory]
        [InlineData("99999999999999999999")]
        [InlineData("1e300")]
        [InlineData("1.4")]
        public void 桁あふれする番号のハンドシェイクも不一致として応答する(string protocol)
        {
            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(new McpMethodTable()),
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"handshake\",\"params\":{\"protocol\":" + protocol + "}}",
                Request(2, "ping"));

            Assert.Single(responses);
            Assert.Equal(JsonRpcErrorCodes.ProtocolMismatch, ErrorCodeOf(responses[0]));
        }

        [Fact]
        public void 説明が上限に入らないときもエラーコードだけは返して接続を保つ()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("verbose", context => throw new InvalidParamsException(new string('a', 4096)));

            IList<IDictionary<string, object>> responses = Exchange(
                CreateConnection(methods, JsonRpcConnection.DefaultRequestTimeout, 1024),
                Handshake(),
                Request(2, "verbose"),
                Request(3, "ping"));

            Assert.Equal(3, responses.Count);
            Assert.Equal(JsonRpcErrorCodes.InvalidParams, ErrorCodeOf(responses[1]));
            Assert.Equal(string.Empty, ErrorMessageOf(responses[1]));
            Assert.Equal("pong", ResultOf(responses[2]));
        }

        [Fact]
        public void 同じ名前の処理は二度登録できない()
        {
            McpMethodTable methods = new McpMethodTable();
            methods.Add("same", context => null);

            Assert.Throws<ArgumentException>(() => methods.Add("same", context => null));
        }

        [Theory]
        [InlineData("handshake")]
        [InlineData("ping")]
        public void 基盤メソッドの名前は登録できない(string name)
        {
            McpMethodTable methods = new McpMethodTable();

            Assert.Throws<ArgumentException>(() => methods.Add(name, context => null));
        }

        [Fact]
        public void 基盤メソッドの名前を数え上げられる()
        {
            Assert.Equal(2, JsonRpcConnection.BaseMethodNames.Count);
            Assert.Contains("handshake", JsonRpcConnection.BaseMethodNames);
            Assert.Contains("ping", JsonRpcConnection.BaseMethodNames);
        }

        /// <summary>UIスレッドを持たないため、委譲された処理はその場で実行する。</summary>
        private sealed class InlineInvoker : IUiInvoker
        {
            public bool TryInvokeOnUi(Action action)
            {
                action();
                return true;
            }
        }

        /// <summary>読み取り用の入力と書き出し先を別に持ち、書き終えたメッセージの件数を数えるストリーム。</summary>
        private sealed class ExchangeStream : Stream
        {
            private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

            private readonly object _gate = new object();
            private readonly MemoryStream _input;
            private readonly MemoryStream _output = new MemoryStream();

            private int _messageCount;
            private int _writesBeforeFailure = int.MaxValue;

            public ExchangeStream(byte[] input)
            {
                _input = new MemoryStream(input);
            }

            /// <summary>区切りまで書き終えたメッセージの件数。書き出しの粒度には依らない。</summary>
            public int MessageCount
            {
                get
                {
                    lock (_gate)
                    {
                        return _messageCount;
                    }
                }
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            /// <summary>指定の回数だけ書き出したあと、以後の書き出しを失敗させる。</summary>
            public void FailWritesAfter(int writes)
            {
                lock (_gate)
                {
                    _writesBeforeFailure = writes;
                }
            }

            /// <summary>書き終えたメッセージが指定の件数に達するまで待つ。</summary>
            public bool WaitForMessages(int count, TimeSpan limit)
            {
                Stopwatch elapsed = Stopwatch.StartNew();
                lock (_gate)
                {
                    while (_messageCount < count)
                    {
                        TimeSpan remaining = limit - elapsed.Elapsed;
                        if (remaining <= TimeSpan.Zero)
                        {
                            return false;
                        }

                        Monitor.Wait(_gate, remaining);
                    }

                    return true;
                }
            }

            /// <summary>書き出された応答を1件ずつ解いて返す。</summary>
            public IList<IDictionary<string, object>> ReadResponses()
            {
                List<IDictionary<string, object>> responses = new List<IDictionary<string, object>>();
                byte[] written;
                lock (_gate)
                {
                    written = _output.ToArray();
                }

                foreach (string line in Utf8WithoutBom.GetString(written).Split('\n'))
                {
                    if (line.Length > 0)
                    {
                        responses.Add((IDictionary<string, object>)Serializer.DeserializeObject(line));
                    }
                }

                return responses;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _input.Read(buffer, offset, count);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                lock (_gate)
                {
                    if (_writesBeforeFailure <= 0)
                    {
                        throw new IOException("書き出しに失敗した。");
                    }

                    _writesBeforeFailure--;
                    _output.Write(buffer, offset, count);
                    for (int index = offset; index < offset + count; index++)
                    {
                        if (buffer[index] == (byte)'\n')
                        {
                            _messageCount++;
                        }
                    }

                    Monitor.PulseAll(_gate);
                }
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _input.Dispose();
                    _output.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
