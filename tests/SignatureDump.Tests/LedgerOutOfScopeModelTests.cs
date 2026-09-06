using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerOutOfScopeModelTests
    {
        private const string TypeName = "PEPlugin.View.IPEViewConnector";

        private const string Key = "PEPlugin.IPEBuilder.Pmx()";

        [Fact]
        public void TypeEntryCarriesTheNameAlone()
        {
            Assert.Equal(TypeName, new OutOfScopeTypeEntry(TypeName).Name);
        }

        [Fact]
        public void TypeEntryWithEmptyNameThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new OutOfScopeTypeEntry(null));
            Assert.Throws<ArgumentException>(() => new OutOfScopeTypeEntry(string.Empty));
            Assert.Throws<ArgumentException>(() => new OutOfScopeTypeEntry("   "));
        }

        [Fact]
        public void ReasonsConsistOfExactlyTheseFour()
        {
            OutOfScopeReason[] expected =
            {
                OutOfScopeReason.EnumType,
                OutOfScopeReason.DelegateType,
                OutOfScopeReason.Route,
                OutOfScopeReason.ArgumentOnly,
            };

            Assert.Equal(expected, (OutOfScopeReason[])Enum.GetValues(typeof(OutOfScopeReason)));
        }

        [Fact]
        public void SignatureEntryCarriesTheKeyAlone()
        {
            Assert.Equal(Key, new OutOfScopeSignatureEntry(Key).Key);
        }

        [Fact]
        public void SignatureEntryWithEmptyKeyThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new OutOfScopeSignatureEntry(null));
            Assert.Throws<ArgumentException>(() => new OutOfScopeSignatureEntry(string.Empty));
        }

        [Fact]
        public void RecordKeepsTypeAndSignatureCollectionsAsGiven()
        {
            LedgerOutOfScopeRecord record = new LedgerOutOfScopeRecord(
                Types(
                    Type("PEPlugin.IPEConnector"),
                    Type("PEPlugin.Vme.OpType")),
                Signatures(Signature("PEPlugin.IPEBuilder.Pmx()")));

            Assert.Equal(2, record.Types.Count);
            Assert.Equal("PEPlugin.IPEConnector", record.Types[0].Name);
            Assert.Equal("PEPlugin.Vme.OpType", record.Types[1].Name);
            Assert.Single(record.Signatures);
            Assert.Equal("PEPlugin.IPEBuilder.Pmx()", record.Signatures[0].Key);
        }

        [Fact]
        public void RecordAcceptsEmptyCollections()
        {
            LedgerOutOfScopeRecord record = new LedgerOutOfScopeRecord(
                new List<OutOfScopeTypeEntry>(), new List<OutOfScopeSignatureEntry>());

            Assert.Empty(record.Types);
            Assert.Empty(record.Signatures);
        }

        [Fact]
        public void CollectionsNotInAscendingOrdinalOrderThrow()
        {
            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(
                    Type("PEPlugin.Vme.OpType"),
                    Type("PEPlugin.IPEConnector")),
                Signatures()));

            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(),
                Signatures(Signature("PEPlugin.IPEBuilder.SC()"), Signature("PEPlugin.IPEBuilder.Pmx()"))));
        }

        [Fact]
        public void DuplicateIdentifierInACollectionThrows()
        {
            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(
                    Type("PEPlugin.IPEConnector"),
                    Type("PEPlugin.IPEConnector")),
                Signatures()));

            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(),
                Signatures(Signature(Key), Signature(Key))));
        }

        [Fact]
        public void NullCollectionThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => new LedgerOutOfScopeRecord(null, Signatures()));
            Assert.Throws<ArgumentNullException>(
                () => new LedgerOutOfScopeRecord(Types(), null));
        }

        [Fact]
        public void NullEntryInACollectionThrows()
        {
            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                new List<OutOfScopeTypeEntry> { null }, Signatures()));
            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(), new List<OutOfScopeSignatureEntry> { null }));
        }

        [Fact]
        public void PublishedCollectionsCannotBeChangedAfterwards()
        {
            LedgerOutOfScopeRecord record = new LedgerOutOfScopeRecord(
                Types(Type("PEPlugin.IPEConnector")),
                Signatures(Signature(Key)));

            Assert.Throws<NotSupportedException>(
                () => record.Types.Add(Type("PEPlugin.Vme.OpType")));
            Assert.Throws<NotSupportedException>(() => record.Types.Clear());
            Assert.Throws<NotSupportedException>(
                () => record.Types[0] = Type("PEPlugin.Vme.OpType"));
            Assert.Throws<NotSupportedException>(
                () => record.Signatures.Add(Signature("PEPlugin.IPEBuilder.SC()")));
            Assert.Throws<NotSupportedException>(() => record.Signatures.RemoveAt(0));
            Assert.Throws<NotSupportedException>(
                () => record.Signatures[0] = Signature("PEPlugin.IPEBuilder.SC()"));
        }

        [Fact]
        public void ChangingTheGivenCollectionsAfterwardsDoesNotAffectTheRecord()
        {
            List<OutOfScopeTypeEntry> types = new List<OutOfScopeTypeEntry>
            {
                Type("PEPlugin.IPEConnector"),
            };
            List<OutOfScopeSignatureEntry> signatures = new List<OutOfScopeSignatureEntry>
            {
                Signature(Key),
            };

            LedgerOutOfScopeRecord record = new LedgerOutOfScopeRecord(types, signatures);
            types.Clear();
            signatures.Clear();

            Assert.Single(record.Types);
            Assert.Single(record.Signatures);
        }

        private static OutOfScopeTypeEntry Type(string name)
        {
            return new OutOfScopeTypeEntry(name);
        }

        private static OutOfScopeSignatureEntry Signature(string key)
        {
            return new OutOfScopeSignatureEntry(key);
        }

        private static IList<OutOfScopeTypeEntry> Types(params OutOfScopeTypeEntry[] entries)
        {
            return new List<OutOfScopeTypeEntry>(entries);
        }

        private static IList<OutOfScopeSignatureEntry> Signatures(params OutOfScopeSignatureEntry[] entries)
        {
            return new List<OutOfScopeSignatureEntry>(entries);
        }
    }
}
