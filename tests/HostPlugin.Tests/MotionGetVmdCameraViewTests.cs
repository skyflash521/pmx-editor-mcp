using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class MotionGetVmdCameraViewTests : IDisposable
    {
        private readonly string _root;

        private readonly HostLog _log;

        private readonly HandleLedger _handles;

        private readonly object _held = new object();

        private readonly int _motion;

        public MotionGetVmdCameraViewTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-camera-view-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
            _handles = new HandleLedger(_log, new HandleIdIssuer());
            _motion = _handles.Issue("PEPlugin.Vmd.IPEVmd", _held, () => { });
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void TheCameraOfEachFrameIsReturnedInTheOrderTheFramesWereGiven()
        {
            Source source = new Source(CameraRead.Found(new[]
            {
                Sample(1f, 2f, 3f, 4f),
                Sample(5f, 6f, 7f, 8f),
            }));

            IDictionary<string, object> envelope = Call(
                source, Arguments(_motion, new object[] { 600, 30 }));

            Assert.True(ToolEnvelope.Succeeded(envelope), "成功でない。");
            object[] cameras = (object[])((IDictionary<string, object>)envelope["value"])["cameras"];
            Assert.Equal(2, cameras.Length);
            IDictionary<string, object> first = (IDictionary<string, object>)cameras[0];
            Assert.Equal(600, first["frame"]);
            Assert.Equal(new object[] { 1.0, 2.0, 3.0 }, (object[])first["position"]);
            Assert.Equal(new object[] { 2.0, 3.0, 4.0 }, (object[])first["target"]);
            Assert.Equal(new object[] { 3.0, 4.0, 5.0 }, (object[])first["upVector"]);
            Assert.Equal(4.0, first["fov"]);
            Assert.Equal(30, ((IDictionary<string, object>)cameras[1])["frame"]);
            Assert.Equal(new[] { 600, 30 }, source.Frames);
            Assert.Same(_held, source.Vmd);
        }

        [Fact]
        public void AMotionWithoutCameraKeysIsRefusedAsNotApplicable()
        {
            IDictionary<string, object> envelope = Call(
                new Source(CameraRead.None), Arguments(_motion, new object[] { 0 }));

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
        }

        [Fact]
        public void AHandleTheLedgerDoesNotKnowIsRefusedWithoutReadingIt()
        {
            Source source = new Source(CameraRead.None);

            IDictionary<string, object> envelope = Call(
                source, Arguments(_motion + 1000, new object[] { 0 }));

            Assert.Equal(ToolEnvelope.InvalidHandle, Code(envelope));
            Assert.Null(source.Frames);
        }

        [Fact]
        public void AHandleThatDoesNotHoldAMotionIsRefused()
        {
            Assert.Equal(
                ToolEnvelope.InvalidHandle,
                Code(Call(new Source(CameraRead.NotAMotion), Arguments(_motion, new object[] { 0 }))));
        }

        [Fact]
        public void TheHandleIsRequired()
        {
            Dictionary<string, object> arguments = Arguments(_motion, new object[] { 0 });
            arguments.Remove("vmd");

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Call(new Source(CameraRead.None), arguments)));
        }

        [Fact]
        public void AHandleThatIsNotAnIntegerIsRefused()
        {
            Dictionary<string, object> arguments = Arguments(_motion, new object[] { 0 });
            arguments["vmd"] = "1";

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Call(new Source(CameraRead.None), arguments)));
        }

        [Fact]
        public void TheFramesAreRequired()
        {
            Dictionary<string, object> arguments = Arguments(_motion, null);
            arguments.Remove("frames");

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Call(new Source(CameraRead.None), arguments)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void FramesThatAreNotNonNegativeIntegersAreRefused(int which)
        {
            object[][] wrong =
            {
                new object[0],
                new object[] { -1 },
                new object[] { 1.5 },
                new object[] { "1" },
                new object[] { 2147483648.0 },
            };

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Call(new Source(CameraRead.None), Arguments(_motion, wrong[which]))));
        }

        [Fact]
        public void TheLeastSizeOfACameraIsTheLengthOfItsSmallestItemWithItsSeparator()
        {
            Source source = new Source(CameraRead.Found(new[] { Sample(0f, 0f, 0f, 0f, 0f) }));

            IDictionary<string, object> envelope = Call(source, Arguments(_motion, new object[] { 0 }));

            object item = ((object[])((IDictionary<string, object>)envelope["value"])["cameras"])[0];
            Assert.Equal(
                MotionGetVmdCameraView.LeastCameraChars,
                new JavaScriptSerializer().Serialize(item).Length + 1);
        }

        [Fact]
        public void MoreFramesThanTheResponseCanHoldAreRefusedBeforeReading()
        {
            Source source = new Source(CameraRead.None);
            int most = ResponseSize.ValueChars(ResponseBudget.MinimumChars) / MotionGetVmdCameraView.LeastCameraChars;

            IDictionary<string, object> envelope = Call(
                source, Arguments(_motion, Frames(most + 1)), ResponseBudget.MinimumChars);

            Assert.Equal(ToolEnvelope.ResponseTooLarge, Code(envelope));
            Assert.Null(source.Frames);
        }

        [Fact]
        public void TheMostFramesThatTheResponseCanHoldAreAccepted()
        {
            int most = ResponseSize.ValueChars(ResponseBudget.MinimumChars) / MotionGetVmdCameraView.LeastCameraChars;
            Source source = new Source(CameraRead.Found(
                Enumerable.Range(0, most).Select(f => Sample(0f, 0f, 0f, 0f, 0f)).ToArray()));

            IDictionary<string, object> envelope = Call(
                source, Arguments(_motion, Frames(most)), ResponseBudget.MinimumChars);

            Assert.True(ToolEnvelope.Succeeded(envelope), "成功でない。");
        }

        [Fact]
        public void AnAnswerThatDoesNotFitTheResponseIsRefused()
        {
            int most = ResponseSize.ValueChars(ResponseBudget.MinimumChars) / MotionGetVmdCameraView.LeastCameraChars;
            Source source = new Source(CameraRead.Found(
                Enumerable.Range(0, most).Select(f => Sample(0.1f, 0.2f, 0.3f, 45.5f, 0.7f)).ToArray()));

            IDictionary<string, object> envelope = Call(
                source, Arguments(_motion, Frames(most)), ResponseBudget.MinimumChars);

            Assert.Equal(ToolEnvelope.ResponseTooLarge, Code(envelope));
        }

        [Fact]
        public void AnArgumentThatTheToolDoesNotTakeIsRefused()
        {
            Dictionary<string, object> arguments = Arguments(_motion, new object[] { 0 });
            arguments["知らない項目"] = 1;

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                Code(Call(new Source(CameraRead.None), arguments)));
        }

        [Fact]
        public void TheSdkIsReadThroughTheUiThread()
        {
            Source source = new Source(CameraRead.Found(new[] { Sample(0f, 0f, 0f, 45f) }));
            RecordingInvoker invoker = new RecordingInvoker();

            IDictionary<string, object> envelope = Call(
                source, Arguments(_motion, new object[] { 0 }), invoker);

            Assert.True(ToolEnvelope.Succeeded(envelope), "成功でない。");
            Assert.Equal(1, invoker.Calls);
            Assert.True(source.ReadInsideInvoker, "UIスレッドへの委譲の外で読んだ。");
        }

        [Fact]
        public void AFailureOfTheSdkIsReportedAsAFailedOperation()
        {
            Source source = new Source(new InvalidOperationException("題材の失敗"));

            IDictionary<string, object> envelope = Call(
                source, Arguments(_motion, new object[] { 0 }));

            Assert.Equal(ToolEnvelope.OperationFailed, Code(envelope));
        }

        [Fact]
        public void ARefusedDelegationIsReportedAsNotAccepted()
        {
            Source source = new Source(CameraRead.None);

            IDictionary<string, object> envelope = Call(
                source, Arguments(_motion, new object[] { 0 }), new DecliningInvoker());

            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
            Assert.Null(source.Frames);
        }

        [Fact]
        public void TheTableAndTheBuilderAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => MotionGetVmdCameraView.AddTo(null, () => null));
            Assert.Throws<ArgumentNullException>(
                () => MotionGetVmdCameraView.AddTo(new McpMethodTable(), (Func<object>)null));
        }

        private static CameraSample Sample(float x, float y, float z, float fov, float pad)
        {
            return new CameraSample(
                new[] { x, y, z }, new[] { x + pad, y + pad, z + pad }, new[] { x, y, z }, fov);
        }

        private static object[] Frames(int count)
        {
            return Enumerable.Repeat((object)0, count).ToArray();
        }

        private static CameraSample Sample(float x, float y, float z, float fov)
        {
            return new CameraSample(
                new[] { x, y, z }, new[] { x + 1f, y + 1f, z + 1f }, new[] { x + 2f, y + 2f, z + 2f }, fov);
        }

        private static Dictionary<string, object> Arguments(int vmd, object[] frames)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>(StringComparer.Ordinal);
            arguments["vmd"] = vmd;
            arguments["frames"] = frames;

            return arguments;
        }

        private static string Code(IDictionary<string, object> envelope)
        {
            return (string)((IDictionary<string, object>)envelope["error"])["code"];
        }

        private IDictionary<string, object> Call(
            Source source, IDictionary<string, object> arguments, int budget = 100000)
        {
            return Call(source, arguments, null, budget);
        }

        private IDictionary<string, object> Call(
            Source source, IDictionary<string, object> arguments, IUiInvoker invoker, int budget = 100000)
        {
            McpMethodTable methods = new McpMethodTable();
            MotionGetVmdCameraView.AddTo(methods, source);
            McpMethod method;
            Assert.True(methods.TryGet(MotionGetVmdCameraView.ToolName, out method), "登録されていない。");
            McpMethodContext context = new McpMethodContext(
                arguments,
                invoker ?? new InlineInvoker(),
                budget,
                _handles,
                new EventQueue(new EventSequenceIssuer()));

            return (IDictionary<string, object>)method(context);
        }

        private sealed class Source : ICameraSource
        {
            private readonly CameraRead _answer;

            private readonly Exception _failure;

            internal Source(CameraRead answer)
            {
                _answer = answer;
            }

            internal Source(Exception failure)
            {
                _failure = failure;
            }

            internal bool ReadInsideInvoker { get; private set; }

            internal object Vmd { get; private set; }

            internal int[] Frames { get; private set; }

            public CameraRead Read(object vmd, IList<int> frames)
            {
                Vmd = vmd;
                Frames = frames.ToArray();
                ReadInsideInvoker = RecordingInvoker.Inside;
                if (_failure != null)
                {
                    throw _failure;
                }

                return _answer;
            }
        }

        private sealed class RecordingInvoker : IUiInvoker
        {
            [ThreadStatic]
            private static bool _inside;

            internal static bool Inside
            {
                get { return _inside; }
            }

            internal int Calls { get; private set; }

            public UiInvocation TryInvokeOnUi(Action action)
            {
                Calls++;
                _inside = true;
                try
                {
                    action();
                }
                finally
                {
                    _inside = false;
                }

                return UiInvocation.Done;
            }
        }

        private sealed class DecliningInvoker : IUiInvoker
        {
            public UiInvocation TryInvokeOnUi(Action action)
            {
                return UiInvocation.Declined;
            }
        }
    }
}
