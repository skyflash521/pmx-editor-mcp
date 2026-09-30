using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(TimedCollection.Name)]
    public sealed class BuiltModelNodesTests : IDisposable
    {
        private readonly GeneratedToolFixture _fixture = new GeneratedToolFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheFramesOfAModelHoldingTheTwoInItsOwnListAreListedOnce()
        {
            FakePmx model = Built(true);
            model.Node.Add(new FakeNode("枠A"));

            Assert.Equal(new[] { "Root", "表情", "枠A" }, Listed(_handle));
        }

        [Fact]
        public void TheFramesOfACloneOfThatModelAreListedOnce()
        {
            FakePmx model = Built(true);
            model.Node.Add(new FakeNode("枠A"));

            IDictionary<string, object> envelope = _fixture.Call(
                "model_clone_pmx", ComposedEditFixture.Given(PmxSession.HandleName, _handle));
            object issued = envelope["value"];
            System.Collections.IList row = issued as System.Collections.IList;
            long clone = Convert.ToInt64(row == null ? issued : row[0]);

            Assert.Equal(new[] { "Root", "表情", "枠A" }, Listed(clone));
        }

        [Fact]
        public void TheFramesOfAModelHoldingTheTwoApartAreListedOnce()
        {
            FakePmx model = Built(false);
            model.Node.Add(new FakeNode("枠A"));

            Assert.Equal(new[] { "Root", "表情", "枠A" }, Listed(_handle));
        }

        [Fact]
        public void RemovingAFrameLeavesTheTwoInTheModelsOwnList()
        {
            FakePmx model = Built(true);
            FakeNode kept = new FakeNode("枠A");
            FakeNode gone = new FakeNode("枠B");
            model.Node.Add(kept);
            model.Node.Add(gone);

            IDictionary<string, object> envelope = _fixture.Call(
                "model_remove_nodes",
                ComposedEditFixture.Given("indices", new object[] { 3 }),
                ComposedEditFixture.Given(PmxSession.HandleName, _handle));

            Assert.True(
                Equals(envelope["ok"], true),
                Equals(envelope["ok"], true) ? string.Empty : ComposedEditFixture.Message(envelope));
            Assert.Equal(
                new IPXNode[] { model.RootNode, model.ExpressionNode, kept }, model.Node.ToArray());
        }

        private long _handle;

        private FakePmx Built(bool inList)
        {
            _fixture.Builder.SystemNodesInList = inList;
            IDictionary<string, object> envelope = _fixture.Call("model_pmx");
            _handle = Convert.ToInt64(((System.Collections.IList)envelope["value"])[0]);
            object held;
            Assert.True(_fixture.Handles.TryGet((int)_handle, out held));

            return (FakePmx)held;
        }

        private IList<string> Listed(long handle)
        {
            IDictionary<string, object> envelope = _fixture.Call(
                "model_list_nodes",
                ComposedEditFixture.Given(PmxSession.HandleName, handle),
                ComposedEditFixture.Given(ToolDispatch.FieldsName, new object[] { "name" }),
                ComposedEditFixture.Given("all", true));
            IDictionary<string, object> value = ComposedEditFixture.Value(envelope);

            return ((object[])value[ToolDispatch.ItemsName])
                .Select(i => (string)((IDictionary<string, object>)i)["name"])
                .ToList();
        }
    }
}
