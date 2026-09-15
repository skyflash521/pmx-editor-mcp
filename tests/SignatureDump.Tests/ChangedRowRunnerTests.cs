using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ChangedRowRunnerTests : IDisposable
    {
        private const string Command = "changed-rows";

        private const string Usage =
            Command + " <前の能力対応表の正本のパス> <いまの能力対応表の正本のパス>";

        private readonly string _root;

        public ChangedRowRunnerTests()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "pmx-editor-mcp-changed-" + Guid.NewGuid().ToString("N"));
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

        /// <summary>どの行も持つ項目だけの行。持つとは限らない項目は呼ぶ側が足す。</summary>
        private static string Row(string key, string editKind, string basis, string members)
        {
            return @"{ ""signatureKey"": """ + key + @""", ""editKind"": """ + editKind + @""",
                       ""basis"": """ + basis + @"""" + members + "}";
        }

        private static string Row(string key, string editKind, string basis)
        {
            return Row(key, editKind, basis, string.Empty);
        }

        private static string Row(string key)
        {
            return Row(key, "read", "根拠。");
        }

        /// <summary>持つとは限らない項目を1つ持つ行。中の値だけを違えられる。</summary>
        private static string WithPostcondition(string key, string effectType)
        {
            return Row(
                key,
                "read",
                "根拠。",
                @", ""postcondition"": [{ ""effectType"": """ + effectType + @""",
                     ""effectKey"": """", ""kind"": ""callLogOnly"", ""comparison"": ""exists"" }]");
        }

        private static string Map(params string[] rows)
        {
            return "{ \"rows\": [" + string.Join(",", rows) + "] }";
        }

        /// <summary>2つの版を一時の置き場へ書いて、そのパスを渡す形にする。</summary>
        private string[] Paths(string before, string after)
        {
            string baseline = Path.Combine(_root, "before.json");
            string current = Path.Combine(_root, "after.json");
            File.WriteAllText(baseline, before);
            File.WriteAllText(current, after);

            return new[] { baseline, current };
        }

        /// <summary>2つの版を突き合わせ、書き出した行を返す。</summary>
        private string[] Selected(string before, string after)
        {
            StringWriter output = new StringWriter();

            int code = CommandRunner.Run(
                new[] { Command }.Concat(Paths(before, after)).ToArray(),
                output,
                new StringWriter());

            Assert.Equal(ExitCodes.Success, code);

            return output.ToString()
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>2つの版を突き合わせたときの終了コードを返す。</summary>
        private int Ends(string before, string after)
        {
            return CommandRunner.Run(
                new[] { Command }.Concat(Paths(before, after)).ToArray(),
                new StringWriter(),
                new StringWriter());
        }

        [Fact(Skip = "impl pending: 能力対応表の2つの版から、中身の変わった行のキーを1行ずつ書き出す")]
        public void WritesTheKeysOfEveryRowWhoseContentChanged()
        {
            string[] selected = Selected(
                Map(Row("T.M()"), Row("T.N()"), Row("T.P()"), WithPostcondition("T.Q()", "none")),
                Map(
                    Row("T.M()", "directChange", "根拠。"),
                    Row("T.N()"),
                    Row("T.P()", "read", "別の根拠。"),
                    WithPostcondition("T.Q()", "stateWritten")));

            Assert.Equal(new[] { "T.M()", "T.P()", "T.Q()" }, selected);
        }

        [Fact(Skip = "impl pending: 能力対応表の2つの版から、足された行のキーを書き出す")]
        public void WritesTheKeyOfARowThatWasAdded()
        {
            string[] selected = Selected(
                Map(Row("T.M()")),
                Map(Row("T.M()"), Row("T.N()")));

            Assert.Equal(new[] { "T.N()" }, selected);
        }

        [Fact(Skip = "impl pending: 消えた行のキーは書き出さない")]
        public void LeavesOutARowThatWasRemoved()
        {
            string[] selected = Selected(
                Map(Row("T.M()"), Row("T.N()")),
                Map(Row("T.M()")));

            Assert.Empty(selected);
        }

        [Fact(Skip = "impl pending: 中身の変わっていない行のキーは書き出さない")]
        public void LeavesOutRowsThatDidNotChange()
        {
            string map = Map(Row("T.M()"), Row("T.N()"));

            Assert.Empty(Selected(map, map));
        }

        [Fact(Skip = "impl pending: 行キーの昇順で並んでいない入力を、読めないものとして断る")]
        public void RowsOutOfOrderEndWithInputUnavailable()
        {
            string ordered = Map(Row("T.M()"), Row("T.N()"));
            string reversed = Map(Row("T.N()"), Row("T.M()"));

            Assert.Equal(ExitCodes.InputUnavailable, Ends(reversed, ordered));
            Assert.Equal(ExitCodes.InputUnavailable, Ends(ordered, reversed));
        }

        [Fact(Skip = "impl pending: 同じ行キーが二度現れる入力を、読めないものとして断る")]
        public void ARepeatedKeyEndsWithInputUnavailable()
        {
            string once = Map(Row("T.M()"));
            string twice = Map(Row("T.M()"), Row("T.M()"));

            Assert.Equal(ExitCodes.InputUnavailable, Ends(twice, once));
            Assert.Equal(ExitCodes.InputUnavailable, Ends(once, twice));
        }

        [Fact(Skip = "impl pending: 引数の数が合わない呼び出しを、使い方を示して断る")]
        public void WrongArgumentCountEndsWithInvalidArguments()
        {
            foreach (int count in new[] { 0, 1, 3 })
            {
                StringWriter error = new StringWriter();

                int code = CommandRunner.Run(
                    new[] { Command }.Concat(Enumerable.Repeat("a", count)).ToArray(),
                    new StringWriter(),
                    error);

                Assert.Equal(ExitCodes.InvalidArguments, code);
                Assert.Contains(Usage, error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact(Skip = "impl pending: どちらの版が無くても、読めないものとして断る")]
        public void AMissingInputEndsWithInputUnavailable()
        {
            string map = Map(Row("T.M()"));
            string[] paths = Paths(map, map);
            foreach (int missing in new[] { 0, 1 })
            {
                string[] args = new[] { Command }.Concat(paths).ToArray();
                args[missing + 1] = Path.Combine(_root, "無い.json");
                StringWriter error = new StringWriter();

                int code = CommandRunner.Run(args, new StringWriter(), error);

                Assert.Equal(ExitCodes.InputUnavailable, code);
                Assert.Contains("無い.json", error.ToString(), StringComparison.Ordinal);
            }
        }

        [Fact(Skip = "impl pending: どちらの版が行の並びを持たなくても、読めないものとして断る")]
        public void AnInputWithoutRowsEndsWithInputUnavailable()
        {
            string map = Map(Row("T.M()"));

            Assert.Equal(ExitCodes.InputUnavailable, Ends("{}", map));
            Assert.Equal(ExitCodes.InputUnavailable, Ends(map, "{}"));
        }
    }
}
