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
    public sealed class TypeRoleRunnerTests : IDisposable
    {
        private const string EmptyExcluded = "{\"signatures\":[]}\n";

        private const string EmptyTable = "{\"types\":[],\"issuances\":[]}\n";

        private readonly string _root;

        public TypeRoleRunnerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-roles-" + Guid.NewGuid().ToString("N"));
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

                int code = TypeRoleRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = TypeRoleRunner.Run(
                Arguments(Path.Combine(_root, "missing"), EmptyTable), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2, 3 })
            {
                string[] args = Arguments(Sdk(), EmptyTable);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = TypeRoleRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = TypeRoleRunner.Run(Arguments(Sdk(), "{"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AnUnloadableAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = TypeRoleRunner.Run(
                Arguments(Broken(), EmptyTable), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ALedgerThatDoesNotResolveIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = TypeRoleRunner.Run(
                new[]
                {
                    Sdk(),
                    Write("empty.md", "| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n|---|---|---|---|---|---|\n"),
                    Write("e.json", EmptyExcluded),
                    Write("t.json", EmptyTable),
                },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("役割の根拠を決められない。", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ATableThatBreaksTheRulesIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = TypeRoleRunner.Run(Arguments(Sdk(), EmptyTable), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("型役割が規則に合わない。", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("表に無い型が在る", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMatchingTableWritesOneSummaryLineAndSucceeds()
        {
            ISet<string> population = Population();
            StringWriter output = new StringWriter();

            int code = TypeRoleRunner.Run(
                Arguments(Sdk(), Table()), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "照合した: 型 {0} 件(コネクタ {1}・イベント引数 0・ハンドル操作 0・操作対象 0"
                        + "・DTO {2})・ハンドルを返しうる行 0 件(発行 0)",
                    population.Count,
                    TypeRoleEvidence.ConnectionRoots.Count,
                    population.Count - TypeRoleEvidence.ConnectionRoots.Count),
                line);
        }

        [Fact]
        public void ASuccessfulRunDoesNotWriteAnyFile()
        {
            string[] args = Arguments(Sdk(), Table());
            string[] before = Fingerprints();

            Assert.Equal(ExitCodes.Success, TypeRoleRunner.Run(args, new StringWriter(), new StringWriter()));
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

        /// <summary>題材の母集合へ、規則を満たす役割を割り当てた正本。</summary>
        private static string Table()
        {
            HashSet<string> roots = new HashSet<string>(
                TypeRoleEvidence.ConnectionRoots, StringComparer.Ordinal);
            StringBuilder builder = new StringBuilder("{\"types\":[");
            int index = 0;
            foreach (string name in Population().OrderBy(n => n, StringComparer.Ordinal))
            {
                builder.Append(index == 0 ? string.Empty : ",");
                builder.Append("{\"typeName\":\"").Append(name).Append("\",\"role\":\"")
                    .Append(roots.Contains(name) ? "connector" : "dto")
                    .Append("\",\"basis\":\"題材の根拠。\"");
                if (roots.Contains(name))
                {
                    builder.Append(",\"elementNoun\":\"root_")
                        .Append(index.ToString(CultureInfo.InvariantCulture)).Append("\"");
                }

                builder.Append("}");
                index++;
            }

            return builder.Append("],\"issuances\":[]}").ToString();
        }

        private static ISet<string> Population()
        {
            return TypeRolePopulation.Resolve(
                LedgerParser.Parse(Ledger()),
                AssemblyEnumerator.Enumerate(Sample),
                new List<ExcludedSignatureRecord>()).RoleTypes;
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
            get { return typeof(TypeRoleRunnerTests).Assembly; }
        }

        private string[] Arguments(string editorDirectory, string table)
        {
            return new[]
            {
                editorDirectory,
                Write("l.md", Ledger()),
                Write("e.json", EmptyExcluded),
                Write("t.json", table),
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
