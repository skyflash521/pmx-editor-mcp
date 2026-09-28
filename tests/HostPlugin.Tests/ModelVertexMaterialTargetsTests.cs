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

            NowAll(_vertices)[1].Weight1 = 0.7f;
            NowAll(_vertices)[1].Bone2 = second;
            NowAll(_vertices)[1].Weight2 = 0.3f;
            NowAll(_vertices)[3].Bone1 = second;
            NowAll(_vertices)[5].Weight1 = 0.5f;
            NowAll(_vertices)[5].Bone2 = second;
            NowAll(_vertices)[5].Weight2 = 0.5f;

            FakeMaterial even = new FakeMaterial("偶");
            even.Faces.Add(new FakeFace(NowAll(_vertices)[0], NowAll(_vertices)[2], NowAll(_vertices)[4]));
            FakeMaterial odd = new FakeMaterial("奇");
            odd.Faces.Add(new FakeFace(NowAll(_vertices)[1], NowAll(_vertices)[3], NowAll(_vertices)[5]));
            _fixture.Model.Material.Add(even);
            _fixture.Model.Material.Add(odd);
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        public static IEnumerable<object[]> Edits()
        {
            yield return new object[] { ModelEditVertices.ToolName, Pairs("operation", "align", "axis", "x"), "align" };
            yield return new object[] { ModelEditNormals.ToolName, Pairs("operation", "flip"), "flip" };
            yield return new object[] { ModelEditUv.ToolName, Pairs("operation", "flipU"), "flipU" };
            yield return new object[] { ModelEditWeights.ToolName, Pairs("operation", "average"), "average" };
            yield return new object[]
            {
                ModelSetDeformType.ToolName, Pairs("operation", "convert", "deform", "bdef1"), "bdef1",
            };
        }

        [Theory]
        [MemberData(nameof(Edits))]
        public void VerticesPointedAtByMaterialAreTheOnlyOnesEdited(
            string tool, object[] pairs, string edit)
        {
            List<string> before = _vertices.Select(Held).ToList();
            List<KeyValuePair<string, object>> given = Given(pairs);
            given.Add(ComposedEditFixture.Given("materialIndices", new object[] { 1 }));

            ComposedEditFixture.Value(_fixture.Call(tool, ComposedEditFixture.Arguments(given.ToArray())));

            AssertOnlyOddChanged(before);
            Assert.Equal(1, _fixture.Commits);
            IPXVertex[] odd = { NowAll(_vertices)[1], NowAll(_vertices)[3], NowAll(_vertices)[5] };
            switch (edit)
            {
                case "align":
                    Assert.All(odd, v => Assert.Equal(4f, v.Position.X));
                    break;

                case "flip":
                    Assert.All(odd, v => Assert.Equal(-1f, v.Normal.Y));
                    break;

                case "flipU":
                    Assert.All(odd, v => Assert.Equal(0.8f, v.UV.X, 5));
                    break;

                case "average":
                    Assert.Single(odd.Select(Weights).Distinct());
                    break;

                default:
                    Assert.All(odd, v => Assert.Equal(1f, v.Weight1));
                    Assert.All(odd, v => Assert.Equal(0f, v.Weight2));
                    Assert.Equal("上", NowAll(_vertices)[1].Bone1.Name);
                    Assert.Equal("下", NowAll(_vertices)[3].Bone1.Name);
                    break;
            }
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
            Assert.Equal(1, _fixture.Commits);
            Assert.Equal(1.5f, NowAll(_vertices)[1].Position.Y);
            Assert.Equal(2.5f, NowAll(_vertices)[3].Position.Y);
            Assert.Equal(3.5f, NowAll(_vertices)[5].Position.Y);
        }

        /// <summary>頂点のボーンと重みの組を、比べられる文字列にする。</summary>
        private static string Weights(IPXVertex vertex)
        {
            return string.Join(
                "|",
                new object[]
                {
                    vertex.Bone1 == null ? string.Empty : vertex.Bone1.Name,
                    vertex.Bone2 == null ? string.Empty : vertex.Bone2.Name,
                    vertex.Weight1,
                    vertex.Weight2,
                }.Select(part => Convert.ToString(part, CultureInfo.InvariantCulture)));
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
                    Assert.Equal(before[at], Held(NowAll(_vertices)[at]));
                }
            }

            Assert.Contains(
                Enumerable.Range(0, _vertices.Count),
                at => at % 2 == 1 && before[at] != Held(NowAll(_vertices)[at]));
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

        /// <summary>握った要素の並びを、それぞれいまのモデルで同じ位置に並んでいる要素へ読み直す。</summary>
        private IList<T> NowAll<T>(IList<T> held)
            where T : class
        {
            return held.Select(_fixture.Now).ToList();
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
