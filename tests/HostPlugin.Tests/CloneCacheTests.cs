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
        public void EveryReadAnswersTheCurrentModelWhenNothingIsKept()
        {
            using (ComposedEditFixture plain = new ComposedEditFixture())
            {
                plain.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
                plain.Call(ModelFindVertexBounds.ToolName, Arguments());
                plain.Model.Vertex[0].Position = new PEPlugin.SDX.V3(-5f, 0f, 0f);

                object[] min = (object[])ComposedEditFixture.Value(
                    plain.Call(ModelFindVertexBounds.ToolName, Arguments()))["min"];

                Assert.Equal(new object[] { -5f, 0f, 0f }, min);
            }
        }

        [Fact]
        public void AModelUpdateTheEditorReportsMakesTheNextReadAnswerTheCurrentModel()
        {
            Find();
            Moved();
            _stamps.Updates++;

            Assert.Equal(new object[] { -5f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void ACountOfUndoableEditsThatChangedMakesTheNextReadAnswerTheCurrentModel()
        {
            Find();
            Moved();
            _stamps.Undos++;

            Assert.Equal(new object[] { -5f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void ACountOfRedoableEditsThatChangedMakesTheNextReadAnswerTheCurrentModel()
        {
            Find();
            Moved();
            _stamps.Redos++;

            Assert.Equal(new object[] { -5f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void AHistoryMoveThatLeftTheCountsWhereTheyWereMakesTheNextReadAnswerTheCurrentModel()
        {
            Find();
            Moved();
            _stamps.Moves++;

            Assert.Equal(new object[] { -5f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void ADifferentFileOrDifferentListSizesMakeTheNextReadAnswerTheCurrentModel()
        {
            _stamps.Identity = "A.pmx|1";
            Find();
            Moved();
            _stamps.Identity = "B.pmx|1";

            Assert.Equal(new object[] { -5f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void AFileRewrittenUnderTheSamePathMakesTheNextReadAnswerTheCurrentModel()
        {
            _stamps.Identity = "a.pmx|1|100";
            Find();
            Moved();
            _stamps.Identity = "a.pmx|2|100";

            Assert.Equal(new object[] { -5f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void ReadsAnswerTheCurrentModelWhenTheStampCannotBeRead()
        {
            _stamps.Failing = true;
            Find();
            Moved();

            Assert.Equal(new object[] { -5f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void AnEditThroughTheToolsShowsInTheNextRead()
        {
            Assert.Equal(new object[] { 1f, 0f, 0f }, MinOf(Find()));

            Align();

            Assert.Equal(new object[] { 2f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void AnEditWorksOnTheCurrentModelEvenWhenAReadKeptAClone()
        {
            Find();
            Moved();

            Align();

            Assert.Equal(new object[] { -1f, 0f, 0f }, MinOf(Find()));
        }

        [Fact]
        public void AnActionThatDidNotSayItOnlyReadDropsTheKeptClone()
        {
            Find();
            _fixture.Invoker.TryInvokeOnUi(() => { });
            Moved();

            Assert.Equal(new object[] { -5f, 0f, 0f }, MinOf(Find()));
        }

        private void Moved()
        {
            _fixture.Model.Vertex[0].Position = new PEPlugin.SDX.V3(-5f, 0f, 0f);
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
