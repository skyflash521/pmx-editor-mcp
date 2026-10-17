using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>接続の根から提供対象へ至る経路の型が、母集合へ入るかどうかを固定する。</summary>
    public sealed class TypeRoleRouteTests
    {
        private static readonly string Root = TypeRoleEvidence.ConnectionRoots[0];

        private const string Middle = "N.IMiddle";

        private const string Alternate = "N.IAlternate";

        private const string Gate = "N.IGate";

        private const string Provided = "N.IProvided";

        private const string Dead = "N.IDead";

        private const string ValueTable = "PEPlugin.SDX.V3";

        [Fact]
        public void TheRootAndTheTypesOnThePathAreRoleTypes()
        {
            ISet<string> roleTypes = Resolve().RoleTypes;

            Assert.Contains(Root, roleTypes);
            Assert.Contains(Middle, roleTypes);
            Assert.Contains(Provided, roleTypes);
        }

        [Fact]
        public void ASecondPathToTheSameProvidedTypeIsAlsoARoute()
        {
            Assert.Contains(Alternate, Resolve().RoleTypes);
        }

        [Fact]
        public void AStepThroughAMethodThatTakesOnlyTheConnectorIsARoute()
        {
            Assert.Contains(Gate, Resolve().RoleTypes);
        }

        [Fact]
        public void AReachableTypeThatLeadsToNoProvidedTypeIsNotARoleType()
        {
            Assert.DoesNotContain(Dead, Resolve().RoleTypes);
        }

        [Fact]
        public void ARootThatLeadsToNoProvidedTypeIsNotARoleTypeEither()
        {
            ISet<string> roleTypes = Resolve().RoleTypes;

            foreach (string root in TypeRoleEvidence.ConnectionRoots.Skip(1))
            {
                Assert.DoesNotContain(root, roleTypes);
            }
        }

        [Fact]
        public void ATypeTheValueTableCoversStaysOutOfTheRouteEvenOnThePath()
        {
            TypeRolePopulation population = TypeRolePopulation.Resolve(
                Ledger(),
                Inventory(
                    Property(Root, "Position", ValueTable),
                    Property(ValueTable, "Owner", Provided),
                    Property(Provided, "Value", "System.Int32")),
                new List<ExcludedSignatureRecord>());

            Assert.Contains(Root, population.RoleTypes);
            Assert.DoesNotContain(ValueTable, population.RoleTypes);
        }

        [Fact]
        public void ARootThatTheEnumerationDoesNotKnowStops()
        {
            InventoryRecord inventory = new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord> { Type(Provided) },
                new List<TypeRecord>(),
                new List<SignatureRecord> { Property(Provided, "Value", "System.Int32") });

            ArgumentException error = Assert.Throws<ArgumentException>(
                () => TypeRolePopulation.Resolve(
                    Ledger(), inventory, new List<ExcludedSignatureRecord>()));

            Assert.Contains(Root, error.Message);
        }

        [Fact]
        public void EveryArgumentOfTheRouteIsRequired()
        {
            InventoryRecord inventory = Inventory(Property(Provided, "Value", "System.Int32"));

            Assert.Throws<ArgumentNullException>(
                () => TypeRoleEvidence.RouteTypesToward(null, Roots(), Targets()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleEvidence.RouteTypesToward(inventory, null, Targets()));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleEvidence.RouteTypesToward(inventory, Roots(), null));
        }

        private static TypeRolePopulation Resolve()
        {
            return TypeRolePopulation.Resolve(
                Ledger(),
                Inventory(
                    Property(Root, "Middle", Middle),
                    Property(Root, "Alternate", Alternate),
                    Property(Root, "Dead", Dead),
                    Connected(Root, "ViewCtrl", Gate),
                    Property(Middle, "Provided", Provided),
                    Property(Alternate, "Provided", Provided),
                    Property(Gate, "Provided", Provided),
                    Property(Dead, "Value", "System.Int32"),
                    Property(Provided, "Value", "System.Int32")),
                new List<ExcludedSignatureRecord>());
        }

        /// <summary>接続の根はどれも列挙に在ることを求められるので、題材はすべてを持つ。</summary>
        private static InventoryRecord Inventory(params SignatureRecord[] signatures)
        {
            List<TypeRecord> types = new List<TypeRecord>();
            List<SignatureRecord> members = new List<SignatureRecord>(signatures);
            foreach (string name in signatures.Select(s => s.DeclaringType)
                .Concat(signatures.Select(s => s.ValueType))
                .Concat(TypeRoleEvidence.ConnectionRoots)
                .Distinct(StringComparer.Ordinal)
                .Where(n => !n.StartsWith("System.", StringComparison.Ordinal)))
            {
                types.Add(Type(name));
            }

            foreach (string root in TypeRoleEvidence.ConnectionRoots.Skip(1))
            {
                members.Add(Property(root, "Version", "System.String"));
            }

            return new InventoryRecord("PEPlugin", "0.0.0.0", types, new List<TypeRecord>(), members);
        }

        private static IList<CapabilityRecord> Ledger()
        {
            return new List<CapabilityRecord>
            {
                Row("CAP-001", Provided, CapabilityStatus.Provided),
                Pattern("CAP-463", "PEPlugin.Pmd.*"),
                Pattern("CAP-466", "PEPlugin.SDX.*"),
            };
        }

        private static IEnumerable<string> Roots()
        {
            return TypeRoleEvidence.ConnectionRoots;
        }

        private static ISet<string> Targets()
        {
            return new HashSet<string>(new[] { Provided }, StringComparer.Ordinal);
        }

        private static TypeRecord Type(string name)
        {
            return new TypeRecord(
                name, TypeKind.Interface, false, true, false, new List<string>(), new List<string>());
        }

        private static SignatureRecord Property(string declaringType, string memberName, string valueType)
        {
            return Member(declaringType, MemberKind.Property, memberName, valueType, new ParameterRecord[0]);
        }

        /// <summary>自動注入コネクタだけを取り値を返すメソッド。取得プロパティと同じく経路になる。</summary>
        private static SignatureRecord Connected(
            string declaringType, string memberName, string valueType)
        {
            return Member(
                declaringType,
                MemberKind.Method,
                memberName,
                valueType,
                new[]
                {
                    new ParameterRecord(
                        "connector", TypeRoleEvidence.InjectedConnector, ParameterDirection.In, false),
                });
        }

        private static SignatureRecord Member(
            string declaringType,
            MemberKind memberKind,
            string memberName,
            string valueType,
            ParameterRecord[] parameters)
        {
            return new SignatureRecord(
                SignatureKeyBuilder.Build(declaringType, memberName, 0, parameters, valueType),
                declaringType,
                memberKind,
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

        private static CapabilityRecord Row(string id, string target, CapabilityStatus status)
        {
            return new CapabilityRecord(
                id,
                "分類",
                target,
                CapabilityTargetKind.Single,
                new List<string> { target },
                status,
                status == CapabilityStatus.Provided ? CapabilityOwner.Model : CapabilityOwner.None,
                string.Empty);
        }

        private static CapabilityRecord Pattern(string id, string target)
        {
            return new CapabilityRecord(
                id,
                "分類",
                target,
                CapabilityTargetKind.Pattern,
                new List<string> { target },
                CapabilityStatus.NotSupported,
                CapabilityOwner.None,
                string.Empty);
        }
    }
}
