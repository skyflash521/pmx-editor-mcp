using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>自分自身へ至る道を持たない型が通れる、実装側の型の候補を台帳から導く範囲。</summary>
    public sealed class ReceiverRouteEvidenceTests
    {
        private const string Base = "N.IBase";

        private const string Derived = "N.IDerived";

        [Fact]
        public void ARowThatNamesTheDerivedTypesMemberMakesItACandidateForTheBaseType()
        {
            Assert.Equal(new[] { Derived }, Candidates(Row("CAP-001", "IDerived.Visible"))[Base]);
        }

        [Fact]
        public void TwoRowsThatNameDifferentDerivedTypesLeaveBothCandidates()
        {
            IDictionary<string, ISet<string>> candidates = Candidates(
                Row("CAP-001", "IDerived.Visible"), Row("CAP-002", "IOther.Visible"));

            Assert.Equal(
                new[] { Derived, "N.IOther" },
                candidates[Base].OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void ARowThatNamesTheBaseTypeItselfMakesNoCandidate()
        {
            Assert.False(Candidates(Row("CAP-001", "IBase.Visible")).ContainsKey(Base));
        }

        [Fact]
        public void ARowThatNamesTheDerivedTypesOwnMemberMakesNoCandidate()
        {
            Assert.False(Candidates(Row("CAP-001", "IDerived.Update")).ContainsKey(Base));
        }

        [Fact]
        public void ARowThatNamesTheDerivedTypeWholeMakesNoCandidate()
        {
            Assert.False(Candidates(Row("CAP-001", "IDerived")).ContainsKey(Base));
        }

        [Fact]
        public void ATypeThatNoRowNamesIsNotInTheTable()
        {
            Assert.Empty(Candidates(Row("CAP-001", "IUnrelated.Alone")));
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => ReceiverRouteEvidence.Candidates(null, new List<CapabilityRecord>()));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverRouteEvidence.Candidates(Inventory(), null));
        }

        private static IDictionary<string, ISet<string>> Candidates(params CapabilityRecord[] rows)
        {
            List<CapabilityRecord> ledger = new List<CapabilityRecord>(rows)
            {
                Pattern("CAP-463"),
                Pattern("CAP-466"),
            };

            return ReceiverRouteEvidence.Candidates(Inventory(), ledger);
        }

        private static InventoryRecord Inventory()
        {
            return new InventoryRecord(
                "Sample",
                "1.0.0.0",
                new List<TypeRecord>
                {
                    Type(Base),
                    Type(Derived, Base),
                    Type("N.IOther", Base),
                    Type("N.IUnrelated"),
                },
                new List<TypeRecord>(),
                new List<SignatureRecord>
                {
                    Signature(Base, "Visible"),
                    Signature(Derived, "Update"),
                    Signature("N.IUnrelated", "Alone"),
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

        private static CapabilityRecord Row(string id, string target)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                target,
                CapabilityTargetKind.Single,
                new List<string> { target },
                CapabilityStatus.Provided,
                CapabilityOwner.View,
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
