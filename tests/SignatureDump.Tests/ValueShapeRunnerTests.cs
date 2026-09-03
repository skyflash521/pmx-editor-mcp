using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ValueShapeRunnerTests : IDisposable
    {
        private const string EmptyExcluded = "{\"signatures\":[]}\n";

        private readonly string _root;

        public ValueShapeRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-shapes-" + Guid.NewGuid().ToString("N"));
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
            foreach (int count in new[] { 0, 1, 2, 3, 5 })
            {
                StringWriter error = new StringWriter();

                int code = ValueShapeRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ValueShapeRunner.Run(
                Arguments(Path.Combine(_root, "none"), Table()), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            string[] args = Arguments(Sdk(), Table());
            args[3] = Path.Combine(_root, "none.md");

            Assert.Equal(
                ExitCodes.InputUnavailable,
                ValueShapeRunner.Run(args, new StringWriter(), new StringWriter()));
        }

        [Fact]
        public void AnUnloadableAssemblyIsInputUnavailable()
        {
            Assert.Equal(
                ExitCodes.InputUnavailable,
                ValueShapeRunner.Run(
                    Arguments(Broken(), Table()), new StringWriter(), new StringWriter()));
        }

        [Fact]
        public void ADocumentWithoutTheSectionIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ValueShapeRunner.Run(
                Arguments(Sdk(), "## 別の節\n\n本文だけ。\n"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains(
                ValueShapeDocument.SectionHeading, error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ATypeMissingFromTheTableIsUnresolved()
        {
            string dropped = Mapped()[0];
            StringWriter error = new StringWriter();

            int code = ValueShapeRunner.Run(
                Arguments(Sdk(), Table(dropped)), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("値の表現が規則に合わない。", error.ToString(), StringComparison.Ordinal);
            Assert.Contains(dropped, error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMatchingTableWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = ValueShapeRunner.Run(Arguments(Sdk(), Table()), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            IList<string> mapped = Mapped();
            Assert.Equal(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "照合した: 型 {0} 件・綴り {1} 種・包む型 {2} 件",
                    mapped.Count,
                    Spellings().Distinct(StringComparer.Ordinal).Count(),
                    mapped.Count - Spellings().Count),
                line);
            Assert.True(mapped.Count > 0);
        }

        [Fact]
        public void ASuccessfulRunDoesNotWriteAnyFile()
        {
            string[] args = Arguments(Sdk(), Table());
            string[] before = Fingerprints();

            Assert.Equal(
                ExitCodes.Success,
                ValueShapeRunner.Run(args, new StringWriter(), new StringWriter()));
            Assert.Equal(before, Fingerprints());
        }

        /// <summary>
        /// 試験用ディレクトリの全ファイルを、名前と中身と更新時刻の指紋の並びにしたもの。入力だけを
        /// 見ると、導入ディレクトリの隣へ書き出す実装を見逃す。
        /// </summary>
        private string[] Fingerprints()
        {
            using (SHA256 sha = SHA256.Create())
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

        /// <summary>題材の値として写せる型を並べた仕様書。除く型を渡すとその行だけを落とす。</summary>
        private static string Table(string dropped = null)
        {
            ValueRepresentationRule rule = ValueRepresentationRule.Create(
                AssemblyEnumerator.Enumerate(Sample));
            StringBuilder builder = new StringBuilder("## 値の表現\n\n")
                .Append(ValueShapeDocument.SectionHeading)
                .Append("\n\n| 型 | 表現 |\n|---|---|\n");
            foreach (string type in Mapped().Where(t => !string.Equals(t, dropped, StringComparison.Ordinal)))
            {
                ValueRepresentation representation;
                builder.Append("| `").Append(type).Append("` | ")
                    .Append(rule.TryClassify(type, out representation)
                        ? "`" + representation.Identifier + "`"
                        : "要素の表現を包む")
                    .Append(" |\n");
            }

            return builder.ToString();
        }

        /// <summary>題材の提供対象が値として用いる型。綴りの昇順に並べる。</summary>
        private static IList<string> Mapped()
        {
            return TypeRolePopulation.Resolve(
                    LedgerParser.Parse(Ledger()),
                    AssemblyEnumerator.Enumerate(Sample),
                    new List<ExcludedSignatureRecord>())
                .ValueMapped
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>題材の型のうち、表現の綴りが1つに決まるものの綴り。</summary>
        private static IList<string> Spellings()
        {
            ValueRepresentationRule rule = ValueRepresentationRule.Create(
                AssemblyEnumerator.Enumerate(Sample));
            List<string> spellings = new List<string>();
            foreach (string type in Mapped())
            {
                ValueRepresentation representation;
                if (rule.TryClassify(type, out representation))
                {
                    spellings.Add(representation.Identifier);
                }
            }

            return spellings;
        }

        /// <summary>題材のアセンブリの公開型を提供として並べ、まとめて指す行を足した台帳。</summary>
        private static string Ledger()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n");
            builder.Append("|---|---|---|---|---|---|\n");

            int id = 1;
            foreach (TypeRecord type in AssemblyEnumerator.Enumerate(Sample).Types)
            {
                builder.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    "| CAP-{0:D3} | 標本 | {1} | 提供 | モデル |  |\n",
                    id++,
                    WithoutTypeArguments(type.Name)));
            }

            builder.Append("| CAP-463 | 標本 | PEPlugin.Pmd.* のまとめ | 非対応 |  |  |\n");
            builder.Append("| CAP-466 | 標本 | PEPlugin.SDX.* のまとめ | 非対応 |  |  |\n");

            return builder.ToString();
        }

        private static string WithoutTypeArguments(string typeName)
        {
            int open = typeName.IndexOf('<');

            return open < 0 ? typeName : typeName.Substring(0, open);
        }

        private static Assembly Sample
        {
            get { return typeof(ValueShapeRunnerTests).Assembly; }
        }

        private string[] Arguments(string editorDirectory, string document)
        {
            return new[]
            {
                editorDirectory,
                Write("l.md", Ledger()),
                Write("e.json", EmptyExcluded),
                Write("c.md", document),
            };
        }

        /// <summary>題材のアセンブリを対象アセンブリとして置いた導入ディレクトリを作る。</summary>
        private string Sdk()
        {
            string directory = Path.Combine(_root, "sdk");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(new Uri(Sample.CodeBase).LocalPath, assemblyPath);
            }

            return directory;
        }

        /// <summary>読み込めないアセンブリを置いた導入ディレクトリを作る。</summary>
        private string Broken()
        {
            string directory = Path.Combine(_root, "broken");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            File.WriteAllBytes(assemblyPath, new byte[] { 0x4D, 0x5A });

            return directory;
        }

        private string Write(string name, string text)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllText(path, text);

            return path;
        }
    }
}
