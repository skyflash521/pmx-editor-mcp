using System;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class TransformViewFollowingTests
    {
        [Fact]
        public void AModelUpdatedDuringTheActionRefreshesTheOpenTransformView()
        {
            Updates updates = new Updates();
            FakeTransformView view = new FakeTransformView { Visible = true };

            Run(new TransformViewFollowing(new InlineDispatcher(), updates, () => view), () => updates.Count++);

            Assert.Equal(1, view.Updates);
        }

        [Fact]
        public void AnActionThatUpdatesNoModelLeavesTheTransformViewAlone()
        {
            FakeTransformView view = new FakeTransformView { Visible = true };

            Run(new TransformViewFollowing(new InlineDispatcher(), new Updates(), () => view), () => { });

            Assert.Equal(0, view.Updates);
        }

        [Fact]
        public void AnActionThatRefreshedTheTransformViewItselfIsNotRefreshedAgain()
        {
            Updates updates = new Updates();
            FakeTransformView view = new FakeTransformView { Visible = true };

            Run(
                new TransformViewFollowing(new InlineDispatcher(), updates, () => view),
                () =>
                {
                    updates.Count++;
                    TransformViewSync.Refresh(view);
                });

            Assert.Equal(1, view.Updates);
        }

        [Fact]
        public void AnActionThatFailsIsReportedWithoutRefreshing()
        {
            Updates updates = new Updates();
            FakeTransformView view = new FakeTransformView { Visible = true };
            TransformViewFollowing following = new TransformViewFollowing(new InlineDispatcher(), updates, () => view);

            IAsyncResult pending = following.Begin(() =>
            {
                updates.Count++;
                throw new InvalidOperationException("落ちた");
            });

            Assert.Throws<InvalidOperationException>(() => following.End(pending));
            Assert.Equal(0, view.Updates);
        }

        private static void Run(IUiDispatcher dispatcher, Action action)
        {
            IAsyncResult pending = dispatcher.Begin(action);
            Assert.True(dispatcher.Wait(pending, TimeSpan.FromSeconds(1)));
            dispatcher.End(pending);
        }

        private sealed class Updates : IModelUpdates
        {
            public int Count { get; set; }
        }

        private sealed class InlineDispatcher : IUiDispatcher
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
}
