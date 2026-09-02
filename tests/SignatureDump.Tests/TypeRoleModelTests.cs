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
        public void AQuotedRecordRequiresAPropertyAndAName()
        {
            Assert.Throws<ArgumentNullException>(() => PropertyNameRecord.FromQuoted(null, "大きさ"));
            Assert.Throws<ArgumentException>(() => PropertyNameRecord.FromQuoted(Size, " "));
        }

        [Fact]
        public void AnAuthoredRecordRequiresABasisAndAnOrigin()
        {
            Assert.Throws<ArgumentNullException>(
                () => PropertyNameRecord.FromAuthored(Size, "大きさ", null, "起こした。"));
            Assert.Throws<ArgumentException>(
                () => PropertyNameRecord.FromAuthored(
                    Size, "大きさ", NameBasis.FromMemberShape(), " "));
            Assert.Throws<ArgumentNullException>(
                () => PropertyNameRecord.FromAuthored(
                    null, "大きさ", NameBasis.FromMemberShape(), "起こした。"));
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
        public void ATypeRoleTableRequiresBothParts()
        {
            Assert.Throws<ArgumentNullException>(
                () => new TypeRoleTable(null, new List<TypeRoleRecord>()));
            Assert.Throws<ArgumentNullException>(
                () => new TypeRoleTable(new List<string>(), null));
        }

        [Fact]
        public void TheKeyJoinsTheThreePartsThatIdentifyAProperty()
        {
            Assert.Equal("N.IThing|Size|System.Int32", Size.Key);
        }
    }
}
