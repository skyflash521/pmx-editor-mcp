using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class CloneCacheTests : IDisposable
    {
        private readonly ModelStamps _stamps = new ModelStamps();

        private readonly ComposedEditFixture _fixture;

        public CloneCacheTests()
        {
            _fixture = new ComposedEditFixture(_stamps);
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(3f, 0f, 0f));
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void ReadsInARowTakeOneCloneAndAnswerTheSame()
        {
            object[] first = MinOf(Find());
            object[] second = MinOf(Find());
            object[] third = MinOf(Find());

            Assert.Equal(1, _fixture.Clones);
            Assert.Equal(first, second);
            Assert.Equal(first, third);
        }

        [Fact]
        public void EveryReadTakesItsOwnCloneWhenNothingIsKept()
        {
            using (ComposedEditFixture plain = new ComposedEditFixture())
            {
                plain.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
                plain.Call(ModelFindVertexBounds.ToolName, Arguments());
                plain.Call(ModelFindVertexBounds.ToolName, Arguments());

                Assert.Equal(2, plain.Clones);
            }
        }

        [Fact]
        public void AModelUpdateTheEditorReportsMakesTheNextReadTakeANewClone()
        {
            Find();
            _fixture.Model.Vertex[0].Position = new PEPlugin.SDX.V3(-5f, 0f, 0f);
            _stamps.Updates++;

            object[] min = MinOf(Find());

            Assert.Equal(new object[] { -5f, 0f, 0f }, min);
            Assert.Equal(2, _fixture.Clones);
        }

        [Fact]
        public void ACountOfUndoableEditsThatChangedMakesTheNextReadTakeANewClone()
        {
            Find();
            _stamps.Undos++;

            Find();

            Assert.Equal(2, _fixture.Clones);
        }

        [Fact]
        public void ACountOfRedoableEditsThatChangedMakesTheNextReadTakeANewClone()
        {
            Find();
            _stamps.Redos++;

            Find();

            Assert.Equal(2, _fixture.Clones);
        }

        [Fact]
        public void AHistoryMoveThatLeftTheCountsWhereTheyWereMakesTheNextReadTakeANewClone()
        {
            Find();
            _stamps.Moves++;

            Find();

            Assert.Equal(2, _fixture.Clones);
        }

        [Fact]
        public void ADifferentFileOrDifferentListSizesMakeTheNextReadTakeANewClone()
        {
            _stamps.Identity = "A.pmx|1";
            Find();
            _stamps.Identity = "B.pmx|1";

            Find();

            Assert.Equal(2, _fixture.Clones);
        }

        [Fact]
        public void AFileRewrittenUnderTheSamePathMakesTheNextReadTakeANewClone()
        {
            _stamps.Identity = "a.pmx|1|100";
            Find();
            _stamps.Identity = "a.pmx|2|100";

            Find();

            Assert.Equal(2, _fixture.Clones);
        }

        [Fact]
        public void ReadsStillAnswerWhenTheStampCannotBeRead()
        {
            _stamps.Failing = true;

            object[] first = MinOf(Find());
            object[] second = MinOf(Find());

            Assert.Equal(first, second);
            Assert.Equal(2, _fixture.Clones);
        }

        [Fact]
        public void AnEditThroughTheToolsShowsInTheNextRead()
        {
            Assert.Equal(new object[] { 1f, 0f, 0f }, MinOf(Find()));

            Align();

            Assert.Equal(new object[] { 2f, 0f, 0f }, MinOf(Find()));
            Assert.Equal(3, _fixture.Clones);
        }

        [Fact]
        public void AnEditTakesAFreshCloneEvenWhenAReadKeptOne()
        {
            Find();

            Align();

            Assert.Equal(2, _fixture.Clones);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void AnActionThatDidNotSayItOnlyReadDropsTheKeptClone()
        {
            Find();
            _fixture.Invoker.TryInvokeOnUi(() => { });
            _fixture.Model.Vertex[0].Position = new PEPlugin.SDX.V3(-5f, 0f, 0f);

            object[] min = MinOf(Find());

            Assert.Equal(new object[] { -5f, 0f, 0f }, min);
            Assert.Equal(2, _fixture.Clones);
        }

        [Fact]
        public void ReadsThroughAHandleNeverTakeOrKeepAClone()
        {
            KeyValuePair<string, object> held = _fixture.HoldModel();

            ComposedEditFixture.Value(_fixture.Call(
                ModelFindVertexBounds.ToolName, Arguments(held)));

            Assert.Equal(0, _fixture.Clones);
        }

        private void Align()
        {
            ComposedEditFixture.Value(_fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", ModelEditVertices.Align),
                    ComposedEditFixture.Given("indices", new object[] { 0, 1 }),
                    ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisX))));
        }

        private IDictionary<string, object> Find()
        {
            return _fixture.Call(ModelFindVertexBounds.ToolName, Arguments());
        }

        private static IDictionary<string, object> Arguments(
            params KeyValuePair<string, object>[] extra)
        {
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("all", true),
            };
            given.AddRange(extra);

            return ComposedEditFixture.Arguments(given.ToArray());
        }

        private static object[] MinOf(IDictionary<string, object> envelope)
        {
            return (object[])ComposedEditFixture.Value(envelope)["min"];
        }
    }
}
