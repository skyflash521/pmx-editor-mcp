using System;
using System.Collections.Generic;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class MotionRotateBoneAboutAxisTests : IDisposable
    {
        private const double Tolerance = 1e-5;

        private readonly ComposedScreenFixture _fixture = new ComposedScreenFixture();

        public MotionRotateBoneAboutAxisTests()
        {
            _fixture.Model.Bone.Add(new FakeBone("足首"));
            _fixture.Model.Bone.Add(new FakeBone("軸固定") { IsFixAxis = true });
            _fixture.TransformView.SelectedBoneIndex = 0;
            _fixture.TransformView.BoneRotate_XYZ = new V3(1f, 2f, 3f);
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData(1.0, 0.0, 0.0, 30.0)]
        [InlineData(0.0, 1.0, 0.0, -45.0)]
        [InlineData(0.0, 0.0, 1.0, 170.0)]
        [InlineData(1.0, 0.0, 0.0875, 40.0)]
        [InlineData(1.0, 1.0, 1.0, 120.0)]
        [InlineData(0.3, -0.5, 0.8, -75.0)]
        [InlineData(1.0, 0.0, 0.0, 90.0)]
        [InlineData(1.0, 0.0, 0.0, -90.0)]
        [InlineData(0.6, 0.0, 0.8, 90.0)]
        public void TheBoneTurnsAboutTheGivenAxisInOneRotation(double x, double y, double z, double angle)
        {
            Rotate(new object[] { x, y, z }, angle);

            V3 turned = Assert.Single(_fixture.TransformView.Rotations);
            double[,] made = EditorTurn(turned);
            double[,] wanted = AxisTurn(x, y, z, angle);
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    Assert.InRange(made[row, column], wanted[row, column] - Tolerance, wanted[row, column] + Tolerance);
                }
            }
        }

        [Fact]
        public void TheAnglesHandedToTheEditorAreReturned()
        {
            IDictionary<string, object> value = ComposedScreenFixture.Value(Rotate(new object[] { 0.0, 1.0, 0.0 }, 30.0));

            V3 turned = Assert.Single(_fixture.TransformView.Rotations);
            Assert.Equal(
                new object[] { (double)turned.X, (double)turned.Y, (double)turned.Z },
                (object[])value[MotionRotateBoneAboutAxis.RotateXyzName]);
        }

        [Fact]
        public void TheRotationInputFieldsAreLeftAsTheyWere()
        {
            Rotate(new object[] { 1.0, 0.0, 0.0 }, 30.0);

            V3 held = new V3(_fixture.TransformView.BoneRotate_XYZ);
            Assert.Equal(1f, held.X);
            Assert.Equal(2f, held.Y);
            Assert.Equal(3f, held.Z);
        }

        [Fact]
        public void AnAxisWithoutLengthIsRefused()
        {
            IDictionary<string, object> envelope = Rotate(new object[] { 0.0, 0.0, 0.0 }, 30.0);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Rotations);
        }

        [Fact]
        public void AnAngleThatIsNotANumberIsRefused()
        {
            IDictionary<string, object> envelope = Rotate(new object[] { 1.0, 0.0, 0.0 }, "30");

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Rotations);
        }

        [Fact]
        public void ABoneWithAFixedAxisIsRefused()
        {
            _fixture.TransformView.SelectedBoneIndex = 1;

            IDictionary<string, object> envelope = Rotate(new object[] { 1.0, 0.0, 0.0 }, 30.0);

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Rotations);
        }

        [Fact]
        public void NoChosenBoneIsRefused()
        {
            _fixture.TransformView.SelectedBoneIndex = -1;

            IDictionary<string, object> envelope = Rotate(new object[] { 1.0, 0.0, 0.0 }, 30.0);

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Rotations);
        }

        [Fact]
        public void HeldModifierKeysAreRefused()
        {
            _fixture.Keys.Held = true;

            IDictionary<string, object> envelope = Rotate(new object[] { 1.0, 0.0, 0.0 }, 30.0);

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Rotations);
        }

        private IDictionary<string, object> Rotate(object[] axis, object angle)
        {
            return _fixture.Call(
                MotionRotateBoneAboutAxis.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given(MotionRotateBoneAboutAxis.AxisName, axis),
                    ComposedScreenFixture.Given(MotionRotateBoneAboutAxis.AngleName, angle)));
        }

        /// <summary>エディタが入力欄の値で回す向き。Z・X・Y の順に、親の軸まわりに重ねる。</summary>
        private static double[,] EditorTurn(V3 degrees)
        {
            return Product(
                Product(AxisTurn(0, 1, 0, degrees.Y), AxisTurn(1, 0, 0, degrees.X)), AxisTurn(0, 0, 1, degrees.Z));
        }

        private static double[,] AxisTurn(double x, double y, double z, double degrees)
        {
            double length = Math.Sqrt((x * x) + (y * y) + (z * z));
            x /= length;
            y /= length;
            z /= length;
            double radians = degrees * Math.PI / 180;
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            double t = 1 - c;

            return new[,]
            {
                { (t * x * x) + c, (t * x * y) - (s * z), (t * x * z) + (s * y) },
                { (t * x * y) + (s * z), (t * y * y) + c, (t * y * z) - (s * x) },
                { (t * x * z) - (s * y), (t * y * z) + (s * x), (t * z * z) + c },
            };
        }

        private static double[,] Product(double[,] left, double[,] right)
        {
            double[,] made = new double[3, 3];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    for (int at = 0; at < 3; at++)
                    {
                        made[row, column] += left[row, at] * right[at, column];
                    }
                }
            }

            return made;
        }
    }
}
