using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class UndoSuppressionTests : IDisposable
    {
        private readonly string _directory;
        private readonly HostLog _log;

        public UndoSuppressionTests()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-undo-" + Guid.NewGuid().ToString("N"));
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
        }

        [Fact]
        public void TheEditRunsBetweenTheLockAndTheUnlock()
        {
            StubLock target = new StubLock();
            UndoSuppression suppression = new UndoSuppression(_log);

            bool left = suppression.Run(target, () => target.Note("edit"));

            Assert.False(left);
            Assert.Equal(new[] { "lock", "edit", "unlock" }, target.Calls);
            Assert.False(suppression.HasLeftover);
        }

        [Fact]
        public void TheUnlockRunsEvenWhenTheEditThrows()
        {
            StubLock target = new StubLock();
            UndoSuppression suppression = new UndoSuppression(_log);

            Assert.Throws<InvalidOperationException>(
                () => suppression.Run(target, () => { throw new InvalidOperationException("編集の失敗。"); }));

            Assert.Equal(new[] { "lock", "unlock" }, target.Calls);
            Assert.False(suppression.HasLeftover);
        }

        [Fact]
        public void AFailedUnlockIsTriedOnceMore()
        {
            StubLock target = new StubLock { UnlockFailures = 1 };
            UndoSuppression suppression = new UndoSuppression(_log);

            bool left = suppression.Run(target, () => { });

            Assert.False(left);
            Assert.False(suppression.HasLeftover);
            Assert.Equal(new[] { "lock", "unlock", "unlock" }, target.Calls);
        }

        [Fact]
        public void AnUnlockThatKeepsFailingIsRemembered()
        {
            StubLock target = new StubLock { UnlockFailures = int.MaxValue };
            UndoSuppression suppression = new UndoSuppression(_log);

            bool left = suppression.Run(target, () => { });

            Assert.True(left);
            Assert.True(suppression.HasLeftover);
            Assert.Equal(new[] { "lock", "unlock", "unlock" }, target.Calls);
        }

        [Fact]
        public void TheLeftoverIsClearedWhenItIsRecovered()
        {
            StubLock target = new StubLock { UnlockFailures = 2 };
            UndoSuppression suppression = new UndoSuppression(_log);
            suppression.Run(target, () => { });

            Assert.True(suppression.TryRecover(target));
            Assert.False(suppression.HasLeftover);
        }

        [Fact]
        public void AFailedRecoveryKeepsTheLeftover()
        {
            StubLock target = new StubLock { UnlockFailures = int.MaxValue };
            UndoSuppression suppression = new UndoSuppression(_log);
            suppression.Run(target, () => { });
            target.Calls.Clear();

            Assert.False(suppression.TryRecover(target));
            Assert.True(suppression.HasLeftover);
            Assert.Equal(new[] { "unlock" }, target.Calls);
        }

        [Fact]
        public void ALeftoverThatArisesWhileRecoveringIsKept()
        {
            UndoSuppression suppression = new UndoSuppression(_log);
            StubLock failing = new StubLock { UnlockFailures = int.MaxValue };
            suppression.Run(failing, () => { });
            StubLock recovering = new StubLock
            {
                OnUnlock = () => suppression.Run(failing, () => { }),
            };

            Assert.False(suppression.TryRecover(recovering));
            Assert.True(suppression.HasLeftover);
            Assert.Equal(1, Occurrences(Log(), "止まったまま残った"));
            Assert.Equal(0, Occurrences(Log(), "回収: 成功"));
        }

        [Fact]
        public void ARecoveryWhileAnotherIsRunningSaysItIsStillLeft()
        {
            UndoSuppression suppression = new UndoSuppression(_log);
            StubLock failing = new StubLock { UnlockFailures = int.MaxValue };
            suppression.Run(failing, () => { });
            bool duringRecovery = true;
            StubLock recovering = new StubLock
            {
                OnUnlock = () => duringRecovery = suppression.TryRecover(new StubLock()),
            };

            Assert.True(suppression.TryRecover(recovering));
            Assert.False(duringRecovery);
        }

        [Fact]
        public void ALockThatFailsIsNotUnlocked()
        {
            StubLock target = new StubLock { FailLock = true };
            UndoSuppression suppression = new UndoSuppression(_log);

            Assert.Throws<InvalidOperationException>(() => suppression.Run(target, () => { }));

            Assert.Equal(new[] { "lock" }, target.Calls);
            Assert.False(suppression.HasLeftover);
        }

        [Fact]
        public void RecoveringWithoutALeftoverDoesNothing()
        {
            StubLock target = new StubLock();
            UndoSuppression suppression = new UndoSuppression(_log);

            Assert.True(suppression.TryRecover(target));
            Assert.Empty(target.Calls);
        }

        [Fact]
        public void TheLeftoverIsRecordedOnlyOnce()
        {
            StubLock target = new StubLock { UnlockFailures = int.MaxValue };
            UndoSuppression suppression = new UndoSuppression(_log);

            suppression.Run(target, () => { });
            suppression.Run(target, () => { });

            Assert.Equal(1, Occurrences(Log(), "止まったまま残った"));
        }

        [Fact]
        public void TheInputsAreRequired()
        {
            UndoSuppression suppression = new UndoSuppression(_log);

            Assert.Throws<ArgumentNullException>(() => new UndoSuppression(null));
            Assert.Throws<ArgumentNullException>(() => suppression.Run(null, () => { }));
            Assert.Throws<ArgumentNullException>(() => suppression.Run(new StubLock(), null));
            Assert.Throws<ArgumentNullException>(() => suppression.TryRecover(null));
        }

        private string Log()
        {
            return File.ReadAllText(_log.FilePath, Encoding.UTF8);
        }

        private static int Occurrences(string text, string word)
        {
            int count = 0;
            for (int index = text.IndexOf(word, StringComparison.Ordinal);
                index >= 0;
                index = text.IndexOf(word, index + word.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        /// <summary>呼ばれた順を覚え、指定の回数だけ戻すのに失敗する相手。</summary>
        private sealed class StubLock : IUndoLock
        {
            public IList<string> Calls { get; } = new List<string>();

            public int UnlockFailures { get; set; }

            /// <summary>戻すのに合わせて走らせるもの。一度走ったら外す。</summary>
            public Action OnUnlock { get; set; }

            public bool FailLock { get; set; }

            public void Note(string call)
            {
                Calls.Add(call);
            }

            public void Lock()
            {
                Calls.Add("lock");
                if (FailLock)
                {
                    throw new InvalidOperationException("止められない。");
                }
            }

            public void Unlock()
            {
                Calls.Add("unlock");
                if (OnUnlock != null)
                {
                    Action hook = OnUnlock;
                    OnUnlock = null;
                    hook();
                }

                if (UnlockFailures > 0)
                {
                    UnlockFailures--;
                    throw new InvalidOperationException("戻せない。");
                }
            }
        }
    }
}
