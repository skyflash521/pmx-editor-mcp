using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class UnchangedElementCountTests : IDisposable
    {
        private const string Changed = "changed";

        private const string All = "all";

        private const string Indices = "indices";

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void FlippingAUvThatSitsOnTheFoldCountsNothing()
        {
            FakeVertex vertex = new FakeVertex(0f, 0f, 0f);
            vertex.UV = new V2(0.5f, 0.25f);
            _fixture.Model.Vertex.Add(vertex);

            IDictionary<string, object> value = Run(
                ModelEditUv.ToolName,
                Operation(ModelEditUv.FlipU),
                ComposedEditFixture.Given(All, true));

            Assert.Equal(0, value[Changed]);
        }

        [Fact]
        public void CopyingAUvTheVertexAlreadyHasCountsNothing()
        {
            FakeVertex source = new FakeVertex(0f, 0f, 0f);
            source.UV = new V2(0.5f, 0.75f);
            FakeVertex target = new FakeVertex(1f, 0f, 0f);
            target.UV = new V2(0.5f, 0.75f);
            _fixture.Model.Vertex.Add(source);
            _fixture.Model.Vertex.Add(target);

            IDictionary<string, object> value = Run(
                ModelEditUv.ToolName,
                Operation(ModelEditUv.Copy),
                ComposedEditFixture.Given(Indices, new object[] { 1 }),
                ComposedEditFixture.Given(ModelEditUv.SourceName, 0));

            Assert.Equal(0, value[Changed]);
        }

        [Fact]
        public void ClampingColoursThatAreAlreadyInRangeCountsNothing()
        {
            FakeMaterial material = new FakeMaterial();
            material.Diffuse = new V4(0.25f, 0.5f, 0.75f, 1f);
            material.Specular = new V3(0f, 0.5f, 1f);
            material.Ambient = new V3(0.5f, 0.5f, 0.5f);
            material.EdgeColor = new V4(0f, 0f, 0f, 1f);
            _fixture.Model.Material.Add(material);

            IDictionary<string, object> value = Run(
                ModelEditMaterials.ToolName,
                Operation(ModelEditMaterials.ClampColor),
                ComposedEditFixture.Given(All, true));

            Assert.Equal(0, value[Changed]);
        }

        [Theory]
        [InlineData(ModelEditBones.FixAxisToTip)]
        [InlineData(ModelEditBones.SetLocalAxis)]
        [InlineData(ModelEditBones.SetPmdBoneKind)]
        public void SettingABoneAgainToWhatItAlreadyHoldsCountsNothing(string operation)
        {
            FakeBone bone = new FakeBone("腕");
            bone.ToOffset = new V3(3f, 4f, 0f);
            _fixture.Model.Bone.Add(bone);
            KeyValuePair<string, object>[] given =
            {
                Operation(operation),
                ComposedEditFixture.Given(Indices, new object[] { 0 }),
            };

            Assert.Equal(1, Run(ModelEditBones.ToolName, given)[Changed]);
            Assert.Equal(0, Run(ModelEditBones.ToolName, given)[Changed]);
        }

        [Fact]
        public void AligningVerticesThatAlreadySitOnTheirAverageCountsNothing()
        {
            _fixture.Model.Vertex.Add(new FakeVertex(1f, 2f, 3f));
            _fixture.Model.Vertex.Add(new FakeVertex(4f, 2f, 5f));

            IDictionary<string, object> value = Run(
                ModelEditVertices.ToolName,
                Operation(ModelEditVertices.Align),
                ComposedEditFixture.Given(All, true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisY));

            Assert.Equal(0, value[Changed]);
        }

        [Fact]
        public void MirroringAVertexWhosePositionAndNormalHaveNoAxisComponentCountsNothing()
        {
            FakeVertex vertex = new FakeVertex(0f, 2f, 3f);
            vertex.Normal = new V3(0f, 1f, 0f);
            _fixture.Model.Vertex.Add(vertex);

            IDictionary<string, object> value = Run(
                ModelEditVertices.ToolName,
                Operation(ModelEditVertices.MirrorModel),
                ComposedEditFixture.Given(All, true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisX));

            Assert.Equal(0, value[Changed]);
        }

        [Fact]
        public void MirroringAnSdefVertexOnTheAxisCountsItWhenOnlyItsCentreIsPlacedAgain()
        {
            FakeBone first = new FakeBone("上");
            first.Position = new V3(0f, 0f, 0f);
            FakeBone second = new FakeBone("下");
            second.Position = new V3(0f, 4f, 0f);
            _fixture.Model.Bone.Add(first);
            _fixture.Model.Bone.Add(second);
            FakeVertex vertex = new FakeVertex(0f, 2f, 3f);
            vertex.Normal = new V3(0f, 1f, 0f);
            vertex.SDEF = true;
            vertex.Bone1 = first;
            vertex.Bone2 = second;
            vertex.Weight1 = 0.5f;
            vertex.Weight2 = 0.5f;
            vertex.SDEF_C = new V3(0f, 9f, 0f);
            _fixture.Model.Vertex.Add(vertex);

            IDictionary<string, object> value = Run(
                ModelEditVertices.ToolName,
                Operation(ModelEditVertices.MirrorModel),
                ComposedEditFixture.Given(All, true),
                ComposedEditFixture.Given(ModelEditVertices.AxisName, ModelEditVertices.AxisX));

            Assert.Equal(1, value[Changed]);
            Assert.Equal(2f, _fixture.Model.Vertex[0].SDEF_C.Y);
        }

        [Fact]
        public void SeparatingAFaceThatSharesNoVertexCountsNothing()
        {
            FakeVertex[] vertices =
            {
                new FakeVertex(0f, 0f, 0f), new FakeVertex(1f, 0f, 0f), new FakeVertex(0f, 1f, 0f),
            };
            foreach (FakeVertex vertex in vertices)
            {
                _fixture.Model.Vertex.Add(vertex);
            }

            FakeMaterial material = new FakeMaterial();
            material.Faces.Add(new FakeFace(vertices[0], vertices[1], vertices[2]));
            _fixture.Model.Material.Add(material);

            IDictionary<string, object> value = Run(
                ModelEditFaces.ToolName,
                Operation(ModelEditFaces.SeparateSharedVertices),
                ComposedEditFixture.Given("parentAll", true),
                ComposedEditFixture.Given(Indices, new object[] { 0 }));

            Assert.Equal(0, value[Changed]);
            Assert.Equal(0, value[ModelEditFaces.AddedVerticesName]);
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
        }

        private IDictionary<string, object> Run(string tool, params KeyValuePair<string, object>[] given)
        {
            return ComposedEditFixture.Value(_fixture.Call(tool, ComposedEditFixture.Arguments(given)));
        }
    }
}
