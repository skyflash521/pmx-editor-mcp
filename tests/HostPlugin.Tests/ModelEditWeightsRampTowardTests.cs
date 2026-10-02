using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditWeightsRampTowardTests : IDisposable
    {
        private const string RampToward = "rampToward";

        private const string TargetIndices = "targetIndices";

        private const string Radius = "radius";

        private const double RampRadius = 2.0;

        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheCloserAVertexIsToTheTargetTheMoreItsWeightMovesToTheTargetWeight()
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            FakeVertex near = Rooted(0.5f, 0f, 0f, bones[0]);
            FakeVertex middle = Rooted(1f, 0f, 0f, bones[0]);
            FakeVertex far = Rooted(1.5f, 0f, 0f, bones[0]);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1, 2, 3 })));

            Near(Ratio(0.5), Share(near, bones[1]));
            Near(1.0 - Ratio(0.5), Share(near, bones[0]));
            Near(Ratio(1.0), Share(middle, bones[1]));
            Near(1.0 - Ratio(1.0), Share(middle, bones[0]));
            Near(Ratio(1.5), Share(far, bones[1]));
            Near(1.0 - Ratio(1.5), Share(far, bones[0]));
            Assert.Equal(3, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AVertexBeyondTheRadiusIsLeftAlone()
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            FakeVertex inside = Rooted(1f, 0f, 0f, bones[0]);
            FakeVertex outside = Rooted(3f, 0f, 0f, bones[0]);
            Weigh(outside, bones[0], 0.5f, bones[1], 0.5f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1, 2 })));

            Near(Ratio(1.0), Share(inside, bones[1]));
            Near(0.5, Share(outside, bones[0]));
            Near(0.5, Share(outside, bones[1]));
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AVertexExactlyAtTheRadiusIsLeftAlone()
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            FakeVertex edge = Rooted(2f, 0f, 0f, bones[0]);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near(1.0, Share(edge, bones[0]));
            Near(0.0, Share(edge, bones[1]));
            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AVertexWhoseRatioIsZeroKeepsWeightsThatDoNotAddUpToOne()
        {
            IList<IPXBone> bones = Bones("根", "副", "先");
            Rooted(0f, 0f, 0f, bones[2]);
            FakeVertex outside = Rooted(3f, 0f, 0f, bones[0]);
            Weigh(outside, bones[0], 0.3f, bones[1], 0.2f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AMiddleVertexTakesTheBlendOfItsOwnWeightsAndTheTargetWeights()
        {
            IList<IPXBone> bones = Bones("根", "副", "先", "先副");
            FakeVertex target = Rooted(0f, 0f, 0f, bones[2]);
            Weigh(target, bones[2], 0.5f, bones[3], 0.5f);
            FakeVertex middle = Rooted(0.6f, 0.8f, 0f, bones[0]);
            Weigh(middle, bones[0], 0.6f, bones[1], 0.4f);

            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            double distance = Math.Sqrt(0.6 * 0.6 + 0.8 * 0.8);
            double ratio = 1.0 - distance / RampRadius;
            Near(0.5, ratio);
            Near((1.0 - ratio) * 0.6, Share(middle, bones[0]));
            Near((1.0 - ratio) * 0.4, Share(middle, bones[1]));
            Near(ratio * 0.5, Share(middle, bones[2]));
            Near(ratio * 0.5, Share(middle, bones[3]));
        }

        [Fact]
        public void TheNearestOfSeveralTargetsDecidesTheDistance()
        {
            IList<IPXBone> bones = Bones("根", "先一", "先二");
            Rooted(0f, 0f, 0f, bones[1]);
            Rooted(1.5f, 0f, 0f, bones[2]);
            FakeVertex between = Rooted(1f, 0f, 0f, bones[0]);

            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0, 1 },
                    ComposedEditFixture.Given("indices", new object[] { 2 })));

            Near(Ratio(0.5), Share(between, bones[2]));
            Near(0.0, Share(between, bones[1]));
            Near(1.0 - Ratio(0.5), Share(between, bones[0]));
        }

        [Fact]
        public void AVertexBeyondTheRadiusOfEveryTargetIsLeftAlone()
        {
            IList<IPXBone> bones = Bones("根", "先一", "先二");
            Rooted(0f, 0f, 0f, bones[1]);
            Rooted(10f, 0f, 0f, bones[2]);
            FakeVertex between = Rooted(5f, 0f, 0f, bones[0]);
            Weigh(between, bones[0], 0.5f, bones[1], 0.5f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0, 1 },
                    ComposedEditFixture.Given("indices", new object[] { 2 })));

            Near(0.5, Share(between, bones[0]));
            Near(0.5, Share(between, bones[1]));
            Near(0.0, Share(between, bones[2]));
            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void OnlyThePickedVerticesAreChanged()
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            FakeVertex picked = Rooted(0.5f, 0f, 0f, bones[0]);
            FakeVertex left = Rooted(0.5f, 0f, 0f, bones[0]);

            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near(Ratio(0.5), Share(picked, bones[1]));
            Near(1.0, Share(left, bones[0]));
            Near(0.0, Share(left, bones[1]));
        }

        [Fact]
        public void TheTargetVerticesThemselvesAreNotChanged()
        {
            IList<IPXBone> bones = Bones("根", "先");
            FakeVertex target = Rooted(0f, 0f, 0f, bones[1]);
            Rooted(0.5f, 0f, 0f, bones[0]);

            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near(1.0, Share(target, bones[1]));
            Near(0.0, Share(target, bones[0]));
        }

        [Fact]
        public void TheWeightsAfterTheRampAddUpToOne()
        {
            IList<IPXBone> bones = Bones("根", "副", "先");
            Rooted(0f, 0f, 0f, bones[2]);
            FakeVertex middle = Rooted(0.7f, 0f, 0f, bones[0]);
            Weigh(middle, bones[0], 0.3f, bones[1], 0.7f);

            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near(
                1.0,
                (double)Share(middle, bones[0]) + Share(middle, bones[1]) + Share(middle, bones[2]));
        }

        [Fact]
        public void TheTargetsAreRequiredForTheRamp()
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            Rooted(0.5f, 0f, 0f, bones[0]);
            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            IDictionary<string, object> envelope = Weights(
                Operation(RampToward),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(Radius, RampRadius));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(TargetIndices, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheRadiusIsRequiredForTheRamp()
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            Rooted(0.5f, 0f, 0f, bones[0]);
            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            IDictionary<string, object> envelope = Weights(
                Operation(RampToward),
                ComposedEditFixture.Given("indices", new object[] { 1 }),
                ComposedEditFixture.Given(TargetIndices, new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Radius, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void ThePickedVerticesAreRequiredForTheRamp()
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            Rooted(0.5f, 0f, 0f, bones[0]);
            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            IDictionary<string, object> envelope = Weights(
                Operation(RampToward),
                ComposedEditFixture.Given(TargetIndices, new object[] { 0 }),
                ComposedEditFixture.Given(Radius, RampRadius));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void ARadiusThatIsNotPositiveIsRefused(double radius)
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            Rooted(0.5f, 0f, 0f, bones[0]);
            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            IDictionary<string, object> envelope = Ramp(
                radius,
                new object[] { 0 },
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Radius, ComposedEditFixture.Message(envelope));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(-1)]
        public void ATargetOutsideTheListIsRefused(int target)
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            Rooted(0.5f, 0f, 0f, bones[0]);
            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            IDictionary<string, object> envelope = Ramp(
                RampRadius,
                new object[] { target },
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Contains(TargetIndices, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void ATargetThatIsNotAPositionIsRefusedAsInvalid()
        {
            IList<IPXBone> bones = Bones("根", "先");
            Rooted(0f, 0f, 0f, bones[1]);
            Rooted(0.5f, 0f, 0f, bones[0]);
            ComposedEditFixture.Value(
                Ramp(
                    RampRadius,
                    new object[] { 0 },
                    ComposedEditFixture.Given("indices", new object[] { 1 })));

            object[] wrong = { 0, "a", new object[] { "a" }, new object[] { 0.5 } };
            foreach (object target in wrong)
            {
                IDictionary<string, object> envelope = Weights(
                    Operation(RampToward),
                    ComposedEditFixture.Given("indices", new object[] { 1 }),
                    ComposedEditFixture.Given(TargetIndices, target),
                    ComposedEditFixture.Given(Radius, RampRadius));

                Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
                Assert.Contains(TargetIndices, ComposedEditFixture.Message(envelope));
            }
        }

        private static double Ratio(double distance)
        {
            return 1.0 - distance / RampRadius;
        }

        private IDictionary<string, object> Ramp(
            double radius, object[] targets, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(RampToward),
                ComposedEditFixture.Given(TargetIndices, targets),
                ComposedEditFixture.Given(Radius, radius),
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

        private FakeVertex Rooted(float x, float y, float z, IPXBone bone)
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

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
