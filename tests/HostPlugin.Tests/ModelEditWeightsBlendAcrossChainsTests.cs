using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditWeightsBlendAcrossChainsTests : IDisposable
    {
        private const string BlendAcrossChains = "blendAcrossChains";

        private const string Chains = "chains";

        private const string Closed = "closed";

        private const string Curve = "curve";

        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void AVertexOnAChainGoesOnlyToThatChain()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            FakeVertex on = Placed(2f, -1f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Blend(row, Picked(on)));

            Near(1.0, Share(on, row[1][0]));
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AVertexHalfwayBetweenTwoChainsGoesHalfToEach()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            FakeVertex halfway = Placed(1f, -1f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(halfway)));

            Near(0.5, Share(halfway, row[0][0]));
            Near(0.5, Share(halfway, row[1][0]));
        }

        [Fact]
        public void TheSharesRunOnAcrossTheMiddleBetweenTwoChains()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            FakeVertex before = Placed(0.99f, -1f, 0f);
            FakeVertex after = Placed(1.01f, -1f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(before, after)));

            Near(0.505, Share(before, row[0][0]));
            Near(0.495, Share(before, row[1][0]));
            Near(0.495, Share(after, row[0][0]));
            Near(0.505, Share(after, row[1][0]));
        }

        [Theory]
        [InlineData(null, 0.25)]
        [InlineData("linear", 0.25)]
        [InlineData("smooth", 0.15625)]
        public void TheCurveTurnsTheRatioAcrossIntoTheNeighboursShare(string curve, double share)
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            FakeVertex quarter = Placed(0.5f, -1f, 0f);
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>> { Picked(quarter) };
            if (curve != null)
            {
                given.Add(ComposedEditFixture.Given(Curve, curve));
            }

            ComposedEditFixture.Value(Blend(row, given.ToArray()));

            Near(share, Share(quarter, row[1][0]));
            Near(1.0 - share, Share(quarter, row[0][0]));
        }

        [Fact]
        public void AVertexBetweenTwoChainsAndTwoSectionsGoesToFourBonesByTheProducts()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            FakeVertex inside = Placed(0.5f, -2f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(inside)));

            Near(0.75 * 0.5, Share(inside, row[0][0]));
            Near(0.75 * 0.5, Share(inside, row[0][1]));
            Near(0.25 * 0.5, Share(inside, row[1][0]));
            Near(0.25 * 0.5, Share(inside, row[1][1]));
            IPXVertex now = _fixture.Now(inside);
            Assert.NotNull(now.Bone4);
        }

        [Theory]
        [InlineData(true, 0.55, 0.45)]
        [InlineData(false, 1.0, 0.0)]
        public void ClosingTheRingMakesTheLastAndTheFirstChainsNeighbours(bool closed, double first, double last)
        {
            IList<IList<IPXBone>> ring = Ring();
            FakeVertex between = Placed(1.1f, -1f, -0.9f);

            ComposedEditFixture.Value(
                Blend(ring, Picked(between), ComposedEditFixture.Given(Closed, closed)));

            Near(first, Share(between, ring[0][0]));
            Near(last, Share(between, ring[3][0]));
            Near(0.0, Share(between, ring[1][0]));
        }

        [Fact]
        public void TheNeighbourTakenIsTheOneWhoseChainLiesNearer()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            FakeVertex left = Placed(1.5f, -1f, 0f);
            FakeVertex right = Placed(2.5f, -1f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(left, right)));

            Near(0.25, Share(left, row[0][0]));
            Near(0.0, Share(left, row[2][0]));
            Near(0.25, Share(right, row[2][0]));
            Near(0.0, Share(right, row[0][0]));
        }

        [Fact]
        public void TheNearestChainIsTheOneWhoseLineLiesNearestNotWhoseRootDoes()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            ((FakeBone)row[2][1]).Position = new V3(0.2f, -2f, 0f);
            FakeVertex below = Placed(0.2f, -3f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(below)));

            Near(1.0, Share(below, row[2][1]));
            Near(0.0, ShareOfChain(below, row[0]));
            Near(0.0, ShareOfChain(below, row[1]));
        }

        [Fact]
        public void TheNeighbourIsTakenFromTheChainsBesideItInTheOrderGiven()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            ((FakeBone)row[2][1]).Position = new V3(0.75f, -2f, 0f);
            FakeVertex below = Placed(0.25f, -3f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(below)));

            Near(0.875, ShareOfChain(below, row[0]));
            Near(0.125, ShareOfChain(below, row[1]));
            Near(0.0, ShareOfChain(below, row[2]));
        }

        [Fact]
        public void TheRatioAcrossIsMeasuredBetweenTheNearestPointsOfTheTwoChains()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f);
            ((FakeBone)row[1][1]).Position = new V3(4f, -2f, 0f);
            ((FakeBone)row[1][1]).ToOffset = new V3(2f, -2f, 0f);
            FakeVertex lower = Placed(1.5f, -4f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(lower)));

            Near(1.0 - (5.625 / 19.125), ShareOfChain(lower, row[0]));
            Near(5.625 / 19.125, ShareOfChain(lower, row[1]));
        }

        [Fact]
        public void EachChainSharesItsPartAlongByItsOwnSections()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f);
            ((FakeBone)row[1][1]).Position = new V3(2f, -1f, 0f);
            ((FakeBone)row[1][1]).ToOffset = new V3(0f, -3f, 0f);
            FakeVertex halfway = Placed(1f, -2f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(halfway)));

            Near(0.5 * 0.5, Share(halfway, row[0][0]));
            Near(0.5 * 0.5, Share(halfway, row[0][1]));
            Near(0.5 * 0.25, Share(halfway, row[1][0]));
            Near(0.5 * 0.75, Share(halfway, row[1][1]));
        }

        [Fact]
        public void AVertexNearASingleChainGoesOnlyToThatChain()
        {
            IList<IList<IPXBone>> row = Row(0f);
            FakeVertex aside = Placed(1f, -2f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(aside)));

            Near(0.5, Share(aside, row[0][0]));
            Near(0.5, Share(aside, row[0][1]));
        }

        [Fact]
        public void AVertexWhoseNearestPointsMeetGoesOnlyToTheNearestChain()
        {
            IList<IList<IPXBone>> row = Row(0f, 0f);
            FakeVertex aside = Placed(1f, -1f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(aside)));

            Near(1.0, Share(aside, row[0][0]));
            Near(0.0, Share(aside, row[1][0]));
        }

        [Fact]
        public void TheSdefIsDropped()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            FakeVertex halfway = Placed(1f, -1f, 0f);
            halfway.Bone1 = row[0][0];
            halfway.Weight1 = 0.5f;
            halfway.Bone2 = row[1][0];
            halfway.Weight2 = 0.5f;
            halfway.SDEF = true;
            halfway.SDEF_C = new V3(1f, 2f, 3f);

            IDictionary<string, object> value = ComposedEditFixture.Value(Blend(row, Picked(halfway)));

            Assert.False(_fixture.Now(halfway).SDEF);
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void OnlyThePickedVerticesAreChanged()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f, 4f);
            FakeVertex picked = Placed(1f, -1f, 0f);
            FakeVertex left = Placed(1f, -1f, 0f);

            ComposedEditFixture.Value(Blend(row, Picked(picked)));

            Near(0.5, Share(picked, row[1][0]));
            Near(1.0, Share(left, _fixture.Model.Bone[0]));
        }

        [Fact]
        public void TheChainsAreRequired()
        {
            Row(0f, 2f);
            FakeVertex vertex = Placed(1f, -1f, 0f);

            IDictionary<string, object> envelope = Weights(Operation(BlendAcrossChains), Picked(vertex));

            Refused(envelope, Chains);
        }

        [Fact]
        public void ChainsThatAreNotListsOfPositionsAreRefused()
        {
            Row(0f, 2f);
            FakeVertex vertex = Placed(1f, -1f, 0f);
            object[] wrong =
            {
                0,
                new object[0],
                new object[] { 1, 2 },
                new object[] { new object[0] },
                new object[] { new object[] { "a" } },
            };
            foreach (object chains in wrong)
            {
                IDictionary<string, object> envelope = Weights(
                    Operation(BlendAcrossChains), ComposedEditFixture.Given(Chains, chains), Picked(vertex));

                Refused(envelope, Chains);
            }
        }

        [Fact]
        public void ACurveThatIsNotKnownIsRefused()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f);
            FakeVertex vertex = Placed(1f, -1f, 0f);

            Refused(Blend(row, Picked(vertex), ComposedEditFixture.Given(Curve, "steep")), Curve);
        }

        [Fact]
        public void AClosedThatIsNotTrueOrFalseIsRefused()
        {
            IList<IList<IPXBone>> row = Row(0f, 2f);
            FakeVertex vertex = Placed(1f, -1f, 0f);

            Refused(Blend(row, Picked(vertex), ComposedEditFixture.Given(Closed, "yes")), Closed);
        }

        [Theory]
        [InlineData("blendAlongChain", Closed)]
        [InlineData("blendAlongChain", Curve)]
        [InlineData("normalize", Closed)]
        [InlineData("normalize", Curve)]
        public void TheInputsAcrossCannotBeGivenToAnotherOperation(string operation, string name)
        {
            IList<IList<IPXBone>> row = Row(0f, 2f);
            FakeVertex vertex = Placed(1f, -1f, 0f);
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>
            {
                Operation(operation),
                Picked(vertex),
                ComposedEditFixture.Given(name, name == Closed ? (object)true : "linear"),
            };
            if (operation == "blendAlongChain")
            {
                given.Add(ComposedEditFixture.Given(Chains, Positions(row)));
            }

            Refused(Weights(given.ToArray()), name);
        }

        private IList<IList<IPXBone>> Row(params float[] xs)
        {
            List<IList<IPXBone>> row = new List<IList<IPXBone>>();
            for (int chain = 0; chain < xs.Length; chain++)
            {
                row.Add(Chain("列" + chain, new V3(xs[chain], 0f, 0f), new V3(0f, -2f, 0f)));
            }

            return row;
        }

        private IList<IList<IPXBone>> Ring()
        {
            V3[] roots =
            {
                new V3(2f, 0f, 0f),
                new V3(0f, 0f, 2f),
                new V3(-2f, 0f, 0f),
                new V3(0f, 0f, -2f),
            };

            return roots.Select((root, at) => Chain("裾" + at, root, new V3(0f, -2f, 0f))).ToList();
        }

        private IList<IPXBone> Chain(string name, V3 root, V3 step)
        {
            if (_fixture.Model.Bone.Count == 0)
            {
                _fixture.Model.Bone.Add(new FakeBone("センター"));
            }

            FakeBone upper = new FakeBone(name + "-1") { Position = root };
            FakeBone lower = new FakeBone(name + "-2")
            {
                Position = Vectors.Add(root, step),
                Parent = upper,
                ToOffset = step,
            };
            upper.Parent = _fixture.Model.Bone[0];
            _fixture.Model.Bone.Add(upper);
            _fixture.Model.Bone.Add(lower);

            return new List<IPXBone> { upper, lower };
        }

        private FakeVertex Placed(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z) { Bone1 = _fixture.Model.Bone[0], Weight1 = 1f };
            _fixture.Model.Vertex.Add(made);

            return made;
        }

        private KeyValuePair<string, object> Picked(params FakeVertex[] vertices)
        {
            return ComposedEditFixture.Given(
                "indices", vertices.Select(vertex => (object)_fixture.Model.Vertex.IndexOf(vertex)).ToArray());
        }

        private object[] Positions(IEnumerable<IList<IPXBone>> chains)
        {
            return chains
                .Select(chain => (object)chain.Select(bone => (object)_fixture.Model.Bone.IndexOf(bone)).ToArray())
                .ToArray();
        }

        private IDictionary<string, object> Blend(
            IList<IList<IPXBone>> chains, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(BlendAcrossChains),
                ComposedEditFixture.Given(Chains, Positions(chains)),
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

        private float ShareOfChain(IPXVertex given, IEnumerable<IPXBone> chain)
        {
            return chain.Sum(bone => Share(given, bone));
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

        private static void Refused(IDictionary<string, object> envelope, string name)
        {
            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(name, ComposedEditFixture.Message(envelope));
        }

        private static void Near(double wanted, double found)
        {
            Assert.Equal(wanted, found, Digits);
        }
    }
}
