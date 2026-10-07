using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditVerticesCopyFromBaseTests : IDisposable
    {
        private const string CopyFromBase = "copyFromBase";

        private const string BasePmxHandle = "basePmxHandle";

        private const string Changed = "changed";

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void ThePickedVerticesTakeThePositionNormalAndSdefValuesOfTheBase()
        {
            FakePmx held = Scene();
            FakeVertex source = (FakeVertex)held.Vertex[1];
            source.Position = new V3(4f, 5f, 6f);
            source.Normal = new V3(0f, 0f, 1f);
            source.SDEF_C = new V3(1f, 2f, 3f);
            source.SDEF_R0 = new V3(2f, 3f, 4f);
            source.SDEF_R1 = new V3(3f, 4f, 5f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Run(Issue(held), 1));

            IPXVertex copied = _fixture.Model.Vertex[1];
            Assert.Equal(1, value[Changed]);
            Assert.Equal(6f, copied.Position.Z);
            Assert.Equal(1f, copied.Normal.Z);
            Assert.Equal(3f, copied.SDEF_C.Z);
            Assert.Equal(4f, copied.SDEF_R0.Z);
            Assert.Equal(5f, copied.SDEF_R1.Z);
        }

        [Fact]
        public void VerticesThatAreNotPickedKeepTheirValues()
        {
            FakePmx held = Scene();
            foreach (IPXVertex vertex in held.Vertex)
            {
                vertex.Position = new V3(9f, 9f, 9f);
            }

            Run(Issue(held), 1);

            Assert.Equal(0f, _fixture.Model.Vertex[0].Position.X);
            Assert.Equal(9f, _fixture.Model.Vertex[1].Position.X);
            Assert.Equal(0f, _fixture.Model.Vertex[2].Position.X);
        }

        [Fact]
        public void APickedVertexAlreadyLikeTheBaseIsNotCounted()
        {
            FakePmx held = Scene();

            Assert.Equal(0, ComposedEditFixture.Value(Run(Issue(held), 0, 1))[Changed]);
        }

        [Fact]
        public void ABaseWithADifferentVertexCountIsRefused()
        {
            FakePmx held = Scene();
            held.Vertex.Add(new FakeVertex(7f, 7f, 7f));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(Run(Issue(held), 0)));
        }

        [Fact]
        public void WithoutABaseItIsRefused()
        {
            Scene();

            IDictionary<string, object> envelope = _fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", CopyFromBase),
                    ComposedEditFixture.Given("indices", new object[] { 0 })));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private FakePmx Scene()
        {
            FakePmx held = new FakePmx();
            Fill(held);
            Fill(_fixture.Model);

            return held;
        }

        private int Issue(FakePmx held)
        {
            return _fixture.Handles.Issue(typeof(IPXPmx).FullName, held, () => { });
        }

        private static void Fill(FakePmx model)
        {
            model.Vertex.Add(new FakeVertex(0f, 0f, 0f));
            model.Vertex.Add(new FakeVertex(0f, 1f, 0f));
            model.Vertex.Add(new FakeVertex(0f, 0f, 1f));
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(model.Vertex[0], model.Vertex[1], model.Vertex[2]));
            model.Material.Add(material);
        }

        private IDictionary<string, object> Run(int handle, params int[] picked)
        {
            return _fixture.Call(
                ModelEditVertices.ToolName,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("operation", CopyFromBase),
                    ComposedEditFixture.Given("indices", picked.Cast<object>().ToArray()),
                    ComposedEditFixture.Given(BasePmxHandle, (long)handle)));
        }
    }
}
