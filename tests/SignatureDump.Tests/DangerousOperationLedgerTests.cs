using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public class DangerousOperationLedgerTests
    {
        private const string TypeName = "N.T";

        [Fact]
        public void ANoteIsResolvedToTheKeyOfItsSignature()
        {
            SignatureRecord save = Signature("Save", "System.String");

            IDictionary<string, DangerKind> noted = Read(
                "危険操作(上書き保存)。該当は Save(System.String)。", save, Signature("Do"));

            KeyValuePair<string, DangerKind> only = Assert.Single(noted);
            Assert.Equal(save.Key, only.Key);
            Assert.Equal(DangerKind.Overwrite, only.Value);
        }

        [Fact]
        public void EveryNoteInOneRemarkIsRead()
        {
            SignatureRecord save = Signature("Save", "System.String");
            SignatureRecord clear = Signature("Clear");

            IDictionary<string, DangerKind> noted = Read(
                "契約注記: 危険操作(上書き保存)。該当は Save(System.String)。"
                    + "危険操作(モデル初期化)。該当は Clear()。呼び出しには確認が要る",
                save,
                clear);

            Assert.Equal(2, noted.Count);
            Assert.Equal(DangerKind.Overwrite, noted[save.Key]);
            Assert.Equal(DangerKind.Reset, noted[clear.Key]);
        }

        [Fact]
        public void ARemarkWithoutANoteReadsAsNothing()
        {
            Assert.Empty(Read("ファイルパスを取る。", Signature("Save", "System.String")));
        }

        [Fact]
        public void AKindThatIsNotKnownStops()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => Read("危険操作(知らない種別)。該当は Save(System.String)。", Signature("Save", "System.String")));

            Assert.Contains("知らない種別", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASignatureThatTheCapabilityDoesNotOwnStops()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => Read("危険操作(上書き保存)。該当は Absent()。", Signature("Save", "System.String")));

            Assert.Contains("Absent()", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheInputsAreRequired()
        {
            SignatureRecord save = Signature("Save", "System.String");
            IList<CapabilityRecord> ledger = LedgerParser.Parse(Ledger(string.Empty));
            InventoryRecord inventory = Inventory(save);
            LedgerPopulation population = LedgerPopulation.Resolve(ledger, inventory);

            Assert.Throws<ArgumentNullException>(
                () => DangerousOperationLedger.Read(null, population, inventory));
            Assert.Throws<ArgumentNullException>(
                () => DangerousOperationLedger.Read(ledger, null, inventory));
            Assert.Throws<ArgumentNullException>(
                () => DangerousOperationLedger.Read(ledger, population, null));
        }

        private static IDictionary<string, DangerKind> Read(
            string remarks, params SignatureRecord[] signatures)
        {
            IList<CapabilityRecord> ledger = LedgerParser.Parse(Ledger(remarks));
            InventoryRecord inventory = Inventory(signatures);

            return DangerousOperationLedger.Read(
                ledger, LedgerPopulation.Resolve(ledger, inventory), inventory);
        }

        private static string Ledger(string remarks)
        {
            return "| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n"
                + "|---|---|---|---|---|---|\n"
                + "| CAP-001 | 標本 | " + TypeName + " | 提供 | モデル | " + remarks + " |\n"
                + "| CAP-463 | 標本 | PEPlugin.Pmd.* のまとめ | 非対応 |  |  |\n"
                + "| CAP-466 | 標本 | PEPlugin.SDX.* のまとめ | 非対応 |  |  |\n";
        }

        private static InventoryRecord Inventory(params SignatureRecord[] signatures)
        {
            return new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord> { Type() },
                new List<TypeRecord>(),
                signatures.ToList());
        }

        private static TypeRecord Type()
        {
            return new TypeRecord(
                TypeName,
                TypeKind.Interface,
                false,
                true,
                false,
                new ReadOnlyCollection<string>(new List<string>()),
                new ReadOnlyCollection<string>(new List<string>()));
        }

        private static SignatureRecord Signature(string memberName, string parameterType = null)
        {
            List<ParameterRecord> parameters = new List<ParameterRecord>();
            if (parameterType != null)
            {
                parameters.Add(new ParameterRecord("arg0", parameterType, ParameterDirection.In, false));
            }

            return new SignatureRecord(
                SignatureKeyBuilder.Build(TypeName, memberName, 0, parameters, "System.Void"),
                TypeName,
                MemberKind.Method,
                memberName,
                false,
                0,
                parameters,
                "System.Void",
                false,
                false,
                OperationDirection.Write);
        }
    }
}
