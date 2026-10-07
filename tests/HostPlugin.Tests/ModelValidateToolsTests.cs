using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelValidateToolsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakeBone _center;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void ASoundModelIsReportedAsSound()
        {
            Sound();

            Assert.Equal(0, Validated()[ModelValidatePmx.FoundName]);
        }

        [Fact]
        public void AnEmptyModelHasNoNameNoCommentAndNoBones()
        {
            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.EmptyModelNameName]);
            Assert.Equal(1, found[PmxStateCheck.EmptyCommentName]);
            Assert.Equal(1, found[PmxStateCheck.NoBonesName]);
            Assert.Equal(3, found[ModelValidatePmx.FoundName]);
        }

        [Theory]
        [InlineData(-1, 1)]
        [InlineData(0, 0)]
        [InlineData(4, 0)]
        [InlineData(5, 1)]
        public void AnAdditionalUvCountOutsideZeroToFourIsCounted(int count, int expected)
        {
            Sound();
            _fixture.Model.Header.UVACount = count;

            Assert.Equal(expected, Validated()[PmxStateCheck.BadUvaCountName]);
        }

        [Fact]
        public void ANormalWithoutLengthOrWithoutAFiniteValueIsCounted()
        {
            Sound();
            _fixture.Model.Vertex[0].Normal = new V3(0f, 0f, 0f);
            _fixture.Model.Vertex[1].Normal = new V3(float.NaN, 0f, 0f);
            _fixture.Model.Vertex[2].Normal = new V3(0f, 3f, 0f);

            IDictionary<string, object> found = Located(PmxStateCheck.InvalidNormalsName);

            Assert.Equal(2, found[PmxStateCheck.InvalidNormalsName]);
            Assert.Equal(new[] { "0+2" }, RunsOf(found));
        }

        [Fact]
        public void AWeightSlotInUseWithoutABoneIsCounted()
        {
            Sound();
            ((FakeVertex)_fixture.Model.Vertex[0]).Bone1 = null;

            Assert.Equal(1, Validated()[PmxStateCheck.InvalidWeightsName]);
        }

        [Fact]
        public void AWeightOnABoneOutsideTheListIsCounted()
        {
            Sound();
            ((FakeVertex)_fixture.Model.Vertex[0]).Bone1 = new FakeBone("居ない");

            Assert.Equal(1, Held()[PmxStateCheck.InvalidWeightsName]);
        }

        [Fact]
        public void TheSlotsCheckedFollowTheNumberOfWeights()
        {
            Sound();
            FakeVertex two = (FakeVertex)_fixture.Model.Vertex[0];
            two.Weight1 = 0.5f;
            two.Weight2 = 0.5f;
            FakeVertex one = (FakeVertex)_fixture.Model.Vertex[1];
            one.Weight2 = 0f;

            Assert.Equal(1, Validated()[PmxStateCheck.InvalidWeightsName]);
        }

        [Fact]
        public void AnSdefVertexWithOneWeightIsCheckedAsBdef1()
        {
            Sound();
            ((FakeVertex)_fixture.Model.Vertex[0]).SDEF = true;

            Assert.Equal(0, Validated()[ModelValidatePmx.FoundName]);
        }

        [Fact]
        public void AQdefVertexIsNotCheckedForWeights()
        {
            Sound();
            FakeVertex vertex = (FakeVertex)_fixture.Model.Vertex[0];
            vertex.QDEF = true;
            vertex.Weight1 = 0.5f;
            vertex.Weight2 = 0.5f;

            Assert.Equal(0, Validated()[PmxStateCheck.InvalidWeightsName]);
        }

        [Fact]
        public void AnSdefVertexWithoutItsValuesOrItsTwoBonesIsCounted()
        {
            Sound();
            FakeVertex empty = (FakeVertex)_fixture.Model.Vertex[0];
            empty.SDEF = true;
            empty.Bone2 = _center;
            empty.Weight1 = 0.5f;
            empty.Weight2 = 0.5f;
            FakeVertex broken = (FakeVertex)_fixture.Model.Vertex[1];
            broken.SDEF = true;
            broken.Weight1 = 0.5f;
            broken.Weight2 = 0.5f;
            broken.SDEF_C = new V3(0f, 1f, 0f);

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.EmptySdefValuesName]);
            Assert.Equal(1, found[PmxStateCheck.SdefBadBoneReferencesName]);
            Assert.Equal(1, found[PmxStateCheck.InvalidWeightsName]);
        }

        [Fact]
        public void AVertexNoFaceUsesIsCounted()
        {
            Sound();
            _fixture.Model.Vertex.Add(Weighed(new FakeVertex(5f, 5f, 5f)));

            IDictionary<string, object> found = Located(PmxStateCheck.UnusedVerticesName);

            Assert.Equal(1, found[PmxStateCheck.UnusedVerticesName]);
            Assert.Equal(new[] { "3+1" }, RunsOf(found));
        }

        [Fact]
        public void AFaceOnAVertexOutsideTheListCountsEachReferenceAndTheFace()
        {
            Sound();
            IPXVertex[] corners = _fixture.Model.Vertex.ToArray();
            _fixture.Model.Material[0].Faces.Add(
                new FakeFace(corners[0], new FakeVertex(9f, 9f, 9f), new FakeVertex(8f, 8f, 8f)));

            IDictionary<string, object> found = Held();

            Assert.Equal(2, found[PmxStateCheck.BadFaceVertexReferencesName]);
            Assert.Equal(1, found[PmxStateCheck.BadFacesName]);
        }

        [Fact]
        public void AFaceThatRepeatsAVertexIsCountedUnlessTheMaterialDrawsOnlyLines()
        {
            Sound();
            IPXVertex[] corners = _fixture.Model.Vertex.ToArray();
            _fixture.Model.Material[0].Faces.Add(new FakeFace(corners[0], corners[1], corners[1]));
            FakeMaterial lines = new FakeMaterial("線") { PrimitiveType = PrimitiveType.Line };
            lines.Faces.Add(new FakeFace(corners[0], corners[2], corners[2]));
            _fixture.Model.Material.Add(lines);
            FakeMaterial edged = new FakeMaterial("縁") { PrimitiveType = PrimitiveType.Line, Edge = true };
            edged.Faces.Add(new FakeFace(corners[1], corners[2], corners[2]));
            _fixture.Model.Material.Add(edged);

            IDictionary<string, object> found = Located(PmxStateCheck.BadFacesName);

            Assert.Equal(2, found[PmxStateCheck.BadFacesName]);
            Assert.Equal(new[] { "1+1", "3+1" }, RunsOf(found));
        }

        [Fact]
        public void TheLaterOfTwoFacesWithTheSameVerticesInTheSameOrderIsCountedAcrossMaterials()
        {
            Sound();
            IPXVertex[] corners = _fixture.Model.Vertex.ToArray();
            FakeMaterial other = new FakeMaterial("別");
            other.Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
            other.Faces.Add(new FakeFace(corners[1], corners[2], corners[0]));
            _fixture.Model.Material.Add(other);

            IDictionary<string, object> found = Located(PmxStateCheck.DuplicateFacesName);

            Assert.Equal(1, found[PmxStateCheck.DuplicateFacesName]);
            Assert.Equal(new[] { "1+1" }, RunsOf(found));
        }

        [Fact]
        public void MaterialsWithoutANameWithARepeatedNameOrWithoutFacesAreCounted()
        {
            Sound();
            _fixture.Model.Material.Add(new FakeMaterial(string.Empty));
            _fixture.Model.Material.Add(new FakeMaterial("材質"));

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.UnnamedMaterialsName]);
            Assert.Equal(1, found[PmxStateCheck.DuplicateMaterialNamesName]);
            Assert.Equal(2, found[PmxStateCheck.MaterialsWithoutFacesName]);
        }

        [Fact]
        public void TextureFilesAreLookedForBesideTheModelFile()
        {
            string folder = Path.Combine(Path.GetTempPath(), "pmx-validate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "在る.png"), string.Empty);
                Sound();
                _fixture.Model.FilePath = Path.Combine(folder, "model.pmx");
                FakeMaterial material = (FakeMaterial)_fixture.Model.Material[0];
                material.Tex = "在る.png";
                material.Sphere = "無い.sph";
                material.SphereMode = SphereType.None;
                material.Toon = "toon03.bmp";
                FakeMaterial other = new FakeMaterial("別") { Tex = "無い.png", Toon = "無い.bmp" };
                other.Faces.Add(new FakeFace(
                    _fixture.Model.Vertex[0], _fixture.Model.Vertex[2], _fixture.Model.Vertex[1]));
                _fixture.Model.Material.Add(other);

                IDictionary<string, object> found = Validated();

                Assert.Equal(1, found[PmxStateCheck.MissingTexturesName]);
                Assert.Equal(1, found[PmxStateCheck.MissingSphereTexturesName]);
                Assert.Equal(1, found[PmxStateCheck.SphereModeNoneName]);
                Assert.Equal(1, found[PmxStateCheck.MissingToonTexturesName]);
                Assert.Equal(4, found[ModelValidatePmx.FoundName]);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void BonesWithoutANameOrWithARepeatedNameAreCounted()
        {
            Sound();
            _fixture.Model.Bone.Add(new FakeBone(string.Empty));
            _fixture.Model.Bone.Add(new FakeBone("センター"));

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.UnnamedBonesName]);
            Assert.Equal(1, found[PmxStateCheck.DuplicateBoneNamesName]);
        }

        [Fact]
        public void TheFirstBoneOfACycleOfParentsIsCounted()
        {
            Sound();
            FakeBone first = new FakeBone("一");
            FakeBone second = new FakeBone("二") { Parent = first };
            first.Parent = second;
            _fixture.Model.Bone.Add(first);
            _fixture.Model.Bone.Add(second);

            Assert.Equal(new[] { "1+1" }, RunsOf(Located(PmxStateCheck.CyclicBonesName)));
        }

        [Fact]
        public void AParentOutsideTheListOrItselfIsCounted()
        {
            Sound();
            FakeBone self = new FakeBone("自分");
            self.Parent = self;
            _fixture.Model.Bone.Add(new FakeBone("外") { Parent = new FakeBone("居ない") });
            _fixture.Model.Bone.Add(self);

            IDictionary<string, object> found = Held();

            Assert.Equal(2, found[PmxStateCheck.BadParentBonesName]);
            Assert.Equal(1, found[PmxStateCheck.CyclicBonesName]);
        }

        [Fact]
        public void ABoneOnALowerLevelThanItsParentIsCounted()
        {
            Sound();
            _center.Level = 2;
            _fixture.Model.Bone.Add(new FakeBone("靴") { Parent = _center });

            Assert.Equal(1, Validated()[PmxStateCheck.BonesDeformedBeforeParentName]);
        }

        [Fact]
        public void ABoneOnTheSameLevelBeforeItsParentInTheListIsCounted()
        {
            Sound();
            FakeBone parent = new FakeBone("足首");
            _fixture.Model.Bone.Add(new FakeBone("靴") { Parent = parent });
            _fixture.Model.Bone.Add(parent);

            Assert.Equal(1, Validated()[PmxStateCheck.BonesDeformedBeforeParentName]);
        }

        [Fact]
        public void ABoneDeformedBeforePhysicsUnderAParentDeformedAfterPhysicsIsCounted()
        {
            Sound();
            _center.IsAfterPhysics = true;
            _fixture.Model.Bone.Add(new FakeBone("飾り") { Parent = _center, Level = 3 });

            Assert.Equal(1, Validated()[PmxStateCheck.BonesDeformedBeforeParentName]);
        }

        [Fact]
        public void ABoneDeformedAfterItsParentIsNotCounted()
        {
            Sound();
            _center.Level = 2;
            _fixture.Model.Bone.Add(new FakeBone("靴") { Parent = _center, Level = 2 });
            _fixture.Model.Bone.Add(new FakeBone("飾り") { Parent = _center, IsAfterPhysics = true });

            Assert.Equal(0, Validated()[ModelValidatePmx.FoundName]);
        }

        [Fact]
        public void AnAppendParentOutsideTheListOrItselfOrDeformedLaterIsCounted()
        {
            Sound();
            FakeBone self = new FakeBone("自分");
            self.AppendParent = self;
            _center.Level = 1;
            _fixture.Model.Bone.Add(self);
            _fixture.Model.Bone.Add(new FakeBone("先") { AppendParent = _center });

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.BadAppendParentBonesName]);
            Assert.Equal(1, found[PmxStateCheck.BonesDeformedBeforeAppendParentName]);
        }

        [Fact]
        public void AnAppendWithoutAnAppendParentIsCounted()
        {
            Sound();
            _fixture.Model.Bone.Add(new FakeBone("付与") { IsAppendRotation = true });

            Assert.Equal(1, Validated()[PmxStateCheck.MissingAppendParentBonesName]);
        }

        [Fact]
        public void AToBoneOutsideTheListIsCounted()
        {
            Sound();
            _center.ToBone = new FakeBone("居ない");

            Assert.Equal(1, Held()[PmxStateCheck.BadToBonesName]);
        }

        [Fact]
        public void AnIkWithoutItsTargetOrWithALinkWithoutABoneIsCounted()
        {
            Sound();
            FakeBone ik = new FakeBone("IK") { IsIK = true };
            ik.IK.Target = _center;
            ik.IK.Links.Add(new FakeIkLink(null));
            _fixture.Model.Bone.Add(ik);
            _fixture.Model.Bone.Add(new FakeBone("的無し") { IsIK = true });

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.BadIkTargetsName]);
            Assert.Equal(1, found[PmxStateCheck.BadIkLinksName]);
        }

        [Fact]
        public void AVisibleBoneOutsideTheFramesAndABoneInTwoFramesAreCounted()
        {
            Sound();
            _fixture.Model.Bone.Add(new FakeBone("見える") { Visible = true });
            FakeNode frame = new FakeNode("体");
            frame.Items.Add(new FakeBoneNodeItem(_center));
            frame.Items.Add(new FakeBoneNodeItem(_center));
            _fixture.Model.Node.Add(frame);

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.BonesNotInFrameName]);
            Assert.Equal(1, found[PmxStateCheck.BonesInFramesTwiceName]);
        }

        [Fact]
        public void MorphsAreCheckedForTheirNamesOffsetsAndFrames()
        {
            Sound();
            Registered(new FakeMorph("笑い")).Offsets.Add(new FakeVertexMorphOffset(null));
            Registered(new FakeMorph("笑い"));
            Registered(new FakeMorph(string.Empty));
            FakeMorph painted = Registered(new FakeMorph("色", MorphKind.Material));
            painted.Offsets.Add(new FakeMaterialMorphOffset(null));
            _fixture.Model.ExpressionNode.Items.Add(new FakeMorphNodeItem(painted));
            _fixture.Model.Morph.Add(new FakeMorph("枠外") { Panel = 4 });

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.UnnamedMorphsName]);
            Assert.Equal(1, found[PmxStateCheck.DuplicateMorphNamesName]);
            Assert.Equal(1, found[PmxStateCheck.BadMorphOffsetsName]);
            Assert.Equal(1, found[PmxStateCheck.MorphsNotInFrameName]);
            Assert.Equal(1, found[PmxStateCheck.MorphsInFramesTwiceName]);
            Assert.Equal(5, found[ModelValidatePmx.FoundName]);
        }

        [Fact]
        public void FramesAreCheckedForTheirNamesAndTheirItems()
        {
            Sound();
            FakeNode unnamed = new FakeNode(string.Empty);
            unnamed.Items.Add(new FakeBoneNodeItem(null));
            FakeNode repeated = new FakeNode("Root");
            repeated.Items.Add(new FakeMorphNodeItem(null));
            _fixture.Model.Node.Add(unnamed);
            _fixture.Model.Node.Add(repeated);

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.UnnamedFramesName]);
            Assert.Equal(1, found[PmxStateCheck.DuplicateFrameNamesName]);
            Assert.Equal(1, found[PmxStateCheck.FramesWithBadBonesName]);
            Assert.Equal(1, found[PmxStateCheck.FramesWithBadMorphsName]);
        }

        [Fact]
        public void ABoneInTheExpressionFrameIsCounted()
        {
            Sound();
            _fixture.Model.ExpressionNode.Items.Add(new FakeBoneNodeItem(_center));

            Assert.Equal(1, Validated()[PmxStateCheck.BoneInExpressionFrameName]);
        }

        [Fact]
        public void BodiesAreCheckedForTheirNamesAndTheirBone()
        {
            Sound();
            _fixture.Model.Body.Add(new FakeBody(string.Empty));
            _fixture.Model.Body.Add(new FakeBody("胴") { Bone = new FakeBone("居ない") });
            _fixture.Model.Body.Add(new FakeBody("胴"));

            IDictionary<string, object> found = Held();

            Assert.Equal(1, found[PmxStateCheck.UnnamedBodiesName]);
            Assert.Equal(1, found[PmxStateCheck.DuplicateBodyNamesName]);
            Assert.Equal(1, found[PmxStateCheck.BodiesWithBadBoneName]);
        }

        [Fact]
        public void JointsAreCheckedForTheirNamesAndBothBodies()
        {
            Sound();
            FakeBody body = new FakeBody("胴");
            _fixture.Model.Body.Add(body);
            _fixture.Model.Joint.Add(new FakeJoint(string.Empty) { BodyA = body, BodyB = body });
            _fixture.Model.Joint.Add(new FakeJoint("繋ぎ") { BodyB = body });
            _fixture.Model.Joint.Add(new FakeJoint("繋ぎ") { BodyA = body });

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.UnnamedJointsName]);
            Assert.Equal(1, found[PmxStateCheck.DuplicateJointNamesName]);
            Assert.Equal(1, found[PmxStateCheck.JointsWithBadBodyAName]);
            Assert.Equal(1, found[PmxStateCheck.JointsWithBadBodyBName]);
        }

        [Fact]
        public void SoftBodiesCountEachBadAnchorAndEachPinOnAnAnchor()
        {
            Sound();
            IPXVertex[] corners = _fixture.Model.Vertex.ToArray();
            FakeBody body = new FakeBody("胴");
            _fixture.Model.Body.Add(body);
            FakeVertex away = Weighed(new FakeVertex(7f, 7f, 7f));
            _fixture.Model.Vertex.Add(away);
            FakeMaterial other = new FakeMaterial("別");
            other.Faces.Add(new FakeFace(away, corners[1], corners[0]));
            _fixture.Model.Material.Add(other);
            FakeSoftBody soft = new FakeSoftBody(string.Empty) { Material = _fixture.Model.Material[0] };
            soft.Anchors.Add(new FakeSoftBodyAnchor(null, null));
            soft.Anchors.Add(new FakeSoftBodyAnchor(null, corners[0]));
            soft.Anchors.Add(new FakeSoftBodyAnchor(body, away));
            soft.Pins.Add(corners[0]);
            soft.Pins.Add(corners[0]);
            _fixture.Model.SoftBody.Add(soft);
            _fixture.Model.SoftBody.Add(new FakeSoftBody("布"));
            _fixture.Model.SoftBody.Add(new FakeSoftBody("布"));

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[PmxStateCheck.UnnamedSoftBodiesName]);
            Assert.Equal(1, found[PmxStateCheck.DuplicateSoftBodyNamesName]);
            Assert.Equal(2, found[PmxStateCheck.SoftBodiesWithBadBodyName]);
            Assert.Equal(1, found[PmxStateCheck.SoftBodiesWithBadVertexName]);
            Assert.Equal(2, found[PmxStateCheck.SoftBodiesPinnedOnAnchorName]);
            Assert.Equal(1, found[PmxStateCheck.SoftBodiesOutsideMaterialName]);
        }

        [Fact]
        public void ASoftBodyMaterialOutsideTheListIsCounted()
        {
            Sound();
            _fixture.Model.SoftBody.Add(new FakeSoftBody("布") { Material = new FakeMaterial("居ない") });

            Assert.Equal(1, Held()[PmxStateCheck.SoftBodiesWithBadMaterialName]);
        }

        [Fact]
        public void AVertexWhoseWeightsDoNotAddUpToOneIsCounted()
        {
            Sound();
            ((FakeVertex)_fixture.Model.Vertex[0]).Weight1 = 0.5f;

            IDictionary<string, object> found = Validated();

            Assert.Equal(1, found[ModelValidatePmx.UnnormalizedWeightsName]);
            Assert.Equal(1, found[ModelValidatePmx.FoundName]);
        }

        [Fact]
        public void AHiddenMorphOnTheExpressionNodeIsCounted()
        {
            Sound();
            Registered(new FakeMorph("隠し")).Panel = 0;
            Registered(new FakeMorph("表示"));

            IDictionary<string, object> found = Located(ModelValidatePmx.HiddenMorphsInExpressionFrameName);

            Assert.Equal(1, found[ModelValidatePmx.HiddenMorphsInExpressionFrameName]);
            Assert.Equal(1, found[ModelValidatePmx.FoundName]);
            Assert.Equal(new[] { "0+1" }, RunsOf(found));
        }

        [Fact]
        public void LookingDoesNotChangeTheModel()
        {
            Sound();
            _fixture.Model.Vertex[0].Normal = new V3(0f, 3f, 0f);

            Validated();

            Assert.Equal(3f, _fixture.Model.Vertex[0].Normal.Y);
            Assert.Single(_fixture.Model.Material[0].Faces);
        }

        [Fact]
        public void TheOffsetAndLimitCutTheRunsAndGiveTheNextOffset()
        {
            Sound();
            for (int at = 0; at < 3; at++)
            {
                _fixture.Model.Vertex[at].Normal = at % 2 == 0 ? new V3(0f, 0f, 0f) : new V3(0f, 1f, 0f);
            }

            FakeVertex extra = Weighed(new FakeVertex(5f, 5f, 5f));
            extra.Normal = new V3(0f, 0f, 0f);
            _fixture.Model.Vertex.Add(extra);
            _fixture.Model.Material[0].Faces.Add(new FakeFace(extra, _fixture.Model.Vertex[0], _fixture.Model.Vertex[1]));

            IDictionary<string, object> found = ComposedEditFixture.Value(
                _fixture.Call(
                    ModelValidatePmx.ToolName,
                    ComposedEditFixture.Arguments(
                        ComposedEditFixture.Given(ModelValidatePmx.RunsName, PmxStateCheck.InvalidNormalsName),
                        ComposedEditFixture.Given(ModelValidatePmx.OffsetName, 1),
                        ComposedEditFixture.Given(ModelValidatePmx.LimitName, 1))));

            Assert.Equal(new[] { "2+2" }, RunsOf(found));
            Assert.Equal(2, found[ModelValidatePmx.RunsTotalName]);
            Assert.False(found.ContainsKey(ModelValidatePmx.NextOffsetName));
        }

        [Fact]
        public void RunsAreLeftOutWhenNotAsked()
        {
            IDictionary<string, object> found = Validated();

            Assert.False(found.ContainsKey(ModelValidatePmx.RunsName));
            Assert.False(found.ContainsKey(ModelValidatePmx.RunsTotalName));
        }

        [Theory]
        [InlineData("found")]
        [InlineData("emptyModelName")]
        public void RunsNamingACategoryWithoutAPositionAreRefused(string name)
        {
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Called(ModelValidatePmx.RunsName, name)));
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
                    ComposedEditFixture.Given(ModelValidatePmx.RunsName, PmxStateCheck.BadFacesName),
                    ComposedEditFixture.Given(ModelValidatePmx.LimitName, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private void Sound()
        {
            FakeModelInfo info = (FakeModelInfo)_fixture.Model.ModelInfo;
            info.ModelName = "モデル";
            info.Comment = "コメント";
            _center = new FakeBone("センター");
            _fixture.Model.Bone.Add(_center);
            FakeVertex[] corners =
            {
                Weighed(new FakeVertex(0f, 0f, 0f)),
                Weighed(new FakeVertex(1f, 0f, 0f)),
                Weighed(new FakeVertex(0f, 1f, 0f)),
            };
            foreach (FakeVertex corner in corners)
            {
                _fixture.Model.Vertex.Add(corner);
            }

            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
            _fixture.Model.Material.Add(material);
        }

        private FakeVertex Weighed(FakeVertex vertex)
        {
            vertex.Bone1 = _center;
            vertex.Weight1 = 1f;

            return vertex;
        }

        private FakeMorph Registered(FakeMorph morph)
        {
            morph.Panel = 4;
            _fixture.Model.Morph.Add(morph);
            _fixture.Model.ExpressionNode.Items.Add(new FakeMorphNodeItem(morph));

            return morph;
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
    }
}
