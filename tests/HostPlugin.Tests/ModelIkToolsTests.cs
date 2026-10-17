using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// IK を親ごとに1つ辿るツール。
    /// </summary>
    [Collection(TimedCollection.Name)]
    public sealed class ModelIkToolsTests : IDisposable
    {
        private readonly GeneratedToolFixture _fixture = new GeneratedToolFixture();

        public ModelIkToolsTests()
        {
            for (int at = 0; at < 4; at++)
            {
                _fixture.Model.Bone.Add(new FakeBone("ボーン" + at));
            }

            foreach (int at in new[] { 1, 3 })
            {
                FakeBone bone = (FakeBone)_fixture.Model.Bone[at];
                bone.IsIK = true;
                bone.IK.Target = _fixture.Model.Bone[0];
            }
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void ListingWithTheParentsAllReturnsOnlyTheBonesThatHaveAnIk()
        {
            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                "model_list_iks",
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(2, value[ToolDispatch.TotalName]);
            Assert.Equal(new object[] { 1, 3 }, ParentIndices(value));
        }

        [Fact]
        public void ListingWithTheParentRangeReturnsOnlyTheBonesThatHaveAnIk()
        {
            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                "model_list_iks",
                ComposedEditFixture.Given(
                    "parentRange",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "start", 0 },
                        { "count", 2 },
                    }),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(new object[] { 1 }, ParentIndices(value));
        }

        [Fact]
        public void ListingWithTheParentsPointedByPositionReturnsTheirIkWhetherOrNotTheyHaveOne()
        {
            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                "model_list_iks",
                ComposedEditFixture.Given("parentIndices", new object[] { 0, 1 }),
                ComposedEditFixture.Given("all", true)));

            Assert.Equal(new object[] { 0, 1 }, ParentIndices(value));
        }

        [Fact]
        public void UpdatingWithTheParentsAllChangesOnlyTheIkOfTheBonesThatHaveOne()
        {
            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                "model_update_iks",
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(
                    ToolDispatch.ValueName,
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "loopCount", 9 },
                    })));

            Assert.Equal(2, value["updated"]);
            IDictionary<string, object> listed = ComposedEditFixture.Value(_fixture.Call(
                "model_list_iks",
                ComposedEditFixture.Given("parentIndices", new object[] { 0, 1, 2, 3 }),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ToolDispatch.FieldsName, new object[] { "loopCount" })));
            Assert.Equal(
                new object[] { 0, 9, 0, 9 },
                ((object[])listed[ToolDispatch.ItemsName])
                    .Select(item => ((IDictionary<string, object>)item)["loopCount"])
                    .ToArray());
        }

        private static object[] ParentIndices(IDictionary<string, object> value)
        {
            return ((object[])value[ToolDispatch.ItemsName])
                .Select(item => ((IDictionary<string, object>)item)["parentIndex"])
                .ToArray();
        }
    }
}
