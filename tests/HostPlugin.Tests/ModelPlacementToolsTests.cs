using System;
using System.Collections.Generic;
using System.Diagnostics;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(TimedCollection.Name)]
    public sealed class ModelPlacementToolsTests : IDisposable
    {
        private const int Digits = 4;

        private const int ManyElements = 20000;

        private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(2);

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

            Near(5.0, Now(vertex).Position.X);
            Near(6.0, Now(bone).Position.Y);
            Near(7.0, Now(body).Position.Z);
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

            Near(2.0, Now(bone).Position.X);
            Near(6.0, Now(bone).Position.Y);
            Near(2.0, Now(bone).Position.Z);
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

            Near(1.5, Now(vertex).Position.Y);
            Near(0.0, Now(vertex).Position.Z);
            Near(2.5, Now(bone).Position.Y);
            Near(1.0, Now(bone).Position.Z);
            Near(3.5, Now(body).Position.Y);
            Near(2.0, Now(body).Position.Z);
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
            Near(0.0, Now(near).Position.X);
            Assert.Equal(3e38f, Now(far).Position.X);
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
            Now(vertex).Normal = new V3(1f, 0f, 0f);
            FakeBone bone = Bone(1f, 0f, 1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 90f, 0f),
                Triple(ModelPlaceElements.CenterName, 1f, 0f, 0f),
                Targets(Target(ElementKinds.Vertex, 0), Target(ElementKinds.Bone, 0))));

            // 行ベクトルへ右から掛ける取り決めで、Y軸まわりに90度回すと +X は -Z へ向く。
            Near(1.0, Now(vertex).Position.X);
            Near(-1.0, Now(vertex).Position.Z);
            Near(2.0, Now(bone).Position.X);
            Near(0.0, Now(bone).Position.Z);
            Near(0.0, Now(vertex).Normal.X);
            Near(-1.0, Now(vertex).Normal.Z);
            Assert.Equal(2, value[ModelPlaceElements.ChangedName]);
        }

        [Fact]
        public void TranslatingMovesAJoint()
        {
            FakeJoint joint = Joint(1f, 2f, 3f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 1f, -1f),
                Targets(Target(ElementKinds.Joint, 0))));

            Near(1.0, Now(joint).Position.X);
            Near(3.0, Now(joint).Position.Y);
            Near(2.0, Now(joint).Position.Z);
            Assert.Equal(1, value[ModelPlaceElements.ChangedName]);
        }

        [Fact]
        public void AligningMovesAJointToThePlace()
        {
            FakeJoint joint = Joint(1f, 2f, 3f);

            Place(
                Operation(ModelPlaceElements.AlignTo),
                Axes(ModelEditVertices.AxisX),
                Position(5f, 6f, 7f),
                Targets(Target(ElementKinds.Joint, 0)));

            Near(5.0, Now(joint).Position.X);
            Near(2.0, Now(joint).Position.Y);
        }

        [Fact]
        public void RotatingAJointTurnsItsPlaceAndItsRotation()
        {
            FakeJoint joint = Joint(2f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 90f, 0f),
                Triple(ModelPlaceElements.CenterName, 0f, 0f, 0f),
                Targets(Target(ElementKinds.Joint, 0))));

            Near(0.0, Now(joint).Position.X);
            Near(-2.0, Now(joint).Position.Z);
            Near(Math.PI / 2, Now(joint).Rotation.Y);
            Near(0.0, Now(joint).Rotation.X);
            Near(0.0, Now(joint).Rotation.Z);
            Assert.Equal(1, value[ModelPlaceElements.ChangedName]);
        }

        [Fact]
        public void RotatingAboutAnAxisTurnsLikeTheSameTurnGivenAsAngles()
        {
            FakeVertex vertex = Vertex(1f, 2f, 3f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationAxisName, 0f, 2f, 0f),
                ComposedEditFixture.Given(ModelPlaceElements.RotationAngleName, 90f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(3.0, Now(vertex).Position.X);
            Near(2.0, Now(vertex).Position.Y);
            Near(-1.0, Now(vertex).Position.Z);
        }

        [Fact]
        public void RotatingAboutATiltedAxisTurnsAboutThatAxis()
        {
            FakeVertex vertex = Vertex(1f, 0f, 0f);
            FakeBody body = Body(0f, 0f, 1f);
            Now(body).Rotation = new V3(0f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationAxisName, 1f, 1f, 1f),
                ComposedEditFixture.Given(ModelPlaceElements.RotationAngleName, 120f),
                Targets(Target(ElementKinds.Vertex, 0), Target(ElementKinds.Body, 0)));

            Near(0.0, Now(vertex).Position.X);
            Near(1.0, Now(vertex).Position.Y);
            Near(0.0, Now(vertex).Position.Z);
            Near(1.0, Now(body).Position.X);
            Near(0.0, Now(body).Position.Z);
        }

        [Fact]
        public void TheAngleAboutAnAxisIsWeakenedByTheRamp()
        {
            FakeVertex vertex = Vertex(1f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationAxisName, 0f, 1f, 0f),
                ComposedEditFixture.Given(ModelPlaceElements.RotationAngleName, 180f),
                Ramp("x", 0f, 2f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(0.0, Now(vertex).Position.X);
            Near(-1.0, Now(vertex).Position.Z);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, false, false)]
        public void RotatingTakesEitherTheAnglesOrTheAxisWithItsAngle(bool angles, bool axis, bool angle)
        {
            Vertex(1f, 0f, 0f);
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                Operation(ModelPlaceElements.RotateBy),
                Targets(Target(ElementKinds.Vertex, 0)),
            };
            if (angles)
            {
                given.Add(Triple(ModelPlaceElements.RotationName, 0f, 90f, 0f));
            }

            if (axis)
            {
                given.Add(Triple(ModelPlaceElements.RotationAxisName, 0f, 1f, 0f));
            }

            if (angle)
            {
                given.Add(ComposedEditFixture.Given(ModelPlaceElements.RotationAngleName, 90f));
            }

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(Place(given.ToArray())));
        }

        [Fact]
        public void AnAxisWithoutLengthIsRefused()
        {
            Vertex(1f, 0f, 0f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationAxisName, 0f, 0f, 0f),
                ComposedEditFixture.Given(ModelPlaceElements.RotationAngleName, 90f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheAxisIsRefusedOutsideRotating()
        {
            Vertex(1f, 0f, 0f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(1f, 0f, 0f),
                Triple(ModelPlaceElements.RotationAxisName, 0f, 1f, 0f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void RotatingAboutXTurnsTheNormalToo()
        {
            FakeVertex vertex = Vertex(0f, 0f, 0f);
            Now(vertex).Normal = new V3(0f, 1f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 90f, 0f, 0f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(0.0, Now(vertex).Normal.Y);
            Near(1.0, Now(vertex).Normal.Z);
        }

        [Fact]
        public void RotatingABodyAlsoTurnsItsRotation()
        {
            FakeBody body = Body(1f, 0f, 0f);
            Now(body).Rotation = new V3(0f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 90f, 0f),
                Targets(Target(ElementKinds.Body, 0)));

            Near(0.0, Now(body).Position.X);
            Near(-1.0, Now(body).Position.Z);
            Near(Math.PI / 2, Now(body).Rotation.Y);
            Near(0.0, Now(body).Rotation.X);
            Near(0.0, Now(body).Rotation.Z);
        }

        [Fact]
        public void ATurnThatRoundsToAQuarterInSinglePrecisionTakesTheEditorsQuarterTurnBranch()
        {
            FakeBody body = Body(0f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 89.99999f, 0f, 30f),
                Targets(Target(ElementKinds.Body, 0)));

            Assert.Equal((float)Math.PI / 2f, Now(body).Rotation.X);
            Near(-Math.PI / 6, Now(body).Rotation.Y);
            Assert.Equal(0f, Now(body).Rotation.Z);
        }

        [Fact]
        public void RotatingARotatedBodyComposesTheTwoTurns()
        {
            FakeBody body = Body(0f, 0f, 0f);
            Now(body).Rotation = new V3(0f, (float)(Math.PI / 4), 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 45f, 0f),
                Targets(Target(ElementKinds.Body, 0)));

            Near(Math.PI / 2, Now(body).Rotation.Y);
        }

        [Fact]
        public void ScalingEvenlyAlsoScalesTheSizeOfABody()
        {
            FakeBody body = Body(1f, 2f, 3f);
            Now(body).BoxSize = new V3(1f, 0.5f, 2f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 2f, 2f),
                Targets(Target(ElementKinds.Body, 0)));

            Near(2.0, Now(body).Position.X);
            Near(6.0, Now(body).Position.Z);
            Near(2.0, Now(body).BoxSize.X);
            Near(1.0, Now(body).BoxSize.Y);
            Near(4.0, Now(body).BoxSize.Z);
        }

        [Fact]
        public void ScalingUnevenlyLeavesTheSizeOfABody()
        {
            FakeBody body = Body(1f, 1f, 1f);
            Now(body).BoxSize = new V3(1f, 1f, 1f);
            FakeBone bone = Bone(1f, 1f, 1f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 1f, 1f),
                Triple(ModelPlaceElements.CenterName, 0f, 1f, 0f),
                Targets(Target(ElementKinds.Body, 0), Target(ElementKinds.Bone, 0)));

            Near(2.0, Now(body).Position.X);
            Near(1.0, Now(body).BoxSize.X);
            Near(2.0, Now(bone).Position.X);
            Near(1.0, Now(bone).Position.Y);
        }

        [Fact]
        public void ScalingUnevenlyTurnsTheNormalsWithTheInverseOfTheScale()
        {
            FakeVertex vertex = Vertex(1f, 1f, 0f);
            Now(vertex).Normal = new V3(1f, 1f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 1f, 1f),
                Targets(Target(ElementKinds.Vertex, 0)));

            double length = Math.Sqrt(0.25 + 1.0);
            Near(0.5 / length, Now(vertex).Normal.X);
            Near(1.0 / length, Now(vertex).Normal.Y);
            Near(0.0, Now(vertex).Normal.Z);
        }

        [Fact]
        public void ScalingEvenlyLeavesTheNormalsAsTheyWere()
        {
            FakeVertex vertex = Vertex(1f, 1f, 0f);
            Now(vertex).Normal = new V3(0f, 2f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 2f, 2f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Equal(0f, Now(vertex).Normal.X);
            Assert.Equal(2f, Now(vertex).Normal.Y);
        }

        [Fact]
        public void ScalingEvenlyByANegativeNumberTurnsTheNormalsOver()
        {
            FakeVertex vertex = Vertex(1f, 1f, 0f);
            Now(vertex).Normal = new V3(0f, 2f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, -1f, -1f, -1f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(-1.0, Now(vertex).Position.X);
            Near(-1.0, Now(vertex).Normal.Y);
        }

        [Fact]
        public void ScalingFlatAlongTurnedAxesFlattensThePositionsAndLeavesTheNormals()
        {
            FakeVertex vertex = Vertex(1f, 1f, 1f);
            Now(vertex).Normal = new V3(0.6f, 0.8f, -0.1f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 0f, 1f, 1f),
                Triple(ModelPlaceElements.ScaleFrameRotationName, 10f, 20f, 30f),
                Targets(Target(ElementKinds.Vertex, 0))));

            Assert.Equal(1, value[ModelPlaceElements.ChangedName]);
            Assert.Equal(0.6f, Now(vertex).Normal.X);
            Assert.Equal(0.8f, Now(vertex).Normal.Y);
            Assert.Equal(-0.1f, Now(vertex).Normal.Z);
            Assert.False(float.IsNaN(Now(vertex).Position.X));
        }

        [Fact]
        public void AVertexWithNoStrengthIsNotTouchedWhenTheAxesAreTurned()
        {
            FakeVertex outside = Vertex(1f, -1f, 0f);
            Now(outside).Normal = new V3(0.6f, 0.8f, 0f);
            FakeVertex inside = Vertex(1f, 3f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 1f, 1f),
                Triple(ModelPlaceElements.ScaleFrameAxisName, 1f, 2f, 3f),
                ComposedEditFixture.Given(ModelPlaceElements.ScaleFrameAngleName, 37.0),
                Ramp("y", 0f, 2f),
                Targets(Every(ElementKinds.Vertex))));

            Assert.Equal(1, value[ModelPlaceElements.ChangedName]);
            Assert.Equal(1f, Now(outside).Position.X);
            Assert.Equal(-1f, Now(outside).Position.Y);
            Assert.Equal(0.6f, Now(outside).Normal.X);
            Assert.Equal(0.8f, Now(outside).Normal.Y);
            Assert.NotEqual(1f, Now(inside).Position.X);
        }

        [Theory]
        [InlineData(45f, 0f, 0f, 1f, 2f, 1f, 0f, 1f, 1f, 0f, 2f, 2f)]
        [InlineData(0f, 45f, 0f, 2f, 1f, 1f, 1f, 0f, -1f, 2f, 0f, -2f)]
        public void TheAnglesTurnTheScaleAxesAboutTheAxesOfTheSameNames(
            float x, float y, float z, float sx, float sy, float sz,
            float px, float py, float pz, float ex, float ey, float ez)
        {
            FakeVertex vertex = Vertex(px, py, pz);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, sx, sy, sz),
                Triple(ModelPlaceElements.ScaleFrameRotationName, x, y, z),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(ex, Now(vertex).Position.X);
            Near(ey, Now(vertex).Position.Y);
            Near(ez, Now(vertex).Position.Z);
        }

        [Fact]
        public void ScalingFlatLeavesTheNormalsWhereTheyCannotBeInverted()
        {
            FakeVertex vertex = Vertex(1f, 1f, 0f);
            Now(vertex).Normal = new V3(0.6f, 0.8f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 0f, 1f, 1f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Equal(0.6f, Now(vertex).Normal.X);
            Assert.Equal(0.8f, Now(vertex).Normal.Y);
        }

        [Fact]
        public void TheScaleAxesCanBeTurnedByAnglesSoThatAnOffAxisPointStretchesAlongThem()
        {
            FakeVertex along = Vertex(1f, 1f, 0f);
            FakeVertex across = Vertex(1f, -1f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 1f, 1f),
                Triple(ModelPlaceElements.ScaleFrameRotationName, 0f, 0f, 45f),
                Targets(Every(ElementKinds.Vertex)));

            Near(2.0, Now(along).Position.X);
            Near(2.0, Now(along).Position.Y);
            Near(1.0, Now(across).Position.X);
            Near(-1.0, Now(across).Position.Y);
        }

        [Fact]
        public void TheScaleAxesCanBeTurnedAboutAnAxis()
        {
            FakeVertex along = Vertex(1f, 1f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 1f, 1f),
                Triple(ModelPlaceElements.ScaleFrameAxisName, 0f, 0f, 1f),
                ComposedEditFixture.Given(ModelPlaceElements.ScaleFrameAngleName, 45.0),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(2.0, Now(along).Position.X);
            Near(2.0, Now(along).Position.Y);
        }

        [Fact]
        public void TheNormalsFollowAScaleAlongTurnedAxes()
        {
            FakeVertex vertex = Vertex(1f, 1f, 0f);
            Now(vertex).Normal = new V3(1f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 1f, 1f),
                Triple(ModelPlaceElements.ScaleFrameRotationName, 0f, 0f, 45f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(0.75 / Math.Sqrt(0.625), Now(vertex).Normal.X);
            Near(-0.25 / Math.Sqrt(0.625), Now(vertex).Normal.Y);
            Near(0.0, Now(vertex).Normal.Z);
        }

        [Theory]
        [InlineData(ModelPlaceElements.TranslateBy)]
        [InlineData(ModelPlaceElements.RotateBy)]
        [InlineData(ModelPlaceElements.AlignTo)]
        public void TheScaleAxesAreNotAcceptedByTheOtherOperations(string operation)
        {
            Vertex(1f, 1f, 0f);

            IDictionary<string, object> envelope = Place(
                Operation(operation),
                Triple(ModelPlaceElements.ScaleFrameRotationName, 0f, 0f, 45f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Fact]
        public void TheScaleAxesGivenByAnglesAndByAnAxisTogetherAreRefused()
        {
            FakeVertex vertex = Vertex(1f, 1f, 0f);

            IDictionary<string, object> envelope = Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 2f, 1f, 1f),
                Triple(ModelPlaceElements.ScaleFrameRotationName, 0f, 0f, 45f),
                Triple(ModelPlaceElements.ScaleFrameAxisName, 0f, 0f, 1f),
                ComposedEditFixture.Given(ModelPlaceElements.ScaleFrameAngleName, 45.0),
                Targets(Target(ElementKinds.Vertex, 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Near(1.0, Now(vertex).Position.X);
        }

        [Fact]
        public void ScalingWithARampScalesEachVertexByWhereItIsAlongTheAxis()
        {
            FakeVertex low = Vertex(1f, 0f, 0f);
            FakeVertex middle = Vertex(1f, 1f, 0f);
            FakeVertex high = Vertex(1f, 2f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 0.5f, 1f, 1f),
                Ramp("y", 0f, 2f),
                Targets(Every(ElementKinds.Vertex))));

            Near(1.0, Now(low).Position.X);
            Near(0.75, Now(middle).Position.X);
            Near(0.5, Now(high).Position.X);
            Assert.Equal(2, value[ModelPlaceElements.ChangedName]);
        }

        [Fact]
        public void ARampHoldsItsEndsOutsideTheRange()
        {
            FakeVertex below = Vertex(1f, -1f, 0f);
            FakeVertex above = Vertex(1f, 3f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 0.5f, 1f, 1f),
                Ramp("y", 0f, 2f),
                Targets(Every(ElementKinds.Vertex)));

            Near(1.0, Now(below).Position.X);
            Near(0.5, Now(above).Position.X);
        }

        [Fact]
        public void ARampFromTheHigherEndGrowsTowardsTheLowerEnd()
        {
            FakeVertex low = Vertex(0f, 0f, 0f);
            FakeVertex high = Vertex(0f, 2f, 0f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 0f, 2f),
                Ramp("y", 2f, 0f),
                Targets(Every(ElementKinds.Vertex)));

            Near(2.0, Now(low).Position.Z);
            Near(0.0, Now(high).Position.Z);
        }

        [Fact]
        public void TheStrengthsOfSeveralRampsAreMultiplied()
        {
            FakeVertex both = Vertex(0f, 2f, 2f);
            FakeVertex half = Vertex(0f, 1f, 2f);
            FakeVertex none = Vertex(0f, 2f, 0f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(2f, 0f, 0f),
                Ramps(RampPart("y", 0f, 2f), RampPart("z", 0f, 2f)),
                Targets(Every(ElementKinds.Vertex)));

            Near(2.0, Now(both).Position.X);
            Near(1.0, Now(half).Position.X);
            Near(0.0, Now(none).Position.X);
        }

        [Fact]
        public void ARisingAndAFallingRampOnOneAxisMoveOnlyTheSpanBetween()
        {
            FakeVertex before = Vertex(0f, 0f, 0.5f);
            FakeVertex rising = Vertex(0f, 0f, 0.7f);
            FakeVertex peak = Vertex(0f, 0f, 0.85f);
            FakeVertex after = Vertex(0f, 0f, 1.2f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(1f, 0f, 0f),
                Ramps(RampPart("z", 0.55f, 0.85f), RampPart("z", 1.1f, 0.85f)),
                Targets(Every(ElementKinds.Vertex)));

            Near(0.0, Now(before).Position.X);
            Near(0.5, Now(rising).Position.X);
            Near(1.0, Now(peak).Position.X);
            Near(0.0, Now(after).Position.X);
        }

        [Fact]
        public void AnEmptyListOfRampsIsRefused()
        {
            Vertex(0f, 0f, 0f);

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Place(
                    Operation(ModelPlaceElements.TranslateBy),
                    Offset(1f, 0f, 0f),
                    Ramps(),
                    Targets(Every(ElementKinds.Vertex)))));
        }

        [Fact]
        public void TranslatingWithARampMovesEachElementByWhereItIsAlongTheAxis()
        {
            FakeVertex vertex = Vertex(0f, 1f, 0f);
            FakeBone bone = Bone(0f, 2f, 0f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 0f, 2f),
                Ramp("y", 0f, 2f),
                Targets(Target(ElementKinds.Vertex, 0), Target(ElementKinds.Bone, 0)));

            Near(1.0, Now(vertex).Position.Z);
            Near(2.0, Now(bone).Position.Z);
        }

        [Fact]
        public void ASmoothRampEasesInAndOutInsteadOfGrowingLinearly()
        {
            FakeVertex quarter = Vertex(0f, 0.5f, 0f);
            FakeVertex half = Vertex(0f, 1f, 0f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 0f, 1f),
                Ramps(Curved(RampPart("y", 0f, 2f), "smooth")),
                Targets(Every(ElementKinds.Vertex)));

            Near(0.15625, Now(quarter).Position.Z);
            Near(0.5, Now(half).Position.Z);
        }

        [Fact]
        public void ALinearCurveIsTheSameAsLeavingTheCurveOut()
        {
            FakeVertex vertex = Vertex(0f, 0.5f, 0f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 0f, 1f),
                Ramps(Curved(RampPart("y", 0f, 2f), "linear")),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(0.25, Now(vertex).Position.Z);
        }

        [Fact]
        public void AnUnknownCurveIsRefused()
        {
            Vertex(0f, 0.5f, 0f);

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Place(
                    Operation(ModelPlaceElements.TranslateBy),
                    Offset(0f, 0f, 1f),
                    Ramps(Curved(RampPart("y", 0f, 2f), "cubic")),
                    Targets(Target(ElementKinds.Vertex, 0)))));
        }

        [Fact]
        public void RotatingWithARampTurnsEachVertexAndItsNormalByWhereItIsAlongTheAxis()
        {
            FakeVertex vertex = Vertex(1f, 1f, 0f);
            Now(vertex).Normal = new V3(1f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.RotateBy),
                Triple(ModelPlaceElements.RotationName, 0f, 90f, 0f),
                Ramp("y", 0f, 2f),
                Targets(Target(ElementKinds.Vertex, 0)));

            Near(Math.Sqrt(0.5), Now(vertex).Position.X);
            Near(-Math.Sqrt(0.5), Now(vertex).Position.Z);
            Near(Math.Sqrt(0.5), Now(vertex).Normal.X);
            Near(-Math.Sqrt(0.5), Now(vertex).Normal.Z);
        }

        [Fact]
        public void ScalingWithoutARampMultipliesByTheGivenFactorItself()
        {
            FakeVertex vertex = Vertex(1000f, 0f, 0f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 0.001f, 1f, 1f),
                Targets(Every(ElementKinds.Vertex)));

            Assert.Equal(1000f * 0.001f, Now(vertex).Position.X);
        }

        [Fact]
        public void ScalingEvenlyWithARampScalesTheSizeOfABodyByItsStrength()
        {
            FakeBody body = Body(0f, 1f, 0f);
            Now(body).BoxSize = new V3(1f, 1f, 1f);

            Place(
                Operation(ModelPlaceElements.ScaleBy),
                Triple(ModelPlaceElements.ScaleName, 3f, 3f, 3f),
                Ramp("y", 0f, 2f),
                Targets(Target(ElementKinds.Body, 0)));

            Near(2.0, Now(body).Position.Y);
            Near(2.0, Now(body).BoxSize.X);
            Near(2.0, Now(body).BoxSize.Y);
            Near(2.0, Now(body).BoxSize.Z);
        }

        [Fact]
        public void ARampWithTheSameEndsIsRefused()
        {
            FakeVertex vertex = Vertex(1f, 1f, 0f);

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Place(
                    Operation(ModelPlaceElements.ScaleBy),
                    Triple(ModelPlaceElements.ScaleName, 0.5f, 1f, 1f),
                    Ramp("y", 1f, 1f),
                    Targets(Every(ElementKinds.Vertex)))));
            Near(1.0, Now(vertex).Position.X);
        }

        [Fact]
        public void ARampAlongAnUnknownAxisIsRefused()
        {
            Vertex(1f, 1f, 0f);

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Place(
                    Operation(ModelPlaceElements.ScaleBy),
                    Triple(ModelPlaceElements.ScaleName, 0.5f, 1f, 1f),
                    Ramp("w", 0f, 2f),
                    Targets(Every(ElementKinds.Vertex)))));
        }

        [Fact]
        public void AligningRefusesARamp()
        {
            Vertex(1f, 1f, 0f);

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ComposedEditFixture.Code(Place(
                    Operation(ModelPlaceElements.AlignTo),
                    Position(0f, 0f, 0f),
                    Axes(ModelPlaceElements.AllAxes),
                    Ramp("y", 0f, 2f),
                    Targets(Every(ElementKinds.Vertex)))));
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

        [Fact]
        public void MovingEveryVertexOfALargeModelFinishesInTime()
        {
            for (int at = 0; at < ManyElements; at++)
            {
                Vertex(0f, 0f, 0f);
            }

            TimeSpan editor = _fixture.EditorTime;
            Stopwatch elapsed = Stopwatch.StartNew();
            IDictionary<string, object> value = ComposedEditFixture.Value(Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 1f, 0f),
                Targets(Every(ElementKinds.Vertex))));
            elapsed.Stop();
            TimeSpan spent = elapsed.Elapsed - (_fixture.EditorTime - editor);

            Assert.Equal(ManyElements, value[ModelPlaceElements.ChangedName]);
            Assert.True(spent < TimeLimit, "動かすのに " + spent + " かかった");
        }

        [Fact]
        public void MovingRemakesOnlyTheKindsThatWerePointedAtInTheView()
        {
            Vertex(1f, 1f, 1f);
            Bone(2f, 2f, 2f);
            Body(3f, 3f, 3f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 1f, 0f),
                Targets(Target(ElementKinds.Vertex, 0), Target(ElementKinds.Bone, 0)));

            Assert.Equal(new[] { ElementKinds.Vertex, ElementKinds.Bone }, _fixture.View.Remade);
            Assert.Equal(0, _fixture.View.Redraws);
        }

        [Fact]
        public void MovingOnlyBonesWhileTheUndoIsRecordedReflectsTheWholeCopy()
        {
            Bone(1f, 1f, 1f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 1f, 0f),
                Targets(Target(ElementKinds.Bone, 0)));

            Assert.Empty(_fixture.Partials);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void MovingOnlyBonesWithTheUndoLockedReflectsOnlyTheBonesOfTheCopy()
        {
            FakeBone bone = Bone(1f, 1f, 1f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 1f, 0f),
                Targets(Target(ElementKinds.Bone, 0)),
                ComposedEditFixture.Given(UndoBarrier.SuppressName, true));

            KeyValuePair<PmxUpdateObject, int> partial = Assert.Single(_fixture.Partials);
            Assert.Equal(PmxUpdateObject.Bone, partial.Key);
            Assert.Equal(-1, partial.Value);
            Assert.Equal(1, _fixture.Commits);
            Assert.True(_fixture.Suppressed);
            Assert.False(_fixture.UndoLocked);
            Near(2.0, Now(bone).Position.Y);
        }

        [Fact]
        public void MovingSeveralKindsReflectsTheWholeCopyOnce()
        {
            Vertex(1f, 1f, 1f);
            Bone(2f, 2f, 2f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 1f, 0f),
                Targets(Target(ElementKinds.Vertex, 0), Target(ElementKinds.Bone, 0)));

            Assert.Empty(_fixture.Partials);
            Assert.Equal(1, _fixture.Commits);
        }

        [Fact]
        public void SuppressingTheUndoWhileMovingSeveralKindsReflectsTheWholeCopyWithTheUndoLocked()
        {
            FakeVertex vertex = Vertex(1f, 1f, 1f);
            FakeBone bone = Bone(2f, 2f, 2f);

            Place(
                Operation(ModelPlaceElements.TranslateBy),
                Offset(0f, 1f, 0f),
                Targets(Target(ElementKinds.Vertex, 0), Target(ElementKinds.Bone, 0)),
                ComposedEditFixture.Given(UndoBarrier.SuppressName, true));

            Assert.Empty(_fixture.Partials);
            Assert.Equal(1, _fixture.Commits);
            Assert.True(_fixture.Suppressed);
            Assert.False(_fixture.UndoLocked);
            Near(2.0, Now(vertex).Position.Y);
            Near(3.0, Now(bone).Position.Y);
        }

        private static object Every(string kind)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ModelPlaceElements.KindName, kind },
                { TargetNames.Element.All, true },
            };
        }

        private static KeyValuePair<string, object> Triple(string name, float x, float y, float z)
        {
            return ComposedEditFixture.Given(name, new object[] { x, y, z });
        }

        private static KeyValuePair<string, object> Ramp(string axis, float from, float to)
        {
            return Ramps(RampPart(axis, from, to));
        }

        private static KeyValuePair<string, object> Ramps(params object[] parts)
        {
            return ComposedEditFixture.Given(ModelPlaceElements.RampName, parts);
        }

        private static object RampPart(string axis, float from, float to)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ModelPlaceElements.RampAxisName, axis },
                { ModelPlaceElements.RampFromName, from },
                { ModelPlaceElements.RampToName, to },
            };
        }

        private static object Curved(object part, string curve)
        {
            ((IDictionary<string, object>)part).Add("curve", curve);

            return part;
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

        /// <summary>握った要素が並んでいた位置に、いまのモデルで並んでいる要素。</summary>
        private T Now<T>(T held)
            where T : class
        {
            return _fixture.Now(held);
        }

        private FakeJoint Joint(float x, float y, float z)
        {
            FakeJoint made = new FakeJoint("Joint") { Position = new V3(x, y, z) };
            _fixture.Model.Joint.Add(made);

            return made;
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
