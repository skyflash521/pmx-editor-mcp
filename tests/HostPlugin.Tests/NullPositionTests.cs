using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(TimedCollection.Name)]
    public sealed class NullPositionTests : IDisposable
    {
        private readonly GeneratedToolFixture _fixture = new GeneratedToolFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void AFaceVertexIsNotClearedWithNull()
        {
            FakeVertex[] vertices = { new FakeVertex(), new FakeVertex(), new FakeVertex() };
            foreach (FakeVertex vertex in vertices)
            {
                _fixture.Model.Vertex.Add(vertex);
            }

            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(vertices[0], vertices[1], vertices[2]));
            _fixture.Model.Material.Add(material);

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_faces",
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ToolDispatch.ValueName, Value("vertex1", null)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Same(vertices[0], material.Faces[0].Vertex1);
        }

        [Fact]
        public void AnIkTargetIsNotClearedWithNull()
        {
            FakeBone target = new FakeBone("目標");
            FakeBone bone = new FakeBone("IK") { IsIK = true };
            bone.IK.Target = target;
            _fixture.Model.Bone.Add(target);
            _fixture.Model.Bone.Add(bone);

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_iks",
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ToolDispatch.ValueName, Value("target", null)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Same(target, bone.IK.Target);
        }

        [Fact]
        public void AnIkLinkBoneIsNotClearedWithNull()
        {
            FakeBone linked = new FakeBone("リンク");
            FakeBone bone = new FakeBone("IK") { IsIK = true };
            bone.IK.Target = linked;
            bone.IK.Links.Add(new FakeIkLink { Bone = linked });
            _fixture.Model.Bone.Add(linked);
            _fixture.Model.Bone.Add(bone);

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_ik_links",
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ToolDispatch.ValueName, Value("bone", null)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Same(linked, bone.IK.Links[0].Bone);
        }

        [Fact]
        public void AnOffsetGivenWhileTheBoneStillPointsAtABoneIsWarnedAbout()
        {
            _fixture.Model.Bone.Add(new FakeBone("先"));
            _fixture.Model.Bone.Add(new FakeBone("元") { ToBone = _fixture.Model.Bone[0] });

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_bones",
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(
                    ToolDispatch.ValueName, Value("toOffset", new object[] { 0d, 1d, 0d })));

            Assert.True(Equals(envelope["ok"], true), "成功でない包み。");
            Assert.Contains(
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>(),
                warning => warning.Contains("toBone"));
        }

        [Fact]
        public void ABoneGivenWhileTheBoneStillHasAnOffsetIsWarnedAbout()
        {
            _fixture.Model.Bone.Add(new FakeBone("先"));
            _fixture.Model.Bone.Add(new FakeBone("元") { ToOffset = new V3(0f, 1f, 0f) });

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_bones",
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ToolDispatch.ValueName, Value("toBone", 0)));

            Assert.True(Equals(envelope["ok"], true), "成功でない包み。");
            Assert.Contains(
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>(),
                warning => warning.Contains("toOffset"));
        }

        [Fact]
        public void SeveralBonesStuckOnTheirBoneAreWarnedAboutInOneWarning()
        {
            _fixture.Model.Bone.Add(new FakeBone("先"));
            _fixture.Model.Bone.Add(new FakeBone("一") { ToBone = _fixture.Model.Bone[0] });
            _fixture.Model.Bone.Add(new FakeBone("二") { ToBone = _fixture.Model.Bone[0] });

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_bones",
                ComposedEditFixture.Given("indices", new object[] { 1, 2 }),
                ComposedEditFixture.Given(
                    ToolDispatch.ValueName, Value("toOffset", new object[] { 0d, 1d, 0d })));

            string warning = Assert.Single(
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>());
            Assert.Contains("位置 1「一」", warning);
            Assert.Contains("位置 2「二」", warning);
        }

        [Fact]
        public void AnIkLinkIsNotBuiltWithoutABone()
        {
            _fixture.Model.Bone.Add(new FakeBone("リンク"));

            IDictionary<string, object> envelope = _fixture.Call(
                "model_ik_link", ComposedEditFixture.Given("bone", null));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains("bone は位置の整数", ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void AnIkLinkIsBuiltWithABonePointedByPosition()
        {
            _fixture.Model.Bone.Add(new FakeBone("リンク"));

            IDictionary<string, object> envelope = _fixture.Call(
                "model_ik_link", ComposedEditFixture.Given("bone", 0));

            Assert.True(Equals(envelope["ok"], true), "成功でない包み。");
        }

        [Fact]
        public void AnOffsetOfZeroGivenWhileTheBoneStillPointsAtABoneIsNotWarnedAbout()
        {
            _fixture.Model.Bone.Add(new FakeBone("先"));
            _fixture.Model.Bone.Add(new FakeBone("元") { ToBone = _fixture.Model.Bone[0] });

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_bones",
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(
                    ToolDispatch.ValueName, Value("toOffset", new object[] { 0d, 0d, 0d })));

            Assert.True(Equals(envelope["ok"], true), "成功でない包み。");
            Assert.False(envelope.ContainsKey(ToolEnvelope.WarningsName), "警告が付いている。");
        }

        [Fact]
        public void AnOffsetAndABoneGivenTogetherAreWarnedAbout()
        {
            _fixture.Model.Bone.Add(new FakeBone("先"));
            _fixture.Model.Bone.Add(new FakeBone("元"));
            Dictionary<string, object> both = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "toOffset", new object[] { 0d, 1d, 0d } },
                { "toBone", 0 },
            };

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_bones",
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(ToolDispatch.ValueName, both));

            Assert.True(Equals(envelope["ok"], true), "成功でない包み。");
            Assert.Single(((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>());
        }

        [Fact]
        public void AVertexMorphOffsetIsNotClearedWithNull()
        {
            FakeVertex vertex = new FakeVertex();
            _fixture.Model.Vertex.Add(vertex);
            FakeMorph morph = new FakeMorph("頂点");
            morph.Offsets.Add(new FakeVertexMorphOffset(vertex));
            _fixture.Model.Morph.Add(morph);

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_morph_offsets",
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(ToolDispatch.ItemTypeName, "vertex_morph_offset"),
                ComposedEditFixture.Given(ToolDispatch.ValueName, Value("vertex", null)));

            Assert.True(Equals(envelope["ok"], false), "成功している。");
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Same(vertex, ((IPXVertexMorphOffset)morph.Offsets[0]).Vertex);
        }

        [Fact]
        public void AVertexMorphOffsetIsNotBuiltWithoutAVertex()
        {
            _fixture.Model.Vertex.Add(new FakeVertex());

            IDictionary<string, object> envelope = _fixture.Call(
                "model_vertex_morph_offset",
                ComposedEditFixture.Given("vertex", null),
                ComposedEditFixture.Given("offset", new object[] { 0d, 0d, 0d }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains("vertex は位置の整数", ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void ABoneHeldByHandleGivenAnOffsetAndABoneIsWarnedAbout()
        {
            _fixture.Model.Bone.Add(new FakeBone("先"));
            IDictionary<string, object> built = _fixture.Call("model_bone");
            Assert.True(
                Equals(built["ok"], true),
                Equals(built["ok"], true) ? string.Empty : ComposedEditFixture.Message(built));
            long handle = Convert.ToInt64(
                ((IEnumerable<object>)(built["value"] is object[] many ? many : new[] { built["value"] })).Single());
            Dictionary<string, object> both = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "toOffset", new object[] { 0d, 1d, 0d } },
                { "toBone", 0 },
            };

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_bones",
                ComposedEditFixture.Given("handles", new object[] { handle }),
                ComposedEditFixture.Given(ToolDispatch.ValueName, both));

            Assert.True(Equals(envelope["ok"], true), "成功でない包み。");
            Assert.Single(((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>());
        }

        [Fact]
        public void ANullWrittenAfterAPositionOnAHeldBoneReplacesThePosition()
        {
            _fixture.Model.Bone.Add(new FakeBone("先"));
            long handle = HeldBone();

            Update(handle, Value("toBone", 0));
            Update(handle, Value("toBone", null));
            IDictionary<string, object> added = _fixture.Call(
                "model_add_bones",
                ComposedEditFixture.Given("handles", new object[] { handle }));

            Assert.True(Equals(added["ok"], true), "成功でない包み。");
            Assert.Null(_fixture.Model.Bone[1].ToBone);
        }

        [Fact]
        public void AnOffsetGivenAfterAPositionOnAHeldBoneInAnEarlierCallIsWarnedAbout()
        {
            _fixture.Model.Bone.Add(new FakeBone("先"));
            long handle = HeldBone();

            Update(handle, Value("toBone", 0));
            IDictionary<string, object> envelope = Update(
                handle, Value("toOffset", new object[] { 0d, 1d, 0d }));

            Assert.Contains(
                "ハンドル " + handle,
                Assert.Single(((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>()));
        }

        private long HeldBone()
        {
            IDictionary<string, object> built = _fixture.Call("model_bone");
            object value = built["value"];

            return Convert.ToInt64(value is object[] many ? many.Single() : value);
        }

        private IDictionary<string, object> Update(long handle, IDictionary<string, object> value)
        {
            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_bones",
                ComposedEditFixture.Given("handles", new object[] { handle }),
                ComposedEditFixture.Given(ToolDispatch.ValueName, value));
            Assert.True(
                Equals(envelope["ok"], true),
                Equals(envelope["ok"], true) ? string.Empty : ComposedEditFixture.Message(envelope));

            return envelope;
        }

        private static IDictionary<string, object> Value(string name, object value)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { name, value } };
        }
    }
}
