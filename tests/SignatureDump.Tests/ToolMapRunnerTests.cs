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
    public sealed class ToolMapRunnerTests : IDisposable
    {
        private const string EmptyExcluded = "{\"signatures\":[]}\n";

        private const string EmptyMap = "{\"rows\":[]}\n";

        private const string Roles =
            "{\"types\":[{\"typeName\":\"PEPlugin.Pmx.IPXVertex\",\"role\":\"operationTarget\""
                + ",\"basis\":\"題材の根拠。\",\"elementNoun\":\"vertex\""
                + ",\"elementNounPlural\":\"vertices\",\"group\":\"model\""
                + ",\"tools\":{\"list\":\"model_list_vertices\""
                + ",\"update\":\"model_update_vertices\"}}]"
                + ",\"issuances\":[],\"collections\":[]}\n";

        private const string ReleaseTool = "session_release_handle";

        private readonly string _root;

        public ToolMapRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-toolmap-" + Guid.NewGuid().ToString("N"));
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
            foreach (int count in new[] { 0, 1, 2, 3, 4, 5, 7 })
            {
                StringWriter error = new StringWriter();

                int code = ToolMapRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolMapRunner.Run(
                Arguments(Path.Combine(_root, "missing"), Assignments(), EmptyMap),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2, 3, 4, 5 })
            {
                string[] args = Arguments(Sdk(), Assignments(), EmptyMap);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = ToolMapRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolMapRunner.Run(
                Arguments(Sdk(), Assignments(), "{"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AnUnloadableAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolMapRunner.Run(
                Arguments(Broken(), Assignments(), EmptyMap), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ARowOutsideTheProvidedIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = ToolMapRunner.Run(
                Arguments(
                    Sdk(),
                    Assignments(),
                    Map("N.A.Absent()", "commonContract", AssignmentMembers("t"))),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("能力対応表が規則に合わない。", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("N.A.Absent()", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnUpdateKindOutsideTheEnumerationIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = ToolMapRunner.Run(
                Arguments(
                    Sdk(),
                    Assignments(),
                    Map(
                        Releases()[0],
                        "commonContract",
                        AssignmentMembers(ReleaseTool)
                            + ",\"updateSpec\":{\"update\":\"Materiaru\",\"refresh\":[]}",
                        "duplicateEdit")),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("列挙型に無い", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnAssemblyWithoutTheUpdateKindEnumerationIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = ToolMapRunner.Run(
                new[]
                {
                    WithoutUpdateKinds(),
                    Write("l2.md", Ledger(typeof(ToolMapRunner).Assembly)),
                    Write("e2.json", EmptyExcluded),
                    Write("r2.json", Roles),
                    Write("a2.json", "{\"assignments\":[]}"),
                    Write("m2.json", EmptyMap),
                },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("公開API列挙に無い", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMatchingMapWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = ToolMapRunner.Run(
                Arguments(Sdk(), Assignments(), MapOfReleases()), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "照合した: 行 {0} 件(共通契約割当 {0}・イベント 0・スキーマ埋め込み 0"
                        + "・直接ディスパッチ 0)",
                    Releases().Count),
                line,
                StringComparison.Ordinal);
        }

        [Fact]
        public void ASuccessfulRunDoesNotWriteAnyFile()
        {
            string[] args = Arguments(Sdk(), Assignments(), MapOfReleases());
            string[] before = Fingerprints();

            Assert.Equal(
                ExitCodes.Success, ToolMapRunner.Run(args, new StringWriter(), new StringWriter()));
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

        private static string AssignmentMembers(string target)
        {
            return "\"assignment\":\"tool\",\"target\":\"" + target
                + "\",\"slotBinding\":{\"receiver\":\"targetHandle\",\"parameters\":{}}";
        }

        private static string Map(
            string key, string rowKind, string members, string editKind = "read")
        {
            return "{\"rows\":[{\"signatureKey\":\"" + key + "\",\"capabilityIds\":[\""
                + Owner(key) + "\"],\"rowKind\":\"" + rowKind + "\",\"editKind\":\"" + editKind
                + "\",\"direction\":\"write\",\"basis\":\"題材の根拠。\"," + members + "}]}";
        }

        /// <summary>題材の解放・破棄を共通契約割当行として並べた対応表。</summary>
        private static string MapOfReleases()
        {
            StringBuilder builder = new StringBuilder("{\"rows\":[");
            int index = 0;
            foreach (string key in Releases())
            {
                builder.Append(index++ == 0 ? string.Empty : ",")
                    .Append("{\"signatureKey\":\"").Append(key)
                    .Append("\",\"capabilityIds\":[\"").Append(Owner(key))
                    .Append("\"],\"rowKind\":\"commonContract\",\"editKind\":\"directChange\"")
                    .Append(",\"direction\":\"write\",\"basis\":\"題材の根拠。\",")
                    .Append(AssignmentMembers(ReleaseTool)).Append("}");
            }

            return builder.Append("]}").ToString();
        }

        /// <summary>題材の解放・破棄を、解放のツールへの束縛として並べた共通契約割当の正本。</summary>
        private static string Assignments()
        {
            StringBuilder builder = new StringBuilder("{\"assignments\":[");
            int index = 0;
            foreach (string key in Releases())
            {
                builder.Append(index++ == 0 ? string.Empty : ",")
                    .Append("{\"signatureKey\":\"").Append(key)
                    .Append("\",\"assignment\":\"tool\",\"target\":\"").Append(ReleaseTool)
                    .Append("\",\"slotBinding\":{\"receiver\":\"targetHandle\"")
                    .Append(",\"parameters\":{}},\"basis\":\"題材の根拠。\"}");
            }

            return builder.Append("]}").ToString();
        }

        /// <summary>題材の提供対象の解放・破棄の行キー。序数の昇順に並べる。</summary>
        private static IList<string> Releases()
        {
            InventoryRecord inventory = AssemblyEnumerator.Enumerate(Sample);
            ISet<string> provided = TypeRolePopulation.Resolve(
                LedgerParser.Parse(Ledger()),
                inventory,
                new List<ExcludedSignatureRecord>()).Signatures;

            return CommonAssignmentEvidence.ReleaseSignatures(inventory, provided)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>その行キーを指す題材の能力のID。台帳は型ごとに1行を持つ。</summary>
        private static string Owner(string key)
        {
            ISet<string> owners;
            LedgerPopulation.Resolve(
                LedgerParser.Parse(Ledger()), AssemblyEnumerator.Enumerate(Sample))
                .Owners.TryGetValue(key, out owners);

            return owners == null ? "CAP-999" : owners.OrderBy(o => o, StringComparer.Ordinal).First();
        }

        /// <summary>題材のアセンブリの公開型を提供として並べ、まとめて指す行を足した台帳。</summary>
        private static string Ledger(Assembly assembly = null)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n");
            builder.Append("|---|---|---|---|---|---|\n");

            int id = 1;
            foreach (TypeRecord type in AssemblyEnumerator.Enumerate(assembly ?? Sample).Types)
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
            get { return typeof(ToolMapRunnerTests).Assembly; }
        }

        private string[] Arguments(string editorDirectory, string assignments, string map)
        {
            return new[]
            {
                editorDirectory,
                Write("l.md", Ledger()),
                Write("e.json", EmptyExcluded),
                Write("r.json", Roles),
                Write("a.json", assignments),
                Write("m.json", map),
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

        /// <summary>
        /// 反映の指定の列挙型を持たないアセンブリを対象アセンブリとして置いた導入ディレクトリを作る。
        /// </summary>
        private string WithoutUpdateKinds()
        {
            string directory = Path.Combine(_root, "no-update-kinds");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(
                    new Uri(typeof(ToolMapRunner).Assembly.CodeBase).LocalPath, assemblyPath);
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
