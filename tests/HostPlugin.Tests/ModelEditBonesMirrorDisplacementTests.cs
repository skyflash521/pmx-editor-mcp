using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditBonesMirrorDisplacementTests : IDisposable
    {
        private const string MirrorDisplacement = "mirrorDisplacement";

        private const string Axis = "axis";

        private const string BasePmxHandle = "basePmxHandle";

        private const string Radius = "radius";

        private const string Changed = "changed";

        private const int Digits = 4;

        private static readonly V3 LeftBase = new V3(2f, 3f, 5f);

        private static readonly V3 LeftNow = new V3(2.5f, 3.25f, 4.25f);

        private static readonly V3 Elsewhere = new V3(7f, 8f, 9f);

        private static readonly V3 Untouched = new V3(5f, 5f, 5f);

        private static readonly V3 LeftOffset = new V3(2f, 1f, -3f);

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakePmx _held;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData("x", -2.375f, 3f, 4.75f)]
        [InlineData("y", 2.625f, -3.5f, 4.75f)]
        [InlineData("z", 2.625f, 3f, -3.75f)]
        public void ThePartnerNamedForTheOtherSideGetsTheMirroredDisplacementAddedToWhereItIsNowOnEachAxis(
            string axis, float x, float y, float z)
        {
            int handle = Standard(axis);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, axis, 0)));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(1, _fixture.Commits);
            AssertPosition(1, x, y, z);
            AssertPosition(0, LeftNow.X, LeftNow.Y, LeftNow.Z);
            AssertPosition(2, Elsewhere.X, Elsewhere.Y, Elsewhere.Z);
        }

        [Fact]
        public void ASdefVertexWeighedToTheMovedPartnerHasItsCentreOnTheNewBoneAxis()
        {
            int handle = Standard("x");
            IPXVertex vertex = _fixture.Model.Vertex[0];
            vertex.Bone1 = _fixture.Model.Bone[1];
            vertex.Bone2 = _fixture.Model.Bone[2];
            vertex.Weight1 = 0.5f;
            vertex.Weight2 = 0.5f;
            vertex.SDEF = true;
            vertex.SDEF_C = new V3(99f, 99f, 99f);

            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            IPXVertex now = _fixture.Model.Vertex[0];
            V3 from = _fixture.Model.Bone[1].Position;
            V3 to = _fixture.Model.Bone[2].Position;
            double[] along = { to.X - from.X, to.Y - from.Y, to.Z - from.Z };
            double length = Math.Sqrt((along[0] * along[0]) + (along[1] * along[1]) + (along[2] * along[2]));
            double[] unit = along.Select(a => a / length).ToArray();
            double reach = (unit[0] * (now.Position.X - from.X)) + (unit[1] * (now.Position.Y - from.Y))
                + (unit[2] * (now.Position.Z - from.Z));
            Assert.Equal(from.X + (unit[0] * reach), now.SDEF_C.X, Digits);
            Assert.Equal(from.Y + (unit[1] * reach), now.SDEF_C.Y, Digits);
            Assert.Equal(from.Z + (unit[2] * reach), now.SDEF_C.Z, Digits);
        }

        [Theory]
        [InlineData("x", -2f, 1f, -3f)]
        [InlineData("y", 2f, -1f, -3f)]
        [InlineData("z", 2f, 1f, 3f)]
        public void WhenBothTipsAreOffsetsThePartnerOffsetIsThePickedOffsetNowMirroredAcrossTheAxis(
            string axis, float x, float y, float z)
        {
            int handle = Standard(axis);
            Offsets(0, new V3(9f, 9f, 9f), LeftOffset);
            Offsets(1, Untouched, Untouched);

            ComposedEditFixture.Value(Run(Full(handle, axis, 0)));

            AssertOffset(1, x, y, z);
            AssertOffset(0, LeftOffset.X, LeftOffset.Y, LeftOffset.Z);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void TheOffsetIsLeftAloneUnlessTheTipsOfBothBonesAreOffsets(
            bool pickedTipIsBone, bool partnerTipIsBone)
        {
            int handle = Standard("x");
            AddBone("先", new V3(1f, 1f, 1f), new V3(1f, 1f, 1f));
            Offsets(0, new V3(9f, 9f, 9f), LeftOffset);
            Offsets(1, Untouched, Untouched);
            if (pickedTipIsBone)
            {
                Tip(0);
            }

            if (partnerTipIsBone)
            {
                Tip(1);
            }

            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            AssertPosition(1, -2.375f, 3f, 4.75f);
            AssertOffset(1, Untouched.X, Untouched.Y, Untouched.Z);
            Assert.Same(
                pickedTipIsBone ? _fixture.Model.Bone[3] : null, _fixture.Model.Bone[0].ToBone);
            Assert.Same(
                partnerTipIsBone ? _fixture.Model.Bone[3] : null, _fixture.Model.Bone[1].ToBone);
        }

        [Theory]
        [InlineData("左腕", "右腕")]
        [InlineData("右腕", "左腕")]
        [InlineData("腕左", "腕右")]
        [InlineData("_左腕_", "_右腕_")]
        public void ThePartnerIsTheBoneWhoseNameHasTheSideSwappedTheWayMirrorPositionSwapsIt(
            string picked, string partner)
        {
            int handle = Scene();
            AddBone(picked, LeftBase, LeftNow);
            AddBone(partner, new V3(-2f, 3f, 5f), new V3(-1.875f, 2.75f, 5.5f));
            AddBone("首", Elsewhere, Elsewhere);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            Assert.Equal(1, value[Changed]);
            AssertPosition(1, -2.375f, 3f, 4.75f);
            AssertPosition(0, LeftNow.X, LeftNow.Y, LeftNow.Z);
            AssertPosition(2, Elsewhere.X, Elsewhere.Y, Elsewhere.Z);
        }

        [Fact]
        public void OnlyTheSideMarkAtTheStartIsSwappedWhenTheNameHasOneAtBothEnds()
        {
            int handle = Scene();
            AddBone("左腕左", LeftBase, LeftNow);
            AddBone("右腕右", new V3(-2.5f, 3f, 5f), new V3(-2.5f, 3f, 5f));
            AddBone("右腕左", new V3(-2f, 3f, 5f), new V3(-1.875f, 2.75f, 5.5f));

            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            AssertPosition(1, -2.5f, 3f, 5f);
            AssertPosition(2, -2.375f, 3f, 4.75f);
        }

        [Theory]
        [InlineData("腕_L", "腕_R")]
        [InlineData("腕", "腕")]
        [InlineData("左腕", "首")]
        [InlineData("手左先", "手右先")]
        public void ABoneWhoseSideSwappedNameIsNoBoneStaysAndNothingElseMoves(string picked, string other)
        {
            int handle = Scene();
            AddBone(picked, LeftBase, LeftNow);
            AddBone(other, new V3(-2f, 3f, 5f), new V3(-1.875f, 2.75f, 5.5f));

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            Assert.Equal(0, value[Changed]);
            AssertPosition(0, LeftNow.X, LeftNow.Y, LeftNow.Z);
            AssertPosition(1, -1.875f, 2.75f, 5.5f);
        }

        [Fact]
        public void ThePartnerIsFoundByNameNotByWhichBoneSitsAtTheMirroredSpot()
        {
            int handle = Scene();
            AddBone("左腕", LeftBase, LeftNow);
            AddBone("右腕", Elsewhere, Elsewhere);
            AddBone("右肩", new V3(-2f, 3f, 5f), new V3(-2f, 3f, 5f));

            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            AssertPosition(1, 6.5f, 8.25f, 8.25f);
            AssertPosition(2, -2f, 3f, 5f);
        }

        [Fact]
        public void ABoneWithNoPartnerStaysWhileThePickedOnesWithAPartnerCarryOn()
        {
            int handle = Standard("x");

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0, 2)));

            Assert.Equal(1, value[Changed]);
            AssertPosition(1, -2.375f, 3f, 4.75f);
            AssertPosition(2, Elsewhere.X, Elsewhere.Y, Elsewhere.Z);
        }

        [Fact]
        public void WhenThePartnerIsPickedTooEachGetsTheOthersDisplacementAndOffsetMirroredFromBeforeEitherMoved()
        {
            int handle = Scene();
            AddBone("左腕", LeftBase, LeftNow);
            AddBone("右腕", new V3(-2f, 3f, 5f), new V3(-1.75f, 2.5f, 5.125f));
            Offsets(0, new V3(9f, 9f, 9f), LeftOffset);
            Offsets(1, new V3(8f, 8f, 8f), new V3(4f, 5f, 6f));

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0, 1)));

            Assert.Equal(2, value[Changed]);
            AssertPosition(0, 2.25f, 2.75f, 4.375f);
            AssertPosition(1, -2.25f, 2.75f, 4.375f);
            AssertOffset(0, -4f, 5f, 6f);
            AssertOffset(1, -2f, 1f, -3f);
        }

        [Fact]
        public void OnlyTheBonesThatWerePickedCarryTheirDisplacementOverAndOnlyThePartnersMove()
        {
            int handle = Standard("x");
            AddBone("左足", new V3(4f, 6f, 8f), new V3(4.25f, 6.5f, 7.5f));
            AddBone("右足", new V3(-4f, 6f, 8f), new V3(-4f, 6f, 8f));

            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            AssertPosition(1, -2.375f, 3f, 4.75f);
            AssertPosition(3, 4.25f, 6.5f, 7.5f);
            AssertPosition(4, -4f, 6f, 8f);
            AssertPosition(0, LeftNow.X, LeftNow.Y, LeftNow.Z);
        }

        [Fact]
        public void MirroringChangesNeitherTheCopyNorItsCounts()
        {
            int handle = Standard("x");
            Offsets(0, new V3(9f, 9f, 9f), LeftOffset);
            V3[] positions = _held.Bone.Select(bone => bone.Position).ToArray();
            V3[] offsets = _held.Bone.Select(bone => bone.ToOffset).ToArray();

            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            Assert.Equal(positions.Length, _held.Bone.Count);
            Assert.Equal(3, _held.Vertex.Count);
            Assert.Single(((FakeMaterial)_held.Material[0]).Faces);
            for (int at = 0; at < positions.Length; at++)
            {
                Assert.Equal(positions[at].X, _held.Bone[at].Position.X);
                Assert.Equal(positions[at].Y, _held.Bone[at].Position.Y);
                Assert.Equal(positions[at].Z, _held.Bone[at].Position.Z);
                Assert.Equal(offsets[at].X, _held.Bone[at].ToOffset.X);
                Assert.Equal(offsets[at].Y, _held.Bone[at].ToOffset.Y);
                Assert.Equal(offsets[at].Z, _held.Bone[at].ToOffset.Z);
            }
        }

        [Fact]
        public void LeavingOutTheCopyIsRefused()
        {
            int handle = Settled();
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(Without(Full(handle, "x", 0), BasePmxHandle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BasePmxHandle, ComposedEditFixture.Message(envelope));
            AssertBonesUnchanged(before);
        }

        [Fact]
        public void LeavingOutTheAxisIsRefused()
        {
            int handle = Settled();
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(Without(Full(handle, "x", 0), Axis));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Axis, ComposedEditFixture.Message(envelope));
            AssertBonesUnchanged(before);
        }

        [Theory]
        [InlineData("w")]
        [InlineData("")]
        [InlineData("X")]
        [InlineData(1.0)]
        public void AnAxisThatIsNotOneOfTheThreeIsRefused(object given)
        {
            int handle = Settled();
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(With(Full(handle, "x", 0), Axis, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Axis, ComposedEditFixture.Message(envelope));
            AssertBonesUnchanged(before);
        }

        [Fact]
        public void ACopyHandleThatIsNotHeldIsRefused()
        {
            int handle = Settled();
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(Full(handle + 1000, "x", 0));

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedEditFixture.Code(envelope));
            AssertBonesUnchanged(before);
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("face")]
        [InlineData("bone")]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing)
        {
            Settled();
            IList<IPXBone> before = Bones();

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
            IDictionary<string, object> envelope = Run(Full(handle, "x", 0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            AssertBonesUnchanged(before);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(99)]
        public void ABoneOutsideTheBonesIsRefused(int outside)
        {
            int handle = Settled();
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(Full(handle, "x", 0, outside));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            AssertBonesUnchanged(before);
        }

        [Theory]
        [InlineData("resetLocalAxis", BasePmxHandle)]
        [InlineData("resetLocalAxis", Axis)]
        [InlineData("mirrorPosition", BasePmxHandle)]
        [InlineData("followVertices", Axis)]
        [InlineData(MirrorDisplacement, Radius)]
        public void TheCopyAndTheAxisArePassedOnlyToTheOperationsThatTakeThem(string operation, string name)
        {
            int handle = Settled();
            IList<IPXBone> before = Bones();
            List<KeyValuePair<string, object>> given = Taking(operation, handle);
            object value = name == Radius ? (object)1.0 : name == Axis ? (object)"x" : (long)handle;
            given.Add(ComposedEditFixture.Given(name, value));

            IDictionary<string, object> envelope = Run(given);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertBonesUnchanged(before);
        }

        [Fact]
        public void TheAxisStillServesMirroringAPosition()
        {
            Settled();

            ComposedEditFixture.Value(Run(new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", "mirrorPosition"),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(Axis, "x"),
            }));

            AssertPosition(1, -2.5f, 3.25f, 4.25f);
        }

        private static List<KeyValuePair<string, object>> Taking(string operation, int handle)
        {
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", operation),
            };
            switch (operation)
            {
                case MirrorDisplacement:
                    given.Add(ComposedEditFixture.Given("indices", new object[] { 0 }));
                    given.Add(ComposedEditFixture.Given(Axis, "x"));
                    given.Add(ComposedEditFixture.Given(BasePmxHandle, (long)handle));
                    break;

                case "followVertices":
                    given.Add(ComposedEditFixture.Given("indices", new object[] { 0 }));
                    given.Add(ComposedEditFixture.Given(Radius, 1.0));
                    given.Add(ComposedEditFixture.Given(BasePmxHandle, (long)handle));
                    break;

                case "mirrorPosition":
                    given.Add(ComposedEditFixture.Given("indices", new object[] { 1 }));
                    given.Add(ComposedEditFixture.Given(Axis, "x"));
                    break;

                default:
                    given.Add(ComposedEditFixture.Given("indices", new object[] { 0 }));
                    break;
            }

            return given;
        }

        private int Settled()
        {
            int handle = Standard("x");
            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            return handle;
        }

        private int Standard(string axis)
        {
            V3 partner = axis == "x"
                ? new V3(-2f, 3f, 5f)
                : axis == "y" ? new V3(2f, -3f, 5f) : new V3(2f, 3f, -5f);
            int handle = Scene();
            AddBone("左腕", LeftBase, LeftNow);
            AddBone(
                "右腕", partner, new V3(partner.X + 0.125f, partner.Y - 0.25f, partner.Z + 0.5f));
            AddBone("首", Elsewhere, Elsewhere);

            return handle;
        }

        private int Scene()
        {
            FakePmx held = new FakePmx();
            Fill(held);
            _held = held;
            Fill(_fixture.Model);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });
        }

        private static void Fill(FakePmx model)
        {
            model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            model.Vertex.Add(new FakeVertex(1f, 0f, 0f));
            model.Vertex.Add(new FakeVertex(0f, 1f, 0f));
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(model.Vertex[0], model.Vertex[1], model.Vertex[2]));
            model.Material.Add(material);
        }

        private void AddBone(string name, V3 baseAt, V3 currentAt)
        {
            _held.Bone.Add(new FakeBone(name) { Position = baseAt });
            _fixture.Model.Bone.Add(new FakeBone(name) { Position = currentAt });
        }

        private void Offsets(int index, V3 inTheCopy, V3 now)
        {
            _held.Bone[index].ToOffset = inTheCopy;
            _fixture.Model.Bone[index].ToOffset = now;
        }

        private void Tip(int index)
        {
            _held.Bone[index].ToBone = _held.Bone[3];
            _fixture.Model.Bone[index].ToBone = _fixture.Model.Bone[3];
        }

        private static List<KeyValuePair<string, object>> Full(int handle, string axis, params int[] picked)
        {
            return new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", MirrorDisplacement),
                ComposedEditFixture.Given("indices", picked.Cast<object>().ToArray()),
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
                ModelEditBones.ToolName, ComposedEditFixture.Arguments(given.ToArray()));
        }

        private IList<IPXBone> Bones()
        {
            return _fixture.Model.Bone
                .Select(bone => (IPXBone)new FakeBone(bone.Name)
                {
                    Position = bone.Position,
                    ToOffset = bone.ToOffset,
                })
                .ToList();
        }

        private void AssertBonesUnchanged(IList<IPXBone> before)
        {
            Assert.Equal(before.Count, _fixture.Model.Bone.Count);
            for (int at = 0; at < before.Count; at++)
            {
                IPXBone found = _fixture.Model.Bone[at];
                Assert.Equal(before[at].Position.X, found.Position.X);
                Assert.Equal(before[at].Position.Y, found.Position.Y);
                Assert.Equal(before[at].Position.Z, found.Position.Z);
                Assert.Equal(before[at].ToOffset.X, found.ToOffset.X);
                Assert.Equal(before[at].ToOffset.Y, found.ToOffset.Y);
                Assert.Equal(before[at].ToOffset.Z, found.ToOffset.Z);
            }
        }

        private void AssertPosition(int index, float x, float y, float z)
        {
            V3 found = _fixture.Model.Bone[index].Position;
            Assert.Equal(x, found.X, Digits);
            Assert.Equal(y, found.Y, Digits);
            Assert.Equal(z, found.Z, Digits);
        }

        private void AssertOffset(int index, float x, float y, float z)
        {
            V3 found = _fixture.Model.Bone[index].ToOffset;
            Assert.Equal(x, found.X, Digits);
            Assert.Equal(y, found.Y, Digits);
            Assert.Equal(z, found.Z, Digits);
        }
    }
}
