using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelCreatePhysicsFitBodiesTests : IDisposable
    {
        private const string FitBodiesToChains = "fitBodiesToChains";

        private const string Chains = "chains";

        private const string Closed = "closed";

        private const string Cover = "cover";

        private const string Thickness = "thickness";

        private const string Changed = "changed";

        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        private readonly Dictionary<FakeBody, V3> _placed = new Dictionary<FakeBody, V3>();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void BodiesAroundAClosedRingAreFittedToTheSectionsAndTheNeighbours()
        {
            IList<IList<IPXBone>> ring = Flared();
            IList<FakeBody> bodies = Held(ring);

            IDictionary<string, object> value = ComposedEditFixture.Value(Fit(
                ring,
                ComposedEditFixture.Given(Closed, true),
                ComposedEditFixture.Given(Thickness, 0.25)));

            IPXBody front = Now(bodies[0]);
            Assert.Equal(BodyBoxKind.Box, front.BoxKind);
            NearAll(new V3(2.5f, -1f, 0f), front.Position);
            Axis(new V3(0f, 0f, 1f), front, 0);
            Axis(Unit(-1f, 2f, 0f), front, 1);
            Axis(Unit(-2f, -1f, 0f), front, 2);
            Near(Math.Sqrt(12.5) / 2.0, front.BoxSize.X);
            Near(Math.Sqrt(5.0) / 2.0, front.BoxSize.Y);
            Near(0.25, front.BoxSize.Z);

            IPXBody lowerSide = Now(bodies[3]);
            NearAll(new V3(0f, -3f, 3.5f), lowerSide.Position);
            Axis(new V3(-1f, 0f, 0f), lowerSide, 0);
            Axis(Unit(0f, 2f, -1f), lowerSide, 1);
            Axis(Unit(0f, -1f, -2f), lowerSide, 2);
            Near(Math.Sqrt(24.5) / 2.0, lowerSide.BoxSize.X);

            Assert.Equal(8, value[Changed]);
            Assert.Empty((object[])value[ModelCreatePhysics.AddedBodiesName]);
            Assert.Empty((object[])value[ModelCreatePhysics.AddedJointsName]);
            Assert.Equal(8, _fixture.Model.Body.Count);
        }

        [Fact]
        public void TheEndsOfAnOpenRowTakeTheirOwnMiddleForTheMissingNeighbour()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            IList<FakeBody> bodies = Held(row);

            ComposedEditFixture.Value(Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            IPXBody first = Now(bodies[0]);
            NearAll(new V3(0f, -1f, 0f), first.Position);
            Axis(new V3(1f, 0f, 0f), first, 0);
            Axis(new V3(0f, 1f, 0f), first, 1);
            Axis(new V3(0f, 0f, 1f), first, 2);
            Near(0.5, first.BoxSize.X);
            Near(1.0, first.BoxSize.Y);
            Near(1.0, Now(bodies[2]).BoxSize.X);
            IPXBody last = Now(bodies[4]);
            Axis(new V3(1f, 0f, 0f), last, 0);
            Near(1.0, last.BoxSize.X);
        }

        [Fact]
        public void AnOpenRingDoesNotTieTheLastChainToTheFirst()
        {
            IList<IList<IPXBone>> ring = Flared();
            IList<FakeBody> bodies = Held(ring);

            ComposedEditFixture.Value(Fit(ring, ComposedEditFixture.Given(Thickness, 0.25)));

            IPXBody front = Now(bodies[0]);
            Axis(Unit(-2f, -1f, 2.5f), front, 0);
            Near(Math.Sqrt(12.5) / 2.0, front.BoxSize.X);
        }

        [Fact]
        public void TheCoverWidensTheWidthAndTheHeightButNotTheThickness()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            IList<FakeBody> bodies = Held(row);

            ComposedEditFixture.Value(Fit(
                row,
                ComposedEditFixture.Given(Cover, 1.5),
                ComposedEditFixture.Given(Thickness, 0.25)));

            IPXBody middle = Now(bodies[2]);
            Near(1.5, middle.BoxSize.X);
            Near(1.5, middle.BoxSize.Y);
            Near(0.25, middle.BoxSize.Z);
            NearAll(new V3(1f, -1f, 0f), middle.Position);
        }

        [Fact]
        public void EverythingButThePlaceTheTurnTheShapeAndTheSizeIsKept()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            IList<FakeBody> bodies = Held(row);
            FakeBody body = bodies[2];
            body.Name = "右1";
            body.NameE = "right1";
            body.Group = 4;
            body.PassGroup[2] = true;
            body.Mass = 3f;
            body.PositionDamping = 0.5f;
            body.RotationDamping = 0.75f;
            body.Restitution = 0.125f;
            body.Friction = 0.375f;
            body.Mode = BodyMode.DynamicWithBone;
            FakeJoint joint = new FakeJoint("縦")
            {
                BodyA = bodies[2],
                BodyB = bodies[3],
                Position = new V3(1f, -2f, 0f),
                Rotation = new V3(0.5f, 0f, 0f),
            };
            _fixture.Model.Joint.Add(joint);

            ComposedEditFixture.Value(Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            IPXBody now = Now(body);
            Assert.Equal("右1", now.Name);
            Assert.Equal("right1", now.NameE);
            Assert.Same(Now(row[1][0]), now.Bone);
            Assert.Equal(4, now.Group);
            Assert.Equal(
                Enumerable.Range(0, 16).Select(at => at == 2).ToArray(), now.PassGroup);
            Assert.Equal(3f, now.Mass);
            Assert.Equal(0.5f, now.PositionDamping);
            Assert.Equal(0.75f, now.RotationDamping);
            Assert.Equal(0.125f, now.Restitution);
            Assert.Equal(0.375f, now.Friction);
            Assert.Equal(BodyMode.DynamicWithBone, now.Mode);
            IPXJoint nowJoint = Assert.Single(_fixture.Model.Joint);
            Assert.Same(now, nowJoint.BodyA);
            Assert.Same(Now(bodies[3]), nowJoint.BodyB);
            NearAll(new V3(1f, -2f, 0f), nowJoint.Position);
            NearAll(new V3(0.5f, 0f, 0f), nowJoint.Rotation);
        }

        [Fact]
        public void ABoneWithoutABodyIsPassedOverAndNoBodyIsMade()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            IList<FakeBody> bodies = Held(row);
            _fixture.Model.Body.Remove(bodies[2]);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            Assert.Equal(5, _fixture.Model.Body.Count);
            Assert.DoesNotContain(
                _fixture.Model.Body, held => ReferenceEquals(held.Bone, Now(row[1][0])));
            Assert.Equal(5, value[Changed]);
            Near(1.0, Now(bodies[3]).BoxSize.X);
        }

        [Fact]
        public void TheFirstBodyOnABoneIsTheOneThatIsFitted()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            IList<FakeBody> bodies = Held(row);
            FakeBody second = new FakeBody("二つ目") { Bone = row[1][0], Position = new V3(9f, 9f, 9f) };
            _fixture.Model.Body.Add(second);

            ComposedEditFixture.Value(Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            NearAll(new V3(1f, -1f, 0f), Now(bodies[2]).Position);
            NearAll(new V3(9f, 9f, 9f), Now(second).Position);
        }

        [Fact]
        public void ASectionOfNoLengthIsPassedOver()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            IList<FakeBody> bodies = Held(row);
            ((FakeBone)row[1][1]).Position = new V3(1f, 0f, 0f);
            ((FakeBone)row[1][1]).ToOffset = new V3(0f, -4f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            Kept(bodies[2]);
            Assert.Equal(5, value[Changed]);
        }

        [Fact]
        public void ASectionWithNeitherNeighbourIsPassedOver()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            IList<IPXBone> longer = Bones("左3");
            ((FakeBone)longer[0]).Position = new V3(0f, -4f, 0f);
            ((FakeBone)longer[0]).ToOffset = new V3(0f, -2f, 0f);
            ((FakeBone)row[0][1]).ToBone = longer[0];
            row[0].Add(longer[0]);
            IList<FakeBody> bodies = Held(row);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            Kept(bodies[2]);
            Assert.Equal(4, value[Changed]);
        }

        [Fact]
        public void ASectionWhoseNeighboursLieAlongItIsPassedOver()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            ((FakeBone)row[1][0]).Position = new V3(0f, -2f, 0f);
            ((FakeBone)row[1][1]).Position = new V3(0f, -4f, 0f);
            IList<FakeBody> bodies = Held(row);

            ComposedEditFixture.Value(Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            Kept(bodies[0]);
        }

        [Fact]
        public void ASectionWhoseNeighbourLiesAlongASlantIsPassedOver()
        {
            IList<IPXBone> bones = Bones("斜め1", "斜め2");
            ((FakeBone)bones[0]).Position = new V3(0f, 0f, 0f);
            ((FakeBone)bones[1]).Position = new V3(1f, 1f, 0f);
            ((FakeBone)bones[0]).ToOffset = new V3(-1f, -1f, 0f);
            ((FakeBone)bones[1]).ToOffset = new V3(-1f, -1f, 0f);
            IList<IList<IPXBone>> row = new List<IList<IPXBone>>
            {
                new List<IPXBone> { bones[0] },
                new List<IPXBone> { bones[1] },
            };
            IList<FakeBody> bodies = Held(row);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            Kept(bodies[0]);
            Kept(bodies[1]);
            Assert.Equal(0, value[Changed]);
        }

        [Fact]
        public void ASectionWithANonFinitePointIsPassedOver()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            IList<FakeBody> bodies = Held(row);
            ((FakeBone)row[1][1]).ToOffset = new V3(float.NaN, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            Kept(bodies[3]);
            Assert.Equal(3, value[Changed]);
        }

        [Fact]
        public void ALongerChainTakesTheOneNeighbourThatReachesThatDeep()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            foreach (int chain in new[] { 1, 2 })
            {
                IList<IPXBone> deeper = Bones("段3-" + chain);
                FakeBone above = (FakeBone)row[chain][1];
                ((FakeBone)deeper[0]).Position = new V3(above.Position.X, -4f, 0f);
                ((FakeBone)deeper[0]).ToOffset = new V3(0f, -2f, 0f);
                above.ToBone = deeper[0];
                row[chain].Add(deeper[0]);
            }

            ((FakeBone)row[2][2]).Position = new V3(4f, -4f, 0f);
            ((FakeBone)row[2][2]).ToOffset = new V3(-1f, -2f, 0f);
            IList<FakeBody> bodies = Held(row);

            ComposedEditFixture.Value(Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            IPXBody deep = Now(bodies[4]);
            NearAll(new V3(1f, -5f, 0f), deep.Position);
            Axis(new V3(1f, 0f, 0f), deep, 0);
            Near(Math.Sqrt(6.25) / 2.0, deep.BoxSize.X);
        }

        [Fact]
        public void TheLastSectionEndsAtTheBoneItPointsTo()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f, 3f);
            IList<IPXBone> end = Bones("列1先");
            ((FakeBone)end[0]).Position = new V3(1f, -3f, 0f);
            ((FakeBone)row[1][1]).ToBone = end[0];
            IList<FakeBody> bodies = Held(row);

            ComposedEditFixture.Value(Fit(row, ComposedEditFixture.Given(Thickness, 0.25)));

            IPXBody last = Now(bodies[3]);
            NearAll(new V3(1f, -2.5f, 0f), last.Position);
            Axis(new V3(0f, 1f, 0f), last, 1);
            Near(0.5, last.BoxSize.Y);
            Near(Math.Sqrt(4.25) / 2.0, last.BoxSize.X);
        }

        [Fact]
        public void FewerThanTwoChainsAreRefused()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            Held(row);

            IDictionary<string, object> envelope = Physics(
                Operation(FitBodiesToChains),
                ComposedEditFixture.Given(Chains, Positions(new[] { row[0] })),
                ComposedEditFixture.Given(Thickness, 0.25));

            Refused(envelope, Chains);
        }

        [Fact]
        public void TheChainsAreRequired()
        {
            Held(Row(0f, 1f));

            IDictionary<string, object> envelope = Physics(
                Operation(FitBodiesToChains), ComposedEditFixture.Given(Thickness, 0.25));

            Refused(envelope, Chains);
        }

        [Fact]
        public void ABoneThatAppearsTwiceIsRefused()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            Held(row);
            object[][] wrong =
            {
                new object[] { new object[] { 0, 1, 0 }, new object[] { 2, 3 } },
                new object[] { new object[] { 0, 1 }, new object[] { 2, 1 } },
            };
            foreach (object[] chains in wrong)
            {
                IDictionary<string, object> envelope = Physics(
                    Operation(FitBodiesToChains),
                    ComposedEditFixture.Given(Chains, chains),
                    ComposedEditFixture.Given(Thickness, 0.25));

                Refused(envelope, Chains);
            }
        }

        [Fact]
        public void ChainsThatAreNotListsOfPositionsAreRefused()
        {
            Held(Row(0f, 1f));
            object[] wrong =
            {
                0,
                "a",
                new object[] { 0, 1 },
                new object[] { new object[] { 0 }, new object[0] },
                new object[] { new object[] { 0 }, new object[] { "a" } },
                new object[] { new object[] { 0 }, new object[] { 0.5 } },
            };
            foreach (object chains in wrong)
            {
                IDictionary<string, object> envelope = Physics(
                    Operation(FitBodiesToChains),
                    ComposedEditFixture.Given(Chains, chains),
                    ComposedEditFixture.Given(Thickness, 0.25));

                Refused(envelope, Chains);
            }
        }

        [Fact]
        public void ABoneOutsideTheListIsRefused()
        {
            Held(Row(0f, 1f));

            IDictionary<string, object> envelope = Physics(
                Operation(FitBodiesToChains),
                ComposedEditFixture.Given(Chains, new object[] { new object[] { 0, 1 }, new object[] { 2, 9 } }),
                ComposedEditFixture.Given(Thickness, 0.25));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Contains(Chains, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheThicknessIsRequired()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            Held(row);

            Refused(Fit(row), Thickness);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void AThicknessThatIsNotAPositiveNumberIsRefused(double thickness)
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            Held(row);

            Refused(Fit(row, ComposedEditFixture.Given(Thickness, thickness)), Thickness);
        }

        [Fact]
        public void AThicknessThatIsNotANumberIsRefused()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            Held(row);

            Refused(Fit(row, ComposedEditFixture.Given(Thickness, "厚い")), Thickness);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public void ACoverThatIsNotAPositiveNumberIsRefused(double cover)
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            Held(row);

            Refused(
                Fit(row, ComposedEditFixture.Given(Thickness, 0.25), ComposedEditFixture.Given(Cover, cover)),
                Cover);
        }

        [Fact]
        public void AClosedThatIsNotTrueOrFalseIsRefused()
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            Held(row);

            Refused(
                Fit(row, ComposedEditFixture.Given(Thickness, 0.25), ComposedEditFixture.Given(Closed, "yes")),
                Closed);
        }

        [Theory]
        [InlineData("indices")]
        [InlineData("all")]
        [InlineData("selected")]
        public void PointingAtBonesBesideTheChainsIsRefused(string target)
        {
            IList<IList<IPXBone>> row = Row(0f, 1f);
            Held(row);
            object given = target == "indices" ? (object)new object[] { 0 } : true;

            IDictionary<string, object> envelope = Fit(
                row,
                ComposedEditFixture.Given(Thickness, 0.25),
                ComposedEditFixture.Given(target, given));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        [Theory]
        [InlineData(Chains)]
        [InlineData(Closed)]
        [InlineData(Cover)]
        [InlineData(Thickness)]
        public void TheChainInputsCannotBeGivenToAnotherOperation(string name)
        {
            Held(Row(0f, 1f));
            object given = name == Chains
                ? new object[] { new object[] { 0, 1 }, new object[] { 2, 3 } }
                : name == Closed ? (object)true : 1.0;

            IDictionary<string, object> envelope = Physics(
                Operation(ModelCreatePhysics.BodyPhysics),
                ComposedEditFixture.Given("indices", new object[] { 0 }),
                ComposedEditFixture.Given(ModelCreatePhysics.ShapeName, ModelCreatePhysics.Box),
                ComposedEditFixture.Given(name, given));

            Refused(envelope, name);
        }

        [Fact]
        public void AnotherOperationStillNeedsItsTargets()
        {
            Held(Row(0f, 1f));

            IDictionary<string, object> envelope = Physics(
                Operation(ModelCreatePhysics.BodyPhysics),
                ComposedEditFixture.Given(ModelCreatePhysics.ShapeName, ModelCreatePhysics.Box));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
        }

        private IList<IList<IPXBone>> Flared()
        {
            float[][] outward =
            {
                new[] { 1f, 0f },
                new[] { 0f, 1f },
                new[] { -1f, 0f },
                new[] { 0f, -1f },
            };
            List<IList<IPXBone>> ring = new List<IList<IPXBone>>();
            for (int chain = 0; chain < outward.Length; chain++)
            {
                IList<IPXBone> bones = Bones("裾" + chain + "-1", "裾" + chain + "-2");
                float x = outward[chain][0];
                float z = outward[chain][1];
                ((FakeBone)bones[0]).Position = new V3(2f * x, 0f, 2f * z);
                ((FakeBone)bones[1]).Position = new V3(3f * x, -2f, 3f * z);
                ((FakeBone)bones[1]).Parent = bones[0];
                ((FakeBone)bones[1]).ToOffset = new V3(x, -2f, z);
                ring.Add(bones);
            }

            return ring;
        }

        private IList<IList<IPXBone>> Row(params float[] xs)
        {
            List<IList<IPXBone>> row = new List<IList<IPXBone>>();
            for (int chain = 0; chain < xs.Length; chain++)
            {
                IList<IPXBone> bones = Bones("列" + chain + "-1", "列" + chain + "-2");
                ((FakeBone)bones[0]).Position = new V3(xs[chain], 0f, 0f);
                ((FakeBone)bones[1]).Position = new V3(xs[chain], -2f, 0f);
                ((FakeBone)bones[1]).Parent = bones[0];
                ((FakeBone)bones[1]).ToOffset = new V3(0f, -2f, 0f);
                row.Add(new List<IPXBone>(bones));
            }

            return row;
        }

        private IList<FakeBody> Held(IList<IList<IPXBone>> chains)
        {
            List<FakeBody> made = new List<FakeBody>();
            foreach (IPXBone bone in chains.SelectMany(chain => chain))
            {
                FakeBody body = new FakeBody(bone.Name)
                {
                    Bone = bone,
                    BoxKind = BodyBoxKind.Sphere,
                    BoxSize = new V3(0.5f, 0.5f, 0.5f),
                    Position = Vectors.Copied(bone.Position),
                    Rotation = new V3(0.1f, 0.2f, 0.3f),
                };
                _fixture.Model.Body.Add(body);
                _placed[body] = Vectors.Copied(bone.Position);
                made.Add(body);
            }

            return made;
        }

        private void Kept(FakeBody body)
        {
            IPXBody now = Now(body);
            Assert.Equal(BodyBoxKind.Sphere, now.BoxKind);
            NearAll(new V3(0.5f, 0.5f, 0.5f), now.BoxSize);
            NearAll(new V3(0.1f, 0.2f, 0.3f), now.Rotation);
            NearAll(_placed[body], now.Position);
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

        private object[] Positions(IEnumerable<IList<IPXBone>> chains)
        {
            return chains
                .Select(chain => (object)chain
                    .Select(bone => (object)_fixture.Model.Bone.IndexOf(bone))
                    .ToArray())
                .ToArray();
        }

        private IDictionary<string, object> Fit(
            IList<IList<IPXBone>> chains, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(FitBodiesToChains),
                ComposedEditFixture.Given(Chains, Positions(chains)),
            };
            all.AddRange(given);

            return Physics(all.ToArray());
        }

        private IDictionary<string, object> Physics(params KeyValuePair<string, object>[] given)
        {
            return _fixture.Call(ModelCreatePhysics.ToolName, ComposedEditFixture.Arguments(given));
        }

        private static KeyValuePair<string, object> Operation(string operation)
        {
            return ComposedEditFixture.Given(ComposedOperation.OperationName, operation);
        }

        private T Now<T>(T held)
            where T : class
        {
            return _fixture.Now(held);
        }

        private static void Refused(IDictionary<string, object> envelope, string name)
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
        }

        private static V3 Unit(float x, float y, float z)
        {
            float length = (float)Math.Sqrt((x * x) + (y * y) + (z * z));

            return new V3(x / length, y / length, z / length);
        }

        /// <summary>剛体の回転が、ローカルの軸 <paramref name="axis"/>(0 が X)を向ける先。</summary>
        private static void Axis(V3 wanted, IPXBody body, int axis)
        {
            V3 local = new V3(axis == 0 ? 1f : 0f, axis == 1 ? 1f : 0f, axis == 2 ? 1f : 0f);
            V3 found = RowMatrix.YawPitchRoll(body.Rotation.Y, body.Rotation.X, body.Rotation.Z)
                .Transform(local);
            NearAll(wanted, found);
        }

        private static void NearAll(V3 wanted, V3 found)
        {
            Near(wanted.X, found.X);
            Near(wanted.Y, found.Y);
            Near(wanted.Z, found.Z);
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
