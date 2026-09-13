using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class MapCoverageGateTests
    {
        private const string Method = "Sdk.Form.Save(System.String)";

        private const string Property = "Sdk.Note.Text()";

        private const string Other = "Sdk.Note.Clear()";

        private const string NoteType = "Sdk.Note";

        [Fact]
        public void AProvidedSignatureThatHasARowPasses()
        {
            MapCoverageGate.Require(Provided(Method), Signatures(), Embedded(), Map(Method));
        }

        [Fact]
        public void AProvidedSignatureWithoutARowIsRefused()
        {
            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => MapCoverageGate.Require(Provided(Method), Signatures(), Embedded(), Map()));

            Assert.Contains("行を持たない提供対象がある", refused.Message, StringComparison.Ordinal);
            Assert.Contains(Method, refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowOnSomethingThatIsNotProvidedIsRefused()
        {
            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
                () => MapCoverageGate.Require(Provided(), Signatures(), Embedded(), Map(Method)));

            Assert.Contains("提供対象でない行がある", refused.Message, StringComparison.Ordinal);
            Assert.Contains(Method, refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheItemOfATypeThatIsEmbeddedNeedsNoProvidedSignature()
        {
            MapCoverageGate.Require(Provided(), Signatures(), Embedded(), Map(Property));
        }

        [Fact]
        public void TheMethodOfATypeThatIsEmbeddedNeedsNoRow()
        {
            MapCoverageGate.Require(Provided(Other), Signatures(), Embedded(), Map());
        }

        [Fact]
        public void TheConstructorOfATypeThatIsEmbeddedNeedsNoRow()
        {
            string key = "Sdk.Note..ctor()";
            IDictionary<string, SignatureRecord> signatures = Signatures();
            signatures.Add(key, Signature(key, NoteType, MemberKind.Constructor, ".ctor"));

            MapCoverageGate.Require(Provided(key), signatures, Embedded(), Map());
        }

        [Fact]
        public void AMemberWhoseValueIsATypeArgumentNeedsNoRow()
        {
            string key = "Sdk.Held<TValue>.Value()";
            IDictionary<string, SignatureRecord> signatures = Signatures();
            signatures.Add(
                key,
                new SignatureRecord(
                    key,
                    "Sdk.Held<TValue>",
                    MemberKind.Property,
                    "Value",
                    false,
                    0,
                    new ParameterRecord[0],
                    "TValue",
                    true,
                    false,
                    OperationDirection.Read,
                    valueTypeIsTypeArgument: true));

            MapCoverageGate.Require(Provided(key), signatures, Embedded(), Map());
        }

        private static ISet<string> Provided(params string[] keys)
        {
            return new HashSet<string>(keys, StringComparer.Ordinal);
        }

        private static ISet<string> Embedded()
        {
            return new HashSet<string>(new[] { NoteType }, StringComparer.Ordinal);
        }

        private static ToolMap Map(params string[] keys)
        {
            return new ToolMap(keys
                .Select(k => new ToolMapRow(
                    k, ToolMapEditKind.Read, null, "根拠。", null, null, null))
                .ToList());
        }

        private static IDictionary<string, SignatureRecord> Signatures()
        {
            return new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)
            {
                { Method, Signature(Method, "Sdk.Form", MemberKind.Method, "Save") },
                { Property, Signature(Property, NoteType, MemberKind.Property, "Text") },
                { Other, Signature(Other, NoteType, MemberKind.Method, "Clear") },
            };
        }

        private static SignatureRecord Signature(
            string key, string declaringType, MemberKind kind, string memberName, bool isStatic = false)
        {
            return new SignatureRecord(
                key,
                declaringType,
                kind,
                memberName,
                isStatic,
                0,
                new ParameterRecord[0],
                "System.Void",
                true,
                false,
                OperationDirection.Read);
        }
    }
}
