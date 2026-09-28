using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelVertexMaterialTargetsTests : IDisposable
    {
        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private readonly List<FakeVertex> _vertices = new List<FakeVertex>();

        public ModelVertexMaterialTargetsTests()
        {
            IPXBone first = new FakeBone("上");
            IPXBone second = new FakeBone("下");
            _fixture.Model.Bone.Add(first);
            _fixture.Model.Bone.Add(second);
            for (int at = 0; at < 6; at++)
            {
                FakeVertex made = new FakeVertex(1f + at, 0.5f * at, 0.25f)
                {
                    Normal = new V3(0f, 1f, 0f),
                    UV = new V2(0.2f, 0.3f),
                    Bone1 = first,
                    Weight1 = 1f,
                };
                _fixture.Model.Vertex.Add(made);
                _vertices.Add(made);
            }

            _vertices[1].Weight1 = 0.7f;
            _vertices[1].Bone2 = second;
            _vertices[1].Weight2 = 0.3f;
            _vertices[3].Bone1 = second;
            _vertices[5].Weight1 = 0.5f;
            _vertices[5].Bone2 = second;
            _vertices[5].Weight2 = 0.5f;

            FakeMaterial even = new FakeMaterial("偶");
            even.Faces.Add(new FakeFace(_vertices[0], _vertices[2], _vertices[4]));
            FakeMaterial odd = new FakeMaterial("奇");
            odd.Faces.Add(new FakeFace(_vertices[1], _vertices[3], _vertices[5]));
            _fixture.Model.Material.Add(even);
            _fixture.Model.Material.Add(odd);
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        public static IEnumerable<object[]> Edits()
        {
            yield return new object[] { ModelEditVertices.ToolName, Pairs("operation", "align", "axis", "x") };
            yield return new object[] { ModelEditNormals.ToolName, Pairs("operation", "flip") };
            yield return new object[] { ModelEditUv.ToolName, Pairs("operation", "flipU") };
            yield return new object[] { ModelEditWeights.ToolName, Pairs("operation", "average") };
            yield return new object[]
            {
                ModelSetDeformType.ToolName, Pairs("operation", "convert", "deform", "bdef1"),
            };
        }

        [Theory]
        [MemberData(nameof(Edits))]
        public void VerticesPointedAtByMaterialAreTheOnlyOnesEdited(string tool, object[] pairs)
        {
            List<string> before = _vertices.Select(Held).ToList();
            List<KeyValuePair<string, object>> given = Given(pairs);
            given.Add(ComposedEditFixture.Given("materialIndices", new object[] { 1 }));

            ComposedEditFixture.Value(_fixture.Call(tool, ComposedEditFixture.Arguments(given.ToArray())));

            AssertOnlyOddChanged(before);
        }

        [Fact]
        public void PlacingMovesTheVerticesPointedAtByMaterial()
        {
            List<string> before = _vertices.Select(Held).ToList();

            IDictionary<string, object> value = ComposedEditFixture.Value(_fixture.Call(
                ModelPlaceElements.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", ModelPlaceElements.TranslateBy),
                    ComposedEditFixture.Given("offset", new object[] { 0.0, 1.0, 0.0 }),
                    ComposedEditFixture.Given("targets", new object[] { VertexTarget() }))));

            Assert.Equal(3, value[ModelPlaceElements.ChangedName]);
            AssertOnlyOddChanged(before);
        }

        [Fact]
        public void MirroringCopiesTheVerticesPointedAtByMaterial()
        {
            ComposedEditFixture.Value(_fixture.Call(
                ModelMirrorElements.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", "copyTargets"),
                    ComposedEditFixture.Given("targets", new object[] { VertexTarget() }))));

            Assert.Equal(9, _fixture.Model.Vertex.Count);
            Assert.Equal(
                new[] { -2f, -4f, -6f },
                _fixture.Model.Vertex.Skip(6).Select(v => v.Position.X).ToArray());
        }

        [Fact]
        public void MaterialsCannotPointAtOtherKinds()
        {
            IDictionary<string, object> bone = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "kind", ElementKinds.Bone },
                { "materialIndices", new object[] { 1 } },
            };

            IDictionary<string, object> envelope = _fixture.Call(
                ModelPlaceElements.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", ModelPlaceElements.TranslateBy),
                    ComposedEditFixture.Given("offset", new object[] { 0.0, 1.0, 0.0 }),
                    ComposedEditFixture.Given("targets", new object[] { bone })));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void MaterialsAndAnotherWayOfPointingAreRefusedTogether()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditNormals.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", "flip"),
                    ComposedEditFixture.Given("all", true),
                    ComposedEditFixture.Given("materialIndices", new object[] { 1 })));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AMaterialOutsideTheModelIsRefused()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditNormals.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", "flip"),
                    ComposedEditFixture.Given("materialIndices", new object[] { 5 })));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        private static IDictionary<string, object> VertexTarget()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "kind", ElementKinds.Vertex },
                { "materialIndices", new object[] { 1 } },
            };
        }

        private void AssertOnlyOddChanged(List<string> before)
        {
            for (int at = 0; at < _vertices.Count; at++)
            {
                if (at % 2 == 0)
                {
                    Assert.Equal(before[at], Held(_vertices[at]));
                }
            }

            Assert.Contains(
                Enumerable.Range(0, _vertices.Count),
                at => at % 2 == 1 && before[at] != Held(_vertices[at]));
        }

        private static string Held(FakeVertex vertex)
        {
            return string.Join(
                "|",
                new object[]
                {
                    vertex.Position.X, vertex.Position.Y, vertex.Position.Z,
                    vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z,
                    vertex.UV.X, vertex.UV.Y,
                    vertex.Bone1 == null ? string.Empty : vertex.Bone1.Name,
                    vertex.Bone2 == null ? string.Empty : vertex.Bone2.Name,
                    vertex.Weight1, vertex.Weight2,
                }.Select(part => Convert.ToString(part, CultureInfo.InvariantCulture)));
        }

        private static object[] Pairs(params object[] pairs)
        {
            return pairs;
        }

        private static List<KeyValuePair<string, object>> Given(object[] pairs)
        {
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>();
            for (int at = 0; at < pairs.Length; at += 2)
            {
                given.Add(ComposedEditFixture.Given((string)pairs[at], pairs[at + 1]));
            }

            return given;
        }
    }
}
