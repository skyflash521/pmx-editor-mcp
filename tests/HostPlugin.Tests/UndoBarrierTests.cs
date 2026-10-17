using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// Undoの記録まわりの前置きを済ませてからツールを呼ぶ包み。生成したツールと組み立てた
    /// ツールが同じ前置きを通ることを、この包みだけで決める。
    /// </summary>
    public sealed class UndoBarrierTests : IDisposable
    {

        private const int UnlockAttemptsWhenReleasing = 2;

        private readonly string _root;

        private readonly HostLog _log;

        public UndoBarrierTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-barrier-" + Guid.NewGuid().ToString("N"));
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
        public void AToolThatIsNotAskedToSuppressIsCalledAndItsAnswerComesBackUntouched()
        {
            int[] calls = { 0 };

            IDictionary<string, object> envelope = Call(
                Guard(EditKind.DuplicateEdit, calls), Arguments());

            Assert.Equal(1, calls[0]);
            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.False(envelope.ContainsKey(ToolEnvelope.WarningsName));
        }

        [Fact]
        public void AskingToSuppressWithSomethingThatIsNotABooleanIsRefused()
        {
            int[] calls = { 0 };

            IDictionary<string, object> envelope = Call(
                Guard(EditKind.DuplicateEdit, calls),
                Arguments(Given(UndoBarrier.SuppressName, "true")));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Equal(0, calls[0]);
        }

        [Fact]
        public void AskingToSuppressWhilePointingThePmxByHandleIsRefused()
        {
            int[] calls = { 0 };

            IDictionary<string, object> envelope = Call(
                Guard(EditKind.DuplicateEdit, calls),
                Arguments(
                    Given(PmxSession.HandleName, 1),
                    Given(UndoBarrier.SuppressName, true)));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Contains("ハンドルで指した", Message(envelope));
            Assert.Equal(0, calls[0]);
        }

        [Theory]
        [InlineData(EditKind.Read)]
        [InlineData(EditKind.DirectChange)]
        [InlineData(EditKind.ViewSession)]
        public void OnlyTheDuplicateEditKindMayAskToSuppress(EditKind kind)
        {
            int[] calls = { 0 };

            IDictionary<string, object> envelope = Call(
                Guard(kind, calls), Arguments(Given(UndoBarrier.SuppressName, true)));

            Assert.Equal(ToolEnvelope.InvalidArgument, Code(envelope));
            Assert.Equal(0, calls[0]);
        }

        [Fact]
        public void ARecordLeftStoppedIsPutBackAndTheAnswerSaysSo()
        {
            StubLock target = new StubLock();
            UndoSuppression undo = Left(target);
            int[] calls = { 0 };

            IDictionary<string, object> envelope = Call(
                Guard(EditKind.DuplicateEdit, calls, undo, target), Arguments());

            Assert.Equal(1, calls[0]);
            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Contains(UndoGate.RecoveredWarning, Warnings(envelope));
            Assert.False(undo.HasLeftover);
        }

        [Fact]
        public void ARecordThatCannotBePutBackStopsAnEditBeforeItRuns()
        {
            StubLock target = new StubLock();
            UndoSuppression undo = Left(target);
            target.UnlockFailures = 1;
            int[] calls = { 0 };

            IDictionary<string, object> envelope = Call(
                Guard(EditKind.DuplicateEdit, calls, undo, target), Arguments());

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
            Assert.Equal(0, calls[0]);
        }

        [Theory]
        [InlineData(EditKind.Read)]
        [InlineData(EditKind.ViewSession)]
        public void ARecordThatCannotBePutBackStillLetsAReadThroughWithAWarning(EditKind kind)
        {
            StubLock target = new StubLock();
            UndoSuppression undo = Left(target);
            target.UnlockFailures = 1;
            int[] calls = { 0 };

            IDictionary<string, object> envelope = Call(
                Guard(kind, calls, undo, target), Arguments());

            Assert.Equal(1, calls[0]);
            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Contains(UndoGate.LeftoverWarning, Warnings(envelope));
        }

        [Fact]
        public void ARecordPutBackIsToldInTheErrorWhenTheToolItselfRefuses()
        {
            StubLock target = new StubLock();
            UndoSuppression undo = Left(target);
            McpMethod guarded = new UndoBarrier(new UndoRecovery(undo, target))
                .Guard(
                    EditKind.DuplicateEdit,
                    context => ToolEnvelope.Failure(
                        ToolEnvelope.IndexOutOfRange, "並びの外を指している。"));

            IDictionary<string, object> envelope = Call(guarded, Arguments());

            Assert.Equal(ToolEnvelope.IndexOutOfRange, Code(envelope));
            Assert.Contains(UndoGate.RecoveredWarning, Message(envelope));
            Assert.False(envelope.ContainsKey(ToolEnvelope.WarningsName));
        }

        [Fact]
        public void TheInnerCallIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => new UndoBarrier(new UndoRecovery(new UndoSuppression(_log), null))
                    .Guard(EditKind.Read, null));
        }

        private UndoSuppression Left(StubLock target)
        {
            UndoSuppression undo = new UndoSuppression(_log);
            target.UnlockFailures = UnlockAttemptsWhenReleasing;
            undo.Run(target, () => { });
            Assert.True(undo.HasLeftover, "残っていない。");

            return undo;
        }

        private McpMethod Guard(
            EditKind kind, int[] calls, UndoSuppression undo = null, IUndoLock target = null)
        {
            return new UndoBarrier(new UndoRecovery(undo ?? new UndoSuppression(_log), target))
                .Guard(kind, context =>
                {
                    calls[0]++;

                    return ToolEnvelope.Success(null);
                });
        }

        private IDictionary<string, object> Call(
            McpMethod method, IDictionary<string, object> arguments)
        {
            return (IDictionary<string, object>)method(
                new McpMethodContext(
                    arguments,
                    new InlineInvoker(),
                    100000,
                    new HandleLedger(_log, new HandleIdIssuer()),
                    new EventQueue(new EventSequenceIssuer())));
        }

        /// <summary>包みが載せた警告。並びの要素の型は問わない。</summary>
        private static IList<string> Warnings(IDictionary<string, object> envelope)
        {
            return ((System.Collections.IEnumerable)envelope[ToolEnvelope.WarningsName])
                .Cast<object>()
                .Select(warning => (string)warning)
                .ToList();
        }

        private static IDictionary<string, object> Arguments(
            params KeyValuePair<string, object>[] given)
        {
            return ComposedEditFixture.Arguments(given);
        }

        private static KeyValuePair<string, object> Given(string name, object value)
        {
            return ComposedEditFixture.Given(name, value);
        }

        private static string Code(IDictionary<string, object> envelope)
        {
            return ComposedEditFixture.Code(envelope);
        }

        private static string Message(IDictionary<string, object> envelope)
        {
            return ComposedEditFixture.Message(envelope);
        }

        /// <summary>指定の回数だけ戻すのに失敗する相手。</summary>
        private sealed class StubLock : IUndoLock
        {
            public int UnlockFailures { get; set; }

            public void Lock()
            {
            }

            public void Unlock()
            {
                if (UnlockFailures > 0)
                {
                    UnlockFailures--;

                    throw new InvalidOperationException("戻せない。");
                }
            }
        }
    }
}
