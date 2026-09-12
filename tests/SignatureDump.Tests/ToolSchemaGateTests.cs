using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolSchemaGateTests
    {
        private const string Tool = "model_list_vertices";

        /// <summary>題材が使う綴り。想定文字数の表もこの並びから作る。</summary>
        private static readonly string[] Known = { "number", "text", "boolean" };

        /// <summary>綴りの並びに対応する想定文字数の表。値は照合に加わらない。</summary>
        private static IDictionary<string, int> Lengths(string[] spellings)
        {
            return spellings.ToDictionary(s => s, s => 1, StringComparer.Ordinal);
        }

        /// <summary>行を1件持つ能力対応表。ツールを持つ行かイベント行かを選べる。</summary>
        private static string MapJson(string eventType = null)
        {
            string row = eventType == null
                ? @"{ ""signatureKey"": ""T.M()"", ""editKind"": ""read"",
                      ""basis"": ""根拠。"",
                      ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                        ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }"
                : @"{ ""signatureKey"": ""T.E()"", ""editKind"": ""read"",
                      ""basis"": ""根拠。"",
                      ""eventType"": """ + eventType + @""" }";
            return @"{ ""rows"": [" + row + "] }";
        }

        private static string SchemaJson(string tool = Tool, string shape = "number", string extra = "")
        {
            return @"{ ""tools"": [{ ""tool"": """ + tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [] }],
                ""output"": { ""origin"": ""hostOutput"", ""shape"": """ + shape + @""" }"
                + extra + "}] }";
        }

        /// <summary>一覧の応答。総数と、要素を並べた切り出しを返す。</summary>
        private const string ListingOutput = @"{ ""origin"": ""hostOutput"", ""members"": [
                  { ""name"": ""total"", ""origin"": ""hostOutput"", ""shape"": ""number"" },
                  { ""name"": ""items"", ""origin"": ""hostOutput"",
                    ""element"": { ""origin"": ""hostOutput"", ""members"": [
                      { ""name"": ""index"", ""origin"": ""hostOutput"",
                        ""shape"": ""number"" }] } }] }";

        /// <summary>
        /// `limit` を受け取る形。呼び分けを2つ持ち、応答と分岐ごとの `limit` を差し替えられる。
        /// </summary>
        private static string LimitSchemaJson(
            string first, string second = "", string output = ListingOutput)
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [
                  { ""branch"": ""first"", ""inputs"": [
                    { ""name"": ""offset"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": false, ""default"": 0,
                      ""bounds"": { ""minimum"": 0, ""maximum"": 100 } },
                    { ""name"": ""limit"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": false" + first + @" }] },
                  { ""branch"": ""second"", ""inputs"": [
                    { ""name"": ""limit"", ""origin"": ""hostInput"", ""shape"": ""number"",
                      ""required"": false" + second + @" }] }],
                ""output"": " + output + @" }] }";
        }

        /// <summary>ツールを持つ行とイベント行を1つずつ持つ能力対応表。</summary>
        private const string ToolAndEvent = @"{ ""rows"": [
  { ""signatureKey"": ""T.E()"", ""editKind"": ""read"", ""basis"": ""根拠。"", ""eventType"": ""view.click"" },
  { ""signatureKey"": ""T.M()"", ""editKind"": ""read"", ""basis"": ""根拠。"",
    ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
      ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }] }";

        private static void Require(
            string schemas, string map, params string[] spellings)
        {
            Require(None, schemas, map, spellings);
        }

        private static void Require(
            IDictionary<string, ComposedTool> composedTools,
            string schemas,
            string map,
            params string[] spellings)
        {
            ToolSchemaGate.Require(
                ToolSchemaJsonReader.Read(schemas),
                ToolMapJsonReader.Read(map),
                new HashSet<string>(
                    spellings.Length == 0 ? Known : spellings, StringComparer.Ordinal),
                Lengths(spellings.Length == 0 ? Known : spellings),
                composedTools);
        }

        [Fact]
        public void AcceptsAListingWhoseCountIsLeftToTheRule()
        {
            Require(LimitSchemaJson(@", ""bounds"": { ""minimum"": 1 }"), MapJson());
        }

        [Theory]
        [InlineData(@"{ ""origin"": ""hostOutput"", ""members"": [
            { ""name"": ""items"", ""origin"": ""hostOutput"",
              ""element"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }")]
        [InlineData(@"{ ""origin"": ""hostOutput"", ""members"": [
            { ""name"": ""total"", ""origin"": ""hostOutput"", ""shape"": ""number"" }] }")]
        [InlineData(@"{ ""origin"": ""hostOutput"", ""members"": [
            { ""name"": ""total"", ""origin"": ""hostOutput"", ""shape"": ""number"" },
            { ""name"": ""items"", ""origin"": ""hostOutput"", ""shape"": ""number"" }] }")]
        [InlineData(@"{ ""origin"": ""hostOutput"", ""members"": [
            { ""name"": ""total"", ""origin"": ""hostOutput"", ""shape"": ""number"" },
            { ""name"": ""events"", ""origin"": ""hostOutput"",
              ""element"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }")]
        public void AcceptsAWrittenCountOnSomethingThatIsNotAListing(string output)
        {
            string written = @", ""default"": 100, ""bounds"": { ""minimum"": 1, ""maximum"": 1000 }";

            Require(LimitSchemaJson(written, written, output), MapJson());
        }

        [Theory]
        [InlineData(@", ""default"": 100", true)]
        [InlineData(@", ""bounds"": { ""minimum"": 1, ""maximum"": 100 }", true)]
        [InlineData(@", ""default"": 100", false)]
        [InlineData(@", ""bounds"": { ""minimum"": 1, ""maximum"": 100 }", false)]
        public void RejectsAListingThatWritesTheCountTheRuleDerives(
            string limit, bool inTheFirstBranch)
        {
            string schemas = inTheFirstBranch
                ? LimitSchemaJson(limit)
                : LimitSchemaJson(string.Empty, limit);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(schemas, MapJson()));

            Assert.Contains(
                "一覧の件数は導く値なので既定と上限を持たない",
                error.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAComposedToolWhoseBranchingDoesNotMatchItsSchema()
        {
            IDictionary<string, ComposedTool> composed =
                new Dictionary<string, ComposedTool>(StringComparer.Ordinal)
                {
                    { "session_release_handle", new ComposedTool(true, "受け持つこと。") },
                };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    composed, SchemaJson("session_release_handle"), @"{ ""rows"": [] }"));

            Assert.Contains(
                "分岐の欄が入出力の形と合わない", error.Message, StringComparison.Ordinal);
        }

        /// <summary>合成ツールを1件も持たない表。</summary>
        private static IDictionary<string, ComposedTool> None
        {
            get { return new Dictionary<string, ComposedTool>(StringComparer.Ordinal); }
        }

        [Fact]
        public void RejectsAComposedToolItemWithoutAnOrigin()
        {
            IDictionary<string, ComposedTool> composed =
                new Dictionary<string, ComposedTool>(StringComparer.Ordinal)
                {
                    { "session_release_handle", new ComposedTool(false, "受け持つこと。") },
                };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    composed,
                    @"{ ""tools"": [{ ""tool"": ""session_release_handle"",
                        ""branches"": [{ ""branch"": ""only"", ""inputs"": [
                          { ""name"": ""handles"", ""required"": true,
                            ""element"": { ""origin"": ""hostInput"",
                              ""shape"": ""number"" } }] }],
                        ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" } }] }",
                    @"{ ""rows"": [] }"));

            Assert.Contains(
                "合成ツールの項目が出所を書いていない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsASpellingWhoseAssumedLengthIsMissing()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolSchemaGate.Require(
                    ToolSchemaJsonReader.Read(SchemaJson()),
                    ToolMapJsonReader.Read(MapJson()),
                    new HashSet<string>(Known, StringComparer.Ordinal),
                    Lengths(new[] { "number", "text" }),
                    None));

            Assert.Contains("想定文字数を持たない綴り", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAnAssumedLengthForASpellingTheDocumentDoesNotHave()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolSchemaGate.Require(
                    ToolSchemaJsonReader.Read(SchemaJson()),
                    ToolMapJsonReader.Read(MapJson()),
                    new HashSet<string>(new[] { "number" }, StringComparer.Ordinal),
                    Lengths(Known),
                    None));

            Assert.Contains("綴りの表に無い想定文字数", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsASpellingThatTheDocumentDoesNotHave()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(SchemaJson(shape: "number"), MapJson(), "text"));

            Assert.Contains("表現の綴りが仕様書に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ReachesTheSpellingOfANestedItem()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Require(
                SchemaJson(shape: "number", extra: @", ""payloads"": [{ ""type"": ""view.click"",
                    ""members"": [{ ""name"": ""x"", ""origin"": ""hostOutput"",
                      ""element"": { ""origin"": ""hostOutput"", ""shape"": ""date"" } }] }]"),
                ToolAndEvent,
                "number"));

            Assert.Contains("表現の綴りが仕様書に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AcceptsEventBranchesThatMatchTheMap()
        {
            Require(
                SchemaJson(extra: @", ""payloads"": [{ ""type"": ""view.click"", ""members"": [] }]"),
                ToolAndEvent);
        }

        [Fact]
        public void RejectsEventBranchesSpreadOverTwoTools()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    @"{ ""tools"": [
                      { ""tool"": ""model_list_vertices"",
                        ""branches"": [{ ""branch"": ""only"", ""inputs"": [] }],
                        ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" },
                        ""payloads"": [{ ""type"": ""view.click"", ""members"": [] }] },
                      { ""tool"": ""view_poll_events"",
                        ""branches"": [{ ""branch"": ""only"", ""inputs"": [] }],
                        ""output"": { ""origin"": ""hostOutput"", ""shape"": ""number"" },
                        ""payloads"": [{ ""type"": ""view.move"", ""members"": [] }] }] }",
                    @"{ ""rows"": [
                      { ""signatureKey"": ""T.C()"", ""editKind"": ""read"",
                        ""basis"": ""根拠。"",
                        ""eventType"": ""view.click"" },
                      { ""signatureKey"": ""T.M()"", ""editKind"": ""read"",
                        ""basis"": ""根拠。"",
                        ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                          ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] },
                      { ""signatureKey"": ""T.V()"", ""editKind"": ""read"",
                        ""basis"": ""根拠。"",
                        ""eventType"": ""view.move"" },
                      { ""signatureKey"": ""T.W()"", ""editKind"": ""read"",
                        ""basis"": ""根拠。"",
                        ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                          ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }] }"));

            Assert.Contains("2つ以上ある", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsAnEventRowWithoutADescribedBranch()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(@"{ ""tools"": [] }", MapJson(eventType: "view.click")));

            Assert.Contains("イベント行の分岐の形が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsADescribedBranchWithoutAnEventRow()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    SchemaJson(extra: @", ""payloads"": [{ ""type"": ""view.click"",
                        ""members"": [] }]"),
                    MapJson()));

            Assert.Contains("イベント行の無い分岐", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsNullInputs()
        {
            ToolSchemaTable schemas = ToolSchemaJsonReader.Read(@"{ ""tools"": [] }");
            ToolMap map = ToolMapJsonReader.Read(@"{ ""rows"": [] }");
            HashSet<string> spellings = new HashSet<string>(StringComparer.Ordinal);

            IDictionary<string, int> lengths = Lengths(Known);

            IDictionary<string, ComposedTool> composed = None;

            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(null, map, spellings, lengths, composed));
            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(schemas, null, spellings, lengths, composed));
            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(schemas, map, null, lengths, composed));
            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(schemas, map, spellings, null, composed));
            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(schemas, map, spellings, lengths, null));
        }
    }
}
