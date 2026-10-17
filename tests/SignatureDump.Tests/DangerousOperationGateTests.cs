using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public class DangerousOperationGateTests
    {
        private const string Close = "PEPlugin.Form.IPEFormConnector.Close()";

        private const string ToFile = "PEPlugin.Pmx.IPXPmx.ToFile(System.String)";

        [Fact]
        public void TheSameSetWithTheSameKindsPasses()
        {
            DangerousOperationGate.Require(
                Map(Close, DangerKind.Shutdown, ToFile, DangerKind.Overwrite),
                Map(Close, DangerKind.Shutdown, ToFile, DangerKind.Overwrite));
        }

        [Fact]
        public void ASignatureTheLedgerDoesNotNoteStops()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => DangerousOperationGate.Require(
                    Map(Close, DangerKind.Shutdown, ToFile, DangerKind.Overwrite),
                    Map(Close, DangerKind.Shutdown)));

            Assert.Contains(ToFile, failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASignatureOnlyTheLedgerNotesStops()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => DangerousOperationGate.Require(
                    Map(Close, DangerKind.Shutdown),
                    Map(Close, DangerKind.Shutdown, ToFile, DangerKind.Overwrite)));

            Assert.Contains(ToFile, failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AKindThatDisagreesStops()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => DangerousOperationGate.Require(
                    Map(Close, DangerKind.Shutdown),
                    Map(Close, DangerKind.Reset)));

            Assert.Contains(Close, failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheInputsAreRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => DangerousOperationGate.Require(null, Map(Close, DangerKind.Shutdown)));
            Assert.Throws<ArgumentNullException>(
                () => DangerousOperationGate.Require(Map(Close, DangerKind.Shutdown), null));
        }

        private static IDictionary<string, DangerKind> Map(params object[] pairs)
        {
            Dictionary<string, DangerKind> map = new Dictionary<string, DangerKind>(StringComparer.Ordinal);
            for (int index = 0; index < pairs.Length; index += 2)
            {
                map.Add((string)pairs[index], (DangerKind)pairs[index + 1]);
            }

            return map;
        }
    }
}
