using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeRoleTableJsonReaderTests
    {
        [Fact]
        public void ATypeIsReadWithItsRoleAndBasis()
        {
            TypeRoleRecord record = Assert.Single(
                ReadTypes(Noun("N.IThing", "connector", "\"elementNoun\":\"thing\"")));

            Assert.Equal("N.IThing", record.TypeName);
            Assert.Equal(TypeRole.Connector, record.Role);
            Assert.Equal("N.IThing の根拠。", record.Basis);
            Assert.Equal("thing", record.ElementNoun);
        }

        [Fact]
        public void EveryRoleNameIsRead()
        {
            IList<TypeRoleRecord> records = ReadTypes(
                Noun("N.A", "connector", "\"elementNoun\":\"alpha\""),
                Item("N.B", "dto"),
                Item("N.C", "eventArgs"),
                Noun("N.D", "handleTarget",
                    "\"elementNoun\":\"delta\",\"elementNounPlural\":\"deltas\""),
                Noun("N.E", "operationTarget",
                    "\"elementNoun\":\"epsilon\",\"elementNounPlural\":\"epsilons\""));

            Assert.Equal(
                new[]
                {
                    TypeRole.Connector,
                    TypeRole.Dto,
                    TypeRole.EventArgs,
                    TypeRole.HandleTarget,
                    TypeRole.OperationTarget,
                },
                records.Select(r => r.Role));
        }

        [Fact]
        public void TypesOutOfOrdinalOrderStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(Item("N.B", "dto"), Item("N.A", "dto")));

            Assert.Contains("昇順", error.Message);
        }

        [Fact]
        public void TheSameTypeTwiceStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(Item("N.A", "dto"), Item("N.A", "dto")));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void AnUnknownRoleStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(Item("N.A", "builder")));

            Assert.Contains("builder", error.Message);
        }

        [Fact]
        public void ATypeNameOrBasisThatIsBlankStops()
        {
            foreach (string item in new[]
            {
                "{\"typeName\":\"N.A\",\"role\":\"dto\",\"basis\":\"\"}",
                "{\"typeName\":\"N.A\",\"role\":\"dto\",\"basis\":\"  \"}",
                "{\"typeName\":\"\",\"role\":\"dto\",\"basis\":\"根拠。\"}",
                "{\"typeName\":\"  \",\"role\":\"dto\",\"basis\":\"根拠。\"}",
            })
            {
                Assert.Throws<FormatException>(() => ReadTypes(item));
            }
        }

        [Fact]
        public void AnUnknownMemberStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(
                    "{\"typeName\":\"N.A\",\"role\":\"dto\",\"basis\":\"根拠。\",\"note\":\"x\"}"));

            Assert.Contains("note", error.Message);
        }

        [Fact]
        public void AMissingMemberStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes("{\"typeName\":\"N.A\",\"role\":\"dto\"}"));

            Assert.Contains("basis", error.Message);
        }

        [Fact]
        public void TheElementNounsAreRead()
        {
            IList<TypeRoleRecord> records = ReadTypes(
                Noun("N.A", "connector", "\"elementNoun\":\"alpha\""),
                Noun("N.B", "handleTarget",
                    "\"elementNoun\":\"beta\",\"elementNounPlural\":\"betas\""),
                Item("N.C", "dto"));

            Assert.Equal(new[] { "alpha", "beta", string.Empty }, records.Select(r => r.ElementNoun));
            Assert.Equal(
                new[] { string.Empty, "betas", string.Empty },
                records.Select(r => r.ElementNounPlural));
        }

        [Fact]
        public void ARoleThatNeedsAnElementNounWithoutOneStops()
        {
            foreach (string role in new[] { "connector", "handleTarget", "operationTarget" })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(Item("N.A", role)));

                Assert.Contains("elementNoun", error.Message);
            }
        }

        [Fact]
        public void ARoleThatNeedsAPluralWithoutOneStops()
        {
            foreach (string role in new[] { "handleTarget", "operationTarget" })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(Noun("N.A", role, "\"elementNoun\":\"alpha\"")));

                Assert.Contains("elementNounPlural", error.Message);
            }
        }

        [Fact]
        public void ARoleThatTakesNoElementNounWithAPluralStops()
        {
            foreach (string role in new[] { "eventArgs", "dto" })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(Noun("N.A", role, "\"elementNounPlural\":\"alphas\"")));

                Assert.Contains("elementNounPlural", error.Message);
            }
        }

        [Fact]
        public void APluralThatIsNotASnakeCaseWordStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(Noun(
                    "N.A",
                    "handleTarget",
                    "\"elementNoun\":\"alpha\",\"elementNounPlural\":\"Alphas\"")));

            Assert.Contains("elementNounPlural", error.Message);
        }

        [Fact]
        public void AnItemThatIsNotAnObjectStopsBeforeTheRole()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles("{\"types\":[\"N.A\"]}"));

            Assert.Contains("項目の組", error.Message);
        }

        [Fact]
        public void AConnectorWithAPluralStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(Noun(
                    "N.A",
                    "connector",
                    "\"elementNoun\":\"alpha\",\"elementNounPlural\":\"alphas\"")));

            Assert.Contains("elementNounPlural", error.Message);
        }

        [Fact]
        public void ARoleThatTakesNoElementNounWithOneStops()
        {
            foreach (string role in new[] { "eventArgs", "dto" })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(Noun("N.A", role, "\"elementNoun\":\"alpha\"")));

                Assert.Contains("elementNoun", error.Message);
            }
        }

        [Fact]
        public void AnElementNounThatIsNotASnakeCaseWordStops()
        {
            foreach (string noun in new[] { "Alpha", "alpha-beta", "_alpha", "alpha__beta", "1alpha", "alpha " })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(Noun("N.A", "connector", "\"elementNoun\":\"" + noun + "\"")));

                Assert.Contains("elementNoun", error.Message);
            }
        }

        [Fact]
        public void TheSameElementNounTwiceStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(
                    Noun("N.A", "connector", "\"elementNoun\":\"alpha\""),
                    Noun("N.B", "connector", "\"elementNoun\":\"alpha\"")));

            Assert.Contains("alpha", error.Message);
        }

        [Fact]
        public void ASingularThatRepeatsAnotherPluralStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(
                    Noun("N.A", "handleTarget",
                        "\"elementNoun\":\"alpha\",\"elementNounPlural\":\"betas\""),
                    Noun("N.B", "connector", "\"elementNoun\":\"betas\"")));

            Assert.Contains("betas", error.Message);
        }

        [Fact]
        public void AnEmptyTableIsRead()
        {
            Assert.Empty(TypeRoleTableJsonReader.ReadTypeRoles("{\"types\":[]}"));
        }

        [Fact]
        public void ATableWithoutTheTypesStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles("{}"));

            Assert.Contains("types", error.Message);
        }

        [Fact]
        public void TypesThatAreNotAListStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles("{\"types\":\"x\"}"));

            Assert.Contains("types", error.Message);
        }

        [Fact]
        public void AnUnknownMemberAtTheRootStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles("{\"types\":[],\"note\":1}"));

            Assert.Contains("note", error.Message);
        }

        [Fact]
        public void AValueThatIsNotAStringStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes("{\"typeName\":\"N.A\",\"role\":1,\"basis\":\"根拠。\"}"));

            Assert.Contains("文字列", error.Message);
        }

        [Fact]
        public void AnItemThatIsNotAnObjectStops()
        {
            Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles("{\"types\":[\"N.A\"]}"));
        }

        [Fact]
        public void TextThatIsNotJsonStops()
        {
            Assert.Throws<FormatException>(() => TypeRoleTableJsonReader.ReadTypeRoles("役割"));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles(null));
        }

        private static IList<TypeRoleRecord> ReadTypes(params string[] items)
        {
            return TypeRoleTableJsonReader.ReadTypeRoles(
                "{\"types\":[" + string.Join(",", items) + "]}");
        }

        private static string Item(string typeName, string role)
        {
            return "{\"typeName\":\"" + typeName + "\",\"role\":\"" + role
                + "\",\"basis\":\"" + typeName + " の根拠。\"}";
        }

        private static string Noun(string typeName, string role, string nouns)
        {
            return "{\"typeName\":\"" + typeName + "\",\"role\":\"" + role
                + "\",\"basis\":\"" + typeName + " の根拠。\"," + nouns + "}";
        }
    }
}
