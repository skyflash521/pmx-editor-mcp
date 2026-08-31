using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class LedgerCoverageRunnerTests : IDisposable
    {
        private const string EmptyBaseline = "{\"capabilities\":[]}\n";

        private const string EmptyOutOfScope = "{\"types\":[],\"signatures\":[]}\n";

        private readonly string _root;

        public LedgerCoverageRunnerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-coverage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void WrongArgumentCountEndsWithInvalidArguments()
        {
            foreach (int count in new[] { 0, 1, 2, 3, 4, 6 })
            {
                StringWriter error = new StringWriter();

                int code = LedgerCoverageRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = LedgerCoverageRunner.Run(
                new[] { Path.Combine(_root, "missing"), Write("l.md", Ledger()), Write("b.json", EmptyBaseline), Write("e.json", Excluded()), Write("o.json", EmptyOutOfScope) },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
        }

        [Fact]
        public void AnyMissingInputIsInputUnavailable()
        {
            string editor = CreateEditorDirectory();
            string[] good =
            {
                editor,
                Write("l.md", Ledger()),
                Write("b.json", EmptyBaseline),
                Write("e.json", Excluded()),
                Write("o.json", EmptyOutOfScope),
            };

            for (int i = 1; i < good.Length; i++)
            {
                string[] args = (string[])good.Clone();
                args[i] = Path.Combine(_root, "missing.json");
                StringWriter error = new StringWriter();

                int code = LedgerCoverageRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void InputWithWrongShapeIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = LedgerCoverageRunner.Run(
                new[] { CreateEditorDirectory(), Write("l.md", Ledger()), Write("b.json", "{"), Write("e.json", Excluded()), Write("o.json", EmptyOutOfScope) },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
        }

        [Fact]
        public void MatchingSetsWriteOneSummaryLineAndSucceed()
        {
            InventoryRecord inventory = Inventory();
            StringWriter output = new StringWriter();

            int code = LedgerCoverageRunner.Run(
                new[] { CreateEditorDirectory(), Write("l.md", Ledger()), Write("b.json", EmptyBaseline), Write("e.json", Excluded()), Write("o.json", EmptyOutOfScope) },
                output,
                new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Equal(
                string.Format(
                    "照合した: 型 {0} 件(台帳 {0}・対象外 0)・シグネチャ {1} 件(母集合 {1}・対象外 0)"
                        + "・除外 {2} 件・提供対象 {3} 件",
                    inventory.Types.Count,
                    inventory.Signatures.Count,
                    Records().Count,
                    inventory.Signatures.Count - Records().Count)
                    + Environment.NewLine,
                output.ToString());
        }

        [Fact]
        public void MismatchListsEveryExtraIdentifierInOrdinalOrder()
        {
            string outOfScope = "{\"types\":["
                + "{\"name\":\"ZZZ.Alpha\",\"reason\":\"route\"},"
                + "{\"name\":\"ZZZ.Beta\",\"reason\":\"route\"},"
                + "{\"name\":\"ZZZ.Gamma\",\"reason\":\"route\"}"
                + "],\"signatures\":[]}\n";
            StringWriter error = new StringWriter();

            int code = LedgerCoverageRunner.Run(
                new[] { CreateEditorDirectory(), Write("l.md", Ledger()), Write("b.json", EmptyBaseline), Write("e.json", Excluded()), Write("o.json", outOfScope) },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains(
                "公開型 に無い: ZZZ.Alpha / ZZZ.Beta / ZZZ.Gamma。",
                error.ToString(),
                StringComparison.Ordinal);
        }

        [Fact]
        public void SuccessfulRunDoesNotModifyInputs()
        {
            string editor = CreateEditorDirectory();
            string[] inputs =
            {
                Write("l.md", Ledger()),
                Write("b.json", EmptyBaseline),
                Write("e.json", Excluded()),
                Write("o.json", EmptyOutOfScope),
            };
            string[] before = Fingerprints();

            int code = LedgerCoverageRunner.Run(
                new[] { editor, inputs[0], inputs[1], inputs[2], inputs[3] },
                new StringWriter(),
                new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Equal(before, Fingerprints());
        }

        [Fact]
        public void DroppingALedgerRowReportsTheMissingTypeAndFailsCollation()
        {
            string dropped = WithoutTypeArguments(Inventory().Types[0].Name);
            StringWriter error = new StringWriter();

            int code = LedgerCoverageRunner.Run(
                new[] { CreateEditorDirectory(), Write("l.md", Ledger(1)), Write("b.json", EmptyBaseline), Write("e.json", Excluded()), Write("o.json", EmptyOutOfScope) },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains(dropped, error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void DroppingAnExclusionReportsItsKeyAndFailsCollation()
        {
            ExcludedSignatureRecord removed = Records()[0];
            string trimmed = ExcludedSignatureJson.Write(Records().Skip(1).ToList());
            StringWriter error = new StringWriter();

            int code = LedgerCoverageRunner.Run(
                new[] { CreateEditorDirectory(), Write("l.md", Ledger()), Write("b.json", EmptyBaseline), Write("e.json", trimmed), Write("o.json", EmptyOutOfScope) },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains(removed.Key, error.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// 試験用ディレクトリの全ファイルを、名前と中身と更新時刻の指紋の並びにしたもの。更新時刻
        /// まで見るのは、同じ中身で書き直す実装を中身の比較だけでは見分けられないためである。
        /// </summary>
        private string[] Fingerprints()
        {
            using (System.Security.Cryptography.SHA256 sha =
                System.Security.Cryptography.SHA256.Create())
            {
                return Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => string.Join(
                        " ",
                        p,
                        BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))),
                        File.GetLastWriteTimeUtc(p).Ticks.ToString(CultureInfo.InvariantCulture),
                        File.GetCreationTimeUtc(p).Ticks.ToString(CultureInfo.InvariantCulture),
                        File.GetAttributes(p).ToString()))
                    .ToArray();
            }
        }

        private string CreateEditorDirectory()
        {
            string editorDirectory = Path.Combine(_root, "editor");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(new Uri(typeof(ISampleApi).Assembly.CodeBase).LocalPath, assemblyPath);
            }

            return editorDirectory;
        }

        private string Write(string name, string content)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        private static InventoryRecord Inventory()
        {
            return AssemblyEnumerator.Enumerate(typeof(ISampleApi).Assembly);
        }

        private static IList<ExcludedSignatureRecord> Records()
        {
            return ExcludedSignatureBuilder.Build(new List<ExcludedBaselineEntry>(), Inventory());
        }

        private static string Excluded()
        {
            return ExcludedSignatureJson.Write(Records());
        }

        /// <summary>
        /// 公開型ごとに1行を置いた台帳。<paramref name="skip"/> で先頭から落とすと、覆えない型が
        /// 出る台帳になる。
        /// </summary>
        private static string Ledger(int skip = 0)
        {
            List<CapabilityRecord> rows = Rows(skip);
            IDictionary<string, int> counts = ExcludedCounts(rows);
            StringBuilder builder = new StringBuilder();
            builder.Append("| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n");
            builder.Append("|---|---|---|---|---|---|\n");

            foreach (CapabilityRecord row in rows)
            {
                int count;
                string remarks = counts.TryGetValue(row.Id, out count) && count > 0
                    ? "非対応件数: " + count.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                builder.Append(string.Format(
                    "| {0} | 標本 | {1} | {2} | {3} | {4} |\n",
                    row.Id,
                    row.Target,
                    row.Status == CapabilityStatus.Provided ? "提供" : "非対応",
                    row.Status == CapabilityStatus.Provided ? "モデル" : string.Empty,
                    remarks));
            }

            return builder.ToString();
        }

        /// <summary>公開型ごとに1行を置き、まとめて指す2行を末尾へ足した行の並び。</summary>
        private static List<CapabilityRecord> Rows(int skip)
        {
            List<CapabilityRecord> rows = new List<CapabilityRecord>();
            int id = 1;
            foreach (TypeRecord type in Inventory().Types.Skip(skip))
            {
                string target = WithoutTypeArguments(type.Name);
                rows.Add(new CapabilityRecord(
                    string.Format(CultureInfo.InvariantCulture, "CAP-{0:D3}", id++),
                    "標本",
                    target,
                    CapabilityTargetKind.Single,
                    new List<string> { target },
                    CapabilityStatus.Provided,
                    CapabilityOwner.Model,
                    string.Empty));
            }

            foreach (string pattern in new[] { "CAP-463", "CAP-466" })
            {
                rows.Add(new CapabilityRecord(
                    pattern,
                    "標本",
                    (pattern == "CAP-463" ? "PEPlugin.Pmd." : "PEPlugin.SDX.") + "* のまとめ",
                    CapabilityTargetKind.Pattern,
                    new List<string>(),
                    CapabilityStatus.NotSupported,
                    CapabilityOwner.None,
                    string.Empty));
            }

            return rows;
        }

        private static IDictionary<string, int> ExcludedCounts(IList<CapabilityRecord> rows)
        {
            LedgerPopulation population = LedgerPopulation.Resolve(rows, Inventory());
            ISet<string> removed = new HashSet<string>(
                Records().Select(r => r.Key), StringComparer.Ordinal);
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ISet<string>> owned in population.Owners)
            {
                if (!removed.Contains(owned.Key))
                {
                    continue;
                }

                foreach (string id in owned.Value)
                {
                    int current;
                    counts[id] = counts.TryGetValue(id, out current) ? current + 1 : 1;
                }
            }

            return counts;
        }

        private static string WithoutTypeArguments(string name)
        {
            StringBuilder builder = new StringBuilder(name.Length);
            int depth = 0;
            foreach (char c in name)
            {
                if (c == '<')
                {
                    depth++;
                }
                else if (c == '>')
                {
                    depth--;
                }
                else if (depth == 0)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
