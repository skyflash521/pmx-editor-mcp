using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>
    /// ブリッジの相手役。名前付きパイプで待ち受け、受け取った要求ごとに、あらかじめ積んだ
    /// 応答を順に返す。応答の代わりに切断させることもでき、契約に沿わない本文もそのまま
    /// 書けるので、ブリッジ側の検証を外から確かめられる。
    /// </summary>
    public sealed class FakeHost : IDisposable
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly List<Func<string, byte[]>> _replies = new List<Func<string, byte[]>>();
        private readonly List<string> _requests = new List<string>();
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private readonly object _gate = new object();

        private NamedPipeServerStream _pipe;
        private Task _listening;

        /// <summary>相手が接続するパイプの名前。テストごとに重ならない名前を作る。</summary>
        public string PipeName { get; } = "pmx-editor-mcp-test-" + Guid.NewGuid().ToString("N");

        /// <summary>これまでに受け取った要求の本文。</summary>
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
            _replies.Add(_ => Utf8WithoutBom.GetBytes(message + "\n"));
            return this;
        }

        /// <summary>受け取った要求に対して、生のバイト列をそのまま返す応答を積む。</summary>
        public FakeHost ReplyBytes(byte[] payload)
        {
            _replies.Add(_ => payload);
            return this;
        }

        /// <summary>受け取った要求から本文を組み立てて返す応答を積む。</summary>
        public FakeHost Reply(Func<string, string> compose)
        {
            _replies.Add(request => Utf8WithoutBom.GetBytes(compose(request) + "\n"));
            return this;
        }

        /// <summary>受け取った要求に応答せず、そのまま切断する。</summary>
        public FakeHost Disconnect()
        {
            _replies.Add(_ => null);
            return this;
        }

        /// <summary>
        /// 背景で待受を始める。積んだ応答は接続をまたいで通し番号で使うので、繋ぎ直しを含む
        /// 筋書きも1つの並びとして書ける。
        /// </summary>
        public FakeHost Start()
        {
            _listening = Task.Run(ListenAsync);
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
                        return;
                    }

                    _pipe = pipe;
                }

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
            while (!_stopping.IsCancellationRequested)
            {
                string request = await ReadLineAsync(pipe).ConfigureAwait(false);
                if (request == null)
                {
                    return;
                }

                int index;
                lock (_gate)
                {
                    _requests.Add(request);
                    index = _requests.Count - 1;
                }

                if (index >= _replies.Count)
                {
                    return;
                }

                byte[] payload = _replies[index](request);
                if (payload == null)
                {
                    return;
                }

                await pipe.WriteAsync(payload.AsMemory(), _stopping.Token).ConfigureAwait(false);
                await pipe.FlushAsync(_stopping.Token).ConfigureAwait(false);
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
