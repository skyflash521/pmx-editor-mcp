using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VmdFrameSamplerTests
    {
        private const double EasedAtOneFifth = 0.028642697259783745;

        private static readonly float[] Identity = { 0f, 0f, 0f, 1f };

        [Fact]
        public void AFrameBetweenTwoKeysOnTheLinearCurveIsMixedInProportion()
        {
            BonePoseValue pose = VmdFrameSampler.Bone(
                "骨", new[] { Key(0, 0f, 0f, 0f), Key(30, 30f, -60f, 90f) }, 10);

            Assert.Equal("骨", pose.Name);
            Assert.Equal(10f, pose.Translation[0], 4);
            Assert.Equal(-20f, pose.Translation[1], 4);
            Assert.Equal(30f, pose.Translation[2], 4);
        }

        [Fact]
        public void AFrameOnAKeyTakesTheValueOfThatKey()
        {
            BonePoseValue pose = VmdFrameSampler.Bone(
                "骨", new[] { Key(0, 0f, 0f, 0f), Key(30, 30f, 0f, 0f), Key(60, 90f, 0f, 0f) }, 30);

            Assert.Equal(30f, pose.Translation[0]);
        }

        [Fact]
        public void AFrameBeforeTheFirstKeyIsMixedFromTheInitialStateAtFrameZero()
        {
            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { Key(10, 5f, 6f, 7f), Key(20, 50f, 0f, 0f) }, 4);

            Assert.Equal(2f, pose.Translation[0], 4);
            Assert.Equal(2.4f, pose.Translation[1], 4);
            Assert.Equal(2.8f, pose.Translation[2], 4);
        }

        [Fact]
        public void FrameZeroWithoutAKeyIsTheInitialState()
        {
            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { Key(10, 5f, 6f, 7f) }, 0);

            Assert.Equal(new[] { 0f, 0f, 0f }, pose.Translation);
            Assert.Equal(new[] { 0f, 0f, 0f, 1f }, pose.Rotation);
        }

        [Fact]
        public void TheRotationBeforeTheFirstKeyTurnsFromNoRotationByTheCurveOfTheFirstKey()
        {
            float half = (float)Math.Sqrt(0.5);
            IplCurve eased = new IplCurve(100, 0, 27, 127);
            BoneKeySample first = new BoneKeySample(
                10,
                new[] { 0f, 0f, 0f },
                new[] { 0f, half, 0f, half },
                IplCurve.Linear,
                IplCurve.Linear,
                IplCurve.Linear,
                eased);

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first }, 2);

            double angle = (Math.PI / 2) * EasedAtOneFifth;
            Assert.Equal(Math.Sin(angle / 2), pose.Rotation[1], 3);
            Assert.Equal(Math.Cos(angle / 2), pose.Rotation[3], 3);
        }

        [Fact]
        public void AFrameAfterTheLastKeyTakesTheValueOfTheLastKey()
        {
            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { Key(0, 0f, 0f, 0f), Key(20, 50f, 0f, 0f) }, 3713);

            Assert.Equal(50f, pose.Translation[0]);
        }

        [Fact]
        public void KeysListedInFrameOrderAreMixedInProportion()
        {
            BonePoseValue pose = VmdFrameSampler.Bone(
                "骨", new[] { Key(0, 0f, 0f, 0f), Key(60, 90f, 0f, 0f), Key(30, 30f, 0f, 0f) }, 45);

            Assert.Equal(60f, pose.Translation[0], 4);
        }

        [Fact]
        public void AKeyAtFrameZeroListedAfterAnotherKeyLeavesTheFirstListedKeyHeldOverTheSpanBeforeIt()
        {
            BonePoseValue pose = VmdFrameSampler.Bone(
                "骨", new[] { Key(60, 90f, 0f, 0f), Key(0, 0f, 0f, 0f), Key(30, 30f, 0f, 0f) }, 45);
            BonePoseValue later = VmdFrameSampler.Bone(
                "骨", new[] { Key(60, 90f, 0f, 0f), Key(0, 0f, 0f, 0f), Key(30, 30f, 0f, 0f) }, 15);

            Assert.Equal(90f, pose.Translation[0], 4);
            Assert.Equal(15f, later.Translation[0], 4);
        }

        [Fact]
        public void AMorphKeyAtFrameZeroListedAfterAnotherKeyLeavesTheFirstListedKeyHeldOverTheSpanBeforeIt()
        {
            MorphValue value = VmdFrameSampler.Morph(
                "笑い", new[] { new MorphKeySample(20, 1f), new MorphKeySample(0, 0f) }, 10);

            Assert.Equal(1f, value.Value, 5);
        }

        [Fact]
        public void OfKeysOnTheSameFrameTheLaterOneInTheListIsUsed()
        {
            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { Key(5, 1f, 0f, 0f), Key(5, 2f, 0f, 0f) }, 5);
            BonePoseValue after = VmdFrameSampler.Bone("骨", new[] { Key(5, 1f, 0f, 0f), Key(5, 2f, 0f, 0f) }, 9);

            Assert.Equal(2f, pose.Translation[0]);
            Assert.Equal(2f, after.Translation[0]);
        }

        [Fact]
        public void NoKeysGiveNoPose()
        {
            Assert.Null(VmdFrameSampler.Bone("骨", new BoneKeySample[0], 0));
            Assert.Null(VmdFrameSampler.Morph("表情", new MorphKeySample[0], 0));
        }

        [Fact]
        public void TheCurveOfTheLaterKeyOfThePairShapesTheTranslation()
        {
            IplCurve eased = new IplCurve(100, 0, 27, 127);
            BoneKeySample first = new BoneKeySample(
                0, new[] { 0f, 0f, 0f }, Identity, eased, eased, eased, IplCurve.Linear);
            BoneKeySample last = new BoneKeySample(
                10, new[] { 10f, 10f, 10f }, Identity, eased, eased, eased, IplCurve.Linear);

            BonePoseValue early = VmdFrameSampler.Bone("骨", new[] { first, last }, 2);

            double wanted = 10 * EasedAtOneFifth;
            Assert.InRange(early.Translation[0], wanted - 1e-3, wanted + 1e-3);
            Assert.NotEqual(2f, early.Translation[0], 1);
        }

        [Fact]
        public void TheCurveOfTheEarlierKeyOfThePairIsNotUsed()
        {
            IplCurve eased = new IplCurve(100, 0, 27, 127);
            BoneKeySample first = new BoneKeySample(
                0, new[] { 0f, 0f, 0f }, Identity, eased, eased, eased, eased);
            BoneKeySample last = new BoneKeySample(
                10, new[] { 10f, 10f, 10f }, Identity, IplCurve.Linear, IplCurve.Linear, IplCurve.Linear, IplCurve.Linear);

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first, last }, 2);

            Assert.Equal(2f, pose.Translation[0], 4);
        }

        [Fact]
        public void EachAxisOfTheTranslationUsesItsOwnCurve()
        {
            IplCurve eased = new IplCurve(100, 0, 27, 127);
            BoneKeySample first = new BoneKeySample(
                0, new[] { 0f, 0f, 0f }, Identity, IplCurve.Linear, IplCurve.Linear, IplCurve.Linear, IplCurve.Linear);
            BoneKeySample last = new BoneKeySample(
                10,
                new[] { 10f, 10f, 10f },
                Identity,
                IplCurve.Linear,
                eased,
                IplCurve.Linear,
                IplCurve.Linear);

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first, last }, 2);

            Assert.Equal(2f, pose.Translation[0], 4);
            Assert.NotEqual(2f, pose.Translation[1], 1);
            Assert.Equal(2f, pose.Translation[2], 4);
        }

        [Theory]
        [InlineData(0.1f, 0.018258316f)]
        [InlineData(0.25f, 0.08845746f)]
        [InlineData(0.5f, 0.6796183f)]
        [InlineData(0.9f, 0.9887125f)]
        public void ACurveGivesTheValueTheEditorsIterationReachesAtTheGivenProgress(float at, float expected)
        {
            IplCurve curve = new IplCurve(90, 10, 20, 120);

            Assert.Equal(expected, VmdFrameSampler.Curve(curve, at), 6);
        }

        [Fact]
        public void ACurveKeepsItsEndsAndIsEvenAboutTheMiddleWhenItsControlPointsMirrorEachOther()
        {
            IplCurve curve = new IplCurve(30, 90, 97, 37);

            Assert.Equal(0f, VmdFrameSampler.Curve(curve, 0f), 6);
            Assert.Equal(1f, VmdFrameSampler.Curve(curve, 1f), 6);
            Assert.Equal(0.5f, VmdFrameSampler.Curve(curve, 0.5f), 6);
        }

        [Fact]
        public void TheRotationBetweenTwoKeysTurnsHalfWayAboutTheSameAxis()
        {
            float half = (float)Math.Sqrt(0.5);
            BoneKeySample first = Rotated(0, Identity);
            BoneKeySample last = Rotated(10, new[] { 0f, half, 0f, half });

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first, last }, 5);

            double quarter = Math.PI / 8;
            Assert.Equal(0d, pose.Rotation[0], 5);
            Assert.Equal(Math.Sin(quarter), pose.Rotation[1], 5);
            Assert.Equal(0d, pose.Rotation[2], 5);
            Assert.Equal(Math.Cos(quarter), pose.Rotation[3], 5);
        }

        [Fact]
        public void TheRotationTakesTheShorterWayRound()
        {
            float half = (float)Math.Sqrt(0.5);
            BoneKeySample first = Rotated(0, Identity);
            BoneKeySample last = Rotated(10, new[] { 0f, -half, 0f, -half });

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first, last }, 5);

            double quarter = Math.PI / 8;
            Assert.Equal(Math.Sin(quarter), pose.Rotation[1], 5);
            Assert.Equal(Math.Cos(quarter), pose.Rotation[3], 5);
        }

        [Fact]
        public void TheRotationCurveOfTheLaterKeyShapesTheRotation()
        {
            float half = (float)Math.Sqrt(0.5);
            IplCurve eased = new IplCurve(100, 0, 27, 127);
            BoneKeySample first = Rotated(0, Identity);
            BoneKeySample last = new BoneKeySample(
                10,
                new[] { 0f, 0f, 0f },
                new[] { 0f, half, 0f, half },
                IplCurve.Linear,
                IplCurve.Linear,
                IplCurve.Linear,
                eased);

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first, last }, 2);

            double angle = (Math.PI / 2) * EasedAtOneFifth;
            Assert.Equal(Math.Sin(angle / 2), pose.Rotation[1], 3);
            Assert.Equal(Math.Cos(angle / 2), pose.Rotation[3], 3);
        }

        [Fact]
        public void ARotationThatIsNotOfUnitLengthIsMixedWithTheWeightsOfTheEditorAndIsNotNormalized()
        {
            BoneKeySample first = Rotated(0, Identity);
            BoneKeySample last = Rotated(10, new[] { 0f, 0.5f, 0f, 0.5f });

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first, last }, 5);

            Assert.Equal(0f, pose.Rotation[0], 5);
            Assert.Equal(0.2886751f, pose.Rotation[1], 5);
            Assert.Equal(0f, pose.Rotation[2], 5);
            Assert.Equal(0.8660254f, pose.Rotation[3], 5);
        }

        [Fact]
        public void RotationsWhoseDotProductIsBeyondTheThresholdAreMixedLinearlyWithoutNormalizing()
        {
            BoneKeySample first = Rotated(0, new[] { 0f, 0f, 0f, 2f });
            BoneKeySample last = Rotated(10, new[] { 0f, 0f, 0f, 4f });

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first, last }, 5);

            Assert.Equal(new[] { 0f, 0f, 0f, 3f }, pose.Rotation);
        }

        [Fact]
        public void TheCurveIsSolvedByTheEditorsNewtonIterationInSinglePrecision()
        {
            BoneKeySample first = Rotated(0, Identity);
            BoneKeySample last = new BoneKeySample(
                10,
                new[] { 10f, 0f, 0f },
                Identity,
                new IplCurve(127, 0, 0, 127),
                IplCurve.Linear,
                IplCurve.Linear,
                IplCurve.Linear);

            BonePoseValue pose = VmdFrameSampler.Bone("骨", new[] { first, last }, 5);

            Assert.Equal(4.999892f, pose.Translation[0], 6);
        }

        [Fact]
        public void AMorphKeyAtFrameMinusOneIsMixedFromBecauseTheEditorDoublesTheFramesBeforeComparing()
        {
            MorphValue value = VmdFrameSampler.Morph(
                "笑い", new[] { new MorphKeySample(-1, 0f), new MorphKeySample(10, 1f) }, 5);

            Assert.Equal(0.5f, value.Value, 5);
        }

        [Fact]
        public void AMorphKeyWhoseFrameIsNegativeIsNotTakenForTheMarkOfNoKeyBefore()
        {
            MorphValue value = VmdFrameSampler.Morph(
                "笑い", new[] { new MorphKeySample(-5, 0f), new MorphKeySample(10, 1f) }, 5);

            Assert.Equal(0.5f, value.Value, 5);
        }

        [Fact]
        public void AMorphBetweenTwoKeysIsMixedInProportion()
        {
            MorphValue value = VmdFrameSampler.Morph(
                "笑い", new[] { new MorphKeySample(10, 0.2f), new MorphKeySample(20, 1f) }, 15);

            Assert.Equal("笑い", value.Name);
            Assert.Equal(0.6f, value.Value, 5);
        }

        [Fact]
        public void AMorphStartsFromZeroAtFrameZeroAndIsHeldAfterTheLastKey()
        {
            MorphKeySample[] keys = { new MorphKeySample(10, 0.2f), new MorphKeySample(20, 1f) };

            Assert.Equal(0f, VmdFrameSampler.Morph("笑い", keys, 0).Value);
            Assert.Equal(0.1f, VmdFrameSampler.Morph("笑い", keys, 5).Value, 5);
            Assert.Equal(1f, VmdFrameSampler.Morph("笑い", keys, 99).Value);
            Assert.Equal(0.2f, VmdFrameSampler.Morph("笑い", keys, 10).Value);
        }

        [Fact]
        public void TheKeysGivenAreNotChanged()
        {
            List<BoneKeySample> keys = new List<BoneKeySample> { Key(30, 30f, 0f, 0f), Key(0, 0f, 0f, 0f) };

            VmdFrameSampler.Bone("骨", keys, 30);

            Assert.Equal(new[] { 30, 0 }, new[] { keys[0].Frame, keys[1].Frame });
            Assert.Equal(30f, keys[0].Translation[0]);
        }

        private static BoneKeySample Key(int frame, float x, float y, float z)
        {
            return new BoneKeySample(
                frame,
                new[] { x, y, z },
                Identity,
                IplCurve.Linear,
                IplCurve.Linear,
                IplCurve.Linear,
                IplCurve.Linear);
        }

        private static BoneKeySample Rotated(int frame, float[] rotation)
        {
            return new BoneKeySample(
                frame,
                new[] { 0f, 0f, 0f },
                rotation,
                IplCurve.Linear,
                IplCurve.Linear,
                IplCurve.Linear,
                IplCurve.Linear);
        }
    }
}
