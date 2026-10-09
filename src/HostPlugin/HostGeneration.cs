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

        private readonly IModalWindowProbe _modals;

        private readonly TimeSpan _modalCheckInterval;

        private readonly object _pending = new object();

        private IAsyncResult _running;

        private readonly HashSet<NamedPipeServerStream> _pipes =
            new HashSet<NamedPipeServerStream>();

        private readonly List<Thread> _workers = new List<Thread>();

        private volatile bool _stopRequested;

        private int _connections;

        /// <summary>
        /// <paramref name="modals"/> はUIスレッドが進まないときにその原因を見るもの、
        /// <paramref name="modalCheckInterval"/> はそれを見直す間隔である。
        /// </summary>
        internal HostGeneration(
            IUiDispatcher uiDispatcher,
            IModalWindowProbe modals,
            TimeSpan modalCheckInterval)
        {
            if (uiDispatcher == null)
            {
                throw new ArgumentNullException(nameof(uiDispatcher));
            }

            if (modals == null)
            {
                throw new ArgumentNullException(nameof(modals));
            }

            if (modalCheckInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(modalCheckInterval), "見直す間隔は正の長さで与える。");
            }

            _uiDispatcher = uiDispatcher;
            _modals = modals;
            _modalCheckInterval = modalCheckInterval;
        }

        public bool IsStopRequested => _stopRequested;

        /// <summary>
        /// この稼働世代が開いているパイプインスタンス。待受中のものと接続中のものの両方を持つ。
        /// ホストの排他ロックの下でだけ触る。
        /// </summary>
        internal ICollection<NamedPipeServerStream> Pipes
        {
            get { return _pipes; }
        }

        internal Thread ServerThread { get; set; }

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

        internal void AddWorker(Thread worker)
        {
            lock (_workers)
            {
                _workers.Add(worker);
            }
        }

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

        internal void NoteConnected()
        {
            Interlocked.Increment(ref _connections);
        }

        internal void NoteDisconnected()
        {
            Interlocked.Decrement(ref _connections);
        }

        /// <summary>
        /// UIスレッドで実行する。停止した稼働世代では実行しない。人の応答を待つ表示でUIスレッドが
        /// 進まないときは、待ち続けずにそのダイアログのタイトルと本文を持って戻る。
        /// </summary>
        public UiInvocation TryInvokeOnUi(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (_stopRequested)
            {
                return UiInvocation.Declined;
            }

            bool executed = false;
            IAsyncResult pending;

            lock (_pending)
            {
                if (_running != null)
                {
                    if (!_uiDispatcher.Wait(_running, TimeSpan.Zero))
                    {
                        return UiInvocation.NotStarted(Standing());
                    }

                    Discard(_running);
                    _running = null;
                }

                pending = _uiDispatcher.Begin(() =>
                {
                    if (_stopRequested)
                    {
                        return;
                    }

                    executed = true;
                    action();
                });
                _running = pending;
            }

            while (!_uiDispatcher.Wait(pending, _modalCheckInterval))
            {
                string shown = _modals.TryDescribe();
                if (shown != null)
                {
                    return UiInvocation.Blocked(Shown(shown));
                }
            }

            lock (_pending)
            {
                if (ReferenceEquals(_running, pending))
                {
                    _running = null;
                    _uiDispatcher.End(pending);
                }
            }

            return executed ? UiInvocation.Done : UiInvocation.Declined;
        }

        /// <summary>置いたまま戻った委譲の後始末。終わったことを確かめてから呼ぶ。</summary>
        private void Discard(IAsyncResult pending)
        {
            try
            {
                _uiDispatcher.End(pending);
            }
            catch (Exception)
            {
            }
        }

        private static string Shown(string shown)
        {
            return "エディタが人の応答を待つ表示を出していて進められない。表示へ答えると進む。"
                + "この呼び出しが実行されたかどうかは確かめられない。表示が消えてから対象を読み直し、"
                + "いまの状態を確かめる。表示が消えたかは " + EditorPrompt.ToolName
                + " で確かめる。表示: " + shown;
        }

        private string Standing()
        {
            string shown = _modals.TryDescribe();
            if (shown != null)
            {
                return Shown(shown);
            }

            return "前の呼び出しがUIスレッドでまだ終わっていない。この呼び出しは始めていない。";
        }

        internal void RequestStop()
        {
            _stopRequested = true;
        }
    }
}
