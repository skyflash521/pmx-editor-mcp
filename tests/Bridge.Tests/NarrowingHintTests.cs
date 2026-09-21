using System;
using System.Text.Json;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class NarrowingHintTests
    {
        [Fact]
        public void AToolThatTakesFieldsIsToldToNarrowTheFields()
        {
            Assert.Contains("fields", Hint("fields", "all"), StringComparison.Ordinal);
        }

        [Fact]
        public void AToolThatTakesOffsetAndLimitIsToldToReadInParts()
        {
            string hint = Hint("offset", "limit");

            Assert.Contains("offset", hint, StringComparison.Ordinal);
            Assert.Contains("limit", hint, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolThatTakesNeitherIsGivenNoHint()
        {
            Assert.Null(Hint("confirm"));
        }

        [Fact]
        public void AnInputThatTheToolDoesNotTakeIsNotNamed()
        {
            Assert.DoesNotContain("offset", Hint("fields"), StringComparison.Ordinal);
        }

        [Fact]
        public void AnInputFormThatIsNotThereIsGivenNoHint()
        {
            Assert.Null(NarrowingHint.Of(default(JsonElement)));
        }

        private static string Hint(params string[] names)
        {
            return NarrowingHint.Of(JsonDocument.Parse(Schema(names)).RootElement);
        }

        private static string Schema(string[] names)
        {
            string properties = string.Empty;
            foreach (string name in names)
            {
                properties += (properties.Length == 0 ? string.Empty : ",")
                    + "\"" + name + "\":{\"type\":\"string\"}";
            }

            return "{\"type\":\"object\",\"properties\":{" + properties + "}}";
        }
    }
}
