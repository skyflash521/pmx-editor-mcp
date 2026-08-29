using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerParserTests
    {
        // 台帳の表と、その前後に置かれる散文・見出し・凡例を1つにした題材。実物に現れる対象の
        // 書き方——区切りの点がメンバーを指すもの・入れ子の型を指すもの・名前空間つきの名前を
        // 並べたもの・まとめて指す2通り・総称型の接尾辞つき——をすべて含める。
        private static readonly string[] LedgerLines =
        {
            "# PEPlugin SDK 能力台帳",
            string.Empty,
            "集計: 提供 7 / 非対応 4 / 要調査 1(計 12)",
            string.Empty,
            "| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |",
            "|---|---|---|---|---|---|",
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

        private static CapabilityRecord Find(string id)
        {
            CapabilityRecord found = Parse().SingleOrDefault(r => r.Id == id);
            Assert.True(found != null, "能力が見つからない: " + id);
            return found;
        }

        [Fact(Skip = "impl pending: 台帳の表の行だけを取り出す")]
        public void 表の行だけを拾う()
        {
            Assert.Equal(
                new[]
                {
                    "CAP-001", "CAP-002", "CAP-003", "CAP-004", "CAP-005", "CAP-006",
                    "CAP-007", "CAP-008", "CAP-009", "CAP-010", "CAP-011", "CAP-012",
                },
                Parse().Select(r => r.Id).ToArray());
        }

        [Fact(Skip = "impl pending: 名前を1つだけ書いた対象をその名前1件として扱う")]
        public void 名前が1つの対象は1件の名前になる()
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

        [Fact(Skip = "impl pending: 区切りの点を含む対象を分割せずに1件の名前として扱う")]
        public void 区切りの点で分割しない()
        {
            // 入れ子の型はメンバーと同じ書き方になるので、字面で分けると型を型とメンバーへ
            // 読み違える。どちらかは公開APIの一覧と突き合わせて決まる。
            CapabilityRecord nested = Find("CAP-006");

            Assert.Equal(CapabilityTargetKind.Single, nested.TargetKind);
            Assert.Equal(new[] { "PXEventArgs.UIModelMouse" }, nested.TargetNames.ToArray());
        }

        [Fact(Skip = "impl pending: 複数の名前を並べた対象を名前の並びへ分ける")]
        public void 並べた対象は名前ごとに分かれる()
        {
            // 実物に現れる要素の数は2・3・5と幅があるので、どれも固定する。
            CapabilityRecord two = Find("CAP-011");
            Assert.Equal(CapabilityTargetKind.Group, two.TargetKind);
            Assert.Equal(
                new[] { "IPERegisteredPluginInfo", "IPEPluginOption" }, two.TargetNames.ToArray());
            Assert.Equal("IPERegisteredPluginInfo / IPEPluginOption", two.Target);

            // 名前空間つきで書かれた要素も、そのままの形で残す。
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

        [Fact(Skip = "impl pending: まとめて指す対象を名前なしとして扱い原文を残す")]
        public void まとめて指す対象は名前を持たず原文を残す()
        {
            // どの名前を指すかが字面から決まらないので、推測で埋めると実在しない名前を指す行が
            // できる。判断の材料は原文だけなので、原文はそのまま残す。
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

        [Fact(Skip = "impl pending: 総称型の型引数の数を表す接尾辞を名前から取り除く")]
        public void 総称型の接尾辞は名前から落ちるが原文には残る()
        {
            CapabilityRecord record = Find("CAP-005");

            // 台帳は総称型を型引数の数の接尾辞つきで書き、公開APIの一覧は山括弧で書く。
            // 突き合わせ側が名前で解決できるよう、接尾辞は落とす。
            Assert.Equal(CapabilityTargetKind.Single, record.TargetKind);
            Assert.Equal(new[] { "IPEVmePrimaryValue" }, record.TargetNames.ToArray());
            Assert.Equal("IPEVmePrimaryValue`1", record.Target);
        }

        [Fact(Skip = "impl pending: ツール化するかどうかの分類を読み取る")]
        public void 分類を読み取る()
        {
            Assert.Equal(CapabilityStatus.Provided, Find("CAP-001").Status);
            Assert.Equal(CapabilityStatus.NotSupported, Find("CAP-007").Status);
            Assert.Equal(CapabilityStatus.NeedsInvestigation, Find("CAP-010").Status);
        }

        [Fact(Skip = "impl pending: 担当するツール契約の区分を読み取る")]
        public void 担当を読み取る()
        {
            Assert.Equal(CapabilityOwner.Model, Find("CAP-001").Owner);
            Assert.Equal(CapabilityOwner.Session, Find("CAP-002").Owner);
            Assert.Equal(CapabilityOwner.View, Find("CAP-003").Owner);
            Assert.Equal(CapabilityOwner.MotionTransform, Find("CAP-004").Owner);
            Assert.Equal(CapabilityOwner.None, Find("CAP-007").Owner);
        }

        [Fact(Skip = "impl pending: 大分類と備考を読み取る")]
        public void 大分類と備考を読み取る()
        {
            CapabilityRecord withRemarks = Find("CAP-002");
            Assert.Equal("本体フォーム", withRemarks.Category);
            Assert.Equal("危険操作", withRemarks.Remarks);

            Assert.Equal(string.Empty, Find("CAP-001").Remarks);
        }

        [Fact(Skip = "impl pending: 台帳を渡さないときは例外にする")]
        public void 台帳を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => LedgerParser.Parse(null));
        }

        [Fact(Skip = "impl pending: 表の行が無い文書を空の結果にする")]
        public void 表の行が無ければ空になる()
        {
            Assert.Empty(LedgerParser.Parse("見出しと散文だけの文書。"));
        }
    }
}
