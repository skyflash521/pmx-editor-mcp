using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class DangerousOperationRunnerTests : IDisposable
    {
        private const string EmptyExcluded = "{\"signatures\":[]}\n";

        private readonly string _root;

        public DangerousOperationRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-danger-" + Guid.NewGuid().ToString("N"));
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
            foreach (int count in new[] { 0, 1, 2, 4 })
            {
                StringWriter error = new StringWriter();

                int code = DangerousOperationRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void MissingTargetAssemblyIsInputUnavailable()
        {
            Assert.Equal(
                ExitCodes.InputUnavailable,
                DangerousOperationRunner.Run(
                    Arguments(Path.Combine(_root, "none"), Ledger(string.Empty)),
                    new StringWriter(),
                    new StringWriter()));
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            string[] args = Arguments(Sdk(), Ledger(string.Empty));
            args[2] = Path.Combine(_root, "none.json");

            Assert.Equal(
                ExitCodes.InputUnavailable,
                DangerousOperationRunner.Run(args, new StringWriter(), new StringWriter()));
        }

        [Fact]
        public void AnUnloadableAssemblyIsInputUnavailable()
        {
            Assert.Equal(
                ExitCodes.InputUnavailable,
                DangerousOperationRunner.Run(
                    Arguments(Broken(), Ledger(string.Empty)), new StringWriter(), new StringWriter()));
        }

        [Fact]
        public void AKindTheRuleDoesNotKnowIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = DangerousOperationRunner.Run(
                Arguments(Sdk(), Ledger("危険操作(知らない種別)。該当は Do()。")),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("知らない種別", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ANotedSignatureThatIsNotThereIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = DangerousOperationRunner.Run(
                Arguments(Sdk(), Ledger("危険操作(エディタ終了)。該当は Absent()。")),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("Absent()", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMatchingLedgerWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = DangerousOperationRunner.Run(
                Arguments(Sdk(), Ledger(string.Empty)), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(
                "照合した: 危険操作に当たるシグネチャ 0 件(エディタ終了 0・上書き保存 0・モデル初期化 0)",
                line);
        }

        [Fact]
        public void ALedgerThatNotesTheDangerousMemberAsDerivedSucceeds()
        {
            StringWriter output = new StringWriter();
            string directory = DangerousSdk();

            int code = DangerousOperationRunner.Run(
                Arguments(directory, Ledger(DangerousAssembly(directory), "危険操作(上書き保存)。該当は SaveAll()。")),
                output,
                new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Equal(
                "照合した: 危険操作に当たるシグネチャ 1 件(エディタ終了 0・上書き保存 1・モデル初期化 0)",
                output.ToString().Trim());
        }

        [Fact]
        public void ADangerousMemberTheLedgerDoesNotNoteIsUnresolved()
        {
            StringWriter error = new StringWriter();
            string directory = DangerousSdk();

            int code = DangerousOperationRunner.Run(
                Arguments(directory, Ledger(DangerousAssembly(directory), string.Empty)),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("台帳が危険操作として記していない", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("SaveAll()", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMemberTheLedgerNotesButTheRuleDoesNotDeriveIsUnresolved()
        {
            StringWriter error = new StringWriter();
            string directory = DangerousSdk();

            int code = DangerousOperationRunner.Run(
                Arguments(
                    directory,
                    Ledger(
                        DangerousAssembly(directory),
                        "危険操作(上書き保存)。該当は SaveAll()。危険操作(エディタ終了)。該当は Touch()。")),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("決め方が危険操作としないものを台帳が記している", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("Touch()", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AKindTheLedgerNotesDifferentlyIsUnresolved()
        {
            StringWriter error = new StringWriter();
            string directory = DangerousSdk();

            int code = DangerousOperationRunner.Run(
                Arguments(directory, Ledger(DangerousAssembly(directory), "危険操作(モデル初期化)。該当は SaveAll()。")),
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("種別が食い違う", error.ToString(), StringComparison.Ordinal);
        }

        /// <summary>題材のアセンブリの公開型を提供として並べ、備考を与えた台帳。</summary>
        private static string Ledger(string remarks)
        {
            return Ledger(Sample, remarks);
        }

        private static string Ledger(Assembly sample, string remarks)
        {
            LedgerJsonBuilder builder = new LedgerJsonBuilder();

            int id = 1;
            foreach (TypeRecord type in AssemblyEnumerator.Enumerate(sample).Types)
            {
                string capability = string.Format(
                    CultureInfo.InvariantCulture, "CAP-{0:D3}", id++);
                builder.Add(
                    capability,
                    "標本",
                    WithoutTypeArguments(type.Name),
                    "提供",
                    "モデル",
                    id == 2 ? remarks : string.Empty);
            }

            return builder.AddNamespaceRows().ToString();
        }

        private static string WithoutTypeArguments(string typeName)
        {
            int open = typeName.IndexOf('<');

            return open < 0 ? typeName : typeName.Substring(0, open);
        }

        private static Assembly Sample
        {
            get { return typeof(DangerousOperationRunnerTests).Assembly; }
        }

        private string[] Arguments(string editorDirectory, string ledger)
        {
            return new[]
            {
                editorDirectory,
                Write("l.md", ledger),
                Write("e.json", EmptyExcluded),
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

        private string DangerousSdk()
        {
            string directory = Path.Combine(_root, "dangerous");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(directory);
            string folder = Path.GetDirectoryName(assemblyPath);
            Directory.CreateDirectory(folder);
            string fileName = Path.GetFileName(assemblyPath);

            AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("DangerousSample" + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.Save,
                folder);
            ModuleBuilder module = assembly.DefineDynamicModule(fileName, fileName);
            TypeBuilder store = module.DefineType(
                "DangerousSample.IStore",
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
            foreach (string name in new[] { "SaveAll", "Touch" })
            {
                store.DefineMethod(
                    name,
                    MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual
                        | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                    typeof(void),
                    Type.EmptyTypes);
            }

            store.CreateType();
            Root(module, "PEPlugin.IPERunArgs", "Version");
            Root(module, "PXCPlugin.IPXCPluginRunArgs", "CPluginVersion");
            Root(module, "PXCPlugin.PXCBridge", "BridgeVersion");
            assembly.Save(fileName);

            return directory;
        }

        private static void Root(ModuleBuilder module, string typeName, string propertyName)
        {
            TypeBuilder root = module.DefineType(
                typeName, TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
            MethodBuilder getter = root.DefineMethod(
                "get_" + propertyName,
                MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual
                    | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName,
                typeof(string),
                Type.EmptyTypes);
            root.DefineProperty(propertyName, PropertyAttributes.None, typeof(string), Type.EmptyTypes)
                .SetGetMethod(getter);
            root.CreateType();
        }

        private static Assembly DangerousAssembly(string directory)
        {
            return Assembly.Load(File.ReadAllBytes(SdkAssemblyLocator.GetAssemblyPath(directory)));
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
