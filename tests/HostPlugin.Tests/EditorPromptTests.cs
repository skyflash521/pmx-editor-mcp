using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class EditorPromptTests
    {
        [Fact]
        public void AShownPromptIsAnsweredWithItsWords()
        {
            IDictionary<string, object> value = Value(Call("確認: 保存しますか？"));

            Assert.Equal(true, value[EditorPrompt.ShownName]);
            Assert.Equal("確認: 保存しますか？", value[EditorPrompt.TextName]);
        }

        [Fact]
        public void NoPromptIsAnsweredAsNotShown()
        {
            IDictionary<string, object> value = Value(Call(null));

            Assert.Equal(false, value[EditorPrompt.ShownName]);
            Assert.False(value.ContainsKey(EditorPrompt.TextName));
        }

        [Fact]
        public void TheAnswerDoesNotWaitForTheUiThread()
        {
            McpMethodTable methods = new McpMethodTable();
            EditorPrompt.AddTo(methods, new FixedProbe("表示"));
            McpMethod method;
            Assert.True(methods.TryGet(EditorPrompt.ToolName, out method), "登録されていない。");

            IDictionary<string, object> envelope = (IDictionary<string, object>)method(
                Context(new RefusingInvoker()));

            Assert.True((bool)envelope["ok"], "包みが成功でない。");
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(
                () => EditorPrompt.AddTo(null, new FixedProbe(null)));
            Assert.Throws<ArgumentNullException>(
                () => EditorPrompt.AddTo(new McpMethodTable(), null));
        }

        private static IDictionary<string, object> Call(string shown)
        {
            McpMethodTable methods = new McpMethodTable();
            EditorPrompt.AddTo(methods, new FixedProbe(shown));
            McpMethod method;
            Assert.True(methods.TryGet(EditorPrompt.ToolName, out method), "登録されていない。");

            return (IDictionary<string, object>)method(Context(new InlineInvoker()));
        }

        private static IDictionary<string, object> Value(IDictionary<string, object> envelope)
        {
            Assert.True((bool)envelope["ok"], "包みが成功でない。");

            return (IDictionary<string, object>)envelope["value"];
        }

        private static McpMethodContext Context(IUiInvoker invoker)
        {
            return new McpMethodContext(
                new Dictionary<string, object>(StringComparer.Ordinal),
                invoker,
                100000,
                new HandleLedger(
                    new HostLog(System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "pmx-editor-mcp-prompt-" + Guid.NewGuid().ToString("N") + ".log")),
                    new HandleIdIssuer()),
                new EventQueue(new EventSequenceIssuer()));
        }

        private sealed class FixedProbe : IModalWindowProbe
        {
            private readonly string _shown;

            public FixedProbe(string shown)
            {
                _shown = shown;
            }

            public string TryDescribe()
            {
                return _shown;
            }
        }

        private sealed class RefusingInvoker : IUiInvoker
        {
            public UiInvocation TryInvokeOnUi(Action action)
            {
                throw new InvalidOperationException("UIスレッドへ委譲した。");
            }
        }
    }
}
