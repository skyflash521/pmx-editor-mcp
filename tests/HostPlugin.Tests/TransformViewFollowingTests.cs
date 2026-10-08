using System;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class TransformViewFollowingTests : IDisposable
    {
        private readonly string _root;

        private readonly HostLog _log;

        public TransformViewFollowingTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-following-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void AModelUpdatedDuringTheActionRefreshesTheOpenTransformView()
        {
            Updates updates = new Updates();
            FakeTransformView view = new FakeTransformView { Visible = true };

            Run(Following(updates, () => view), () => updates.Count++);

            Assert.Equal(1, view.Updates);
        }

        [Fact]
        public void AnActionThatUpdatesNoModelLeavesTheTransformViewAlone()
        {
            FakeTransformView view = new FakeTransformView { Visible = true };

            Run(Following(new Updates(), () => view), () => { });

            Assert.Equal(0, view.Updates);
        }

        [Fact]
        public void AnActionThatRefreshedTheTransformViewItselfIsNotRefreshedAgain()
        {
            Updates updates = new Updates();
            FakeTransformView view = new FakeTransformView { Visible = true };

            Run(
                Following(updates, () => view),
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
            TransformViewFollowing following = Following(updates, () => view);

            IAsyncResult pending = following.Begin(() =>
            {
                updates.Count++;
                throw new InvalidOperationException("落ちた");
            });

            Assert.Throws<InvalidOperationException>(() => following.End(pending));
            Assert.Equal(0, view.Updates);
        }

        [Fact]
        public void ARefreshThatFailsAfterASuccessfulActionIsRecordedAndTheCallStillSucceeds()
        {
            Updates updates = new Updates();
            FakeTransformView view = new FakeTransformView { Visible = true, FailsToUpdate = true };

            Run(Following(updates, () => view), () => updates.Count++);

            Assert.Contains("TransformView を読み直せない。", Logged());
        }

        [Fact]
        public void AnActionThatFailsIsReportedAsItsOwnFailureAndNotRecordedAsARefreshFailure()
        {
            Updates updates = new Updates();
            FakeTransformView view = new FakeTransformView { Visible = true, FailsToUpdate = true };
            TransformViewFollowing following = Following(updates, () => view);

            IAsyncResult pending = following.Begin(() =>
            {
                updates.Count++;
                throw new InvalidOperationException("落ちた");
            });

            InvalidOperationException thrown =
                Assert.Throws<InvalidOperationException>(() => following.End(pending));
            Assert.Equal("落ちた", thrown.Message);
            Assert.DoesNotContain("TransformView を読み直せない。", Logged());
        }

        private TransformViewFollowing Following(IModelUpdates updates, Func<object> transformView)
        {
            return new TransformViewFollowing(new InlineDispatcher(), _log, updates, transformView);
        }

        private string Logged()
        {
            return File.Exists(_log.FilePath) ? File.ReadAllText(_log.FilePath, Encoding.UTF8) : string.Empty;
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

            public int HistoryMoves { get; set; }
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
