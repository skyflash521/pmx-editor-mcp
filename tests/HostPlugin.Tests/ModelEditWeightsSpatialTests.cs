using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditWeightsSpatialTests : IDisposable
    {
        private const string SmoothSpatial = "smoothSpatial";

        private const string AverageNear = "averageNear";

        private const string Radius = "radius";

        private const string Iterations = "iterations";

        private const string Threshold = "threshold";

        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void PartsTheMeshDoesNotJoinAreSmoothedIntoEachOtherWhereSmoothLeavesThemAlone()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f, bones[0]);
            FakeVertex second = Vertex(0.5f, 0f, 0f, bones[1]);
            Part(first, bones[0]);
            Part(second, bones[1]);

            Weights(
                Operation(ModelEditWeights.Smooth),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }),
                ComposedEditFixture.Given(ModelEditWeights.StrengthName, 1.0));
            Near(1.0, Share(first, bones[0]));
            Near(1.0, Share(second, bones[1]));

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Spatial(1.0, 1, ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            double near = Gauss(0.5, 1.0);
            Near(1.0 / (1.0 + near), Share(first, bones[0]));
            Near(near / (1.0 + near), Share(first, bones[1]));
            Near(near / (1.0 + near), Share(second, bones[0]));
            Near(1.0 / (1.0 + near), Share(second, bones[1]));
            Assert.Equal(2, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void TheGaussianSigmaIsHalfTheRadius()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex centre = Vertex(0f, 0f, 0f, bones[0]);
            Vertex(1f, 0f, 0f, bones[1]);
            Vertex(0f, 1.5f, 0f, bones[2]);

            ComposedEditFixture.Value(Spatial(2.0, 1, ComposedEditFixture.Given("all", true)));

            double second = Gauss(1.0, 2.0);
            double third = Gauss(1.5, 2.0);
            double total = 1.0 + second + third;
            Near(1.0 / total, Share(centre, bones[0]));
            Near(second / total, Share(centre, bones[1]));
            Near(third / total, Share(centre, bones[2]));
        }

        [Fact]
        public void AVertexBeyondTheRadiusIsNotReachedEvenWhenTheMeshJoinsIt()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f, bones[0]);
            FakeVertex second = Vertex(3f, 0f, 0f, bones[1]);
            FakeVertex third = Vertex(3f, 0f, 1f, bones[1]);
            Join(first, second, third);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Spatial(2.0, 3, ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            Near(1.0, Share(first, bones[0]));
            Near(0.0, Share(first, bones[1]));
            Near(1.0, Share(second, bones[1]));
            Assert.Equal(0, value[ModelEditWeights.ChangedName]);

            Weights(
                Operation(ModelEditWeights.Smooth),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelEditWeights.StrengthName, 1.0));
            Near(1.0, Share(first, bones[1]));
        }

        [Fact]
        public void OnlyThePickedVerticesTakePartInTheSmoothing()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex first = Vertex(0f, 0f, 0f, bones[0]);
            FakeVertex second = Vertex(0.5f, 0f, 0f, bones[1]);
            FakeVertex left = Vertex(0.2f, 0f, 0f, bones[2]);

            ComposedEditFixture.Value(Spatial(
                1.0, 1, ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            double near = Gauss(0.5, 1.0);
            Near(1.0 / (1.0 + near), Share(first, bones[0]));
            Near(near / (1.0 + near), Share(first, bones[1]));
            Near(0.0, Share(first, bones[2]));
            Near(1.0, Share(left, bones[2]));
            Near(0.0, Share(second, bones[2]));
        }

        [Fact]
        public void EachIterationSmoothsTheResultOfTheOneBefore()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f, bones[0]);
            FakeVertex second = Vertex(0.5f, 0f, 0f, bones[1]);

            ComposedEditFixture.Value(Spatial(1.0, 2, ComposedEditFixture.Given("all", true)));

            double near = Gauss(0.5, 1.0);
            double once = 1.0 / (1.0 + near);
            double other = near / (1.0 + near);
            double twice = (once + near * other) / (1.0 + near);
            Near(twice, Share(first, bones[0]));
            Near(1.0 - twice, Share(first, bones[1]));
            Near(1.0 - twice, Share(second, bones[0]));
        }

        [Fact]
        public void TheRadiusIsRequiredForTheSpatialSmoothing()
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Spatial(1.0, 1, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope = Weights(
                Operation(SmoothSpatial),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(Iterations, 1));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Radius, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheIterationsAreRequiredForTheSpatialSmoothing()
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Spatial(1.0, 1, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope = Weights(
                Operation(SmoothSpatial),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(Radius, 1.0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Iterations, ComposedEditFixture.Message(envelope));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void ARadiusThatIsNotPositiveIsRefused(double radius)
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Spatial(1.0, 1, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope =
                Spatial(radius, 1, ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Radius, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void ZeroIterationsAreRefused()
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Spatial(1.0, 1, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope =
                Spatial(1.0, 0, ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Iterations, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheRadiusIsRefusedOnAnyOperationButTheSpatialSmoothing()
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Spatial(1.0, 1, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope = Weights(
                Operation(AverageNear),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(Threshold, 1.0),
                ComposedEditFixture.Given(Radius, 1.0));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Radius, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void EachGroupWithinTheThresholdIsAveragedOnItsOwn()
        {
            IList<IPXBone> bones = Bones("一", "二", "三", "四");
            FakeVertex first = Vertex(0f, 0f, 0f, bones[0]);
            FakeVertex second = Vertex(0.4f, 0f, 0f, bones[0]);
            Weigh(second, bones[0], 0.5f, bones[1], 0.5f);
            FakeVertex third = Vertex(10f, 0f, 0f, bones[2]);
            FakeVertex fourth = Vertex(10.4f, 0f, 0f, bones[3]);

            IDictionary<string, object> value = ComposedEditFixture.Value(Grouped(
                0.5, ComposedEditFixture.Given("all", true)));

            Near(0.75, Share(first, bones[0]));
            Near(0.25, Share(first, bones[1]));
            Near(0.75, Share(second, bones[0]));
            Near(0.25, Share(second, bones[1]));
            Near(0.0, Share(first, bones[2]));
            Near(0.5, Share(third, bones[2]));
            Near(0.5, Share(third, bones[3]));
            Near(0.5, Share(fourth, bones[2]));
            Near(0.5, Share(fourth, bones[3]));
            Near(0.0, Share(third, bones[0]));
            Assert.Equal(4, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AVertexBeyondTheThresholdIsLeftAloneEvenWhenTheMeshJoinsIt()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f, bones[0]);
            FakeVertex second = Vertex(3f, 0f, 0f, bones[1]);
            FakeVertex third = Vertex(3f, 0f, 1f, bones[1]);
            Join(first, second, third);

            IDictionary<string, object> value = ComposedEditFixture.Value(Grouped(
                0.5, ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            Near(1.0, Share(first, bones[0]));
            Near(1.0, Share(second, bones[1]));
            Assert.Equal(0, value[ModelEditWeights.ChangedName]);

            Weights(
                Operation(ModelEditWeights.Average),
                ComposedEditFixture.Given("indices", new object[] { 0, 1 }));
            Near(0.5, Share(first, bones[1]));
        }

        [Fact]
        public void AVertexExactlyAtTheThresholdIsInTheGroup()
        {
            IList<IPXBone> bones = Bones("一", "二");
            FakeVertex first = Vertex(0f, 0f, 0f, bones[0]);
            FakeVertex second = Vertex(0.5f, 0f, 0f, bones[1]);

            ComposedEditFixture.Value(Grouped(0.5, ComposedEditFixture.Given("all", true)));

            Near(0.5, Share(first, bones[0]));
            Near(0.5, Share(second, bones[0]));
        }

        [Fact]
        public void OnlyThePickedVerticesJoinAGroup()
        {
            IList<IPXBone> bones = Bones("一", "二", "三");
            FakeVertex first = Vertex(0f, 0f, 0f, bones[0]);
            FakeVertex second = Vertex(0.2f, 0f, 0f, bones[1]);
            FakeVertex left = Vertex(0.1f, 0f, 0f, bones[2]);

            ComposedEditFixture.Value(Grouped(
                0.5, ComposedEditFixture.Given("indices", new object[] { 0, 1 })));

            Near(0.5, Share(first, bones[0]));
            Near(0.5, Share(first, bones[1]));
            Near(0.0, Share(first, bones[2]));
            Near(1.0, Share(left, bones[2]));
            Near(0.5, Share(second, bones[0]));
        }

        [Fact]
        public void TheThresholdIsRequiredForTheGroupAveraging()
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Grouped(0.5, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope = Weights(
                Operation(AverageNear),
                ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Threshold, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void ANegativeThresholdIsRefused()
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Grouped(0.5, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope =
                Grouped(-0.5, ComposedEditFixture.Given("all", true));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Threshold, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheThresholdIsRefusedOnAnyOperationButTheGroupAveraging()
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Grouped(0.5, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope = Weights(
                Operation(SmoothSpatial),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(Radius, 1.0),
                ComposedEditFixture.Given(Iterations, 1),
                ComposedEditFixture.Given(Threshold, 0.5));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Threshold, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheIterationsAreRefusedOnTheGroupAveraging()
        {
            IList<IPXBone> bones = Bones("一", "二");
            Vertex(0f, 0f, 0f, bones[0]);
            Vertex(0.5f, 0f, 0f, bones[1]);
            ComposedEditFixture.Value(Grouped(0.5, ComposedEditFixture.Given("all", true)));

            IDictionary<string, object> envelope = Weights(
                Operation(AverageNear),
                ComposedEditFixture.Given("all", true),
                ComposedEditFixture.Given(Threshold, 0.5),
                ComposedEditFixture.Given(Iterations, 1));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Iterations, ComposedEditFixture.Message(envelope));
        }

        private IDictionary<string, object> Spatial(
            double radius, int iterations, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(SmoothSpatial),
                ComposedEditFixture.Given(Radius, radius),
                ComposedEditFixture.Given(Iterations, iterations),
            };
            all.AddRange(given);

            return Weights(all.ToArray());
        }

        private IDictionary<string, object> Grouped(
            double threshold, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(AverageNear),
                ComposedEditFixture.Given(Threshold, threshold),
            };
            all.AddRange(given);

            return Weights(all.ToArray());
        }

        private IDictionary<string, object> Weights(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelEditWeights.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
        }

        private IList<IPXBone> Bones(params string[] names)
        {
            List<IPXBone> made = new List<IPXBone>();
            foreach (string name in names)
            {
                FakeBone bone = new FakeBone(name);
                _fixture.Model.Bone.Add(bone);
                made.Add(bone);
            }

            return made;
        }

        private FakeVertex Vertex(float x, float y, float z, IPXBone bone)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            made.Bone1 = bone;
            made.Weight1 = 1f;
            _fixture.Model.Vertex.Add(made);

            return made;
        }

        private static void Weigh(
            FakeVertex weighed, IPXBone first, float firstShare, IPXBone second, float secondShare)
        {
            weighed.Bone1 = first;
            weighed.Weight1 = firstShare;
            weighed.Bone2 = second;
            weighed.Weight2 = secondShare;
        }

        private void Part(FakeVertex corner, IPXBone bone)
        {
            FakeVertex up = Vertex(corner.Position.X, 10f, 0f, bone);
            FakeVertex side = Vertex(corner.Position.X, 10f, 1f, bone);
            Join(corner, up, side);
        }

        private void Join(FakeVertex first, FakeVertex second, FakeVertex third)
        {
            FakeMaterial material = new FakeMaterial("材質");
            material.Faces.Add(new FakeFace(first, second, third));
            _fixture.Model.Material.Add(material);
        }

        private float Share(IPXVertex given, IPXBone weighed)
        {
            IPXVertex now = _fixture.Now(given);
            IPXBone target = _fixture.Now(weighed);
            IPXBone[] held = { now.Bone1, now.Bone2, now.Bone3, now.Bone4 };
            float[] weights = { now.Weight1, now.Weight2, now.Weight3, now.Weight4 };
            float share = 0f;
            for (int at = 0; at < held.Length; at++)
            {
                if (ReferenceEquals(held[at], target))
                {
                    share += weights[at];
                }
            }

            return share;
        }

        private static double Gauss(double distance, double radius)
        {
            double sigma = radius / 2.0;

            return Math.Exp(-distance * distance / (2.0 * sigma * sigma));
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
