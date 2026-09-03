using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>所有の経路が、列挙のメンバーの並びとしてつながることを固定する。</summary>
    public sealed class OwnerPathGateTests
    {
        private const string Root = "N.IRoot";

        private const string Middle = "N.IMiddle";

        private const string Leaf = "N.ILeaf";

        private static readonly ElementCollectionRecord Middles =
            Owned("N.IRoot.Middles()", "N.IRoot.Middles()");

        [Fact]
        public void AChainThatFollowsTheEnumerationPasses()
        {
            Require(Middles, Owned("N.IMiddle.Leaves()", "N.IRoot.Middles()", "N.IMiddle.Leaves()"));
        }

        [Fact]
        public void ASingularStageBridgesTwoLists()
        {
            Require(
                Middles,
                Owned(
                    "N.ILeafHolder.Leaves()",
                    "N.IRoot.Middles()",
                    "N.IMiddle.Holder()",
                    "N.ILeafHolder.Leaves()"));
        }

        [Fact]
        public void AChainThatDoesNotEndWithItsOwnListStops()
        {
            Assert.Contains(
                "末尾",
                Stops(Middles, Owned("N.IMiddle.Leaves()", "N.IRoot.Middles()")));
        }

        [Fact]
        public void AChainWithAGapStops()
        {
            Assert.Contains(
                "つながっていない",
                Stops(
                    Middles,
                    Owned("N.IMiddle.Leaves()", "N.IRoot.Value()", "N.IMiddle.Leaves()")));
        }

        [Fact]
        public void AChainThatDoesNotStartAtARootStops()
        {
            Assert.Contains(
                "所有の根",
                Stops(Middles, Owned("N.IMiddle.Leaves()", "N.IMiddle.Leaves()")));
        }

        [Fact]
        public void AStageThatIsAListTheTableDoesNotOwnStops()
        {
            Assert.Contains(
                "所有しないリスト",
                Stops(
                    Middles,
                    Owned("N.ILeaf.Marks()", "N.IRoot.Refs()", "N.ILeaf.Marks()")));
        }

        [Fact]
        public void ASingularStageThatPointsAtAnOwnedTypeStops()
        {
            Assert.Contains(
                "所有される型を指す単数の段",
                Stops(
                    Middles,
                    Owned(
                        "N.IMiddle.Leaves()",
                        "N.IRoot.TheMiddle()",
                        "N.IMiddle.Leaves()")));
        }

        [Fact]
        public void AStageThatIsNotProvidedStops()
        {
            Assert.Contains(
                "N.IRoot.Absent()",
                Stops(Owned("N.IRoot.Middles()", "N.IRoot.Absent()", "N.IRoot.Middles()")));
        }

        [Fact]
        public void AStageThatIsNotAReadablePropertyStops()
        {
            Assert.Contains(
                "N.IRoot.Make()",
                Stops(Owned("N.IRoot.Middles()", "N.IRoot.Make()", "N.IRoot.Middles()")));
        }

        [Fact]
        public void ARootThatIsAnOwnedTypeStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => OwnerPathGate.Require(
                    new List<ElementCollectionRecord> { Middles },
                    Signatures(),
                    new[] { Root, Middle }));

            Assert.Contains(Middle, error.Message);
        }

        [Fact]
        public void AListThatIsNotOwnedIsNotChecked()
        {
            OwnerPathGate.Require(
                new List<ElementCollectionRecord>
                {
                    new ElementCollectionRecord("N.IRoot.Refs()", false, "根拠。"),
                },
                Signatures(),
                Roots());
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => OwnerPathGate.Require(null, Signatures(), Roots()));
            Assert.Throws<ArgumentNullException>(
                () => OwnerPathGate.Require(new List<ElementCollectionRecord>(), null, Roots()));
            Assert.Throws<ArgumentNullException>(
                () => OwnerPathGate.Require(
                    new List<ElementCollectionRecord>(), Signatures(), null));
        }

        private static void Require(params ElementCollectionRecord[] records)
        {
            OwnerPathGate.Require(records.ToList(), Signatures(), Roots());
        }

        private static string Stops(params ElementCollectionRecord[] records)
        {
            return Assert.Throws<InvalidOperationException>(() => Require(records)).Message;
        }

        private static IEnumerable<string> Roots()
        {
            return new[] { Root };
        }

        private static ElementCollectionRecord Owned(string signatureKey, params string[] path)
        {
            return new ElementCollectionRecord(
                signatureKey, true, signatureKey + " の根拠。", path.ToList());
        }

        private static IDictionary<string, SignatureRecord> Signatures()
        {
            return new[]
            {
                Property(Root, "Middles", List(Middle)),
                Property(Root, "Refs", List(Leaf)),
                Property(Root, "TheMiddle", Middle),
                Property(Root, "Value", "System.Int32"),
                Property(Middle, "Holder", "N.ILeafHolder"),
                Property(Middle, "Leaves", List(Leaf)),
                Property("N.ILeafHolder", "Leaves", List(Leaf)),
                Property(Leaf, "Marks", List("N.IMark")),
                Method(Root, "Make", Middle),
            }.ToDictionary(s => s.Key, StringComparer.Ordinal);
        }

        private static string List(string element)
        {
            return "System.Collections.Generic.IList<" + element + ">";
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
                OperationDirection.Read,
                false);
        }
    }
}
