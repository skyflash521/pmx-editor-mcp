using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelMorphFromMovedBoneMorphTests : IDisposable
    {
        private const string BasePmxHandle = "basePmxHandle";

        private const string Name = "name";

        private const string BoneMorphName = "boneMorphName";

        private const string ModelMorphFromMovedName = "model_morph_from_moved";

        private const string Added = "added";

        private const string Offsets = "offsets";

        private const string BoneAdded = "boneAdded";

        private const string BoneOffsets = "boneOffsets";

        private const string VertexMorphCalled = "足元_頂点";

        private const string BoneMorphCalled = "足元_ボーン";

        private const int Digits = 4;

        private static readonly V3[] BonesInCopy =
        {
            new V3(0f, 1f, 0f), new V3(0f, 2f, 0f), new V3(0f, 3f, 0f), new V3(1f, 3f, 0f),
            new V3(1f, 4f, 0f), new V3(2f, 3f, 0f),
        };

        private static readonly V3[] BonesNow =
        {
            new V3(0.5f, 1.75f, 0f), new V3(0f, 2f, 1f), new V3(0f, 3f, 0f), new V3(1.5f, 3.25f, -0.5f),
            new V3(1f, 4f, 0f), new V3(2.5f, 3.25f, -0.5f),
        };

        private static readonly int[] Parents = { -1, 0, -1, -1, 3, 3 };

        private static readonly Spec[] Vertices =
        {
            new Spec(new V3(0f, 1f, 1f), new V3(0.5f, 1.75f, 1f), new[] { 0 }, new[] { 1f }),
            new Spec(new V3(0f, 1.5f, 0f), new V3(0.125f, 1.5f, 0.5f), new[] { 0, 1 }, new[] { 0.25f, 0.75f }),
            new Spec(
                new V3(1f, 2f, 0f),
                new V3(1.5f, 2.25f, 0.25f),
                new[] { 0, 1, 3, 2 },
                new[] { 0.5f, 0.25f, 0.125f, 0.125f }),
            new Spec(new V3(0.5f, 2f, 0.5f), new V3(0.5f, 2.5f, 1f), new[] { 1, 3 }, new[] { 0.75f, 0.25f }, true),
            new Spec(new V3(2f, 0f, 0f), new V3(2.5f, 0f, 0.5f), new[] { 0 }, new[] { 0f }),
            new Spec(new V3(2f, 1f, 0f), new V3(1.5f, 1f, 0.25f), new[] { 2 }, new[] { 1f }),
            new Spec(new V3(3f, 3f, 3f), new V3(3f, 3f, 3f), new[] { 2 }, new[] { 1f }),
            new Spec(new V3(1f, 3.5f, 0f), new V3(1f, 3.5f, 0f), new[] { 3 }, new[] { 1f }),
            new Spec(new V3(1f, 4.5f, 0f), new V3(1.25f, 4.5f, 0.25f), new[] { 4 }, new[] { 1f }),
            new Spec(new V3(0.5f, 2f, 0f), new V3(0.5f, 2f, 0.5f), new[] { 1 }, new[] { 1f }),
            new Spec(new V3(2f, 3.5f, 0f), new V3(2.5f, 3.75f, 0f), new[] { 5 }, new[] { 1f }),
        };

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheTwoMorphsAtOneBringEveryVertexBackToItsPlaceInTheCopy()
        {
            int handle = Scene(BonesNow, Vertices);

            Run(handle, VertexMorphCalled, BoneMorphCalled);

            V3[] reproduced = Reproduced();
            Assert.Equal(Vertices.Length, reproduced.Length);
            for (int at = 0; at < Vertices.Length; at++)
            {
                Assert.Equal(Vertices[at].InCopy.X, reproduced[at].X, Digits);
                Assert.Equal(Vertices[at].InCopy.Y, reproduced[at].Y, Digits);
                Assert.Equal(Vertices[at].InCopy.Z, reproduced[at].Z, Digits);
            }
        }

        [Fact]
        public void TheBoneMorphMovesEachBoneByItsOwnMoveLessItsParentsAndTurnsNone()
        {
            int handle = Scene(BonesNow, Vertices);

            Run(handle, VertexMorphCalled, BoneMorphCalled);

            IPXMorph morph = MorphCalled(BoneMorphCalled);
            Assert.Equal(MorphKind.Bone, morph.Kind);
            Assert.Equal(MorphCalled(VertexMorphCalled).Panel, morph.Panel);
            IPXBoneMorphOffset[] offsets = morph.Offsets.Cast<IPXBoneMorphOffset>().ToArray();
            Assert.Equal(4, offsets.Length);
            AssertMove(offsets, 0, -0.5f, -0.75f, 0f);
            AssertMove(offsets, 1, 0.5f, 0.75f, -1f);
            AssertMove(offsets, 3, -0.5f, -0.25f, 0.5f);
            AssertMove(offsets, 4, 0.5f, 0.25f, -0.5f);
            foreach (IPXBoneMorphOffset offset in offsets)
            {
                Assert.Equal(0f, offset.Rotation.X, Digits);
                Assert.Equal(0f, offset.Rotation.Y, Digits);
                Assert.Equal(0f, offset.Rotation.Z, Digits);
                Assert.Equal(1f, offset.Rotation.W, Digits);
            }
        }

        [Theory]
        [InlineData(1, 0f, 0.1875f, 0.25f)]
        [InlineData(2, -0.1875f, 0.15625f, -0.0625f)]
        [InlineData(3, 0.125f, -0.4375f, 0.125f)]
        [InlineData(4, 0f, 0.75f, -0.5f)]
        [InlineData(5, 0.5f, 0f, -0.25f)]
        [InlineData(7, 0.5f, 0.25f, -0.5f)]
        [InlineData(8, -0.25f, 0f, -0.25f)]
        [InlineData(9, 0f, 0f, 0.5f)]
        [InlineData(10, 0f, 0f, -0.5f)]
        public void AVertexOffsetIsTheDistanceToTheCopyLessWhatTheBoneMorphMovesTheVertex(
            int vertex, float x, float y, float z)
        {
            int handle = Scene(BonesNow, Vertices);

            Run(handle, VertexMorphCalled, BoneMorphCalled);

            IPXVertexMorphOffset offset = Assert.Single(OffsetsOf(vertex));
            Assert.Equal(x, offset.Offset.X, Digits);
            Assert.Equal(y, offset.Offset.Y, Digits);
            Assert.Equal(z, offset.Offset.Z, Digits);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        public void AVertexThatTheBoneMorphAloneBringsToTheCopyGetsNoOffset(int vertex)
        {
            int handle = Scene(BonesNow, Vertices);

            Run(handle, VertexMorphCalled, BoneMorphCalled);

            Assert.Empty(OffsetsOf(vertex));
            Assert.Equal(
                Vertices.Length - 2,
                MorphCalled(VertexMorphCalled).Offsets.Count);
        }

        [Fact]
        public void TheCallNamesTheTwoMorphsItAddedAndHowManyOffsetsEachHolds()
        {
            int handle = Scene(BonesNow, Vertices);
            FakePmx copy = (FakePmx)Held(handle);
            copy.Morph.Add(new FakeMorph("既存", MorphKind.Vertex));
            _fixture.Model.Morph.Add(new FakeMorph("既存", MorphKind.Vertex));

            IDictionary<string, object> value = Run(handle, VertexMorphCalled, BoneMorphCalled);

            Assert.Equal(1, value[Added]);
            Assert.Equal(9, value[Offsets]);
            Assert.Equal(2, value[BoneAdded]);
            Assert.Equal(4, value[BoneOffsets]);
            Assert.Equal(VertexMorphCalled, _fixture.Model.Morph[1].Name);
            Assert.Equal(BoneMorphCalled, _fixture.Model.Morph[2].Name);
            Assert.Equal(3, _fixture.Model.Morph.Count);
        }

        [Fact]
        public void TheBonesAndTheVerticesOfTheModelStayWhereTheyAre()
        {
            int handle = Scene(BonesNow, Vertices);

            Run(handle, VertexMorphCalled, BoneMorphCalled);

            for (int at = 0; at < BonesNow.Length; at++)
            {
                AssertAt(BonesNow[at], _fixture.Model.Bone[at].Position);
            }

            for (int at = 0; at < Vertices.Length; at++)
            {
                AssertAt(Vertices[at].Now, _fixture.Model.Vertex[at].Position);
            }
        }

        [Fact]
        public void WhenNoBoneMovedNoBoneMorphIsMadeAndTheVertexMorphCarriesTheWholeDistance()
        {
            Spec[] vertices =
            {
                new Spec(new V3(0f, 1f, 1f), new V3(0.5f, 1f, 1f), new[] { 0, 1 }, new[] { 0.25f, 0.75f }),
                new Spec(new V3(3f, 3f, 3f), new V3(3f, 3f, 3f), new[] { 1 }, new[] { 1f }),
            };
            int handle = Scene(BonesInCopy, vertices);

            IDictionary<string, object> value = Run(handle, VertexMorphCalled, BoneMorphCalled);

            IPXMorph made = Assert.Single(_fixture.Model.Morph);
            Assert.Equal(VertexMorphCalled, made.Name);
            IPXVertexMorphOffset offset = (IPXVertexMorphOffset)Assert.Single(made.Offsets);
            Assert.Same(_fixture.Model.Vertex[0], offset.Vertex);
            AssertAt(new V3(-0.5f, 0f, 0f), offset.Offset);
            Assert.Equal(0, value[Added]);
            Assert.Equal(1, value[Offsets]);
            Assert.False(value.ContainsKey(BoneAdded));
            Assert.False(value.ContainsKey(BoneOffsets));
        }

        [Fact]
        public void ABoneMorphNameThatIsTheNameOfTheVertexMorphIsRefused()
        {
            int handle = Scene(BonesNow, Vertices);
            Run(handle, VertexMorphCalled, BoneMorphCalled);
            _fixture.Model.Morph.Clear();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelMorphFromMovedName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(BasePmxHandle, (long)handle),
                    ComposedEditFixture.Given(Name, VertexMorphCalled),
                    ComposedEditFixture.Given(BoneMorphName, VertexMorphCalled)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BoneMorphName, ComposedEditFixture.Message(envelope));
            Assert.Empty(_fixture.Model.Morph);
        }

        [Theory]
        [MemberData(nameof(BadNames))]
        public void ABoneMorphNameThatIsEmptyOrNotTextIsRefused(object given)
        {
            int handle = Scene(BonesNow, Vertices);
            Run(handle, VertexMorphCalled, BoneMorphCalled);
            _fixture.Model.Morph.Clear();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelMorphFromMovedName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(BasePmxHandle, (long)handle),
                    ComposedEditFixture.Given(Name, VertexMorphCalled),
                    ComposedEditFixture.Given(BoneMorphName, given)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(BoneMorphName, ComposedEditFixture.Message(envelope));
            Assert.Empty(_fixture.Model.Morph);
        }

        public static IEnumerable<object[]> BadNames()
        {
            yield return new object[] { string.Empty };
            yield return new object[] { 5L };
            yield return new object[] { true };
            yield return new object[] { new object[] { BoneMorphCalled } };
        }

        private IDictionary<string, object> Run(int handle, string vertexMorph, string boneMorph)
        {
            return ComposedEditFixture.Value(_fixture.Call(
                ModelMorphFromMovedName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(BasePmxHandle, (long)handle),
                    ComposedEditFixture.Given(Name, vertexMorph),
                    ComposedEditFixture.Given(BoneMorphName, boneMorph))));
        }

        private int Scene(V3[] bonesNow, Spec[] vertices)
        {
            FakePmx copy = new FakePmx();
            Fill(copy, BonesInCopy, vertices.Select(spec => spec.InCopy).ToArray(), vertices);
            Fill(_fixture.Model, bonesNow, vertices.Select(spec => spec.Now).ToArray(), vertices);

            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });
        }

        private static void Fill(FakePmx model, V3[] bones, V3[] positions, Spec[] vertices)
        {
            for (int at = 0; at < bones.Length; at++)
            {
                model.Bone.Add(new FakeBone("ボーン" + at) { Position = bones[at] });
            }

            for (int at = 0; at < bones.Length; at++)
            {
                if (Parents[at] >= 0)
                {
                    model.Bone[at].Parent = model.Bone[Parents[at]];
                }
            }

            for (int at = 0; at < vertices.Length; at++)
            {
                FakeVertex vertex = new FakeVertex(positions[at].X, positions[at].Y, positions[at].Z)
                {
                    SDEF = vertices[at].Sdef,
                    SDEF_R0 = new V3(1f, 0f, 0f),
                    SDEF_R1 = new V3(-1f, 0f, 0f),
                };
                IPXBone[] held = new IPXBone[4];
                float[] weights = new float[4];
                for (int slot = 0; slot < vertices[at].Bones.Length; slot++)
                {
                    held[slot] = model.Bone[vertices[at].Bones[slot]];
                    weights[slot] = vertices[at].Weights[slot];
                }

                vertex.Bone1 = held[0];
                vertex.Bone2 = held[1];
                vertex.Bone3 = held[2];
                vertex.Bone4 = held[3];
                vertex.Weight1 = weights[0];
                vertex.Weight2 = weights[1];
                vertex.Weight3 = weights[2];
                vertex.Weight4 = weights[3];
                model.Vertex.Add(vertex);
            }
        }

        private object Held(int handle)
        {
            object held;
            Assert.True(_fixture.Handles.TryGet(handle, out held));

            return held;
        }

        private IPXMorph MorphCalled(string name)
        {
            return Assert.Single(_fixture.Model.Morph, morph => morph.Name == name);
        }

        private IList<IPXVertexMorphOffset> OffsetsOf(int vertex)
        {
            return MorphCalled(VertexMorphCalled).Offsets
                .Cast<IPXVertexMorphOffset>()
                .Where(offset => ReferenceEquals(offset.Vertex, _fixture.Model.Vertex[vertex]))
                .ToList();
        }

        private void AssertMove(IPXBoneMorphOffset[] offsets, int bone, float x, float y, float z)
        {
            IPXBoneMorphOffset found = Assert.Single(
                offsets, offset => ReferenceEquals(offset.Bone, _fixture.Model.Bone[bone]));
            AssertAt(new V3(x, y, z), found.Translation);
        }

        private static void AssertAt(V3 expected, V3 found)
        {
            Assert.Equal(expected.X, found.X, Digits);
            Assert.Equal(expected.Y, found.Y, Digits);
            Assert.Equal(expected.Z, found.Z, Digits);
        }

        private V3[] Reproduced()
        {
            Dictionary<int, V3> moves = new Dictionary<int, V3>();
            foreach (IPXMorph morph in _fixture.Model.Morph.Where(m => m.Kind == MorphKind.Bone))
            {
                foreach (IPXBoneMorphOffset offset in morph.Offsets.Cast<IPXBoneMorphOffset>())
                {
                    int at = _fixture.Model.Bone.IndexOf(offset.Bone);
                    Assert.True(at >= 0);
                    V3 held;
                    if (!moves.TryGetValue(at, out held))
                    {
                        held = new V3(0f, 0f, 0f);
                    }

                    moves[at] = new V3(
                        held.X + offset.Translation.X,
                        held.Y + offset.Translation.Y,
                        held.Z + offset.Translation.Z);
                }
            }

            Dictionary<int, V3> totals = new Dictionary<int, V3>();
            for (int at = 0; at < _fixture.Model.Bone.Count; at++)
            {
                float x = 0f;
                float y = 0f;
                float z = 0f;
                for (IPXBone walked = _fixture.Model.Bone[at]; walked != null; walked = walked.Parent)
                {
                    V3 own;
                    if (moves.TryGetValue(_fixture.Model.Bone.IndexOf(walked), out own))
                    {
                        x += own.X;
                        y += own.Y;
                        z += own.Z;
                    }
                }

                totals[at] = new V3(x, y, z);
            }

            V3[] placed = new V3[_fixture.Model.Vertex.Count];
            for (int at = 0; at < placed.Length; at++)
            {
                IPXVertex vertex = _fixture.Model.Vertex[at];
                float x = vertex.Position.X;
                float y = vertex.Position.Y;
                float z = vertex.Position.Z;
                foreach (IPXMorph morph in _fixture.Model.Morph.Where(m => m.Kind == MorphKind.Vertex))
                {
                    foreach (IPXVertexMorphOffset offset in morph.Offsets.Cast<IPXVertexMorphOffset>())
                    {
                        if (ReferenceEquals(offset.Vertex, vertex))
                        {
                            x += offset.Offset.X;
                            y += offset.Offset.Y;
                            z += offset.Offset.Z;
                        }
                    }
                }

                IPXBone[] bones = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
                float[] weights = { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 };
                if (weights.All(weight => weight == 0f))
                {
                    weights[0] = 1f;
                }

                for (int slot = 0; slot < bones.Length; slot++)
                {
                    V3 move;
                    if (weights[slot] != 0f && totals.TryGetValue(_fixture.Model.Bone.IndexOf(bones[slot]), out move))
                    {
                        x += weights[slot] * move.X;
                        y += weights[slot] * move.Y;
                        z += weights[slot] * move.Z;
                    }
                }

                placed[at] = new V3(x, y, z);
            }

            return placed;
        }

        private sealed class Spec
        {
            public Spec(V3 inCopy, V3 now, int[] bones, float[] weights, bool sdef = false)
            {
                InCopy = inCopy;
                Now = now;
                Bones = bones;
                Weights = weights;
                Sdef = sdef;
            }

            public V3 InCopy { get; }

            public V3 Now { get; }

            public int[] Bones { get; }

            public float[] Weights { get; }

            public bool Sdef { get; }
        }
    }
}
