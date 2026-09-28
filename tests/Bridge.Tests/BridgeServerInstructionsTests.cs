using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using PmxEditorMcp.SignatureDump;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>初期化でクライアントへ渡すサーバーの指示文が、ツールの実際の挙動と食い違わないこと。</summary>
    public sealed class BridgeServerInstructionsTests
    {
        private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(60);

        /// <summary>組み立てた定義の大半の名前が持つ語。当たりが件数の既定を超える。</summary>
        private const string CommonWord = "model_";

        [Fact]
        public async Task TheInstructionsDoNotSayTheToolSearchReturnsEveryHitWhenItStopsAtTheCount()
        {
            using CancellationTokenSource limit = new CancellationTokenSource(TestWait);
            await using McpClient client = await BridgeToolsTests.StartBridgeAsync(null, null, limit.Token);

            CallToolResult result = await client.CallToolAsync(
                FixedToolTable.FindToolName,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [FixedToolTable.FindToolTextParameter] = CommonWord,
                    ["limit"] = null,
                    ["offset"] = null,
                },
                cancellationToken: limit.Token);
            string said = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
            JsonObject value = JsonNode.Parse(said.Substring(said.IndexOf('{'))).AsObject();

            Assert.NotEqual(true, result.IsError);
            Assert.True(value["total"].GetValue<int>() > ToolSearch.DefaultLimit);
            Assert.Equal(ToolSearch.DefaultLimit, value["tools"].AsArray().Count);
            Assert.NotNull(value["nextOffset"]);

            string instructions = client.ServerInstructions;
            Assert.Contains(FixedToolTable.FindToolName, instructions);
            Assert.DoesNotContain("打ち切らず", instructions);
            Assert.True(
                instructions.Contains("nextOffset", StringComparison.Ordinal),
                "指示文は find_tool の続きの読み方に触れていない: " + instructions);
        }
    }
}
