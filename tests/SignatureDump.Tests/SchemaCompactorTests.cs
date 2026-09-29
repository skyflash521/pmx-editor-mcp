using System.Text.Json.Nodes;
using PmxEditorMcp.SignatureDump;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SchemaCompactorTests
    {
        private const string Handle = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":2147483647}";

        private const string Repeated =
            "{\"type\":\"object\",\"properties\":{\"pmxHandle\":" + Handle
            + ",\"handles\":{\"type\":\"array\",\"items\":" + Handle + ",\"minItems\":1}"
            + ",\"parentHandle\":" + Handle + "},"
            + "\"anyOf\":[{\"required\":[\"handles\"]},{\"required\":[\"parentHandle\"]}],"
            + "\"additionalProperties\":false}";

        [Fact]
        public void ARepeatedSubschemaMovesToTheDefinitionsAndComesBackUnchanged()
        {
            string compacted = SchemaCompactor.Compact(Repeated);

            Assert.True(compacted.Length < Repeated.Length);
            JsonObject root = JsonNode.Parse(compacted).AsObject();
            Assert.True(root["$defs"] is JsonObject);
            Assert.Equal("#/$defs/a", root["properties"]["pmxHandle"]["$ref"].GetValue<string>());
            Assert.Equal("#/$defs/a", root["properties"]["handles"]["items"]["$ref"].GetValue<string>());
            Assert.Equal(JsonNode.Parse(Repeated).ToJsonString(), SchemaCompactor.Inline(compacted));
        }

        [Fact]
        public void TheRootAndItsPropertiesStayInPlace()
        {
            JsonObject root = JsonNode.Parse(SchemaCompactor.Compact(Repeated)).AsObject();

            Assert.Equal("object", root["type"].GetValue<string>());
            Assert.True(root["properties"] is JsonObject);
            Assert.False(root["additionalProperties"].GetValue<bool>());
        }

        [Fact]
        public void ASchemaWithoutRepetitionIsLeftAsWritten()
        {
            const string single = "{\"type\":\"object\",\"properties\":{\"pmxHandle\":" + Handle + "}}";

            Assert.Equal(single, SchemaCompactor.Compact(single));
        }

        [Fact]
        public void ABranchDropsTheObjectTypeTheRootAlreadyRequires()
        {
            const string branched =
                "{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"boolean\"}},"
                + "\"anyOf\":[{\"type\":\"object\",\"required\":[\"a\"]},{\"type\":\"object\"}]}";

            Assert.Equal(
                "{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"boolean\"}},"
                    + "\"anyOf\":[{\"required\":[\"a\"]},{}]}",
                SchemaCompactor.Shortened(branched));
        }

        [Fact]
        public void ABranchKeepsItsTypeWhenTheRootDoesNotRequireAnObject()
        {
            const string branched = "{\"anyOf\":[{\"type\":\"object\"},{\"type\":\"array\"}]}";

            Assert.Equal(branched, SchemaCompactor.Shortened(branched));
        }

        [Fact]
        public void AConstantDropsOnlyTheTypeItAlreadyHas()
        {
            const string constant =
                "{\"type\":\"object\",\"properties\":{\"all\":{\"type\":\"boolean\",\"const\":true},"
                + "\"kind\":{\"type\":\"string\",\"const\":\"a\"},\"n\":{\"type\":\"integer\",\"const\":1},"
                + "\"odd\":{\"type\":\"string\",\"const\":true}}}";

            Assert.Equal(
                "{\"type\":\"object\",\"properties\":{\"all\":{\"const\":true},"
                    + "\"kind\":{\"const\":\"a\"},\"n\":{\"type\":\"integer\",\"const\":1},"
                    + "\"odd\":{\"type\":\"string\",\"const\":true}}}",
                SchemaCompactor.Shortened(constant));
        }

        [Fact]
        public void AUniqueListOfChoicesDropsOnlyAnUpperCountItCannotReach()
        {
            const string listed =
                "{\"type\":\"object\",\"properties\":{"
                + "\"wide\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"a\",\"b\"]},\"uniqueItems\":true,\"maxItems\":9},"
                + "\"tight\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"a\",\"b\",\"c\"]},\"uniqueItems\":true,\"maxItems\":2},"
                + "\"repeated\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"a\",\"b\"]},\"maxItems\":9}}}";

            Assert.Equal(
                "{\"type\":\"object\",\"properties\":{"
                    + "\"wide\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"a\",\"b\"]},\"uniqueItems\":true},"
                    + "\"tight\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"a\",\"b\",\"c\"]},\"uniqueItems\":true,\"maxItems\":2},"
                    + "\"repeated\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"a\",\"b\"]},\"maxItems\":9}}}",
                SchemaCompactor.Shortened(listed));
        }

        [Fact]
        public void AListWhoseLeadingItemsAreNotTheChoicesKeepsItsUpperCount()
        {
            const string listed =
                "{\"type\":\"object\",\"properties\":{\"picked\":{\"type\":\"array\",\"prefixItems\":[{\"const\":\"b\"}],"
                + "\"items\":{\"enum\":[\"a\"]},\"uniqueItems\":true,\"maxItems\":1}}}";

            Assert.Equal(listed, SchemaCompactor.Shortened(listed));
        }

        [Fact]
        public void KeywordsThatHoldValuesAreNotTakenForSubschemas()
        {
            const string valued =
                "{\"type\":\"object\",\"properties\":{\"a\":{\"const\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":2147483647}},"
                + "\"b\":{\"default\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":2147483647}}}}";

            Assert.Equal(valued, SchemaCompactor.Compact(valued));
        }
    }
}
