using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerParserTests
    {
        private const string HeaderRow = "| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |";

        private const string SeparatorRow = "|---|---|---|---|---|---|";

        private static readonly string[] LedgerLines =
        {
            "# PEPlugin SDK 能力台帳",
            string.Empty,
            "集計: 提供 7 / 非対応 4 / 要調査 1(計 12)",
            string.Empty,
            HeaderRow,
            SeparatorRow,
            "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |",
            "| CAP-002 | 本体フォーム | IPEFormConnector.Close | 提供 | セッション | 危険操作 |",
            "| CAP-003 | ビュー | IPXPmxViewConnector | 提供 | ビュー | 型単位 |",
            "| CAP-004 | モーション | IPEVmeBone.SetPosition | 提供 | 変形・モーション |  |",
            "| CAP-005 | VMEデータ型 | IPEVmePrimaryValue`1 | 提供 | 変形・モーション | 総称型 |",
            "| CAP-006 | Cプラグイン連携 | PXEventArgs.UIModelMouse | 提供 | ビュー | 入れ子の公開データ型(型単位) |",
            "| CAP-007 | Cプラグイン実装拡張点 | PXCPlugin.RegisterBase / IPXCPlugin / PXCPluginClass | 非対応 |  | 実装専用 |",
            "| CAP-008 | SDX数値型 | PEPlugin.SDX.*(M・Q・V2) | 非対応 |  | 数値計算のため対象外 |",
            "| CAP-009 | PMDレガシー | PEPlugin.Pmd.* のコネクタ・データ型と IPEBuilder のPMD/X系生成 | 非対応 |  | PMX系に同等機能 |",
            "| CAP-010 | 未確定 | IPXThing.Probe | 要調査 |  | 実機で確定する |",
            "| CAP-011 | プラグイン情報 | IPERegisteredPluginInfo / IPEPluginOption | 提供 | セッション | 読み取り専用 |",
            "| CAP-012 | プラグイン機構 | IPEPlugin / PEPluginClass / PEPluginOption / IPERunArgs / PECheckResult | 非対応 |  | 実装専用 |",
            string.Empty,
            "表の外に書かれた散文。ここは行として拾わない。",
        };

        private static IList<CapabilityRecord> Parse()
        {
            return LedgerParser.Parse(string.Join("\n", LedgerLines));
        }

        private static string Compose(params string[] lines)
        {
            return string.Join("\n", lines);
        }

        private static CapabilityRecord Find(string id)
        {
            CapabilityRecord found = Parse().SingleOrDefault(r => r.Id == id);
            Assert.True(found != null, "能力が見つからない: " + id);
            return found;
        }

        [Fact]
        public void PicksUpOnlyTableRows()
        {
            Assert.Equal(
                new[]
                {
                    "CAP-001", "CAP-002", "CAP-003", "CAP-004", "CAP-005", "CAP-006",
                    "CAP-007", "CAP-008", "CAP-009", "CAP-010", "CAP-011", "CAP-012",
                },
                Parse().Select(r => r.Id).ToArray());
        }

        [Fact]
        public void SingleNameTargetYieldsOneName()
        {
            CapabilityRecord bare = Find("CAP-003");
            Assert.Equal(CapabilityTargetKind.Single, bare.TargetKind);
            Assert.Equal(new[] { "IPXPmxViewConnector" }, bare.TargetNames.ToArray());
            Assert.Equal("IPXPmxViewConnector", bare.Target);

            CapabilityRecord dotted = Find("CAP-001");
            Assert.Equal(CapabilityTargetKind.Single, dotted.TargetKind);
            Assert.Equal(new[] { "IPXPmxConnector.GetCurrentState" }, dotted.TargetNames.ToArray());
            Assert.Equal("IPXPmxConnector.GetCurrentState", dotted.Target);
        }

        /// <summary>
        /// 入れ子の型はメンバーと同じ書き方になるので、字面で分けると型を型とメンバーへ
        /// 読み違える。どちらかは公開APIの一覧と突き合わせて決まる。
        /// </summary>
        [Fact]
        public void DoesNotSplitOnTheDotSeparator()
        {
            CapabilityRecord nested = Find("CAP-006");

            Assert.Equal(CapabilityTargetKind.Single, nested.TargetKind);
            Assert.Equal(new[] { "PXEventArgs.UIModelMouse" }, nested.TargetNames.ToArray());
        }

        [Fact]
        public void ListedTargetsAreSplitPerName()
        {
            CapabilityRecord two = Find("CAP-011");
            Assert.Equal(CapabilityTargetKind.Group, two.TargetKind);
            Assert.Equal(
                new[] { "IPERegisteredPluginInfo", "IPEPluginOption" }, two.TargetNames.ToArray());
            Assert.Equal("IPERegisteredPluginInfo / IPEPluginOption", two.Target);

            CapabilityRecord three = Find("CAP-007");
            Assert.Equal(CapabilityTargetKind.Group, three.TargetKind);
            Assert.Equal(
                new[] { "PXCPlugin.RegisterBase", "IPXCPlugin", "PXCPluginClass" }, three.TargetNames.ToArray());
            Assert.Equal("PXCPlugin.RegisterBase / IPXCPlugin / PXCPluginClass", three.Target);

            CapabilityRecord five = Find("CAP-012");
            Assert.Equal(CapabilityTargetKind.Group, five.TargetKind);
            Assert.Equal(
                new[]
                {
                    "IPEPlugin", "PEPluginClass", "PEPluginOption", "IPERunArgs", "PECheckResult",
                },
                five.TargetNames.ToArray());
            Assert.Equal(
                "IPEPlugin / PEPluginClass / PEPluginOption / IPERunArgs / PECheckResult", five.Target);
        }

        /// <summary>
        /// どの名前を指すかが字面から決まらないので、推測で埋めると実在しない名前を指す行が
        /// できる。判断の材料は原文だけなので、原文はそのまま残す。
        /// </summary>
        [Fact]
        public void PatternTargetHasNoNamesAndKeepsTheRawText()
        {
            CapabilityRecord parenthesized = Find("CAP-008");
            Assert.Equal(CapabilityTargetKind.Pattern, parenthesized.TargetKind);
            Assert.Empty(parenthesized.TargetNames);
            Assert.Equal("PEPlugin.SDX.*(M・Q・V2)", parenthesized.Target);

            CapabilityRecord withProse = Find("CAP-009");
            Assert.Equal(CapabilityTargetKind.Pattern, withProse.TargetKind);
            Assert.Empty(withProse.TargetNames);
            Assert.Equal(
                "PEPlugin.Pmd.* のコネクタ・データ型と IPEBuilder のPMD/X系生成", withProse.Target);
        }

        [Fact]
        public void GenericAritySuffixIsDroppedFromNamesButKeptInRawText()
        {
            CapabilityRecord record = Find("CAP-005");

            Assert.Equal(CapabilityTargetKind.Single, record.TargetKind);
            Assert.Equal(new[] { "IPEVmePrimaryValue" }, record.TargetNames.ToArray());
            Assert.Equal("IPEVmePrimaryValue`1", record.Target);

            IList<CapabilityRecord> others = LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | VMEデータ型 | IPEVmePair`2 | 提供 | 変形・モーション |  |",
                "| CAP-002 | VMEデータ型 | IPEVmeWide`10 | 提供 | 変形・モーション |  |"));

            Assert.Equal(
                new[] { "IPEVmePair", "IPEVmeWide" },
                others.Select(r => r.TargetNames.Single()).ToArray());
        }

        /// <summary>
        /// 落とすと、台帳の誤記が実在する非総称型の名前へ化けて照合に通ってしまう。
        /// </summary>
        [Fact]
        public void SuffixThatCannotBeAGenericArityIsNotDropped()
        {
            IList<CapabilityRecord> records = LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | VMEデータ型 | IPEVmePrimaryValue`0 | 提供 | 変形・モーション |  |",
                "| CAP-002 | VMEデータ型 | IPEVmePrimaryValue`01 | 提供 | 変形・モーション |  |"));

            Assert.Equal(
                new[] { "IPEVmePrimaryValue`0", "IPEVmePrimaryValue`01" },
                records.Select(r => r.TargetNames.Single()).ToArray());
        }

        [Fact]
        public void ReadsTheStatusColumn()
        {
            Assert.Equal(CapabilityStatus.Provided, Find("CAP-001").Status);
            Assert.Equal(CapabilityStatus.NotSupported, Find("CAP-007").Status);
            Assert.Equal(CapabilityStatus.NeedsInvestigation, Find("CAP-010").Status);
        }

        [Fact]
        public void ReadsTheOwnerColumn()
        {
            Assert.Equal(CapabilityOwner.Model, Find("CAP-001").Owner);
            Assert.Equal(CapabilityOwner.Session, Find("CAP-002").Owner);
            Assert.Equal(CapabilityOwner.View, Find("CAP-003").Owner);
            Assert.Equal(CapabilityOwner.MotionTransform, Find("CAP-004").Owner);
            Assert.Equal(CapabilityOwner.None, Find("CAP-007").Owner);
        }

        [Fact]
        public void ReadsTheCategoryAndRemarksColumns()
        {
            CapabilityRecord withRemarks = Find("CAP-002");
            Assert.Equal("本体フォーム", withRemarks.Category);
            Assert.Equal("危険操作", withRemarks.Remarks);

            Assert.Equal(string.Empty, Find("CAP-001").Remarks);
        }

        [Fact]
        public void MissingLedgerThrows()
        {
            Assert.Throws<ArgumentNullException>(() => LedgerParser.Parse(null));
        }

        [Fact]
        public void NoTableRowsYieldAnEmptyResult()
        {
            Assert.Empty(LedgerParser.Parse("見出しと散文だけの文書。"));
        }

        /// <summary>
        /// 同じ列の数を持つ別の表や、表を作らない単独の行を能力として数えないため。
        /// </summary>
        [Fact]
        public void RowsWithoutTheCapabilityHeaderAreNotPickedUp()
        {
            Assert.Empty(LedgerParser.Parse(Compose(
                "| 名前 | 版 |",
                "|---|---|",
                "| PEPlugin.dll | 0.0.8.9 |",
                string.Empty,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |")));
        }

        [Fact]
        public void HeaderWithoutASeparatorRowThrows()
        {
            Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |")));
        }

        /// <summary>
        /// 読み飛ばすと、その能力に対する突き合わせが行われないまま検査が通ってしまう。
        /// </summary>
        [Fact]
        public void RowWithWrongColumnCountThrowsInsteadOfBeingSkipped()
        {
            Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  | 余り |")));

            Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |")));
        }

        [Fact]
        public void EscapedPipeBecomesCellContent()
        {
            CapabilityRecord record = LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル | 縦棒 \\| 入り |"))
                .Single();

            Assert.Equal("縦棒 | 入り", record.Remarks);
        }

        [Fact]
        public void EscapedBackslashBecomesOneCharacter()
        {
            IList<CapabilityRecord> records = LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル | 末尾は \\\\ |"));

            Assert.Equal("末尾は \\", records.Single().Remarks);
        }

        /// <summary>
        /// バックスラッシュを無条件に落とすと、記号を含む名前が別の名前へ化けて後段の照合に通る。
        /// </summary>
        [Fact]
        public void BackslashThatIsNotAnEscapeIsKept()
        {
            CapabilityRecord record = LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル | 配置先は _plugin\\PmxEditorMcp |"))
                .Single();

            Assert.Equal("配置先は _plugin\\PmxEditorMcp", record.Remarks);
        }

        /// <summary>
        /// 端の縦棒が無い行を表の終わりと見なすと、そこから先の能力を黙って読み落とす。
        /// </summary>
        [Fact]
        public void EdgePipesMayBeOmitted()
        {
            IList<CapabilityRecord> records = LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル | 経路 |",
                "| CAP-002 | ビュー | IPXPmxViewConnector | 提供 | ビュー | 型単位",
                "| CAP-003 | モーション | IPEVmeBone.SetPosition | 提供 | 変形・モーション |  |"));

            Assert.Equal(new[] { "CAP-001", "CAP-002", "CAP-003" }, records.Select(r => r.Id).ToArray());
        }

        /// <summary>
        /// 知らない語を既知の値へ黙って倒す誤りと区別するため、止まった理由がその語であることまで
        /// 見る。
        /// </summary>
        [Fact]
        public void UnknownStatusOrOwnerThrows()
        {
            FormatException status = Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 保留 |  |  |")));
            Assert.Contains("保留", status.Message, StringComparison.Ordinal);

            FormatException owner = Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | 物理 |  |")));
            Assert.Contains("物理", owner.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// 表が終わったことにすると、そこから先の能力が検査されないまま通ってしまう。
        /// </summary>
        [Fact]
        public void RowWithoutAnyPipeInsideTheTableThrows()
        {
            Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |",
                "CAP-002 ビュー IPXPmxViewConnector 提供 ビュー",
                "| CAP-003 | モーション | IPEVmeBone.SetPosition | 提供 | 変形・モーション |  |")));
        }

        [Fact]
        public void TableEndsAtTheRowWhereAnotherBlockStarts()
        {
            // Markdownの表は空行を挟まなくても、次の構造が始まればそこで終わる。
            string capability = "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |";
            string[] structures =
            {
                "> 表の後に続く引用。",
                "## 次の見出し",
                "- 箇条書き",
                "1. 番号つきの箇条書き",
                "```",
                "<div>",
                "[ラベル]: https://example.invalid/",
            };

            foreach (string structure in structures)
            {
                IList<CapabilityRecord> records =
                    LedgerParser.Parse(Compose(HeaderRow, SeparatorRow, capability, structure));

                Assert.True(
                    records.Count == 1 && records[0].Id == "CAP-001", "表が終わらない: " + structure);
            }

            // 下線で示す見出しは行頭の字が普通の文なので、下線の側から見分ける。
            IList<CapabilityRecord> underlined = LedgerParser.Parse(Compose(
                HeaderRow, SeparatorRow, capability, "次の見出し", "------"));

            Assert.Equal("CAP-001", underlined.Single().Id);

            IList<CapabilityRecord> beforeUnderline = LedgerParser.Parse(Compose(
                HeaderRow, SeparatorRow, capability, "------"));

            Assert.Equal("CAP-001", beforeUnderline.Single().Id);
        }

        /// <summary>
        /// 区切りでない行を区切りとして受けると、その次から能力の行として読み進めてしまう。
        /// </summary>
        [Fact]
        public void SeparatorRowWithAWrongShapeThrows()
        {
            Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                "|:|:|:|:|:|:|",
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |")));
        }

        [Fact]
        public void SeparatorRowAcceptsAlignmentMarkersAndShortForms()
        {
            // Markdownの表の区切りはハイフン1つで成立し、コロンで寄せを指定できる。
            IList<CapabilityRecord> records = LedgerParser.Parse(Compose(
                HeaderRow,
                "| - | :- | -: | :-: | --- | :---: |",
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |"));

            Assert.Equal("CAP-001", records.Single().Id);
        }

        [Fact]
        public void RowWhoseStatusAndOwnerDisagreeThrows()
        {
            Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 |  |  |")));

            Assert.Throws<FormatException>(() => LedgerParser.Parse(Compose(
                HeaderRow,
                SeparatorRow,
                "| CAP-001 | プラグイン機構 | IPEPlugin | 非対応 | モデル |  |")));
        }
    }
}
