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
        public void EveryGroupNameIsRead()
        {
            IList<TypeRoleRecord> records = ReadTypes(
                Grouped("N.A", "model"),
                Grouped("N.B", "motion"),
                Grouped("N.C", "session"),
                Grouped("N.D", "view"));

            Assert.Equal(
                new[]
                {
                    CapabilityOwner.Model,
                    CapabilityOwner.MotionTransform,
                    CapabilityOwner.Session,
                    CapabilityOwner.View,
                },
                records.Select(r => r.Group));
        }

        [Fact]
        public void ARoleWithoutAnIndependentToolHasNoGroup()
        {
            IList<TypeRoleRecord> records = ReadTypes(Item("N.A", "dto"), Item("N.B", "eventArgs"));

            Assert.Equal(
                new[] { CapabilityOwner.None, CapabilityOwner.None }, records.Select(r => r.Group));
        }

        [Fact]
        public void AnUnknownGroupStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadTypes(Grouped("N.A", "変形・モーション")));

            Assert.Contains("担当群", error.Message);
        }

        [Fact]
        public void ARoleWithAnIndependentToolWithoutAGroupStops()
        {
            foreach (string role in new[] { "connector", "handleTarget", "operationTarget" })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(
                        "{\"typeName\":\"N.A\",\"role\":\"" + role
                            + "\",\"basis\":\"根拠。\",\"elementNoun\":\"alpha\""
                            + ",\"elementNounPlural\":\"alphas\"}"));

                Assert.Contains("group", error.Message);
            }
        }

        [Fact]
        public void ARoleWithoutAnIndependentToolWithAGroupStops()
        {
            foreach (string role in new[] { "dto", "eventArgs" })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(
                        "{\"typeName\":\"N.A\",\"role\":\"" + role
                            + "\",\"basis\":\"根拠。\",\"group\":\"model\"}"));

                Assert.Contains("group", error.Message);
            }
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
                () => TypeRoleTableJsonReader.ReadTypeRoles(
                    "{\"types\":[\"N.A\"],\"issuances\":[],\"collections\":[]}"));

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
        public void TheConnectionPathIsRead()
        {
            IList<TypeRoleRecord> records = ReadTypes(
                Noun("N.A", "connector",
                    "\"elementNoun\":\"alpha\",\"connectionPath\":\"Host.Alpha\""),
                Noun("N.B", "connector", "\"elementNoun\":\"beta\""));

            Assert.Equal(new[] { "Host.Alpha", string.Empty }, records.Select(r => r.ConnectionPath));
        }

        [Fact]
        public void ARoleThatIsNotAConnectorWithAConnectionPathStops()
        {
            foreach (string nouns in new[]
            {
                "\"role\":\"eventArgs\"",
                "\"role\":\"dto\"",
                "\"role\":\"handleTarget\",\"elementNoun\":\"alpha\""
                    + ",\"elementNounPlural\":\"alphas\",\"group\":\"model\"",
                "\"role\":\"operationTarget\",\"elementNoun\":\"alpha\""
                    + ",\"elementNounPlural\":\"alphas\",\"group\":\"model\"",
            })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(
                        "{\"typeName\":\"N.A\"," + nouns
                            + ",\"basis\":\"根拠。\",\"connectionPath\":\"Host.Alpha\"}"));

                Assert.Contains("connectionPath", error.Message);
            }
        }

        [Fact]
        public void AConnectionPathThatIsBlankStops()
        {
            foreach (string path in new[] { string.Empty, "  " })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => ReadTypes(Noun(
                        "N.A",
                        "connector",
                        "\"elementNoun\":\"alpha\",\"connectionPath\":\"" + path + "\"")));

                Assert.Contains("connectionPath", error.Message);
            }
        }

        [Fact]
        public void AnEmptyTableIsRead()
        {
            TypeRoleTable table = TypeRoleTableJsonReader.ReadTypeRoles(
                "{\"types\":[],\"issuances\":[],\"collections\":[]}");

            Assert.Empty(table.Types);
            Assert.Empty(table.Issuances);
        }

        [Fact]
        public void ATableWithoutTheTypesStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles("{\"issuances\":[],\"collections\":[]}"));

            Assert.Contains("types", error.Message);
        }

        [Fact]
        public void TypesThatAreNotAListStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles(
                    "{\"types\":\"x\",\"issuances\":[],\"collections\":[]}"));

            Assert.Contains("types", error.Message);
        }

        [Fact]
        public void AnUnknownMemberAtTheRootStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles(
                    "{\"types\":[],\"issuances\":[],\"collections\":[],\"note\":1}"));

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
        public void AnIssuanceIsReadWithItsKindAndBasis()
        {
            IList<HandleIssuanceRecord> records = ReadIssuances(
                Issuance("N.A.Make()", "\"issues\":true,\"kind\":\"factory\""),
                Issuance("N.B.Get()", "\"issues\":false"));

            Assert.Equal(new[] { "N.A.Make()", "N.B.Get()" }, records.Select(r => r.SignatureKey));
            Assert.Equal(new[] { true, false }, records.Select(r => r.Issues));
            Assert.Equal(HandleIssuanceKind.Factory, records[0].Kind);
            Assert.Null(records[1].Kind);
            Assert.Equal("N.B.Get() の根拠。", records[1].Basis);
        }

        [Fact]
        public void EveryIssuanceKindNameIsRead()
        {
            IList<HandleIssuanceRecord> records = ReadIssuances(
                Issuance("N.A", "\"issues\":true,\"kind\":\"constructor\""),
                Issuance("N.B", "\"issues\":true,\"kind\":\"factory\""),
                Issuance("N.C", "\"issues\":true,\"kind\":\"receiverBound\""));

            Assert.Equal(
                new HandleIssuanceKind?[]
                {
                    HandleIssuanceKind.Constructor,
                    HandleIssuanceKind.Factory,
                    HandleIssuanceKind.ReceiverBound,
                },
                records.Select(r => r.Kind));
        }

        [Fact]
        public void IssuancesOutOfOrdinalOrderStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadIssuances(
                    Issuance("N.B", "\"issues\":false"), Issuance("N.A", "\"issues\":false")));

            Assert.Contains("昇順", error.Message);
        }

        [Fact]
        public void TheSameIssuanceTwiceStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadIssuances(
                    Issuance("N.A", "\"issues\":false"), Issuance("N.A", "\"issues\":false")));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void AnIssuanceThatIssuesWithoutAKindStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadIssuances(Issuance("N.A", "\"issues\":true")));

            Assert.Contains("kind", error.Message);
        }

        [Fact]
        public void AnIssuanceThatDoesNotIssueWithAKindStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadIssuances(
                    Issuance("N.A", "\"issues\":false,\"kind\":\"factory\"")));

            Assert.Contains("kind", error.Message);
        }

        [Fact]
        public void AnUnknownIssuanceKindStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadIssuances(Issuance("N.A", "\"issues\":true,\"kind\":\"builder\"")));

            Assert.Contains("builder", error.Message);
        }

        [Fact]
        public void AnIssuanceFlagThatIsNotABooleanStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadIssuances(Issuance("N.A", "\"issues\":\"true\"")));

            Assert.Contains("issues", error.Message);
        }

        [Fact]
        public void AnIssuanceWithoutTheFlagStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadIssuances("{\"signatureKey\":\"N.A\",\"basis\":\"根拠。\"}"));

            Assert.Contains("issues", error.Message);
        }

        [Fact]
        public void ATableWithoutTheIssuancesStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles("{\"types\":[],\"collections\":[]}"));

            Assert.Contains("issuances", error.Message);
        }

        [Fact]
        public void ACollectionIsReadWithItsOwnershipAndBasis()
        {
            IList<ElementCollectionRecord> records = ReadCollections(
                Collection("N.A.Items()", true), Collection("N.B.Refs()", false));

            Assert.Equal(new[] { "N.A.Items()", "N.B.Refs()" }, records.Select(r => r.SignatureKey));
            Assert.Equal(new[] { true, false }, records.Select(r => r.Owns));
            Assert.Equal("N.B.Refs() の根拠。", records[1].Basis);
        }

        [Fact]
        public void CollectionsOutOfOrdinalOrderStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadCollections(Collection("N.B", true), Collection("N.A", true)));

            Assert.Contains("昇順", error.Message);
        }

        [Fact]
        public void TheSameCollectionTwiceStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadCollections(Collection("N.A", true), Collection("N.A", true)));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void TheOwnerPathIsRead()
        {
            IList<ElementCollectionRecord> records = ReadCollections(
                "{\"signatureKey\":\"N.A.Items()\",\"owns\":true"
                    + ",\"ownerPath\":[\"N.R.As()\",\"N.A.Items()\"],\"basis\":\"根拠。\"}",
                Collection("N.B.Refs()", false));

            Assert.Equal(new[] { "N.R.As()", "N.A.Items()" }, records[0].OwnerPath);
            Assert.Empty(records[1].OwnerPath);
        }

        [Fact]
        public void AnOwningCollectionWithoutAnOwnerPathStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadCollections(
                    "{\"signatureKey\":\"N.A.Items()\",\"owns\":true,\"basis\":\"根拠。\"}"));

            Assert.Contains("ownerPath", error.Message);
        }

        [Fact]
        public void AReferencingCollectionWithAnOwnerPathStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadCollections(
                    "{\"signatureKey\":\"N.B.Refs()\",\"owns\":false"
                        + ",\"ownerPath\":[\"N.B.Refs()\"],\"basis\":\"根拠。\"}"));

            Assert.Contains("ownerPath", error.Message);
        }

        [Fact]
        public void AnEmptyOwnerPathStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadCollections(
                    "{\"signatureKey\":\"N.A.Items()\",\"owns\":true"
                        + ",\"ownerPath\":[],\"basis\":\"根拠。\"}"));

            Assert.Contains("ownerPath", error.Message);
        }

        [Fact]
        public void AnOwnerPathStageThatIsNotAStringStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadCollections(
                    "{\"signatureKey\":\"N.A.Items()\",\"owns\":true"
                        + ",\"ownerPath\":[5],\"basis\":\"根拠。\"}"));

            Assert.Contains("ownerPath", error.Message);
        }

        [Fact]
        public void AnOwnershipFlagThatIsNotABooleanStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadCollections(
                    "{\"signatureKey\":\"N.A\",\"owns\":\"true\",\"basis\":\"根拠。\"}"));

            Assert.Contains("owns", error.Message);
        }

        [Fact]
        public void ACollectionWithoutTheFlagStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ReadCollections("{\"signatureKey\":\"N.A\",\"basis\":\"根拠。\"}"));

            Assert.Contains("owns", error.Message);
        }

        [Fact]
        public void ATableWithoutTheCollectionsStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => TypeRoleTableJsonReader.ReadTypeRoles(
                    "{\"types\":[],\"issuances\":[]}"));

            Assert.Contains("collections", error.Message);
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
            return Read(items, new string[0]).Types;
        }

        private static IList<HandleIssuanceRecord> ReadIssuances(params string[] items)
        {
            return Read(new string[0], items).Issuances;
        }

        private static IList<ElementCollectionRecord> ReadCollections(params string[] items)
        {
            return TypeRoleTableJsonReader.ReadTypeRoles(
                "{\"types\":[],\"issuances\":[],\"collections\":["
                    + string.Join(",", items) + "]}").Collections;
        }

        private static TypeRoleTable Read(string[] types, string[] issuances)
        {
            return TypeRoleTableJsonReader.ReadTypeRoles(
                "{\"types\":[" + string.Join(",", types)
                    + "],\"issuances\":[" + string.Join(",", issuances)
                    + "],\"collections\":[]}");
        }

        private static string Grouped(string typeName, string group)
        {
            return "{\"typeName\":\"" + typeName + "\",\"role\":\"connector\",\"basis\":\""
                + typeName + " の根拠。\",\"elementNoun\":\"" + typeName.Substring(2).ToLowerInvariant()
                + "\",\"group\":\"" + group + "\"}";
        }

        private static string Item(string typeName, string role)
        {
            return "{\"typeName\":\"" + typeName + "\",\"role\":\"" + role
                + "\",\"basis\":\"" + typeName + " の根拠。\"}";
        }

        private static string Collection(string signatureKey, bool owns)
        {
            return "{\"signatureKey\":\"" + signatureKey + "\",\"owns\":"
                + (owns ? "true" : "false")
                + (owns ? ",\"ownerPath\":[\"" + signatureKey + "\"]" : string.Empty)
                + ",\"basis\":\"" + signatureKey + " の根拠。\"}";
        }

        private static string Issuance(string signatureKey, string flag)
        {
            return "{\"signatureKey\":\"" + signatureKey + "\"," + flag
                + ",\"basis\":\"" + signatureKey + " の根拠。\"}";
        }

        /// <summary>担当群は独立したツールを持つ役割が必ず持つので、題材の側で補う。</summary>
        private static string Noun(string typeName, string role, string nouns)
        {
            string group = role == "eventArgs" || role == "dto"
                ? string.Empty
                : ",\"group\":\"model\"";

            return "{\"typeName\":\"" + typeName + "\",\"role\":\"" + role
                + "\",\"basis\":\"" + typeName + " の根拠。\"," + nouns + group + "}";
        }
    }
}
