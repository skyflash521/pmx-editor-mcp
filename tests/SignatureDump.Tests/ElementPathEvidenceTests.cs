using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>PMXから受け手へ至る道の導き方。</summary>
    public sealed class ElementPathEvidenceTests
    {
        private const string Pmx = "PEPlugin.Pmx.IPXPmx";

        private const string Vertex = "PEPlugin.Pmx.IPXVertex";

        private const string Header = "PEPlugin.Pmx.IPXHeader";

        private const string Morph = "PEPlugin.Pmx.IPXMorph";

        private const string Offset = "PEPlugin.Pmx.IPXMorphOffset";

        private const string MaterialOffset = "PEPlugin.Pmx.IPXMaterialMorphOffset";

        private const string VertexList = Pmx + ".Vertex()";

        private const string MorphList = Pmx + ".Morph()";

        private const string Bone = "PEPlugin.Pmx.IPXBone";

        private const string Ik = "PEPlugin.Pmx.IPXIK";

        private const string BoneList = Pmx + ".Bone()";

        private const string IkKey = Bone + ".IK()";

        private const string Label = "PEPlugin.Pmx.IPXLabel";

        private const string LabelOfPmx = Pmx + ".Label()";

        private const string LabelOfBone = Bone + ".Label()";

        private const string Part = "PEPlugin.Pmx.IPXPart";

        private const string Mid = "PEPlugin.Pmx.IPXMid";

        private const string Far = "PEPlugin.Pmx.IPXFar";

        private const string PartList = MaterialOffset + ".Parts()";

        private const string MidOfBone = Bone + ".Mid()";

        private const string FarOfMid = Mid + ".Far()";

        private const string OffsetList = Morph + ".Offsets()";

        private const string HeaderKey = Pmx + ".Header()";

        [Fact]
        public void TheModelItselfIsTheReceiverOfItsOwnPath()
        {
            AccessPath path = Resolve()[Pmx];

            Assert.Equal(AccessPathKind.Whole, path.Kind);
            Assert.Null(path.RowKey);
            Assert.Empty(path.Parents);
        }

        [Fact]
        public void AListTheModelHoldsItselfNeedsNoParent()
        {
            AccessPath path = Resolve()[Vertex];

            Assert.Equal(AccessPathKind.Element, path.Kind);
            Assert.Equal(VertexList, path.RowKey);
            Assert.Empty(path.Parents);
            Assert.Equal(Vertex, path.ElementType);
        }

        [Fact]
        public void AListUnderAnotherListCarriesTheOwningPathAsItsParents()
        {
            AccessPath path = Resolve()[Offset];

            Assert.Equal(AccessPathKind.Element, path.Kind);
            Assert.Equal(OffsetList, path.RowKey);
            Assert.Equal(new[] { MorphList }, path.Parents.ToArray());
        }

        [Fact]
        public void EveryConcreteTypeOfAnAbstractListIsReachedThroughThatList()
        {
            AccessPath path = Resolve()[MaterialOffset];

            Assert.Equal(OffsetList, path.RowKey);
            Assert.Equal(new[] { MorphList }, path.Parents.ToArray());
            Assert.Equal(MaterialOffset, path.ElementType);
        }

        [Fact]
        public void AChildTheModelHoldsOneOfIsReachedByThatProperty()
        {
            AccessPath path = Resolve()[Header];

            Assert.Equal(AccessPathKind.Child, path.Kind);
            Assert.Equal(HeaderKey, path.RowKey);
            Assert.Empty(path.Parents);
            Assert.False(path.Listed);
        }

        [Fact]
        public void AChildOfAnElementIsReachedThroughTheListThatHoldsIt()
        {
            AccessPath path = Resolve()[Ik];

            Assert.Equal(AccessPathKind.Element, path.Kind);
            Assert.Equal(IkKey, path.RowKey);
            Assert.False(path.Listed);
            Assert.Equal(new[] { BoneList }, path.Parents.ToArray());
        }

        [Fact]
        public void TheWayWithFewerStepsIsTheOneThatRemains()
        {
            AccessPath path = Resolve()[Label];

            Assert.Equal(AccessPathKind.Child, path.Kind);
            Assert.Equal(LabelOfPmx, path.RowKey);
            Assert.Empty(path.Parents);
        }

        [Fact]
        public void AWayFoundLaterStillWinsWhenItHasFewerSteps()
        {
            AccessPath path = Resolve()[Far];

            Assert.Equal(FarOfMid, path.RowKey);
            Assert.Equal(new[] { BoneList, MidOfBone }, path.Parents.ToArray());
        }

        [Fact]
        public void ATypeThatTheModelDoesNotReachHasNoPath()
        {
            Assert.False(Resolve().ContainsKey("PEPlugin.Form.IPEFormConnector"));
        }

        [Fact]
        public void OnlyTheStepsThatHoldAListAreWalkedAsLists()
        {
            IDictionary<string, SignatureRecord> signatures = Inventory().Signatures
                .ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);

            Assert.True(ElementPathEvidence.Listed(signatures, MorphList));
            Assert.False(ElementPathEvidence.Listed(signatures, HeaderKey));
            Assert.False(ElementPathEvidence.Listed(signatures, "知らない行"));
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => ElementPathEvidence.Resolve(null, Roles()));
            Assert.Throws<ArgumentNullException>(
                () => ElementPathEvidence.Resolve(Inventory(), null));
        }

        private static IDictionary<string, AccessPath> Resolve()
        {
            return ElementPathEvidence.Resolve(Inventory(), Roles());
        }

        private static InventoryRecord Inventory()
        {
            return new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new[]
                {
                    Type(Offset),
                    Type(MaterialOffset, Offset),
                    Type(Vertex),
                    Type(Bone),
                    Type(Ik),
                    Type(Label),
                    Type(Part),
                    Type(Mid),
                    Type(Far),
                    Type(Header),
                    Type(Morph),
                    Type(Pmx),
                }.ToList(),
                new List<TypeRecord>(),
                new[]
                {
                    List(Pmx, "Vertex", Vertex),
                    List(Pmx, "Bone", Bone),
                    Single(Bone, "IK", Ik),
                    Single(Bone, "Label", Label),
                    Single(Bone, "Mid", Mid),
                    Single(Mid, "Far", Far),
                    Single(Part, "Far", Far),
                    List(MaterialOffset, "Parts", Part),
                    Single(Pmx, "Label", Label),
                    List(Pmx, "Morph", Morph),
                    List(Morph, "Offsets", Offset),
                    Single(Pmx, "Header", Header),
                }.ToList());
        }

        private static TypeRoleTable Roles()
        {
            TypeRoleRecord[] types =
            {
                Role(Pmx, "pmx", "pmxes"),
                Role(Vertex, "vertex", "vertices"),
                Role(Bone, "bone", "bones"),
                Role(Ik, "ik", "iks"),
                Role(Label, "label", "labels"),
                Role(Part, "part", "parts"),
                Role(Mid, "mid", "mids"),
                Role(Far, "far", "fars"),
                Role(Header, "header", "headers"),
                Role(Morph, "morph", "morphs"),
                Role(Offset, "morph_offset", "morph_offsets"),
                Role(MaterialOffset, "material_morph_offset", "material_morph_offsets"),
            };
            ElementCollectionRecord[] collections =
            {
                new ElementCollectionRecord(VertexList, true, "題材。", new[] { VertexList }),
                new ElementCollectionRecord(BoneList, true, "題材。", new[] { BoneList }),
                new ElementCollectionRecord(MorphList, true, "題材。", new[] { MorphList }),
                new ElementCollectionRecord(
                    OffsetList, true, "題材。", new[] { MorphList, OffsetList }),
                new ElementCollectionRecord(
                    PartList, true, "題材。", new[] { MorphList, OffsetList, PartList }),
            };

            return new TypeRoleTable(types, new HandleIssuanceRecord[0], collections);
        }

        private static TypeRoleRecord Role(string typeName, string noun, string plural)
        {
            return new TypeRoleRecord(
                typeName, TypeRole.OperationTarget, "題材の根拠。", noun, plural);
        }

        private static TypeRecord Type(string name, params string[] baseTypes)
        {
            return new TypeRecord(
                name,
                TypeKind.Interface,
                false,
                false,
                false,
                baseTypes.ToList(),
                new List<string>());
        }

        private static SignatureRecord List(
            string declaringType, string memberName, string elementType)
        {
            return Property(
                declaringType,
                memberName,
                "System.Collections.Generic.IList<" + elementType + ">");
        }

        private static SignatureRecord Single(
            string declaringType, string memberName, string valueType)
        {
            return Property(declaringType, memberName, valueType);
        }

        private static SignatureRecord Property(
            string declaringType, string memberName, string valueType)
        {
            return new SignatureRecord(
                declaringType + "." + memberName + "()",
                declaringType,
                MemberKind.Property,
                memberName,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                true,
                false,
                OperationDirection.Read);
        }
    }
}
