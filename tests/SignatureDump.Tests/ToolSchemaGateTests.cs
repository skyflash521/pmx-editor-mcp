using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolSchemaGateTests
    {
        private const string Tool = "model_list_vertices";

        /// <summary>題材の応答サイズ予算。値の枠はここから警告の枠を引いたものになる。</summary>
        private const int Budget = 100000;

        /// <summary>題材が使う綴り。想定文字数の表もこの並びから作る。</summary>
        private static readonly string[] Known = { "number", "text", "boolean" };

        private static readonly Dictionary<string, int> BySpelling =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "number", 11 },
                { "text", 256 },
                { "boolean", 5 },
            };

        /// <summary>綴りの並びに対応する想定文字数の表。表に無い綴りは知らない値とする。</summary>
        private static IDictionary<string, int> Lengths(string[] spellings)
        {
            return spellings.ToDictionary(
                s => s,
                s => BySpelling.ContainsKey(s) ? BySpelling[s] : 1,
                StringComparer.Ordinal);
        }

        /// <summary>ツールを1件持つ能力対応表。ツールの名前と分岐だけを差し替える。</summary>
        private static string MapJson(string tool = Tool, string eventType = null)
        {
            string row = eventType == null
                ? @"{ ""signatureKey"": ""T.M()"", ""capabilityIds"": [""CAP-001""],
                      ""rowKind"": ""directDispatch"", ""editKind"": ""read"",
                      ""direction"": ""read"", ""basis"": ""根拠。"", ""tool"": """ + tool + @""",
                      ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                        ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }"
                : @"{ ""signatureKey"": ""T.E()"", ""capabilityIds"": [""CAP-001""],
                      ""rowKind"": ""eventBranch"", ""editKind"": ""read"",
                      ""direction"": ""read"", ""basis"": ""根拠。"",
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

        /// <summary>ツールを割り当てた行とイベント行を1つずつ持つ能力対応表。</summary>
        private const string ToolAndEvent = @"{ ""rows"": [
  { ""signatureKey"": ""T.E()"", ""capabilityIds"": [""CAP-001""],
    ""rowKind"": ""eventBranch"", ""editKind"": ""read"", ""direction"": ""read"",
    ""basis"": ""根拠。"", ""eventType"": ""view.click"" },
  { ""signatureKey"": ""T.M()"", ""capabilityIds"": [""CAP-001""],
    ""rowKind"": ""directDispatch"", ""editKind"": ""read"", ""direction"": ""read"",
    ""basis"": ""根拠。"", ""tool"": """ + Tool + @""",
    ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
      ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] }] }";

        /// <summary>一覧を返すツール。要素は選び方の外の1項目と、選べる2項目を持つ。</summary>
        private static string ListingJson(int limitDefault, int limitMaximum)
        {
            return @"{ ""tools"": [{ ""tool"": """ + Tool + @""",
                ""branches"": [{ ""branch"": ""only"", ""inputs"": [] }],
                ""output"": { ""origin"": ""hostOutput"", ""members"": [
                  { ""name"": ""items"", ""origin"": ""hostOutput"", ""maxItems"": 100,
                    ""element"": { ""origin"": ""hostOutput"", ""members"": [
                      { ""name"": ""index"", ""origin"": ""hostOutput"", ""shape"": ""number"" },
                      { ""name"": ""name"", ""origin"": ""sdkReturn"", ""shape"": ""text"" },
                      { ""name"": ""flag"", ""origin"": ""sdkReturn"",
                        ""shape"": ""boolean"" }] } }] },
                ""listing"": { ""limitDefault"": " + limitDefault + @",
                               ""limitMaximum"": " + limitMaximum + @" } }] }";
        }

        private static void Require(
            string schemas, string map, params string[] spellings)
        {
            ToolSchemaGate.Require(
                ToolSchemaJsonReader.Read(schemas),
                ToolMapJsonReader.Read(map),
                new HashSet<string>(
                    spellings.Length == 0 ? Known : spellings, StringComparer.Ordinal),
                Lengths(spellings.Length == 0 ? Known : spellings),
                Budget);
        }

        [Fact]
        public void AcceptsATableThatCoversExactlyTheAssignedTools()
        {
            Require(SchemaJson(), MapJson());
        }

        [Fact]
        public void RejectsAnAssignedToolThatTheTableDoesNotDescribe()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(@"{ ""tools"": [] }", MapJson()));

            Assert.Contains("入出力の形が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RejectsADescribedToolThatNoRowAssigns()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(SchemaJson(), @"{ ""rows"": [] }"));

            Assert.Contains("どの行にも割り当てていない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AcceptsListingCountsThatTheAssumedLengthsDerive()
        {
            // 値の枠は 100,000 − 2,000。一覧応答の枠を引いた 97,000 を、選び方の外の 19 と
            // 選べる 264・13 で割る。
            Require(
                ListingJson(97000 / (19 + 264 + 13) / 2, 97000 / (19 + 13)),
                MapJson(),
                "number",
                "text",
                "boolean");
        }

        [Fact]
        public void RejectsListingCountsThatDoNotMatchTheDerivedOnes()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    ListingJson(100, 97000 / (19 + 13)), MapJson(), "number", "text", "boolean"));

            Assert.Contains("想定文字数から逆算した値と合わない", error.Message, StringComparison.Ordinal);

            Assert.Throws<InvalidOperationException>(
                () => Require(
                    ListingJson(97000 / (19 + 264 + 13) / 2, 97000 / (19 + 13) - 1),
                    MapJson(),
                    "number",
                    "text",
                    "boolean"));
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
                    Budget));

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
                    Budget));

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
                    ""members"": [{ ""name"": ""x"", ""origin"": ""sdkOut"",
                      ""element"": { ""origin"": ""sdkOut"", ""shape"": ""date"" },
                      ""maxItems"": 2 }] }]"),
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
                      { ""signatureKey"": ""T.C()"", ""capabilityIds"": [""CAP-001""],
                        ""rowKind"": ""eventBranch"", ""editKind"": ""read"",
                        ""direction"": ""read"", ""basis"": ""根拠。"",
                        ""eventType"": ""view.click"" },
                      { ""signatureKey"": ""T.M()"", ""capabilityIds"": [""CAP-001""],
                        ""rowKind"": ""directDispatch"", ""editKind"": ""read"",
                        ""direction"": ""read"", ""basis"": ""根拠。"",
                        ""tool"": """ + Tool + @""",
                        ""postcondition"": [{ ""effectType"": ""none"", ""effectKey"": """",
                          ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }] },
                      { ""signatureKey"": ""T.V()"", ""capabilityIds"": [""CAP-001""],
                        ""rowKind"": ""eventBranch"", ""editKind"": ""read"",
                        ""direction"": ""read"", ""basis"": ""根拠。"",
                        ""eventType"": ""view.move"" },
                      { ""signatureKey"": ""T.W()"", ""capabilityIds"": [""CAP-001""],
                        ""rowKind"": ""directDispatch"", ""editKind"": ""read"",
                        ""direction"": ""read"", ""basis"": ""根拠。"",
                        ""tool"": ""view_poll_events"",
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

            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(null, map, spellings, lengths, Budget));
            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(schemas, null, spellings, lengths, Budget));
            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(schemas, map, null, lengths, Budget));
            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(schemas, map, spellings, null, Budget));
        }
    }
}
