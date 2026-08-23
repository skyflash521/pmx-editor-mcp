using System;
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

        private volatile bool _stopRequested;
        private volatile bool _clientConnected;

        /// <summary>稼働世代はホストだけが作る。</summary>
        internal HostGeneration(IUiDispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
        }

        /// <summary>この稼働世代で受付を止めたかどうか。</summary>
        public bool IsStopRequested => _stopRequested;

        /// <summary>この稼働世代が公開している待受中のパイプインスタンス。ホストの排他ロックの下でだけ触る。</summary>
        internal NamedPipeServerStream PublishedPipe { get; set; }

        /// <summary>この稼働世代の待受を担うスレッド。</summary>
        internal Thread ServerThread { get; set; }

        /// <summary>この稼働世代がクライアントと接続中かどうか。</summary>
        internal bool IsClientConnected
        {
            get { return _clientConnected; }
            set { _clientConnected = value; }
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
