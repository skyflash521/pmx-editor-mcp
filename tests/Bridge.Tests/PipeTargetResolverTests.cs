using System;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class PipeTargetResolverTests
    {
        /// <summary>
        /// 環境変数名が本文に出るだけでは、それで指定できるという案内になっているとは限らない
        /// (できない理由として名前を挙げる本文でも通ってしまう)。指定の手段までを一続きの
        /// 語句として固定する。
        /// </summary>
        private const string PipeNameGuidance = "環境変数 PMX_EDITOR_MCP_PIPE で接続先のパイプ名を指定する";

        /// <summary>
        /// 明示指定の寿命を伝えているかを見るための語。パイプ名にはエディタのプロセスIDが入るので、
        /// 恒久的な設定へ書き写すと、エディタを起動し直した時点で死んだ名前を指す。寿命と
        /// 指定し直しの必要を一続きの語句として固定する。
        /// </summary>
        private const string PipeNameLifetime = "この指定は今動いているエディタ限りで、エディタを起動し直したら指定し直す";

        [Fact]
        public void 接続先の指定と発見に用いる名前は契約で定めた値である()
        {
            Assert.Equal("PMX_EDITOR_MCP_PIPE", PipeTargetResolver.EnvironmentVariableName);
            Assert.Equal("PmxEditor_x64", PipeTargetResolver.EditorProcessName);
        }

        [Fact]
        public void パイプ名はエディタのプロセスIDで決まる()
        {
            Assert.Equal("pmx-editor-mcp-1234", PipeTargetResolver.PipeNameForProcess(1234));
        }

        [Fact]
        public void 明示指定があればエディタを数えずにその名前を使う()
        {
            string resolved = PipeTargetResolver.Resolve("pmx-editor-mcp-9", new int[] { 1234, 5678 });

            Assert.Equal("pmx-editor-mcp-9", resolved);
        }

        [Fact]
        public void 自動発見へ落ちるのは明示指定が無いときだけとする()
        {
            // 空文字列は「指定が無い」ではなく「空の名前を指定した」として扱い、黙って
            // 自動発見へ落とさない(設定の誤りを隠さないため)。
            string resolved = PipeTargetResolver.Resolve(string.Empty, new int[] { 1234 });

            Assert.Equal(string.Empty, resolved);
        }

        [Fact]
        public void 明示指定が無くエディタが1つならそのプロセスIDで決める()
        {
            string resolved = PipeTargetResolver.Resolve(null, new int[] { 1234 });

            Assert.Equal("pmx-editor-mcp-1234", resolved);
        }

        [Fact]
        public void エディタが起動していなければ起動を促すエラーにする()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(null, new int[0]));

            Assert.Equal(BridgeErrorCodes.NoEditor, error.Code);
            Assert.Contains("PMXエディタ", error.Message);
            Assert.Contains("起動", error.Message);
        }

        [Fact]
        public void エディタが複数なら明示指定の手段を案内するエラーにする()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(null, new int[] { 5678, 1234 }));

            Assert.Equal(BridgeErrorCodes.MultipleEditors, error.Code);
            Assert.Contains(PipeNameGuidance, error.Message);
        }

        [Fact(Skip = "impl pending: 明示指定が今動いているエディタ限りであることを案内へ含める")]
        public void 複数起動の案内は指定が今のエディタ限りであることを伝える()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(null, new int[] { 5678, 1234 }));

            Assert.Contains(PipeNameLifetime, error.Message);
        }

        [Fact]
        public void 複数起動のエラーは候補のパイプ名を1つずつ列挙する()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(null, new int[] { 30, 10, 20 }));

            string[] candidates = Array.FindAll(
                error.Message.Split('\n'), line => line.StartsWith("pmx-editor-mcp-"));

            Assert.Equal(
                new string[] { "pmx-editor-mcp-10", "pmx-editor-mcp-20", "pmx-editor-mcp-30" },
                candidates);
        }
    }
}
