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

        private const int RequestBytes = 8000000;

        private const int TokenLimit = 200000;

        /// <summary>想定文字数の題材。綴りごとの値は共通契約仕様書の表が正本である。</summary>
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

        /// <summary>並びの上限は予算から導く値なので、正本に無くても入る。</summary>
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

        /// <summary>空にできない並びだけが下限を持つ。どれがそれかは項目の名前が決める。</summary>
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

        /// <summary>ホストが自分で入れる引数は、呼び出す側へ現れない。</summary>
        [Fact]
        public void AnInputTheHostFillsInIsNotShownToTheCaller()
        {
            string schema = Schema(Tool(
                "one",
                Branch(Input("name", "text", true), Injected("connector"))));

            Assert.DoesNotContain("connector", schema);
            Assert.Contains("\"name\"", schema);
        }

        /// <summary>値を持たないことを許す項目は、その形と null の両方を受け取る。</summary>
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

        /// <summary>値で分かれる呼び分けは、その項目の値そのものが分岐を選ぶ。</summary>
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

            Assert.Contains("\"kind\":{\"type\":\"string\",\"const\":\"name\"}", schema);
            Assert.Contains("\"kind\":{\"type\":\"string\",\"const\":\"index\"}", schema);
        }

        /// <summary>縛るのは分岐を選ぶ入力だけで、名前が同じだけの入れ子の項目は縛らない。</summary>
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
        public void SeveralBranchesBecomeAChoiceOfShapes()
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

            Assert.StartsWith("{\"type\":\"object\",\"oneOf\":[{\"type\":\"object\"", schema);
            Assert.Contains("\"index\":{\"type\":\"number\"}", schema);
        }

        /// <summary>まとまりのうち1つだけを受け取る決まりは、形の上でも1つだけに閉じる。</summary>
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

        /// <summary>一覧の件数は予算から導く値なので、正本に書かれていなくても既定と上限が入る。</summary>
        [Fact]
        public void TheNumberOfRowsAListReturnsComesFromTheBudget()
        {
            string schema = Schema(Listing());

            Assert.Contains("\"limit\":{\"type\":\"number\",\"minimum\":1,\"maximum\":", schema);
            Assert.Contains("\"default\":", schema);
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
                NoDangerousTools));
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
                NoDangerousTools);

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
                new HashSet<string>(new[] { "one" }, StringComparer.Ordinal))[0].InputSchema;

            Assert.Contains("\"suppressUndo\":{\"type\":\"boolean\"}", written);
            Assert.DoesNotContain("suppressUndo\"]", written);
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
                NoDangerousTools)[0].InputSchema;

            Assert.Contains("\"confirm\":{\"type\":\"boolean\"}", written);
            Assert.Contains("\"required\":[\"name\",\"confirm\"]", written);
        }

        [Fact]
        public void AToolWhoseConfirmationDependsOnTheTargetDoesNotAlwaysRequireIt()
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
                NoDangerousTools)[0].InputSchema;

            Assert.Contains("\"confirm\":{\"type\":\"boolean\"}", written);
            Assert.Contains("\"required\":[\"name\"],\"additionalProperties\":false", written);
            Assert.Contains(
                "\"anyOf\":[{\"required\":[\"pmxHandle\"]},{\"required\":[\"confirm\"]}]",
                written);
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
                NoDangerousTools)[0].InputSchema;

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
                NoDangerousTools)[0].InputSchema;
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
