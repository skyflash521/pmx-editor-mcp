using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using PEPlugin.Form;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class PreconditionGateClosingTests : IDisposable
    {
        private const string ToolName = "session_close";

        private const string FormType = "PEPlugin.Form.IPEFormConnector";

        private const string ModulePath = @"C:\plugins\PmxEditorMcp.HostPlugin.dll";

        private readonly string _directory;

        private readonly HostLog _log;

        public PreconditionGateClosingTests()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-closing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _log = new HostLog(Path.Combine(_directory, "host.log"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>
        /// 取り消せる編集が0件でも、やり直せる編集が残っているなら閉じない。エディタが尋ねる状態
        /// (履歴を切り詰めたあとに取り消し切った、0件でない件数で保存したあとに取り消して0件へ戻した)と
        /// 尋ねない状態(保存したときの件数が0のまま取り消し切った)は、SDKからはどちらもこの形で読める。
        /// </summary>
        [Theory]
        [InlineData(3)]
        [InlineData(256)]
        public void ClosingWithSomethingToRedoIsRefused(int redoable)
        {
            FormDouble form = new FormDouble { UndoCount = 0, RedoCount = redoable };

            IDictionary<string, object> envelope = Call(form);

            Assert.True(form.Closed == 0, "エディタが確認のダイアログを出しうる状態で閉じた。");
            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
        }

        /// <summary>取り消せる編集もやり直せる編集も無ければ閉じる。</summary>
        [Fact]
        public void ClosingWithNoHistoryCloses()
        {
            FormDouble form = new FormDouble { UndoCount = 0, RedoCount = 0 };

            Call(form);

            Assert.Equal(1, form.Closed);
        }

        /// <summary>取り消せる編集が残っていれば閉じない。</summary>
        [Fact]
        public void ClosingWithSomethingToUndoIsRefused()
        {
            FormDouble form = new FormDouble { UndoCount = 2, RedoCount = 0 };

            IDictionary<string, object> envelope = Call(form);

            Assert.Equal(0, form.Closed);
            Assert.Equal(ToolEnvelope.NotApplicable, Code(envelope));
        }

        private static string Code(IDictionary<string, object> envelope)
        {
            Assert.False((bool)envelope["ok"], "閉じるのを断っていない。");

            return (string)((IDictionary<string, object>)envelope["error"])["code"];
        }

        private IDictionary<string, object> Call(FormDouble form)
        {
            IPEFormConnector connector = form.Connector;
            SdkRelayTable relay = GeneratedSdkRelay.Create();
            Dictionary<string, SdkReceiver> receivers = GeneratedSdkReceivers.Create();
            receivers[FormType] = connection => connector;
            ResidentConnection resident = ResidentConnection.Hold(
                new StubRunArgs(
                    new StubPluginHost(
                        new StubConnector(
                            new StubSystemConnector(
                                new StubCPluginRunArgs(new StubCPluginConnector())))),
                    ModulePath),
                _log);
            UndoSuppression undo = new UndoSuppression(_log);
            PmxSession current = new PmxSession(
                relay, receivers, resident, GeneratedSdkFlows.Current, GeneratedSdkFlows.Pmx, undo);

            McpMethodTable methods = new McpMethodTable();
            ToolDispatch.AddTo(
                methods,
                relay,
                receivers,
                GeneratedSdkLists.Create(),
                resident,
                current,
                new PmxSession(
                    relay, receivers, resident, GeneratedSdkFlows.Bridge, GeneratedSdkFlows.Pmx, undo),
                new UndoRecovery(undo, current.UndoLock),
                GeneratedTools.Calls(new List<string>()),
                GeneratedTools.Aggregations(new List<string>()),
                GeneratedTools.Elements(new List<string>()),
                GeneratedTools.Preconditions(),
                new StillModifierKeys(),
                new EventBindingTable(GeneratedTools.Attachments(), GeneratedTools.Payloads()),
                _log,
                new ScreenRefresh(() => null, () => connector),
                new ScreenTargets(() => null, () => connector),
                EditMeasure.ByRowKey());

            McpMethod method;
            Assert.True(methods.TryGet(ToolName, out method), "登録されていないツール: " + ToolName);

            return (IDictionary<string, object>)method(new McpMethodContext(
                new Dictionary<string, object>(StringComparer.Ordinal) { { ToolDispatch.ConfirmName, true } },
                new InlineInvoker(),
                100000,
                new HandleLedger(_log, new HandleIdIssuer()),
                new EventQueue(new EventSequenceIssuer())));
        }

        private sealed class FormDouble : RealProxy
        {
            public FormDouble()
                : base(typeof(IPEFormConnector))
            {
            }

            public IPEFormConnector Connector
            {
                get { return (IPEFormConnector)GetTransparentProxy(); }
            }

            public int UndoCount { get; set; }

            public int RedoCount { get; set; }

            public int Closed { get; private set; }

            public override IMessage Invoke(IMessage message)
            {
                IMethodCallMessage call = (IMethodCallMessage)message;
                switch (call.MethodName)
                {
                    case "get_UndoCount":
                        return new ReturnMessage(UndoCount, null, 0, call.LogicalCallContext, call);

                    case "get_RedoCount":
                        return new ReturnMessage(RedoCount, null, 0, call.LogicalCallContext, call);

                    case "Close":
                        Closed++;
                        return new ReturnMessage(null, null, 0, call.LogicalCallContext, call);

                    default:
                        return new ReturnMessage(new NotSupportedException(call.MethodName), call);
                }
            }
        }
    }
}
