using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// スキーマ正本からMCPクライアントへ載せる入力の形を組み立てる。件数と要素数の上限は正本に
    /// 書かず、予算から逆算した値をここで入れる。
    /// </summary>
    public sealed class ToolDefinitionBuilderTests
    {
        private const int ValueChars = 98000;

        private static readonly IDictionary<SchemaItem, string> NoSdkShapes =
            new Dictionary<SchemaItem, string>();

        private static readonly ISet<string> NoDangerousTools =
            new HashSet<string>(StringComparer.Ordinal);

        private static readonly ISet<string> NoDrawingTools =
            new HashSet<string>(StringComparer.Ordinal);

        private const int RequestBytes = 8000000;

        private const int TokenLimit = 200000;

        /// <summary>想定文字数の題材。</summary>
        private static readonly IDictionary<string, int> Lengths =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "boolean", 5 },
                { "number", 12 },
                { "text", 40 },
                { "enum_name", 20 },
            };

        [Fact]
        public void ATextInputBecomesAStringProperty()
        {
            Assert.Equal(
                "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}},"
                    + "\"required\":[\"name\"],\"additionalProperties\":false}",
                Schema(Tool("one", Branch(Input("name", "text", true)))));
        }

        [Fact]
        public void AnInputThatIsNotRequiredIsLeftOutOfTheRequiredList()
        {
            Assert.Equal(
                "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}},"
                    + "\"additionalProperties\":false}",
                Schema(Tool("one", Branch(Input("name", "text", false)))));
        }

        [Theory]
        [InlineData("boolean", "{\"type\":\"boolean\"}")]
        [InlineData("number", "{\"type\":\"number\"}")]
        [InlineData("text", "{\"type\":\"string\"}")]
        [InlineData("enum_name", "{\"type\":\"string\"}")]
        [InlineData("size", "{\"type\":\"array\",\"items\":{\"type\":\"number\"},\"minItems\":2,\"maxItems\":2}")]
        [InlineData("color", "{\"type\":\"array\",\"items\":{\"type\":\"number\"},\"minItems\":3,\"maxItems\":4}")]
        [InlineData("number_array", "{\"type\":\"array\",\"items\":{\"type\":\"number\"}}")]
        [InlineData("json", "{}")]
        public void EachSpellingTakesItsOwnShape(string shape, string expected)
        {
            Assert.Contains(
                "\"value\":" + expected,
                Schema(Tool("one", Branch(Input("value", shape, true)))));
        }

        [Fact]
        public void ASpellingThatTheBuilderDoesNotCarryIsRefused()
        {
            Assert.Throws<InvalidOperationException>(
                () => Schema(Tool("one", Branch(Input("value", "unknown_shape", true)))));
        }

        [Fact]
        public void BoundsAndDefaultsFromTheSourceAreCarriedThrough()
        {
            string schema = Schema(Tool(
                "one",
                Branch(new SchemaItem(
                    "number", null, null, "depth", ItemOrigin.HostInput, true, 3, true,
                    new ValueBounds(1, 9), null, "一次資料", false, null))));

            Assert.Contains("\"depth\":{\"type\":\"number\",\"minimum\":1,\"maximum\":9,\"default\":3}", schema);
        }

        [Fact]
        public void AnArrayWithoutAWrittenLimitTakesTheDerivedOne()
        {
            string schema = Schema(Tool("one", Branch(Array("values", "number", null))));

            Assert.Contains("\"type\":\"array\",\"items\":{\"type\":\"number\"},\"maxItems\":", schema);
        }

        [Fact]
        public void AnArrayWhoseLengthTheSourceFixesKeepsThatLength()
        {
            Assert.Contains(
                "\"maxItems\":4}",
                Schema(Tool("one", Branch(Array("values", "number", 4)))));
        }

        [Fact]
        public void OnlyAnArrayThatCannotBeEmptyCarriesALowerBound()
        {
            Assert.Contains(
                "\"minItems\":1",
                Schema(Tool("one", Branch(Array("handles", "number", null)))));
            Assert.DoesNotContain(
                "\"minItems\"",
                Schema(Tool("one", Branch(Array("values", "number", null)))));
        }

        [Fact]
        public void OnlyAnArrayThatRefusesARepeatCarriesUniqueItems()
        {
            Assert.Contains(
                "\"uniqueItems\":true",
                Schema(Tool("one", Branch(Unique(Array("values", "number", null))))));
            Assert.DoesNotContain(
                "\"uniqueItems\"",
                Schema(Tool("one", Branch(Array("values", "number", null)))));
        }

        [Fact]
        public void AnInputTheHostFillsInIsNotShownToTheCaller()
        {
            string schema = Schema(Tool(
                "one",
                Branch(Input("name", "text", true), Injected("connector"))));

            Assert.DoesNotContain("connector", schema);
            Assert.Contains("\"name\"", schema);
        }

        [Fact]
        public void AnItemThatAllowsNoValueTakesNullToo()
        {
            Assert.Contains(
                "\"name\":{\"type\":[\"string\",\"null\"]}",
                Schema(Tool("one", Branch(Nullable("name", "text")))));
        }

        [Fact]
        public void AnArrayThatAllowsNoValueTakesNullToo()
        {
            Assert.Contains(
                "\"values\":{\"type\":[\"array\",\"null\"],",
                Schema(Tool("one", Branch(NullableArray("values", "number")))));
        }

        [Fact]
        public void ABranchChosenByAValuePinsThatValue()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    new SchemaBranch(
                        "byName", "kind", "name", new[] { Input("kind", "text", true) },
                        new SchemaChoice[0]),
                    new SchemaBranch(
                        "byIndex", "kind", "index", new[] { Input("kind", "text", true) },
                        new SchemaChoice[0]),
                },
                Output("number"),
                null));

            Assert.Contains(
                "\"kind\":{\"anyOf\":[{\"type\":\"string\",\"const\":\"name\"},"
                    + "{\"type\":\"string\",\"const\":\"index\"}]}",
                schema);
        }

        [Fact]
        public void AnInputWhoseShapesOverlapStillTakesTheValuesBothAllowed()
        {
            SchemaItem WithMembers(params SchemaItem[] members) => new SchemaItem(
                null, members, null, "value", ItemOrigin.HostInput, false, null, false, null, null,
                null, false, null);

            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    new SchemaBranch(
                        "byOffset", null, null,
                        new[] { WithMembers(Input("offset", "number", false)) },
                        new SchemaChoice[0]),
                    new SchemaBranch(
                        "byRatio", null, null,
                        new[] { WithMembers(Input("ratio", "number", false)) },
                        new SchemaChoice[0]),
                },
                Output("number"),
                null));

            Assert.Contains("\"value\":{\"anyOf\":[", schema);
            Assert.DoesNotContain("\"oneOf\":[{\"type\":\"object\"", schema);
        }

        [Fact]
        public void ANestedItemWithTheSameNameIsNotPinned()
        {
            SchemaItem nested = new SchemaItem(
                null,
                new[] { Input("kind", "text", true) },
                null,
                "detail",
                ItemOrigin.HostInput,
                true,
                null,
                false,
                null,
                null,
                null,
                false,
                null);

            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    new SchemaBranch(
                        "byName", "kind", "name", new[] { Input("kind", "text", true), nested },
                        new SchemaChoice[0]),
                },
                Output("number"),
                null));

            Assert.Equal(1, Occurrences(schema, "\"const\""));
        }

        [Fact]
        public void SeveralBranchesBecomeOneSetOfInputs()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    Branch(Input("name", "text", true)),
                    new SchemaBranch(
                        "other", null, null, new[] { Input("index", "number", true) }, new SchemaChoice[0]),
                },
                Output("number"),
                null));

            Assert.StartsWith("{\"type\":\"object\",\"properties\":{", schema);
            Assert.Contains("\"name\":{\"type\":\"string\"}", schema);
            Assert.Contains("\"index\":{\"type\":\"number\"}", schema);
        }

        [Fact]
        public void AnInputThatMeetsOneBranchIsTakenAndOneThatMeetsNoBranchIsNot()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    Branch(Input("name", "text", true)),
                    new SchemaBranch(
                        "other", null, null, new[] { Input("index", "number", true) }, new SchemaChoice[0]),
                },
                Output("number"),
                null));

            Assert.True(Takes(schema, "{\"name\":\"a\"}"), schema);
            Assert.True(Takes(schema, "{\"index\":1}"), schema);
            Assert.False(Takes(schema, "{}"), schema);
            Assert.False(Takes(schema, "{\"name\":\"a\",\"index\":1}"), schema);
        }

        [Fact]
        public void TheInputsABranchRequiresTogetherStayRequiredTogether()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    new SchemaBranch(
                        "plain", null, null, new[] { Input("count", "number", false) },
                        new SchemaChoice[0]),
                    new SchemaBranch(
                        "named",
                        null,
                        null,
                        new[]
                        {
                            Array("boneNames", "text", 4),
                            Array("morphNames", "text", 4),
                            Input("count", "number", false),
                        },
                        new SchemaChoice[0]),
                },
                Output("number"),
                null));

            Assert.True(Takes(schema, "{}"), schema);
            Assert.True(Takes(schema, "{\"count\":2}"), schema);
            Assert.True(Takes(schema, "{\"boneNames\":[\"a\"],\"morphNames\":[\"b\"]}"), schema);
            Assert.False(Takes(schema, "{\"morphNames\":[\"b\"]}"), schema);
            Assert.False(Takes(schema, "{\"boneNames\":[\"a\"]}"), schema);
        }

        [Fact]
        public void TheWaysOfPointingOfTwoBranchesAreNotTakenTogether()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    new SchemaBranch(
                        "position",
                        null,
                        null,
                        new[]
                        {
                            Optional(Array("indices", "number", 4)),
                            Input("all", "boolean", false),
                            Input("limit", "number", false),
                        },
                        new[] { new SchemaChoice(new[] { "all", "indices" }, true) }),
                    new SchemaBranch(
                        "held",
                        null,
                        null,
                        new[] { Array("handles", "number", 4), Input("limit", "number", false) },
                        new SchemaChoice[0]),
                },
                Output("number"),
                null));

            Assert.True(Takes(schema, "{\"indices\":[0]}"), schema);
            Assert.True(Takes(schema, "{\"all\":true,\"limit\":3}"), schema);
            Assert.True(Takes(schema, "{\"handles\":[1]}"), schema);
            Assert.False(Takes(schema, "{\"indices\":[0],\"handles\":[1]}"), schema);
            Assert.False(Takes(schema, "{\"all\":true,\"handles\":[1]}"), schema);
            Assert.False(Takes(schema, "{\"all\":true,\"indices\":[0]}"), schema);
            Assert.False(Takes(schema, "{\"limit\":3}"), schema);
        }

        [Fact]
        public void ANameNarrowingAloneStandsInForTheRequiredGroupOfAlternatives()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[]
                        {
                            Optional(Array("indices", "number", 4)),
                            Input("all", "boolean", false),
                            Optional(Array("nameContains", "text", 4)),
                        },
                        new[] { new SchemaChoice(new[] { "all", "indices" }, true) }),
                },
                Output("number"),
                null));

            Assert.True(Takes(schema, "{\"nameContains\":[\"a\"]}"), schema);
            Assert.True(Takes(schema, "{\"all\":true,\"nameContains\":[\"a\"]}"), schema);
            Assert.True(Takes(schema, "{\"indices\":[0],\"nameContains\":[\"a\"]}"), schema);
            Assert.False(
                Takes(schema, "{\"all\":true,\"indices\":[0],\"nameContains\":[\"a\"]}"), schema);
            Assert.False(Takes(schema, "{}"), schema);
        }

        [Fact]
        public void ARequiredGroupOfAlternativesIsClosedToOne()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[] { Input("byName", "text", false), Input("byIndex", "number", false) },
                        new[] { new SchemaChoice(new[] { "byName", "byIndex" }, true) }),
                },
                Output("number"),
                null));

            Assert.Contains(
                "\"oneOf\":[{\"required\":[\"byName\"]},{\"required\":[\"byIndex\"]}]", schema);
            Assert.DoesNotContain("\"not\"", schema);
        }

        [Fact]
        public void AGroupOfAlternativesThatIsNotRequiredAlsoAllowsNone()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[] { Input("byName", "text", false), Input("byIndex", "number", false) },
                        new[] { new SchemaChoice(new[] { "byName", "byIndex" }, false) }),
                },
                Output("number"),
                null));

            Assert.Contains("\"not\":{\"anyOf\":[", schema);
        }

        [Fact]
        public void TheNumberOfRowsAListReturnsComesFromTheBudget()
        {
            string schema = Schema(Listing());

            Assert.Contains("\"limit\":{\"type\":\"number\",\"minimum\":1,\"maximum\":", schema);
            Assert.Contains("\"default\":", schema);
        }

        [Fact]
        public void TheNumberOfRowsAListForEachTargetReturnsComesFromTheBudget()
        {
            string schema = Schema(PerTargetListing());

            Assert.Matches(
                "\"limit\":\\{\"type\":\"number\",\"minimum\":1,\"maximum\":\\d+,\"default\":\\d+\\}",
                schema);
        }

        [Fact]
        public void TheNumberOfHandlesToIssueComesFromWhatTheAnswerCanCarry()
        {
            string schema = Schema(new ToolSchema(
                "one",
                new[] { Branch(Input("count", "number", true)) },
                new SchemaItem(
                    null, null, Element("number"), null, ItemOrigin.HostOutput, null, null, false,
                    null, null, null, false, null),
                null));

            Assert.Contains("\"count\":{\"type\":\"number\",\"minimum\":1,\"maximum\":", schema);
        }

        [Fact]
        public void AToolWithoutADescriptionIsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[] { Tool("one", Branch(Input("name", "text", true))) }),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                NoDangerousTools,
                NoDangerousTools,
                NoDangerousTools,
                NoDrawingTools));
        }

        [Fact]
        public void ToolsComeOutInTheOrderOfTheirNames()
        {
            IList<ToolDefinition> definitions = ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[]
                {
                    Tool("second", Branch(Input("name", "text", true))),
                    Tool("first", Branch(Input("name", "text", true))),
                }),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "first", "ひとつめ" },
                    { "second", "ふたつめ" },
                },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                NoDangerousTools,
                NoDangerousTools,
                NoDangerousTools,
                NoDrawingTools);

            Assert.Equal(new[] { "first", "second" }, definitions.Select(d => d.Name).ToArray());
            Assert.Equal("ひとつめ", definitions[0].Description);
        }

        [Fact]
        public void AToolThatReflectsAllAtOnceTakesTheAskingToStopTheUndo()
        {
            ToolSchema schema = Tool("one", Branch(Input("name", "text", true)));

            string written = ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { "one", "受け持つこと" } },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                NoDangerousTools,
                NoDangerousTools,
                new HashSet<string>(new[] { "one" }, StringComparer.Ordinal),
                NoDrawingTools)[0].InputSchema;

            Assert.Contains("\"suppressUndo\":{\"type\":\"boolean\"}", written);
            Assert.DoesNotContain("suppressUndo\"]", written);
        }

        [Fact]
        public void AToolNamedAsDrawingIsMarkedAsReturningAnImage()
        {
            ToolSchema schema = Tool("one", Branch(Input("name", "text", true)));

            IList<ToolDefinition> definitions = ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { "one", "受け持つこと" } },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                NoDangerousTools,
                NoDangerousTools,
                NoDangerousTools,
                new HashSet<string>(new[] { "one" }, StringComparer.Ordinal));

            Assert.True(definitions[0].ReturnsImage);
        }

        [Fact]
        public void AToolNotNamedAsDrawingIsNotMarkedAsReturningAnImage()
        {
            ToolSchema schema = Tool("one", Branch(Input("name", "text", true)));

            IList<ToolDefinition> definitions = ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { "one", "受け持つこと" } },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                NoDangerousTools,
                NoDangerousTools,
                NoDangerousTools,
                NoDrawingTools);

            Assert.False(definitions[0].ReturnsImage);
        }

        [Fact]
        public void AToolThatDoesNotReflectAllAtOnceDoesNotTakeTheAskingToStopTheUndo()
        {
            string written = Schema(Tool("one", Branch(Input("name", "text", true))));

            Assert.DoesNotContain("suppressUndo", written);
        }

        [Fact]
        public void ADangerousToolTakesTheConfirmationAsARequiredArgument()
        {
            ToolSchema schema = Tool("one", Branch(Input("name", "text", true)));

            string written = ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { "one", "受け持つこと" } },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                new HashSet<string>(new[] { "one" }, StringComparer.Ordinal),
                NoDangerousTools,
                NoDangerousTools,
                NoDrawingTools)[0].InputSchema;

            Assert.Contains("\"confirm\":{\"type\":\"boolean\"}", written);
            Assert.Contains("\"required\":[\"name\",\"confirm\"]", written);
        }

        [Fact]
        public void AToolThatEmptiesOnlyTheModelItsHandlePointsAtRequiresTheHandleAndTakesNoConfirmation()
        {
            ToolSchema schema = Tool(
                "one",
                Branch(Input("name", "text", true), Input("pmxHandle", "number", false)));

            string written = ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { "one", "受け持つこと" } },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                new HashSet<string>(new[] { "one" }, StringComparer.Ordinal),
                new HashSet<string>(new[] { "one" }, StringComparer.Ordinal),
                NoDangerousTools,
                NoDrawingTools)[0].InputSchema;

            Assert.DoesNotContain("confirm", written);
            Assert.Contains("\"required\":[\"name\",\"pmxHandle\"],\"additionalProperties\":false", written);
        }

        [Fact]
        public void OnlyTheDangerousToolsAreMarkedDestructive()
        {
            IList<ToolDefinition> definitions = ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[]
                {
                    Tool("confirming", Branch(Input("name", "text", true))),
                    Tool("emptying", Branch(Input("name", "text", true), Input("pmxHandle", "number", false))),
                    Tool("reading", Branch(Input("name", "text", true))),
                }),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "confirming", "受け持つこと" },
                    { "emptying", "受け持つこと" },
                    { "reading", "受け持つこと" },
                },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                new HashSet<string>(new[] { "confirming", "emptying" }, StringComparer.Ordinal),
                new HashSet<string>(new[] { "emptying" }, StringComparer.Ordinal),
                NoDangerousTools,
                NoDrawingTools);

            Assert.Equal(
                new[] { true, true, false },
                definitions.Select(definition => definition.Destructive).ToArray());
        }

        [Fact]
        public void AToolThatIsNotDangerousDoesNotTakeTheConfirmation()
        {
            Assert.DoesNotContain(
                "confirm", Schema(Tool("one", Branch(Input("name", "text", true)))));
        }

        [Fact]
        public void AnItemThatWritesNoSpellingTakesTheOneDerivedFromItsRow()
        {
            SchemaItem input = new SchemaItem(
                null, null, null, "path", null, true, null, false,
                null, null, null, false, null);
            ToolSchema schema = new ToolSchema(
                "one", new[] { Branch(input) }, Output("number"), null);

            string written = ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { "one", "受け持つこと" } },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                new Dictionary<SchemaItem, string> { { input, "text" } },
                NoDangerousTools,
                NoDangerousTools,
                NoDangerousTools,
                NoDrawingTools)[0].InputSchema;

            Assert.Contains("\"path\":{\"type\":\"string\"}", written);
        }

        [Fact]
        public void AnItemWithNeitherASpellingNorADerivedOneIsRefused()
        {
            SchemaItem input = new SchemaItem(
                null, null, null, "path", null, true, null, false,
                null, null, null, false, null);

            Assert.Throws<InvalidOperationException>(
                () => Schema(new ToolSchema(
                    "one", new[] { Branch(input) }, Output("number"), null)));
        }

        private static int Occurrences(string text, string part)
        {
            int count = 0;
            for (int at = text.IndexOf(part, StringComparison.Ordinal);
                at >= 0;
                at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        private static string Schema(ToolSchema schema)
        {
            return ToolDefinitionBuilder.Build(
                new ToolSchemaTable(new[] { schema }),
                new Dictionary<string, string>(StringComparer.Ordinal) { { schema.Tool, "受け持つこと" } },
                new AssumedLength(Lengths),
                ValueChars,
                RequestBytes,
                TokenLimit,
                NoSdkShapes,
                NoDangerousTools,
                NoDangerousTools,
                NoDangerousTools,
                NoDrawingTools)[0].InputSchema;
        }

        private static ToolSchema Tool(string name, SchemaBranch branch)
        {
            return new ToolSchema(name, new[] { branch }, Output("number"), null);
        }

        /// <summary>総数と切り出した並びを返す形。一覧を返すツールはこの形を取る。</summary>
        private static ToolSchema Listing()
        {
            SchemaItem row = new SchemaItem(
                null,
                new[] { Member("index", "number", ItemOrigin.HostOutput), Member("name", "text", null) },
                null,
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                false,
                null);

            SchemaItem output = new SchemaItem(
                null,
                new[]
                {
                    Member("total", "number", ItemOrigin.HostOutput),
                    new SchemaItem(
                        null, null, row, "items", ItemOrigin.HostOutput, null, null, false,
                        null, null, null, false, null),
                },
                null,
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                false,
                null);

            return new ToolSchema(
                "one",
                new[] { Branch(Input("limit", "number", false)) },
                output,
                null);
        }

        private static ToolSchema PerTargetListing()
        {
            SchemaItem answer = new SchemaItem(
                null,
                new[]
                {
                    Member("total", "number", ItemOrigin.HostOutput),
                    new SchemaItem(
                        null, null, Member(null, "text", null), "items", ItemOrigin.HostOutput, null,
                        null, false, null, null, null, false, null),
                    Member("nextOffset", "number", ItemOrigin.HostOutput),
                },
                null,
                null,
                ItemOrigin.HostOutput,
                null,
                null,
                false,
                null,
                null,
                null,
                false,
                null);

            return new ToolSchema(
                "one",
                new[] { Branch(Array("handles", "number", null), Input("limit", "number", false)) },
                new SchemaItem(
                    null, null, answer, null, ItemOrigin.HostOutput, null, null, false,
                    null, null, null, false, null),
                null);
        }

        private static bool Takes(string schema, string input)
        {
            using (System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(input))
            {
                return Json.Schema.JsonSchema.FromText(schema)
                    .Evaluate(document.RootElement)
                    .IsValid;
            }
        }

        private static SchemaItem Optional(SchemaItem item)
        {
            return new SchemaItem(
                item.Shape, item.Members, item.Element, item.Name, item.Origin, false, item.Default,
                item.HasDefault, item.Bounds, item.Nullable, item.Source, item.Injected,
                item.MaxItems);
        }

        private static SchemaBranch Branch(params SchemaItem[] inputs)
        {
            return new SchemaBranch("only", null, null, inputs, new SchemaChoice[0]);
        }

        private static SchemaItem Input(string name, string shape, bool required)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostInput, required, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Injected(string name)
        {
            return new SchemaItem(
                null, null, null, name, ItemOrigin.HostInput, true, null, false,
                null, null, null, true, null);
        }

        private static SchemaItem Nullable(string name, string shape)
        {
            return new SchemaItem(
                shape, null, null, name, ItemOrigin.HostInput, false, null, false,
                null, true, null, false, null);
        }

        private static SchemaItem NullableArray(string name, string shape)
        {
            return new SchemaItem(
                null, null, Element(shape), name, ItemOrigin.HostInput, false, null, false,
                null, true, null, false, null);
        }

        private static SchemaItem Member(string name, string shape, ItemOrigin? origin)
        {
            return new SchemaItem(
                shape, null, null, name, origin, null, null, false, null, null, null, false, null);
        }

        private static SchemaItem Array(string name, string shape, int? maxItems)
        {
            return new SchemaItem(
                null, null, Element(shape), name, ItemOrigin.HostInput, true, null, false,
                null, null, maxItems == null ? null : "一次資料", false, maxItems);
        }

        private static SchemaItem Unique(SchemaItem item)
        {
            return new SchemaItem(
                item.Shape, item.Members, item.Element, item.Name, item.Origin, item.Required,
                item.Default, item.HasDefault, item.Bounds, item.Nullable, item.Source,
                item.Injected, item.MaxItems, uniqueItems: true);
        }

        private static SchemaItem Element(string shape)
        {
            return new SchemaItem(
                shape, null, null, null, ItemOrigin.HostInput, null, null, false,
                null, null, null, false, null);
        }

        private static SchemaItem Output(string shape)
        {
            return new SchemaItem(
                shape, null, null, null, ItemOrigin.HostOutput, null, null, false,
                null, null, null, false, null);
        }
    }
}
