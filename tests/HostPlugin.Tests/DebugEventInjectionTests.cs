using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class DebugEventInjectionTests : IDisposable
    {
        private readonly string _root;

        private readonly HostLog _log;

        public DebugEventInjectionTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-inject-" + Guid.NewGuid().ToString("N"));
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
        public void TheClosedEntryIsNotOnTheTable()
        {
            McpMethodTable methods = new McpMethodTable();

            DebugEventInjection.AddTo(methods, false);

            McpMethod method;
            Assert.False(methods.TryGet(DebugEventInjection.MethodName, out method));
        }

        [Fact]
        public void TheOpenEntryIsOnTheTable()
        {
            McpMethodTable methods = new McpMethodTable();

            DebugEventInjection.AddTo(methods, true);

            McpMethod method;
            Assert.True(methods.TryGet(DebugEventInjection.MethodName, out method));
        }

        [Fact]
        public void TheEventGoesThroughTheSameQueueAsRealOnes()
        {
            EventQueue events = new EventQueue();
            object payload = new Dictionary<string, object>(StringComparer.Ordinal) { { "x", 1 } };

            object result = DebugEventInjection.Enqueue(
                Context(events, "ui.model.mouse", 7, payload));

            EventDrainResult drained = events.Drain(EventQueue.DefaultLimit);
            QueuedEvent queued = Assert.Single(drained.Events);
            Assert.Equal("ui.model.mouse", queued.Type);
            Assert.Equal(7, queued.SourceHandle);
            Assert.Same(payload, queued.Payload);
            Assert.Equal(queued.Seq, Assert.IsType<Dictionary<string, object>>(result)["seq"]);
        }

        [Fact]
        public void ThePayloadMayBeLeftOut()
        {
            EventQueue events = new EventQueue();
            Dictionary<string, object> parameters =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "type", "ui.model.mouse" },
                    { "sourceHandle", 1 },
                };

            DebugEventInjection.Enqueue(new McpMethodContext(
                parameters, new StubUiInvoker(), 100000, new HandleLedger(_log), events));

            Assert.Null(Assert.Single(events.Drain(EventQueue.DefaultLimit).Events).Payload);
        }

        [Fact]
        public void EachInjectionTakesTheNextNumber()
        {
            EventQueue events = new EventQueue();

            DebugEventInjection.Enqueue(Context(events, "a", 1, null));
            object second = DebugEventInjection.Enqueue(Context(events, "b", 1, null));

            Assert.Equal(2L, Assert.IsType<Dictionary<string, object>>(second)["seq"]);
        }

        [Theory]
        [InlineData(null, 1)]
        [InlineData("", 1)]
        [InlineData("  ", 1)]
        public void ATypeThatIsNotTextStops(string type, int sourceHandle)
        {
            Rejects("type", type, sourceHandle);
        }

        [Fact]
        public void ATypeThatIsNotAStringStops()
        {
            EventQueue events = new EventQueue();
            Dictionary<string, object> parameters =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "type", 5 },
                    { "sourceHandle", 1 },
                };

            InvalidParamsException error = Assert.Throws<InvalidParamsException>(
                () => DebugEventInjection.Enqueue(new McpMethodContext(
                    parameters, new StubUiInvoker(), 100000, new HandleLedger(_log), events)));

            Assert.Contains("type", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ASourceHandleThatIsNotPositiveStops(int sourceHandle)
        {
            Rejects("sourceHandle", "ui.model.mouse", sourceHandle);
        }

        [Theory]
        [InlineData("seven")]
        [InlineData("7")]
        [InlineData(true)]
        [InlineData(7.5)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(null)]
        public void ASourceHandleThatIsNotAWholeNumberStops(object sourceHandle)
        {
            EventQueue events = new EventQueue();
            Dictionary<string, object> parameters =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "type", "ui.model.mouse" },
                    { "sourceHandle", sourceHandle },
                };

            InvalidParamsException error = Assert.Throws<InvalidParamsException>(
                () => DebugEventInjection.Enqueue(new McpMethodContext(
                    parameters, new StubUiInvoker(), 100000, new HandleLedger(_log), events)));

            Assert.Contains("sourceHandle", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASourceHandleBeyondTheRangeOfAnIntegerStops()
        {
            EventQueue events = new EventQueue();
            Dictionary<string, object> parameters =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "type", "ui.model.mouse" },
                    { "sourceHandle", (long)int.MaxValue + 1 },
                };

            Assert.Throws<InvalidParamsException>(() => DebugEventInjection.Enqueue(
                new McpMethodContext(
                    parameters, new StubUiInvoker(), 100000, new HandleLedger(_log), events)));
        }

        [Fact]
        public void ASourceHandleWrittenAsAWholeDecimalIsTaken()
        {
            EventQueue events = new EventQueue();
            Dictionary<string, object> parameters =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "type", "ui.model.mouse" },
                    { "sourceHandle", 7m },
                };

            DebugEventInjection.Enqueue(new McpMethodContext(
                parameters, new StubUiInvoker(), 100000, new HandleLedger(_log), events));

            Assert.Equal(7, Assert.Single(events.Drain(EventQueue.DefaultLimit).Events).SourceHandle);
        }

        [Fact]
        public void AMissingArgumentStops()
        {
            EventQueue events = new EventQueue();

            Assert.Throws<InvalidParamsException>(() => DebugEventInjection.Enqueue(
                new McpMethodContext(
                    new Dictionary<string, object>(StringComparer.Ordinal),
                    new StubUiInvoker(),
                    100000,
                    new HandleLedger(_log),
                    events)));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => DebugEventInjection.AddTo(null, true));
            Assert.Throws<ArgumentNullException>(() => DebugEventInjection.Enqueue(null));
        }

        /// <summary>名前は検査が打つ文字列そのものなので、定数と実装を揃えて変えても気づける。</summary>
        [Fact]
        public void TheMethodNameIsACallerContract()
        {
            Assert.Equal("debug_enqueue_event", DebugEventInjection.MethodName);
        }

        private void Rejects(string name, string type, int sourceHandle)
        {
            Dictionary<string, object> parameters =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "type", type },
                    { "sourceHandle", sourceHandle },
                };

            InvalidParamsException error = Assert.Throws<InvalidParamsException>(
                () => DebugEventInjection.Enqueue(new McpMethodContext(
                    parameters, new StubUiInvoker(), 100000, new HandleLedger(_log), new EventQueue())));

            Assert.Contains(name, error.Message, StringComparison.Ordinal);
        }

        private McpMethodContext Context(
            EventQueue events, string type, int sourceHandle, object payload)
        {
            Dictionary<string, object> parameters =
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { "type", type },
                    { "sourceHandle", sourceHandle },
                    { "payload", payload },
                };

            return new McpMethodContext(
                parameters, new StubUiInvoker(), 100000, new HandleLedger(_log), events);
        }

        private sealed class StubUiInvoker : IUiInvoker
        {
            public bool TryInvokeOnUi(Action action)
            {
                action();
                return true;
            }
        }
    }
}
