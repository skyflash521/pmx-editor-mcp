using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeRoleTableJsonReaderTests
    {
        private const string Roots = "\"connectionRoots\":[\"N.IRoot\"]";

        [Fact]
        public void ATypeIsReadWithItsRoleAndBasis()
        {
            TypeRoleRecord record = Assert.Single(ReadTypes(Item("N.IThing", "connector")));

            Assert.Equal("N.IThing", record.TypeName);
            Assert.Equal(TypeRole.Connector, record.Role);
            Assert.Equal("N.IThing の根拠。", record.Basis);
        }

        [Fact]
        public void EveryRoleNameIsRead()
        {
            IList<TypeRoleRecord> records = ReadTypes(
                Item("N.A", "connector"),
                Item("N.B", "dto"),
                Item("N.C", "eventArgs"),
                Item("N.D", "handleTarget"),
                Item("N.E", "operationTarget"));

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
        public void AnEmptyTableIsRead()
        {
            Assert.Empty(TypeRoleTableJsonReader.Read("{" + Roots + ",\"types\":[]}").Types);
        }

        [Fact]
        public void TheConnectionRootsAreRead()
        {
            TypeRoleTable table = TypeRoleTableJsonReader.Read(
                "{\"connectionRoots\":[\"N.IAlpha\",\"N.IBeta\"],\"types\":[]}");

            Assert.Equal(new[] { "N.IAlpha", "N.IBeta" }, table.ConnectionRoots);
        }

        [Fact]
        public void RootsOutOfOrdinalOrderStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read(
                    "{\"connectionRoots\":[\"N.IBeta\",\"N.IAlpha\"],\"types\":[]}"));

            Assert.Contains("昇順", error.Message);
        }

        [Fact]
        public void TheSameRootTwiceStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read(
                    "{\"connectionRoots\":[\"N.IAlpha\",\"N.IAlpha\"],\"types\":[]}"));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void ARootThatIsBlankStops()
        {
            Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read(
                    "{\"connectionRoots\":[\"  \"],\"types\":[]}"));
        }

        [Fact]
        public void AnEmptyListOfRootsStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read("{\"connectionRoots\":[],\"types\":[]}"));

            Assert.Contains("connectionRoots", error.Message);
        }

        [Fact]
        public void ABrokenHalfIsSeenWhenReadingTheOther()
        {
            FormatException byTypes = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read(
                    "{\"connectionRoots\":5,\"types\":[" + Item("N.A", "dto") + "]}"));
            FormatException byRoots = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read("{" + Roots + ",\"types\":\"x\"}"));

            Assert.Contains("connectionRoots", byTypes.Message);
            Assert.Contains("types", byRoots.Message);
        }

        [Fact]
        public void ATableWithoutTheRootsStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read("{\"types\":[]}"));

            Assert.Contains("connectionRoots", error.Message);
        }

        [Fact]
        public void AnUnknownMemberAtTheRootStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read("{" + Roots + ",\"types\":[],\"note\":1}"));

            Assert.Contains("note", error.Message);
        }

        [Fact]
        public void AValueThatIsNotAStringStops()
        {
            FormatException byRoot = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read("{\"connectionRoots\":[5],\"types\":[]}"));
            FormatException byRole = Assert.Throws<FormatException>(
                () => ReadTypes("{\"typeName\":\"N.A\",\"role\":1,\"basis\":\"根拠。\"}"));

            Assert.Contains("文字列", byRoot.Message);
            Assert.Contains("文字列", byRole.Message);
        }

        [Fact]
        public void ARootWithoutTheTableStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read("{" + Roots + "}"));

            Assert.Contains("types", error.Message);
        }

        [Fact]
        public void AnItemThatIsNotAnObjectStops()
        {
            Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.Read("{" + Roots + ",\"types\":[\"N.A\"]}"));
        }

        [Fact]
        public void TextThatIsNotJsonStops()
        {
            Assert.Throws<FormatException>(() => TypeRoleTableJsonReader.Read("役割"));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => TypeRoleTableJsonReader.Read(null));
        }

        private static IList<TypeRoleRecord> ReadTypes(params string[] items)
        {
            return TypeRoleTableJsonReader.Read(
                "{" + Roots + ",\"types\":[" + string.Join(",", items) + "]}").Types;
        }

        private static string Item(string typeName, string role)
        {
            return "{\"typeName\":\"" + typeName + "\",\"role\":\"" + role
                + "\",\"basis\":\"" + typeName + " の根拠。\"}";
        }
    }
}
