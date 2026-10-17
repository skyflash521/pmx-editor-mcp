using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>溜まったイベントを取り出すツール。</summary>
    public sealed class EventPollTests : IDisposable
    {
        private const int Budget = 100000;

        private readonly string _root;

        private readonly HostLog _log;

        public EventPollTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-poll-" + Guid.NewGuid().ToString("N"));
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
        public void EventsComeBackOldestFirstWithWhatTheQueueHeld()
        {
            EventQueue queue = Queue();
            queue.Enqueue("view_undo", 3, null);
            queue.Enqueue("view_redo", 3, Payload("x", 1));

            IDictionary<string, object> value = Value(Poll(queue, Arguments()));

            object[] events = (object[])value["events"];
            Assert.Equal(2, events.Length);
            Assert.Equal("view_undo", Member(events[0], "type"));
            Assert.Equal(3, Member(events[0], "sourceHandle"));
            Assert.Null(Member(events[0], "payload"));
            Assert.Equal("view_redo", Member(events[1], "type"));
            Assert.Equal(
                1, ((IDictionary<string, object>)Member(events[1], "payload"))["x"]);
            Assert.Equal(0, value["dropped"]);
            Assert.Equal(0, value["remaining"]);
        }

        [Fact]
        public void OnlyTheAskedForCountComesBackAndTheRestStays()
        {
            EventQueue queue = Queue();
            queue.Enqueue("view_undo", 1, null);
            queue.Enqueue("view_redo", 1, null);
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("limit", 1L);

            IDictionary<string, object> value = Value(Poll(queue, arguments));

            Assert.Single((object[])value["events"]);
            Assert.Equal(1, value["remaining"]);
        }

        [Fact]
        public void WhatTheQueueThrewAwayIsToldOnceAndThenForgotten()
        {
            EventQueue queue = Queue();
            for (int at = 0; at < EventQueue.Capacity + 2; at++)
            {
                queue.Enqueue("view_undo", 1, null);
            }

            Assert.Equal(2, Value(Poll(queue, Arguments()))["dropped"]);
            Assert.Equal(0, Value(Poll(queue, Arguments()))["dropped"]);
        }

        [Fact]
        public void AnEventThatDoesNotFitOnItsOwnIsThrownAwayAndCounted()
        {
            EventQueue queue = Queue();
            queue.Enqueue("view_redo", 1, Payload("text", new string('あ', 9000)));
            queue.Enqueue("view_undo", 1, null);

            IDictionary<string, object> value = Value(Poll(queue, Arguments(), ResponseBudget.MinimumChars));

            object[] events = (object[])value["events"];
            Assert.Equal("view_undo", Member(Assert.Single(events), "type"));
            Assert.Equal(1, value["dropped"]);
        }

        /// <summary>
        /// 先に収めたイベントのあとで収まらなくなったイベントは、捨てずに列へ残し、次の取り出しで返る。
        /// </summary>
        [Fact]
        public void AnEventThatNoLongerFitsAfterOthersStaysForTheNextPoll()
        {
            EventQueue queue = Queue();
            queue.Enqueue("view_redo", 1, Payload("text", new string('a', 5000)));
            queue.Enqueue("view_undo", 2, Payload("text", new string('b', 5000)));
            queue.Enqueue("view_redo", 3, null);

            IDictionary<string, object> first = Value(Poll(queue, Arguments(), ResponseBudget.MinimumChars));

            Assert.Equal(1, Member(Assert.Single((object[])first["events"]), "sourceHandle"));
            Assert.Equal(0, first["dropped"]);
            Assert.Equal(2, first["remaining"]);

            IDictionary<string, object> second = Value(Poll(queue, Arguments(), ResponseBudget.MinimumChars));

            object[] rest = (object[])second["events"];
            Assert.Equal(2, rest.Length);
            Assert.Equal(2, Member(rest[0], "sourceHandle"));
            Assert.Equal(3, Member(rest[1], "sourceHandle"));
            Assert.Equal(0, second["dropped"]);
            Assert.Equal(0, second["remaining"]);
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(1001L)]
        [InlineData(1.5)]
        [InlineData("2")]
        public void ACountOutsideTheRangeIsRefused(object given)
        {
            IDictionary<string, object> arguments = Arguments();
            arguments.Add("limit", given);

            IDictionary<string, object> envelope = Poll(Queue(), arguments);

            Assert.False((bool)envelope["ok"]);
            Assert.Equal(
                ToolEnvelope.InvalidArgument,
                ((IDictionary<string, object>)envelope["error"])["code"]);
        }

        private static IDictionary<string, object> Payload(string name, object value)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { { name, value } };
        }

        private static object Member(object item, string name)
        {
            return ((IDictionary<string, object>)item)[name];
        }

        private static IDictionary<string, object> Value(IDictionary<string, object> envelope)
        {
            Assert.True((bool)envelope["ok"], "包みが成功でない。");

            return (IDictionary<string, object>)envelope["value"];
        }

        private static EventQueue Queue()
        {
            return new EventQueue(new EventSequenceIssuer());
        }

        private static IDictionary<string, object> Arguments()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private IDictionary<string, object> Poll(
            EventQueue queue, IDictionary<string, object> arguments, int budget = Budget)
        {
            McpMethodTable methods = new McpMethodTable();
            EventPoll.AddTo(methods);
            McpMethod method;
            Assert.True(methods.TryGet(EventPoll.ToolName, out method));

            return (IDictionary<string, object>)method(new McpMethodContext(
                arguments,
                new InlineInvoker(),
                budget,
                new HandleLedger(_log, new HandleIdIssuer()),
                queue));
        }
    }
}
