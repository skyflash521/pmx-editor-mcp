using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using PmxEditorMcp.Bridge;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>語からツールを引くこと。</summary>
    public sealed class ToolSearchTests
    {
        private const int Budget = 10000;

        private static readonly ToolSearch.Entry[] Tools =
        {
            new ToolSearch.Entry("model_edit_materials", "材質を結合・分割・取り出しする。"),
            new ToolSearch.Entry("model_clone_face", "面を複製する。"),
            new ToolSearch.Entry("view_filter_display", "絞込み表示を切り替える。"),
        };

        [Fact]
        public void TheToolsWhoseNameOrDutyCarriesTheWordComeBackInTheOrderOfTheirNames()
        {
            Assert.Equal(
                new[] { "model_edit_materials" }, Names(ToolSearch.Answer("分割", null, null, Tools, Budget)));
            Assert.Equal(
                new[] { "model_clone_face", "model_edit_materials" },
                Names(ToolSearch.Answer("model_", null, null, Tools, Budget)));
        }

        [Fact]
        public void TheWordIsAlsoTriedAgainstTheNameWithTheWidthAndTheCaseSetAside()
        {
            Assert.Equal(
                new[] { "view_filter_display" },
                Names(ToolSearch.Answer("VIEW_FILTER", null, null, Tools, Budget)));
        }

        [Fact]
        public void AWordThatIsMissingOrEmptyIsRefused()
        {
            Assert.Equal("TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer(null, null, null, Tools, Budget)));
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer(string.Empty, null, null, Tools, Budget)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(501)]
        public void ACountOutsideTheBoundsIsRefused(int limit)
        {
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer("model_", limit, null, Tools, Budget)));
        }

        [Fact]
        public void APositionBelowZeroOrPastTheHitsIsRefused()
        {
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer("model_", null, -1, Tools, Budget)));
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer("model_", null, 3, Tools, Budget)));
        }

        [Fact]
        public void TheRestIsLeftBehindWithThePositionToTakeItFrom()
        {
            JsonObject answer = ToolSearch.Answer("model_", 1, null, Tools, Budget);

            Assert.Equal(new[] { "model_clone_face" }, Names(answer));
            Assert.Equal(2, Total(answer));
            Assert.Equal(1, Next(answer));
            Assert.Equal(new[] { "model_edit_materials" }, Names(ToolSearch.Answer("model_", null, 1, Tools, Budget)));
        }

        [Fact]
        public void TheNamesStopAtTheRoomTheResponseHasEvenWhenTheCountWouldAllowMore()
        {
            List<ToolSearch.Entry> many = new List<ToolSearch.Entry>();
            for (int at = 0; at < 200; at++)
            {
                many.Add(new ToolSearch.Entry("model_" + new string('x', 200) + at, "説明。"));
            }

            JsonObject answer = ToolSearch.Answer("model_", ToolSearch.MaximumLimit, null, many, 10000);

            Assert.True(Names(answer).Length < many.Count);
            Assert.Equal(Names(answer).Length, Next(answer));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => ToolSearch.Found(null, Tools));
            Assert.Throws<ArgumentNullException>(() => ToolSearch.Found("model_", null));
            Assert.Throws<ArgumentNullException>(
                () => ToolSearch.Answer("model_", null, null, null, Budget));
            Assert.Throws<ArgumentNullException>(() => new ToolSearch.Entry(null, "説明。"));
        }

        private static string[] Names(JsonObject answer)
        {
            return ((JsonArray)answer["value"]["tools"])
                .Select(node => node.GetValue<string>())
                .ToArray();
        }

        private static int Total(JsonObject answer)
        {
            return answer["value"]["total"].GetValue<int>();
        }

        private static int Next(JsonObject answer)
        {
            return answer["value"]["nextOffset"].GetValue<int>();
        }

        private static string Code(JsonObject answer)
        {
            return answer["error"]["code"].GetValue<string>();
        }
    }
}
