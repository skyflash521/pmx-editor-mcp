using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeRoleModelTests
    {
        private static readonly PropertyRecord Size =
            new PropertyRecord("N.IThing", "Size", "System.Int32");

        [Fact]
        public void ADocumentSectionKeepsItsPathAndLines()
        {
            NameBasis basis = NameBasis.FromDocumentSection("doc/spec.txt", 4, 9);

            Assert.Equal(NameBasisKind.DocumentSection, basis.Kind);
            Assert.Equal("doc/spec.txt", basis.Path);
            Assert.Equal(4, basis.FirstLine);
            Assert.Equal(9, basis.LastLine);
        }

        [Fact]
        public void ADocumentSectionOfOneLineIsAllowed()
        {
            NameBasis basis = NameBasis.FromDocumentSection("doc/spec.txt", 4, 4);

            Assert.Equal(4, basis.FirstLine);
            Assert.Equal(4, basis.LastLine);
        }

        [Fact]
        public void ADocumentSectionRequiresAResolvablePlace()
        {
            Assert.Throws<ArgumentNullException>(() => NameBasis.FromDocumentSection(null, 1, 1));
            Assert.Throws<ArgumentException>(() => NameBasis.FromDocumentSection(" ", 1, 1));
        }

        [Fact]
        public void ALineBeforeTheFirstOneStops()
        {
            Assert.Throws<ArgumentException>(() => NameBasis.FromDocumentSection("doc/spec.txt", 0, 1));
        }

        [Fact]
        public void ALastLineBeforeTheFirstStops()
        {
            Assert.Throws<ArgumentException>(() => NameBasis.FromDocumentSection("doc/spec.txt", 5, 4));
        }

        [Fact]
        public void AMemberShapeHasNoPlace()
        {
            NameBasis basis = NameBasis.FromMemberShape();

            Assert.Equal(NameBasisKind.MemberShape, basis.Kind);
            Assert.Equal(string.Empty, basis.Path);
            Assert.Equal(0, basis.FirstLine);
            Assert.Equal(0, basis.LastLine);
        }

        [Fact]
        public void ANameRecordRequiresTheItemTheNameTheBasisAndTheOrigin()
        {
            Assert.Throws<ArgumentNullException>(() => Authored(null, "Size", "大きさ"));
            Assert.Throws<ArgumentException>(() => Authored(" ", "Size", "大きさ"));
            Assert.Throws<ArgumentNullException>(() => Authored("N.IThing", null, "大きさ"));
            Assert.Throws<ArgumentException>(() => Authored("N.IThing", "Size", " "));
            Assert.Throws<ArgumentNullException>(
                () => new PropertyNameRecord("N.IThing", "Size", "大きさ", null, "起こした。"));
            Assert.Throws<ArgumentException>(
                () => new PropertyNameRecord(
                    "N.IThing", "Size", "大きさ", NameBasis.FromMemberShape(), " "));
        }

        [Fact]
        public void ANameRecordKeepsTheItemAsItsKey()
        {
            Assert.Equal("N.IThing|Size", Authored("N.IThing", "Size", "大きさ").Key);
        }

        private static PropertyNameRecord Authored(
            string declaringType, string memberName, string japaneseName)
        {
            return new PropertyNameRecord(
                declaringType, memberName, japaneseName, NameBasis.FromMemberShape(), "起こした。");
        }

        [Fact]
        public void ATypeRoleRecordRequiresANameAndABasis()
        {
            Assert.Throws<ArgumentNullException>(
                () => new TypeRoleRecord(null, TypeRole.Dto, "根拠。"));
            Assert.Throws<ArgumentException>(
                () => new TypeRoleRecord(" ", TypeRole.Dto, "根拠。"));
            Assert.Throws<ArgumentNullException>(
                () => new TypeRoleRecord("N.IThing", TypeRole.Dto, null));
            Assert.Throws<ArgumentException>(
                () => new TypeRoleRecord("N.IThing", TypeRole.Dto, " "));
        }

        [Fact]
        public void AHandleIssuanceRequiresItsKeyAndBasis()
        {
            Assert.Throws<ArgumentNullException>(
                () => new HandleIssuanceRecord(null, false, "根拠。"));
            Assert.Throws<ArgumentException>(
                () => new HandleIssuanceRecord(" ", false, "根拠。"));
            Assert.Throws<ArgumentException>(
                () => new HandleIssuanceRecord("N.A.Get()", false, " "));
        }

        [Fact]
        public void ATypeRoleTableRequiresEveryPart()
        {
            Assert.Throws<ArgumentNullException>(
                () => new TypeRoleTable(
                    null, new List<HandleIssuanceRecord>(), new List<ElementCollectionRecord>()));
            Assert.Throws<ArgumentNullException>(
                () => new TypeRoleTable(
                    new List<TypeRoleRecord>(), null, new List<ElementCollectionRecord>()));
            Assert.Throws<ArgumentNullException>(
                () => new TypeRoleTable(
                    new List<TypeRoleRecord>(), new List<HandleIssuanceRecord>(), null));
        }

        [Fact]
        public void AnElementCollectionRequiresItsKeyAndBasis()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ElementCollectionRecord(null, false, "根拠。"));
            Assert.Throws<ArgumentException>(
                () => new ElementCollectionRecord(" ", false, "根拠。"));
            Assert.Throws<ArgumentException>(
                () => new ElementCollectionRecord("N.A.Items()", false, " "));
            Assert.Throws<ArgumentException>(
                () => new ElementCollectionRecord("N.A.Items()", true, "根拠。"));
            Assert.Throws<ArgumentException>(
                () => new ElementCollectionRecord(
                    "N.A.Refs()", false, "根拠。", new List<string> { "N.A.Refs()" }));
        }

        [Fact]
        public void TheKeyJoinsTheThreePartsThatIdentifyAProperty()
        {
            Assert.Equal("N.IThing|Size|System.Int32", Size.Key);
        }
    }
}
