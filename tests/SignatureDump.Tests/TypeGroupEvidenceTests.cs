using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>型の担当群を台帳から導く範囲を固定する。</summary>
    public sealed class TypeGroupEvidenceTests
    {
        private const string Thing = "N.IThing";

        [Fact]
        public void ATypeWhoseSignaturesOneRowCoversTakesThatRowsOwner()
        {
            IDictionary<string, ISet<CapabilityOwner>> owners = OwnersByType(
                Row("CAP-001", "IThing", CapabilityOwner.View));

            Assert.Equal(new[] { CapabilityOwner.View }, owners[Thing]);
        }

        [Fact]
        public void ATypeWhoseSignaturesTwoRowsCoverTakesBothOwners()
        {
            IDictionary<string, ISet<CapabilityOwner>> owners = OwnersByType(
                Row("CAP-001", "IThing", CapabilityOwner.View),
                Row("CAP-002", "IThing.Update", CapabilityOwner.Model));

            Assert.Equal(
                new[] { CapabilityOwner.Model, CapabilityOwner.View },
                owners[Thing].OrderBy(o => o.ToString(), StringComparer.Ordinal));
        }

        [Fact]
        public void ATypeCoveredOnlyByRowsWithoutAnOwnerTakesNone()
        {
            IDictionary<string, ISet<CapabilityOwner>> owners = OwnersByType(
                Unsupported("CAP-001", "IThing"));

            Assert.Empty(owners[Thing]);
        }

        [Fact]
        public void ABaseTypeTakesTheOwnerOfARowThatCoversItsSignatures()
        {
            IDictionary<string, ISet<CapabilityOwner>> owners = OwnersByType(
                Row("CAP-001", "IDerived", CapabilityOwner.Session));

            Assert.Equal(new[] { CapabilityOwner.Session }, owners["N.IBase"]);
        }

        [Fact]
        public void ATypeThatDeclaresNothingTakesTheOwnerOfARowThatNamesIt()
        {
            IDictionary<string, ISet<CapabilityOwner>> owners = OwnersByType(
                Row("CAP-001", "IEmpty", CapabilityOwner.Model));

            Assert.Equal(new[] { CapabilityOwner.Model }, owners["N.IEmpty"]);
        }

        [Fact]
        public void ARowThatCoversOnlyADerivedMemberGivesTheBaseTypeNothing()
        {
            IDictionary<string, ISet<CapabilityOwner>> owners = OwnersByType(
                Row("CAP-001", "IDerived.Update", CapabilityOwner.Session));

            Assert.Equal(new[] { CapabilityOwner.Session }, owners["N.IDerived"]);
            Assert.False(owners.ContainsKey("N.IBase"));
        }

        [Fact]
        public void AGenericTypeIsKeyedByTheNumberOfItsTypeArguments()
        {
            IDictionary<string, ISet<CapabilityOwner>> owners = OwnersByType(
                Row("CAP-001", "IValue", CapabilityOwner.MotionTransform));

            Assert.Equal(new[] { CapabilityOwner.MotionTransform }, owners["N.IValue<1>"]);
        }

        [Fact]
        public void ATypeWhoseSignaturesNoRowCoversIsNotInTheTable()
        {
            IDictionary<string, ISet<CapabilityOwner>> owners = OwnersByType(
                Row("CAP-001", "IOther", CapabilityOwner.View));

            Assert.False(owners.ContainsKey(Thing));
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => TypeGroupEvidence.OwnersByType(null, Inventory()));
            Assert.Throws<ArgumentNullException>(
                () => TypeGroupEvidence.OwnersByType(new List<CapabilityRecord>(), null));
        }

        private static IDictionary<string, ISet<CapabilityOwner>> OwnersByType(
            params CapabilityRecord[] rows)
        {
            List<CapabilityRecord> ledger = new List<CapabilityRecord>(rows)
            {
                Pattern("CAP-463"),
                Pattern("CAP-466"),
            };

            return TypeGroupEvidence.OwnersByType(ledger, Inventory());
        }

        private static InventoryRecord Inventory()
        {
            return new InventoryRecord(
                "Sample",
                "1.0.0.0",
                new List<TypeRecord>
                {
                    Type(Thing),
                    Type("N.IOther"),
                    Type("N.IBase"),
                    Type("N.IDerived", "N.IBase"),
                    Type("N.IValue<TValue>"),
                    Type("N.IEmpty"),
                },
                new List<TypeRecord>(),
                new List<SignatureRecord>
                {
                    Signature(Thing, "Update"),
                    Signature("N.IOther", "Update"),
                    Signature("N.IBase", "Visible"),
                    Signature("N.IDerived", "Update"),
                    Signature("N.IValue<TValue>", "Get"),
                });
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

        private static SignatureRecord Signature(string declaringType, string memberName)
        {
            return new SignatureRecord(
                declaringType + "." + memberName + "()",
                declaringType,
                MemberKind.Method,
                memberName,
                false,
                0,
                new List<ParameterRecord>(),
                "System.Void",
                false,
                false,
                OperationDirection.Read);
        }

        private static CapabilityRecord Row(string id, string target, CapabilityOwner owner)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                target,
                CapabilityTargetKind.Single,
                new List<string> { target },
                CapabilityStatus.Provided,
                owner,
                string.Empty);
        }

        private static CapabilityRecord Unsupported(string id, string target)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                target,
                CapabilityTargetKind.Single,
                new List<string> { target },
                CapabilityStatus.NotSupported,
                CapabilityOwner.None,
                string.Empty);
        }

        private static CapabilityRecord Pattern(string id)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                "N.Absent.* のまとめ",
                CapabilityTargetKind.Pattern,
                new List<string>(),
                CapabilityStatus.NotSupported,
                CapabilityOwner.None,
                string.Empty);
        }
    }
}
