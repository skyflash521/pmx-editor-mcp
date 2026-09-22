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

        [Fact]
        public void TranslatingMovesEveryKindThatWasPointedAtByTheSameAmount()
        {
            FakeVertex vertex = Vertex(1f, 1f, 1f);
            FakeBone bone = Bone(2f, 2f, 2f);
            FakeBody body = Body(3f, 3f, 3f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 0.5f, -1f),
                Targets(
                    Target(ElementKinds.Vertex, 0),
                    Target(ElementKinds.Bone, 0),
                    Target(ElementKinds.Body, 0))));

            Near(1.5, vertex.Position.Y);
            Near(0.0, vertex.Position.Z);
            Near(2.5, bone.Position.Y);
            Near(1.0, bone.Position.Z);
            Near(3.5, body.Position.Y);
            Near(2.0, body.Position.Z);
            Assert.Equal(3, value[ModelPlaceElements.ChangedName]);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void TranslatingPastTheEndOfTheFloatRangeIsRefusedWithoutMovingAnything()
        {
            FakeBone near = Bone(0f, 0f, 0f);
            FakeBone far = Bone(3e38f, 0f, 0f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(3e38f, 0f, 0f),
                Targets(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { ModelPlaceElements.KindName, ElementKinds.Bone },
                    { "all", true },
                }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Near(0.0, near.Position.X);
            Assert.Equal(3e38f, far.Position.X);
            Assert.Equal(0, _fixture.Commits);
        }

        [Fact]
        public void TranslatingByNothingCountsNothing()
        {
            Bone(2f, 2f, 2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 0f, 0f),
                Targets(Target(ElementKinds.Bone, 0))));

            Assert.Equal(0, value[ModelPlaceElements.ChangedName]);
        }

        [Fact]
        public void TranslatingRefusesThePlaceToAlignTo()
        {
            Bone(2f, 2f, 2f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(1f, 0f, 0f),
                Position(5f, 6f, 7f),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void AligningRefusesTheAmountToTranslateBy()
        {
            Bone(2f, 2f, 2f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelPlaceElements.AllAxes),
                Position(5f, 6f, 7f),
                Offset(1f, 0f, 0f),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void LeavingOutTheAmountToTranslateByIsRefused()
        {
            Bone(2f, 2f, 2f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.TranslateBy),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void RotatingTurnsThePositionsAndTheNormalsAboutTheCentre()
        {
            FakeVertex vertex = Vertex(2f, 0f, 0f);
            FakeBone bone = Bone(1f, 0f, 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 90f, 0f),
                Triple(ModelPlaceElements.CenterName, 1f, 0f, 0f),
                Targets(Target(ElementKinds.Vertex, 0), Target(ElementKinds.Bone, 0))));

            // 行ベクトルへ右から掛ける取り決めで、Y軸まわりに90度回すと +X は -Z へ向く。
            Near(1.0, vertex.Position.X);
            Near(-1.0, vertex.Position.Z);
            Near(2.0, bone.Position.X);
            Near(0.0, bone.Position.Z);
            Near(1.0, vertex.Normal.Y);
            Assert.Equal(2, value[ModelPlaceElements.ChangedName]);
        }

        [Fact]
        public void RotatingAboutXTurnsTheNormalToo()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            vertex.Normal = new V3(0f, 1f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 90f, 0f, 0f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(0.0, vertex.Normal.Y);
            Near(1.0, vertex.Normal.Z);
        }

        [Fact]
        public void RotatingABodyAlsoTurnsItsRotation()
        {
            FakeBody body = Body(1f, 0f, 0f);
            body.Rotation = new V3(0f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 90f, 0f),
                Targets(Target(ElementKinds.Body, 0)));

            Near(0.0, body.Position.X);
            Near(-1.0, body.Position.Z);
            Near(Math.PI / 2, body.Rotation.Y);
            Near(0.0, body.Rotation.X);
            Near(0.0, body.Rotation.Z);
        }

        [Fact]
        public void ATurnThatRoundsToAQuarterInSinglePrecisionTakesTheEditorsQuarterTurnBranch()
        {
            FakeBody body = Body(0f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 89.99999f, 0f, 30f),
                Targets(Target(ElementKinds.Body, 0)));

            Assert.Equal((float)Math.PI / 2f, body.Rotation.X);
            Near(-Math.PI / 6, body.Rotation.Y);
            Assert.Equal(0f, body.Rotation.Z);
        }

        [Fact]
        public void RotatingARotatedBodyComposesTheTwoTurns()
        {
            FakeBody body = Body(0f, 0f, 0f);
            body.Rotation = new V3(0f, (float)(Math.PI / 4), 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 45f, 0f),
                Targets(Target(ElementKinds.Body, 0)));

            Near(Math.PI / 2, body.Rotation.Y);
        }

        [Fact]
        public void ScalingEvenlyAlsoScalesTheSizeOfABody()
        {
            FakeBody body = Body(1f, 2f, 3f);
            body.BoxSize = new V3(1f, 0.5f, 2f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 2f, 2f),
                Targets(Target(ElementKinds.Body, 0)));

            Near(2.0, body.Position.X);
            Near(6.0, body.Position.Z);
            Near(2.0, body.BoxSize.X);
            Near(1.0, body.BoxSize.Y);
            Near(4.0, body.BoxSize.Z);
        }

        [Fact]
        public void ScalingUnevenlyLeavesTheSizeOfABody()
        {
            FakeBody body = Body(1f, 1f, 1f);
            body.BoxSize = new V3(1f, 1f, 1f);
            FakeBone bone = Bone(1f, 1f, 1f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 1f, 1f),
                Triple(ModelPlaceElements.CenterName, 0f, 1f, 0f),
                Targets(Target(ElementKinds.Body, 0), Target(ElementKinds.Bone, 0)));

            Near(2.0, body.Position.X);
            Near(1.0, body.BoxSize.X);
            Near(2.0, bone.Position.X);
            Near(1.0, bone.Position.Y);
        }

        [Fact]
        public void RotatingRefusesTheAmountToTranslateBy()
        {
            Bone(1f, 1f, 1f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 90f, 0f),
                Offset(1f, 0f, 0f),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TranslatingRefusesTheCentre()
        {
            Bone(1f, 1f, 1f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(1f, 0f, 0f),
                Triple(ModelPlaceElements.CenterName, 0f, 0f, 0f),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private static KeyValuePair<string, object> Triple(string name, float x, float y, float z)
        {
            return ComposedEditFixture.Given(name, new object[] { x, y, z });
        }

        private static KeyValuePair<string, object> Offset(float x, float y, float z)
        {
            return ComposedEditFixture.Given(
                ModelPlaceElements.OffsetName, new object[] { x, y, z });
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
