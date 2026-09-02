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
    public sealed class PropertyNameRunnerTests : IDisposable
    {
        private const string EmptyDocument = "<doc><members /></doc>";

        private const string EmptyNames = "{\"propertyNames\":[]}\n";

        private const string EmptyExcluded = "{\"signatures\":[]}\n";

        private const string NoteName = "大きさ";

        private const string Authored =
            ",\"basis\":{\"kind\":\"memberShape\"},\"origin\":\"メンバー名から起こした。\"";

        private readonly string _root;

        public PropertyNameRunnerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-names-" + Guid.NewGuid().ToString("N"));
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

                int code = PropertyNameRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = PropertyNameRunner.Run(
                Arguments(Path.Combine(_root, "missing"), EmptyNames), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.dll", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 1, 2, 3 })
            {
                string[] args = Arguments(Sdk(), EmptyNames);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = PropertyNameRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AMissingDocumentIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = PropertyNameRunner.Run(
                Arguments(Broken(false), EmptyNames), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("PEPlugin.XML", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = PropertyNameRunner.Run(Arguments(Broken(), "{"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void AnUnloadableAssemblyIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = PropertyNameRunner.Run(
                Arguments(Broken(), EmptyNames), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ALedgerThatDoesNotResolveIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = PropertyNameRunner.Run(
                new[]
                {
                    Sdk(),
                    Write("empty.md", "| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n|---|---|---|---|---|---|\n"),
                    Write("e.json", EmptyExcluded),
                    Write("n.json", EmptyNames),
                },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("母集合を決められない。", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ATableThatMissesAPropertyIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = PropertyNameRunner.Run(
                Arguments(Sdk(), EmptyNames), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("日本語名が規則に合わない。", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("表に無い項目が在る", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMatchingTableWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = PropertyNameRunner.Run(
                Arguments(Sdk(), Names()), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Equal(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "照合した: 項目 {0} 件(記載を採る 0・名前を起こす {0})",
                    Expected().Count),
                output.ToString().Trim());
        }

        [Fact]
        public void APropertyWithANoteIsCountedAsQuoted()
        {
            PropertyRecord noted = Expected()[0];
            StringWriter output = new StringWriter();

            int code = PropertyNameRunner.Run(
                Arguments(Sdk(Document(noted)), Names(noted)), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("記載を採る 1・", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void APropertyWithANoteThatIsAuthoredIsUnresolved()
        {
            PropertyRecord noted = Expected()[0];
            StringWriter error = new StringWriter();

            int code = PropertyNameRunner.Run(
                Arguments(Sdk(Document(noted)), Names()), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("決め方が記載の出現数と合わない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ASuccessfulRunDoesNotModifyInputs()
        {
            string[] args = Arguments(Sdk(), Names());
            string[] before = Fingerprints();

            Assert.Equal(
                ExitCodes.Success,
                PropertyNameRunner.Run(args, new StringWriter(), new StringWriter()));
            Assert.Equal(before, Fingerprints());
        }

        [Fact]
        public void NullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => PropertyNameRunner.Run(null, new StringWriter(), new StringWriter()));
            Assert.Throws<ArgumentNullException>(
                () => PropertyNameRunner.Run(new string[0], null, new StringWriter()));
            Assert.Throws<ArgumentNullException>(
                () => PropertyNameRunner.Run(new string[0], new StringWriter(), null));
        }

        private string[] Arguments(string editorDirectory, string names)
        {
            return new[]
            {
                editorDirectory,
                Write("l.md", Ledger()),
                Write("e.json", EmptyExcluded),
                Write("n.json", names),
            };
        }

        /// <summary>題材のアセンブリを対象アセンブリとして置いた導入ディレクトリを作る。</summary>
        private string Sdk(string document = EmptyDocument)
        {
            string directory = Path.Combine(
                _root, "sdk" + document.Length.ToString(CultureInfo.InvariantCulture));
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            if (!File.Exists(assemblyPath))
            {
                File.Copy(new Uri(Sample.CodeBase).LocalPath, assemblyPath);
            }

            File.WriteAllText(SdkAssemblyLocator.GetDocumentPath(directory), document);

            return directory;
        }

        /// <summary>読み込めないアセンブリを置いた導入ディレクトリを作る。</summary>
        private string Broken(bool withDocument = true)
        {
            string directory = Path.Combine(_root, "broken" + (withDocument ? "-doc" : string.Empty));
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            File.WriteAllBytes(assemblyPath, new byte[] { 0x4D, 0x5A });
            if (withDocument)
            {
                File.WriteAllText(SdkAssemblyLocator.GetDocumentPath(directory), EmptyDocument);
            }

            return directory;
        }

        /// <summary>1件の member にだけ記載を持つドキュメントXML。</summary>
        private static string Document(PropertyRecord property)
        {
            return "<doc><members><member name=\"P:"
                + DocumentNoteReader.MemberName(property.DeclaringType, property.MemberName)
                + "\"><summary>" + NoteName + " get/set</summary></member></members></doc>";
        }

        /// <summary>題材のアセンブリの公開型を提供として並べ、まとめて指す行を足した台帳。</summary>
        private static string Ledger()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |\n");
            builder.Append("|---|---|---|---|---|---|\n");

            int id = 1;
            foreach (TypeRecord type in Inventory().Types)
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

        /// <summary>
        /// 母集合の全項目へ、同じ型の中で重ならない名前を付けた正本。<paramref name="quoted"/> を
        /// 渡した項目だけが記載を採る側になる。
        /// </summary>
        private static string Names(PropertyRecord quoted = null)
        {
            StringBuilder builder = new StringBuilder("{\"propertyNames\":[");
            string separator = string.Empty;
            foreach (PropertyRecord property in Expected())
            {
                bool takesTheNote = quoted != null
                    && string.Equals(property.Key, quoted.Key, StringComparison.Ordinal);
                builder.Append(separator).Append(string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"declaringType\":\"{0}\",\"memberName\":\"{1}\",\"propertyType\":\"{2}\","
                        + "\"japaneseName\":\"{3}\",\"decision\":\"{4}\"{5}}}",
                    property.DeclaringType,
                    property.MemberName,
                    property.PropertyType,
                    takesTheNote ? NoteName : property.MemberName,
                    takesTheNote ? "quoted" : "authored",
                    takesTheNote ? string.Empty : Authored));
                separator = ",";
            }

            return builder.Append("]}").ToString();
        }

        private static IList<PropertyRecord> Expected()
        {
            TypeRolePopulation population = TypeRolePopulation.Resolve(
                LedgerParser.Parse(Ledger()),
                Inventory(),
                new List<ExcludedSignatureRecord>());

            return RoleTypeProperties.Enumerate(population.RoleTypes, Candidates());
        }

        private static IEnumerable<Type> Candidates()
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(Visible).ToList();
        }

        private static IEnumerable<Type> Visible(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes().Where(t => t.IsVisible);
            }
            catch (ReflectionTypeLoadException)
            {
                return new Type[0];
            }
        }

        private static InventoryRecord Inventory()
        {
            return AssemblyEnumerator.Enumerate(Sample);
        }

        private static Assembly Sample
        {
            get { return typeof(PropertyNameRunnerTests).Assembly; }
        }

        private static string WithoutTypeArguments(string typeName)
        {
            int open = typeName.IndexOf('<');

            return open < 0 ? typeName : typeName.Substring(0, open);
        }

        /// <summary>
        /// 試験用ディレクトリの全ファイルを、名前と中身と更新時刻の指紋の並びにしたもの。更新時刻
        /// まで見るのは、同じ中身で書き直す実装を中身の比較だけでは見分けられないためである。
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

        private string Write(string name, string text)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllText(path, text);

            return path;
        }
    }
}
