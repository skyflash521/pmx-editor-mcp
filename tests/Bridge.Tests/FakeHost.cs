using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>
    /// ブリッジの相手役。名前付きパイプで待ち受け、受け取った要求ごとに、あらかじめ積んだ
    /// 応答を順に返す。応答の代わりに切断させることもでき、契約に沿わない本文もそのまま
    /// 書けるので、ブリッジ側の検証を外から確かめられる。
    ///
    /// 要求の読み取りは応答の組み立てと切り離して先へ進める。応答を返すまで読み取りを止める
    /// 作りだと、相手が応答を待たずに次の要求を送っていても要求の数に現れず、直列化の破れを
    /// 見逃す。
    /// </summary>
    public sealed class FakeHost : IDisposable
    {
        private const string LineFeed = "\n";

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>応答を返さずに接続を保つことを表す本文。</summary>
        private static readonly byte[] NoReply = Array.Empty<byte>();

        private readonly List<Func<string, Task<byte[]>>> _replies = new List<Func<string, Task<byte[]>>>();
        private readonly List<string> _requests = new List<string>();
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private readonly object _gate = new object();

        private readonly TaskCompletionSource<bool> _listeningStarted =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private NamedPipeServerStream _pipe;
        private Task _listening;

        /// <summary>
        /// テストごとに重ならない名前で待ち受ける。この名前は接頭辞に続く部分がプロセスIDに
        /// ならないので、待受の列挙による接続先の発見では拾われない。
        /// </summary>
        public FakeHost()
        {
        }

        /// <summary>待ち受ける名前を選んで生成する。発見の対象にしたい場合に用いる。</summary>
        public FakeHost(string pipeName)
        {
            PipeName = pipeName;
        }

        /// <summary>相手が接続するパイプの名前。</summary>
        public string PipeName { get; } = "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N");

        /// <summary>これまでに受け取った要求の本文。応答を返す前の要求も含む。</summary>
        public IReadOnlyList<string> Requests
        {
            get
            {
                lock (_gate)
                {
                    return _requests.ToArray();
                }
            }
        }

        /// <summary>受け取った要求に対して、本文をそのまま1行として返す応答を積む。</summary>
        public FakeHost Reply(string message)
        {
            _replies.Add(_ => Task.FromResult(Utf8WithoutBom.GetBytes(message + LineFeed)));
            return this;
        }

        /// <summary>受け取った要求に対して、生のバイト列をそのまま返す応答を積む。</summary>
        public FakeHost ReplyBytes(byte[] payload)
        {
            _replies.Add(_ => Task.FromResult(payload));
            return this;
        }

        /// <summary>受け取った要求から本文を組み立てて返す応答を積む。</summary>
        public FakeHost Reply(Func<string, string> compose)
        {
            _replies.Add(request => Task.FromResult(Utf8WithoutBom.GetBytes(compose(request) + LineFeed)));
            return this;
        }

        /// <summary>
        /// 受け取った要求から本文を組み立てて返す応答を積む。組み立てを待たせられるので、
        /// 応答を保留したまま次の呼び出しの振る舞いを確かめられる。組み立てには待受の停止を
        /// 知らせる合図を渡す——渡さないと、保留したまま終わるテストで後始末が止まり、本来の
        /// 失敗が後始末の失敗に覆われる。
        /// </summary>
        public FakeHost ReplyAsync(Func<string, CancellationToken, Task<string>> compose)
        {
            _replies.Add(async request =>
            {
                string message = await compose(request, _stopping.Token).ConfigureAwait(false);
                return Utf8WithoutBom.GetBytes(message + LineFeed);
            });
            return this;
        }

        /// <summary>受け取った要求に応答せず、そのまま切断する。</summary>
        public FakeHost Disconnect()
        {
            _replies.Add(_ => Task.FromResult<byte[]>(null));
            return this;
        }

        /// <summary>
        /// 受け取った要求に応答せず、接続は保ったままにする。相手の待つ上限を確かめるための
        /// 筋書きで、相手が打ち切って閉じれば待受は次の接続へ進める。
        /// </summary>
        public FakeHost Stall()
        {
            _replies.Add(_ => Task.FromResult(NoReply));
            return this;
        }

        /// <summary>
        /// 背景で待受を始める。積んだ応答は接続をまたいで通し番号で使うので、繋ぎ直しを含む
        /// 筋書きも1つの並びとして書ける。
        /// </summary>
        public FakeHost Start()
        {
            _listening = Task.Run(ListenAsync);

            // パイプが公開されるのは背景の待受の中なので、戻った時点ではまだ名前が現れて
            // いないことがある。待ち受けているパイプを列挙して相手を探す側は一度見るだけで
            // 済ませるため、公開を見届けてから戻る。
            if (!_listeningStarted.Task.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new InvalidOperationException("試験用ホストの待受を始められなかった。");
            }

            return this;
        }

        /// <summary>
        /// 待受を止める。公開中のパイプを閉じて待機を解いたうえで、待受が実際に終わるのを
        /// 見届ける——見届けないと、終われなかった待受をテストが気付かずに残す。
        /// </summary>
        public void Dispose()
        {
            _stopping.Cancel();

            lock (_gate)
            {
                _pipe?.Dispose();
            }

            if (_listening != null && !_listening.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new InvalidOperationException("試験用ホストの待受を止められなかった。");
            }

            _stopping.Dispose();
        }

        private async Task ListenAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                lock (_gate)
                {
                    if (_stopping.IsCancellationRequested)
                    {
                        pipe.Dispose();
                        _listeningStarted.TrySetResult(false);
                        return;
                    }

                    _pipe = pipe;
                }

                _listeningStarted.TrySetResult(true);

                try
                {
                    await pipe.WaitForConnectionAsync(_stopping.Token).ConfigureAwait(false);
                    await ServeAsync(pipe).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    // 相手が閉じただけなので、次の接続を待つ。
                }
                catch (ObjectDisposedException)
                {
                    // 停止でパイプを閉じた。
                    return;
                }
                finally
                {
                    lock (_gate)
                    {
                        _pipe = null;
                    }

                    pipe.Dispose();
                }
            }
        }

        private async Task ServeAsync(NamedPipeServerStream pipe)
        {
            Channel<ReceivedRequest> received = Channel.CreateUnbounded<ReceivedRequest>();
            Task reading = ReadRequestsAsync(pipe, received.Writer);

            try
            {
                while (await received.Reader.WaitToReadAsync(_stopping.Token).ConfigureAwait(false))
                {
                    ReceivedRequest request = await received.Reader
                        .ReadAsync(_stopping.Token)
                        .ConfigureAwait(false);

                    if (request.Index >= _replies.Count)
                    {
                        return;
                    }

                    byte[] payload = await _replies[request.Index](request.Body).ConfigureAwait(false);
                    if (payload == null)
                    {
                        return;
                    }

                    if (payload.Length == 0)
                    {
                        continue;
                    }

                    await pipe.WriteAsync(payload.AsMemory(), _stopping.Token).ConfigureAwait(false);
                    await pipe.FlushAsync(_stopping.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                // パイプを閉じないと読み取りが戻らないので、閉じてから終わりを見届ける。
                pipe.Dispose();
                await reading.ConfigureAwait(false);
            }
        }

        private async Task ReadRequestsAsync(Stream pipe, ChannelWriter<ReceivedRequest> writer)
        {
            try
            {
                while (true)
                {
                    string body = await ReadLineAsync(pipe).ConfigureAwait(false);
                    if (body == null)
                    {
                        return;
                    }

                    int index;
                    lock (_gate)
                    {
                        _requests.Add(body);
                        index = _requests.Count - 1;
                    }

                    await writer.WriteAsync(new ReceivedRequest(body, index)).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 停止した。
            }
            catch (IOException)
            {
                // 相手が閉じた。
            }
            catch (ObjectDisposedException)
            {
                // パイプを閉じた。
            }
            finally
            {
                writer.Complete();
            }
        }

        private async Task<string> ReadLineAsync(Stream stream)
        {
            using MemoryStream body = new MemoryStream();
            byte[] buffer = new byte[1];

            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(), _stopping.Token).ConfigureAwait(false);
                if (read <= 0)
                {
                    return null;
                }

                if (buffer[0] == (byte)'\n')
                {
                    return Utf8WithoutBom.GetString(body.GetBuffer(), 0, (int)body.Length);
                }

                body.WriteByte(buffer[0]);
            }
        }

        /// <summary>読み取った要求と、積んだ応答のどれを使うかを決める通し番号。</summary>
        private readonly struct ReceivedRequest
        {
            public ReceivedRequest(string body, int index)
            {
                Body = body;
                Index = index;
            }

            public string Body { get; }

            public int Index { get; }
        }
    }

    /// <summary>試験用のホストへ接続する。</summary>
    public sealed class FakeHostConnector : IHostConnector
    {
        private readonly string _pipeName;

        /// <summary>接続先のパイプ名を与えて生成する。</summary>
        public FakeHostConnector(string pipeName)
        {
            _pipeName = pipeName;
        }

        /// <summary>これまでに接続を開いた回数。繋ぎ直しの有無を外から数えるために持つ。</summary>
        public int ConnectCount { get; private set; }

        /// <summary>生成時に与えられたパイプへ接続する。</summary>
        public async Task<Stream> ConnectAsync(CancellationToken cancellationToken)
        {
            ConnectCount++;

            NamedPipeClientStream pipe = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
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
}
