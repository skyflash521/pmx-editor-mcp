using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditNormalsRotateFromBaseTests : IDisposable
    {
        private const string ToolName = "model_edit_normals";

        private const string RotateFromBase = "rotateFromBase";

        private const string BasePmxHandle = "basePmxHandle";

        private const string Changed = "changed";

        private const string QuarterTurnAboutY = "quarterTurnAboutY";

        private const string ThirdTurnAboutDiagonal = "thirdTurnAboutDiagonal";

        private const string HalfTurnAboutX = "halfTurnAboutX";

        private const string EighthTurnAboutZ = "eighthTurnAboutZ";

        private const string QuarterTurnAboutZ = "quarterTurnAboutZ";

        private const string HalfTurnAboutZ = "halfTurnAboutZ";

        private const string SixthTurnAboutZ = "sixthTurnAboutZ";

        private const double Tight = 1e-4;

        private const double Loose = 3e-3;

        private const double Resting = 1e-5;

        private static readonly V3 Shift = new V3(2f, 3f, -1f);

        private static readonly V3 BaseNormal = new V3(0f, -1f, 0f);

        private static readonly V3[] Triangle =
        {
            new V3(1f, 0f, 0f), new V3(0f, 2f, 0f), new V3(0f, 0f, 3f),
        };

        private static readonly V3[] Plane =
        {
            new V3(0f, 0f, 0f), new V3(2f, 0f, 0f), new V3(0f, 1f, 0f),
        };

        private static readonly V3[] Normals =
        {
            new V3(1f, 2f, 2f), new V3(2f, -1f, 2f), new V3(-2f, 2f, 1f),
        };

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakePmx _held;

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData(QuarterTurnAboutY)]
        [InlineData(ThirdTurnAboutDiagonal)]
        [InlineData(HalfTurnAboutX)]
        [InlineData(EighthTurnAboutZ)]
        public void EveryNormalTurnsByTheRotationThatCarriedItsFace(string turn)
        {
            int handle = Scene(
                Triangle, Triangle.Select(point => Moved(turn, point)).ToArray(), Normals, new[] { 0, 1, 2 });

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(handle));

            Assert.Equal(3, value[Changed]);
            Assert.Equal(1, _fixture.Commits);
            for (int at = 0; at < Normals.Length; at++)
            {
                AssertNormal(at, Turn(turn, Normals[at]), Tight);
            }
        }

        [Fact]
        public void RewritingTheNormalsFromTheCopyRemakesOnlyTheVerticesInTheView()
        {
            int handle = Turned();

            ComposedEditFixture.Value(Run(handle));

            Assert.Equal(new[] { ElementKinds.Vertex }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Theory]
        [InlineData(QuarterTurnAboutZ)]
        [InlineData(HalfTurnAboutZ)]
        [InlineData(SixthTurnAboutZ)]
        public void ARotationAboutTheFaceNormalTurnsTheNormalsAlthoughTheFaceNormalStays(string turn)
        {
            V3[] tilted = { new V3(0.6f, 0f, 0.8f), new V3(0f, 0.6f, 0.8f), new V3(-0.6f, 0f, 0.8f) };
            V3[] current = Plane.Select(point => Moved(turn, point)).ToArray();
            AssertDirection(0f, 0f, 1f, FaceNormal(Plane));
            AssertDirection(0f, 0f, 1f, FaceNormal(current));
            int handle = Scene(Plane, current, tilted, new[] { 0, 1, 2 });

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(handle));

            Assert.Equal(3, value[Changed]);
            for (int at = 0; at < tilted.Length; at++)
            {
                AssertNormal(at, Turn(turn, tilted[at]), Tight);
            }
        }

        [Fact]
        public void TheAngleBetweenACraftedNormalAndItsFaceSurvivesTheRotation()
        {
            V3[] crafted = { new V3(0.8f, 0f, 0.6f), new V3(0f, -0.6f, 0.8f), new V3(0.6f, 0.8f, 0f) };
            V3[] current = Plane.Select(point => Moved(ThirdTurnAboutDiagonal, point)).ToArray();
            double[] faceBefore = FaceNormal(Plane);
            double[] faceNow = FaceNormal(current);
            Assert.True(Angle(crafted[0], faceBefore) > 0.5);
            int handle = Scene(Plane, current, crafted, new[] { 0, 1, 2 });

            ComposedEditFixture.Value(Run(handle));

            for (int at = 0; at < crafted.Length; at++)
            {
                Assert.Equal(
                    Angle(crafted[at], faceBefore), Angle(_fixture.Model.Vertex[at].Normal, faceNow), Tight);
                AssertNormal(at, Turn(ThirdTurnAboutDiagonal, crafted[at]), Tight);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AVertexOfSeveralFacesTurnsByTheirRotationsWeighedByArea(bool smallFaceFirst)
        {
            double twenty = Math.PI / 9d;
            V3[] baseline =
            {
                new V3(0f, 0f, 0f), new V3(3f, 0f, 0f), new V3(0f, 2f, 0f),
                new V3(2f, 0f, 0f), new V3(0f, 0f, 1f),
            };
            V3[] current = (V3[])baseline.Clone();
            current[3] = new V3((float)(2d * Math.Cos(twenty)), (float)(2d * Math.Sin(twenty)), 0f);
            V3[] normals = baseline.Select(point => new V3(1f, 0f, 2f)).ToArray();
            int[] large = { 0, 1, 2 };
            int[] small = { 0, 3, 4 };
            int handle = Scene(
                baseline,
                current,
                normals,
                (smallFaceFirst ? small.Concat(large) : large.Concat(small)).ToArray());
            double five = Math.PI / 36d;

            ComposedEditFixture.Value(Run(handle, Given("indices", new object[] { 0 })));

            AssertNormal(0, new V3((float)Math.Cos(five), (float)Math.Sin(five), 2f), Loose);
        }

        [Fact]
        public void TheVerticesOfAFaceThatDidNotMoveKeepTheirNormals()
        {
            V3[] resting = Triangle.Select(point => new V3(point.X + 4f, point.Y, point.Z)).ToArray();
            V3[] normals =
            {
                Normals[0], Normals[1], Normals[2], new V3(0f, 0f, 1f), new V3(0.6f, 0.8f, 0f), new V3(1f, 2f, 2f),
            };
            int handle = Scene(
                Triangle.Concat(resting).ToArray(),
                Triangle.Select(point => Moved(QuarterTurnAboutY, point)).Concat(resting).ToArray(),
                normals,
                new[] { 0, 1, 2, 3, 4, 5 });

            ComposedEditFixture.Value(Run(handle));

            for (int at = 0; at < 3; at++)
            {
                AssertNormal(at, Turn(QuarterTurnAboutY, normals[at]), Tight);
            }

            for (int at = 3; at < 6; at++)
            {
                AssertNormal(at, normals[at], Resting);
            }
        }

        [Theory]
        [InlineData("inBoth")]
        [InlineData("onlyNow")]
        [InlineData("onlyInTheCopy")]
        public void AFaceWithoutAreaInTheCopyOrNowDoesNotCount(string where)
        {
            V3[] baseline;
            V3[] current;
            Shapeless(where, out baseline, out current);
            V3[] normals = Normals.Concat(new[] { new V3(0f, 0f, 1f), new V3(1f, 0f, 0f) }).ToArray();
            int handle = Scene(baseline, current, normals, new[] { 0, 1, 2, 0, 3, 4 });

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(handle));

            Assert.Equal(3, value[Changed]);
            for (int at = 0; at < 3; at++)
            {
                AssertNormal(at, Turn(QuarterTurnAboutY, normals[at]), Tight);
            }

            AssertSame(3, normals[3]);
            AssertSame(4, normals[4]);
        }

        [Fact]
        public void AVerySlenderFaceWithAreaStillCounts()
        {
            V3[] sliver = { new V3(0f, 0f, 0f), new V3(1000f, 0f, 0f), new V3(2000f, 1e-7f, 0f) };
            int handle = Scene(
                sliver, sliver.Select(point => Turn(QuarterTurnAboutY, point)).ToArray(), Normals, new[] { 0, 1, 2 });

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(handle));

            Assert.Equal(3, value[Changed]);
            for (int at = 0; at < Normals.Length; at++)
            {
                AssertNormal(at, Turn(QuarterTurnAboutY, Normals[at]), Tight);
            }
        }

        [Fact]
        public void AVertexUsedByNoFaceKeepsItsNormal()
        {
            V3[] normals = Normals.Concat(new[] { new V3(0f, 0f, 1f) }).ToArray();
            int handle = Scene(
                Triangle.Concat(new[] { new V3(9f, 9f, 9f) }).ToArray(),
                Triangle.Select(point => Moved(QuarterTurnAboutY, point)).Concat(new[] { new V3(5f, 5f, 5f) }).ToArray(),
                normals,
                new[] { 0, 1, 2 });

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(handle));

            Assert.Equal(3, value[Changed]);
            AssertSame(3, normals[3]);
        }

        [Theory]
        [InlineData("all", new[] { 0, 1, 2, 3, 4, 5 })]
        [InlineData("indices", new[] { 4 })]
        [InlineData("selected", new[] { 3 })]
        [InlineData("materialIndices", new[] { 3, 4, 5 })]
        public void OnlyThePickedVerticesTurn(string how, int[] turning)
        {
            V3[] second = Triangle.Select(point => new V3(point.X + 6f, point.Y, point.Z)).ToArray();
            V3[] normals = Normals.Concat(new[] { new V3(3f, 0f, 0f), new V3(0f, 3f, 0f), new V3(-1f, 2f, -2f) }).ToArray();
            int handle = Scene(
                Triangle.Concat(second).ToArray(),
                Triangle.Concat(second).Select(point => Moved(QuarterTurnAboutY, point)).ToArray(),
                normals,
                new[] { 0, 1, 2 },
                new[] { 3, 4, 5 });

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(handle, Pointing(how)));

            Assert.Equal(turning.Length, value[Changed]);
            for (int at = 0; at < normals.Length; at++)
            {
                if (turning.Contains(at))
                {
                    AssertNormal(at, Turn(QuarterTurnAboutY, normals[at]), Tight);
                }
                else
                {
                    AssertSame(at, normals[at]);
                }
            }
        }

        [Fact]
        public void TheLengthOfANormalIsKeptNotMadeIntoOne()
        {
            V3[] lengths = { new V3(0.5f, 0f, 0f), new V3(3f, 4f, 0f), new V3(0f, 0f, -7f) };
            int handle = Scene(
                Triangle, Triangle.Select(point => Moved(QuarterTurnAboutY, point)).ToArray(), lengths, new[] { 0, 1, 2 });

            ComposedEditFixture.Value(Run(handle));

            AssertNormal(0, new V3(0f, 0f, -0.5f), Tight);
            AssertNormal(1, new V3(0f, 4f, -3f), Tight);
            AssertNormal(2, new V3(-7f, 0f, 0f), Tight);
        }

        [Fact]
        public void AZeroNormalStaysZero()
        {
            V3[] withZero = { new V3(0f, 0f, 0f), Normals[1], Normals[2] };
            int handle = Scene(
                Triangle, Triangle.Select(point => Moved(QuarterTurnAboutY, point)).ToArray(), withZero, new[] { 0, 1, 2 });

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(handle));

            Assert.Equal(2, value[Changed]);
            AssertSame(0, withZero[0]);
            AssertNormal(1, Turn(QuarterTurnAboutY, withZero[1]), Tight);
        }

        [Fact]
        public void LeavingOutTheCopyIsRefused()
        {
            int handle = Turned();
            ComposedEditFixture.Value(Run(handle));
            V3[] before = NormalsNow();

            IDictionary<string, object> envelope = Call(Without(Full(handle, All()), BasePmxHandle));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BasePmxHandle, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void ACopyHandleThatIsNotHeldIsRefused()
        {
            int handle = Turned();
            ComposedEditFixture.Value(Run(handle));
            V3[] before = NormalsNow();

            IDictionary<string, object> envelope = Run(handle + 1000);

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedEditFixture.Code(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("face")]
        [InlineData("bone")]
        public void ACopyWhoseCountsDifferFromTheModelIsRefused(string differing)
        {
            int same = Turned();
            ComposedEditFixture.Value(Run(same));
            V3[] before = NormalsNow();

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
            IDictionary<string, object> envelope = Run(handle);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData("average")]
        [InlineData("averageNear")]
        [InlineData("fromFaces")]
        [InlineData("normalize")]
        [InlineData("flip")]
        [InlineData("rotate")]
        public void TheCopyIsPassedOnlyToRotatingFromIt(string operation)
        {
            int handle = Turned();
            ComposedEditFixture.Value(Run(handle));
            V3[] before = NormalsNow();
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", operation),
                All(),
                ComposedEditFixture.Given(BasePmxHandle, (long)handle),
            };
            if (operation == "averageNear")
            {
                given.Add(ComposedEditFixture.Given("threshold", 0.1));
            }

            if (operation == "rotate")
            {
                given.Add(ComposedEditFixture.Given("rotationAxis", new object[] { 0f, 1f, 0f }));
                given.Add(ComposedEditFixture.Given("rotationAngle", 90f));
            }

            IDictionary<string, object> envelope = Call(given);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BasePmxHandle, ComposedEditFixture.Message(envelope));
            AssertUnchanged(before);
        }

        [Fact]
        public void RotatingFromTheCopyChangesNeitherTheCopyNorItsCounts()
        {
            int handle = Turned();
            V3[] positions = _held.Vertex.Select(vertex => vertex.Position).ToArray();
            IPXVertex[] corners = ((FakeMaterial)_held.Material[0]).Faces
                .SelectMany(face => new[] { face.Vertex1, face.Vertex2, face.Vertex3 })
                .ToArray();

            ComposedEditFixture.Value(Run(handle));

            Assert.Equal(positions.Length, _held.Vertex.Count);
            Assert.Single(_held.Material);
            Assert.Empty(_held.Bone);
            Assert.Equal(
                corners,
                ((FakeMaterial)_held.Material[0]).Faces
                    .SelectMany(face => new[] { face.Vertex1, face.Vertex2, face.Vertex3 })
                    .ToArray());
            for (int at = 0; at < positions.Length; at++)
            {
                Assert.Equal(positions[at].X, _held.Vertex[at].Position.X);
                Assert.Equal(positions[at].Y, _held.Vertex[at].Position.Y);
                Assert.Equal(positions[at].Z, _held.Vertex[at].Position.Z);
                Assert.Equal(BaseNormal.X, _held.Vertex[at].Normal.X);
                Assert.Equal(BaseNormal.Y, _held.Vertex[at].Normal.Y);
                Assert.Equal(BaseNormal.Z, _held.Vertex[at].Normal.Z);
            }
        }

        private static void Shapeless(string where, out V3[] baseline, out V3[] current)
        {
            V3[] moved = Triangle.Select(point => Moved(QuarterTurnAboutY, point)).ToArray();
            V3[] line = { new V3(2f, 0f, 0f), new V3(3f, 0f, 0f) };
            switch (where)
            {
                case "inBoth":
                    baseline = Triangle.Concat(line).ToArray();
                    current = moved.Concat(line.Select(point => Moved(QuarterTurnAboutY, point))).ToArray();
                    break;

                case "onlyNow":
                    baseline = Triangle.Concat(new[] { new V3(1f, 4f, 0f), new V3(1f, 0f, 5f) }).ToArray();
                    current = moved.Concat(new[] { moved[0], moved[0] }).ToArray();
                    break;

                default:
                    baseline = Triangle.Concat(line).ToArray();
                    current = moved.Concat(new[] { new V3(7f, 1f, 2f), new V3(-3f, 5f, 1f) }).ToArray();
                    break;
            }
        }

        private static V3 Turn(string turn, V3 point)
        {
            switch (turn)
            {
                case QuarterTurnAboutY:
                    return new V3(point.Z, point.Y, -point.X);

                case ThirdTurnAboutDiagonal:
                    return new V3(point.Z, point.X, point.Y);

                case HalfTurnAboutX:
                    return new V3(point.X, -point.Y, -point.Z);

                case EighthTurnAboutZ:
                    return AboutZ(point, Math.Sqrt(0.5), Math.Sqrt(0.5));

                case QuarterTurnAboutZ:
                    return AboutZ(point, 0d, 1d);

                case HalfTurnAboutZ:
                    return AboutZ(point, -1d, 0d);

                default:
                    return AboutZ(point, 0.5, Math.Sqrt(3d) / 2d);
            }
        }

        private static V3 AboutZ(V3 point, double cosine, double sine)
        {
            return new V3(
                (float)(point.X * cosine - point.Y * sine),
                (float)(point.X * sine + point.Y * cosine),
                point.Z);
        }

        private static V3 Moved(string turn, V3 point)
        {
            V3 turned = Turn(turn, point);

            return new V3(turned.X + Shift.X, turned.Y + Shift.Y, turned.Z + Shift.Z);
        }

        private static double[] FaceNormal(V3[] corners)
        {
            double[] first = { corners[1].X - corners[0].X, corners[1].Y - corners[0].Y, corners[1].Z - corners[0].Z };
            double[] second = { corners[2].X - corners[0].X, corners[2].Y - corners[0].Y, corners[2].Z - corners[0].Z };

            return new[]
            {
                first[1] * second[2] - first[2] * second[1],
                first[2] * second[0] - first[0] * second[2],
                first[0] * second[1] - first[1] * second[0],
            };
        }

        private static double Angle(V3 normal, double[] face)
        {
            double dot = normal.X * face[0] + normal.Y * face[1] + normal.Z * face[2];
            double lengths = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z)
                * Math.Sqrt(face[0] * face[0] + face[1] * face[1] + face[2] * face[2]);

            return Math.Acos(dot / lengths);
        }

        private int Turned()
        {
            return Scene(
                Triangle, Triangle.Select(point => Moved(QuarterTurnAboutY, point)).ToArray(), Normals, new[] { 0, 1, 2 });
        }

        private int Scene(V3[] baseline, V3[] current, V3[] normals, params int[][] materials)
        {
            FakePmx held = new FakePmx();
            Fill(held, baseline, baseline.Select(point => BaseNormal).ToArray(), materials);
            _held = held;
            Fill(_fixture.Model, current, normals, materials);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });
        }

        private static void Fill(FakePmx model, V3[] positions, V3[] normals, int[][] materials)
        {
            for (int at = 0; at < positions.Length; at++)
            {
                FakeVertex vertex = new FakeVertex(positions[at].X, positions[at].Y, positions[at].Z);
                vertex.Normal = new V3(normals[at].X, normals[at].Y, normals[at].Z);
                model.Vertex.Add(vertex);
            }

            foreach (int[] corners in materials)
            {
                FakeMaterial material = new FakeMaterial("材質");
                for (int at = 0; at < corners.Length; at += 3)
                {
                    material.Faces.Add(new FakeFace(
                        model.Vertex[corners[at]], model.Vertex[corners[at + 1]], model.Vertex[corners[at + 2]]));
                }

                model.Material.Add(material);
            }
        }

        private static KeyValuePair<string, object> All()
        {
            return ComposedEditFixture.Given("all", true);
        }

        private static KeyValuePair<string, object> Given(string name, object value)
        {
            return ComposedEditFixture.Given(name, value);
        }

        private KeyValuePair<string, object>[] Pointing(string how)
        {
            switch (how)
            {
                case "indices":
                    return new[] { Given("indices", new object[] { 4 }) };

                case "selected":
                    _fixture.View.Selected[ElementKinds.Vertex] = new[] { 3 };

                    return new[] { Given("selected", true) };

                case "materialIndices":
                    return new[] { Given("materialIndices", new object[] { 1 }) };

                default:
                    return new[] { All() };
            }
        }

        private static List<KeyValuePair<string, object>> Full(int handle, params KeyValuePair<string, object>[] pointing)
        {
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                ComposedEditFixture.Given("operation", RotateFromBase),
                ComposedEditFixture.Given(BasePmxHandle, (long)handle),
            };
            given.AddRange(pointing);

            return given;
        }

        private static List<KeyValuePair<string, object>> Without(
            List<KeyValuePair<string, object>> given, string name)
        {
            return given.Where(pair => pair.Key != name).ToList();
        }

        private IDictionary<string, object> Run(int handle, params KeyValuePair<string, object>[] pointing)
        {
            return Call(Full(handle, pointing.Length == 0 ? new[] { All() } : pointing));
        }

        private IDictionary<string, object> Call(List<KeyValuePair<string, object>> given)
        {
            return _fixture.Call(ToolName, ComposedEditFixture.Arguments(given.ToArray()));
        }

        private V3[] NormalsNow()
        {
            return _fixture.Model.Vertex.Select(vertex => vertex.Normal).ToArray();
        }

        private void AssertNormal(int index, V3 wanted, double tolerance)
        {
            V3 found = _fixture.Model.Vertex[index].Normal;
            Assert.Equal((double)wanted.X, (double)found.X, tolerance);
            Assert.Equal((double)wanted.Y, (double)found.Y, tolerance);
            Assert.Equal((double)wanted.Z, (double)found.Z, tolerance);
        }

        private void AssertSame(int index, V3 wanted)
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
                AssertSame(at, before[at]);
            }
        }

        private static void AssertDirection(float x, float y, float z, double[] found)
        {
            double length = Math.Sqrt(found[0] * found[0] + found[1] * found[1] + found[2] * found[2]);
            Assert.Equal((double)x, found[0] / length, Tight);
            Assert.Equal((double)y, found[1] / length, Tight);
            Assert.Equal((double)z, found[2] / length, Tight);
        }
    }
}
