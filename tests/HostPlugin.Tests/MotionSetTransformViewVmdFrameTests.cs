using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class MotionSetTransformViewVmdFrameTests : IDisposable
    {
        private const string Head = "^[^V]*Vocaloid Pose Data file";

        private const string BoneReg =
            "\\n+\\s*Bone(?<no>\\d+)\\s*\\{\\s*(?<name>[^\\r\\n]+)[\\r\\n]+\\s*(?<trans_x>[^,]+),(?<trans_y>[^,]+),(?<trans_z>[^;]+);[^\\n]*\\n+(?<rot_x>[^,]+),(?<rot_y>[^,]+),(?<rot_z>[^,]+),(?<rot_w>[^;]+);[^\\n]*\\n+\\s*\\}";

        private const string MorphReg =
            "\\n+\\s*Morph(?<no>\\d+)\\s*\\{\\s*(?<name>[^\\r\\n]+)[\\r\\n]+\\s*(?<val>[^;]+);[^\\n]*\\n+\\s*\\}";

        private readonly ComposedScreenFixture _fixture = new ComposedScreenFixture();

        private readonly Source _source = new Source();

        private readonly object _held = new object();

        private readonly int _motion;

        public MotionSetTransformViewVmdFrameTests()
        {
            _fixture.Model.Bone.Add(new FakeBone("センター"));
            _fixture.Model.Bone.Add(new FakeBone("右腕"));
            _fixture.Model.Bone.Add(new FakeBone(" 空白から"));
            _fixture.Model.Bone.Add(new FakeBone("改\r\n行"));
            _fixture.Model.Bone.Add(new FakeBone(string.Empty));
            _fixture.Model.Morph.Add(new FakeMorph("笑い"));
            _fixture.Model.Morph.Add(new FakeMorph("\t表情"));
            _fixture.TransformView.Visible = true;
            _fixture.Poses = _source;
            _motion = _fixture.Handles.Issue("PEPlugin.Vmd.IPEVmd", _held, () => { });
            _source.Answer = PoseRead.Found(
                new[]
                {
                    new BonePoseValue("センター", new[] { 1.5f, -2f, 0.25f }, new[] { 0f, 0.5f, 0f, 0.8660254f }),
                    new BonePoseValue("右腕", new[] { 0f, 0f, 0f }, new[] { 0f, 0f, 0f, 1f }),
                },
                new[] { new MorphValue("笑い", 0.75f) });
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheEditorIsToldToResetBeforeItIsGivenThePose()
        {
            IDictionary<string, object> envelope = Call(_motion, 30);

            Assert.True(ToolEnvelope.Succeeded(envelope), "成功でない。");
            Assert.Equal(new[] { "reset", "vpd" }, _fixture.TransformView.Calls);
        }

        [Fact]
        public void PosingTwiceTakesOneClone()
        {
            Call(_motion, 30);
            Call(_motion, 31);

            Assert.Equal(1, _fixture.Clones);
        }

        [Fact]
        public void TheFrameAndTheNumbersOfBonesAndMorphsSetAreReturned()
        {
            IDictionary<string, object> value = ComposedScreenFixture.Value(Call(_motion, 30));

            Assert.Equal(30, value["frame"]);
            Assert.Equal(2, value["bones"]);
            Assert.Equal(1, value["morphs"]);
        }

        [Fact]
        public void TheSourceIsGivenTheMotionTheFrameAndTheNamesTheModelCanTake()
        {
            Call(_motion, 3713);

            Assert.Same(_held, _source.Vmd);
            Assert.Equal(3713, _source.Frame);
            Assert.Equal(new[] { "センター", "右腕" }, _source.Bones.OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.Equal(new[] { "笑い" }, _source.Morphs.ToArray());
        }

        [Fact]
        public void AMorphTheEditorDropsBecauseAnEarlierMorphHasItsNameIsNotOfferedToTheSource()
        {
            _fixture.Model.Morph.Add(new FakeMorph("笑い"));
            _fixture.Model.Morph.Add(new FakeMorph("驚き"));

            Call(_motion, 3);

            Assert.Equal(new[] { "笑い" }, _source.Morphs.ToArray());
        }

        [Fact]
        public void EveryMorphOfAModelWhoseMorphNamesDoNotRepeatIsOfferedToTheSource()
        {
            _fixture.Model.Morph.Add(new FakeMorph("驚き"));

            Call(_motion, 3);

            Assert.Equal(new[] { "笑い", "驚き" }, _source.Morphs.OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void ThePoseIsGivenAsATextTheEditorReadsAsAVpd()
        {
            Call(_motion, 30);

            string text = Assert.Single(_fixture.TransformView.VpdTexts);
            Assert.Matches(new Regex(Head, RegexOptions.IgnoreCase), text);
            List<Match> bones = new Regex(BoneReg, RegexOptions.IgnoreCase).Matches(text).Cast<Match>().ToList();
            Assert.Equal(2, bones.Count);
            Assert.Equal("センター", bones[0].Groups["name"].Value);
            Assert.Equal(1.5f, ParsedSingle(bones[0], "trans_x"));
            Assert.Equal(-2f, ParsedSingle(bones[0], "trans_y"));
            Assert.Equal(0.25f, ParsedSingle(bones[0], "trans_z"));
            Assert.Equal(0f, ParsedSingle(bones[0], "rot_x"));
            Assert.Equal(0.5f, ParsedSingle(bones[0], "rot_y"));
            Assert.Equal(0f, ParsedSingle(bones[0], "rot_z"));
            Assert.Equal(0.8660254f, ParsedSingle(bones[0], "rot_w"));
            Assert.Equal("右腕", bones[1].Groups["name"].Value);
            List<Match> morphs = new Regex(MorphReg, RegexOptions.IgnoreCase).Matches(text).Cast<Match>().ToList();
            Match morph = Assert.Single(morphs);
            Assert.Equal("笑い", morph.Groups["name"].Value);
            Assert.Equal(0.75f, ParsedSingle(morph, "val"));
        }

        [Fact]
        public void AnArgumentTheToolDoesNotTakeIsRefused()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                MotionSetTransformViewVmdFrame.ToolName,
                ComposedScreenFixture.Arguments(
                    ComposedScreenFixture.Given("vmd", _motion),
                    ComposedScreenFixture.Given("frame", 0),
                    ComposedScreenFixture.Given("知らない項目", 1)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Theory]
        [InlineData("1")]
        [InlineData(1.5)]
        [InlineData(null)]
        public void AHandleThatIsNotAnIntegerIsRefused(object given)
        {
            IDictionary<string, object> envelope = Call(given, 0);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Fact]
        public void TheHandleIsRequired()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                MotionSetTransformViewVmdFrame.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("frame", 0)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1.5)]
        [InlineData("1")]
        [InlineData(2147483648.0)]
        [InlineData(null)]
        public void AFrameThatIsNotANonNegativeIntegerIsRefused(object frame)
        {
            IDictionary<string, object> envelope = Call(_motion, frame);

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Fact]
        public void TheFrameIsRequired()
        {
            IDictionary<string, object> envelope = _fixture.Call(
                MotionSetTransformViewVmdFrame.ToolName,
                ComposedScreenFixture.Arguments(ComposedScreenFixture.Given("vmd", _motion)));

            Assert.Equal(ToolEnvelope.InvalidArgument, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void AHandleTheLedgerDoesNotKnowIsRefusedWithoutReadingIt()
        {
            IDictionary<string, object> envelope = Call(_motion + 1000, 0);

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedScreenFixture.Code(envelope));
            Assert.Null(_source.Vmd);
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Fact]
        public void AHandleThatDoesNotHoldAMotionIsRefused()
        {
            _source.Answer = PoseRead.NotAMotion;

            IDictionary<string, object> envelope = Call(_motion, 0);

            Assert.Equal(ToolEnvelope.InvalidHandle, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Fact]
        public void ATransformViewThatIsNotOpenIsRefusedBeforeAnythingIsRead()
        {
            _fixture.TransformView.Visible = false;

            IDictionary<string, object> envelope = Call(_motion, 0);

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Null(_source.Vmd);
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Fact]
        public void AMotionWhoseNamesFitNoBoneOrMorphOfTheModelIsRefusedAndTheEditorIsLeftAlone()
        {
            _source.Answer = PoseRead.Found(new BonePoseValue[0], new MorphValue[0]);

            IDictionary<string, object> envelope = Call(_motion, 0);

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Fact]
        public void AValueThatIsNotFiniteIsRefusedAndTheEditorIsLeftAlone()
        {
            _source.Answer = PoseRead.Found(
                new[] { new BonePoseValue("センター", new[] { float.NaN, 0f, 0f }, new[] { 0f, 0f, 0f, 1f }) },
                new MorphValue[0]);

            IDictionary<string, object> envelope = Call(_motion, 0);

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Fact]
        public void AMorphValueThatIsNotFiniteIsRefusedAndTheEditorIsLeftAlone()
        {
            _source.Answer = PoseRead.Found(new BonePoseValue[0], new[] { new MorphValue("笑い", float.PositiveInfinity) });

            IDictionary<string, object> envelope = Call(_motion, 0);

            Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
            Assert.Empty(_fixture.TransformView.Calls);
        }

        [Fact]
        public void APoseTheEditorDoesNotTakeAsAVpdIsReportedAsAFailedOperation()
        {
            _fixture.TransformView.AcceptsVpd = false;

            IDictionary<string, object> envelope = Call(_motion, 0);

            Assert.Equal(ToolEnvelope.OperationFailed, ComposedScreenFixture.Code(envelope));
        }

        [Fact]
        public void ACultureWhoseDecimalSeparatorIsNotAPointIsRefusedAndTheEditorIsLeftAlone()
        {
            CultureInfo held = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                IDictionary<string, object> envelope = Call(_motion, 0);

                Assert.Equal(ToolEnvelope.NotApplicable, ComposedScreenFixture.Code(envelope));
                Assert.Empty(_fixture.TransformView.Calls);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = held;
            }
        }

        [Fact]
        public void TheNumbersAreWrittenInTheCultureTheEditorReadsThemIn()
        {
            CultureInfo held = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-GB");
                _source.Answer = PoseRead.Found(
                    new[] { new BonePoseValue("センター", new[] { 1234.5f, 0f, 0f }, new[] { 0f, 0f, 0f, 1f }) },
                    new MorphValue[0]);

                Call(_motion, 0);

                string text = Assert.Single(_fixture.TransformView.VpdTexts);
                Match bone = Assert.Single(new Regex(BoneReg, RegexOptions.IgnoreCase).Matches(text).Cast<Match>());
                Assert.Equal(1234.5f, float.Parse(bone.Groups["trans_x"].Value, CultureInfo.CurrentCulture));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = held;
            }
        }

        [Fact]
        public void TheSdkSourceReadsThePoseOfTheFrameFromTheKeysOfTheNamesWanted()
        {
            PoseVmd vmd = new PoseVmd();
            vmd.BoneNames[0] = "センター";
            vmd.BoneNames[1] = "モデルに無い骨";
            vmd.MorphNames[0] = "笑い";
            vmd.MorphNames[1] = "モデルに無い表情";
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 0, FrameIndex = 0 });
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 1, FrameIndex = 0, Translation = new PEPlugin.SDX.V3(9f, 9f, 9f) });
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 0, FrameIndex = 30, Translation = new PEPlugin.SDX.V3(0f, 0f, 30f) });
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 7, FrameIndex = 0 });
            vmd.Morph.Add(new PoseMorphKey { MorphIndex = 0, FrameIndex = 0, Value = 0f });
            vmd.Morph.Add(new PoseMorphKey { MorphIndex = 0, FrameIndex = 20, Value = 1f });
            vmd.Morph.Add(new PoseMorphKey { MorphIndex = 1, FrameIndex = 0, Value = 1f });

            PoseRead read = new SdkVmdPoseSource().Read(
                vmd, new HashSet<string> { "センター", "右腕" }, new HashSet<string> { "笑い" }, 15);

            Assert.False(read.IsNotAMotion);
            BonePoseValue bone = Assert.Single(read.Bones);
            Assert.Equal("センター", bone.Name);
            Assert.Equal(15f, bone.Translation[2], 4);
            MorphValue morph = Assert.Single(read.Morphs);
            Assert.Equal("笑い", morph.Name);
            Assert.Equal(0.75f, morph.Value, 5);
        }

        [Fact]
        public void TheSdkSourceTakesTheNamesFromTheNameTableAWholeFileFillsAndNotFromTheIndexTable()
        {
            PoseVmd vmd = new PoseVmd();
            vmd.BoneNames[3] = "右腕";
            vmd.MorphNames[5] = "笑い";
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 3, FrameIndex = 0, Translation = new PEPlugin.SDX.V3(1f, 2f, 3f) });
            vmd.Morph.Add(new PoseMorphKey { MorphIndex = 5, FrameIndex = 0, Value = 0.5f });

            PoseRead read = new SdkVmdPoseSource().Read(
                vmd, new HashSet<string> { "右腕" }, new HashSet<string> { "笑い" }, 0);

            Assert.Equal("右腕", Assert.Single(read.Bones).Name);
            Assert.Equal("笑い", Assert.Single(read.Morphs).Name);
        }

        [Fact]
        public void TheSdkSourceReadsAKeyAtFrameZeroListedAfterAnotherKeyOfTheSameBoneAsTheEditorDoes()
        {
            PoseVmd vmd = new PoseVmd();
            vmd.BoneNames[0] = "センター";
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 0, FrameIndex = 30, Translation = new PEPlugin.SDX.V3(0f, 0f, 30f) });
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 0, FrameIndex = 0 });

            BonePoseValue bone = Assert.Single(
                new SdkVmdPoseSource().Read(vmd, new HashSet<string> { "センター" }, new HashSet<string>(), 15).Bones);

            Assert.Equal(30f, bone.Translation[2], 4);
        }

        [Fact]
        public void TheSdkSourceReadsAKeyAtFrameZeroListedAfterAnotherKeyOfTheSameMorphAsTheEditorDoes()
        {
            PoseVmd vmd = new PoseVmd();
            vmd.MorphNames[0] = "笑い";
            vmd.Morph.Add(new PoseMorphKey { MorphIndex = 0, FrameIndex = 20, Value = 1f });
            vmd.Morph.Add(new PoseMorphKey { MorphIndex = 0, FrameIndex = 0, Value = 0f });

            MorphValue morph = Assert.Single(
                new SdkVmdPoseSource().Read(vmd, new HashSet<string>(), new HashSet<string> { "笑い" }, 10).Morphs);

            Assert.Equal(1f, morph.Value, 5);
        }

        [Fact]
        public void TheSdkSourceInterpolatesWithTheCurveOfTheLaterKey()
        {
            PoseVmd vmd = new PoseVmd();
            vmd.BoneNames[0] = "センター";
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 0, FrameIndex = 0 });
            vmd.Bone.Add(new PoseBoneKey
            {
                BoneIndex = 0,
                FrameIndex = 10,
                Translation = new PEPlugin.SDX.V3(10f, 10f, 10f),
                IplX = new PoseIpl { X1 = 100, Y1 = 0, X2 = 27, Y2 = 127 },
            });

            PoseRead read = new SdkVmdPoseSource().Read(
                vmd, new HashSet<string> { "センター" }, new HashSet<string>(), 2);

            BonePoseValue bone = Assert.Single(read.Bones);
            Assert.NotEqual(2f, bone.Translation[0], 1);
            Assert.Equal(2f, bone.Translation[1], 4);
        }

        [Fact]
        public void TheSdkSourceReadsTheRotationInTheOrderXyzwAndTheCurveOfEachComponent()
        {
            PoseVmd vmd = new PoseVmd();
            vmd.BoneNames[0] = "センター";
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 0, FrameIndex = 0 });
            vmd.Bone.Add(new PoseBoneKey
            {
                BoneIndex = 0,
                FrameIndex = 10,
                Translation = new PEPlugin.SDX.V3(10f, 10f, 10f),
                Rotation = new PEPlugin.SDX.Q(0f, (float)Math.Sqrt(0.5), 0f, (float)Math.Sqrt(0.5)),
                IplZ = new PoseIpl { X1 = 100, Y1 = 0, X2 = 27, Y2 = 127 },
                IplR = new PoseIpl { X1 = 100, Y1 = 0, X2 = 27, Y2 = 127 },
            });

            BonePoseValue bone = Assert.Single(
                new SdkVmdPoseSource().Read(vmd, new HashSet<string> { "センター" }, new HashSet<string>(), 2).Bones);

            Assert.Equal(2f, bone.Translation[0], 4);
            Assert.NotEqual(2f, bone.Translation[2], 1);
            Assert.Equal(0f, bone.Rotation[0], 5);
            Assert.Equal(0f, bone.Rotation[2], 5);
            Assert.True(bone.Rotation[1] > 0f && bone.Rotation[1] < (float)Math.Sqrt(0.5));
            double angle = (Math.PI / 2) * VmdFrameSampler.Curve(new IplCurve(100, 0, 27, 127), 0.2f);
            Assert.Equal(Math.Sin(angle / 2), bone.Rotation[1], 4);
            Assert.Equal(Math.Cos(angle / 2), bone.Rotation[3], 4);
        }

        [Fact]
        public void TheSdkSourceTakesAnObjectThatIsNotAMotionForOne()
        {
            PoseRead read = new SdkVmdPoseSource().Read(
                new object(), new HashSet<string>(), new HashSet<string>(), 0);

            Assert.True(read.IsNotAMotion);
        }

        [Fact]
        public void TheSdkSourceGivesNothingForAMotionWhoseNamesAreNotWanted()
        {
            PoseVmd vmd = new PoseVmd();
            vmd.BoneNames[0] = "別の骨";
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 0, FrameIndex = 0 });

            PoseRead read = new SdkVmdPoseSource().Read(
                vmd, new HashSet<string> { "センター" }, new HashSet<string>(), 0);

            Assert.False(read.IsNotAMotion);
            Assert.Empty(read.Bones);
            Assert.Empty(read.Morphs);
        }

        [Fact]
        public void TheRegisteredToolTakesTheMotionFromTheSdkSourceByDefault()
        {
            _fixture.Poses = new SdkVmdPoseSource();
            PoseVmd vmd = new PoseVmd();
            vmd.BoneNames[0] = "センター";
            vmd.Bone.Add(new PoseBoneKey { BoneIndex = 0, FrameIndex = 0, Translation = new PEPlugin.SDX.V3(1f, 2f, 3f) });
            int handle = _fixture.Handles.Issue("PEPlugin.Vmd.IPEVmd", vmd, () => { });

            IDictionary<string, object> value = ComposedScreenFixture.Value(Call(handle, 8));

            Assert.Equal(1, value["bones"]);
            Assert.Equal(0, value["morphs"]);
        }

        private static float ParsedSingle(Match match, string group)
        {
            return float.Parse(match.Groups[group].Value, CultureInfo.CurrentCulture);
        }

        private IDictionary<string, object> Call(object vmd, object frame)
        {
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>();
            if (vmd != null)
            {
                given.Add(ComposedScreenFixture.Given("vmd", vmd));
            }

            if (frame != null)
            {
                given.Add(ComposedScreenFixture.Given("frame", frame));
            }

            return _fixture.Call(
                MotionSetTransformViewVmdFrame.ToolName, ComposedScreenFixture.Arguments(given.ToArray()));
        }

        private sealed class Source : IVmdPoseSource
        {
            internal PoseRead Answer { get; set; }

            internal object Vmd { get; private set; }

            internal int Frame { get; private set; }

            internal ISet<string> Bones { get; private set; }

            internal ISet<string> Morphs { get; private set; }

            public PoseRead Read(object vmd, ISet<string> bones, ISet<string> morphs, int frame)
            {
                Vmd = vmd;
                Frame = frame;
                Bones = bones;
                Morphs = morphs;

                return Answer;
            }
        }
    }
}
