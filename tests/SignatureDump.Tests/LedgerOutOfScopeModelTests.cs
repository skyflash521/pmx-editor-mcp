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
        public void 型の項目は名前と理由を持つ()
        {
            OutOfScopeTypeEntry entry = new OutOfScopeTypeEntry(TypeName, OutOfScopeReason.Route);

            Assert.Equal(TypeName, entry.Name);
            Assert.Equal(OutOfScopeReason.Route, entry.Reason);
        }

        [Fact]
        public void 型の項目の名前が空だと例外になる()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OutOfScopeTypeEntry(null, OutOfScopeReason.Route));
            Assert.Throws<ArgumentException>(
                () => new OutOfScopeTypeEntry(string.Empty, OutOfScopeReason.Route));
            Assert.Throws<ArgumentException>(
                () => new OutOfScopeTypeEntry("   ", OutOfScopeReason.Route));
        }

        [Fact]
        public void 理由はこの4つだけからなる()
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
        public void 型の項目は閉集合の4つの理由をすべて採れる()
        {
            Assert.Equal(
                OutOfScopeReason.EnumType,
                new OutOfScopeTypeEntry(TypeName, OutOfScopeReason.EnumType).Reason);
            Assert.Equal(
                OutOfScopeReason.DelegateType,
                new OutOfScopeTypeEntry(TypeName, OutOfScopeReason.DelegateType).Reason);
            Assert.Equal(
                OutOfScopeReason.Route,
                new OutOfScopeTypeEntry(TypeName, OutOfScopeReason.Route).Reason);
            Assert.Equal(
                OutOfScopeReason.ArgumentOnly,
                new OutOfScopeTypeEntry(TypeName, OutOfScopeReason.ArgumentOnly).Reason);
        }

        [Fact]
        public void 閉集合に無い理由は型の項目にも採れない()
        {
            Assert.Throws<ArgumentException>(
                () => new OutOfScopeTypeEntry(TypeName, (OutOfScopeReason)(-1)));
        }

        [Fact]
        public void シグネチャの項目は行キーと理由を持つ()
        {
            OutOfScopeSignatureEntry entry = new OutOfScopeSignatureEntry(Key, OutOfScopeReason.Route);

            Assert.Equal(Key, entry.Key);
            Assert.Equal(OutOfScopeReason.Route, entry.Reason);
        }

        [Fact]
        public void シグネチャの項目の行キーが空だと例外になる()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OutOfScopeSignatureEntry(null, OutOfScopeReason.Route));
            Assert.Throws<ArgumentException>(
                () => new OutOfScopeSignatureEntry(string.Empty, OutOfScopeReason.Route));
        }

        [Fact]
        public void 型ごと対象外になる理由はシグネチャの項目に採れない()
        {
            Assert.Throws<ArgumentException>(
                () => new OutOfScopeSignatureEntry(Key, OutOfScopeReason.EnumType));
            Assert.Throws<ArgumentException>(
                () => new OutOfScopeSignatureEntry(Key, OutOfScopeReason.DelegateType));
            Assert.Throws<ArgumentException>(
                () => new OutOfScopeSignatureEntry(Key, OutOfScopeReason.ArgumentOnly));
        }

        [Fact]
        public void 一覧は型とシグネチャの並びをそのまま持つ()
        {
            LedgerOutOfScopeRecord record = new LedgerOutOfScopeRecord(
                Types(
                    Type("PEPlugin.IPEConnector", OutOfScopeReason.Route),
                    Type("PEPlugin.Vme.OpType", OutOfScopeReason.EnumType)),
                Signatures(Signature("PEPlugin.IPEBuilder.Pmx()")));

            Assert.Equal(2, record.Types.Count);
            Assert.Equal("PEPlugin.IPEConnector", record.Types[0].Name);
            Assert.Equal(OutOfScopeReason.EnumType, record.Types[1].Reason);
            Assert.Single(record.Signatures);
            Assert.Equal("PEPlugin.IPEBuilder.Pmx()", record.Signatures[0].Key);
        }

        [Fact]
        public void 一覧は空の並びを受け取れる()
        {
            LedgerOutOfScopeRecord record = new LedgerOutOfScopeRecord(
                new List<OutOfScopeTypeEntry>(), new List<OutOfScopeSignatureEntry>());

            Assert.Empty(record.Types);
            Assert.Empty(record.Signatures);
        }

        [Fact]
        public void 一覧の並びは序数の昇順でないと例外になる()
        {
            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(
                    Type("PEPlugin.Vme.OpType", OutOfScopeReason.EnumType),
                    Type("PEPlugin.IPEConnector", OutOfScopeReason.Route)),
                Signatures()));

            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(),
                Signatures(Signature("PEPlugin.IPEBuilder.SC()"), Signature("PEPlugin.IPEBuilder.Pmx()"))));
        }

        [Fact]
        public void 一覧に同じ識別子が二度現れると例外になる()
        {
            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(
                    Type("PEPlugin.IPEConnector", OutOfScopeReason.Route),
                    Type("PEPlugin.IPEConnector", OutOfScopeReason.Route)),
                Signatures()));

            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(),
                Signatures(Signature(Key), Signature(Key))));
        }

        [Fact]
        public void 一覧に並びとしてnullを渡すと例外になる()
        {
            Assert.Throws<ArgumentNullException>(
                () => new LedgerOutOfScopeRecord(null, Signatures()));
            Assert.Throws<ArgumentNullException>(
                () => new LedgerOutOfScopeRecord(Types(), null));
        }

        [Fact]
        public void 一覧に中身のない項目を混ぜると例外になる()
        {
            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                new List<OutOfScopeTypeEntry> { null }, Signatures()));
            Assert.Throws<ArgumentException>(() => new LedgerOutOfScopeRecord(
                Types(), new List<OutOfScopeSignatureEntry> { null }));
        }

        [Fact]
        public void 一覧が公開する並びは後から変えられない()
        {
            LedgerOutOfScopeRecord record = new LedgerOutOfScopeRecord(
                Types(Type("PEPlugin.IPEConnector", OutOfScopeReason.Route)),
                Signatures(Signature(Key)));

            Assert.Throws<NotSupportedException>(
                () => record.Types.Add(Type("PEPlugin.Vme.OpType", OutOfScopeReason.EnumType)));
            Assert.Throws<NotSupportedException>(() => record.Types.Clear());
            Assert.Throws<NotSupportedException>(
                () => record.Types[0] = Type("PEPlugin.Vme.OpType", OutOfScopeReason.EnumType));
            Assert.Throws<NotSupportedException>(
                () => record.Signatures.Add(Signature("PEPlugin.IPEBuilder.SC()")));
            Assert.Throws<NotSupportedException>(() => record.Signatures.RemoveAt(0));
            Assert.Throws<NotSupportedException>(
                () => record.Signatures[0] = Signature("PEPlugin.IPEBuilder.SC()"));
        }

        [Fact]
        public void 一覧に渡した並びを後から変えても中身は変わらない()
        {
            List<OutOfScopeTypeEntry> types = new List<OutOfScopeTypeEntry>
            {
                Type("PEPlugin.IPEConnector", OutOfScopeReason.Route),
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

        private static OutOfScopeTypeEntry Type(string name, OutOfScopeReason reason)
        {
            return new OutOfScopeTypeEntry(name, reason);
        }

        private static OutOfScopeSignatureEntry Signature(string key)
        {
            return new OutOfScopeSignatureEntry(key, OutOfScopeReason.Route);
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
