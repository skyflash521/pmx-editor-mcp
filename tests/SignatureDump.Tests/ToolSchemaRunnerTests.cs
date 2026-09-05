using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ToolSchemaRunnerTests : IDisposable
    {
        private const string Tool = "model_list_vertices";

        private const string Spellings =
            "### 表現の綴り\n\n| 綴り | JSONの形 |\n|---|---|\n| `number` | 数値 |\n";

        private const string Contract = Spellings
            + "\n#### 想定文字数\n\n| 綴り | 想定文字数 |\n|---|---|\n| `number` | 11 |\n";

        private const string Architecture =
            "## 応答サイズ予算の設定\n\n- 未設定時の既定は **100,000**——題材。\n";

        private const string EmptyMap = "{\"rows\":[]}\n";

        private const string EmptySchemas = "{\"tools\":[]}\n";

        private readonly string _root;

        public ToolSchemaRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-schemas-" + Guid.NewGuid().ToString("N"));
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

                int code = ToolSchemaRunner.Run(
                    Enumerable.Repeat("a", count).ToArray(), new StringWriter(), error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
            }
        }

        [Fact]
        public void AMissingInputFileIsInputUnavailable()
        {
            foreach (int missing in new[] { 0, 1, 2, 3 })
            {
                string[] args = Arguments(EmptyMap, EmptySchemas);
                args[missing] = Path.Combine(_root, "gone");
                StringWriter error = new StringWriter();

                int code = ToolSchemaRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("gone", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void UnreadableInputIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolSchemaRunner.Run(
                Arguments(EmptyMap, "{"), new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()));
        }

        [Fact]
        public void ADocumentWithoutTheSpellingSectionIsInputUnavailable()
        {
            StringWriter error = new StringWriter();

            int code = ToolSchemaRunner.Run(
                new[]
                {
                    Write("c2.md", "## 値の表現\n"),
                    Write("a2.md", Architecture),
                    Write("m2.json", EmptyMap),
                    Write("s2.json", EmptySchemas),
                },
                new StringWriter(),
                error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("節が無い", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ADocumentWithoutTheAssumedLengthSectionIsInputUnavailable()
        {
            string[] args = Arguments(EmptyMap, EmptySchemas);
            args[0] = Write("c3.md", Spellings);
            StringWriter error = new StringWriter();

            int code = ToolSchemaRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("想定文字数", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ADocumentWithoutTheBudgetDefaultIsInputUnavailable()
        {
            string[] args = Arguments(EmptyMap, EmptySchemas);
            args[1] = Write("a3.md", "## 応答サイズ予算の設定\n\n- 既定は無い。\n");
            StringWriter error = new StringWriter();

            int code = ToolSchemaRunner.Run(args, new StringWriter(), error);

            Assert.Equal(ExitCodes.InputUnavailable, code);
            Assert.Contains("既定の予算が読めない", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AToolWithoutAnAssignedRowIsUnresolved()
        {
            StringWriter error = new StringWriter();

            int code = ToolSchemaRunner.Run(
                Arguments(EmptyMap, Schemas()), new StringWriter(), error);

            Assert.Equal(ExitCodes.Unresolved, code);
            Assert.Contains("スキーマ正本が規則に合わない。", error.ToString(), StringComparison.Ordinal);
            Assert.Contains(Tool, error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void AMatchingTableWritesOneSummaryLineAndSucceeds()
        {
            StringWriter output = new StringWriter();

            int code = ToolSchemaRunner.Run(
                Arguments(Map(), Schemas()), output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            string line = Assert.Single(
                output.ToString().Split(
                    new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(
                "照合した: ツール 1 件(呼び分け 1・項目 1・イベントの分岐 0)"
                    + "・綴り 1 種・予算 100000 文字",
                line);
        }

        [Fact]
        public void ASuccessfulRunDoesNotWriteAnyFile()
        {
            string[] args = Arguments(Map(), Schemas());
            string[] before = Fingerprints();

            Assert.Equal(
                ExitCodes.Success,
                ToolSchemaRunner.Run(args, new StringWriter(), new StringWriter()));
            Assert.Equal(before, Fingerprints());
        }

        /// <summary>
        /// 試験用ディレクトリの全ファイルを、名前と中身と更新時刻の指紋の並びにしたもの。入力だけを
        /// 見ると、隣へ書き出す実装を見逃す。
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
                        File.GetLastWriteTimeUtc(p).Ticks.ToString()))
                    .ToArray();
            }
        }

        private static string Map()
        {
            return "{\"rows\":[{\"signatureKey\":\"T.M()\",\"capabilityIds\":[\"CAP-001\"]"
                + ",\"rowKind\":\"directDispatch\",\"editKind\":\"read\",\"direction\":\"read\""
                + ",\"basis\":\"題材の根拠。\",\"tool\":\"" + Tool + "\""
                + ",\"postcondition\":[{\"effectType\":\"none\",\"effectKey\":\"\""
                + ",\"kind\":\"callLogOnly\",\"comparison\":\"exists\"}]}]}";
        }

        private static string Schemas()
        {
            return "{\"tools\":[{\"tool\":\"" + Tool + "\""
                + ",\"branches\":[{\"branch\":\"only\",\"inputs\":[]}]"
                + ",\"output\":{\"origin\":\"hostOutput\",\"shape\":\"number\"}}]}";
        }

        private string[] Arguments(string map, string schemas)
        {
            return new[]
            {
                Write("c.md", Contract),
                Write("a.md", Architecture),
                Write("m.json", map),
                Write("s.json", schemas),
            };
        }

        private string Write(string name, string text)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllText(path, text);

            return path;
        }
    }
}
