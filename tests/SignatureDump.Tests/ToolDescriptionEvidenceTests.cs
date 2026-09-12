using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolDescriptionEvidenceTests
    {
        private const string Owner = "PEPlugin.Pmx.IPXVertex";

        private const string ListTool = "model_list_vertices";

        [Fact]
        public void AToolTakesItsTargetAndSourceFromTheRoleTableAndTheSignature()
        {
            ToolDescriptionMaterial material = Only(Map(Row("Draw", ListTool, null)));

            Assert.Equal(ListTool, material.Tool);
            Assert.Equal("model", material.Group);
            Assert.Equal("vertex", material.ElementNoun);
            Assert.Equal(Owner, material.TypeName);
        }

        [Fact]
        public void TheQualifierIsTheElementNounAtTheEndOfTheName()
        {
            Assert.Equal("vertices", Only(Map(Row("Draw", ListTool, null))).Qualifier);
            Assert.Equal("vertex", Only(Map(Row("Draw", "model_clear_vertex", null))).Qualifier);
        }

        [Fact]
        public void ANameWithoutTheElementNounHasNoQualifierAndKeepsTheWholeActionWord()
        {
            ToolDescriptionMaterial material = Only(Map(Row("Draw", "model_clear", null)));

            Assert.Null(material.Qualifier);
            Assert.Equal("clear", material.ActionWord);
        }

        [Fact]
        public void TheActionWordDropsTheGroupAndTheQualifier()
        {
            Assert.Equal("list", Only(Map(Row("Draw", ListTool, null))).ActionWord);
        }

        [Fact]
        public void ANameThatIsNothingButTheGroupAndTheElementNounKeepsItsActionWord()
        {
            ToolDescriptionMaterial material = Only(Map(Row("Draw", "model_vertex", null)));

            Assert.Null(material.Qualifier);
            Assert.Equal("vertex", material.ActionWord);
        }

        [Fact]
        public void TheContractNoteComesFromTheLedgerOfThatSignature()
        {
            ToolDescriptionMaterial material =
                Only(Map(Row("Draw", ListTool, null)), ContractNotes("Draw"));

            Assert.Equal("使うな。", material.ContractNote);
        }

        [Fact]
        public void TheSourceNoteComesFromTheDocumentOfThatMember()
        {
            Assert.Equal("頂点を描く", Only(Map(Row("Draw", ListTool, null))).SourceNote);
        }

        [Fact]
        public void TheSourceNoteOfAFieldComesFromTheSameDocumentAsAProperty()
        {
            IList<ToolDescriptionMaterial> materials = ToolDescriptionEvidence.Collect(
                Map(Row("Tint", "model_get_vertex", null)).Map,
                Roles(),
                Names(),
                Inventory(),
                Named(Key("Tint"), "model_get_vertex"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                MethodNotes(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Owner + ".Tint", "色合い" },
                });

            Assert.Equal("色合い", Assert.Single(materials).SourceNote);
        }

        [Fact]
        public void AMemberTheDocumentDoesNotCarryHasNoSourceNote()
        {
            Assert.Null(Only(Map(Row("Erase", ListTool, null))).SourceNote);
        }

        [Fact]
        public void TheNotesOfSeveralRowsOfOneToolAreJoinedWithoutRepeating()
        {
            ToolDescriptionMaterial material = Only(
                Map(Row("Draw", ListTool, null), Row("Erase", ListTool, null)),
                ContractNotes("Draw", "Erase"));

            Assert.Equal("使うな。", material.ContractNote);
        }

        [Fact]
        public void TheEmbeddedRowsBecomeTheIndexTerms()
        {
            ToolDescriptionMaterial material = Only(Map(
                Row("Draw", ListTool, null),
                Row("Index", null, new[] { ListTool })));

            Assert.Equal(new[] { "Index" }, material.IndexTerms.Select(t => t.Name).ToArray());
            Assert.Equal(
                new[] { "頂点の番号" }, material.IndexTerms.Select(t => t.JapaneseName).ToArray());
        }

        [Fact]
        public void AnEmbeddedRowOfAnotherToolIsNotAnIndexTerm()
        {
            IList<ToolDescriptionMaterial> materials = Collect(Map(
                Row("Draw", ListTool, null),
                Row("Index", null, new[] { "model_update_vertices" })));

            Assert.Empty(
                Assert.Single(materials, m => m.Tool == ListTool).IndexTerms);
        }

        /// <summary>正本に載らない項目の日本語名は、記載から採る。</summary>
        [Fact]
        public void TheIndexTermOfAnItemOutsideTheTableComesFromTheNote()
        {
            IList<ToolDescriptionMaterial> materials = ToolDescriptionEvidence.Collect(
                Map(Row("Draw", ListTool, null), Row("Depth", null, new[] { ListTool })).Map,
                Roles(),
                Names(),
                Inventory(),
                Named(Key("Draw"), ListTool),
                new Dictionary<string, string>(StringComparer.Ordinal),
                MethodNotes(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { Owner + ".Depth", "奥行き" },
                });

            Assert.Equal(
                new[] { "奥行き" },
                Assert.Single(materials).IndexTerms.Select(t => t.JapaneseName).ToArray());
        }

        [Fact]
        public void AnEmbeddedRowWithoutAJapaneseNameStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Collect(Map(
                    Row("Draw", ListTool, null),
                    Row("Depth", null, new[] { ListTool }))));

            Assert.Contains("日本語名が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmbeddedRowOfAnotherDeclaringTypeIsStillAnIndexTerm()
        {
            ToolDescriptionMaterial material = Only(Map(
                Row("Draw", ListTool, null), Embedded(Bone, "Index", ListTool)));

            Assert.Equal(new[] { "Index" }, material.IndexTerms.Select(t => t.Name).ToArray());
            Assert.Equal(Owner, material.TypeName);
        }

        [Fact]
        public void RowsOfOneToolThatPointAtDifferentDeclaringTypesStop()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Collect(Map(
                    Row("Draw", ListTool, null),
                    OtherType(ListTool))));

            Assert.Contains("違う宣言型を指している", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowKeyThatIsNotInTheInventoryStops()
        {
            const string key = "PEPlugin.Pmx.IPXVertex.Gone()";
            ToolMapRow row = new ToolMapRow(
                key,
                ToolMapEditKind.Read,
                null,
                "根拠。",
                null,
                null,
                null);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Collect(new Fixture(new ToolMap(new[] { row }), Named(key, ListTool))));

            Assert.Contains("配布物に無いシグネチャ", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolOnATypeTheRoleTableDoesNotCarryStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolDescriptionEvidence.Collect(
                    Map(Row("Draw", ListTool, null)).Map,
                    new TypeRoleTable(
                        new List<TypeRoleRecord>(),
                        new List<HandleIssuanceRecord>(),
                        new List<ElementCollectionRecord>()),
                    Names(),
                    Inventory(),
                    Named(Key("Draw"), ListTool),
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    MethodNotes(),
                    new Dictionary<string, string>(StringComparer.Ordinal)));

            Assert.Contains("型役割表に無い型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolOnATypeWithoutAnElementNounStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CollectWith(new TypeRoleRecord(
                    Owner,
                    TypeRole.OperationTarget,
                    "題材の根拠。",
                    string.Empty,
                    string.Empty,
                    CapabilityOwner.Model)));

            Assert.Contains("要素名詞を持たない型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolOnATypeWithoutAGroupStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CollectWith(new TypeRoleRecord(
                    Owner, TypeRole.Dto, "題材の根拠。", "vertex")));

            Assert.Contains("担当群を持たない型", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowWithoutAToolMakesNoMaterial()
        {
            Assert.Empty(Collect(Map(Row("Draw", null, null))));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            IDictionary<string, string> empty = new Dictionary<string, string>(StringComparer.Ordinal);
            ToolMap map = Map().Map;

            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    null, Roles(), Names(), Inventory(), empty, empty, MethodNotes(), empty));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    map, null, Names(), Inventory(), empty, empty, MethodNotes(), empty));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    map, Roles(), null, Inventory(), empty, empty, MethodNotes(), empty));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    map, Roles(), Names(), null, empty, empty, MethodNotes(), empty));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    map, Roles(), Names(), Inventory(), null, empty, MethodNotes(), empty));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    map, Roles(), Names(), Inventory(), empty, null, MethodNotes(), empty));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    map, Roles(), Names(), Inventory(), empty, empty, null, empty));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    map, Roles(), Names(), Inventory(), empty, empty, MethodNotes(), null));
        }

        /// <summary>
        /// 型役割表は総称型を引数の数で書き、列挙は型引数の名前で書くので、引き当ては同じ鍵へ写して
        /// から行う。写さずに引くと、正しく書いた行が「型役割表に無い」で落ちる。
        /// </summary>
        [Fact]
        public void AGenericDeclaringTypeIsFoundByItsDefinitionName()
        {
            const string Open = "PEPlugin.Vme.IPEValue<T>";
            const string Closed = "PEPlugin.Vme.IPEValue<1>";
            string key = SignatureKeyBuilder.Build(
                Open, "Draw", 0, new ParameterRecord[0], "System.Int32");
            ToolMapRow row = new ToolMapRow(
                key,
                ToolMapEditKind.Read,
                null,
                "根拠。",
                null,
                null,
                null);
            InventoryRecord inventory = new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord>(),
                new List<TypeRecord>(),
                new List<SignatureRecord>
                {
                    new SignatureRecord(
                        key,
                        Open,
                        MemberKind.Method,
                        "Draw",
                        false,
                        0,
                        new ParameterRecord[0],
                        "System.Int32",
                        true,
                        false,
                        OperationDirection.Read),
                });

            IList<ToolDescriptionMaterial> materials = ToolDescriptionEvidence.Collect(
                new ToolMap(new List<ToolMapRow> { row }),
                new TypeRoleTable(
                    new List<TypeRoleRecord>
                    {
                        new TypeRoleRecord(
                            Closed,
                            TypeRole.OperationTarget,
                            "題材の根拠。",
                            "value",
                            "values",
                            CapabilityOwner.Model),
                    },
                    new List<HandleIssuanceRecord>(),
                    new List<ElementCollectionRecord>()),
                Names(),
                inventory,
                Named(key, "model_draw_value"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal));

            Assert.Equal(Open, Assert.Single(materials).TypeName);
        }

        private static ToolDescriptionMaterial Only(Fixture fixture)
        {
            return Assert.Single(Collect(fixture));
        }

        private static ToolDescriptionMaterial Only(
            Fixture fixture, IDictionary<string, string> contractNotes)
        {
            return Assert.Single(Collect(fixture, contractNotes));
        }

        private static IList<ToolDescriptionMaterial> CollectWith(TypeRoleRecord role)
        {
            return ToolDescriptionEvidence.Collect(
                Map(Row("Draw", ListTool, null)).Map,
                new TypeRoleTable(
                    new List<TypeRoleRecord> { role },
                    new List<HandleIssuanceRecord>(),
                    new List<ElementCollectionRecord>()),
                Names(),
                Inventory(),
                Named(Key("Draw"), ListTool),
                new Dictionary<string, string>(StringComparer.Ordinal),
                MethodNotes(),
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static IList<ToolDescriptionMaterial> Collect(Fixture fixture)
        {
            return Collect(fixture, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static IList<ToolDescriptionMaterial> Collect(
            Fixture fixture, IDictionary<string, string> contractNotes)
        {
            return ToolDescriptionEvidence.Collect(
                fixture.Map,
                Roles(),
                Names(),
                Inventory(),
                fixture.ToolNames,
                contractNotes,
                MethodNotes(),
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static Fixture Map(params Written[] rows)
        {
            Dictionary<string, string> toolNames =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Written written in rows.Where(r => r.Tool != null))
            {
                toolNames[written.Row.SignatureKey] = written.Tool;
            }

            return new Fixture(new ToolMap(rows.Select(r => r.Row).ToList()), toolNames);
        }

        /// <summary>行キー1件にツールの名前を与えた表。</summary>
        private static IDictionary<string, string> Named(string signatureKey, string tool)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { signatureKey, tool },
            };
        }

        /// <summary>能力対応表と、行キーから引くツールの名前の組。名前は行が書かない。</summary>
        private sealed class Fixture
        {
            public Fixture(ToolMap map, IDictionary<string, string> toolNames)
            {
                Map = map;
                ToolNames = toolNames;
            }

            public ToolMap Map { get; }

            public IDictionary<string, string> ToolNames { get; }
        }

        /// <summary>行と、その行が持つツールの名前。</summary>
        private sealed class Written
        {
            public Written(ToolMapRow row, string tool)
            {
                Row = row;
                Tool = tool;
            }

            public ToolMapRow Row { get; }

            public string Tool { get; }
        }

        private static Written Row(string memberName, string tool, IList<string> embeddedIn)
        {
            bool property = !Methods.Contains(memberName);
            return new Written(
                new ToolMapRow(
                    Key(memberName),
                    ToolMapEditKind.Read,
                    null,
                    "根拠。",
                    null,
                    null,
                    embeddedIn),
                tool);
        }

        private static string Key(string memberName)
        {
            return SignatureKeyBuilder.Build(
                Owner, memberName, 0, new ParameterRecord[0], "System.Int32");
        }

        private static IDictionary<string, string> ContractNotes(params string[] memberNames)
        {
            Dictionary<string, string> notes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string memberName in memberNames)
            {
                notes.Add(Key(memberName), "使うな。");
            }

            return notes;
        }

        private static readonly string[] Methods = { "Draw", "Erase" };

        private static readonly string[] Fields = { "Tint" };

        /// <summary>題材のメンバーの種類。名前で決める。</summary>
        private static MemberKind Kind(string memberName)
        {
            if (Methods.Contains(memberName))
            {
                return MemberKind.Method;
            }

            return Fields.Contains(memberName) ? MemberKind.Field : MemberKind.Property;
        }

        private const string Bone = "PEPlugin.Pmx.IPXBone";

        /// <summary>宣言型だけが違う、埋め込みの行。</summary>
        private static Written Embedded(string declaringType, string memberName, string tool)
        {
            return new Written(
                new ToolMapRow(
                    SignatureKeyBuilder.Build(
                        declaringType, memberName, 0, new ParameterRecord[0], "System.Int32"),
                    ToolMapEditKind.Read,
                    null,
                    "根拠。",
                    null,
                    null,
                    new[] { tool }),
                null);
        }

        /// <summary>宣言型だけが違う行。</summary>
        private static Written OtherType(string tool)
        {
            return new Written(
                new ToolMapRow(
                    SignatureKeyBuilder.Build(
                        Bone, "Draw", 0, new ParameterRecord[0], "System.Int32"),
                    ToolMapEditKind.Read,
                    null,
                    "根拠。",
                    null,
                    null,
                    null),
                tool);
        }

        private static InventoryRecord Inventory()
        {
            List<SignatureRecord> signatures = new List<SignatureRecord>();
            foreach (string memberName in new[] { "Draw", "Erase", "Index", "Depth", "Tint" })
            {
                signatures.Add(Signature(memberName));
            }

            signatures.Add(new SignatureRecord(
                SignatureKeyBuilder.Build(Bone, "Index", 0, new ParameterRecord[0], "System.Int32"),
                Bone,
                MemberKind.Property,
                "Index",
                false,
                0,
                new ParameterRecord[0],
                "System.Int32",
                true,
                false,
                OperationDirection.Read));
            signatures.Add(new SignatureRecord(
                SignatureKeyBuilder.Build(Bone, "Draw", 0, new ParameterRecord[0], "System.Int32"),
                Bone,
                MemberKind.Method,
                "Draw",
                false,
                0,
                new ParameterRecord[0],
                "System.Int32",
                true,
                false,
                OperationDirection.Read));

            return new InventoryRecord(
                "PEPlugin", "0.0.0.0", new List<TypeRecord>(), new List<TypeRecord>(), signatures);
        }

        private static SignatureRecord Signature(string memberName)
        {
            return new SignatureRecord(
                SignatureKeyBuilder.Build(Owner, memberName, 0, new ParameterRecord[0], "System.Int32"),
                Owner,
                Kind(memberName),
                memberName,
                false,
                0,
                new ParameterRecord[0],
                "System.Int32",
                true,
                false,
                OperationDirection.Read);
        }

        private static IDictionary<string, string> MethodNotes()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { Owner + ".Draw", "頂点を描く" },
            };
        }

        private static TypeRoleTable Roles()
        {
            return new TypeRoleTable(
                new List<TypeRoleRecord>
                {
                    new TypeRoleRecord(
                        Owner,
                        TypeRole.OperationTarget,
                        "題材の根拠。",
                        "vertex",
                        "vertices",
                        CapabilityOwner.Model),
                },
                new List<HandleIssuanceRecord>(),
                new List<ElementCollectionRecord>());
        }

        private static IList<PropertyNameRecord> Names()
        {
            return new List<PropertyNameRecord>
            {
                new PropertyNameRecord(
                    Owner, "Index", "頂点の番号", NameBasis.FromMemberShape(), "起こした。"),
                new PropertyNameRecord(
                    Bone, "Index", "ボーンの番号", NameBasis.FromMemberShape(), "起こした。"),
            };
        }
    }
}
