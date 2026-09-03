using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerPopulationTests
    {
        [Fact]
        public void RowNamedByTypeCoversAllPublicSignaturesOfThatTypeAndItsBases()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.IBase", TypeKind.Interface),
                        Type("N.IDerived", TypeKind.Interface, "N.IBase")),
                    Signatures(
                        Signature("N.IBase.Visible()", "N.IBase", "Visible"),
                        Signature("N.IDerived.Update()", "N.IDerived", "Update"))),
                Row("CAP-001", "IDerived"));

            Assert.Equal(
                new[] { "N.IBase.Visible()", "N.IDerived.Update()" },
                population.Signatures.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Contains("N.IDerived", population.Types);
        }

        [Fact]
        public void BaseTypesOfTheResolvedTypeAreIncludedInTheTypeSet()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.IBase", TypeKind.Interface),
                        Type("N.IMiddle", TypeKind.Interface, "N.IBase"),
                        Type("N.IDerived", TypeKind.Interface, "N.IMiddle", "N.IBase")),
                    Signatures(Signature("N.IBase.Visible()", "N.IBase", "Visible"))),
                Row("CAP-001", "IDerived"));

            Assert.Equal(
                new[] { "N.IBase", "N.IDerived", "N.IMiddle" },
                population.Types.OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void BaseTypesAreIncludedInTheTypeSetForMemberNamedRowsToo()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.IBase", TypeKind.Interface),
                        Type("N.IDerived", TypeKind.Interface, "N.IBase")),
                    Signatures(Signature("N.IBase.Visible()", "N.IBase", "Visible"))),
                Row("CAP-001", "IDerived.Visible"));

            Assert.Contains("N.IBase", population.Types);
            Assert.Contains("N.IDerived", population.Types);
        }

        [Fact]
        public void BaseNamesOutsideTheTargetAssemblyAreNotIncludedInTheTypeSet()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(Type("N.IDerived", TypeKind.Interface, "System.ICloneable", "N.IMissing<System.Int32>")),
                    Signatures(Signature("N.IDerived.Run()", "N.IDerived", "Run"))),
                Row("CAP-001", "IDerived"));

            Assert.Equal(new[] { "N.IDerived" }, population.Types.ToArray());
        }

        [Fact]
        public void RowNamedByTypeAndMemberCoversOnlySignaturesWithThatName()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.IBase", TypeKind.Interface),
                        Type("N.IDerived", TypeKind.Interface, "N.IBase")),
                    Signatures(
                        Signature("N.IBase.Visible()", "N.IBase", "Visible"),
                        Signature("N.IBase.Focus()", "N.IBase", "Focus"),
                        Signature("N.IDerived.Update()", "N.IDerived", "Update"))),
                Row("CAP-001", "IDerived.Visible"));

            Assert.Equal(new[] { "N.IBase.Visible()" }, population.Signatures.ToArray());
        }

        [Fact]
        public void BothFullyQualifiedAndNamespaceStrippedNamesResolve()
        {
            IList<TypeRecord> types = Types(Type("N.M.IThing", TypeKind.Interface));
            IList<SignatureRecord> signatures =
                Signatures(Signature("N.M.IThing.Run()", "N.M.IThing", "Run"));

            Assert.Single(Resolve(Inventory(types, signatures), Row("CAP-001", "N.M.IThing")).Signatures);
            Assert.Single(Resolve(Inventory(types, signatures), Row("CAP-001", "IThing")).Signatures);
        }

        [Fact]
        public void NestedTypesResolveByDotSeparatedName()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.Outer", TypeKind.Class),
                        Type("N.Outer+Inner", TypeKind.Class)),
                    Signatures(Signature("N.Outer+Inner.X()", "N.Outer+Inner", "X"))),
                Row("CAP-001", "Outer.Inner"));

            Assert.Equal(new[] { "N.Outer+Inner.X()" }, population.Signatures.ToArray());
            Assert.Contains("N.Outer+Inner", population.Types);
        }

        [Fact]
        public void GenericTypesResolveByNameWithoutTypeArguments()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(Type("N.IValue<TValue>", TypeKind.Interface)),
                    Signatures(Signature("N.IValue<TValue>.Get()", "N.IValue<TValue>", "Get"))),
                Row("CAP-001", "IValue"));

            Assert.Equal(new[] { "N.IValue<TValue>.Get()" }, population.Signatures.ToArray());
        }

        [Fact]
        public void NestedTypeWithGenericOuterAndInnerResolves()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.Outer<T>", TypeKind.Class),
                        Type("N.Outer<T>+Inner<U>", TypeKind.Class)),
                    Signatures(Signature("N.Outer<T>+Inner<U>.X()", "N.Outer<T>+Inner<U>", "X"))),
                Row("CAP-001", "Outer.Inner"));

            Assert.Equal(new[] { "N.Outer<T>+Inner<U>.X()" }, population.Signatures.ToArray());
        }

        [Fact]
        public void TypeWithoutANamespaceResolves()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(Type("IThing", TypeKind.Interface)),
                    Signatures(Signature("IThing.Run()", "IThing", "Run"))),
                Row("CAP-001", "IThing"));

            Assert.Equal(new[] { "IThing.Run()" }, population.Signatures.ToArray());
        }

        [Fact]
        public void TypesDifferingOnlyInGenericArityCannotBeResolved()
        {
            Assert.Throws<InvalidOperationException>(() => Resolve(
                Inventory(
                    Types(
                        Type("N.IValue<T>", TypeKind.Interface),
                        Type("N.IValue<T1,T2>", TypeKind.Interface)),
                    Signatures()),
                Row("CAP-001", "IValue")));
        }

        [Fact]
        public void GroupedRowResolvesEveryListedName()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.IOne", TypeKind.Interface),
                        Type("N.ITwo", TypeKind.Interface),
                        Type("N.IThree", TypeKind.Interface)),
                    Signatures(
                        Signature("N.IOne.A()", "N.IOne", "A"),
                        Signature("N.ITwo.B()", "N.ITwo", "B"),
                        Signature("N.IThree.C()", "N.IThree", "C"))),
                Group("CAP-001", "IOne", "ITwo", "IThree"));

            Assert.Equal(3, population.Signatures.Count);
            foreach (string key in population.Signatures)
            {
                Assert.Equal(new[] { "CAP-001" }, population.Owners[key].ToArray());
            }

            Assert.Equal(3, population.Types.Count);
        }

        [Fact]
        public void NameMatchingMoreThanOneTypeCannotBeResolved()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Resolve(
                Inventory(
                    Types(Type("A.IThing", TypeKind.Interface), Type("B.IThing", TypeKind.Interface)),
                    Signatures()),
                Row("CAP-001", "IThing")));

            Assert.Contains("CAP-001", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void NameMatchingNothingCannotBeResolved()
        {
            Assert.Throws<InvalidOperationException>(() => Resolve(
                Inventory(Types(Type("N.IThing", TypeKind.Interface)), Signatures()),
                Row("CAP-001", "IOther")));
        }

        [Fact]
        public void NameResolvedAsATypeIsNotRereadAsAMember()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.Outer", TypeKind.Class),
                        Type("N.Outer+Inner", TypeKind.Class)),
                    Signatures(
                        Signature("N.Outer.Inner()", "N.Outer", "Inner"),
                        Signature("N.Outer+Inner.X()", "N.Outer+Inner", "X"))),
                Row("CAP-001", "Outer.Inner"));

            Assert.Equal(new[] { "N.Outer+Inner.X()" }, population.Signatures.ToArray());
        }

        [Fact]
        public void RowNamingAMissingPublicMemberCannotBeResolved()
        {
            Assert.Throws<InvalidOperationException>(() => Resolve(
                Inventory(
                    Types(Type("N.IThing", TypeKind.Interface)),
                    Signatures(Signature("N.IThing.Run()", "N.IThing", "Run"))),
                Row("CAP-001", "IThing.Missing")));
        }

        [Fact]
        public void RowForATypeWithoutPublicMembersMayResolveToNothing()
        {
            LedgerPopulation population = Resolve(
                Inventory(Types(Type("N.IMarker", TypeKind.Interface)), Signatures()),
                Row("CAP-001", "IMarker"));

            Assert.Empty(population.Signatures);
            Assert.Contains("N.IMarker", population.Types);
        }

        [Fact]
        public void TheSameSignatureMayBelongToSeveralRows()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.IBase", TypeKind.Interface),
                        Type("N.IOne", TypeKind.Interface, "N.IBase"),
                        Type("N.ITwo", TypeKind.Interface, "N.IBase")),
                    Signatures(Signature("N.IBase.Visible()", "N.IBase", "Visible"))),
                Row("CAP-001", "IOne"),
                Row("CAP-002", "ITwo"));

            Assert.Equal(
                new[] { "CAP-001", "CAP-002" },
                population.Owners["N.IBase.Visible()"].OrderBy(i => i, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PatternRowCoversNonEnumTypesOfItsNamespaceOnly()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("PEPlugin.Pmd.IPEPmd", TypeKind.Interface),
                        Type("PEPlugin.Pmd.BoneKind", TypeKind.Enum),
                        Type("PEPlugin.SDX.V3", TypeKind.Struct)),
                    Signatures(
                        Signature("PEPlugin.Pmd.IPEPmd.Save()", "PEPlugin.Pmd.IPEPmd", "Save"),
                        Signature("PEPlugin.SDX.V3.X()", "PEPlugin.SDX.V3", "X"))),
                Pattern("CAP-463"),
                Pattern("CAP-466"));

            Assert.Contains("PEPlugin.Pmd.IPEPmd", population.Types);
            Assert.Contains("PEPlugin.SDX.V3", population.Types);
            Assert.DoesNotContain("PEPlugin.Pmd.BoneKind", population.Types);
            Assert.Equal(2, population.Signatures.Count);
        }

        [Fact]
        public void PmdRowAlsoCoversBuilderMembersReturningThatNamespace()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("PEPlugin.IPEBuilder", TypeKind.Interface),
                        Type("PEPlugin.Pmd.IPEBone", TypeKind.Interface)),
                    Signatures(
                        Signature(
                            "PEPlugin.IPEBuilder.CreateBone()",
                            "PEPlugin.IPEBuilder",
                            "CreateBone",
                            "PEPlugin.Pmd.IPEBone"),
                        Signature(
                            "PEPlugin.IPEBuilder.CreateVmd()",
                            "PEPlugin.IPEBuilder",
                            "CreateVmd",
                            "PEPlugin.Vmd.IPEVmd"))),
                Pattern("CAP-463"),
                Pattern("CAP-466"));

            Assert.Contains("PEPlugin.IPEBuilder.CreateBone()", population.Signatures);
            Assert.DoesNotContain("PEPlugin.IPEBuilder.CreateVmd()", population.Signatures);
        }

        [Fact]
        public void PatternRowWithoutAResolutionRuleCannotBeResolved()
        {
            Assert.Throws<InvalidOperationException>(() => Resolve(
                Inventory(Types(), Signatures()),
                Pattern("CAP-463"),
                Pattern("CAP-466"),
                Pattern("CAP-900")));
        }

        [Fact]
        public void MissingRowForAResolutionRuleCannotBeResolved()
        {
            Assert.Throws<InvalidOperationException>(() => Resolve(
                Inventory(Types(), Signatures()),
                Pattern("CAP-463")));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => LedgerPopulation.Resolve(null, Inventory(Types(), Signatures())));
            Assert.Throws<ArgumentNullException>(
                () => LedgerPopulation.Resolve(new List<CapabilityRecord>(), null));
        }

        [Fact]
        public void NamedTypesRecordsTheResolvedTypeButNotItsBasesOrItsMembersOwner()
        {
            LedgerPopulation population = Resolve(
                Inventory(
                    Types(
                        Type("N.IBase", TypeKind.Interface),
                        Type("N.IDerived", TypeKind.Interface, "N.IBase"),
                        Type("N.IOther", TypeKind.Interface)),
                    Signatures(
                        Signature("N.IBase.Visible()", "N.IBase", "Visible"),
                        Signature("N.IOther.Update()", "N.IOther", "Update"))),
                Row("CAP-001", "IDerived"),
                Row("CAP-002", "IOther.Update"));

            Assert.Equal(
                new[] { "N.IDerived" },
                population.NamedTypes.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.Equal(new[] { "CAP-001" }, population.NamedTypes["N.IDerived"].ToArray());
        }

        private static LedgerPopulation Resolve(InventoryRecord inventory, params CapabilityRecord[] rows)
        {
            List<CapabilityRecord> ledger = new List<CapabilityRecord>(rows);
            if (!rows.Any(r => r.TargetKind == CapabilityTargetKind.Pattern))
            {
                ledger.Add(Pattern("CAP-463"));
                ledger.Add(Pattern("CAP-466"));
            }

            return LedgerPopulation.Resolve(ledger, inventory);
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
                CapabilityOwner.Model,
                string.Empty);
        }

        private static CapabilityRecord Group(string id, params string[] names)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                string.Join(" / ", names),
                CapabilityTargetKind.Group,
                names.ToList(),
                CapabilityStatus.Provided,
                CapabilityOwner.Model,
                string.Empty);
        }

        private static CapabilityRecord Pattern(string id)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                "N.* のまとめ",
                CapabilityTargetKind.Pattern,
                new List<string>(),
                CapabilityStatus.NotSupported,
                CapabilityOwner.None,
                string.Empty);
        }

        private static TypeRecord Type(string name, TypeKind kind, params string[] baseTypes)
        {
            return new TypeRecord(name, kind, false, false, false, baseTypes.ToList(), new List<string>());
        }

        private static SignatureRecord Signature(
            string key, string declaringType, string memberName, string valueType = "System.Void")
        {
            return new SignatureRecord(
                key,
                declaringType,
                MemberKind.Method,
                memberName,
                false,
                0,
                new List<ParameterRecord>(),
                valueType,
                false,
                false,
                OperationDirection.Read);
        }

        private static IList<TypeRecord> Types(params TypeRecord[] types)
        {
            return types.ToList();
        }

        private static IList<SignatureRecord> Signatures(params SignatureRecord[] signatures)
        {
            return signatures.ToList();
        }

        private static InventoryRecord Inventory(
            IList<TypeRecord> types, IList<SignatureRecord> signatures)
        {
            return new InventoryRecord("Sample", "1.0.0.0", types, new List<TypeRecord>(), signatures);
        }
    }
}
