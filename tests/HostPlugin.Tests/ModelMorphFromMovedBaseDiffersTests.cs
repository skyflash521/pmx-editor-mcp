using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelMorphFromMovedBaseDiffersTests : IDisposable
    {
        private const string BasePmxHandle = "basePmxHandle";

        private const string Name = "name";

        private const string BoneMorphName = "boneMorphName";

        private const string ModelMorphFromMovedName = "model_morph_from_moved";

        private const string VertexMorphCalled = "素足_頂点";

        private const string BoneMorphCalled = "素足_ボーン";

        private const int Digits = 4;

        private static readonly V3[] BonesInCopy = { new V3(0f, 0f, 0f), new V3(0f, 1f, 0f) };

        private static readonly V3[] BonesNow = { new V3(0f, 0f, 0f), new V3(0.5f, 1f, 0f) };

        private static readonly V3[] VerticesInCopy =
        {
            new V3(0f, 0f, 0f), new V3(0f, 1f, 1f), new V3(1f, 0.5f, 0f), new V3(2f, 2f, 2f),
        };

        private static readonly V3[] VerticesNow =
        {
            new V3(0f, 0f, 0.5f), new V3(0.5f, 1f, 1f), new V3(1.25f, 0.5f, 0.25f), new V3(2f, 2f, 2f),
        };

        private static readonly int[] WeightedTo = { 0, 1, 1, 0 };

        private static readonly float[] Weights = { 1f, 1f, 0.25f, 1f };

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData("normal")]
        [InlineData("uv")]
        [InlineData("edgeScale")]
        [InlineData("weight")]
        [InlineData("boneRef")]
        public void AnAttributeOtherThanThePositionThatDiffersFromTheCopyIsNotRefusedAndTheTwoMorphsBringTheCopyShapeBack(
            string attribute)
        {
            FakePmx copy = Scene();
            Differ(copy.Vertex[2], attribute);

            IDictionary<string, object> envelope = Call(Issue(copy));

            Assert.NotNull(ComposedEditFixture.Value(envelope));
            AssertBackInTheCopy();
        }

        [Fact]
        public void AMorphOfTheModelThatTheCopyDoesNotHaveDoesNotRefuseTheCall()
        {
            FakePmx copy = Scene();
            _fixture.Model.Morph.Add(new FakeMorph("既存", MorphKind.Vertex));

            IDictionary<string, object> envelope = Call(Issue(copy));

            Assert.NotNull(ComposedEditFixture.Value(envelope));
            Assert.Equal(3, _fixture.Model.Morph.Count);
            Assert.Equal(VertexMorphCalled, _fixture.Model.Morph[1].Name);
            Assert.Equal(BoneMorphCalled, _fixture.Model.Morph[2].Name);
            AssertBackInTheCopy();
        }

        [Fact]
        public void ABodyOfTheModelThatTheCopyDoesNotHaveDoesNotRefuseTheCall()
        {
            FakePmx copy = Scene();
            _fixture.Model.Body.Add(new FakeBody("既存"));

            IDictionary<string, object> envelope = Call(Issue(copy));

            Assert.NotNull(ComposedEditFixture.Value(envelope));
            Assert.Equal(2, _fixture.Model.Morph.Count);
            AssertBackInTheCopy();
        }

        [Theory]
        [InlineData("vertex")]
        [InlineData("material")]
        [InlineData("bone")]
        [InlineData("face")]
        public void ADifferentCountOfVerticesMaterialsBonesOrFacesOfAMaterialIsRefused(string counted)
        {
            FakePmx copy = Scene();
            Assert.NotNull(ComposedEditFixture.Value(Call(Issue(copy))));
            _fixture.Model.Morph.Clear();
            switch (counted)
            {
                case "vertex":
                    copy.Vertex.Add(new FakeVertex());
                    break;
                case "material":
                    copy.Material.Add(new FakeMaterial("余り"));
                    break;
                case "bone":
                    copy.Bone.Add(new FakeBone("余り"));
                    break;
                default:
                    copy.Material[1].Faces.RemoveAt(0);
                    break;
            }

            IDictionary<string, object> envelope = Call(Issue(copy));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Empty(_fixture.Model.Morph);
        }

        [Fact]
        public void TheVerticesAndTheBonesOfTheModelStayWhereTheyAre()
        {
            FakePmx copy = Scene();
            Differ(copy.Vertex[1], "normal");

            Assert.NotNull(ComposedEditFixture.Value(Call(Issue(copy))));

            for (int at = 0; at < BonesNow.Length; at++)
            {
                AssertAt(BonesNow[at], _fixture.Model.Bone[at].Position);
            }

            for (int at = 0; at < VerticesNow.Length; at++)
            {
                AssertAt(VerticesNow[at], _fixture.Model.Vertex[at].Position);
            }
        }

        private IDictionary<string, object> Call(int handle)
        {
            return _fixture.Call(
                ModelMorphFromMovedName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given(BasePmxHandle, (long)handle),
                    ComposedEditFixture.Given(Name, VertexMorphCalled),
                    ComposedEditFixture.Given(BoneMorphName, BoneMorphCalled)));
        }

        private int Issue(FakePmx copy)
        {
            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, copy, () => { });
        }

        private FakePmx Scene()
        {
            FakePmx copy = new FakePmx();
            Fill(copy, BonesInCopy, VerticesInCopy);
            Fill(_fixture.Model, BonesNow, VerticesNow);

            return copy;
        }

        private static void Fill(FakePmx model, V3[] bones, V3[] positions)
        {
            for (int at = 0; at < bones.Length; at++)
            {
                model.Bone.Add(new FakeBone("ボーン" + at) { Position = bones[at] });
            }

            model.Bone[1].Parent = model.Bone[0];
            for (int at = 0; at < positions.Length; at++)
            {
                FakeVertex vertex = new FakeVertex(positions[at].X, positions[at].Y, positions[at].Z)
                {
                    Bone1 = model.Bone[WeightedTo[at]],
                    Weight1 = Weights[at],
                };
                if (Weights[at] < 1f)
                {
                    vertex.Bone2 = model.Bone[0];
                    vertex.Weight2 = 1f - Weights[at];
                }

                model.Vertex.Add(vertex);
            }

            model.Material.Add(new FakeMaterial("材質0"));
            model.Material.Add(new FakeMaterial("材質1"));
            model.Material[0].Faces.Add(new FakeFace(model.Vertex[0], model.Vertex[1], model.Vertex[2]));
            model.Material[0].Faces.Add(new FakeFace(model.Vertex[0], model.Vertex[2], model.Vertex[3]));
            model.Material[1].Faces.Add(new FakeFace(model.Vertex[1], model.Vertex[2], model.Vertex[3]));
            model.Material[1].Faces.Add(new FakeFace(model.Vertex[0], model.Vertex[1], model.Vertex[3]));
        }

        private static void Differ(IPXVertex vertex, string attribute)
        {
            switch (attribute)
            {
                case "normal":
                    vertex.Normal = new V3(1f, 0f, 0f);
                    break;
                case "uv":
                    vertex.UV = new V2(0.5f, 0.25f);
                    break;
                case "edgeScale":
                    vertex.EdgeScale = 2f;
                    break;
                case "weight":
                    vertex.Weight1 = 0.75f;
                    vertex.Weight2 = 0.25f;
                    break;
                default:
                    vertex.Bone2 = vertex.Bone1;
                    break;
            }
        }

        private void AssertBackInTheCopy()
        {
            Dictionary<int, V3> moves = new Dictionary<int, V3>();
            foreach (IPXMorph morph in _fixture.Model.Morph.Where(m => m.Kind == MorphKind.Bone))
            {
                foreach (IPXBoneMorphOffset offset in morph.Offsets.Cast<IPXBoneMorphOffset>())
                {
                    moves[_fixture.Model.Bone.IndexOf(offset.Bone)] = offset.Translation;
                }
            }

            V3[] totals = new V3[_fixture.Model.Bone.Count];
            for (int at = 0; at < totals.Length; at++)
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

            for (int at = 0; at < VerticesInCopy.Length; at++)
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

                IPXBone[] bones = { vertex.Bone1, vertex.Bone2 };
                float[] weights = { vertex.Weight1, vertex.Weight2 };
                for (int slot = 0; slot < bones.Length; slot++)
                {
                    if (bones[slot] != null)
                    {
                        V3 move = totals[_fixture.Model.Bone.IndexOf(bones[slot])];
                        x += weights[slot] * move.X;
                        y += weights[slot] * move.Y;
                        z += weights[slot] * move.Z;
                    }
                }

                AssertAt(VerticesInCopy[at], new V3(x, y, z));
            }
        }

        private static void AssertAt(V3 expected, V3 found)
        {
            Assert.Equal(expected.X, found.X, Digits);
            Assert.Equal(expected.Y, found.Y, Digits);
            Assert.Equal(expected.Z, found.Z, Digits);
        }
    }
}
