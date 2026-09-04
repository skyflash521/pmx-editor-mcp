using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolSchemaGateTests
    {
        private const string Tool = "model_list_vertices";

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

        private static void Require(
            string schemas, string map, params string[] spellings)
        {
            ToolSchemaGate.Require(
                ToolSchemaJsonReader.Read(schemas),
                ToolMapJsonReader.Read(map),
                new HashSet<string>(
                    spellings.Length == 0 ? new[] { "number", "text" } : spellings,
                    StringComparer.Ordinal));
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

            Assert.Throws<ArgumentNullException>(() => ToolSchemaGate.Require(null, map, spellings));
            Assert.Throws<ArgumentNullException>(
                () => ToolSchemaGate.Require(schemas, null, spellings));
            Assert.Throws<ArgumentNullException>(() => ToolSchemaGate.Require(schemas, map, null));
        }
    }
}
