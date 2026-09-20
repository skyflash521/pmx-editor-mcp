using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelValidateToolsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void AModelWithNothingWrongIsReportedAsSound()
        {
            IList<IPXVertex> corners = Corners();
            Material(new FakeFace(corners[0], corners[1], corners[2]));

            Assert.Equal(0, Validated()[ModelValidatePmx.FoundName]);
        }

        [Fact]
        public void AFaceThatDoesNotHaveThreeVerticesIsCounted()
        {
            IList<IPXVertex> corners = Corners();
            Material(new FakeFace(corners[0], corners[1], corners[1]));

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[ModelValidatePmx.UnsoundFacesName]);
            Assert.Equal(1, found[ModelValidatePmx.FoundName]);
        }

        [Fact]
        public void AFaceThatPointsAtAVertexOutsideTheListIsCounted()
        {
            IList<IPXVertex> corners = Corners();
            Material(new FakeFace(corners[0], corners[1], new FakeVertex(9f, 9f, 9f)));

            Assert.Equal(1, Validated()[ModelValidatePmx.DanglingFacesName]);
        }

        [Fact]
        public void AVertexWeighedToABoneOutsideTheListIsCounted()
        {
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.Bone1 = new FakeBone("居ない");
            vertex.Weight1 = 1f;
            _fixture.Model.Vertex.Add(vertex);

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[ModelValidatePmx.DanglingWeightsName]);
            Assert.Equal(0, found[ModelValidatePmx.UnnormalizedWeightsName]);
        }

        [Fact]
        public void ABoneThatPointsAtABoneOutsideTheListIsCounted()
        {
            FakeBone bone = new FakeBone("腕");
            bone.Parent = new FakeBone("居ない");
            bone.ToBone = new FakeBone("これも居ない");
            _fixture.Model.Bone.Add(bone);

            Assert.Equal(2, Validated()[ModelValidatePmx.DanglingBonesName]);
        }

        [Fact]
        public void AnOffsetThatPointsAtSomethingOutsideTheListIsCounted()
        {
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            morph.Offsets.Add(new FakeVertexMorphOffset(new FakeVertex(0f, 0f, 0f)));
            _fixture.Model.Morph.Add(morph);

            Assert.Equal(1, Validated()[ModelValidatePmx.DanglingMorphOffsetsName]);
        }

        [Fact]
        public void ANodeItemThatPointsAtSomethingOutsideTheListIsCounted()
        {
            FakeNode node = new FakeNode("枠");
            node.Items.Add(new FakeBoneNodeItem(new FakeBone("居ない")));
            _fixture.Model.Node.Add(node);

            Assert.Equal(1, Validated()[ModelValidatePmx.DanglingNodeItemsName]);
        }

        [Fact]
        public void ABodyThatPointsAtABoneOutsideTheListIsCounted()
        {
            FakeBody body = new FakeBody("剛体");
            body.Bone = new FakeBone("居ない");
            _fixture.Model.Body.Add(body);

            Assert.Equal(1, Validated()[ModelValidatePmx.DanglingPhysicsName]);
        }

        [Fact]
        public void AVertexWhoseWeightsDoNotAddUpToOneIsCounted()
        {
            FakeBone bone = new FakeBone("腕");
            _fixture.Model.Bone.Add(bone);
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.Bone1 = bone;
            vertex.Weight1 = 0.5f;
            _fixture.Model.Vertex.Add(vertex);

            Assert.Equal(1, Validated()[ModelValidatePmx.UnnormalizedWeightsName]);
        }

        [Fact]
        public void TwoFacesOfTheSameThreeVerticesInOneMaterialAreCountedOnce()
        {
            IList<IPXVertex> corners = Corners();
            Material(
                new FakeFace(corners[0], corners[1], corners[2]),
                new FakeFace(corners[2], corners[0], corners[1]));

            Assert.Equal(1, Validated()[ModelValidatePmx.DuplicateFacesName]);
        }

        [Fact]
        public void TwoFacesThatDoNotHaveThreeVerticesButShareThemAreCountedAsDuplicate()
        {
            IList<IPXVertex> corners = Corners();
            Material(
                new FakeFace(corners[0], corners[1], corners[1]),
                new FakeFace(corners[1], corners[1], corners[0]));

            IDictionary<string, object> found = Validated();

            Assert.Equal(2, found[ModelValidatePmx.UnsoundFacesName]);
            Assert.Equal(1, found[ModelValidatePmx.DuplicateFacesName]);
        }

        [Fact]
        public void AVertexWhoseHeaviestBoneIsNotInTheFirstSlotIsCounted()
        {
            FakeBone light = new FakeBone("指");
            FakeBone heavy = new FakeBone("腕");
            _fixture.Model.Bone.Add(light);
            _fixture.Model.Bone.Add(heavy);
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.Bone1 = light;
            vertex.Weight1 = 0.25f;
            vertex.Bone2 = heavy;
            vertex.Weight2 = 0.75f;
            _fixture.Model.Vertex.Add(vertex);

            Assert.Equal(1, Validated()[ModelValidatePmx.UnnormalizedWeightsName]);
        }

        [Fact]
        public void AVertexThatHoldsABoneInASlotWithoutWeightIsCounted()
        {
            FakeBone held = new FakeBone("腕");
            FakeBone empty = new FakeBone("指");
            _fixture.Model.Bone.Add(held);
            _fixture.Model.Bone.Add(empty);
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.Bone1 = held;
            vertex.Weight1 = 1f;
            vertex.Bone2 = empty;
            vertex.Weight2 = 0f;
            _fixture.Model.Vertex.Add(vertex);

            Assert.Equal(1, Validated()[ModelValidatePmx.UnnormalizedWeightsName]);
        }

        [Fact]
        public void AVertexThatHoldsAWeightInASlotWithoutABoneIsCounted()
        {
            FakeBone held = new FakeBone("腕");
            _fixture.Model.Bone.Add(held);
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.Bone1 = held;
            vertex.Weight1 = 0.5f;
            vertex.Weight2 = 0.5f;
            _fixture.Model.Vertex.Add(vertex);

            Assert.Equal(1, Validated()[ModelValidatePmx.UnnormalizedWeightsName]);
        }

        [Fact]
        public void TheSameThreeVerticesInTwoMaterialsAreNotCounted()
        {
            IList<IPXVertex> corners = Corners();
            Material(new FakeFace(corners[0], corners[1], corners[2]));
            Material(new FakeFace(corners[0], corners[1], corners[2]));

            Assert.Equal(0, Validated()[ModelValidatePmx.DuplicateFacesName]);
        }

        [Fact]
        public void LookingDoesNotChangeTheModel()
        {
            IList<IPXVertex> corners = Corners();
            FakeMaterial material = Material(
                new FakeFace(corners[0], corners[1], corners[1]));
            _fixture.Model.Bone.Add(new FakeBone("腕"));

            Validated();

            Assert.Equal(3, _fixture.Model.Vertex.Count);
            Assert.Single(material.Faces);
        }

        private IDictionary<string, object> Validated()
        {
            return ComposedEditFixture.Value(
                _fixture.Call(ModelValidatePmx.ToolName, ComposedEditFixture.Arguments()));
        }

        private IList<IPXVertex> Corners()
        {
            List<IPXVertex> made = new List<IPXVertex>
            {
                new FakeVertex(0f, 0f, 0f),
                new FakeVertex(1f, 0f, 0f),
                new FakeVertex(0f, 1f, 0f),
            };
            foreach (IPXVertex vertex in made)
            {
                _fixture.Model.Vertex.Add(vertex);
            }

            return made;
        }

        private FakeMaterial Material(params IPXFace[] faces)
        {
            FakeMaterial made = new FakeMaterial("材質");
            foreach (IPXFace face in faces)
            {
                made.Faces.Add(face);
            }

            _fixture.Model.Material.Add(made);

            return made;
        }
    }
}
