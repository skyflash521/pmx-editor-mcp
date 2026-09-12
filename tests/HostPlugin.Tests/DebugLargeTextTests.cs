using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class DebugLargeTextTests : IDisposable
    {
        private const int Budget = 100000;

        private readonly string _root;

        private readonly HostLog _log;

        public DebugLargeTextTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-large-" + Guid.NewGuid().ToString("N"));
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

            DebugLargeText.AddTo(methods, false);

            McpMethod method;
            Assert.False(methods.TryGet(DebugLargeText.MethodName, out method));
        }

        [Fact]
        public void TheOpenEntryIsOnTheTable()
        {
            McpMethodTable methods = new McpMethodTable();

            DebugLargeText.AddTo(methods, true);

            McpMethod method;
            Assert.True(methods.TryGet(DebugLargeText.MethodName, out method));
        }

        [Fact]
        public void TheTableIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => DebugLargeText.AddTo(null, true));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1000)]
        [InlineData(Budget)]
        public void TheTextHasExactlyTheRequestedNumberOfCharacters(int chars)
        {
            string text = Assert.IsType<string>(DebugLargeText.Build(Context(chars)));

            Assert.Equal(chars, text.Length);
            Assert.Equal(new string(DebugLargeText.FillCharacter, chars), text);
        }

        [Fact]
        public void ARequestBeyondTheBudgetStops()
        {
            InvalidParamsException error = Assert.Throws<InvalidParamsException>(
                () => DebugLargeText.Build(Context(Budget + 1)));

            Assert.Contains("chars", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ANumberThatIsNotPositiveStops(int chars)
        {
            Assert.Throws<InvalidParamsException>(() => DebugLargeText.Build(Context(chars)));
        }

        [Theory]
        [InlineData("100")]
        [InlineData(true)]
        [InlineData(1.5)]
        public void AValueThatIsNotAnIntegerStops(object chars)
        {
            Assert.Throws<InvalidParamsException>(() => DebugLargeText.Build(Context(chars)));
        }

        [Fact]
        public void TheNumberOfCharactersIsRequired()
        {
            Assert.Throws<InvalidParamsException>(() => DebugLargeText.Build(new McpMethodContext(
                new Dictionary<string, object>(StringComparer.Ordinal),
                new StubUiInvoker(),
                Budget,
                new HandleLedger(_log, new HandleIdIssuer()),
                new EventQueue(new EventSequenceIssuer()))));
        }

        [Fact]
        public void TheContextIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => DebugLargeText.Build(null));
        }

        private McpMethodContext Context(object chars)
        {
            Dictionary<string, object> parameters =
                new Dictionary<string, object>(StringComparer.Ordinal) { { "chars", chars } };

            return new McpMethodContext(
                parameters, new StubUiInvoker(), Budget, new HandleLedger(_log, new HandleIdIssuer()), new EventQueue(new EventSequenceIssuer()));
        }

        private sealed class StubUiInvoker : IUiInvoker
        {
            public UiInvocation TryInvokeOnUi(Action action)
            {
                action();
                return UiInvocation.Done;
            }
        }
    }
}
