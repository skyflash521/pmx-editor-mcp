using System;
using System.Collections.Generic;
using System.Linq;
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

            Assert.Equal(1, Held()[ModelValidatePmx.DanglingFacesName]);
        }

        [Fact]
        public void AVertexWeighedToABoneOutsideTheListIsCounted()
        {
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.Bone1 = new FakeBone("居ない");
            vertex.Weight1 = 1f;
            _fixture.Model.Vertex.Add(vertex);

            IDictionary<string, object> found = Held();

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

            Assert.Equal(2, Held()[ModelValidatePmx.DanglingBonesName]);
        }

        [Fact]
        public void AnOffsetThatPointsAtSomethingOutsideTheListIsCounted()
        {
            FakeMorph morph = new FakeMorph("笑い", MorphKind.Vertex);
            morph.Offsets.Add(new FakeVertexMorphOffset(new FakeVertex(0f, 0f, 0f)));
            _fixture.Model.Morph.Add(morph);

            Assert.Equal(1, Held()[ModelValidatePmx.DanglingMorphOffsetsName]);
        }

        [Fact]
        public void ANodeItemThatPointsAtSomethingOutsideTheListIsCounted()
        {
            FakeNode node = new FakeNode("枠");
            node.Items.Add(new FakeBoneNodeItem(new FakeBone("居ない")));
            _fixture.Model.Node.Add(node);

            Assert.Equal(1, Held()[ModelValidatePmx.DanglingNodeItemsName]);
        }

        [Fact]
        public void ANodeItemForAMorphWithNoMorphIsNotCounted()
        {
            FakeNode node = new FakeNode("枠");
            node.Items.Add(new FakeMorphNodeItem(null));
            _fixture.Model.Node.Add(node);

            Assert.Equal(0, Held()[ModelValidatePmx.DanglingNodeItemsName]);
        }

        [Fact]
        public void AnImpulseOffsetWithNoBodyIsNotCounted()
        {
            FakeMorph morph = new FakeMorph("衝撃", MorphKind.Impulse);
            morph.Offsets.Add(new FakeImpulseMorphOffset(null));
            _fixture.Model.Morph.Add(morph);

            Assert.Equal(0, Held()[ModelValidatePmx.DanglingMorphOffsetsName]);
        }

        [Fact]
        public void AnAnchorWithNoBodyOrNoVertexIsNotCounted()
        {
            FakeSoftBody soft = new FakeSoftBody();
            soft.Anchors.Add(new FakeSoftBodyAnchor(null, null));
            _fixture.Model.SoftBody.Add(soft);

            Assert.Equal(0, Held()[ModelValidatePmx.DanglingPhysicsName]);
        }

        [Fact]
        public void ABodyThatPointsAtABoneOutsideTheListIsCounted()
        {
            FakeBody body = new FakeBody("剛体");
            body.Bone = new FakeBone("居ない");
            _fixture.Model.Body.Add(body);

            Assert.Equal(1, Held()[ModelValidatePmx.DanglingPhysicsName]);
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
        public void ASlotWithoutWeightIsEmptyWhicheverBoneItHolds()
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

            Assert.Equal(0, Validated()[ModelValidatePmx.UnnormalizedWeightsName]);
        }

        [Fact]
        public void AnSdefVertexWhoseLighterBoneComesFirstIsNotCounted()
        {
            FakeBone light = new FakeBone("指");
            FakeBone heavy = new FakeBone("腕");
            _fixture.Model.Bone.Add(light);
            _fixture.Model.Bone.Add(heavy);
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.SDEF = true;
            vertex.Bone1 = light;
            vertex.Weight1 = 0.25f;
            vertex.Bone2 = heavy;
            vertex.Weight2 = 0.75f;
            _fixture.Model.Vertex.Add(vertex);

            Assert.Equal(0, Validated()[ModelValidatePmx.UnnormalizedWeightsName]);
        }

        [Fact]
        public void AnSdefVertexWhoseWeightsDoNotAddUpToOneIsCounted()
        {
            FakeBone light = new FakeBone("指");
            FakeBone heavy = new FakeBone("腕");
            _fixture.Model.Bone.Add(light);
            _fixture.Model.Bone.Add(heavy);
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.SDEF = true;
            vertex.Bone1 = light;
            vertex.Weight1 = 0.25f;
            vertex.Bone2 = heavy;
            vertex.Weight2 = 0.5f;
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
        public void AHiddenMorphOnTheExpressionNodeIsCounted()
        {
            FakeMorph hidden = new FakeMorph("隠し") { Panel = 0 };
            FakeMorph shown = new FakeMorph("表示") { Panel = 4 };
            _fixture.Model.Morph.Add(hidden);
            _fixture.Model.Morph.Add(shown);
            _fixture.Model.ExpressionNode.Items.Add(new FakeMorphNodeItem(hidden));
            _fixture.Model.ExpressionNode.Items.Add(new FakeMorphNodeItem(shown));

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[ModelValidatePmx.HiddenMorphsInExpressionFrameName]);
            Assert.Equal(1, found[ModelValidatePmx.FoundName]);
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

        [Fact]
        public void RunsGiveThePositionsOfTheVerticesThatAreNotNormalized()
        {
            FakeBone bone = new FakeBone("腕");
            _fixture.Model.Bone.Add(bone);
            foreach (float weight in new[] { 1f, 0.5f, 0.5f, 1f, 0.5f })
            {
                FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
                vertex.Bone1 = bone;
                vertex.Weight1 = weight;
                _fixture.Model.Vertex.Add(vertex);
            }

            IDictionary<string, object> found = Located(ModelValidatePmx.UnnormalizedWeightsName);

            Assert.Equal(3, found[ModelValidatePmx.UnnormalizedWeightsName]);
            Assert.Equal(new[] { "1+2", "4+1" }, RunsOf(found));
            Assert.Equal(2, found[ModelValidatePmx.RunsTotalName]);
            Assert.False(found.ContainsKey(ModelValidatePmx.NextOffsetName));
        }

        [Fact]
        public void RunsGiveTheFacePositionsCountedAcrossMaterials()
        {
            IList<IPXVertex> corners = Corners();
            Material(new FakeFace(corners[0], corners[1], corners[2]));
            Material(
                new FakeFace(corners[0], corners[1], corners[2]),
                new FakeFace(corners[0], corners[1], corners[1]));

            Assert.Equal(
                new[] { "2+1" }, RunsOf(Located(ModelValidatePmx.UnsoundFacesName)));
            Assert.Empty(RunsOf(Located(ModelValidatePmx.DuplicateFacesName)));
        }

        [Fact]
        public void RunsGiveTheLaterFaceOfTwoFacesOfTheSameThreeVertices()
        {
            IList<IPXVertex> corners = Corners();
            Material(new FakeFace(corners[0], corners[1], corners[2]));
            Material(
                new FakeFace(corners[0], corners[1], corners[2]),
                new FakeFace(corners[2], corners[0], corners[1]));

            Assert.Equal(
                new[] { "2+1" }, RunsOf(Located(ModelValidatePmx.DuplicateFacesName)));
        }

        [Fact]
        public void RunsGiveTheDanglingFacePositionsCountedAcrossMaterials()
        {
            IList<IPXVertex> corners = Corners();
            Material(new FakeFace(corners[0], corners[1], corners[2]));
            Material(
                new FakeFace(corners[0], corners[1], corners[2]),
                new FakeFace(corners[0], corners[1], new FakeVertex(9f, 9f, 9f)),
                new FakeFace(corners[0], new FakeVertex(8f, 8f, 8f), corners[2]));

            Assert.Equal(
                new[] { "2+2" }, RunsOf(LocatedInHeldModel(ModelValidatePmx.DanglingFacesName)));
        }

        [Fact]
        public void RunsGiveTheDanglingWeightPositions()
        {
            FakeBone bone = new FakeBone("腕");
            _fixture.Model.Bone.Add(bone);
            FakeVertex sound = new FakeVertex(0f, 0f, 0f);
            sound.Bone1 = bone;
            sound.Weight1 = 1f;
            _fixture.Model.Vertex.Add(sound);
            FakeVertex loose = new FakeVertex(0f, 0f, 0f);
            loose.Bone1 = new FakeBone("居ない");
            loose.Weight1 = 1f;
            _fixture.Model.Vertex.Add(loose);

            Assert.Equal(
                new[] { "1+1" }, RunsOf(LocatedInHeldModel(ModelValidatePmx.DanglingWeightsName)));
        }

        [Fact]
        public void RunsGiveABoneWithTwoDanglingReferencesOnceWhileTheCountStaysTwo()
        {
            _fixture.Model.Bone.Add(new FakeBone("根"));
            FakeBone broken = new FakeBone("腕");
            broken.Parent = new FakeBone("居ない");
            broken.ToBone = new FakeBone("これも居ない");
            _fixture.Model.Bone.Add(broken);
            _fixture.Model.Bone.Add(new FakeBone("指"));

            IDictionary<string, object> found = LocatedInHeldModel(ModelValidatePmx.DanglingBonesName);

            Assert.Equal(2, found[ModelValidatePmx.DanglingBonesName]);
            Assert.Equal(new[] { "1+1" }, RunsOf(found));
        }

        [Fact]
        public void RunsGiveTheHiddenMorphPositions()
        {
            FakeMorph shown = new FakeMorph("表示") { Panel = 4 };
            FakeMorph hidden = new FakeMorph("隠し") { Panel = 0 };
            _fixture.Model.Morph.Add(shown);
            _fixture.Model.Morph.Add(hidden);
            _fixture.Model.ExpressionNode.Items.Add(new FakeMorphNodeItem(hidden));

            Assert.Equal(
                new[] { "1+1" },
                RunsOf(Located(ModelValidatePmx.HiddenMorphsInExpressionFrameName)));
        }

        [Fact]
        public void TheOffsetAndLimitCutTheRunsAndGiveTheNextOffset()
        {
            FakeBone bone = new FakeBone("腕");
            _fixture.Model.Bone.Add(bone);
            foreach (float weight in new[] { 0.5f, 1f, 0.5f, 1f, 0.5f })
            {
                FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
                vertex.Bone1 = bone;
                vertex.Weight1 = weight;
                _fixture.Model.Vertex.Add(vertex);
            }

            IDictionary<string, object> found = ComposedEditFixture.Value(
                _fixture.Call(
                    ModelValidatePmx.ToolName,
                    ComposedEditFixture.Arguments(
                        ComposedEditFixture.Given(
                            ModelValidatePmx.RunsName, ModelValidatePmx.UnnormalizedWeightsName),
                        ComposedEditFixture.Given(ModelValidatePmx.OffsetName, 1),
                        ComposedEditFixture.Given(ModelValidatePmx.LimitName, 1))));

            Assert.Equal(new[] { "2+1" }, RunsOf(found));
            Assert.Equal(3, found[ModelValidatePmx.RunsTotalName]);
            Assert.Equal(2, found[ModelValidatePmx.NextOffsetName]);
        }

        [Fact]
        public void RunsAreLeftOutWhenNotAsked()
        {
            IDictionary<string, object> found = Validated();

            Assert.False(found.ContainsKey(ModelValidatePmx.RunsName));
            Assert.False(found.ContainsKey(ModelValidatePmx.RunsTotalName));
        }

        [Fact]
        public void RunsNamingACategoryWithoutAPositionAreRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Called(ModelValidatePmx.RunsName, ModelValidatePmx.FoundName)));
        }

        [Fact]
        public void RunsThatAreNotACategoryNameAreRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Called(ModelValidatePmx.RunsName, true)));
        }

        [Fact]
        public void TheOffsetAndLimitAreRefusedWithoutRuns()
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Called(ModelValidatePmx.OffsetName, 0)));
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Called(ModelValidatePmx.LimitName, 1)));
        }

        [Fact]
        public void AnOffsetOrLimitBelowTheLeastIsRefused()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                ModelValidatePmx.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(
                        ModelValidatePmx.RunsName, ModelValidatePmx.UnsoundFacesName),
                    ComposedEditFixture.Given(ModelValidatePmx.LimitName, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private IDictionary<string, object> Called(string name, object value)
        {
            return _fixture.Call(
                ModelValidatePmx.ToolName,
                ComposedEditFixture.Arguments(ComposedEditFixture.Given(name, value)));
        }

        private IDictionary<string, object> Located(string name)
        {
            return ComposedEditFixture.Value(Called(ModelValidatePmx.RunsName, name));
        }

        private IDictionary<string, object> LocatedInHeldModel(string name)
        {
            return ComposedEditFixture.Value(
                _fixture.Call(
                    ModelValidatePmx.ToolName,
                    ComposedEditFixture.Arguments(
                        _fixture.HoldModel(),
                        ComposedEditFixture.Given(ModelValidatePmx.RunsName, name))));
        }

        private static string[] RunsOf(IDictionary<string, object> found)
        {
            return ((IEnumerable<object>)found[ModelValidatePmx.RunsName])
                .Cast<IDictionary<string, object>>()
                .Select(run => run["start"] + "+" + run["count"])
                .ToArray();
        }

        private IDictionary<string, object> Validated()
        {
            return ComposedEditFixture.Value(
                _fixture.Call(ModelValidatePmx.ToolName, ComposedEditFixture.Arguments()));
        }

        /// <summary>
        /// 題材のモデルをハンドルで預けて調べる。並びの外を指す要素はエディタの現在のPMXには
        /// 置けないので、ハンドルで持つPMXで調べる。
        /// </summary>
        private IDictionary<string, object> Held()
        {
            return ComposedEditFixture.Value(
                _fixture.Call(
                    ModelValidatePmx.ToolName, ComposedEditFixture.Arguments(_fixture.HoldModel())));
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
