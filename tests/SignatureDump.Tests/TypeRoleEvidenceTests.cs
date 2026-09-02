using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeRoleEvidenceTests
    {
        private const string Root = "N.IRoot";

        private const string Injected = TypeRoleEvidence.InjectedConnector;

        [Fact]
        public void TheArgumentOfAnEventHandlerIsEvidence()
        {
            ISet<string> types = TypeRoleEvidence.EventArgumentTypes(Inventory(
                Event(Root, "Clicked", "System.EventHandler<N.ClickArgs>")));

            Assert.Equal(new[] { "N.ClickArgs" }, types);
        }

        [Fact]
        public void AHandlerWithoutATypeArgumentGivesNoEvidence()
        {
            Assert.Empty(TypeRoleEvidence.EventArgumentTypes(Inventory(
                Event(Root, "Changed", "System.EventHandler"))));
        }

        [Fact]
        public void AnArgumentIsRecordedByItsDefinitionKey()
        {
            ISet<string> types = TypeRoleEvidence.EventArgumentTypes(Inventory(
                Event(Root, "Clicked", "System.EventHandler<N.Args<System.Int32>>")));

            Assert.Equal(new[] { "N.Args<1>" }, types);
        }

        [Fact]
        public void AnotherDelegateAsTheHandlerStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => TypeRoleEvidence.EventArgumentTypes(Inventory(
                    Event(Root, "Clicked", "N.ClickHandler"))));

            Assert.Contains("Clicked", error.Message);
        }

        [Fact]
        public void MembersThatAreNotEventsGiveNoEvidence()
        {
            Assert.Empty(TypeRoleEvidence.EventArgumentTypes(Inventory(
                Property(Root, "Value", "System.EventHandler<N.ClickArgs>"),
                Method(Root, "Get", "System.EventHandler<N.ClickArgs>"))));
        }

        [Fact]
        public void ARootIsReachedWithAnEmptyPath()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(Property(Root, "Value", "System.Int32")), new[] { Root });

            Assert.Equal(string.Empty, reached[Root]);
        }

        [Fact]
        public void ARootWithoutMembersStops()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => TypeRoleEvidence.ReachableFromRoots(
                    Inventory(Property(Root, "Value", "System.Int32")), new[] { "N.IAbsent" }));

            Assert.Contains("N.IAbsent", error.Message);
        }

        [Fact]
        public void AReadablePropertyIsAStep()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Property(Root, "Host", "N.IHost"),
                    Property("N.IHost", "View", "N.IView"),
                    Property("N.IView", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal("Host", reached["N.IHost"]);
            Assert.Equal("Host.View", reached["N.IView"]);
        }

        [Fact]
        public void APropertyThatCannotBeReadIsNotAStep()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    WriteOnly(Root, "Host", "N.IHost"),
                    Property("N.IHost", "Value", "System.Int32")),
                new[] { Root });

            Assert.False(reached.ContainsKey("N.IHost"));
        }

        [Fact]
        public void AnIndexerIsNotAStep()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Indexer(Root, "Item", "N.IHost"),
                    Property("N.IHost", "Value", "System.Int32")),
                new[] { Root });

            Assert.False(reached.ContainsKey("N.IHost"));
        }

        [Fact]
        public void AMethodTakingOnlyTheInjectedConnectorIsAStep()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Method(Root, "SystemCtrl", "N.ISystem", Arg("c", Injected)),
                    Property("N.ISystem", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal("SystemCtrl()", reached["N.ISystem"]);
        }

        [Fact]
        public void AMethodWithoutArgumentsIsNotAStep()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Method(Root, "Vertex", "N.IVertex"),
                    Property("N.IVertex", "Value", "System.Int32")),
                new[] { Root });

            Assert.False(reached.ContainsKey("N.IVertex"));
        }

        [Fact]
        public void AMethodTakingAnotherArgumentIsNotAStep()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Method(Root, "Open", "N.ISystem", Arg("c", Injected), Arg("index", "System.Int32")),
                    Property("N.ISystem", "Value", "System.Int32")),
                new[] { Root });

            Assert.False(reached.ContainsKey("N.ISystem"));
        }

        [Fact]
        public void AMethodWithoutAReturnValueIsNotAStep()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Method(Root, "Apply", "System.Void", Arg("c", Injected)),
                    Property("N.ISystem", "Value", "System.Int32")),
                new[] { Root });

            Assert.False(reached.ContainsKey("N.ISystem"));
        }

        [Fact]
        public void AClosedGenericStepIsMatchedByItsDefinitionKey()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Property(Root, "Value", "N.IBox<System.Int32>"),
                    Property("N.IBox<TValue>", "Inner", "System.Int32")),
                new[] { Root });

            Assert.Equal("Value", reached["N.IBox<1>"]);
        }

        [Fact]
        public void APropertyDeclaredByABaseTypeIsAStep()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Records(
                    new[] { Type(Root, "N.IBase"), Type("N.IBase") },
                    Property(Root, "Own", "System.Int32"),
                    Property("N.IBase", "Host", "N.IHost"),
                    Property("N.IHost", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal("Host", reached["N.IHost"]);
        }

        [Fact]
        public void ATypeThatOnlyInheritsMembersIsReached()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Records(
                    new[] { Type(Root), Type("N.IHelper", "N.IBase"), Type("N.IBase") },
                    Property(Root, "Helper", "N.IHelper"),
                    Property("N.IBase", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal("Helper", reached["N.IHelper"]);
        }

        [Fact]
        public void ATypeWithNoMembersAtAllIsNotReached()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(Property(Root, "Value", "N.IEmpty")), new[] { Root });

            Assert.False(reached.ContainsKey("N.IEmpty"));
        }

        [Fact]
        public void ABaseReachedThroughTwoInheritancePathsIsWalked()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Records(
                    new[]
                    {
                        Type(Root, "N.IOne", "N.ITwo"),
                        Type("N.IOne", "N.IBase"),
                        Type("N.ITwo", "N.IBase"),
                        Type("N.IBase"),
                    },
                    Property("N.IBase", "Host", "N.IHost"),
                    Property("N.IHost", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal("Host", reached["N.IHost"]);
        }

        [Fact]
        public void ABaseThatHasNoTypeRecordIsSkipped()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Records(
                    new[] { Type(Root, "System.ICloneable") },
                    Property(Root, "Host", "N.IHost"),
                    Property("N.IHost", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal("Host", reached["N.IHost"]);
        }

        [Fact]
        public void TheShortestPathFoundFirstIsKept()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Property(Root, "Near", "N.ITarget"),
                    Property(Root, "Host", "N.IHost"),
                    Property("N.IHost", "Far", "N.ITarget"),
                    Property("N.ITarget", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal("Near", reached["N.ITarget"]);
        }

        [Fact]
        public void OfTwoPathsOfTheSameLengthTheEarlierMemberWins()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Property(Root, "Alpha", "N.ITarget"),
                    Property(Root, "Beta", "N.ITarget"),
                    Property("N.ITarget", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal("Alpha", reached["N.ITarget"]);
        }

        [Fact]
        public void SeveralRootsAreWalkedTogether()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Property(Root, "Value", "System.Int32"),
                    Property("N.IOther", "View", "N.IView"),
                    Property("N.IView", "Value", "System.Int32")),
                new[] { Root, "N.IOther" });

            Assert.Equal(string.Empty, reached["N.IOther"]);
            Assert.Equal("View", reached["N.IView"]);
        }

        [Fact]
        public void ACycleDoesNotRepeat()
        {
            IDictionary<string, string> reached = TypeRoleEvidence.ReachableFromRoots(
                Inventory(
                    Property(Root, "Host", "N.IHost"),
                    Property("N.IHost", "Back", Root)),
                new[] { Root });

            Assert.Equal(string.Empty, reached[Root]);
            Assert.Equal("Host", reached["N.IHost"]);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => TypeRoleEvidence.EventArgumentTypes(null));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleEvidence.ReachableFromRoots(null, new[] { Root }));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleEvidence.ReachableFromRoots(Inventory(), null));
            Assert.Throws<ArgumentException>(
                () => TypeRoleEvidence.ReachableFromRoots(Inventory(), new[] { " " }));
        }

        [Fact]
        public void TheBaseTypesOfAReachedTypeAreIncluded()
        {
            ISet<string> reached = TypeRoleEvidence.ReachableWithBaseTypes(
                Records(
                    new[]
                    {
                        Type(Root),
                        Type("N.IWindow", "N.ISizable"),
                        Type("N.ISizable", "N.IBase"),
                        Type("N.IBase"),
                        Type("N.IApart", "N.IApartBase"),
                        Type("N.IApartBase"),
                    },
                    Property(Root, "Window", "N.IWindow"),
                    Property("N.IWindow", "Value", "System.Int32")),
                new[] { Root });

            Assert.Equal(
                new[] { "N.IBase", Root, "N.ISizable", "N.IWindow" },
                reached.OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void ARootWithoutMembersStopsTheWalkWithBaseTypesToo()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => TypeRoleEvidence.ReachableWithBaseTypes(
                    Inventory(Property(Root, "Value", "System.Int32")), new[] { "N.IAbsent" }));

            Assert.Contains("N.IAbsent", error.Message);
        }

        [Fact]
        public void EveryArgumentOfTheWalkWithBaseTypesIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleEvidence.ReachableWithBaseTypes(null, new[] { Root }));
            Assert.Throws<ArgumentNullException>(
                () => TypeRoleEvidence.ReachableWithBaseTypes(Inventory(), null));
        }

        private static InventoryRecord Records(
            IEnumerable<TypeRecord> types, params SignatureRecord[] signatures)
        {
            return new InventoryRecord(
                "PEPlugin", "0.0.0.0", types.ToList(), new List<TypeRecord>(), signatures.ToList());
        }

        private static TypeRecord Type(string name, params string[] baseTypes)
        {
            return new TypeRecord(
                name, TypeKind.Interface, false, true, false, baseTypes.ToList(), new List<string>());
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

        private static SignatureRecord Event(string declaringType, string memberName, string valueType)
        {
            return Signature(declaringType, MemberKind.Event, memberName, valueType, true);
        }

        private static SignatureRecord Property(string declaringType, string memberName, string valueType)
        {
            return Signature(declaringType, MemberKind.Property, memberName, valueType, true);
        }

        private static SignatureRecord WriteOnly(string declaringType, string memberName, string valueType)
        {
            return Signature(declaringType, MemberKind.Property, memberName, valueType, false);
        }

        private static SignatureRecord Indexer(string declaringType, string memberName, string valueType)
        {
            return Signature(
                declaringType,
                MemberKind.Property,
                memberName,
                valueType,
                true,
                Arg("index", "System.Int32"));
        }

        private static SignatureRecord Method(
            string declaringType, string memberName, string valueType, params ParameterRecord[] parameters)
        {
            return Signature(
                declaringType, MemberKind.Method, memberName, valueType, false, parameters);
        }

        private static ParameterRecord Arg(string name, string typeName)
        {
            return new ParameterRecord(name, typeName, ParameterDirection.In, false);
        }

        private static SignatureRecord Signature(
            string declaringType,
            MemberKind memberKind,
            string memberName,
            string valueType,
            bool canRead,
            params ParameterRecord[] parameters)
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
                canRead,
                false,
                OperationDirection.Read,
                false);
        }
    }
}
