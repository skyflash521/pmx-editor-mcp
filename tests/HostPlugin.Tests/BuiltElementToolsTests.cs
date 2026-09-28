using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(TimedCollection.Name)]
    public sealed class BuiltElementToolsTests : IDisposable
    {
        private const int StandingMorphs = 5;

        private const int CalledMorph = 4;

        private readonly GeneratedToolFixture _fixture = new GeneratedToolFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void AGroupMorphKeepsTheOffsetBuiltWithTheMorphItCallsWhenTheOffsetIsAddedAfterIt()
        {
            StandMorphs();
            long morph = Built("model_morph");
            Succeeded(_fixture.Call(
                "model_update_morphs",
                Given(TargetNames.Element.Handles, new object[] { morph }),
                Given(ToolDispatch.ValueName, Value("kind", "Group"))));
            Succeeded(_fixture.Call(
                "model_add_morphs", Given(TargetNames.Element.Handles, new object[] { morph })));
            long offset = Built(
                "model_group_morph_offset", Given("morph", CalledMorph), Given("ratio", 1d));

            Succeeded(_fixture.Call(
                "model_add_morph_offsets",
                Given(
                    ToolDispatch.AssignmentsName,
                    new object[] { Assignment(ToolDispatch.ParentIndexName, StandingMorphs, offset) })));

            CallsTheMorph(StandingMorphs);
        }

        [Fact]
        public void AGroupMorphKeepsTheOffsetBuiltWithTheMorphItCallsWhenItIsAddedHoldingTheOffset()
        {
            StandMorphs();
            long morph = Built("model_morph");
            Succeeded(_fixture.Call(
                "model_update_morphs",
                Given(TargetNames.Element.Handles, new object[] { morph }),
                Given(ToolDispatch.ValueName, Value("kind", "Group"))));
            long offset = Built(
                "model_group_morph_offset", Given("morph", CalledMorph), Given("ratio", 1d));
            Succeeded(_fixture.Call(
                "model_add_morph_offsets",
                Given(
                    ToolDispatch.AssignmentsName,
                    new object[] { Assignment(ToolDispatch.ParentHandleName, morph, offset) })));

            Succeeded(_fixture.Call(
                "model_add_morphs", Given(TargetNames.Element.Handles, new object[] { morph })));

            CallsTheMorph(StandingMorphs);
        }

        [Fact]
        public void AGroupMorphKeepsAnOffsetWhoseMorphWasWrittenBeforeItWasAdded()
        {
            StandMorphs();
            long morph = Built("model_morph");
            Succeeded(_fixture.Call(
                "model_update_morphs",
                Given(TargetNames.Element.Handles, new object[] { morph }),
                Given(ToolDispatch.ValueName, Value("kind", "Group"))));
            Succeeded(_fixture.Call(
                "model_add_morphs", Given(TargetNames.Element.Handles, new object[] { morph })));
            long offset = Built("model_group_morph_offset");
            Succeeded(_fixture.Call(
                "model_update_morph_offsets",
                Given(TargetNames.Element.Handles, new object[] { offset }),
                Given(ToolDispatch.ItemTypeName, "group_morph_offset"),
                Given(ToolDispatch.ValueName, Value("morph", CalledMorph, "ratio", 1d))));

            Succeeded(_fixture.Call(
                "model_add_morph_offsets",
                Given(
                    ToolDispatch.AssignmentsName,
                    new object[] { Assignment(ToolDispatch.ParentIndexName, StandingMorphs, offset) })));

            CallsTheMorph(StandingMorphs);
        }

        [Fact]
        public void AVertexMorphOffsetBuiltWithAVertexAndACoordinateOffsetMovesThatVertexOnceAdded()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            _fixture.Model.Morph.Add(new FakeMorph("頂点", MorphKind.Vertex));

            long offset = Built(
                "model_vertex_morph_offset",
                Given("vertex", 1),
                Given("offset", new object[] { 0d, 2d, 0d }));
            Succeeded(_fixture.Call(
                "model_add_morph_offsets",
                Given(
                    ToolDispatch.AssignmentsName,
                    new object[] { Assignment(ToolDispatch.ParentIndexName, 0, offset) })));

            IPXVertexMorphOffset added =
                (IPXVertexMorphOffset)Assert.Single(_fixture.Model.Morph[0].Offsets);
            Assert.Same(_fixture.Model.Vertex[1], added.Vertex);
            Assert.Equal(new[] { 0f, 2f, 0f }, new[] { added.Offset.X, added.Offset.Y, added.Offset.Z });
        }

        private void StandMorphs()
        {
            for (int at = 0; at < StandingMorphs; at++)
            {
                _fixture.Model.Morph.Add(new FakeMorph("頂点" + at, MorphKind.Vertex));
            }
        }

        private void CallsTheMorph(int at)
        {
            Assert.True(
                _fixture.Model.Morph.Count == StandingMorphs + 1,
                "足したグループモーフがモデルに無い。モーフの件数: " + _fixture.Model.Morph.Count);
            IPXMorph group = _fixture.Model.Morph[at];
            Assert.Equal(MorphKind.Group, group.Kind);
            IPXGroupMorphOffset offset = (IPXGroupMorphOffset)Assert.Single(group.Offsets);
            Assert.Same(_fixture.Model.Morph[CalledMorph], offset.Morph);
            Assert.Equal(1f, offset.Ratio);
        }

        private long Built(string tool, params KeyValuePair<string, object>[] given)
        {
            IDictionary<string, object> envelope = _fixture.Call(tool, given);
            Succeeded(envelope);
            object value = envelope["value"];
            IList row = value as IList;

            return Convert.ToInt64(row == null ? value : Assert.Single(row.Cast<object>()));
        }

        private static void Succeeded(IDictionary<string, object> envelope)
        {
            Assert.True(
                Equals(envelope["ok"], true),
                "成功でない包み: "
                    + (Equals(envelope["ok"], true) ? string.Empty : ComposedEditFixture.Message(envelope)));
        }

        private static IDictionary<string, object> Assignment(string parentName, long parent, long handle)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { parentName, parent },
                { TargetNames.Element.Handles, new object[] { handle } },
            };
        }

        private static IDictionary<string, object> Value(params object[] pairs)
        {
            Dictionary<string, object> value = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int at = 0; at < pairs.Length; at += 2)
            {
                value.Add((string)pairs[at], pairs[at + 1]);
            }

            return value;
        }

        private static KeyValuePair<string, object> Given(string name, object value)
        {
            return ComposedEditFixture.Given(name, value);
        }
    }
}
