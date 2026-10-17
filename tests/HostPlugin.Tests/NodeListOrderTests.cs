using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(TimedCollection.Name)]
    public sealed class NodeListOrderTests : IDisposable
    {
        private readonly GeneratedToolFixture _fixture = new GeneratedToolFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheNodeListStartsWithTheRootNodeAndThenTheExpressionNode()
        {
            _fixture.Model.Node.Add(new FakeNode("手"));

            IList<IDictionary<string, object>> items = Items(_fixture.Call(
                "model_list_nodes", ComposedEditFixture.Given("all", true)));

            Assert.Equal(
                new object[] { "Root", "表情", "手" }, items.Select(i => i["name"]).ToArray());
        }

        [Fact]
        public void TheNodeListPutsTheNodesInTheOrderTheFileHoldsThem()
        {
            _fixture.Model.Node.Add(new FakeNode("手"));

            IList<IDictionary<string, object>> items = Items(_fixture.Call(
                "model_list_nodes", ComposedEditFixture.Given("all", true)));

            Assert.Equal(
                ReferenceCleanup.Nodes(_fixture.Model).Select(n => n.Name).ToArray(),
                items.Select(i => (string)i["name"]).ToArray());
        }

        [Fact]
        public void TheItemsOfTheFirstNodeAreTheRootNodesItems()
        {
            FakeBone bone = new FakeBone("センター");
            _fixture.Model.Bone.Add(bone);
            _fixture.Model.RootNode.Items.Add(new FakeBoneNodeItem(bone));
            FakeMorph morph = new FakeMorph("笑い");
            _fixture.Model.Morph.Add(morph);
            _fixture.Model.ExpressionNode.Items.Add(new FakeMorphNodeItem(morph));

            IList<IDictionary<string, object>> root = Items(_fixture.Call(
                "model_list_node_items",
                ComposedEditFixture.Given("parentIndices", new object[] { 0 }),
                ComposedEditFixture.Given("all", true)));
            IList<IDictionary<string, object>> expression = Items(_fixture.Call(
                "model_list_node_items",
                ComposedEditFixture.Given("parentIndices", new object[] { 1 }),
                ComposedEditFixture.Given("all", true)));

            Assert.Single(root);
            Assert.True(root[0].ContainsKey("bone"), "Root の中身がボーンでない。");
            Assert.Single(expression);
            Assert.True(expression[0].ContainsKey("morph"), "表情の中身がモーフでない。");
        }

        private static IList<IDictionary<string, object>> Items(IDictionary<string, object> envelope)
        {
            Assert.True(
                Equals(envelope["ok"], true),
                Equals(envelope["ok"], true) ? string.Empty : ComposedEditFixture.Message(envelope));
            IDictionary<string, object> value = (IDictionary<string, object>)envelope["value"];

            return ((object[])value[ToolDispatch.ItemsName])
                .Cast<IDictionary<string, object>>()
                .ToList();
        }
    }
}
