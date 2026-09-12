using System;
using System.Threading;

namespace PmxEditorMcp.Tests
{
    /// <summary>終わっている委譲。UIスレッドを持たない題材で、その場で実行した結果を表す。</summary>
    internal sealed class FinishedPending : IAsyncResult
    {
        private readonly ManualResetEvent _done = new ManualResetEvent(true);

        public object AsyncState
        {
            get { return null; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { return _done; }
        }

        public bool CompletedSynchronously
        {
            get { return true; }
        }

        public bool IsCompleted
        {
            get { return true; }
        }
    }

    /// <summary>まだ終わらない委譲。UIスレッドが進まない並びを作るのに使う。</summary>
    internal sealed class UnfinishedPending : IAsyncResult
    {
        private readonly ManualResetEvent _done = new ManualResetEvent(false);

        public object AsyncState
        {
            get { return null; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { return _done; }
        }

        public bool CompletedSynchronously
        {
            get { return false; }
        }

        public bool IsCompleted
        {
            get { return false; }
        }

        /// <summary>その委譲が終わったことにする。</summary>
        public void Finish()
        {
            _done.Set();
        }
    }
}
