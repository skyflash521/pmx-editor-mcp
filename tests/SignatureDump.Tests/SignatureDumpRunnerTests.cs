using System;
using System.IO;
using System.Linq;
using System.Text;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SignatureDumpRunnerTests : IDisposable
    {
        private readonly string _root;

        public SignatureDumpRunnerTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-signature-dump-" + Guid.NewGuid().ToString("N"));
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
                // 取り除けなくてもテストの結果は変わらない。一時ディレクトリなので放置してよい。
            }
        }

        // 導入ディレクトリの形をまねた一時ディレクトリを作り、対象アセンブリの位置へこの
        // テストアセンブリ自身を置く。実物のSDKを持ち込まずに、入口から書き出しまでを通せる。
        private string CreateEditorDirectory()
        {
            string editorDirectory = Path.Combine(_root, "editor");
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath));
            File.Copy(new Uri(typeof(ISampleApi).Assembly.CodeBase).LocalPath, assemblyPath);
            return editorDirectory;
        }

        [Fact]
        public void 列挙結果を書き出して成功で終わる()
        {
            string editorDirectory = CreateEditorDirectory();
            string outputPath = Path.Combine(_root, "signatures.json");
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();

            int code = SignatureDumpRunner.Run(new[] { editorDirectory, outputPath }, output, error);

            Assert.Equal(SignatureDumpRunner.ExitSuccess, code);
            Assert.NotEqual(string.Empty, output.ToString());
            Assert.Equal(string.Empty, error.ToString());
            Assert.True(File.Exists(outputPath));
            string json = File.ReadAllText(outputPath);
            Assert.Contains("\"key\":\"PmxEditorMcp.SignatureDump.Tests.Sample.ISampleApi.GetCount()\"", json);
        }

        [Fact]
        public void 対象アセンブリを掴んだままにしない()
        {
            string editorDirectory = CreateEditorDirectory();
            string outputPath = Path.Combine(_root, "signatures.json");

            SignatureDumpRunner.Run(new[] { editorDirectory, outputPath }, new StringWriter(), new StringWriter());

            // 対象アセンブリをパスから読み込むと掴んだままになり、呼び出し元は消せなくなる。
            // 一時ディレクトリへ複製して渡す呼び出し元では、実行のたびに複製が溜まっていく。
            // 依存アセンブリは導入ディレクトリの実体を指すので、この題材では読み込まれない。
            Directory.Delete(editorDirectory, true);
            Assert.False(Directory.Exists(editorDirectory));
        }

        [Fact]
        public void 同じ入力からは同じバイト列が書き出される()
        {
            string editorDirectory = CreateEditorDirectory();
            string first = Path.Combine(_root, "first.json");
            string second = Path.Combine(_root, "second.json");

            SignatureDumpRunner.Run(new[] { editorDirectory, first }, new StringWriter(), new StringWriter());
            SignatureDumpRunner.Run(new[] { editorDirectory, second }, new StringWriter(), new StringWriter());

            // 復号後の文字列で比べると、符号化やBOMの有無が違っても同じに見える。読み手はファイルを
            // そのまま読むので、バイト列で比べる。
            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }

        [Fact]
        public void 書き出しはBOMなしUTF8になる()
        {
            string editorDirectory = CreateEditorDirectory();
            string outputPath = Path.Combine(_root, "signatures.json");

            SignatureDumpRunner.Run(new[] { editorDirectory, outputPath }, new StringWriter(), new StringWriter());

            byte[] bytes = File.ReadAllBytes(outputPath);
            string text = new UTF8Encoding(false, true).GetString(bytes);

            Assert.Equal((byte)'{', bytes[0]);
            Assert.Equal(new UTF8Encoding(false).GetBytes(text), bytes);
            Assert.EndsWith("\n", text);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public void 引数の数が違うと不正な引数で終わる(int count)
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            string[] args = Enumerable.Repeat(_root, count).ToArray();

            int code = SignatureDumpRunner.Run(args, output, error);

            Assert.Equal(SignatureDumpRunner.ExitInvalidArguments, code);
            Assert.NotEqual(string.Empty, error.ToString());
            Assert.Equal(string.Empty, output.ToString());
        }

        [Fact]
        public void 対象アセンブリが無いと読み込み失敗で終わる()
        {
            string editorDirectory = Path.Combine(_root, "empty");
            Directory.CreateDirectory(editorDirectory);
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            string outputPath = Path.Combine(_root, "signatures.json");

            int code = SignatureDumpRunner.Run(new[] { editorDirectory, outputPath }, output, error);

            Assert.Equal(SignatureDumpRunner.ExitAssemblyUnavailable, code);
            Assert.Contains(SdkAssemblyLocator.GetAssemblyPath(editorDirectory), error.ToString());
            Assert.Equal(string.Empty, output.ToString());
            Assert.False(File.Exists(outputPath));
        }

        [Fact]
        public void 書き出せないと書き出し失敗で終わる()
        {
            string editorDirectory = CreateEditorDirectory();
            string outputPath = Path.Combine(_root, "missing", "signatures.json");
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();

            int code = SignatureDumpRunner.Run(new[] { editorDirectory, outputPath }, output, error);

            Assert.Equal(SignatureDumpRunner.ExitWriteFailed, code);
            Assert.NotEqual(string.Empty, error.ToString());
            Assert.Equal(string.Empty, output.ToString());
            Assert.False(File.Exists(outputPath));
        }

        [Fact]
        public void 引数を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(
                () => SignatureDumpRunner.Run(null, new StringWriter(), new StringWriter()));
            Assert.Throws<ArgumentNullException>(
                () => SignatureDumpRunner.Run(new string[0], null, new StringWriter()));
            Assert.Throws<ArgumentNullException>(
                () => SignatureDumpRunner.Run(new string[0], new StringWriter(), null));
        }
    }
}
