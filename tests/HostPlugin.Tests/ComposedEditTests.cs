using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 1メンバーへ写らない組み立てのツールを、生成したツールと同じ複製編集の経路へ乗せる枠。
    /// </summary>
    public sealed class ComposedEditTests : IDisposable
    {

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheBodyGetsTheCloneOfTheCurrentPmxAndTheChangeIsReflectedOnce()
        {
            object seen = null;

            IDictionary<string, object> envelope = _fixture.Call(
                Method((context, pmx) =>
                {
                    seen = pmx;

                    return ComposedEditResult.Complete(7);
                }),
                ComposedEditFixture.Arguments());

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Equal(7, envelope["value"]);
            Assert.Same(_fixture.Model, seen);
            Assert.Equal(1, _fixture.Clones);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void PointingThePmxByHandleSkipsTheCloneAndTheReflection()
        {
            FakePmx held = new FakePmx();
            int handle = _fixture.Handles.Issue(typeof(FakePmx).FullName, held, () => { });
            object seen = null;

            _fixture.Call(
                Method((context, pmx) =>
                {
                    seen = pmx;

                    return ComposedEditResult.Complete(null);
                }),
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(PmxSession.HandleName, handle)));

            Assert.Same(held, seen);
            Assert.Equal(0, _fixture.Clones);
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void AHandleTheLedgerDoesNotKnowIsRefusedWithoutReflecting()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                Method((context, pmx) => ComposedEditResult.Complete(null)),
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(PmxSession.HandleName, 99)));

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedEditFixture.Code(envelope));
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void AnArgumentTheToolDoesNotTakeIsRefusedBeforeTheCloneIsTaken()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                Method((context, pmx) => ComposedEditResult.Complete(null)),
                ComposedEditFixture.Arguments(ComposedEditFixture.Given("知らない項目", 1)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(0, _fixture.Clones);
        }

        [Fact]
        public void TheNamesTheToolTakesGoThrough()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                Method((context, pmx) => ComposedEditResult.Complete(null), new[] { "kind" }),
                ComposedEditFixture.Arguments(ComposedEditFixture.Given("kind", "vertex")));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
        }

        [Fact]
        public void ABodyThatRefusesLeavesTheModelUnreflected()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                Method((context, pmx) => ComposedEditResult.Refuse(
                    ToolEnvelope.IndexOutOfRange, "並びの外を指している。")),
                ComposedEditFixture.Arguments());

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void ABodyThatThrowsAnswersThatTheStateIsUnchanged()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                Method((context, pmx) => throw new InvalidOperationException("途中で落ちた。")),
                ComposedEditFixture.Arguments());

            Assert.Equal(ToolEnvelope.OperationFailed, ComposedEditFixture.Code(envelope));
            Assert.Contains("未変更", ComposedEditFixture.Message(envelope));
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void ABodyThatThrowsOnAHeldPmxAnswersThatTheResultIsUnknown()
        {
            FakePmx held = new FakePmx();
            int handle = _fixture.Handles.Issue(typeof(FakePmx).FullName, held, () => { });

            IDictionary<string, object> envelope = _fixture.Call(
                Method((context, pmx) => throw new InvalidOperationException("途中で落ちた。")),
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(PmxSession.HandleName, handle)));

            Assert.Equal(ToolEnvelope.OperationFailed, ComposedEditFixture.Code(envelope));
            Assert.Contains("結果不明", ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void AskingToSuppressTheUndoRecordReachesTheReflection()
        {
            _fixture.Call(
                Method((context, pmx) => ComposedEditResult.Complete(null)),
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(UndoBarrier.SuppressName, true)));

            Assert.True(_fixture.Suppressed, "抑止の頼みが反映へ届いていない。");
        }

        [Fact]
        public void TheFlowAndTheBarrierAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new ComposedEdit(null, _fixture.Barrier()));
            Assert.Throws<ArgumentNullException>(() => new ComposedEdit(_fixture.Session(), null));
        }

        [Fact]
        public void TheRecoveryIsRequiredToBuildTheBarrier()
        {
            Assert.Throws<ArgumentNullException>(() => new UndoBarrier(null));
        }

        private McpMethod Method(
            Func<McpMethodContext, object, ComposedEditResult> body, IList<string> known = null)
        {
            return _fixture.Edit.Method(known ?? new string[0], body);
        }
    }
}
