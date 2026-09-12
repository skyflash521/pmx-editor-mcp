using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>台帳が発行したハンドルを解放するツール。</summary>
    public sealed class HandleReleaseTests : IDisposable
    {
        private readonly string _root;

        private readonly HostLog _log;

        public HandleReleaseTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-release-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _log = new HostLog(Path.Combine(_root, "host.log"));
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
        public void ReleasingAnswersWithTheHandlesInTheOrderTheyWentAway()
        {
            HandleLedger handles = Ledger();
            int first = handles.Issue("題材", new object(), () => { });
            int second = handles.Issue("題材", new object(), () => { });
            int third = handles.Issue("題材", new object(), () => { });

            IDictionary<string, object> envelope = Call(
                handles, new object[] { third, first, second });

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
            Assert.Equal(new object[] { third, first, second }, (object[])envelope["value"]);
            Assert.Equal(0, handles.Count);
        }

        [Fact]
        public void ReleasingTakesTheOnesThatDependOnItTooAndCountsThemOnce()
        {
            HandleLedger handles = Ledger();
            int owner = handles.Issue("題材", new object(), () => { });
            int child = handles.Issue("題材", new object(), () => { }, new[] { owner });

            IDictionary<string, object> envelope = Call(handles, new object[] { child, owner });

            object[] invalidated = (object[])envelope["value"];
            Assert.Equal(2, invalidated.Length);
            Assert.Equal(child, invalidated[0]);
            Assert.Equal(0, handles.Count);
        }

        [Fact]
        public void AHandleTheLedgerDoesNotKnowIsRefusedWithoutReleasingAnything()
        {
            HandleLedger handles = Ledger();
            int held = handles.Issue("題材", new object(), () => { });

            IDictionary<string, object> envelope = Call(handles, new object[] { held, held + 99 });

            Assert.Equal(
                ToolEnvelope.InvalidHandle,
                (string)((IDictionary<string, object>)envelope["error"])["code"]);
            Assert.Equal(1, handles.Count);
        }

        [Fact]
        public void TheSameHandleTwiceIsRefused()
        {
            HandleLedger handles = Ledger();
            int held = handles.Issue("題材", new object(), () => { });

            IDictionary<string, object> envelope = Call(handles, new object[] { held, held });

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                (string)((IDictionary<string, object>)envelope["error"])["code"]);
            Assert.Equal(1, handles.Count);
        }

        [Fact]
        public void AnEmptyListIsRefused()
        {
            HandleLedger handles = Ledger();

            IDictionary<string, object> envelope = Call(handles, new object[0]);

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                (string)((IDictionary<string, object>)envelope["error"])["code"]);
        }

        [Fact]
        public void AnItemThatIsNotAWholeNumberIsRefused()
        {
            HandleLedger handles = Ledger();

            IDictionary<string, object> envelope = Call(handles, new object[] { 1.5 });

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                (string)((IDictionary<string, object>)envelope["error"])["code"]);
        }

        [Fact]
        public void AnArgumentThatTheToolDoesNotTakeIsRefused()
        {
            HandleLedger handles = Ledger();
            McpMethod method = Method();
            Dictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { HandleRelease.HandlesName, new object[] { 1 } },
                    { "知らない項目", 1 },
                };

            IDictionary<string, object> envelope = (IDictionary<string, object>)method(
                Context(handles, arguments));

            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                (string)((IDictionary<string, object>)envelope["error"])["code"]);
        }

        [Fact]
        public void TheTableIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => HandleRelease.AddTo(null));
        }

        private IDictionary<string, object> Call(HandleLedger handles, object[] given)
        {
            Dictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { HandleRelease.HandlesName, given },
                };

            return (IDictionary<string, object>)Method()(Context(handles, arguments));
        }

        private static McpMethod Method()
        {
            McpMethodTable methods = new McpMethodTable();
            HandleRelease.AddTo(methods);
            McpMethod method;
            Assert.True(methods.TryGet(HandleRelease.ToolName, out method), "登録されていない。");

            return method;
        }

        private static McpMethodContext Context(
            HandleLedger handles, IDictionary<string, object> arguments)
        {
            return new McpMethodContext(
                arguments,
                new InlineInvoker(),
                100000,
                handles,
                new EventQueue(new EventSequenceIssuer()));
        }

        private HandleLedger Ledger()
        {
            return new HandleLedger(_log, new HandleIdIssuer());
        }
    }
}
