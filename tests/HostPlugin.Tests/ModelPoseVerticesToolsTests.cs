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
        private static readonly float Half = (float)Math.Sqrt(0.5);

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

        private static void AssertSpot(V3 actual, float x, float y, float z)
        {
            Assert.Equal(x, actual.X, 4);
            Assert.Equal(y, actual.Y, 4);
            Assert.Equal(z, actual.Z, 4);
        }
    }
}
