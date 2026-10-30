using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelPoseVerticesToolsTests : IDisposable
    {
        private const float Within = 0.0001f;

        private static readonly float Half = (float)Math.Sqrt(0.5);

        private static readonly float[][] GrantRigAtRest =
        {
            new[] { 2f, 1.75f, 0f, 1f, 1f, 0f },
            new[] { -1f, 0.75f, 1.25f, 0.25f, -0.5f, -0.5f },
            new[] { -0.25f, 1.25f, 0f, 0.5f, 0.25f, 1f },
            new[] { -0.5f, 1.25f, 1f, -0.25f, -0.5f, -1f },
            new[] { 1.25f, 0.75f, 1.5f, 0.5f, -1f, 0.25f },
            new[] { 0f, 0.25f, -1f, 0.5f, 0.25f, 0.5f },
            new[] { -2.5f, 1.25f, -0.75f, 1f, 0.25f, -1f },
            new[] { 0.75f, 1.25f, 0f, -0.5f, 0.25f, -1f },
            new[] { 1.25f, 0.5f, 0.75f, 0f, 0.25f, 1f },
            new[] { 0.5f, 1.75f, -0.25f, 0.5f, -0.25f, 0f },
            new[] { 0.25f, 0.5f, 0.25f, 0f, 0.5f, -0.25f },
            new[] { 0f, 1.25f, 0.75f, -1f, 0.5f, -0.25f },
            new[] { -0.25f, 1.75f, -0.5f, 0.25f, -0.25f, -1f },
            new[] { 0.25f, 1.75f, 1f, -1f, 0f, -1f },
            new[] { 1f, 0.5f, 1f, 0f, -1f, 0.25f },
            new[] { -0.5f, 0.5f, 0.5f, -1f, -1f, -1f },
            new[] { 2f, 1.75f, -0.25f, -0.5f, 0.5f, 0.25f },
        };

        private static readonly float[][] GrantRigFullyPosed =
        {
            new[] { 1.4326139f, 0.6018717f, -0.2991268f, 0.741752f, 0.1005689f, 0.6630911f },
            new[] { -0.5217021f, 0.4656044f, 1.4853276f, 0.1081035f, -0.1484379f, -0.9829953f },
            new[] { -0.3837163f, 1.3834936f, -0.2412161f, 0.9958474f, -0.075771f, 0.0504656f },
            new[] { -0.1282486f, 1.75f, 1.2189622f, -0.625418f, -0.4364358f, -0.64682f },
            new[] { 2.3345976f, -0.0253411f, 0.9730996f, -0.5536837f, -0.8318946f, 0.0372256f },
            new[] { -0.8466098f, 1.6021309f, -0.6535779f, 0.9241226f, 0.3816817f, 0.0177925f },
            new[] { -2.8040744f, 0.7550616f, 0.6282318f, -0.4953993f, 0.2976492f, -0.8160787f },
            new[] { 1.2259424f, 1.8013716f, -0.643016f, -0.4296253f, 0.8582589f, 0.2807378f },
            new[] { 1.7244736f, 0.2307813f, -0.6675652f, 0.2405402f, 0.2509034f, 0.9376502f },
            new[] { 0.1700618f, 2.0422716f, -0.691686f, 0.29755f, -0.4666329f, -0.8328972f },
            new[] { 0.877569f, 0.9201519f, -0.0911314f, 0.4325523f, 0.9000841f, -0.0524134f },
            new[] { 0.4185635f, 1.0389378f, 0.4904639f, -0.6257212f, 0.4115721f, 0.6626321f },
            new[] { -0.3185638f, 2.2795275f, 0.1110983f, -0.4247825f, 0.2753246f, -0.862413f },
            new[] { -0.0127043f, 0.1888383f, 0.2261075f, -0.6647602f, 0.7369942f, -0.1222026f },
            new[] { 1.3205607f, 0.353087f, 0.4995508f, -0.7283442f, -0.6713392f, 0.1371801f },
            new[] { -0.592809f, 0.4234271f, 0.1466941f, -0.9637729f, -0.0121559f, -0.266447f },
            new[] { 2.1309601f, 1.119826f, -0.3363029f, -0.4009307f, 0.3858342f, 0.830895f },
        };

        private static readonly float[][] GrantRigPartlyPosed =
        {
            new[] { -1.0521855f, 2.2254258f, 1.02257f, 0.5588763f, -0.0578731f, -0.8272291f },
            new[] { -0.7540993f, 0.8882148f, 0.3176294f, 0.8029665f, -0.150933f, 0.5765969f },
            new[] { -0.1831245f, 1.1271704f, 1.0432847f, -0.3504436f, 0.0211308f, 0.9363454f },
            new[] { 0.2674646f, 1.1671865f, 1.0637893f, -0.6202778f, -0.3830104f, -0.6845133f },
            new[] { 1.8403995f, 0.1321425f, 0.1781047f, 0.0453397f, -0.5431977f, 0.8383797f },
            new[] { 1.0274532f, 0.3406852f, 0.3915188f, -0.9596717f, 0.2146305f, 0.1815597f },
            new[] { -1.3296602f, 1.1725832f, 0.168469f, 0.963445f, 0.25839f, 0.07077f },
            new[] { -1.6121009f, 1.4479943f, 1.5534697f, -0.554314f, 0.6357088f, -0.5372246f },
            new[] { -1.4546436f, 0.7393215f, 0.1539299f, -0.3734052f, 0.1415373f, -0.9168074f },
            new[] { 0.1477402f, 1.5535856f, 1.1073882f, 0.4606822f, -0.3881316f, 0.7982016f },
            new[] { 0.0724228f, 0.5560443f, -0.0880798f, 0.2046265f, 0.835976f, -0.5091877f },
            new[] { -0.296526f, 1.5717789f, 0.1333809f, 0.4839691f, 0.505456f, -0.7143446f },
            new[] { -0.0959507f, 1.9861008f, 0.1983773f, 0.3150465f, -0.9381491f, -0.1436036f },
            new[] { -0.4540507f, 2.7381927f, 2.1338134f, -0.5373754f, 0.8194926f, -0.1991471f },
            new[] { 0.6260876f, 1.1430201f, 0.7487855f, -0.1447092f, 0.5140863f, 0.8454434f },
            new[] { 1.507836f, 1.8520693f, 1.1820608f, 0.8315915f, 0.5072422f, 0.2261878f },
            new[] { -0.045825f, 1.3185348f, -0.5596817f, 0.1944694f, 0.9429933f, 0.2700838f },
        };

        private static readonly float[][] OrderRigPosed =
        {
            new[] { 1.1661304f, 0.8330307f, 0.2805254f, 0.6195549f, -0.2140361f, 0.7552087f },
            new[] { 0.2156042f, 2.0092036f, 0.5996351f, 0.6563654f, -0.7171191f, -0.2343601f },
            new[] { 1.1286975f, -0.3033029f, -1.0237074f, 0.1098707f, 0.59548f, 0.7958216f },
            new[] { 2.5078633f, 1.3549908f, -1.3536515f, -0.6840913f, -0.713794f, 0.1500573f },
            new[] { 1.3454168f, 1.25f, 1.1544486f, -0.2567913f, -0.6963106f, -0.6702311f },
            new[] { 1.3507524f, 1.2694717f, -0.5229574f, 0.701169f, -0.6156024f, 0.359716f },
            new[] { 2.9778982f, 0.832188f, 0.1895347f, -0.7262385f, 0.1371664f, -0.6736194f },
        };

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void ABoneTurnsAndMovesItsVertexAboutItsRestPosition()
        {
            FakeBone bone = Bone(0f, 1f, 0f);
            FakeVertex vertex = Vertex(1f, 1f, 0f, bone, 1f, 0f, 0f);

            IDictionary<string, object> value = Pose(new[] { Entry(0, Turn(90), new[] { 1.0, 0.0, 0.0 }) }, 0);

            Assert.Equal(1, value["updated"]);
            AssertSpot(At(vertex).Position, 1f, 2f, 0f);
            AssertSpot(At(vertex).Normal, 0f, 1f, 0f);
        }

        [Fact]
        public void AQuaternionOfAnyLengthTurnsLikeItsUnitDirection()
        {
            FakeBone bone = Bone(0f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 0f, 0f, bone, 3f, 0f, 0f);
            double[] longer = Turn(90).Select(c => c * 4d).ToArray();

            Pose(new[] { Entry(0, longer, null) }, 0);

            AssertSpot(At(vertex).Position, 0f, 1f, 0f);
            AssertSpot(At(vertex).Normal, 0f, 1f, 0f);
        }

        [Fact]
        public void AChildFollowsTheTurnOfItsParent()
        {
            FakeBone parent = Bone(0f, 0f, 0f);
            FakeBone child = Bone(1f, 0f, 0f, parent);
            FakeVertex vertex = Vertex(2f, 0f, 0f, child, 1f, 0f, 0f);

            Pose(new[] { Entry(0, Turn(90), null) }, 0);

            AssertSpot(At(vertex).Position, 0f, 2f, 0f);
        }

        [Fact]
        public void AParentOrGrantorAfterItsFollowerInTheBoneListIsEvaluatedFirst()
        {
            FakeBone follower = Bone(1f, 0f, 0f);
            FakeBone granted = Bone(0f, 0f, 0f);
            FakeBone parent = Bone(0f, 0f, 0f);
            FakeBone grantor = Bone(0f, 0f, 0f);
            follower.Parent = parent;
            granted.IsAppendRotation = true;
            granted.AppendParent = grantor;
            granted.AppendRatio = 1f;
            FakeVertex below = Vertex(2f, 0f, 0f, follower, 1f, 0f, 0f);
            FakeVertex above = Vertex(1f, 0f, 0f, granted, 1f, 0f, 0f);

            Pose(new[] { Entry(2, Turn(90), null), Entry(3, Turn(90), null) }, 0, 1);

            AssertSpot(At(below).Position, 0f, 2f, 0f);
            AssertSpot(At(above).Position, 0f, 1f, 0f);
        }

        [Fact]
        public void ABoneChainThatLoopsBackToItselfIsRefusedAndNothingIsWritten()
        {
            FakeBone first = Bone(0f, 0f, 0f);
            FakeBone second = Bone(0f, 0f, 0f, first);
            first.Parent = second;
            FakeBone free = Bone(0f, 0f, 0f);
            FakeVertex looped = Vertex(1f, 0f, 0f, first, 1f, 0f, 0f);
            FakeVertex clear = Vertex(1f, 0f, 0f, free, 1f, 0f, 0f);

            IDictionary<string, object> refused = _fixture.Call(
                ModelPoseVertices.ToolName, Arguments(new[] { Entry(2, Turn(90), null) }, new[] { 0, 1 }));
            IDictionary<string, object> value = Pose(new[] { Entry(2, Turn(90), null) }, 1);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(refused));
            AssertSpot(At(looped).Position, 1f, 0f, 0f);
            Assert.Equal(1, value["updated"]);
            AssertSpot(At(clear).Position, 0f, 1f, 0f);
        }

        [Fact]
        public void LinearWeightsAreDividedByTheirSumAndEmptySlotsAreSkipped()
        {
            FakeBone first = Bone(0f, 0f, 0f);
            FakeBone second = Bone(0f, 0f, 0f);
            FakeVertex pair = Vertex(0f, 0f, 0f, first, 0f, 1f, 0f);
            pair.Bone2 = second;
            pair.Weight1 = 0.25f;
            pair.Weight2 = 0.75f;
            FakeVertex short4 = Vertex(0f, 0f, 0f, first, 0f, 1f, 0f);
            short4.Weight1 = 0.25f;
            short4.Bone2 = second;
            short4.Weight2 = 0.25f;
            FakeVertex hole = Vertex(0f, 0f, 0f, first, 0f, 1f, 0f);
            hole.Weight1 = 0.25f;
            hole.Bone2 = second;
            hole.Weight2 = 0.25f;
            hole.Weight3 = 0.5f;

            Pose(
                new[] { Entry(0, null, new[] { 2.0, 0.0, 0.0 }), Entry(1, null, new[] { 0.0, 4.0, 0.0 }) },
                0, 1, 2);

            AssertSpot(At(pair).Position, (0.25f * 2f) + (0.75f * 0f), (0.25f * 0f) + (0.75f * 4f), 0f);
            AssertSpot(At(short4).Position, (0.25f * 2f) / 0.5f, (0.25f * 4f) / 0.5f, 0f);
            AssertSpot(At(hole).Position, (0.25f * 2f) / 0.5f, (0.25f * 4f) / 0.5f, 0f);
        }

        [Fact]
        public void SdefTakesItsCenterFromTheAxisBetweenItsBonesAndNotFromTheFile()
        {
            FakeBone first = Bone(0f, 0f, 0f);
            FakeBone second = Bone(2f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 1f, 0f, first, 0f, 1f, 0f);
            vertex.Bone2 = second;
            vertex.Weight1 = 0.5f;
            vertex.Weight2 = 0.5f;
            vertex.SDEF = true;
            vertex.SDEF_C = new V3(9f, 9f, 9f);
            vertex.SDEF_R0 = new V3(4f, 0f, 0f);
            vertex.SDEF_R1 = new V3(-4f, 0f, 0f);

            Pose(new[] { Entry(0, Turn(90), null) }, 0);

            AssertSpot(At(vertex).Position, -0.5f - Half, 1.5f + Half, 0f);
            AssertSpot(At(vertex).Normal, -Half, Half, 0f);
            AssertSpot(At(vertex).SDEF_C, 9f, 9f, 9f);
        }

        [Fact]
        public void QdefBlendsDualQuaternionsAndNotMatrices()
        {
            FakeBone first = Bone(0f, 0f, 0f);
            FakeBone second = Bone(0f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 0f, 0f, first, 0f, 1f, 0f);
            vertex.Bone2 = second;
            vertex.Weight1 = 0.5f;
            vertex.Weight2 = 0.5f;
            vertex.QDEF = true;

            Pose(new[] { Entry(0, Turn(60), new[] { 2.0, 0.0, 0.0 }), Entry(1, Turn(-60), null) }, 0);

            AssertSpot(At(vertex).Position, 1f + 1f, 0f - (float)Math.Tan(Math.PI / 6d), 0f);
            AssertSpot(At(vertex).Normal, 0f, 1f, 0f);
        }

        [Fact]
        public void ARotationGrantTurnsByTheRatioOfTheGrantorsRotationAfterTheOwnRotation()
        {
            FakeBone grantor = Bone(0f, 0f, 0f);
            FakeBone half = Grant(Bone(0f, 0f, 0f), grantor, 0.5f, rotation: true);
            FakeBone reversed = Grant(Bone(0f, 0f, 0f), grantor, -1f, rotation: true);
            FakeBone both = Grant(Bone(0f, 0f, 0f), grantor, 0.5f, rotation: true);
            FakeVertex onHalf = Vertex(1f, 0f, 0f, half, 1f, 0f, 0f);
            FakeVertex onReversed = Vertex(1f, 0f, 0f, reversed, 1f, 0f, 0f);
            FakeVertex onBoth = Vertex(1f, 0f, 0f, both, 1f, 0f, 0f);

            Pose(new[] { Entry(0, Turn(90), null), Entry(3, Turn(90), null) }, 0, 1, 2);

            AssertSpot(At(onHalf).Position, Half, Half, 0f);
            AssertSpot(At(onReversed).Position, 0f, -1f, 0f);
            double both135 = Math.PI * 135d / 180d;
            AssertSpot(At(onBoth).Position, (float)Math.Cos(both135), (float)Math.Sin(both135), 0f);
        }

        [Fact]
        public void ATranslationGrantMovesByTheRatioOfTheGrantorsMoveAndAddsTheOwnMove()
        {
            FakeBone grantor = Bone(0f, 0f, 0f);
            FakeBone follower = Grant(Bone(0f, 0f, 0f), grantor, 2f, translation: true);
            FakeVertex vertex = Vertex(0f, 0f, 0f, follower, 0f, 1f, 0f);

            Pose(new[] { Entry(0, null, new[] { 1.0, 0.0, 0.0 }), Entry(1, null, new[] { 0.0, 1.0, 0.0 }) }, 0);

            AssertSpot(At(vertex).Position, 2f, 1f, 0f);
        }

        [Fact]
        public void ALocalGrantUsesTheGrantorsWorldRotationAndWorldMove()
        {
            FakeBone root = Bone(0f, 0f, 0f);
            FakeBone grantor = Bone(1f, 0f, 0f, root);
            FakeBone follower = Grant(Bone(5f, 0f, 0f), grantor, 1f, rotation: true, translation: true);
            follower.IsAppendLocal = true;
            FakeBone plain = Grant(Bone(5f, 0f, 0f), grantor, 1f, rotation: true, translation: true);
            FakeVertex local = Vertex(6f, 0f, 0f, follower, 1f, 0f, 0f);
            FakeVertex direct = Vertex(6f, 0f, 0f, plain, 1f, 0f, 0f);

            Pose(new[] { Entry(0, Turn(90), null), Entry(1, Turn(90), null) }, 0, 1);

            AssertSpot(At(local).Position, 6f - 3f, 1f, 0f);
            AssertSpot(At(local).Normal, -1f, 0f, 0f);
            AssertSpot(At(direct).Position, 5f + 0f, 1f, 0f);
        }

        [Fact]
        public void OnlyTheChosenVerticesAreWrittenAndEachOneWithoutWeightsIsCounted()
        {
            FakeBone bone = Bone(0f, 0f, 0f);
            FakeVertex chosen = Vertex(1f, 0f, 0f, bone, 1f, 0f, 0f);
            FakeVertex other = Vertex(1f, 0f, 0f, bone, 1f, 0f, 0f);
            FakeVertex bare = Vertex(1f, 0f, 0f, null, 1f, 0f, 0f);

            IDictionary<string, object> value = Pose(new[] { Entry(0, Turn(90), null) }, 0, 2);

            Assert.Equal(2, value["updated"]);
            AssertSpot(At(chosen).Position, 0f, 1f, 0f);
            AssertSpot(At(other).Position, 1f, 0f, 0f);
            AssertSpot(At(bare).Position, 1f, 0f, 0f);
        }

        [Fact]
        public void AMaterialPicksTheVerticesOfItsFaces()
        {
            FakeBone bone = Bone(0f, 0f, 0f);
            FakeVertex inside = Vertex(1f, 0f, 0f, bone, 1f, 0f, 0f);
            FakeVertex outside = Vertex(1f, 0f, 0f, bone, 1f, 0f, 0f);
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(inside, inside, inside));
            _fixture.Model.Material.Add(material);

            IDictionary<string, object> value = Pose(new[] { Entry(0, Turn(90), null) }, Material(0));

            Assert.Equal(1, value["updated"]);
            AssertSpot(At(inside).Position, 0f, 1f, 0f);
            AssertSpot(At(outside).Position, 1f, 0f, 0f);
        }

        [Fact]
        public void TheModelOfAHandleIsPosedAndTheCurrentModelIsNotTouched()
        {
            FakeBone current = Bone(0f, 0f, 0f);
            FakeVertex untouched = Vertex(1f, 0f, 0f, current, 1f, 0f, 0f);
            FakePmx copy = new FakePmx();
            FakeBone bone = new FakeBone("ボーン");
            copy.Bone.Add(bone);
            FakeVertex vertex = new FakeVertex(1f, 0f, 0f)
            {
                Normal = new V3(1f, 0f, 0f),
                Bone1 = bone,
                Weight1 = 1f,
            };
            copy.Vertex.Add(vertex);
            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });

            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                ModelPoseVertices.ToolName,
                Arguments(new[] { Entry(0, Turn(90), null) }, 0, (long)handle)));

            Assert.Equal(1, value["updated"]);
            AssertSpot(vertex.Position, 0f, 1f, 0f);
            AssertSpot(At(untouched).Position, 1f, 0f, 0f);
            AssertSpot(At(untouched).Normal, 1f, 0f, 0f);
        }

        [Fact]
        public void ANormalIsLeftAtLengthOne()
        {
            FakeBone bone = Bone(0f, 0f, 0f);
            FakeVertex vertex = Vertex(0f, 0f, 0f, bone, 0f, 3f, 0f);

            Pose(new[] { Entry(0, Turn(90), null) }, 0);

            AssertSpot(At(vertex).Normal, -1f, 0f, 0f);
        }

        [Fact]
        public void AMalformedLaterBoneEntryIsRefusedAsAnInvalidArgument()
        {
            Bone(0f, 0f, 0f);
            Bone(0f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 0f, 0f, _fixture.Model.Bone[0], 1f, 0f, 0f);
            Dictionary<string, object> strange = Entry(1, null, null);
            strange["scale"] = new object[] { 1.0, 1.0, 1.0 };

            IDictionary<string, object> envelope = _fixture.Call(
                ModelPoseVertices.ToolName,
                Arguments(new[] { Entry(0, Turn(90), null), strange }, 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            AssertSpot(At(vertex).Position, 1f, 0f, 0f);
        }

        [Fact]
        public void APositionThatOnlyOverflowsAsASinglePrecisionNumberIsRefusedAndNothingIsWritten()
        {
            FakeBone parent = Bone(0f, 0f, 0f);
            FakeBone child = Bone(0f, 0f, 0f, parent);
            FakeVertex near = Vertex(1f, 0f, 0f, parent, 1f, 0f, 0f);
            FakeVertex far = Vertex(0f, 0f, 0f, child, 1f, 0f, 0f);

            IDictionary<string, object> envelope = _fixture.Call(
                ModelPoseVertices.ToolName,
                Arguments(
                    new[] { Entry(0, null, new[] { 3e38, 0.0, 0.0 }), Entry(1, null, new[] { 3e38, 0.0, 0.0 }) },
                    new[] { 0, 1 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            AssertSpot(At(near).Position, 1f, 0f, 0f);
            AssertSpot(At(far).Position, 0f, 0f, 0f);
        }

        [Fact]
        public void TurnsPastAQuarterOnPartOfTheGrantRigLandWhereTheEditorBakesThem()
        {
            GrantRig();

            IDictionary<string, object> value = Pose(GrantRigPartPose(), Enumerable.Range(0, 17).ToArray());

            Assert.Equal(17, value["updated"]);
            AssertLanded(GrantRigPartlyPosed, 0);
        }

        [Fact]
        public void QuaternionsOfMixedLengthsOnTheGrantRigLandWhereTheirUnitDirectionsDo()
        {
            GrantRig();

            IDictionary<string, object> value = Pose(GrantRigLongFullPose(), Enumerable.Range(0, 17).ToArray());

            Assert.Equal(17, value["updated"]);
            AssertLanded(GrantRigFullyPosed, 0);
        }

        [Fact]
        public void AMaterialOnTheGrantRigPosesTheVerticesOfItsFaceAndLeavesTheRest()
        {
            GrantRig();
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(
                new FakeFace(_fixture.Model.Vertex[0], _fixture.Model.Vertex[1], _fixture.Model.Vertex[2]));
            _fixture.Model.Material.Add(material);

            IDictionary<string, object> value = Pose(GrantRigPartPose(), Material(0));

            Assert.Equal(3, value["updated"]);
            AssertLanded(GrantRigPartlyPosed.Take(3).ToArray(), 0);
            AssertLanded(GrantRigAtRest.Skip(3).ToArray(), 3);
        }

        [Fact]
        public void TheOrderRigWithParentsAndGrantorsAfterTheirFollowersLandsWhereTheEditorBakesIt()
        {
            OrderRig();

            IDictionary<string, object> value = Pose(OrderRigPose(), Enumerable.Range(0, 7).ToArray());

            Assert.Equal(7, value["updated"]);
            AssertLanded(OrderRigPosed, 0);
        }

        [Theory]
        [MemberData(nameof(Refused))]
        public void ARefusedCallWritesNothing(string reason, string code)
        {
            FakeBone bone = Bone(0f, 0f, 0f);
            FakeVertex vertex = Vertex(1f, 0f, 0f, bone, 1f, 0f, 0f);
            Dictionary<string, object> good = Entry(0, Turn(90), null);
            Dictionary<string, object> arguments = Arguments(new[] { good }, 0);
            Refuse(reason, arguments, good);

            IDictionary<string, object> envelope = _fixture.Call(ModelPoseVertices.ToolName, arguments);

            Assert.Equal(code, ComposedEditFixture.Code(envelope));
            AssertSpot(At(vertex).Position, 1f, 0f, 0f);
            AssertSpot(At(vertex).Normal, 1f, 0f, 0f);
        }

        public static IEnumerable<object[]> Refused()
        {
            yield return new object[] { "boneOutside", ToolEnvelope.IndexOutOfRange };
            yield return new object[] { "vertexOutside", ToolEnvelope.IndexOutOfRange };
            yield return new object[] { "zeroRotation", ToolEnvelope.InvalidArgument };
            yield return new object[] { "shortRotation", ToolEnvelope.InvalidArgument };
            yield return new object[] { "shortTranslation", ToolEnvelope.InvalidArgument };
            yield return new object[] { "twice", ToolEnvelope.InvalidArgument };
            yield return new object[] { "noBones", ToolEnvelope.InvalidArgument };
            yield return new object[] { "emptyBones", ToolEnvelope.InvalidArgument };
            yield return new object[] { "noTarget", ToolEnvelope.InvalidArgument };
            yield return new object[] { "strangeMember", ToolEnvelope.InvalidArgument };
            yield return new object[] { "nullRotation", ToolEnvelope.InvalidArgument };
            yield return new object[] { "nullTranslation", ToolEnvelope.InvalidArgument };
            yield return new object[] { "noHandle", ToolEnvelope.InvalidHandle };
            yield return new object[] { "wrongHandle", ToolEnvelope.InvalidHandle };
        }

        private void Refuse(string reason, Dictionary<string, object> arguments, Dictionary<string, object> good)
        {
            Dictionary<string, object> bad = Entry(0, null, null);
            switch (reason)
            {
                case "boneOutside":
                    arguments["bones"] = new object[] { good, Entry(1, null, null) };
                    break;
                case "vertexOutside":
                    arguments["indices"] = new object[] { 0, 1 };
                    break;
                case "zeroRotation":
                    bad["rotation"] = new object[] { 0.0, 0.0, 0.0, 0.0 };
                    arguments["bones"] = new object[] { bad };
                    break;
                case "shortRotation":
                    bad["rotation"] = new object[] { 0.0, 0.0, 1.0 };
                    arguments["bones"] = new object[] { bad };
                    break;
                case "shortTranslation":
                    bad["translation"] = new object[] { 0.0, 1.0 };
                    arguments["bones"] = new object[] { bad };
                    break;
                case "twice":
                    arguments["bones"] = new object[] { good, Entry(0, null, new[] { 0.0, 1.0, 0.0 }) };
                    break;
                case "noBones":
                    arguments.Remove("bones");
                    break;
                case "emptyBones":
                    arguments["bones"] = new object[0];
                    break;
                case "noTarget":
                    arguments.Remove("indices");
                    break;
                case "strangeMember":
                    bad["scale"] = new object[] { 1.0, 1.0, 1.0 };
                    arguments["bones"] = new object[] { bad };
                    break;
                case "nullRotation":
                    bad["rotation"] = null;
                    arguments["bones"] = new object[] { bad };
                    break;
                case "nullTranslation":
                    bad["translation"] = null;
                    arguments["bones"] = new object[] { bad };
                    break;
                case "noHandle":
                    arguments["pmxHandle"] = 999999L;
                    break;
                default:
                    arguments["pmxHandle"] = (long)_fixture.Handles.Issue("Other", new object(), () => { });
                    break;
            }
        }

        private void GrantRig()
        {
            FakeBone root = Bone(0f, 0f, 0f);
            FakeBone a = Bone(0f, 1f, 0f, root);
            FakeBone b = Bone(1f, 1.5f, 0.25f, a);
            FakeBone c = Bone(2f, 1f, -0.25f, b);
            FakeBone drot = Grant(Bone(-1f, 1f, 0f, root), a, 0.5f, rotation: true);
            FakeBone etrans = Grant(Bone(0f, 2f, 1f, root), a, 1f, translation: true);
            FakeBone fneg = Grant(Bone(-1f, 0.5f, 0.5f, root), b, -0.75f, rotation: true);
            FakeBone glocal = Grant(Bone(1f, 0f, 1f, root), b, 0.5f, rotation: true, translation: true);
            FakeBone hneg = Grant(Bone(0f, 0.25f, -1.5f, a), c, -1f, translation: true);
            FakeBone ichain = Grant(Bone(-2f, 1f, 0f, root), drot, 1f, rotation: true);
            FakeBone jboth = Grant(Bone(1.5f, 2f, 0.5f, b), c, 0.75f, rotation: true, translation: true);
            FakeBone klocalneg = Grant(Bone(0.5f, 0.5f, 1f, a), c, -0.5f, rotation: true);
            glocal.IsAppendLocal = true;
            klocalneg.IsAppendLocal = true;
            Vertex(2f, 1.75f, 0f, c, 1f, 1f, 0f);
            Vertex(-1f, 0.75f, 1.25f, fneg, 0.25f, -0.5f, -0.5f);
            Vertex(-0.25f, 1.25f, 0f, drot, 0.5f, 0.25f, 1f);
            Vertex(-0.5f, 1.25f, 1f, etrans, -0.25f, -0.5f, -1f);
            Vertex(1.25f, 0.75f, 1.5f, glocal, 0.5f, -1f, 0.25f);
            Vertex(0f, 0.25f, -1f, hneg, 0.5f, 0.25f, 0.5f);
            Vertex(-2.5f, 1.25f, -0.75f, ichain, 1f, 0.25f, -1f);
            Vertex(0.75f, 1.25f, 0f, jboth, -0.5f, 0.25f, -1f);
            Vertex(1.25f, 0.5f, 0.75f, klocalneg, 0f, 0.25f, 1f);
            FakeVertex v9 = Vertex(0.5f, 1.75f, -0.25f, b, 0.5f, -0.25f, 0f);
            v9.Weight1 = 0.25f;
            v9.Bone2 = drot;
            v9.Weight2 = 0.75f;
            FakeVertex v10 = Vertex(0.25f, 0.5f, 0.25f, glocal, 0f, 0.5f, -0.25f);
            v10.Weight1 = 0.5f;
            v10.Bone2 = a;
            v10.Weight2 = 0.5f;
            FakeVertex v11 = Vertex(0f, 1.25f, 0.75f, a, -1f, 0.5f, -0.25f);
            v11.Weight1 = 0.5f;
            v11.Bone2 = b;
            v11.Weight2 = 0.25f;
            v11.Bone3 = c;
            v11.Weight3 = 0.125f;
            v11.Bone4 = fneg;
            v11.Weight4 = 0.125f;
            FakeVertex v12 = Vertex(-0.25f, 1.75f, -0.5f, a, 0.25f, -0.25f, -1f);
            v12.Weight1 = 0.25f;
            v12.Bone2 = etrans;
            v12.Weight2 = 0.25f;
            v12.Bone3 = hneg;
            v12.Weight3 = 0.125f;
            v12.Bone4 = jboth;
            v12.Weight4 = 0.125f;
            FakeVertex v13 = Vertex(0.25f, 1.75f, 1f, b, -1f, 0f, -1f);
            v13.Weight1 = 0.25f;
            v13.Bone2 = c;
            v13.Weight2 = 0.75f;
            v13.SDEF = true;
            v13.SDEF_C = new V3(0.25f, 1.75f, 1f);
            v13.SDEF_R0 = new V3(-0.25f, 1.375f, 0.5f);
            v13.SDEF_R1 = new V3(0f, 1.375f, 1f);
            FakeVertex v14 = Vertex(1f, 0.5f, 1f, glocal, 0f, -1f, 0.25f);
            v14.Weight1 = 0.5f;
            v14.Bone2 = b;
            v14.Weight2 = 0.5f;
            v14.SDEF = true;
            v14.SDEF_C = new V3(1f, 0.5f, 1f);
            v14.SDEF_R0 = new V3(0.375f, 0.5f, 1f);
            v14.SDEF_R1 = new V3(0.375f, 0.75f, 0.5f);
            FakeVertex v15 = Vertex(-0.5f, 0.5f, 0.5f, a, -1f, -1f, -1f);
            v15.Weight1 = 0.25f;
            v15.Bone2 = b;
            v15.Weight2 = 0.25f;
            v15.Bone3 = c;
            v15.Weight3 = 0.25f;
            v15.Bone4 = drot;
            v15.Weight4 = 0.25f;
            v15.QDEF = true;
            FakeVertex v16 = Vertex(2f, 1.75f, -0.25f, c, -0.5f, 0.5f, 0.25f);
            v16.Weight1 = 0.5f;
            v16.Bone2 = glocal;
            v16.Weight2 = 0.25f;
            v16.Bone3 = root;
            v16.Weight3 = 0.25f;
            v16.Bone4 = ichain;
            v16.Weight4 = 0f;
            v16.QDEF = true;
        }

        private void OrderRig()
        {
            FakeBone root = Bone(0f, 0f, 0f);
            FakeBone x = Bone(1f, 1f, 0f);
            FakeBone y = Bone(0f, 2f, 0f, root);
            FakeBone z = Bone(1f, 0f, 0f, root);
            FakeBone w = Grant(Bone(2f, 1f, 1f, x), y, -0.5f, rotation: true);
            FakeBone u = Grant(Bone(0f, 1f, 1f, root), w, 1f, translation: true);
            x.Parent = z;
            y.AppendParent = z;
            y.AppendRatio = 0.5f;
            y.IsAppendRotation = true;
            y.IsAppendTranslation = true;
            Vertex(0.5f, 1f, 0.25f, x, -0.5f, 0.5f, 1f);
            Vertex(-0.5f, 2f, 0.5f, y, 0f, -1f, 0f);
            Vertex(1f, -0.5f, -0.75f, z, -0.25f, 1f, 1f);
            Vertex(2.25f, 0.75f, 0.25f, w, -0.5f, -0.5f, 0f);
            Vertex(0.75f, 1.25f, 1.5f, u, 0.25f, -1f, -1f);
            FakeVertex v5 = Vertex(0.5f, 0.5f, 0f, x, -0.25f, -1f, 1f);
            v5.Weight1 = 0.25f;
            v5.Bone2 = y;
            v5.Weight2 = 0.75f;
            FakeVertex v6 = Vertex(2.25f, 0.75f, 1.75f, w, -0.25f, 0f, -1f);
            v6.Weight1 = 0.5f;
            v6.Bone2 = u;
            v6.Weight2 = 0.5f;
        }

        private static Dictionary<string, object>[] GrantRigPartPose()
        {
            return new[]
            {
                Entry(0, new[] { 0.07087704, 0.28350815, 0.07087704, 0.95371695 }, new[] { 0.25, 0.125, -0.25 }),
                Entry(1, new[] { 0.0, 0.98480775, 0.0, -0.17364818 }, null),
                Entry(2, new[] { 0.9961947, 0.0, 0.0, 0.08715574 }, null),
                Entry(3, new[] { 0.0, 0.0, 0.8660254, 0.5 }, null),
            };
        }

        private static Dictionary<string, object>[] GrantRigLongFullPose()
        {
            return new[]
            {
                Entry(0, new[] { 0.0, 0.17431148, 0.0, 1.9923894 }, new[] { 0.05, 0.0, 0.1 }),
                Entry(1, new[] { 0.530330085, 0.530330085, 0.0, 1.2990381 }, new[] { 0.1, 0.2, -0.1 }),
                Entry(2, new[] { 0.0, -0.5411961, -0.5411961, 1.84775906 }, new[] { -0.2, 0.1, 0.3 }),
                Entry(3, new[] { 0.81115958, 0.0, 0.81115958, 1.63830408 }, new[] { 0.15, -0.1, 0.2 }),
                Entry(4, new[] { 0.0, 0.5176381, 0.0, 1.93185166 }, new[] { 0.1, 0.1, 0.1 }),
                Entry(5, new[] { 0.0, 0.34729636, 0.0, 1.9696155 }, new[] { 0.0, 0.3, 0.0 }),
                Entry(6, new[] { 0.43287922, 0.0, 0.0, 1.95259202 }, null),
                Entry(7, new[] { 0.0, 0.0, -0.68404028, 1.87938524 }, new[] { 0.2, 0.0, -0.1 }),
                Entry(8, new[] { -0.5176381, 0.0, 0.0, 1.93185166 }, new[] { 0.1, 0.0, 0.1 }),
                Entry(9, new[] { 0.0, 0.0, 0.76536686, 1.84775906 }, null),
                Entry(10, new[] { 0.24992294, 0.24992294, 0.24992294, 1.95259202 }, new[] { 0.0, 0.1, 0.0 }),
                Entry(11, new[] { 0.0, -0.84523652, 0.0, 1.81261558 }, new[] { 0.1, 0.1, 0.0 }),
            };
        }

        private static Dictionary<string, object>[] OrderRigPose()
        {
            return new[]
            {
                Entry(0, new[] { 0.0, 0.13052619, 0.0, 0.99144486 }, null),
                Entry(1, new[] { 0.34202014, 0.0, 0.0, 0.93969262 }, new[] { 0.25, 0.0, 0.125 }),
                Entry(2, new[] { 0.0, 0.0, 0.21643961, 0.97629601 }, new[] { 0.0, 0.125, 0.0 }),
                Entry(3, new[] { 0.0, 0.32650558, 0.32650558, 0.88701083 }, new[] { 0.5, 0.25, 0.0 }),
                Entry(4, new[] { -0.21263111, -0.21263111, 0.0, 0.95371695 }, new[] { 0.0, 0.0, 0.25 }),
                Entry(5, new[] { 0.0, 0.17364818, 0.0, 0.98480775 }, new[] { 0.125, 0.0, 0.0 }),
            };
        }

        private static double[] Turn(double degrees)
        {
            double half = degrees * Math.PI / 360d;

            return new[] { 0d, 0d, Math.Sin(half), Math.Cos(half) };
        }

        private static Dictionary<string, object> Entry(int index, double[] rotation, double[] translation)
        {
            Dictionary<string, object> entry = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "index", index },
            };
            if (rotation != null)
            {
                entry["rotation"] = rotation.Cast<object>().ToArray();
            }

            if (translation != null)
            {
                entry["translation"] = translation.Cast<object>().ToArray();
            }

            return entry;
        }

        private static Dictionary<string, object> Material(int at)
        {
            return new Dictionary<string, object> { { "materialIndices", new object[] { at } } };
        }

        private static Dictionary<string, object> Arguments(
            Dictionary<string, object>[] bones, int vertex, long? handle = null)
        {
            return Arguments(bones, new[] { vertex }, handle);
        }

        private static Dictionary<string, object> Arguments(
            Dictionary<string, object>[] bones, int[] vertices, long? handle = null)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "bones", bones.Cast<object>().ToArray() },
                { "indices", vertices.Cast<object>().ToArray() },
            };
            if (handle.HasValue)
            {
                arguments["pmxHandle"] = handle.Value;
            }

            return arguments;
        }

        private IDictionary<string, object> Pose(Dictionary<string, object>[] bones, params int[] vertices)
        {
            return ComposedEditFixture.Value(_fixture.Call(
                ModelPoseVertices.ToolName, Arguments(bones, vertices)));
        }

        private IDictionary<string, object> Pose(Dictionary<string, object>[] bones, Dictionary<string, object> target)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>(target, StringComparer.Ordinal)
            {
                { "bones", bones.Cast<object>().ToArray() },
            };

            return ComposedEditFixture.Value(_fixture.Call(ModelPoseVertices.ToolName, arguments));
        }

        private FakeBone Bone(float x, float y, float z, FakeBone parent = null)
        {
            FakeBone bone = new FakeBone("ボーン" + _fixture.Model.Bone.Count)
            {
                Position = new V3(x, y, z),
                Parent = parent,
            };
            _fixture.Model.Bone.Add(bone);

            return bone;
        }

        private static FakeBone Grant(
            FakeBone bone, FakeBone from, float ratio, bool rotation = false, bool translation = false)
        {
            bone.AppendParent = from;
            bone.AppendRatio = ratio;
            bone.IsAppendRotation = rotation;
            bone.IsAppendTranslation = translation;

            return bone;
        }

        private FakeVertex Vertex(float x, float y, float z, IPXBone bone, float nx, float ny, float nz)
        {
            FakeVertex vertex = new FakeVertex(x, y, z)
            {
                Normal = new V3(nx, ny, nz),
                Bone1 = bone,
                Weight1 = bone == null ? 0f : 1f,
            };
            _fixture.Model.Vertex.Add(vertex);

            return vertex;
        }

        private FakeVertex At(FakeVertex vertex)
        {
            return _fixture.Now(vertex);
        }

        private void AssertLanded(float[][] spots, int first)
        {
            for (int at = 0; at < spots.Length; at++)
            {
                IPXVertex vertex = _fixture.Model.Vertex[first + at];
                float[] spot = spots[at];
                AssertNear(vertex.Position, spot[0], spot[1], spot[2]);
                AssertNear(vertex.Normal, spot[3], spot[4], spot[5]);
            }
        }

        private static void AssertNear(V3 actual, float x, float y, float z)
        {
            Assert.Equal(x, actual.X, Within);
            Assert.Equal(y, actual.Y, Within);
            Assert.Equal(z, actual.Z, Within);
        }

        private static void AssertSpot(V3 actual, float x, float y, float z)
        {
            Assert.Equal(x, actual.X, 4);
            Assert.Equal(y, actual.Y, 4);
            Assert.Equal(z, actual.Z, 4);
        }
    }
}
