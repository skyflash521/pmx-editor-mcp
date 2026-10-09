using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using PmxEditorMcp.Bridge;
using PmxEditorMcp.SignatureDump;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    /// <summary>語からツールを引くこと。</summary>
    public sealed class ToolSearchTests
    {
        private const int Budget = 10000;

        private static readonly ToolMatch.Entry[] Tools =
        {
            new ToolMatch.Entry("model_edit_materials", "材質を結合・分割・取り出しする。"),
            new ToolMatch.Entry("model_clone_face", "面を複製する。"),
            new ToolMatch.Entry("view_filter_display", "絞込み表示を切り替える。"),
        };

        [Fact]
        public void TheToolsWhoseNameOrDutyCarriesTheWordComeBackInTheOrderOfTheirNames()
        {
            Assert.Equal(
                new[] { "model_edit_materials" }, Names(ToolSearch.Answer(new[] { "分割" }, null, null, Tools, Budget)));
            Assert.Equal(
                new[] { "model_clone_face", "model_edit_materials" },
                Names(ToolSearch.Answer(new[] { "model_" }, null, null, Tools, Budget)));
        }

        [Fact]
        public void TheWordIsAlsoTriedAgainstTheNameWithTheWidthAndTheCaseSetAside()
        {
            Assert.Equal(
                new[] { "view_filter_display" },
                Names(ToolSearch.Answer(new[] { "VIEW_FILTER" }, null, null, Tools, Budget)));
        }

        [Fact]
        public void AWordThatIsMissingOrEmptyIsRefused()
        {
            Assert.Equal("TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer(null, null, null, Tools, Budget)));
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer(new[] { string.Empty }, null, null, Tools, Budget)));
        }

        [Fact]
        public void AnEmptyListOfWordsOrAnEmptyWordAmongThemIsRefused()
        {
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT",
                Code(ToolSearch.Answer(new string[0], null, null, Tools, Budget)));
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT",
                Code(ToolSearch.Answer(new[] { "分割", string.Empty }, null, null, Tools, Budget)));
        }

        [Fact]
        public void TheToolsWhoseNameOrDutyCarriesAnyOfTheWordsComeBackInTheOrderOfTheirNames()
        {
            JsonObject answer = ToolSearch.Answer(new[] { "絞込", "分割" }, null, null, Tools, Budget);

            Assert.Equal(new[] { "model_edit_materials", "view_filter_display" }, Names(answer));
            Assert.Equal(2, Total(answer));
        }

        [Fact]
        public void AToolThatCarriesSeveralOfTheWordsIsListedOnce()
        {
            JsonObject answer = ToolSearch.Answer(new[] { "model_", "分割", "材質" }, null, null, Tools, Budget);

            Assert.Equal(new[] { "model_clone_face", "model_edit_materials" }, Names(answer));
            Assert.Equal(2, Total(answer));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ACountOutsideTheBoundsIsRefused(int limit)
        {
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer(new[] { "model_" }, limit, null, Tools, Budget)));
        }

        [Fact]
        public void APositionBelowZeroOrPastTheHitsIsRefused()
        {
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer(new[] { "model_" }, null, -1, Tools, Budget)));
            Assert.Equal(
                "TOOL_INVALID_ARGUMENT", Code(ToolSearch.Answer(new[] { "model_" }, null, 3, Tools, Budget)));
        }

        [Fact]
        public void TheRestIsLeftBehindWithThePositionToTakeItFrom()
        {
            JsonObject answer = ToolSearch.Answer(new[] { "model_" }, 1, null, Tools, Budget);

            Assert.Equal(new[] { "model_clone_face" }, Names(answer));
            Assert.Equal(2, Total(answer));
            Assert.Equal(1, Next(answer));
            Assert.Equal(new[] { "model_edit_materials" }, Names(ToolSearch.Answer(new[] { "model_" }, null, 1, Tools, Budget)));
        }

        [Fact]
        public void TheNamesStopAtTheRoomTheResponseHasEvenWhenTheCountWouldAllowMore()
        {
            List<ToolMatch.Entry> many = new List<ToolMatch.Entry>();
            for (int at = 0; at < 200; at++)
            {
                many.Add(new ToolMatch.Entry("model_" + new string('x', 200) + at, "説明。"));
            }

            JsonObject answer = ToolSearch.Answer(new[] { "model_" }, ToolSearch.MaximumLimit, null, many, 10000);

            Assert.True(Names(answer).Length < many.Count);
            Assert.Equal(Names(answer).Length, Next(answer));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => ToolMatch.Found(null, Tools));
            Assert.Throws<ArgumentNullException>(() => ToolMatch.Found(new[] { "model_" }, null));
            Assert.Throws<ArgumentNullException>(
                () => ToolSearch.Answer(new[] { "model_" }, null, null, null, Budget));
            Assert.Throws<ArgumentNullException>(() => new ToolMatch.Entry(null, "説明。"));
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
