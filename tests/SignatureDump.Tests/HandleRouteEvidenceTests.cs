using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>その型の実体を得る道が在るかの決まり方を固定する。</summary>
    public sealed class HandleRouteEvidenceTests
    {
        private const string Pmx = "PEPlugin.Pmx.IPXPmx";

        private const string Held = "N.IHeld";

        private const string Reached = "N.IReached";

        [Fact]
        public void ThePmxItselfIsReached()
        {
            Assert.Contains(Pmx, Route());
        }

        [Fact]
        public void APropertyOfThePmxReachesItsValueType()
        {
            Assert.Contains(Reached, Route(Property(Pmx, "Held", Reached)));
        }

        [Fact]
        public void ThePropertiesOfAReachedTypeReachTheirValueTypes()
        {
            Assert.Contains(
                "N.IDeep",
                Route(Property(Pmx, "Held", Reached), Property(Reached, "Deep", "N.IDeep")));
        }

        [Fact]
        public void APropertyOfATypeNoRouteLeadsToReachesNothing()
        {
            Assert.DoesNotContain(Reached, Route(Property(Held, "Held", Reached)));
        }

        [Fact]
        public void AConnectorIsReached()
        {
            Assert.Contains(
                Held,
                HandleRouteEvidence.Reached(
                    Signatures(), Roles(new[] { Role(Held, TypeRole.Connector) })));
        }

        [Fact]
        public void TheElementOfAnOwnedListIsReached()
        {
            SignatureRecord list = Property(
                Held, "Items", "System.Collections.Generic.IList<" + Reached + ">");

            Assert.Contains(
                Reached,
                HandleRouteEvidence.Reached(
                    Signatures(list),
                    Roles(collections: new[] { Collection(list.Key, true) })));
        }

        [Fact]
        public void TheElementOfAListSomewhereElseOwnsIsNotReached()
        {
            SignatureRecord list = Property(
                Held, "Items", "System.Collections.Generic.IList<" + Reached + ">");

            Assert.DoesNotContain(
                Reached,
                HandleRouteEvidence.Reached(
                    Signatures(list),
                    Roles(collections: new[] { Collection(list.Key, false) })));
        }

        [Fact]
        public void TheValueOfACallThatMakesANewInstanceIsReached()
        {
            SignatureRecord made = Method(Held, "Make", Reached);

            Assert.Contains(
                Reached,
                HandleRouteEvidence.Reached(
                    Signatures(made), Roles(issuances: new[] { Issuance(made.Key, true) })));
        }

        [Fact]
        public void TheValueOfACallThatOnlyHandsBackAnInstanceIsNotReached()
        {
            SignatureRecord handed = Method(Held, "Narrow", Reached);

            Assert.DoesNotContain(
                Reached,
                HandleRouteEvidence.Reached(
                    Signatures(handed), Roles(issuances: new[] { Issuance(handed.Key, false) })));
        }

        [Fact]
        public void ATypeTakenInAfterTheWalkDoesNotReachWhatItsPropertiesHold()
        {
            SignatureRecord list = Property(
                "N.IOwner", "Items", "System.Collections.Generic.IList<" + Reached + ">");
            SignatureRecord made = Method(Held, "Make", "N.IMade");
            ISet<string> route = HandleRouteEvidence.Reached(
                Signatures(
                    list,
                    made,
                    Property(Held, "Onward", "N.IBeyondConnector"),
                    Property(Reached, "Onward", "N.IBeyondElement"),
                    Property("N.IMade", "Onward", "N.IBeyondMade")),
                Roles(
                    new[] { Role(Held, TypeRole.Connector) },
                    new[] { Issuance(made.Key, true) },
                    new[] { Collection(list.Key, true) }));

            Assert.Contains(Held, route);
            Assert.Contains(Reached, route);
            Assert.Contains("N.IMade", route);
            Assert.DoesNotContain("N.IBeyondConnector", route);
            Assert.DoesNotContain("N.IBeyondElement", route);
            Assert.DoesNotContain("N.IBeyondMade", route);
        }

        [Fact]
        public void BothArgumentsAreRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => HandleRouteEvidence.Reached(null, Roles()));
            Assert.Throws<ArgumentNullException>(
                () => HandleRouteEvidence.Reached(Signatures(), null));
        }

        private static ISet<string> Route(params SignatureRecord[] signatures)
        {
            return HandleRouteEvidence.Reached(Signatures(signatures), Roles());
        }

        private static IDictionary<string, SignatureRecord> Signatures(
            params SignatureRecord[] signatures)
        {
            return signatures.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
        }

        private static TypeRoleTable Roles(
            IList<TypeRoleRecord> types = null,
            IList<HandleIssuanceRecord> issuances = null,
            IList<ElementCollectionRecord> collections = null)
        {
            return new TypeRoleTable(
                types ?? new List<TypeRoleRecord>(),
                issuances ?? new List<HandleIssuanceRecord>(),
                collections ?? new List<ElementCollectionRecord>());
        }

        private static TypeRoleRecord Role(string typeName, TypeRole role)
        {
            return new TypeRoleRecord(typeName, role, "根拠", "もの", "もの", CapabilityOwner.Session);
        }

        private static HandleIssuanceRecord Issuance(string signatureKey, bool issues)
        {
            return new HandleIssuanceRecord(signatureKey, issues, "根拠");
        }

        private static ElementCollectionRecord Collection(string signatureKey, bool owns)
        {
            return new ElementCollectionRecord(
                signatureKey, owns, "根拠", owns ? new List<string> { signatureKey } : null);
        }

        private static SignatureRecord Property(
            string declaringType, string memberName, string valueType)
        {
            return Signature(declaringType, MemberKind.Property, memberName, valueType);
        }

        private static SignatureRecord Method(
            string declaringType, string memberName, string valueType)
        {
            return Signature(declaringType, MemberKind.Method, memberName, valueType);
        }

        private static SignatureRecord Signature(
            string declaringType, MemberKind memberKind, string memberName, string valueType)
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
                true,
                false,
                OperationDirection.Read);
        }
    }
}
