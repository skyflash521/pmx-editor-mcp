using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class PmxModelDoublesTests
    {
        [Fact]
        public void ChangingWhatWasReflectedAsAWholeLeavesTheStateAsItWas()
        {
            FakePmx state = Model();
            FakePmx passed = FakeEditorState.Duplicate(state);

            FakeEditorState.Reflect(state, passed, PmxUpdateObject.All, -1);
            passed.Vertex[0].Position.X = 9f;
            passed.Vertex[0].Position = new V3(7f, 7f, 7f);
            passed.Bone[0].Name = "書き換え";
            passed.Material[0].Faces[0].Vertex1 = passed.Vertex[2];

            Assert.Equal(1f, state.Vertex[0].Position.X);
            Assert.Equal("親", state.Bone[0].Name);
            Assert.Same(state.Vertex[0], state.Material[0].Faces[0].Vertex1);
        }

        [Fact]
        public void ChangingTheFacesThatWereReflectedLeavesTheStateAsItWas()
        {
            FakePmx state = Model();
            FakePmx passed = FakeEditorState.Duplicate(state);

            FakeEditorState.Reflect(state, passed, PmxUpdateObject.Face, -1);
            passed.Material[0].Faces[0].Vertex1 = passed.Vertex[2];

            Assert.Same(state.Vertex[0], state.Material[0].Faces[0].Vertex1);
        }

        [Fact]
        public void ChangingTheOneVertexThatWasReflectedLeavesTheStateAsItWas()
        {
            FakePmx state = Model();
            FakePmx passed = FakeEditorState.Duplicate(state);

            FakeEditorState.Reflect(state, passed, PmxUpdateObject.Vertex, 1);
            passed.Vertex[1].Position.X = 9f;

            Assert.Equal(2f, state.Vertex[1].Position.X);
        }

        private static FakePmx Model()
        {
            FakePmx model = new FakePmx();
            FakeBone bone = new FakeBone("親");
            model.Bone.Add(bone);
            for (int at = 1; at <= 3; at++)
            {
                model.Vertex.Add(new FakeVertex(at, 0f, 0f) { Bone1 = bone, Weight1 = 1f });
            }

            FakeMaterial material = new FakeMaterial();
            material.Faces.Add(new FakeFace(model.Vertex[0], model.Vertex[1], model.Vertex[2]));
            model.Material.Add(material);

            return model;
        }
    }
}
