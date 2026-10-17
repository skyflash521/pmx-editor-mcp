using System;
using System.Threading;

namespace PmxEditorMcp.Tests
{
    internal sealed class ModelStamps
    {
        public int Updates { get; set; }

        public int Moves { get; set; }

        public int Undos { get; set; }

        public int Redos { get; set; }

        public string Identity { get; set; }

        public bool Failing { get; set; }

        public ModelStamp Current()
        {
            if (Failing)
            {
                throw new InvalidOperationException("印を読めない。");
            }

            return new ModelStamp(Updates, Moves, Undos, Redos, Identity);
        }
    }

    internal sealed class DispatchedInvoker : IUiInvoker
    {
        private readonly IUiDispatcher _dispatcher;

        public DispatchedInvoker(IUiDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public UiInvocation TryInvokeOnUi(Action action)
        {
            IAsyncResult pending = _dispatcher.Begin(action);
            _dispatcher.Wait(pending, TimeSpan.FromSeconds(1));
            _dispatcher.End(pending);

            return UiInvocation.Done;
        }
    }

    internal sealed class ImmediateDispatcher : IUiDispatcher
    {
        public IAsyncResult Begin(Action action)
        {
            Done done = new Done();
            try
            {
                action();
            }
            catch (Exception exception)
            {
                done.Failure = exception;
            }

            return done;
        }

        public bool Wait(IAsyncResult pending, TimeSpan limit)
        {
            return pending.IsCompleted;
        }

        public void End(IAsyncResult pending)
        {
            Exception failure = ((Done)pending).Failure;
            if (failure != null)
            {
                throw failure;
            }
        }

        private sealed class Done : IAsyncResult
        {
            public Exception Failure { get; set; }

            public bool IsCompleted
            {
                get { return true; }
            }

            public WaitHandle AsyncWaitHandle
            {
                get { return new ManualResetEvent(true); }
            }

            public object AsyncState
            {
                get { return null; }
            }

            public bool CompletedSynchronously
            {
                get { return true; }
            }
        }
    }
}
