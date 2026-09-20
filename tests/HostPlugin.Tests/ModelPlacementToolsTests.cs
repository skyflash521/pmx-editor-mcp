using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelPlacementToolsTests : IDisposable
    {
        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void AligningMovesEveryKindThatWasPointedAtInOneCall()
        {
            FakeVertex vertex = Vertex(1f, 1f, 1f);
            FakeBone bone = Bone(2f, 2f, 2f);
            FakeBody body = Body(3f, 3f, 3f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelPlaceElements.AllAxes),
                Position(5f, 6f, 7f),
                Targets(
                    Target(ElementKinds.Vertex, 0),
                    Target(ElementKinds.Bone, 0),
                    Target(ElementKinds.Body, 0))));

            Near(5.0, vertex.Position.X);
            Near(6.0, bone.Position.Y);
            Near(7.0, body.Position.Z);
            Assert.Equal(3, value[ModelPlaceElements.ChangedName]);
        }

        [Fact]
        public void AligningOneAxisLeavesTheOtherTwoWhereTheyWere()
        {
            FakeBone bone = Bone(2f, 2f, 2f);

            Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelEditVertices.AxisY),
                Position(5f, 6f, 7f),
                Targets(Target(ElementKinds.Bone, 0)));

            Near(2.0, bone.Position.X);
            Near(6.0, bone.Position.Y);
            Near(2.0, bone.Position.Z);
        }

        [Fact]
        public void AnElementThatIsAlreadyThereIsNotCounted()
        {
            Bone(5f, 6f, 7f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelPlaceElements.AllAxes),
                Position(5f, 6f, 7f),
                Targets(Target(ElementKinds.Bone, 0))));

            Assert.Equal(0, value[ModelPlaceElements.ChangedName]);
        }

        [Fact]
        public void AKindThatCannotBePlacedIsRefused()
        {
            Vertex(1f, 1f, 1f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelPlaceElements.AllAxes),
                Position(0f, 0f, 0f),
                Targets(Target(ElementKinds.Morph, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheSameKindTwiceIsRefused()
        {
            Bone(1f, 1f, 1f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelPlaceElements.AllAxes),
                Position(0f, 0f, 0f),
                Targets(Target(ElementKinds.Bone, 0), Target(ElementKinds.Bone, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void PointingOutsideTheListIsRefused()
        {
            Bone(1f, 1f, 1f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelPlaceElements.AllAxes),
                Position(0f, 0f, 0f),
                Targets(Target(ElementKinds.Bone, 3)));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void LeavingOutThePlaceToAlignToIsRefused()
        {
            Bone(1f, 1f, 1f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelPlaceElements.AllAxes),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void LeavingOutWhatToAlignIsRefused()
        {
            Bone(1f, 1f, 1f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelPlaceElements.AllAxes),
                Position(0f, 0f, 0f));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private IDictionary<string, object> Place(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(
                ModelPlaceElements.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
        }

        private static KeyValuePair<string, object> Axes(string axes)
        {
            return ComposedEditFixture.Given(ModelPlaceElements.AxesName, axes);
        }

        private static KeyValuePair<string, object> Position(float x, float y, float z)
        {
            return ComposedEditFixture.Given(
                ModelPlaceElements.PositionName, new object[] { x, y, z });
        }

        private static KeyValuePair<string, object> Targets(params object[] targets)
        {
            return ComposedEditFixture.Given(ModelPlaceElements.TargetsName, targets);
        }

        private static object Target(string kind, int at)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ModelPlaceElements.KindName, kind },
                { "indices", new object[] { at } },
            };
        }

        private FakeVertex Vertex(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            _fixture.Model.Vertex.Add(made);

            return made;
        }

        private FakeBone Bone(float x, float y, float z)
        {
            FakeBone made = new FakeBone("ボーン") { Position = new V3(x, y, z) };
            _fixture.Model.Bone.Add(made);

            return made;
        }

        private FakeBody Body(float x, float y, float z)
        {
            FakeBody made = new FakeBody("剛体") { Position = new V3(x, y, z) };
            _fixture.Model.Body.Add(made);

            return made;
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
