using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditMorphsMirrorOffsetsTests : IDisposable
    {
        private const string MirrorOffsets = "mirrorOffsets";

        private const string Axis = "axis";

        private const string BasePmxHandle = "basePmxHandle";

        private const string TargetIndices = "targetIndices";

        private const string Changed = "changed";

        private const string Removed = "removed";

        private const int Digits = 4;

        private static readonly V3 LeftBase = new V3(2f, 3f, 5f);

        private static readonly V3 LeftNow = new V3(2.5f, 3.25f, 4.25f);

        private static readonly V3 First = new V3(0.5f, -0.25f, 0.75f);

        private static readonly V3 Second = new V3(1.5f, 2f, -3f);

        private static readonly Q FirstTurn = new Q(0.5f, -0.5f, 0.5f, 0.5f);

        private static readonly Q SecondTurn = new Q(0.5f, 0.5f, 0.5f, -0.5f);

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakePmx _held;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData("x", -0.5f, -0.25f, 0.75f)]
        [InlineData("y", 0.5f, 0.25f, 0.75f)]
        [InlineData("z", 0.5f, -0.25f, -0.75f)]
        public void AVertexMorphGetsTheMirroredOffsetOnThePartnerVertexAndKeepsTheOriginalOnEachAxis(
            string axis, float x, float y, float z)
        {
            int handle = Standard(axis);
            Morph("左目", MorphKind.Vertex, Moved(0, First));

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Run(Full(handle, axis, new[] { 0 }, new[] { 0 })));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(0, value[Removed]);
            Assert.Equal(1, _fixture.Commits);
            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertVertexOffset(0, 1, x, y, z);
            AssertVertexOffset(0, 0, First.X, First.Y, First.Z);
        }

        [Fact]
        public void AnOffsetThePartnerVertexAlreadyHasIsReplacedNotAddedTo()
        {
            int handle = Pairs();
            Morph("左目", MorphKind.Vertex, Moved(0, First), Moved(1, new V3(1f, 2f, 3f)));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertVertexOffset(0, 1, -0.5f, -0.25f, 0.75f);
            AssertVertexOffset(0, 0, First.X, First.Y, First.Z);
        }

        [Fact]
        public void OnlyTheOffsetsOfTheVerticesThatWerePointedAtAreMirrored()
        {
            int handle = Pairs();
            Morph("左目", MorphKind.Vertex, Moved(0, First), Moved(2, Second));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(3, _fixture.Model.Morph[0].Offsets.Count);
            AssertVertexOffset(0, 1, -0.5f, -0.25f, 0.75f);
            AssertVertexOffset(0, 2, Second.X, Second.Y, Second.Z);
        }

        [Fact]
        public void APointedAtVertexWithNoOffsetInTheMorphAddsNothingToItsPartner()
        {
            int handle = Pairs();
            Morph("左目", MorphKind.Vertex, Moved(0, First));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0, 2 })));

            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertVertexOffset(0, 1, -0.5f, -0.25f, 0.75f);
        }

        [Fact]
        public void OnlyThePickedMorphsGetMirroredOffsets()
        {
            int handle = Pairs();
            Morph("左目", MorphKind.Vertex, Moved(0, First));
            Morph("右目", MorphKind.Vertex, Moved(0, First));

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            Assert.Single(_fixture.Model.Morph[1].Offsets);
        }

        [Fact]
        public void TheChangedCountIsTheNumberOfMorphsThatGotAnOffset()
        {
            int handle = Standard("x");
            Morph("左目", MorphKind.Vertex, Moved(0, First));
            Morph("上", MorphKind.Vertex, Moved(2, First));

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Run(Full(handle, "x", new[] { 0, 1 }, new[] { 0, 2 })));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            Assert.Single(_fixture.Model.Morph[1].Offsets);
        }

        [Fact]
        public void WhenThePartnerIsPointedAtTooEachGetsTheOthersOffsetMirroredFromBeforeEitherChanged()
        {
            int handle = Pairs();
            Morph("左目", MorphKind.Vertex, Moved(0, First), Moved(1, Second));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0, 1 })));

            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertVertexOffset(0, 0, -1.5f, 2f, -3f);
            AssertVertexOffset(0, 1, -0.5f, -0.25f, 0.75f);
        }

        [Fact]
        public void AVertexWithNoVertexAtItsMirroredSpotGetsNothingAdded()
        {
            int handle = Standard("x");
            Morph("上", MorphKind.Vertex, Moved(2, First));
            string before = Shape();

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Run(Full(handle, "x", new[] { 0 }, new[] { 2 })));

            Assert.Equal(0, value[Changed]);
            Assert.Equal(before, Shape());
        }

        [Theory]
        [InlineData(0.0005f, true)]
        [InlineData(0.002f, false)]
        public void AVertexWithinTheToleranceOfTheMirroredSpotIsThePartnerAndOneBeyondItIsNot(
            float gap, bool found)
        {
            V3[] baseline =
            {
                LeftBase, new V3(-2f, 3f, 5f + gap), new V3(7f, 8f, 9f), new V3(11f, 12f, 13f),
            };
            int handle = Scene(baseline, baseline);
            Morph("左目", MorphKind.Vertex, Moved(0, First));

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(found ? 1 : 0, value[Changed]);
            Assert.Equal(found ? 2 : 1, _fixture.Model.Morph[0].Offsets.Count);
            if (found)
            {
                AssertVertexOffset(0, 1, -0.5f, -0.25f, 0.75f);
            }
        }

        [Fact]
        public void ThePartnerVertexIsFoundAtTheMirroredSpotInTheCopyNotWhereVerticesAreNow()
        {
            V3[] baseline =
            {
                LeftBase, new V3(-2f, 3f, 5f), new V3(9f, 9f, 9f), new V3(8f, 8f, 8f),
            };
            V3[] current =
            {
                LeftNow, new V3(-4f, 0f, 0f), new V3(-2f, 3f, 5f), new V3(-2.5f, 3.25f, 4.25f),
            };
            int handle = Scene(baseline, current);
            Morph("左目", MorphKind.Vertex, Moved(0, First));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertVertexOffset(0, 1, -0.5f, -0.25f, 0.75f);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void SurroundingsInTheCopyDecideWhichOfTwoVerticesAtTheSamePositionIsThePartner(
            bool firstIsMatching, bool surroundingsSwappedNow)
        {
            int handle = Layered(firstIsMatching, surroundingsSwappedNow);
            Morph("左目", MorphKind.Vertex, Moved(0, First), Moved(3, Second));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0, 3 })));

            int matching = firstIsMatching ? 6 : 9;
            int other = firstIsMatching ? 9 : 6;
            Assert.Equal(4, _fixture.Model.Morph[0].Offsets.Count);
            AssertVertexOffset(0, matching, -0.5f, -0.25f, 0.75f);
            AssertVertexOffset(0, other, -1.5f, 2f, -3f);
        }

        [Theory]
        [InlineData("x", -0.5f, -0.25f, 0.75f, 0.5f, 0.5f, -0.5f, 0.5f)]
        [InlineData("y", 0.5f, 0.25f, 0.75f, -0.5f, -0.5f, -0.5f, 0.5f)]
        [InlineData("z", 0.5f, -0.25f, -0.75f, -0.5f, 0.5f, 0.5f, 0.5f)]
        public void ABoneMorphGetsTheMirroredTranslationAndRotationOnThePartnerBoneOnEachAxis(
            string axis, float x, float y, float z, float qx, float qy, float qz, float qw)
        {
            int handle = Bones(axis, "左腕", "右腕");
            Morph("腕上げ", MorphKind.Bone, Posed(0, First, FirstTurn));

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Run(Full(handle, axis, new[] { 0 }, new[] { 0 })));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertBoneOffset(0, 1, x, y, z, qx, qy, qz, qw);
            AssertBoneOffset(0, 0, First.X, First.Y, First.Z, FirstTurn.X, FirstTurn.Y, FirstTurn.Z, FirstTurn.W);
        }

        [Fact]
        public void AnOffsetThePartnerBoneAlreadyHasIsReplacedNotAddedTo()
        {
            int handle = Bones("x", "左腕", "右腕");
            Morph("腕上げ", MorphKind.Bone, Posed(0, First, FirstTurn), Posed(1, Second, SecondTurn));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertBoneOffset(0, 1, -0.5f, -0.25f, 0.75f, 0.5f, 0.5f, -0.5f, 0.5f);
        }

        [Fact]
        public void WhenThePartnerBoneIsPointedAtTooEachGetsTheOthersOffsetMirroredFromBeforeEitherChanged()
        {
            int handle = Bones("x", "左腕", "右腕");
            Morph("腕上げ", MorphKind.Bone, Posed(0, First, FirstTurn), Posed(1, Second, SecondTurn));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0, 1 })));

            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertBoneOffset(0, 0, -1.5f, 2f, -3f, 0.5f, -0.5f, -0.5f, -0.5f);
            AssertBoneOffset(0, 1, -0.5f, -0.25f, 0.75f, 0.5f, 0.5f, -0.5f, 0.5f);
        }

        [Theory]
        [InlineData("左腕", "右腕")]
        [InlineData("右腕", "左腕")]
        [InlineData("腕左", "腕右")]
        [InlineData("_左腕_", "_右腕_")]
        [InlineData("手左先", "手右先")]
        public void ThePartnerBoneIsTheOneWhoseNameHasTheSideSwappedTheWayMirrorPositionSwapsIt(
            string picked, string partner)
        {
            int handle = Bones("x", picked, partner);
            Morph("腕上げ", MorphKind.Bone, Posed(0, First, FirstTurn));

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(2, _fixture.Model.Morph[0].Offsets.Count);
            AssertBoneOffset(0, 1, -0.5f, -0.25f, 0.75f, 0.5f, 0.5f, -0.5f, 0.5f);
        }

        [Theory]
        [InlineData("腕_L", "腕_R")]
        [InlineData("左腕", "首")]
        public void ABoneWhoseSideSwappedNameIsNoBoneGetsNothingAdded(string picked, string other)
        {
            int handle = Bones("x", picked, other);
            Morph("腕上げ", MorphKind.Bone, Posed(0, First, FirstTurn));
            string before = Shape();

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(0, value[Changed]);
            Assert.Equal(before, Shape());
        }

        [Theory]
        [InlineData(MorphKind.Material)]
        [InlineData(MorphKind.UV)]
        [InlineData(MorphKind.Group)]
        [InlineData(MorphKind.Flip)]
        [InlineData(MorphKind.Impulse)]
        public void AMorphThatIsNeitherVertexNorBoneIsRefusedAsNotApplicable(MorphKind kind)
        {
            Settled(() => Other(kind));
            string before = Shape();

            IDictionary<string, object> envelope = Run(Full(
                Handle(), "x", new[] { 1 }, new[] { 0 }));

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedEditFixture.Code(envelope));
            Assert.Equal(before, Shape());
        }

        [Fact]
        public void MorphsOfDifferentKindsPickedTogetherAreRefused()
        {
            Settled(() =>
            {
                AddBone("左腕", LeftBase);
                AddBone("右腕", new V3(-2f, 3f, 5f));

                return Morph("腕上げ", MorphKind.Bone, Posed(0, First, FirstTurn));
            });
            string before = Shape();

            IDictionary<string, object> envelope = Run(Full(
                Handle(), "x", new[] { 0, 1 }, new[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(before, Shape());
        }

        [Fact]
        public void MirroringChangesNeitherTheCopyNorItsCounts()
        {
            int handle = Standard("x");
            Morph("左目", MorphKind.Vertex, Moved(0, First));
            V3[] positions = _held.Vertex.Select(vertex => vertex.Position).ToArray();

            ComposedEditFixture.Value(Run(Full(handle, "x", new[] { 0 }, new[] { 0 })));

            Assert.Equal(positions.Length, _held.Vertex.Count);
            Assert.Single(((FakeMaterial)_held.Material[0]).Faces);
            Assert.Empty(_held.Bone);
            Assert.Empty(_held.Morph);
            for (int at = 0; at < positions.Length; at++)
            {
                Assert.Equal(positions[at].X, _held.Vertex[at].Position.X);
                Assert.Equal(positions[at].Y, _held.Vertex[at].Position.Y);
                Assert.Equal(positions[at].Z, _held.Vertex[at].Position.Z);
            }
        }

        [Fact]
        public void LeavingOutTheCopyIsRefused()
        {
            Settled();
            string before = Shape();

            IDictionary<string, object> envelope = Run(Without(
                Full(Handle(), "x", new[] { 0 }, new[] { 0 }), BasePmxHandle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BasePmxHandle, ComposedEditFixture.Message(envelope));
            Assert.Equal(before, Shape());
        }

        [Fact]
        public void LeavingOutTheAxisIsRefused()
        {
            Settled();
            string before = Shape();

            IDictionary<string, object> envelope = Run(Without(
                Full(Handle(), "x", new[] { 0 }, new[] { 0 }), Axis));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Axis, ComposedEditFixture.Message(envelope));
            Assert.Equal(before, Shape());
        }

        [Theory]
        [InlineData("w")]
        [InlineData("")]
        [InlineData("X")]
        [InlineData(1.0)]
        public void AnAxisThatIsNotOneOfTheThreeIsRefused(object given)
        {
            Settled();
            string before = Shape();

            IDictionary<string, object> envelope = Run(With(
                Full(Handle(), "x", new[] { 0 }, new[] { 0 }), Axis, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Axis, ComposedEditFixture.Message(envelope));
            Assert.Equal(before, Shape());
        }

        [Fact]
        public void LeavingOutTheTargetsIsRefused()
        {
            Settled();
            string before = Shape();

            IDictionary<string, object> envelope = Run(Without(
                Full(Handle(), "x", new[] { 0 }, new[] { 0 }), TargetIndices));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(TargetIndices, ComposedEditFixture.Message(envelope));
            Assert.Equal(before, Shape());
        }

        [Fact]
        public void ACopyHandleThatIsNotHeldIsRefused()
        {
            Settled();
            string before = Shape();

            IDictionary<string, object> envelope = Run(Full(
                Handle() + 1000, "x", new[] { 0 }, new[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedEditFixture.Code(envelope));
            Assert.Equal(before, Shape());
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("face")]
        [InlineData("bone")]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing)
        {
            Settled();
            string before = Shape();

            FakePmx copy = FakeEditorState.Duplicate(_fixture.Model);
            switch (differing)
            {
                case "vertex":
                    copy.Vertex.Add(new FakeVertex(5f, 5f, 5f));
                    break;

                case "face":
                    ((FakeMaterial)copy.Material[0]).Faces.RemoveAt(0);
                    break;

                default:
                    copy.Bone.Add(new FakeBone("ボーン"));
                    break;
            }

            int handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });
            IDictionary<string, object> envelope = Run(Full(handle, "x", new[] { 0 }, new[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Equal(before, Shape());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(99)]
        public void AMorphOutsideTheMorphsIsRefused(int outside)
        {
            Settled();
            string before = Shape();

            IDictionary<string, object> envelope = Run(Full(
                Handle(), "x", new[] { 0, outside }, new[] { 0 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Equal(before, Shape());
        }

        [Theory]
        [InlineData("addOffsets", BasePmxHandle)]
        [InlineData("splitVerticesByAxis", BasePmxHandle)]
        [InlineData(MirrorOffsets, "boundary")]
        [InlineData(MirrorOffsets, "name")]
        public void TheCopyIsPassedOnlyToMirroringAndTheBoundaryAndTheNameAreNotPassedToIt(
            string operation, string name)
        {
            Settled();
            string before = Shape();
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", operation),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
            };
            if (operation == MirrorOffsets)
            {
                given.Add(ComposedEditFixture.Given(Axis, "x"));
                given.Add(ComposedEditFixture.Given(BasePmxHandle, (long)Handle()));
                given.Add(ComposedEditFixture.Given(TargetIndices, new object[] { 0 }));
            }

            if (operation == "addOffsets")
            {
                given.Add(ComposedEditFixture.Given(TargetIndices, new object[] { 0 }));
            }

            if (operation == "splitVerticesByAxis")
            {
                given.Add(ComposedEditFixture.Given(Axis, "x"));
            }

            given.Add(ComposedEditFixture.Given(
                name, name == BasePmxHandle ? (long)Handle() : name == "boundary" ? (object)0.0 : "名前"));

            IDictionary<string, object> envelope = Run(given);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            Assert.Equal(before, Shape());
        }

        [Fact]
        public void TheAxisStillServesSplittingBySide()
        {
            Settled();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", "splitVerticesByAxis"),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(Axis, "x"),
            }));

            Assert.Equal(1, value[Removed]);
        }

        private int _handle;

        private int Handle()
        {
            return _handle;
        }

        private void Settled(Func<IPXMorph> other = null)
        {
            _handle = Standard("x");
            Morph("左目", MorphKind.Vertex, Moved(0, First));
            if (other != null)
            {
                other();
            }

            ComposedEditFixture.Value(Run(Full(_handle, "x", new[] { 0 }, new[] { 0 })));
        }

        private IPXMorph Other(MorphKind kind)
        {
            switch (kind)
            {
                case MorphKind.Material:
                    return Morph("他", kind, new FakeMaterialMorphOffset(_fixture.Model.Material[0]));

                case MorphKind.UV:
                    return Morph("他", kind, new FakeUVMorphOffset(_fixture.Model.Vertex[0]));

                case MorphKind.Impulse:
                    return Morph("他", kind, new FakeImpulseMorphOffset(_fixture.Model.Body[0]));

                default:
                    return Morph("他", kind, new FakeGroupMorphOffset(_fixture.Model.Morph[0]));
            }
        }

        private int Standard(string axis)
        {
            V3 partner = axis == "x"
                ? new V3(-2f, 3f, 5f)
                : axis == "y" ? new V3(2f, -3f, 5f) : new V3(2f, 3f, -5f);
            V3[] baseline = { LeftBase, partner, new V3(7f, 8f, 9f), new V3(11f, 12f, 13f) };
            V3[] current = (V3[])baseline.Clone();
            current[0] = LeftNow;
            current[1] = new V3(partner.X + 0.125f, partner.Y - 0.25f, partner.Z + 0.5f);
            int handle = Scene(baseline, current);
            _fixture.Model.Body.Add(new FakeBody("剛体"));

            return handle;
        }

        private int Pairs()
        {
            V3[] baseline =
            {
                LeftBase, new V3(-2f, 3f, 5f), new V3(4f, 6f, 8f), new V3(-4f, 6f, 8f),
            };
            V3[] current = (V3[])baseline.Clone();
            current[0] = LeftNow;

            return Scene(baseline, current);
        }

        private int Bones(string axis, string pickedName, string partnerName)
        {
            int handle = Standard(axis);
            V3 partner = axis == "x"
                ? new V3(-2f, 3f, 5f)
                : axis == "y" ? new V3(2f, -3f, 5f) : new V3(2f, 3f, -5f);
            AddBone(pickedName, LeftBase);
            AddBone(partnerName, partner);
            AddBone("首", new V3(7f, 8f, 9f));

            return handle;
        }

        private int Layered(bool firstIsMatching, bool surroundingsSwappedNow)
        {
            V3[] layers =
            {
                new V3(2f, 0f, 0f), new V3(3f, 1f, 0f), new V3(3f, -1f, 0f),
                new V3(2f, 0f, 0f), new V3(3f, 4f, 0f), new V3(3f, -4f, 0f),
            };
            V3[] matching =
            {
                new V3(-2f, 0f, 0f), new V3(-3f, 1f, 0f), new V3(-3f, -1f, 0f),
            };
            V3[] other =
            {
                new V3(-2f, 0f, 0f), new V3(-3f, 4f, 0f), new V3(-3f, -4f, 0f),
            };
            V3[] baseline = layers.Concat(firstIsMatching ? matching : other)
                .Concat(firstIsMatching ? other : matching)
                .ToArray();
            V3[] current = (V3[])baseline.Clone();
            if (surroundingsSwappedNow)
            {
                current[7] = baseline[10];
                current[8] = baseline[11];
                current[10] = baseline[7];
                current[11] = baseline[8];
            }

            return Scene(
                baseline,
                current,
                new[] { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, new[] { 9, 10, 11 } });
        }

        private int Scene(V3[] baseline, V3[] current, int[][] faces = null)
        {
            FakePmx held = new FakePmx();
            Fill(held, baseline, faces);
            _held = held;
            Fill(_fixture.Model, current, faces);
            _handle = _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });

            return _handle;
        }

        private static void Fill(FakePmx model, V3[] positions, int[][] faces)
        {
            foreach (V3 position in positions)
            {
                model.Vertex.Add(new FakeVertex(position.X, position.Y, position.Z));
            }

            FakeMaterial material = new FakeMaterial("材質");
            foreach (int[] face in faces ?? new[] { new[] { 0, 1, 2 } })
            {
                material.Faces.Add(new FakeFace(
                    model.Vertex[face[0]], model.Vertex[face[1]], model.Vertex[face[2]]));
            }

            model.Material.Add(material);
        }

        private void AddBone(string name, V3 at)
        {
            _held.Bone.Add(new FakeBone(name) { Position = at });
            _fixture.Model.Bone.Add(new FakeBone(name) { Position = at });
        }

        private FakeMorph Morph(string name, MorphKind kind, params IPXMorphOffset[] offsets)
        {
            FakeMorph morph = new FakeMorph(name, kind);
            foreach (IPXMorphOffset offset in offsets)
            {
                morph.Offsets.Add(offset);
            }

            _fixture.Model.Morph.Add(morph);

            return morph;
        }

        private IPXMorphOffset Moved(int vertex, V3 offset)
        {
            return new FakeVertexMorphOffset(_fixture.Model.Vertex[vertex]) { Offset = offset };
        }

        private IPXMorphOffset Posed(int bone, V3 translation, Q rotation)
        {
            return new FakeBoneMorphOffset(_fixture.Model.Bone[bone])
            {
                Translation = translation,
                Rotation = rotation,
            };
        }

        private static List<KeyValuePair<string, object>> Full(
            int handle, string axis, int[] morphs, int[] targets)
        {
            return new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", MirrorOffsets),
                ComposedEditFixture.Given("indices", morphs.Cast<object>().ToArray()),
                ComposedEditFixture.Given(TargetIndices, targets.Cast<object>().ToArray()),
                ComposedEditFixture.Given(Axis, axis),
                ComposedEditFixture.Given(BasePmxHandle, (long)handle),
            };
        }

        private static List<KeyValuePair<string, object>> Without(
            List<KeyValuePair<string, object>> given, string name)
        {
            return given.Where(pair => pair.Key != name).ToList();
        }

        private static List<KeyValuePair<string, object>> With(
            List<KeyValuePair<string, object>> given, string name, object value)
        {
            List<KeyValuePair<string, object>> made = Without(given, name);
            made.Add(ComposedEditFixture.Given(name, value));

            return made;
        }

        private IDictionary<string, object> Run(List<KeyValuePair<string, object>> given)
        {
            return _fixture.Call(
                ModelEditMorphs.ToolName, ComposedEditFixture.Arguments(given.ToArray()));
        }

        private string Shape()
        {
            return string.Join(
                "|",
                _fixture.Model.Morph.Select(
                    morph => morph.Name + ":" + string.Join(",", morph.Offsets.Select(Described))));
        }

        private string Described(IPXMorphOffset offset)
        {
            IPXVertexMorphOffset vertex = offset as IPXVertexMorphOffset;
            if (vertex != null)
            {
                return _fixture.Model.Vertex.IndexOf(vertex.Vertex) + "@" + Text(vertex.Offset);
            }

            IPXBoneMorphOffset bone = offset as IPXBoneMorphOffset;
            if (bone != null)
            {
                return _fixture.Model.Bone.IndexOf(bone.Bone) + "@" + Text(bone.Translation)
                    + "/" + Text(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);
            }

            return offset.GetType().Name;
        }

        private static string Text(V3 value)
        {
            return Text(value.X, value.Y, value.Z);
        }

        private static string Text(params float[] values)
        {
            return string.Join(
                ";", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
        }

        private void AssertVertexOffset(int morph, int vertex, float x, float y, float z)
        {
            V3 found = _fixture.Model.Morph[morph].Offsets
                .OfType<IPXVertexMorphOffset>()
                .Single(offset => _fixture.Model.Vertex.IndexOf(offset.Vertex) == vertex)
                .Offset;
            Assert.Equal(x, found.X, Digits);
            Assert.Equal(y, found.Y, Digits);
            Assert.Equal(z, found.Z, Digits);
        }

        private void AssertBoneOffset(
            int morph, int bone, float x, float y, float z, float qx, float qy, float qz, float qw)
        {
            IPXBoneMorphOffset found = _fixture.Model.Morph[morph].Offsets
                .OfType<IPXBoneMorphOffset>()
                .Single(offset => _fixture.Model.Bone.IndexOf(offset.Bone) == bone);
            Assert.Equal(x, found.Translation.X, Digits);
            Assert.Equal(y, found.Translation.Y, Digits);
            Assert.Equal(z, found.Translation.Z, Digits);
            Assert.Equal(qx, found.Rotation.X, Digits);
            Assert.Equal(qy, found.Rotation.Y, Digits);
            Assert.Equal(qz, found.Rotation.Z, Digits);
            Assert.Equal(qw, found.Rotation.W, Digits);
        }
    }
}
