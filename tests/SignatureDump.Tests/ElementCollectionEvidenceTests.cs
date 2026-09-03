using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>要素を並べるリストの母集合の決まり方を固定する。</summary>
    public sealed class ElementCollectionEvidenceTests
    {
        private const string Owner = "N.IOwner";

        private const string Element = "N.IElement";

        private const string ElementList = "System.Collections.Generic.IList<N.IElement>";

        [Fact]
        public void AListOfRoleTypesIsACandidateWithItsElementType()
        {
            SignatureRecord signature = Property(Owner, "Items", ElementList);

            Assert.Equal(Element, Assert.Single(Candidates(signature)).Value);
        }

        [Fact]
        public void AMethodThatReturnsAListIsNotACandidate()
        {
            Assert.Empty(Candidates(
                Signature(Owner, MemberKind.Method, "GetItems", ElementList, true)));
        }

        [Fact]
        public void AnIndexerThatReturnsAListIsNotACandidate()
        {
            Assert.Empty(Candidates(Indexer(Owner, "Item", ElementList)));
        }

        [Fact]
        public void AWriteOnlyListIsNotACandidate()
        {
            Assert.Empty(Candidates(
                Signature(Owner, MemberKind.Property, "Items", ElementList, false)));
        }

        [Fact]
        public void AnArrayOfRoleTypesIsNotACandidate()
        {
            Assert.Empty(Candidates(Property(Owner, "Items", Element + "[]")));
        }

        [Fact]
        public void AListOfATypeOutsideTheTableIsNotACandidate()
        {
            Assert.Empty(Candidates(
                Property(Owner, "Names", "System.Collections.Generic.IList<System.String>")));
        }

        [Fact]
        public void AListOnATypeOutsideTheTableIsNotACandidate()
        {
            Assert.Empty(Candidates(Property("N.IOutside", "Items", ElementList)));
        }

        [Fact]
        public void AListThatIsNotProvidedIsNotACandidate()
        {
            Assert.Empty(ElementCollectionEvidence.Candidates(
                Inventory(Property(Owner, "Items", ElementList)),
                Roles(),
                new HashSet<string>(StringComparer.Ordinal)));
        }

        [Fact]
        public void TheConcreteTypesOfABaseAreItsLeaves()
        {
            IDictionary<string, IList<string>> kinds = ElementCollectionEvidence.ConcreteTypes(
                Inventory(Type(Element), Type("N.IMiddle", Element), Type("N.ILeaf", "N.IMiddle")),
                Kinds(Element, "N.IMiddle", "N.ILeaf"));

            Assert.Equal(new[] { "N.ILeaf" }, kinds[Element]);
            Assert.Equal(new[] { "N.ILeaf" }, kinds["N.IMiddle"]);
        }

        [Fact]
        public void ATypeNoRoleTypeInheritsHasNoConcreteTypes()
        {
            IDictionary<string, IList<string>> kinds = ElementCollectionEvidence.ConcreteTypes(
                Inventory(Type(Element), Type("N.ILeaf", Element)),
                Kinds(Element, "N.ILeaf"));

            Assert.False(kinds.ContainsKey("N.ILeaf"));
        }

        [Fact]
        public void AClassIsNotAConcreteType()
        {
            IDictionary<string, IList<string>> kinds = ElementCollectionEvidence.ConcreteTypes(
                Inventory(Type(Element), Class("N.Thing", Element)),
                Kinds(Element, "N.Thing"));

            Assert.Empty(kinds);
        }

        [Fact]
        public void ATypeOutsideTheTableIsNotAConcreteType()
        {
            IDictionary<string, IList<string>> kinds = ElementCollectionEvidence.ConcreteTypes(
                Inventory(Type(Element), Type("N.IOutside", Element)),
                Kinds(Element));

            Assert.Empty(kinds);
        }

        [Fact]
        public void BothArgumentsOfTheConcreteTypesAreRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => ElementCollectionEvidence.ConcreteTypes(null, Kinds(Element)));
            Assert.Throws<ArgumentNullException>(
                () => ElementCollectionEvidence.ConcreteTypes(Inventory(Type(Element)), null));
        }

        private static IDictionary<string, TypeRole> Kinds(params string[] names)
        {
            Dictionary<string, TypeRole> roles =
                new Dictionary<string, TypeRole>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                roles.Add(name, TypeRole.OperationTarget);
            }

            return roles;
        }

        private static TypeRecord Class(string name, params string[] baseTypes)
        {
            return new TypeRecord(
                name,
                TypeKind.Class,
                false,
                false,
                false,
                baseTypes.ToList(),
                new List<string>());
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

        private static InventoryRecord Inventory(params TypeRecord[] types)
        {
            return new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                types.ToList(),
                new List<TypeRecord>(),
                new List<SignatureRecord>());
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            InventoryRecord inventory = Inventory(Property(Owner, "Items", ElementList));

            Assert.Throws<ArgumentNullException>(
                () => ElementCollectionEvidence.Candidates(null, Roles(), Provided()));
            Assert.Throws<ArgumentNullException>(
                () => ElementCollectionEvidence.Candidates(inventory, null, Provided()));
            Assert.Throws<ArgumentNullException>(
                () => ElementCollectionEvidence.Candidates(inventory, Roles(), null));
        }

        private static IDictionary<string, string> Candidates(params SignatureRecord[] signatures)
        {
            return ElementCollectionEvidence.Candidates(
                Inventory(signatures),
                Roles(),
                new HashSet<string>(signatures.Select(s => s.Key), StringComparer.Ordinal));
        }

        private static ISet<string> Provided()
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        private static IDictionary<string, TypeRole> Roles()
        {
            return new Dictionary<string, TypeRole>(StringComparer.Ordinal)
            {
                { Owner, TypeRole.OperationTarget },
                { Element, TypeRole.OperationTarget },
            };
        }

        private static InventoryRecord Inventory(params SignatureRecord[] signatures)
        {
            return new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord>(),
                new List<TypeRecord>(),
                signatures.ToList());
        }

        private static SignatureRecord Property(
            string declaringType, string memberName, string valueType)
        {
            return Signature(declaringType, MemberKind.Property, memberName, valueType, true);
        }

        private static SignatureRecord Indexer(
            string declaringType, string memberName, string valueType)
        {
            ParameterRecord[] parameters = new[]
            {
                new ParameterRecord("index", "System.Int32", ParameterDirection.In, false),
            };

            return new SignatureRecord(
                SignatureKeyBuilder.Build(declaringType, memberName, 0, parameters, valueType),
                declaringType,
                MemberKind.Property,
                memberName,
                false,
                0,
                parameters,
                valueType,
                true,
                false,
                OperationDirection.Read,
                false);
        }

        private static SignatureRecord Signature(
            string declaringType,
            MemberKind memberKind,
            string memberName,
            string valueType,
            bool canRead)
        {
            ParameterRecord[] parameters = new ParameterRecord[0];

            return new SignatureRecord(
                SignatureKeyBuilder.Build(declaringType, memberName, 0, parameters, valueType),
                declaringType,
                memberKind,
                memberName,
                false,
                0,
                parameters,
                valueType,
                canRead,
                false,
                OperationDirection.Read,
                false);
        }
    }
}
