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
            ToolDescriptionMaterial material = Only(Map(Row("Draw", ListTool, null, null)));

            Assert.Equal(ListTool, material.Tool);
            Assert.Equal("model", material.Group);
            Assert.Equal("vertex", material.ElementNoun);
            Assert.Equal(Owner, material.TypeName);
        }

        [Fact]
        public void TheQualifierIsTheElementNounAtTheEndOfTheName()
        {
            Assert.Equal("vertices", Only(Map(Row("Draw", ListTool, null, null))).Qualifier);
            Assert.Equal("vertex", Only(Map(Row("Draw", "model_clear_vertex", null, null))).Qualifier);
        }

        [Fact]
        public void ANameWithoutTheElementNounHasNoQualifierAndKeepsTheWholeActionWord()
        {
            ToolDescriptionMaterial material = Only(Map(Row("Draw", "model_clear", null, null)));

            Assert.Null(material.Qualifier);
            Assert.Equal("clear", material.ActionWord);
        }

        [Fact]
        public void TheActionWordDropsTheGroupAndTheQualifier()
        {
            Assert.Equal("list", Only(Map(Row("Draw", ListTool, null, null))).ActionWord);
        }

        [Fact]
        public void ANameThatIsNothingButTheGroupAndTheElementNounKeepsItsActionWord()
        {
            ToolDescriptionMaterial material = Only(Map(Row("Draw", "model_vertex", null, null)));

            Assert.Null(material.Qualifier);
            Assert.Equal("vertex", material.ActionWord);
        }

        [Fact]
        public void TheContractNoteComesFromTheRow()
        {
            Assert.Equal("使うな。", Only(Map(Row("Draw", ListTool, "使うな。", null))).ContractNote);
        }

        [Fact]
        public void TheSourceNoteComesFromTheDocumentOfThatMember()
        {
            Assert.Equal("頂点を描く", Only(Map(Row("Draw", ListTool, null, null))).SourceNote);
        }

        [Fact]
        public void AMemberTheDocumentDoesNotCarryHasNoSourceNote()
        {
            Assert.Null(Only(Map(Row("Erase", ListTool, null, null))).SourceNote);
        }

        [Fact]
        public void TheNotesOfSeveralRowsOfOneToolAreJoinedWithoutRepeating()
        {
            ToolDescriptionMaterial material = Only(Map(
                Row("Draw", ListTool, "使うな。", null),
                Row("Erase", ListTool, "使うな。", null)));

            Assert.Equal("使うな。", material.ContractNote);
        }

        [Fact]
        public void TheEmbeddedRowsBecomeTheIndexTerms()
        {
            ToolDescriptionMaterial material = Only(Map(
                Row("Draw", ListTool, null, null),
                Row("Index", null, null, new[] { ListTool })));

            Assert.Equal(new[] { "Index" }, material.IndexTerms.Select(t => t.Name).ToArray());
            Assert.Equal(
                new[] { "頂点の番号" }, material.IndexTerms.Select(t => t.JapaneseName).ToArray());
        }

        [Fact]
        public void AnEmbeddedRowOfAnotherToolIsNotAnIndexTerm()
        {
            ToolDescriptionMaterial material = Only(Map(
                Row("Draw", ListTool, null, null),
                Row("Index", null, null, new[] { "model_update_vertices" })));

            Assert.Empty(material.IndexTerms);
        }

        [Fact]
        public void AnEmbeddedRowWithoutAJapaneseNameStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Collect(Map(
                    Row("Draw", ListTool, null, null),
                    Row("Depth", null, null, new[] { ListTool }))));

            Assert.Contains("日本語名が無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnEmbeddedRowOfAnotherDeclaringTypeIsStillAnIndexTerm()
        {
            ToolDescriptionMaterial material = Only(Map(
                Row("Draw", ListTool, null, null), Embedded(Bone, "Index", ListTool)));

            Assert.Equal(new[] { "Index" }, material.IndexTerms.Select(t => t.Name).ToArray());
            Assert.Equal(Owner, material.TypeName);
        }

        [Fact]
        public void RowsOfOneToolThatPointAtDifferentDeclaringTypesStop()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Collect(Map(
                    Row("Draw", ListTool, null, null),
                    OtherType(ListTool))));

            Assert.Contains("違う宣言型を指している", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowKeyThatIsNotInTheInventoryStops()
        {
            ToolMapRow row = new ToolMapRow(
                "PEPlugin.Pmx.IPXVertex.Gone()",
                new[] { "C1" },
                ToolMapRowKind.Composed,
                ToolMapEditKind.Read,
                OperationDirection.Read,
                null,
                null,
                null,
                "根拠。",
                ListTool,
                null,
                null,
                null,
                null,
                null,
                null);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Collect(new ToolMap(new[] { row })));

            Assert.Contains("配布物に無いシグネチャ", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AToolOnATypeTheRoleTableDoesNotCarryStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ToolDescriptionEvidence.Collect(
                    Map(Row("Draw", ListTool, null, null)),
                    new TypeRoleTable(
                        new List<TypeRoleRecord>(),
                        new List<HandleIssuanceRecord>(),
                        new List<ElementCollectionRecord>()),
                    Names(),
                    Inventory(),
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
                    string.Empty,
                    CapabilityOwner.Model,
                    new Dictionary<ToolVerb, string> { { ToolVerb.List, ListTool } })));

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
            Assert.Empty(Collect(Map(Row("Draw", null, null, null))));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    null, Roles(), Names(), Inventory(), MethodNotes(),
                    new Dictionary<string, string>(StringComparer.Ordinal)));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    Map(), null, Names(), Inventory(), MethodNotes(),
                    new Dictionary<string, string>(StringComparer.Ordinal)));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    Map(), Roles(), null, Inventory(), MethodNotes(),
                    new Dictionary<string, string>(StringComparer.Ordinal)));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    Map(), Roles(), Names(), null, MethodNotes(),
                    new Dictionary<string, string>(StringComparer.Ordinal)));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    Map(), Roles(), Names(), Inventory(), null,
                    new Dictionary<string, string>(StringComparer.Ordinal)));
            Assert.Throws<ArgumentNullException>(
                () => ToolDescriptionEvidence.Collect(
                    Map(), Roles(), Names(), Inventory(), MethodNotes(), null));
        }

        private static ToolDescriptionMaterial Only(ToolMap map)
        {
            return Assert.Single(Collect(map));
        }

        private static IList<ToolDescriptionMaterial> CollectWith(TypeRoleRecord role)
        {
            return ToolDescriptionEvidence.Collect(
                Map(Row("Draw", ListTool, null, null)),
                new TypeRoleTable(
                    new List<TypeRoleRecord> { role },
                    new List<HandleIssuanceRecord>(),
                    new List<ElementCollectionRecord>()),
                Names(),
                Inventory(),
                MethodNotes(),
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static IList<ToolDescriptionMaterial> Collect(ToolMap map)
        {
            return ToolDescriptionEvidence.Collect(
                map,
                Roles(),
                Names(),
                Inventory(),
                MethodNotes(),
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        private static ToolMap Map(params ToolMapRow[] rows)
        {
            return new ToolMap(rows.ToList());
        }

        private static ToolMapRow Row(
            string memberName, string tool, string note, IList<string> embeddedIn)
        {
            bool property = !Methods.Contains(memberName);
            return new ToolMapRow(
                SignatureKeyBuilder.Build(Owner, memberName, 0, new ParameterRecord[0], "System.Int32"),
                new[] { "C1" },
                embeddedIn == null ? ToolMapRowKind.Composed : ToolMapRowKind.SchemaEmbedded,
                ToolMapEditKind.Read,
                OperationDirection.Read,
                null,
                null,
                note,
                "根拠。",
                tool,
                null,
                null,
                null,
                null,
                null,
                embeddedIn);
        }

        private static readonly string[] Methods = { "Draw", "Erase" };

        private const string Bone = "PEPlugin.Pmx.IPXBone";

        /// <summary>宣言型だけが違う、埋め込みの行。</summary>
        private static ToolMapRow Embedded(string declaringType, string memberName, string tool)
        {
            return new ToolMapRow(
                SignatureKeyBuilder.Build(
                    declaringType, memberName, 0, new ParameterRecord[0], "System.Int32"),
                new[] { "C1" },
                ToolMapRowKind.SchemaEmbedded,
                ToolMapEditKind.Read,
                OperationDirection.Read,
                null,
                null,
                null,
                "根拠。",
                null,
                null,
                null,
                null,
                null,
                null,
                new[] { tool });
        }

        /// <summary>宣言型だけが違う行。</summary>
        private static ToolMapRow OtherType(string tool)
        {
            return new ToolMapRow(
                SignatureKeyBuilder.Build(Bone, "Draw", 0, new ParameterRecord[0], "System.Int32"),
                new[] { "C1" },
                ToolMapRowKind.Composed,
                ToolMapEditKind.Read,
                OperationDirection.Read,
                null,
                null,
                null,
                "根拠。",
                tool,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        private static InventoryRecord Inventory()
        {
            List<SignatureRecord> signatures = new List<SignatureRecord>();
            foreach (string memberName in new[] { "Draw", "Erase", "Index", "Depth" })
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
                Methods.Contains(memberName) ? MemberKind.Method : MemberKind.Property,
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
                        string.Empty,
                        CapabilityOwner.Model,
                        new Dictionary<ToolVerb, string>
                        {
                            { ToolVerb.List, ListTool },
                            { ToolVerb.Update, "model_update_vertices" },
                        }),
                },
                new List<HandleIssuanceRecord>(),
                new List<ElementCollectionRecord>());
        }

        private static IList<PropertyNameRecord> Names()
        {
            return new List<PropertyNameRecord>
            {
                PropertyNameRecord.FromQuoted(
                    new PropertyRecord(Owner, "Index", "System.Int32"), "頂点の番号"),
                PropertyNameRecord.FromQuoted(
                    new PropertyRecord(Bone, "Index", "System.Int32"), "ボーンの番号"),
            };
        }
    }
}
