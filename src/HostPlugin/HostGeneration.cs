using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;

namespace PmxEditorMcp
{
    /// <summary>
    /// ホストの稼働世代。受付停止の有無・公開中のパイプインスタンス・IPCサーバースレッドを
    /// ひとまとめに持つ。停止手順も状態判定もUIディスパッチの可否もこの単位で決まり、
    /// 停止した稼働世代での禁止は新しい稼働世代へ持ち越さない。
    /// </summary>
    public sealed class HostGeneration : IUiInvoker
    {
        private readonly IUiDispatcher _uiDispatcher;

        private readonly HashSet<NamedPipeServerStream> _pipes =
            new HashSet<NamedPipeServerStream>();

        private readonly List<Thread> _workers = new List<Thread>();

        private volatile bool _stopRequested;

        private int _connections;

        /// <summary>稼働世代はホストだけが作る。</summary>
        internal HostGeneration(IUiDispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
        }

        /// <summary>この稼働世代で受付を止めたかどうか。</summary>
        public bool IsStopRequested => _stopRequested;

        /// <summary>
        /// この稼働世代が開いているパイプインスタンス。待受中のものと接続中のものの両方を持つ。
        /// ホストの排他ロックの下でだけ触る。
        /// </summary>
        internal ICollection<NamedPipeServerStream> Pipes
        {
            get { return _pipes; }
        }

        /// <summary>この稼働世代の待受を担うスレッド。</summary>
        internal Thread ServerThread { get; set; }

        /// <summary>
        /// この稼働世代の仕事がまだ残っているか。待受のスレッドだけでなく、接続を処理している
        /// スレッドも見る——待受が終わっても要求の処理が残っていれば、まだ止まり切っていない。
        /// </summary>
        internal bool HasLiveThreads
        {
            get
            {
                if (ServerThread != null && ServerThread.IsAlive)
                {
                    return true;
                }

                lock (_workers)
                {
                    foreach (Thread worker in _workers)
                    {
                        if (worker.IsAlive)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        /// <summary>接続を処理するスレッドを覚える。</summary>
        internal void AddWorker(Thread worker)
        {
            lock (_workers)
            {
                _workers.Add(worker);
            }
        }

        /// <summary>接続を処理するスレッドを忘れる。</summary>
        internal void RemoveWorker(Thread worker)
        {
            lock (_workers)
            {
                _workers.Remove(worker);
            }
        }

        /// <summary>この稼働世代がクライアントと接続中かどうか。1本でも繋がっていれば真。</summary>
        internal bool IsClientConnected
        {
            get { return Volatile.Read(ref _connections) > 0; }
        }

        /// <summary>接続が1本増えた。</summary>
        internal void NoteConnected()
        {
            Interlocked.Increment(ref _connections);
        }

        /// <summary>接続が1本減った。</summary>
        internal void NoteDisconnected()
        {
            Interlocked.Decrement(ref _connections);
        }

        /// <summary>
        /// UIスレッドで実行する。停止した稼働世代では実行せず偽を返す。
        /// </summary>
        public bool TryInvokeOnUi(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (_stopRequested)
            {
                return false;
            }

            bool executed = false;
            _uiDispatcher.Invoke(() =>
            {
                // 委譲が実際に走るのはUIスレッドが空くときで、その間に停止手順が終わっていることがある。
                if (_stopRequested)
                {
                    return;
                }

                executed = true;
                action();
            });

            return executed;
        }

        /// <summary>この稼働世代の受付を止める。</summary>
        internal void RequestStop()
        {
            _stopRequested = true;
        }
    }
}
