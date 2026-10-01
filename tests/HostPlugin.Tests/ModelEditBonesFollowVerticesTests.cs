using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditBonesFollowVerticesTests : IDisposable
    {
        private const string FollowVertices = "followVertices";

        private const string Radius = "radius";

        private const string BasePmxHandle = "basePmxHandle";

        private const string QuarterTurnAboutY = "quarterTurnAboutY";

        private const string ThirdTurnAboutDiagonal = "thirdTurnAboutDiagonal";

        private const int Digits = 4;

        private static readonly V3 BoneAt = new V3(0.2f, 0.1f, 0.3f);

        private static readonly V3 Offset = new V3(2f, 0f, 1f);

        private static readonly V3[] Around =
        {
            new V3(0.5f, 0f, 0f), new V3(0f, 0.5f, 0f), new V3(0f, 0f, 0.5f), new V3(0.4f, 0.4f, 0.4f),
        };

        private static readonly V3[] OnTheRadius =
        {
            new V3(0.5f, 0f, 0f), new V3(0f, 0.5f, 0f), new V3(0f, 0f, 0.5f),
        };

        private static readonly V3[] FarSide =
        {
            new V3(20.5f, 0f, 0f), new V3(20f, 0.5f, 0f), new V3(20f, 0f, 0.5f),
        };

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakePmx _held;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData(QuarterTurnAboutY, 2.3f, 3.1f, -1.2f, 1f, 0f, -2f)]
        [InlineData(ThirdTurnAboutDiagonal, 2.3f, 3.2f, -0.9f, 1f, 2f, 0f)]
        public void ABoneWhoseTipIsAnOffsetMovesAndTurnsItsOffsetWithTheFitOfTheVerticesAroundIt(
            string turn, float x, float y, float z, float offsetX, float offsetY, float offsetZ)
        {
            int handle = Around4(turn);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);
            V3[] vertices = Positions();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                handle, 1.0, new[] { 0 })));

            Assert.Equal(1, value["changed"]);
            Assert.Equal(1, _fixture.Commits);
            AssertBone(0, x, y, z, offsetX, offsetY, offsetZ);
            Assert.Null(_fixture.Model.Bone[0].ToBone);
            AssertVertices(vertices);
            Assert.Equal(BoneAt.X, _held.Bone[0].Position.X);
            Assert.Equal(BoneAt.Y, _held.Bone[0].Position.Y);
            Assert.Equal(BoneAt.Z, _held.Bone[0].Position.Z);
            Assert.Equal(Offset.X, _held.Bone[0].ToOffset.X);
            Assert.Equal(Offset.Z, _held.Bone[0].ToOffset.Z);
        }

        [Fact]
        public void ABoneWhoseTipIsAnotherBoneMovesAndKeepsItsTipAndItsOffset()
        {
            int handle = Around4(QuarterTurnAboutY);
            AddBone("腕", BoneAt, BoneAt, new V3(7f, 8f, 9f), new V3(7f, 8f, 9f));
            AddBone("手", new V3(3f, 3f, 3f), new V3(3f, 3f, 3f), Offset, Offset);
            _fixture.Model.Bone[0].ToBone = _fixture.Model.Bone[1];
            _held.Bone[0].ToBone = _held.Bone[1];

            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));

            AssertBone(0, 2.3f, 3.1f, -1.2f, 7f, 8f, 9f);
            Assert.Same(_fixture.Model.Bone[1], _fixture.Model.Bone[0].ToBone);
            AssertBone(1, 3f, 3f, 3f, 2f, 0f, 1f);
        }

        [Fact]
        public void ABoneIsCarriedFromItsPositionAndOffsetInTheCopyNotFromWhereTheyAreNow()
        {
            int handle = Around4(QuarterTurnAboutY);
            AddBone("腕", BoneAt, new V3(9f, 9f, 9f), Offset, new V3(5f, 5f, 5f));

            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));

            AssertBone(0, 2.3f, 3.1f, -1.2f, 1f, 0f, -2f);
        }

        [Fact]
        public void TheVerticesAreFoundAroundTheBoneByTheirPositionsInTheCopyNotByWhereTheyAreNow()
        {
            V3[] baseline = Around.Concat(new[]
            {
                new V3(10f, 10f, 10f), new V3(11f, 10f, 10f), new V3(10f, 11f, 10f),
            }).ToArray();
            V3[] current = Around.Select(point => Moved(QuarterTurnAboutY, point))
                .Concat(new[] { new V3(0.3f, 0.1f, 0.3f), new V3(0.1f, 0.3f, 0.3f), new V3(0.2f, 0.1f, 0.1f) })
                .ToArray();
            int handle = Scene(baseline, current);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);

            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));

            AssertBone(0, 2.3f, 3.1f, -1.2f, 1f, 0f, -2f);
        }

        [Fact]
        public void TheVerticesBeyondTheRadiusDoNotTakePartInTheFit()
        {
            V3[] baseline = Around.Concat(new[] { new V3(5f, 5f, 5f) }).ToArray();
            V3[] current = Around.Select(point => Moved(QuarterTurnAboutY, point))
                .Concat(new[] { new V3(-7f, 0f, 3f) })
                .ToArray();
            int handle = Scene(baseline, current);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);

            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));

            AssertBone(0, 2.3f, 3.1f, -1.2f, 1f, 0f, -2f);
        }

        [Theory]
        [InlineData(0.5, true)]
        [InlineData(0.4999, false)]
        public void TheVerticesExactlyAtTheRadiusTakePartInTheFitAndThoseJustBeyondItDoNot(
            double radius, bool follows)
        {
            int handle = Scene(OnTheRadius, OnTheRadius.Select(point => Moved(QuarterTurnAboutY, point)).ToArray());
            AddBone("腕", new V3(0f, 0f, 0f), new V3(0f, 0f, 0f), Offset, Offset);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                handle, radius, new[] { 0 })));

            Assert.Equal(follows ? 1 : 0, value["changed"]);
            if (follows)
            {
                AssertBone(0, 2f, 3f, -1f, 1f, 0f, -2f);
            }
            else
            {
                AssertBone(0, 0f, 0f, 0f, 2f, 0f, 1f);
            }
        }

        [Theory]
        [InlineData("none")]
        [InlineData("one")]
        [InlineData("two")]
        [InlineData("collinear")]
        public void ABoneWithTooFewOrCollinearVerticesAroundItStaysWhileTheOthersInTheSameCallMove(
            string around)
        {
            V3[] near = around == "none"
                ? new V3[0]
                : around == "one"
                    ? new[] { new V3(0.5f, 0f, 0f) }
                    : around == "two"
                        ? new[] { new V3(0.5f, 0f, 0f), new V3(0f, 0.5f, 0f) }
                        : new[] { new V3(0.3f, 0f, 0f), new V3(0.5f, 0f, 0f), new V3(0.7f, 0f, 0f) };
            int handle = Pair(near);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(With(
                Full(handle, 1.0, new[] { 0 }), "all", true, "indices")));

            Assert.Equal(1, value["changed"]);
            AssertBone(0, 0f, 0f, 0f, 2f, 0f, 1f);
            AssertBone(1, 2f, 3f, -21f, 1f, 0f, -2f);
        }

        [Fact]
        public void OnlyTheBonesThatWerePickedMove()
        {
            int handle = Pair(OnTheRadius);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(
                handle, 1.0, new[] { 0 })));

            Assert.Equal(1, value["changed"]);
            AssertBone(0, 2f, 3f, -1f, 1f, 0f, -2f);
            AssertBone(1, 20f, 0f, 0f, 2f, 0f, 1f);
        }

        [Fact]
        public void AllTheBonesMoveWhenAllAreGiven()
        {
            int handle = Pair(OnTheRadius);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(With(
                Full(handle, 1.0, new[] { 0 }), "all", true, "indices")));

            Assert.Equal(2, value["changed"]);
            AssertBone(0, 2f, 3f, -1f, 1f, 0f, -2f);
            AssertBone(1, 2f, 3f, -21f, 1f, 0f, -2f);
        }

        [Fact]
        public void ABoneOutsideTheBonesIsRefused()
        {
            int handle = Pair(OnTheRadius);
            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(Full(handle, 1.0, new[] { 2 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            AssertBonesUnchanged(before);
        }

        [Fact]
        public void LeavingOutTheRadiusIsRefused()
        {
            int handle = Around4(QuarterTurnAboutY);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);
            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(Without(
                Full(handle, 1.0, new[] { 0 }), Radius));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Radius, ComposedEditFixture.Message(envelope));
            AssertBonesUnchanged(before);
        }

        [Theory]
        [MemberData(nameof(BadNumbers))]
        public void ARadiusThatIsNegativeNotFiniteOrNotANumberIsRefused(object given)
        {
            int handle = Around4(QuarterTurnAboutY);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);
            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(With(
                Full(handle, 1.0, new[] { 0 }), Radius, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Radius, ComposedEditFixture.Message(envelope));
            AssertBonesUnchanged(before);
        }

        public static IEnumerable<object[]> BadNumbers()
        {
            yield return new object[] { -0.5 };
            yield return new object[] { "0.5" };
            yield return new object[] { new object[] { 0.5 } };
            yield return new object[] { double.PositiveInfinity };
            yield return new object[] { double.NaN };
        }

        [Fact]
        public void LeavingOutTheCopyIsRefused()
        {
            int handle = Around4(QuarterTurnAboutY);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);
            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(Without(
                Full(handle, 1.0, new[] { 0 }), BasePmxHandle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BasePmxHandle, ComposedEditFixture.Message(envelope));
            AssertBonesUnchanged(before);
        }

        [Fact]
        public void ACopyHandleThatIsNotHeldIsRefused()
        {
            int handle = Around4(QuarterTurnAboutY);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);
            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));
            IList<IPXBone> before = Bones();

            IDictionary<string, object> envelope = Run(Full(handle + 1000, 1.0, new[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedEditFixture.Code(envelope));
            AssertBonesUnchanged(before);
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("face")]
        [InlineData("bone")]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing)
        {
            int same = Around4(QuarterTurnAboutY);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);
            ComposedEditFixture.Value(Run(Full(same, 1.0, new[] { 0 })));
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
            IDictionary<string, object> envelope = Run(Full(handle, 1.0, new[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            AssertBonesUnchanged(before);
        }

        [Theory]
        [InlineData(Radius)]
        [InlineData(BasePmxHandle)]
        public void TheRadiusAndTheCopyArePassedOnlyToFollowingTheVertices(string name)
        {
            int handle = Around4(QuarterTurnAboutY);
            AddBone("腕", BoneAt, BoneAt, Offset, Offset);
            ComposedEditFixture.Value(Run(Full(handle, 1.0, new[] { 0 })));
            IList<IPXBone> before = Bones();
            object given = name == Radius ? (object)1.0 : (long)handle;

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditBones.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", ModelEditBones.ResetLocalAxis),
                    ComposedEditFixture.Given("indices", new object[] { 0 }),
                    ComposedEditFixture.Given(name, given)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertBonesUnchanged(before);
        }

        private int Around4(string turn)
        {
            V3[] vertices = Around.Concat(new[] { new V3(5f, 5f, 5f) }).ToArray();

            return Scene(
                vertices,
                vertices.Select((point, at) => at < Around.Length ? Moved(turn, point) : new V3(-7f, 0f, 3f)).ToArray());
        }

        private int Pair(V3[] nearTheFirst)
        {
            V3[] baseline = FarSide.Concat(nearTheFirst).ToArray();
            int handle = Scene(baseline, baseline.Select(point => Moved(QuarterTurnAboutY, point)).ToArray());
            AddBone("近", new V3(0f, 0f, 0f), new V3(0f, 0f, 0f), Offset, Offset);
            AddBone("遠", new V3(20f, 0f, 0f), new V3(20f, 0f, 0f), Offset, Offset);

            return handle;
        }

        private static V3 Moved(string turn, V3 point)
        {
            V3 turned = turn == QuarterTurnAboutY
                ? new V3(point.Z, point.Y, -point.X)
                : new V3(point.Z, point.X, point.Y);

            return new V3(turned.X + 2f, turned.Y + 3f, turned.Z - 1f);
        }

        private int Scene(V3[] baseline, V3[] current)
        {
            FakePmx held = new FakePmx();
            Fill(held, baseline);
            _held = held;
            Fill(_fixture.Model, current);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });
        }

        private static void Fill(FakePmx model, V3[] positions)
        {
            foreach (V3 position in positions)
            {
                model.Vertex.Add(new FakeVertex(position.X, position.Y, position.Z));
            }

            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(model.Vertex[0], model.Vertex[1], model.Vertex[2]));
            model.Material.Add(material);
        }

        private void AddBone(string name, V3 baseAt, V3 currentAt, V3 baseOffset, V3 currentOffset)
        {
            _held.Bone.Add(new FakeBone(name) { Position = baseAt, ToOffset = baseOffset });
            _fixture.Model.Bone.Add(new FakeBone(name) { Position = currentAt, ToOffset = currentOffset });
        }

        private static List<KeyValuePair<string, object>> Full(int handle, double radius, int[] picked)
        {
            return new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", FollowVertices),
                ComposedEditFixture.Given("indices", picked.Cast<object>().ToArray()),
                ComposedEditFixture.Given(Radius, radius),
                ComposedEditFixture.Given(BasePmxHandle, (long)handle),
            };
        }

        private static List<KeyValuePair<string, object>> Without(
            List<KeyValuePair<string, object>> given, string name)
        {
            return given.Where(pair => pair.Key != name).ToList();
        }

        private static List<KeyValuePair<string, object>> With(
            List<KeyValuePair<string, object>> given, string name, object value, string replacing = null)
        {
            List<KeyValuePair<string, object>> made = Without(Without(given, name), replacing);
            made.Add(ComposedEditFixture.Given(name, value));

            return made;
        }

        private IDictionary<string, object> Run(List<KeyValuePair<string, object>> given)
        {
            return _fixture.Call(
                ModelEditBones.ToolName, ComposedEditFixture.Arguments(given.ToArray()));
        }

        private V3[] Positions()
        {
            return _fixture.Model.Vertex.Select(vertex => vertex.Position).ToArray();
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

        private void AssertVertices(V3[] before)
        {
            Assert.Equal(before.Length, _fixture.Model.Vertex.Count);
            for (int at = 0; at < before.Length; at++)
            {
                V3 found = _fixture.Model.Vertex[at].Position;
                Assert.Equal(before[at].X, found.X);
                Assert.Equal(before[at].Y, found.Y);
                Assert.Equal(before[at].Z, found.Z);
            }
        }

        private void AssertBone(
            int index, float x, float y, float z, float offsetX, float offsetY, float offsetZ)
        {
            IPXBone found = _fixture.Model.Bone[index];
            Assert.Equal(x, found.Position.X, Digits);
            Assert.Equal(y, found.Position.Y, Digits);
            Assert.Equal(z, found.Position.Z, Digits);
            Assert.Equal(offsetX, found.ToOffset.X, Digits);
            Assert.Equal(offsetY, found.ToOffset.Y, Digits);
            Assert.Equal(offsetZ, found.ToOffset.Z, Digits);
        }
    }
}
