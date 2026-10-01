using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditVerticesMirrorDisplacementTests : IDisposable
    {
        private const string MirrorDisplacement = "mirrorDisplacement";

        private const string Axis = "axis";

        private const string BasePmxHandle = "basePmxHandle";

        private const string Changed = "changed";

        private const int Digits = 4;

        private static readonly V3 LeftBase = new V3(2f, 3f, 5f);

        private static readonly V3 LeftNow = new V3(2.5f, 3.25f, 4.25f);

        private static readonly V3 LeftNormal = new V3(0.6f, 0.64f, 0.48f);

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
        public void ThePartnerAtTheMirroredSpotGetsTheMirroredDisplacementAddedToWhereItIsNowOnEachAxis(
            string axis, float x, float y, float z)
        {
            int handle = Standard(axis);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, axis, 0)));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(1, _fixture.Commits);
            AssertAt(1, x, y, z);
            AssertAt(0, LeftNow.X, LeftNow.Y, LeftNow.Z);
        }

        [Theory]
        [InlineData("x", -0.6f, 0.64f, 0.48f)]
        [InlineData("y", 0.6f, -0.64f, 0.48f)]
        [InlineData("z", 0.6f, 0.64f, -0.48f)]
        public void ThePartnerNormalIsThePickedVertexNormalNowMirroredAcrossTheAxis(
            string axis, float x, float y, float z)
        {
            int handle = Standard(axis);

            ComposedEditFixture.Value(Run(Full(handle, axis, 0)));

            AssertNormal(1, x, y, z);
            AssertNormal(0, LeftNormal.X, LeftNormal.Y, LeftNormal.Z);
        }

        [Fact]
        public void ThePartnerIsFoundAtTheMirroredSpotInTheCopyNotWhereVerticesAreNow()
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

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            Assert.Equal(1, value[Changed]);
            AssertAt(1, -4.5f, 0.25f, -0.75f);
            AssertAt(2, -2f, 3f, 5f);
            AssertAt(3, -2.5f, 3.25f, 4.25f);
            AssertAt(0, LeftNow.X, LeftNow.Y, LeftNow.Z);
        }

        [Fact]
        public void APickedVertexWithNoVertexAtItsMirroredSpotStaysWhileTheOthersCarryOn()
        {
            V3[] baseline =
            {
                LeftBase, new V3(-2f, 3f, 5f), new V3(4f, 6f, 8f), new V3(11f, 12f, 13f),
            };
            V3[] current =
            {
                LeftNow, new V3(-2f, 3f, 5f), new V3(4.25f, 6.5f, 7.5f), new V3(11f, 12f, 13f),
            };
            int handle = Scene(baseline, current);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0, 2)));

            Assert.Equal(1, value[Changed]);
            AssertAt(1, -2.5f, 3.25f, 4.25f);
            AssertAt(2, 4.25f, 6.5f, 7.5f);
            AssertAt(3, 11f, 12f, 13f);
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
            int handle = Scene(baseline, Now(baseline, LeftNow));

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            Assert.Equal(found ? 1 : 0, value[Changed]);
            if (found)
            {
                AssertAt(1, -2.5f, 3.25f, 4.25f + gap);
            }
            else
            {
                AssertAt(1, -2f, 3f, 5f + gap);
            }
        }

        [Fact]
        public void AVertexOnTheAxisIsItsOwnPartnerAndKeepsOnlyTheSymmetricPartOfItsDisplacementAndNormal()
        {
            V3[] baseline =
            {
                new V3(0f, 3f, 5f), new V3(7f, 8f, 9f), new V3(11f, 12f, 13f),
            };
            int handle = Scene(baseline, Now(baseline, new V3(0.25f, 3.25f, 4.25f)));
            SetNormals(0, new V3(0f, 0f, 1f), LeftNormal);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            Assert.Equal(1, value[Changed]);
            AssertAt(0, 0f, 3.25f, 4.25f);
            AssertNormal(0, 0f, 0.8f, 0.6f);
        }

        [Fact]
        public void WhenThePartnerIsPickedTooEachGetsTheOthersDisplacementMirroredFromBeforeEitherMoved()
        {
            V3[] baseline =
            {
                LeftBase, new V3(-2f, 3f, 5f), new V3(7f, 8f, 9f), new V3(11f, 12f, 13f),
            };
            V3[] current = (V3[])baseline.Clone();
            current[0] = LeftNow;
            current[1] = new V3(-1.75f, 2.5f, 5.125f);
            int handle = Scene(baseline, current);
            SetNormals(0, new V3(0f, 0f, 1f), LeftNormal);
            SetNormals(1, new V3(0f, 0f, -1f), new V3(0.8f, 0f, 0.6f));

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0, 1)));

            Assert.Equal(2, value[Changed]);
            AssertAt(0, 2.25f, 2.75f, 4.375f);
            AssertAt(1, -2.25f, 2.75f, 4.375f);
            AssertNormal(0, -0.8f, 0f, 0.6f);
            AssertNormal(1, -0.6f, 0.64f, 0.48f);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SurroundingsInTheCopyDecideWhichOfTwoVerticesAtTheSamePositionIsThePartner(bool firstIsMatching)
        {
            int handle = Layered(firstIsMatching, false);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0, 3)));

            AssertLayered(value, firstIsMatching);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TheSurroundingsAreComparedAsTheyAreInTheCopyNotAsTheyAreNow(bool firstIsMatching)
        {
            int handle = Layered(firstIsMatching, true);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Full(handle, "x", 0, 3)));

            AssertLayered(value, firstIsMatching);
        }

        [Fact]
        public void OnlyThePickedVerticesCarryTheirDisplacementOverAndOnlyThePartnersMove()
        {
            V3[] baseline =
            {
                LeftBase, new V3(-2f, 3f, 5f), new V3(4f, 6f, 8f), new V3(-4f, 6f, 8f),
            };
            V3[] current = (V3[])baseline.Clone();
            current[0] = LeftNow;
            current[2] = new V3(4.25f, 6.5f, 7.5f);
            int handle = Scene(baseline, current);
            SetNormals(0, new V3(0f, 0f, 1f), LeftNormal);
            V3[] positions = Positions();
            V3[] normals = Normals();

            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            AssertAt(1, -2.5f, 3.25f, 4.25f);
            for (int at = 0; at < positions.Length; at++)
            {
                if (at != 1)
                {
                    AssertSame(positions[at], at);
                    AssertSameNormal(normals[at], at);
                }
            }
        }

        [Fact]
        public void MirroringChangesNeitherTheCopyNorItsCounts()
        {
            int handle = Standard("x");
            V3[] positions = _held.Vertex.Select(vertex => vertex.Position).ToArray();
            V3[] normals = _held.Vertex.Select(vertex => vertex.Normal).ToArray();

            ComposedEditFixture.Value(Run(Full(handle, "x", 0)));

            Assert.Equal(positions.Length, _held.Vertex.Count);
            Assert.Single(((FakeMaterial)_held.Material[0]).Faces);
            Assert.Empty(_held.Bone);
            for (int at = 0; at < positions.Length; at++)
            {
                Assert.Equal(positions[at].X, _held.Vertex[at].Position.X);
                Assert.Equal(positions[at].Y, _held.Vertex[at].Position.Y);
                Assert.Equal(positions[at].Z, _held.Vertex[at].Position.Z);
                Assert.Equal(normals[at].X, _held.Vertex[at].Normal.X);
                Assert.Equal(normals[at].Y, _held.Vertex[at].Normal.Y);
                Assert.Equal(normals[at].Z, _held.Vertex[at].Normal.Z);
            }
        }

        [Fact]
        public void LeavingOutTheCopyIsRefused()
        {
            int handle = Settled();
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Without(Full(handle, "x", 0), BasePmxHandle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BasePmxHandle, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void LeavingOutTheAxisIsRefused()
        {
            int handle = Settled();
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Without(Full(handle, "x", 0), Axis));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Axis, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData("w")]
        [InlineData("")]
        [InlineData("X")]
        [InlineData(1.0)]
        public void AnAxisThatIsNotOneOfTheThreeIsRefused(object given)
        {
            int handle = Settled();
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(With(Full(handle, "x", 0), Axis, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Axis, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void ACopyHandleThatIsNotHeldIsRefused()
        {
            int handle = Settled();
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Full(handle + 1000, "x", 0));

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedEditFixture.Code(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("face")]
        [InlineData("bone")]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing)
        {
            Settled();
            V3[] before = Positions();

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
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(99)]
        public void AVertexOutsideTheVerticesIsRefused(int outside)
        {
            int handle = Settled();
            V3[] before = Positions();

            IDictionary<string, object> envelope = Run(Full(handle, "x", 0, outside));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData("align", BasePmxHandle)]
        [InlineData("weld", BasePmxHandle)]
        [InlineData("weld", Axis)]
        [InlineData("followGuide", Axis)]
        [InlineData(MirrorDisplacement, "guideIndices")]
        [InlineData(MirrorDisplacement, "mode")]
        public void TheCopyAndTheAxisArePassedOnlyToTheOperationsThatTakeThem(string operation, string name)
        {
            int handle = Settled();
            V3[] before = Positions();
            List<KeyValuePair<string, object>> given = Taking(operation, handle);
            object value = name == "guideIndices"
                ? new object[] { 0, 1, 2 }
                : name == "mode" ? (object)"rigid" : name == Axis ? (object)"x" : (long)handle;
            given.Add(ComposedEditFixture.Given(name, value));

            IDictionary<string, object> envelope = Run(given);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void TheAxisStillServesTheOperationsThatTookItBefore()
        {
            Settled();

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", "align"),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }),
                ComposedEditFixture.Given(Axis, "y"),
            }));

            Assert.Equal(2, value[Changed]);
            AssertAt(0, 2.5f, 3.125f, 4.25f);
            AssertAt(1, -2.375f, 3.125f, 4.75f);
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

                case "followGuide":
                    given.Add(ComposedEditFixture.Given("indices", new object[] { 3 }));
                    given.Add(ComposedEditFixture.Given("guideIndices", new object[] { 0, 1, 2 }));
                    given.Add(ComposedEditFixture.Given("mode", "rigid"));
                    given.Add(ComposedEditFixture.Given(BasePmxHandle, (long)handle));
                    break;

                case "align":
                    given.Add(ComposedEditFixture.Given("indices", new object[] { 2, 3 }));
                    given.Add(ComposedEditFixture.Given(Axis, "y"));
                    break;

                default:
                    given.Add(ComposedEditFixture.Given("indices", new object[] { 2, 3 }));
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
            V3[] baseline = { LeftBase, partner, new V3(7f, 8f, 9f), new V3(11f, 12f, 13f) };
            V3[] current = (V3[])baseline.Clone();
            current[0] = LeftNow;
            current[1] = new V3(partner.X + 0.125f, partner.Y - 0.25f, partner.Z + 0.5f);
            int handle = Scene(baseline, current);
            SetNormals(0, new V3(0f, 0f, 1f), LeftNormal);
            SetNormals(1, new V3(0f, 0f, -1f), new V3(1f, 0f, 0f));

            return handle;
        }

        private int Layered(bool firstIsMatching, bool surroundingsSwapped)
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
            current[0] = new V3(2.5f, 0.25f, -0.75f);
            current[3] = new V3(2.25f, -0.5f, 0.125f);
            int first = 6;
            int second = 9;
            current[firstIsMatching ? first : second] = new V3(-2.125f, 0f, 0.5f);
            current[firstIsMatching ? second : first] = new V3(-1.75f, 1f, -0.25f);
            if (surroundingsSwapped)
            {
                current[first + 1] = baseline[second + 1];
                current[first + 2] = baseline[second + 2];
                current[second + 1] = baseline[first + 1];
                current[second + 2] = baseline[first + 2];
            }

            int handle = Scene(
                baseline,
                current,
                new[] { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, new[] { 9, 10, 11 } });
            SetNormals(0, new V3(0f, 0f, 1f), LeftNormal);
            SetNormals(3, new V3(0f, 0f, 1f), new V3(0.8f, 0f, 0.6f));

            return handle;
        }

        private void AssertLayered(IDictionary<string, object> value, bool firstIsMatching)
        {
            int matching = firstIsMatching ? 6 : 9;
            int other = firstIsMatching ? 9 : 6;

            Assert.Equal(2, value[Changed]);
            AssertAt(matching, -2.625f, 0.25f, -0.25f);
            AssertNormal(matching, -0.6f, 0.64f, 0.48f);
            AssertAt(other, -2f, 0.5f, -0.125f);
            AssertNormal(other, -0.8f, 0f, 0.6f);
            AssertAt(0, 2.5f, 0.25f, -0.75f);
            AssertAt(3, 2.25f, -0.5f, 0.125f);
        }

        private static V3[] Now(V3[] baseline, V3 pickedNow)
        {
            V3[] current = (V3[])baseline.Clone();
            current[0] = pickedNow;

            return current;
        }

        private int Scene(V3[] baseline, V3[] current, int[][] faces = null)
        {
            FakePmx held = new FakePmx();
            Fill(held, baseline, faces);
            _held = held;
            Fill(_fixture.Model, current, faces);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });
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

        private void SetNormals(int index, V3 inTheCopy, V3 now)
        {
            _held.Vertex[index].Normal = inTheCopy;
            _fixture.Model.Vertex[index].Normal = now;
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
                ModelEditVertices.ToolName, ComposedEditFixture.Arguments(given.ToArray()));
        }

        private V3[] Positions()
        {
            return _fixture.Model.Vertex.Select(vertex => vertex.Position).ToArray();
        }

        private V3[] Normals()
        {
            return _fixture.Model.Vertex.Select(vertex => vertex.Normal).ToArray();
        }

        private void AssertAt(int index, float x, float y, float z)
        {
            V3 found = _fixture.Model.Vertex[index].Position;
            Assert.Equal(x, found.X, Digits);
            Assert.Equal(y, found.Y, Digits);
            Assert.Equal(z, found.Z, Digits);
        }

        private void AssertNormal(int index, float x, float y, float z)
        {
            V3 found = _fixture.Model.Vertex[index].Normal;
            Assert.Equal(x, found.X, Digits);
            Assert.Equal(y, found.Y, Digits);
            Assert.Equal(z, found.Z, Digits);
        }

        private void AssertSame(V3 wanted, int index)
        {
            V3 found = _fixture.Model.Vertex[index].Position;
            Assert.Equal(wanted.X, found.X);
            Assert.Equal(wanted.Y, found.Y);
            Assert.Equal(wanted.Z, found.Z);
        }

        private void AssertSameNormal(V3 wanted, int index)
        {
            V3 found = _fixture.Model.Vertex[index].Normal;
            Assert.Equal(wanted.X, found.X);
            Assert.Equal(wanted.Y, found.Y);
            Assert.Equal(wanted.Z, found.Z);
        }

        private void AssertUnchanged(V3[] before)
        {
            Assert.Equal(before.Length, _fixture.Model.Vertex.Count);
            for (int at = 0; at < before.Length; at++)
            {
                AssertSame(before[at], at);
            }
        }
    }
}
