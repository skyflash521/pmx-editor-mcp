using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditWeightsFromSurfaceSmoothTests : IDisposable
    {
        private const string FromSurface = "fromSurface";

        private const string SmoothSpatial = "smoothSpatial";

        private const string SurfaceMaterialIndices = "surfaceMaterialIndices";

        private const string Falloff = "falloff";

        private const string SourceSmoothRadius = "sourceSmoothRadius";

        private const string FalloffSmoothRadius = "falloffSmoothRadius";

        private const double Gap = 1.0;

        private const double Close = 0.1;

        private const double Middle = 0.4;

        private const double Far = 0.8;

        private const double FalloffNear = 0.2;

        private const double FalloffFade = 0.4;

        private const double CloseFade = 0.0;

        private const double MiddleFade = 0.5;

        private const double FarFade = 1.0;

        private const double SourceRadius = 2.0;

        private const double FalloffRadius = 4.0;

        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private FakeMaterial _surface;

        public static IEnumerable<object[]> BadRadii()
        {
            yield return new object[] { 0.0 };
            yield return new object[] { -1.0 };
            yield return new object[] { "1.0" };
            yield return new object[] { new object[] { 1.0 } };
            yield return new object[] { double.PositiveInfinity };
            yield return new object[] { double.NaN };
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void WithoutTheTwoSmoothingRadiiEachVertexTakesTheWeightsOfItsOwnNearestPoint()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Close, bones[2]);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Picking(first, second));

            Near(1.0, Share(first, bones[0]));
            Near(0.0, Share(first, bones[1]));
            Near(1.0, Share(second, bones[1]));
            Near(0.0, Share(second, bones[0]));
            Near(0.0, Share(first, bones[2]));
            Assert.Equal(2, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void WithoutTheTwoSmoothingRadiiTheFalloffBlendsEachVertexOnItsOwn()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Middle, bones[2]);

            ComposedEditFixture.Value(Picking(first, second, Fading()));

            Near(1.0, Share(first, bones[0]));
            Near(0.0, Share(first, bones[2]));
            Near(1.0 - MiddleFade, Share(second, bones[1]));
            Near(MiddleFade, Share(second, bones[2]));
            Near(0.0, Share(second, bones[0]));
        }

        [Fact]
        public void TheCopiedWeightsAreSmoothedIntoEachOtherByTheGaussianBeforeTheyAreMixed()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Close, bones[2]);

            IDictionary<string, object> value = ComposedEditFixture.Value(Picking(
                first,
                second,
                ComposedEditFixture.Given(SourceSmoothRadius, SourceRadius)));

            double reach = Gauss(Gap, SourceRadius);
            Assert.NotEqual(1.0, 1.0 / (1.0 + reach), Digits);
            Near(1.0 / (1.0 + reach), Share(first, bones[0]));
            Near(reach / (1.0 + reach), Share(first, bones[1]));
            Near(1.0 / (1.0 + reach), Share(second, bones[1]));
            Near(reach / (1.0 + reach), Share(second, bones[0]));
            Near(0.0, Share(first, bones[2]));
            Near(0.0, Share(second, bones[2]));
            Assert.Equal(2, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void ACopyBeyondTheSourceSmoothRadiusIsNotSmoothedIn()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Close, bones[2]);

            ComposedEditFixture.Value(Picking(
                first,
                second,
                ComposedEditFixture.Given(SourceSmoothRadius, Gap / 2.0)));

            Near(1.0, Share(first, bones[0]));
            Near(0.0, Share(first, bones[1]));
            Near(1.0, Share(second, bones[1]));
            Near(0.0, Share(second, bones[0]));
        }

        [Fact]
        public void OnlyThePickedVerticesTakePartInTheSourceSmoothing()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex left = Target(Gap, Close, bones[2]);

            ComposedEditFixture.Value(Picking(
                first,
                ComposedEditFixture.Given(SourceSmoothRadius, SourceRadius)));

            Near(1.0, Share(first, bones[0]));
            Near(0.0, Share(first, bones[1]));
            Near(0.0, Share(first, bones[2]));
            Near(1.0, Share(left, bones[2]));
        }

        [Fact]
        public void TheBlendingFractionOfTheFalloffIsSmoothedIntoEachOtherByTheGaussian()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Far, bones[2]);

            IDictionary<string, object> value = ComposedEditFixture.Value(Picking(
                first,
                second,
                Fading(),
                ComposedEditFixture.Given(FalloffSmoothRadius, FalloffRadius)));

            double reach = Gauss(Apart(Close, Far), FalloffRadius);
            double firstFade = (CloseFade + (reach * FarFade)) / (1.0 + reach);
            double secondFade = (FarFade + (reach * CloseFade)) / (1.0 + reach);
            Assert.NotEqual(CloseFade, firstFade, Digits);
            Assert.NotEqual(FarFade, secondFade, Digits);
            Near(1.0 - firstFade, Share(first, bones[0]));
            Near(firstFade, Share(first, bones[2]));
            Near(1.0 - secondFade, Share(second, bones[1]));
            Near(secondFade, Share(second, bones[2]));
            Near(0.0, Share(first, bones[1]));
            Near(0.0, Share(second, bones[0]));
            Assert.Equal(2, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AFractionBeyondTheFalloffSmoothRadiusIsNotSmoothedIn()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Far, bones[2]);

            IDictionary<string, object> value = ComposedEditFixture.Value(Picking(
                first,
                second,
                Fading(),
                ComposedEditFixture.Given(FalloffSmoothRadius, Gap / 2.0)));

            Near(1.0, Share(first, bones[0]));
            Near(0.0, Share(first, bones[2]));
            Near(1.0, Share(second, bones[2]));
            Near(0.0, Share(second, bones[1]));
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void OnlyThePickedVerticesTakePartInTheFalloffSmoothing()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            Target(Gap, Far, bones[2]);

            ComposedEditFixture.Value(Picking(
                first,
                Fading(),
                ComposedEditFixture.Given(FalloffSmoothRadius, FalloffRadius)));

            Near(1.0, Share(first, bones[0]));
            Near(0.0, Share(first, bones[2]));
        }

        [Fact]
        public void BothSmoothingsTakeEffectTogetherEachWithItsOwnRadius()
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Middle, bones[2]);

            ComposedEditFixture.Value(Picking(
                first,
                second,
                Fading(),
                ComposedEditFixture.Given(SourceSmoothRadius, SourceRadius),
                ComposedEditFixture.Given(FalloffSmoothRadius, FalloffRadius)));

            double source = Gauss(Apart(Close, Middle), SourceRadius);
            double fraction = Gauss(Apart(Close, Middle), FalloffRadius);
            Assert.NotEqual(source, fraction, Digits);
            double firstFade = (CloseFade + (fraction * MiddleFade)) / (1.0 + fraction);
            double secondFade = (MiddleFade + (fraction * CloseFade)) / (1.0 + fraction);
            double own = 1.0 / (1.0 + source);
            Near((1.0 - firstFade) * own, Share(first, bones[0]));
            Near((1.0 - firstFade) * (1.0 - own), Share(first, bones[1]));
            Near(firstFade, Share(first, bones[2]));
            Near((1.0 - secondFade) * (1.0 - own), Share(second, bones[0]));
            Near((1.0 - secondFade) * own, Share(second, bones[1]));
            Near(secondFade, Share(second, bones[2]));
        }

        [Theory]
        [MemberData(nameof(BadRadii))]
        public void ASourceSmoothRadiusThatIsNotAPositiveFiniteNumberIsRefused(object given)
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Close, bones[2]);
            ComposedEditFixture.Value(Picking(
                first,
                second,
                ComposedEditFixture.Given(SourceSmoothRadius, SourceRadius)));

            IDictionary<string, object> envelope = Picking(
                first,
                second,
                ComposedEditFixture.Given(SourceSmoothRadius, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(SourceSmoothRadius, ComposedEditFixture.Message(envelope));
        }

        [Theory]
        [MemberData(nameof(BadRadii))]
        public void AFalloffSmoothRadiusThatIsNotAPositiveFiniteNumberIsRefused(object given)
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Far, bones[2]);
            ComposedEditFixture.Value(Picking(
                first,
                second,
                Fading(),
                ComposedEditFixture.Given(FalloffSmoothRadius, FalloffRadius)));

            IDictionary<string, object> envelope = Picking(
                first,
                second,
                Fading(),
                ComposedEditFixture.Given(FalloffSmoothRadius, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(FalloffSmoothRadius, ComposedEditFixture.Message(envelope));
        }

        [Theory]
        [InlineData(SourceSmoothRadius, SmoothSpatial)]
        [InlineData(SourceSmoothRadius, ModelEditWeights.Average)]
        [InlineData(FalloffSmoothRadius, SmoothSpatial)]
        [InlineData(FalloffSmoothRadius, ModelEditWeights.Average)]
        public void TheSmoothRadiiArePassedOnlyToTheCopyFromTheSurface(string name, string operation)
        {
            IList<IPXBone> bones = Bones("甲", "乙", "元");
            Plate(bones[0], 0.0);
            Plate(bones[1], Gap);
            FakeVertex first = Target(0.0, Close, bones[2]);
            FakeVertex second = Target(Gap, Close, bones[2]);
            ComposedEditFixture.Value(Picking(
                first,
                second,
                Fading(),
                ComposedEditFixture.Given(name, SourceRadius)));
            ComposedEditFixture.Value(Other(operation, first, second));

            IDictionary<string, object> envelope = Other(
                operation,
                first,
                second,
                ComposedEditFixture.Given(name, SourceRadius));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
        }

        private IDictionary<string, object> Picking(
            FakeVertex first, params KeyValuePair<string, object>[] given)
        {
            return Picking(first, null, given);
        }

        private IDictionary<string, object> Picking(
            FakeVertex first, FakeVertex second, params KeyValuePair<string, object>[] given)
        {
            List<object> picked = new List<object> { _fixture.Model.Vertex.IndexOf(first) };
            if (second != null)
            {
                picked.Add(_fixture.Model.Vertex.IndexOf(second));
            }

            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(FromSurface),
                ComposedEditFixture.Given(SurfaceMaterialIndices, new object[] { 0 }),
                ComposedEditFixture.Given("indices", picked.ToArray()),
            };
            all.AddRange(given);

            return Weights(all.ToArray());
        }

        private IDictionary<string, object> Other(
            string operation,
            FakeVertex first,
            FakeVertex second,
            params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(operation),
                ComposedEditFixture.Given(
                    "indices",
                    new object[]
                    {
                        _fixture.Model.Vertex.IndexOf(first),
                        _fixture.Model.Vertex.IndexOf(second),
                    }),
            };
            if (operation == SmoothSpatial)
            {
                all.Add(ComposedEditFixture.Given("radius", SourceRadius));
                all.Add(ComposedEditFixture.Given("iterations", 1));
            }

            all.AddRange(given);

            return Weights(all.ToArray());
        }

        private static KeyValuePair<string, object> Fading()
        {
            return ComposedEditFixture.Given(
                Falloff,
                ComposedEditFixture.Arguments(
                    ComposedEditFixture.Given("near", FalloffNear),
                    ComposedEditFixture.Given("fade", FalloffFade)));
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

        private FakeMaterial Surface()
        {
            if (_surface == null)
            {
                _surface = new FakeMaterial("面");
                _fixture.Model.Material.Add(_surface);
            }

            return _surface;
        }

        private void Plate(IPXBone bone, double x)
        {
            FakeVertex[] corners =
            {
                new FakeVertex((float)(x - 0.5), 0f, -1f),
                new FakeVertex((float)(x + 0.5), 0f, -1f),
                new FakeVertex((float)x, 0f, 1f),
            };
            foreach (FakeVertex corner in corners)
            {
                Weigh(corner, bone, 1f);
                _fixture.Model.Vertex.Add(corner);
            }

            Surface().Faces.Add(new FakeFace(corners[0], corners[1], corners[2]));
        }

        private FakeVertex Target(double x, double height, IPXBone bone)
        {
            FakeVertex target = new FakeVertex((float)x, (float)height, 0f);
            Weigh(target, bone, 1f);
            _fixture.Model.Vertex.Add(target);

            return target;
        }

        private static void Weigh(FakeVertex weighed, IPXBone bone, float share)
        {
            weighed.Bone1 = bone;
            weighed.Weight1 = share;
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

        private static double Apart(double heightOfFirst, double heightOfSecond)
        {
            double rise = heightOfSecond - heightOfFirst;

            return Math.Sqrt((Gap * Gap) + (rise * rise));
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
