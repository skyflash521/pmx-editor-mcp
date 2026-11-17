using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelEditWeightsBlendAlongChainTests : IDisposable
    {
        private const string BlendAlongChain = "blendAlongChain";

        private const string Chains = "chains";

        private const int Digits = 4;

        private readonly ComposedEditFixture _fixture = new ComposedEditFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void AVertexAtTheMiddleOfASectionGoesWhollyToThatSectionsBone()
        {
            IList<IPXBone> bones = Chain();
            FakeVertex first = Placed(1f, 0f, 0f);
            FakeVertex second = Placed(4f, 0f, 0f);
            FakeVertex third = Placed(7f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1, 2, 3 })));

            Near(1.0, Share(first, bones[0]));
            Near(1.0, Share(second, bones[1]));
            Near(1.0, Share(third, bones[2]));
            Assert.Equal(2, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void ThePositionAlongABentChainIsTheDistanceTravelledAlongIt()
        {
            IList<IPXBone> bones = Bones("髪1", "髪2", "髪3");
            Place(bones[0], 0f, 0f, 0f);
            Place(bones[1], 2f, 0f, 0f);
            Place(bones[2], 2f, 4f, 0f);
            ((FakeBone)bones[2]).ToOffset = new V3(2f, 0f, 0f);
            Rooted(0f, 0f, 0f);
            FakeVertex bent = Placed(2f, 1f, 0f);

            ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near((4.0 - 3.0) / (4.0 - 1.0), Share(bent, bones[0]));
            Near((3.0 - 1.0) / (4.0 - 1.0), Share(bent, bones[1]));
        }

        [Fact]
        public void AVertexAtTheBorderOfSectionsIsSharedByTheDistanceToBothMiddles()
        {
            IList<IPXBone> bones = Chain();
            FakeVertex lower = Placed(2f, 0f, 0f);
            FakeVertex upper = Placed(6f, 0f, 0f);

            ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1, 2 })));

            Near((4.0 - 2.0) / (4.0 - 1.0), Share(lower, bones[0]));
            Near((2.0 - 1.0) / (4.0 - 1.0), Share(lower, bones[1]));
            Near((7.0 - 6.0) / (7.0 - 4.0), Share(upper, bones[1]));
            Near((6.0 - 4.0) / (7.0 - 4.0), Share(upper, bones[2]));
        }

        [Fact]
        public void AVertexAwayFromTheChainIsSharedByWhereItProjectsOntoTheChain()
        {
            IList<IPXBone> bones = Chain();
            FakeVertex off = Placed(3f, 1f, 0f);

            ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near((4.0 - 3.0) / (4.0 - 1.0), Share(off, bones[0]));
            Near((3.0 - 1.0) / (4.0 - 1.0), Share(off, bones[1]));
        }

        [Fact]
        public void AVertexBeyondEitherEndGoesWhollyToTheEndBone()
        {
            IList<IPXBone> bones = Chain();
            FakeVertex beforeRoot = Placed(-1f, 0f, 0f);
            FakeVertex nearRoot = Placed(0.5f, 0f, 0f);
            FakeVertex nearTip = Placed(7.5f, 0f, 0f);
            FakeVertex beyondTip = Placed(10f, 0f, 0f);

            ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1, 2, 3, 4 })));

            Near(1.0, Share(beforeRoot, bones[0]));
            Near(1.0, Share(nearRoot, bones[0]));
            Near(1.0, Share(nearTip, bones[2]));
            Near(1.0, Share(beyondTip, bones[2]));
        }

        [Fact]
        public void TheLastBoneEndsAtTheBoneItPointsTo()
        {
            IList<IPXBone> bones = Chain();
            FakeBone end = new FakeBone("髪先");
            end.Position = new V3(8f, 0f, 0f);
            _fixture.Model.Bone.Add(end);
            ((FakeBone)bones[2]).ToBone = end;
            ((FakeBone)bones[2]).ToOffset = new V3(100f, 0f, 0f);
            FakeVertex nearTip = Placed(7.5f, 0f, 0f);

            ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near(1.0, Share(nearTip, bones[2]));
            Near(0.0, Share(nearTip, bones[1]));
        }

        [Fact]
        public void AChainWithANonFinitePointIsNotChosen()
        {
            IList<IPXBone> bones = Chain();
            ((FakeBone)bones[2]).ToOffset = new V3(float.NaN, 0f, 0f);
            FakeVertex near = Placed(2f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near(1.0, Share(near, bones[0]));
            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AVertexTakesTheNearestOfSeveralChains()
        {
            IList<IPXBone> bones = Bones("髪1", "髪2", "髪3", "髪4");
            Place(bones[0], 0f, 0f, 0f);
            Place(bones[1], 2f, 0f, 0f);
            ((FakeBone)bones[1]).ToOffset = new V3(2f, 0f, 0f);
            Place(bones[2], 0f, 10f, 0f);
            Place(bones[3], 2f, 10f, 0f);
            ((FakeBone)bones[3]).ToOffset = new V3(2f, 0f, 0f);
            Rooted(0f, 0f, 0f);
            FakeVertex upper = Placed(2f, 8f, 0f);
            FakeVertex lower = Placed(2f, 2f, 0f);

            ComposedEditFixture.Value(
                Blend(
                    new object[] { new object[] { 0, 1 }, new object[] { 2, 3 } },
                    ComposedEditFixture.Given("indices", new object[] { 1, 2 })));

            Near(0.5, Share(upper, bones[2]));
            Near(0.5, Share(upper, bones[3]));
            Near(0.0, Share(upper, bones[0]));
            Near(0.0, Share(upper, bones[1]));
            Near(0.5, Share(lower, bones[0]));
            Near(0.5, Share(lower, bones[1]));
            Near(0.0, Share(lower, bones[2]));
            Near(0.0, Share(lower, bones[3]));
        }

        [Fact]
        public void TheResultIsWrittenAsBdef2OrBdef1()
        {
            IList<IPXBone> bones = Chain();
            FakeVertex whole = Placed(1f, 0f, 0f);
            FakeVertex shared = Placed(2f, 0f, 0f);
            Weigh(whole, bones[0], 0.25f, bones[1], 0.25f, bones[2], 0.5f);

            ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1, 2 })));

            IPXVertex wholeNow = _fixture.Now(whole);
            Assert.Same(_fixture.Now(bones[0]), wholeNow.Bone1);
            Near(1.0, wholeNow.Weight1);
            Assert.Null(wholeNow.Bone2);
            Assert.Null(wholeNow.Bone3);
            Assert.Null(wholeNow.Bone4);
            IPXVertex sharedNow = _fixture.Now(shared);
            Assert.NotNull(sharedNow.Bone2);
            Assert.Null(sharedNow.Bone3);
            Assert.Null(sharedNow.Bone4);
        }

        [Fact]
        public void TheSdefIsDropped()
        {
            IList<IPXBone> bones = Chain();
            FakeVertex shared = Placed(2f, 0f, 0f);
            Weigh(shared, bones[1], 0.5f, bones[2], 0.5f);
            shared.SDEF = true;
            shared.SDEF_C = new V3(1f, 2f, 3f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1 })));

            IPXVertex now = _fixture.Now(shared);
            Assert.False(now.SDEF);
            Assert.NotNull(now.Bone2);
            Near((4.0 - 2.0) / (4.0 - 1.0), Share(shared, bones[0]));
            Assert.Equal(1, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void AVertexAlreadyHoldingTheBlendIsNotCounted()
        {
            IList<IPXBone> bones = Chain();
            Placed(1f, 0f, 0f);

            IDictionary<string, object> value = ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1 })));

            Assert.Equal(0, value[ModelEditWeights.ChangedName]);
        }

        [Fact]
        public void OnlyThePickedVerticesAreChanged()
        {
            IList<IPXBone> bones = Chain();
            FakeVertex picked = Placed(4f, 0f, 0f);
            FakeVertex left = Placed(4f, 0f, 0f);

            ComposedEditFixture.Value(
                Blend(Along(0, 1, 2), ComposedEditFixture.Given("indices", new object[] { 1 })));

            Near(1.0, Share(picked, bones[1]));
            Near(1.0, Share(left, bones[0]));
        }

        [Fact]
        public void TheChainsAreRequired()
        {
            Chain();
            Placed(1f, 0f, 0f);

            IDictionary<string, object> envelope = Weights(
                Operation(BlendAlongChain),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Chains, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void NoChainOrAnEmptyChainIsRefused()
        {
            Chain();
            Placed(1f, 0f, 0f);
            object[] wrong =
            {
                new object[0],
                new object[] { new object[0] },
                new object[] { new object[] { 0, 1 }, new object[0] },
            };
            foreach (object chains in wrong)
            {
                IDictionary<string, object> envelope = Blend(
                    (object[])chains, ComposedEditFixture.Given("indices", new object[] { 1 }));

                Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
                Assert.Contains(Chains, ComposedEditFixture.Message(envelope));
            }
        }

        [Fact]
        public void ChainsThatAreNotListsOfPositionsAreRefusedAsInvalid()
        {
            Chain();
            Placed(1f, 0f, 0f);
            object[] wrong =
            {
                0,
                "a",
                new object[] { 0, 1 },
                new object[] { new object[] { "a" } },
                new object[] { new object[] { 0.5 } },
            };
            foreach (object chains in wrong)
            {
                IDictionary<string, object> envelope = Weights(
                    Operation(BlendAlongChain),
                    ComposedEditFixture.Given(Chains, chains),
                    ComposedEditFixture.Given("indices", new object[] { 1 }));

                Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
                Assert.Contains(Chains, ComposedEditFixture.Message(envelope));
            }
        }

        [Theory]
        [InlineData(3)]
        [InlineData(-1)]
        public void ABoneOutsideTheListIsRefused(int bone)
        {
            Chain();
            Placed(1f, 0f, 0f);

            IDictionary<string, object> envelope = Blend(
                Along(0, 1, bone), ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(envelope));
            Assert.Contains(Chains, ComposedEditFixture.Message(envelope));
        }

        [Fact]
        public void TheChainsCannotBeGivenToAnotherOperation()
        {
            Chain();
            Placed(1f, 0f, 0f);

            IDictionary<string, object> envelope = Weights(
                Operation(ModelEditWeights.Normalize),
                ComposedEditFixture.Given(Chains, Along(0, 1, 2)),
                ComposedEditFixture.Given("indices", new object[] { 1 }));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedEditFixture.Code(envelope));
            Assert.Contains(Chains, ComposedEditFixture.Message(envelope));
        }

        private IList<IPXBone> Chain()
        {
            IList<IPXBone> bones = Bones("髪1", "髪2", "髪3");
            Place(bones[0], 0f, 0f, 0f);
            Place(bones[1], 2f, 0f, 0f);
            Place(bones[2], 6f, 0f, 0f);
            ((FakeBone)bones[1]).Parent = bones[0];
            ((FakeBone)bones[2]).Parent = bones[1];
            ((FakeBone)bones[2]).ToOffset = new V3(2f, 0f, 0f);
            Rooted(0f, 0f, 0f);

            return bones;
        }

        private static object[] Along(params int[] bones)
        {
            object[] chain = new object[bones.Length];
            for (int at = 0; at < bones.Length; at++)
            {
                chain[at] = bones[at];
            }

            return new object[] { chain };
        }

        private IDictionary<string, object> Blend(
            object[] chains, params KeyValuePair<string, object>[] given)
        {
            List<KeyValuePair<string, object>> all = new List<KeyValuePair<string, object>>
            {
                Operation(BlendAlongChain),
                ComposedEditFixture.Given(Chains, chains),
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

        private static void Place(IPXBone bone, float x, float y, float z)
        {
            ((FakeBone)bone).Position = new V3(x, y, z);
        }

        private FakeVertex Rooted(float x, float y, float z)
        {
            FakeVertex made = new FakeVertex(x, y, z);
            made.Bone1 = _fixture.Model.Bone[0];
            made.Weight1 = 1f;
            _fixture.Model.Vertex.Add(made);

            return made;
        }

        private FakeVertex Placed(float x, float y, float z)
        {
            return Rooted(x, y, z);
        }

        private static void Weigh(
            FakeVertex weighed, IPXBone first, float firstShare, IPXBone second, float secondShare)
        {
            weighed.Bone1 = first;
            weighed.Weight1 = firstShare;
            weighed.Bone2 = second;
            weighed.Weight2 = secondShare;
        }

        private static void Weigh(
            FakeVertex weighed,
            IPXBone first,
            float firstShare,
            IPXBone second,
            float secondShare,
            IPXBone third,
            float thirdShare)
        {
            Weigh(weighed, first, firstShare, second, secondShare);
            weighed.Bone3 = third;
            weighed.Weight3 = thirdShare;
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
