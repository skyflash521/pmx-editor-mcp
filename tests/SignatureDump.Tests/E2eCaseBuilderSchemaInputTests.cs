using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class E2eCaseBuilderSchemaInputTests
    {
        private const string Tool = "motion_create_vmd";

        private const string Count = "count";

        private const string BoneNames = "boneNames";

        private const string MorphNames = "morphNames";

        [Theory]
        [InlineData(MorphNames)]
        [InlineData(BoneNames)]
        public void AnInputOnlyThePublicSchemaAcceptsIsSentAndExpectedToBeAccepted(string alone)
        {
            IList<E2eCase> cases = Built();

            Assert.Contains(
                cases,
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && (c.Expectation == E2eExpectation.Called
                        || c.Expectation == E2eExpectation.Success)
                    && c.Code == null
                    && c.Arguments.Keys.OrderBy(k => k, StringComparer.Ordinal)
                        .SequenceEqual(new[] { alone }, StringComparer.Ordinal));
        }

        [Fact]
        public void TheInputThatOnlyThePublicSchemaAcceptsCarriesAValueOfItsShape()
        {
            E2eCase sent = Built().FirstOrDefault(
                c => string.Equals(c.Tool, Tool, StringComparison.Ordinal)
                    && c.Code == null
                    && c.Arguments.Count == 1
                    && c.Arguments.ContainsKey(MorphNames));

            Assert.NotNull(sent);
            object[] names = Assert.IsType<object[]>(sent.Arguments[MorphNames]);
            Assert.NotEmpty(names);
            Assert.All(names, one => Assert.IsType<string>(one));
        }

        private static IList<E2eCase> Built()
        {
            return E2eCaseBuilder.Build(
                new ToolMap(new ToolMapRow[0]),
                new ToolSchemaTable(new[] { Creating() }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<SchemaItem, string>());
        }

        private static ToolSchema Creating()
        {
            return new ToolSchema(
                Tool,
                new[]
                {
                    new SchemaBranch(
                        "plain", null, null, new[] { Counted() }, new SchemaChoice[0]),
                    new SchemaBranch(
                        "named",
                        null,
                        null,
                        new[] { Names(BoneNames), Names(MorphNames), Counted() },
                        new SchemaChoice[0]),
                },
                new SchemaItem(
                    null,
                    null,
                    new SchemaItem(
                        "number", null, null, null, ItemOrigin.HostOutput, null, null, false,
                        null, null, null, false, null),
                    null,
                    ItemOrigin.HostOutput,
                    null,
                    null,
                    false,
                    null,
                    null,
                    null,
                    false,
                    null),
                null);
        }

        private static SchemaItem Counted()
        {
            return new SchemaItem(
                "number", null, null, Count, ItemOrigin.HostInput, false, null, false, null, null,
                null, false, null);
        }

        private static SchemaItem Names(string name)
        {
            return new SchemaItem(
                null,
                null,
                new SchemaItem(
                    "text", null, null, null, ItemOrigin.HostInput, null, null, false, null,
                    null, null, false, null),
                name,
                ItemOrigin.HostInput,
                true,
                null,
                false,
                null,
                null,
                null,
                false,
                null);
        }
    }
}
