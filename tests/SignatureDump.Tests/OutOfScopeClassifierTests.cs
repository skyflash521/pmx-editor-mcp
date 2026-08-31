using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class OutOfScopeClassifierTests
    {
        [Fact]
        public void EnumTypeYieldsTheEnumTypeReason()
        {
            Assert.Equal(
                OutOfScopeReason.EnumType,
                Classify(Types(Type("N.Kind", TypeKind.Enum)), Signatures(), "N.Kind"));
        }

        [Fact]
        public void DelegateTypeYieldsTheDelegateTypeReason()
        {
            Assert.Equal(
                OutOfScopeReason.DelegateType,
                Classify(
                    Types(Type("N.Proc", TypeKind.Delegate)),
                    Signatures(Signature("N.Proc.Invoke()", "N.Proc", "Invoke", MemberKind.Method, "N.Kind")),
                    "N.Proc"));
        }

        [Fact]
        public void TypeWhoseMembersAreAllRoutesYieldsTheRouteReason()
        {
            Assert.Equal(
                OutOfScopeReason.Route,
                Classify(
                    Types(Type("N.IHub", TypeKind.Interface), Type("N.IThing", TypeKind.Interface)),
                    Signatures(Signature("N.IHub.Thing()", "N.IHub", "Thing", MemberKind.Method, "N.IThing")),
                    "N.IHub"));
        }

        [Fact]
        public void PropertiesAndFieldsCanAlsoBeRoutes()
        {
            Assert.Equal(
                OutOfScopeReason.Route,
                Classify(
                    Types(Type("N.IHub", TypeKind.Interface), Type("N.IThing", TypeKind.Interface)),
                    Signatures(
                        Signature("N.IHub.Thing()", "N.IHub", "Thing", MemberKind.Property, "N.IThing")),
                    "N.IHub"));

            Assert.Equal(
                OutOfScopeReason.Route,
                Classify(
                    Types(Type("N.Hub", TypeKind.Class), Type("N.IThing", TypeKind.Interface)),
                    Signatures(
                        Signature("N.Hub.Thing()", "N.Hub", "Thing", MemberKind.Field, "N.IThing")),
                    "N.Hub"));
        }

        [Fact]
        public void MemberWithPublicArgumentsIsNotARoute()
        {
            Assert.Null(Classify(
                Types(Type("N.IHub", TypeKind.Interface), Type("N.IThing", TypeKind.Interface)),
                Signatures(
                    new SignatureRecord(
                        "N.IHub.Thing(System.Int32)",
                        "N.IHub",
                        MemberKind.Method,
                        "Thing",
                        false,
                        0,
                        new List<ParameterRecord>
                        {
                            new ParameterRecord("index", "System.Int32", ParameterDirection.In, false),
                        },
                        "N.IThing",
                        false,
                        false,
                        OperationDirection.Read)),
                "N.IHub"));
        }

        [Fact]
        public void MemberWhoseValueTypeIsOutsideTheAssemblyIsNotARoute()
        {
            Assert.Null(Classify(
                Types(Type("N.IHub", TypeKind.Interface)),
                Signatures(
                    Signature("N.IHub.Name()", "N.IHub", "Name", MemberKind.Method, "System.String")),
                "N.IHub"));
        }

        [Fact]
        public void TypeWithASingleNonRouteMemberIsNotARoute()
        {
            Assert.Null(Classify(
                Types(Type("N.IHub", TypeKind.Interface), Type("N.IThing", TypeKind.Interface)),
                Signatures(
                    Signature("N.IHub.Thing()", "N.IHub", "Thing", MemberKind.Method, "N.IThing"),
                    Signature("N.IHub.Name()", "N.IHub", "Name", MemberKind.Method, "System.String")),
                "N.IHub"));
        }

        [Fact]
        public void MemberReturningItsOwnTypeIsNotARoute()
        {
            Assert.Null(Classify(
                Types(Type("N.IThing", TypeKind.Interface)),
                Signatures(
                    Signature("N.IThing.Clone()", "N.IThing", "Clone", MemberKind.Method, "N.IThing")),
                "N.IThing"));
        }

        [Fact]
        public void EventsAndConstructorsAreNotRoutes()
        {
            Assert.Null(Classify(
                Types(Type("N.Holder", TypeKind.Class), Type("N.Proc", TypeKind.Delegate)),
                Signatures(
                    Signature("N.Holder.Changed()", "N.Holder", "Changed", MemberKind.Event, "N.Proc")),
                "N.Holder"));

            Assert.Null(Classify(
                Types(Type("N.Holder", TypeKind.Class), Type("N.IThing", TypeKind.Interface)),
                Signatures(
                    Signature("N.Holder..ctor()", "N.Holder", ".ctor", MemberKind.Constructor, "N.IThing")),
                "N.Holder"));
        }

        [Fact]
        public void TypeWithoutPublicMembersIsARouteWhenAllBasesAre()
        {
            Assert.Equal(
                OutOfScopeReason.Route,
                Classify(
                    Types(
                        Type("N.IThing", TypeKind.Interface),
                        Type("N.IHub", TypeKind.Interface),
                        Type("N.IEmpty", TypeKind.Interface, "N.IHub")),
                    Signatures(Signature("N.IHub.Thing()", "N.IHub", "Thing", MemberKind.Method, "N.IThing")),
                    "N.IEmpty"));
        }

        [Fact]
        public void TypeWithoutPublicMembersOrBasesIsNotARoute()
        {
            Assert.Null(Classify(
                Types(Type("N.IEmpty", TypeKind.Interface)), Signatures(), "N.IEmpty"));
        }

        [Fact]
        public void TypeWithASingleNonRouteBaseIsNotARoute()
        {
            Assert.Null(Classify(
                Types(
                    Type("N.IThing", TypeKind.Interface),
                    Type("N.IHub", TypeKind.Interface),
                    Type("N.IOther", TypeKind.Interface),
                    Type("N.IEmpty", TypeKind.Interface, "N.IHub", "N.IOther")),
                Signatures(
                    Signature("N.IHub.Thing()", "N.IHub", "Thing", MemberKind.Method, "N.IThing"),
                    Signature("N.IOther.Name()", "N.IOther", "Name", MemberKind.Method, "System.String")),
                "N.IEmpty"));
        }

        [Fact]
        public void TypeAppearingOnlyAsAnArgumentYieldsTheArgumentOnlyReason()
        {
            Assert.Equal(
                OutOfScopeReason.ArgumentOnly,
                Classify(
                    Types(Type("N.Option", TypeKind.Class), Type("N.IThing", TypeKind.Interface)),
                    Signatures(
                        Signature("N.Option.Frame()", "N.Option", "Frame", MemberKind.Field, "System.Int32"),
                        Parameterized("N.IThing.Run(N.Option)", "N.IThing", "Run", "N.Option")),
                    "N.Option"));
        }

        [Fact]
        public void TypeAlsoAppearingAsAReturnValueIsNotArgumentOnly()
        {
            Assert.Null(Classify(
                Types(Type("N.Option", TypeKind.Class), Type("N.IThing", TypeKind.Interface)),
                Signatures(
                    Signature("N.Option.Frame()", "N.Option", "Frame", MemberKind.Field, "System.Int32"),
                    Parameterized("N.IThing.Run(N.Option)", "N.IThing", "Run", "N.Option"),
                    Signature("N.IThing.Make()", "N.IThing", "Make", MemberKind.Method, "N.Option")),
                "N.Option"));
        }

        [Fact]
        public void TypeAppearingNowhereIsNotArgumentOnly()
        {
            Assert.Null(Classify(
                Types(Type("N.Option", TypeKind.Class)),
                Signatures(
                    Signature("N.Option.Frame()", "N.Option", "Frame", MemberKind.Field, "System.Int32")),
                "N.Option"));
        }

        [Fact]
        public void TypeWithItsOwnSignatureInThePopulationIsNotArgumentOnly()
        {
            IList<TypeRecord> types = Types(Type("N.Option", TypeKind.Class), Type("N.IThing", TypeKind.Interface));
            IList<SignatureRecord> signatures = Signatures(
                Signature("N.Option.Frame()", "N.Option", "Frame", MemberKind.Field, "System.Int32"),
                Parameterized("N.IThing.Run(N.Option)", "N.IThing", "Run", "N.Option"));
            OutOfScopeClassifier classifier = new OutOfScopeClassifier(
                Inventory(types, signatures),
                new HashSet<string>(new[] { "N.Option.Frame()" }, StringComparer.Ordinal));

            Assert.Null(classifier.ClassifyType("N.Option"));
        }

        [Fact]
        public void ByReferenceArgumentsCountForArgumentOnly()
        {
            Assert.Equal(
                OutOfScopeReason.ArgumentOnly,
                Classify(
                    Types(Type("N.Option", TypeKind.Class), Type("N.IThing", TypeKind.Interface)),
                    Signatures(
                        Signature("N.Option.Frame()", "N.Option", "Frame", MemberKind.Field, "System.Int32"),
                        Parameterized("N.IThing.Run(N.Option&)", "N.IThing", "Run", "N.Option&")),
                    "N.Option"));
        }

        [Fact]
        public void TypeReturnedByReferenceIsNotArgumentOnly()
        {
            Assert.Null(Classify(
                Types(Type("N.Option", TypeKind.Class), Type("N.IThing", TypeKind.Interface)),
                Signatures(
                    Signature("N.Option.Frame()", "N.Option", "Frame", MemberKind.Field, "System.Int32"),
                    Parameterized("N.IThing.Run(N.Option)", "N.IThing", "Run", "N.Option"),
                    Signature("N.IThing.Ref()", "N.IThing", "Ref", MemberKind.Method, "N.Option&")),
                "N.Option"));
        }

        [Fact]
        public void EnumTypeIsChosenBeforeOtherReasons()
        {
            Assert.Equal(
                OutOfScopeReason.EnumType,
                Classify(
                    Types(Type("N.Kind", TypeKind.Enum), Type("N.IThing", TypeKind.Interface)),
                    Signatures(Parameterized("N.IThing.Run(N.Kind)", "N.IThing", "Run", "N.Kind")),
                    "N.Kind"));
        }

        [Fact]
        public void DelegateTypeIsChosenBeforeRouteAndArgumentOnly()
        {
            Assert.Equal(
                OutOfScopeReason.DelegateType,
                Classify(
                    Types(Type("N.Proc", TypeKind.Delegate), Type("N.IThing", TypeKind.Interface)),
                    Signatures(
                        Signature("N.Proc.Invoke()", "N.Proc", "Invoke", MemberKind.Method, "N.IThing"),
                        Parameterized("N.IThing.Run(N.Proc)", "N.IThing", "Run", "N.Proc")),
                    "N.Proc"));
        }

        [Fact]
        public void RouteIsChosenBeforeArgumentOnly()
        {
            Assert.Equal(
                OutOfScopeReason.Route,
                Classify(
                    Types(Type("N.IHub", TypeKind.Interface), Type("N.IThing", TypeKind.Interface)),
                    Signatures(
                        Signature("N.IHub.Thing()", "N.IHub", "Thing", MemberKind.Method, "N.IThing"),
                        Parameterized("N.IThing.Run(N.IHub)", "N.IThing", "Run", "N.IHub")),
                    "N.IHub"));
        }

        [Fact]
        public void SignaturesCanOnlyTakeTheRouteReason()
        {
            IList<TypeRecord> types = Types(Type("N.IHub", TypeKind.Interface), Type("N.IThing", TypeKind.Interface));
            SignatureRecord route =
                Signature("N.IHub.Thing()", "N.IHub", "Thing", MemberKind.Method, "N.IThing");
            SignatureRecord other =
                Signature("N.IHub.Name()", "N.IHub", "Name", MemberKind.Method, "System.String");
            OutOfScopeClassifier classifier = new OutOfScopeClassifier(
                Inventory(types, Signatures(route, other)),
                new HashSet<string>(StringComparer.Ordinal));

            Assert.Equal(OutOfScopeReason.Route, classifier.ClassifySignature(route));
            Assert.Null(classifier.ClassifySignature(other));
        }

        [Fact]
        public void NameThatIsNotAPublicTypeThrows()
        {
            OutOfScopeClassifier classifier = new OutOfScopeClassifier(
                Inventory(Types(), Signatures()), new HashSet<string>(StringComparer.Ordinal));

            Assert.Throws<ArgumentException>(() => classifier.ClassifyType("N.Missing"));
            Assert.Throws<ArgumentNullException>(() => classifier.ClassifyType(null));
            Assert.Throws<ArgumentNullException>(() => classifier.ClassifySignature(null));
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OutOfScopeClassifier(null, new HashSet<string>(StringComparer.Ordinal)));
            Assert.Throws<ArgumentNullException>(
                () => new OutOfScopeClassifier(Inventory(Types(), Signatures()), null));
        }

        private static OutOfScopeReason? Classify(
            IList<TypeRecord> types, IList<SignatureRecord> signatures, string name)
        {
            return new OutOfScopeClassifier(
                Inventory(types, signatures), new HashSet<string>(StringComparer.Ordinal))
                .ClassifyType(name);
        }

        private static TypeRecord Type(string name, TypeKind kind, params string[] baseTypes)
        {
            return new TypeRecord(name, kind, false, false, false, baseTypes.ToList(), new List<string>());
        }

        private static SignatureRecord Signature(
            string key, string declaringType, string memberName, MemberKind kind, string valueType)
        {
            return new SignatureRecord(
                key,
                declaringType,
                kind,
                memberName,
                false,
                0,
                new List<ParameterRecord>(),
                valueType,
                false,
                false,
                OperationDirection.Read);
        }

        private static SignatureRecord Parameterized(
            string key, string declaringType, string memberName, string parameterType)
        {
            return new SignatureRecord(
                key,
                declaringType,
                MemberKind.Method,
                memberName,
                false,
                0,
                new List<ParameterRecord>
                {
                    new ParameterRecord("value", parameterType, ParameterDirection.In, false),
                },
                "System.Void",
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
