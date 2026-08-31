using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerCoverageTests
    {
        private const string Thing = "N.IThing";

        private const string Hub = "N.IHub";

        private const string Run = "N.IThing.Run()";

        private const string Route = "N.IHub.Thing()";

        [Fact]
        public void 型もシグネチャも過不足が無ければ内訳を返す()
        {
            LedgerCoverageResult result = LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing)),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route)));

            Assert.Equal(2, result.PublicTypes);
            Assert.Equal(1, result.LedgerTypes);
            Assert.Equal(1, result.OutOfScopeTypes);
            Assert.Equal(2, result.PublicSignatures);
            Assert.Equal(1, result.Population);
            Assert.Equal(0, result.OutOfScopeSignatures);
            Assert.Equal(0, result.Excluded);
            Assert.Equal(1, result.Provided);
        }

        [Fact]
        public void 足りない識別子は打ち切らず序数順で並べる()
        {
            string[] names = { "N.G", "N.F", "N.E", "N.D", "N.C", "N.B", "N.A" };
            IList<TypeRecord> types = names.Select(Type).ToList();
            types.Add(Type(Thing));
            IList<SignatureRecord> signatures = new List<SignatureRecord>
            {
                Method(Run, Thing, "Run", "System.Void"),
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LedgerCoverage.Verify(
                    Ledger(Row("CAP-001", Thing)),
                    new InventoryRecord("Sample", "1.0.0.0", types, new List<TypeRecord>(), signatures),
                    Baseline(),
                    Excluded(),
                    OutOfScope()));

            Assert.Contains(
                "に無い: N.A / N.B / N.C / N.D / N.E / N.F / N.G。",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void 台帳にも対象外一覧にも無い型があると照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing)),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope()));
        }

        [Fact]
        public void 台帳と対象外一覧の両方に在る型があると照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing), Row("CAP-002", Hub)),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 公開型に無い型が対象外一覧に在ると照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing), Row("CAP-002", Hub)),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry("N.IMissing", OutOfScopeReason.Route))));
        }

        [Fact]
        public void 型の理由が算出値と違うと照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing)),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.ArgumentOnly))));
        }

        [Fact]
        public void 対象外にできる理由が無い型を一覧へ載せると照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Hub)),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry(Thing, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 母集合にも対象外にも無いシグネチャがあると照合できない()
        {
            IList<TypeRecord> types = new List<TypeRecord> { Type(Thing), Type(Hub) };
            IList<SignatureRecord> signatures = new List<SignatureRecord>
            {
                Method(Run, Thing, "Run", "System.Void"),
                Method(Route, Hub, "Thing", Thing),
                Method("N.IHub.Name()", Hub, "Name", "System.String"),
            };

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing)),
                new InventoryRecord("Sample", "1.0.0.0", types, new List<TypeRecord>(), signatures),
                Baseline(),
                Excluded(),
                OutOfScope()));
        }

        [Fact]
        public void 行が指す型のメンバーはシグネチャ単位の対象外にできる()
        {
            LedgerCoverageResult result = LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing), Row("CAP-002", "IHub.Name")),
                WithNamedMember(),
                Baseline(),
                Excluded(),
                OutOfScope(new OutOfScopeTypeEntry[0], new[] { SignatureEntry(Route) }));

            Assert.Equal(2, result.LedgerTypes);
            Assert.Equal(0, result.OutOfScopeTypes);
            Assert.Equal(2, result.Population);
            Assert.Equal(1, result.OutOfScopeSignatures);
        }

        [Fact]
        public void シグネチャの理由が算出値と違うと照合できない()
        {
            IList<OutOfScopeSignatureEntry> signatures = new List<OutOfScopeSignatureEntry>
            {
                new OutOfScopeSignatureEntry("N.IHub.Name()", OutOfScopeReason.Route),
            };

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing), Row("CAP-002", "IHub.Thing")),
                WithNamedMember(),
                Baseline(),
                Excluded(),
                new LedgerOutOfScopeRecord(new List<OutOfScopeTypeEntry>(), signatures)));
        }

        [Fact]
        public void 公開シグネチャに無い行キーを一覧へ載せると照合できない()
        {
            IList<OutOfScopeSignatureEntry> signatures = new List<OutOfScopeSignatureEntry>
            {
                new OutOfScopeSignatureEntry("N.IHub.Missing()", OutOfScopeReason.Route),
            };

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing), Row("CAP-002", Hub)),
                Inventory(),
                Baseline(),
                Excluded(),
                new LedgerOutOfScopeRecord(new List<OutOfScopeTypeEntry>(), signatures)));
        }

        [Fact]
        public void 同じ能力IDが台帳に二度あると照合できない()
        {
            IList<CapabilityRecord> ledger = new List<CapabilityRecord>
            {
                Row("CAP-001", Thing),
                Row("CAP-001", Hub),
                Pattern("CAP-463"),
                Pattern("CAP-466"),
            };

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                ledger, Inventory(), Baseline(), Excluded(), OutOfScope()));
        }

        [Fact]
        public void 型ごと対象外の型が宣言するシグネチャを一覧へ重ねて書くと照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing)),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(
                    new[] { TypeEntry(Hub, OutOfScopeReason.Route) },
                    new[] { SignatureEntry(Route) })));
        }

        [Fact]
        public void 除外一覧が算出した期待集合と違うと照合できない()
        {
            IList<ExcludedSignatureRecord> wrong = new List<ExcludedSignatureRecord>
            {
                ExcludedSignatureRecord.FromBaseline(Run, "CAP-001"),
            };

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing)),
                Inventory(),
                Baseline(),
                wrong,
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 凍結した組を除外一覧が落としていると照合できない()
        {
            IList<ExcludedBaselineEntry> baseline = new List<ExcludedBaselineEntry>
            {
                new ExcludedBaselineEntry("CAP-001", new List<string> { Run }),
            };

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing)),
                Inventory(),
                baseline,
                Excluded(),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 備考の非対応件数が除外一覧と合うと照合できる()
        {
            IList<ExcludedBaselineEntry> baseline = Frozen();

            LedgerCoverageResult result = LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "非対応件数: 1")),
                Inventory(),
                baseline,
                ExcludedSignatureBuilder.Build(baseline, Inventory()),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route)));

            Assert.Equal(1, result.Excluded);
            Assert.Equal(0, result.Provided);
        }

        [Fact]
        public void ExcludedCountMayBeFollowedByRemarksAfterPeriod()
        {
            IList<ExcludedBaselineEntry> baseline = Frozen();

            LedgerCoverageResult result = LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "非対応件数: 1。契約注記: 代替を使う")),
                Inventory(),
                baseline,
                ExcludedSignatureBuilder.Build(baseline, Inventory()),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route)));

            Assert.Equal(1, result.Excluded);
        }

        [Fact]
        public void 備考の非対応件数が除外一覧と合わないと照合できない()
        {
            IList<ExcludedBaselineEntry> baseline = Frozen();

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "非対応件数: 2")),
                Inventory(),
                baseline,
                ExcludedSignatureBuilder.Build(baseline, Inventory()),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 除外があるのに非対応件数を書いていないと照合できない()
        {
            IList<ExcludedBaselineEntry> baseline = Frozen();

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Row("CAP-001", Thing)),
                Inventory(),
                baseline,
                ExcludedSignatureBuilder.Build(baseline, Inventory()),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 除外が無いのに非対応件数を書くと照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "非対応件数: 1")),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 除外が無い行に0を書いても照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "非対応件数: 0")),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 分類が提供でない行は正しい件数を書いても照合できない()
        {
            IList<ExcludedBaselineEntry> baseline = Frozen("CAP-002");
            IList<ExcludedSignatureRecord> excluded =
                ExcludedSignatureBuilder.Build(baseline, Inventory());

            foreach (CapabilityStatus status in
                new[] { CapabilityStatus.NotSupported, CapabilityStatus.NeedsInvestigation })
            {
                IList<CapabilityRecord> ledger = new List<CapabilityRecord>
                {
                    new CapabilityRecord(
                        "CAP-002",
                        "大分類",
                        Thing,
                        CapabilityTargetKind.Single,
                        new List<string> { Thing },
                        status,
                        CapabilityOwner.None,
                        "非対応件数: 1"),
                    Pattern("CAP-463"),
                    Pattern("CAP-466"),
                };

                Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                    ledger,
                    Inventory(),
                    baseline,
                    excluded,
                    OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
            }
        }

        [Fact]
        public void 非対応件数が数で終わっていないと照合できない()
        {
            IList<ExcludedBaselineEntry> baseline = Frozen();

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "非対応件数: 1件")),
                Inventory(),
                baseline,
                ExcludedSignatureBuilder.Build(baseline, Inventory()),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 備考に非対応件数が二度現れると照合できない()
        {
            IList<ExcludedBaselineEntry> baseline = Frozen();

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "非対応件数: 1。非対応件数: 2")),
                Inventory(),
                baseline,
                ExcludedSignatureBuilder.Build(baseline, Inventory()),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 基底型から継いだ除外も非対応件数に数える()
        {
            IList<TypeRecord> types = new List<TypeRecord>
            {
                Type("N.IBase"),
                Derived("N.IDerived", "N.IBase"),
            };
            IList<SignatureRecord> signatures = new List<SignatureRecord>
            {
                Method("N.IBase.Legacy()", "N.IBase", "Legacy", "System.Void"),
            };
            InventoryRecord inventory =
                new InventoryRecord("Sample", "1.0.0.0", types, new List<TypeRecord>(), signatures);
            IList<ExcludedBaselineEntry> baseline = new List<ExcludedBaselineEntry>
            {
                new ExcludedBaselineEntry("CAP-001", new List<string> { "N.IBase.Legacy()" }),
            };
            IList<ExcludedSignatureRecord> excluded =
                ExcludedSignatureBuilder.Build(baseline, inventory);

            LedgerCoverageResult result = LedgerCoverage.Verify(
                Ledger(
                    Counted("CAP-001", "IBase", "非対応件数: 1"),
                    Counted("CAP-002", "IDerived", "非対応件数: 1")),
                inventory,
                baseline,
                excluded,
                OutOfScope());

            Assert.Equal(1, result.Excluded);

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(
                    Counted("CAP-001", "IBase", "非対応件数: 1"),
                    Row("CAP-002", "IDerived")),
                inventory,
                baseline,
                excluded,
                OutOfScope()));
        }

        [Fact]
        public void 非対応件数が数になっていないと照合できない()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "非対応件数: 多数")),
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 非対応件数は備考の先頭にあるものだけを読む()
        {
            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                Ledger(Counted("CAP-001", Thing, "契約注記: 非対応件数: 1")),
                Inventory(),
                Frozen2(),
                ExcludedSignatureBuilder.Build(Frozen2(), Inventory()),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 提供対象の担当が2つに分かれると照合できない()
        {
            IList<CapabilityRecord> ledger = new List<CapabilityRecord>
            {
                Row("CAP-001", Thing, CapabilityOwner.Model),
                Row("CAP-002", "IThing.Run", CapabilityOwner.View),
                Pattern("CAP-463"),
                Pattern("CAP-466"),
            };

            Assert.Throws<InvalidOperationException>(() => LedgerCoverage.Verify(
                ledger,
                Inventory(),
                Baseline(),
                Excluded(),
                OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route))));
        }

        [Fact]
        public void 引数がnullだと例外になる()
        {
            IList<CapabilityRecord> ledger = Ledger(Row("CAP-001", Thing));
            LedgerOutOfScopeRecord outOfScope = OutOfScope(TypeEntry(Hub, OutOfScopeReason.Route));

            Assert.Throws<ArgumentNullException>(() => LedgerCoverage.Verify(
                null, Inventory(), Baseline(), Excluded(), outOfScope));
            Assert.Throws<ArgumentNullException>(() => LedgerCoverage.Verify(
                ledger, null, Baseline(), Excluded(), outOfScope));
            Assert.Throws<ArgumentNullException>(() => LedgerCoverage.Verify(
                ledger, Inventory(), null, Excluded(), outOfScope));
            Assert.Throws<ArgumentNullException>(() => LedgerCoverage.Verify(
                ledger, Inventory(), Baseline(), null, outOfScope));
            Assert.Throws<ArgumentNullException>(() => LedgerCoverage.Verify(
                ledger, Inventory(), Baseline(), Excluded(), null));
        }

        private static InventoryRecord Inventory()
        {
            IList<TypeRecord> types = new List<TypeRecord> { Type(Thing), Type(Hub) };
            IList<SignatureRecord> signatures = new List<SignatureRecord>
            {
                Method(Run, Thing, "Run", "System.Void"),
                Method(Route, Hub, "Thing", Thing),
            };

            return new InventoryRecord("Sample", "1.0.0.0", types, new List<TypeRecord>(), signatures);
        }

        /// <summary>経路でないメンバーを持たせて、その型を行が指せるようにした列挙。</summary>
        private static InventoryRecord WithNamedMember()
        {
            IList<TypeRecord> types = new List<TypeRecord> { Type(Thing), Type(Hub) };
            IList<SignatureRecord> signatures = new List<SignatureRecord>
            {
                Method(Run, Thing, "Run", "System.Void"),
                Method("N.IHub.Name()", Hub, "Name", "System.String"),
                Method(Route, Hub, "Thing", Thing),
            };

            return new InventoryRecord("Sample", "1.0.0.0", types, new List<TypeRecord>(), signatures);
        }

        private static TypeRecord Type(string name)
        {
            return new TypeRecord(
                name, TypeKind.Interface, false, false, false, new List<string>(), new List<string>());
        }

        private static TypeRecord Derived(string name, params string[] baseTypes)
        {
            return new TypeRecord(
                name, TypeKind.Interface, false, false, false, baseTypes.ToList(), new List<string>());
        }

        private static SignatureRecord Method(
            string key, string declaringType, string memberName, string valueType)
        {
            return new SignatureRecord(
                key,
                declaringType,
                MemberKind.Method,
                memberName,
                false,
                0,
                new List<ParameterRecord>(),
                valueType,
                false,
                false,
                OperationDirection.Read);
        }

        private static IList<CapabilityRecord> Ledger(params CapabilityRecord[] rows)
        {
            List<CapabilityRecord> ledger = new List<CapabilityRecord>(rows);
            ledger.Add(Pattern("CAP-463"));
            ledger.Add(Pattern("CAP-466"));
            return ledger;
        }

        private static CapabilityRecord Row(
            string id, string target, CapabilityOwner owner = CapabilityOwner.Model)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                target,
                CapabilityTargetKind.Single,
                new List<string> { target },
                CapabilityStatus.Provided,
                owner,
                string.Empty);
        }

        private static CapabilityRecord Counted(string id, string target, string remarks)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                target,
                CapabilityTargetKind.Single,
                new List<string> { target },
                CapabilityStatus.Provided,
                CapabilityOwner.Model,
                remarks);
        }

        private static CapabilityRecord Pattern(string id)
        {
            return new CapabilityRecord(
                id,
                "大分類",
                "N.* のまとめ",
                CapabilityTargetKind.Pattern,
                new List<string>(),
                CapabilityStatus.NotSupported,
                CapabilityOwner.None,
                string.Empty);
        }

        private static IList<ExcludedBaselineEntry> Baseline()
        {
            return new List<ExcludedBaselineEntry>();
        }

        /// <summary>行が指すシグネチャを1件だけ凍結した組。</summary>
        private static IList<ExcludedBaselineEntry> Frozen(string id = "CAP-001")
        {
            return new List<ExcludedBaselineEntry>
            {
                new ExcludedBaselineEntry(id, new List<string> { Run }),
            };
        }

        private static IList<ExcludedBaselineEntry> Frozen2()
        {
            return Frozen();
        }

        private static IList<ExcludedSignatureRecord> Excluded()
        {
            return new List<ExcludedSignatureRecord>();
        }

        private static OutOfScopeTypeEntry TypeEntry(string name, OutOfScopeReason reason)
        {
            return new OutOfScopeTypeEntry(name, reason);
        }

        private static OutOfScopeSignatureEntry SignatureEntry(string key)
        {
            return new OutOfScopeSignatureEntry(key, OutOfScopeReason.Route);
        }

        private static LedgerOutOfScopeRecord OutOfScope(params OutOfScopeTypeEntry[] types)
        {
            return OutOfScope(types, new OutOfScopeSignatureEntry[0]);
        }

        private static LedgerOutOfScopeRecord OutOfScope(
            OutOfScopeTypeEntry[] types, OutOfScopeSignatureEntry[] signatures)
        {
            return new LedgerOutOfScopeRecord(
                types.OrderBy(t => t.Name, StringComparer.Ordinal).ToList(),
                signatures.OrderBy(s => s.Key, StringComparer.Ordinal).ToList());
        }
    }
}
