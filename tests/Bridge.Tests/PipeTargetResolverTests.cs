using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class PipeTargetResolverTests
    {
        private const string MultipleHostsMessage =
            "ホストが 3 つ待ち受けているため接続先を1つに決められない。どのエディタを対象に"
                + "するかを利用者に確かめる。待ち受けているホスト:\n"
                + "pmx-editor-mcp-2\npmx-editor-mcp-10\npmx-editor-mcp-30";

        /// <summary>
        /// ホストの待受パイプと紛らわしいが候補にしてはならない名前。ホストは自分のプロセスIDを
        /// 十進で書くだけなので、符号・空白・桁区切り・先頭の0・ASCII以外の数字はホストが作る
        /// 名前には現れない。プロセスIDに0は割り当てられない。
        /// </summary>
        public static TheoryData<string> NamesThatAreNotHostListeners => new TheoryData<string>
        {
            "pmx-editor-mcp-",
            "pmx-editor-mcp-abc",
            "pmx-editor-mcp--1",
            "pmx-editor-mcp-+1",
            "pmx-editor-mcp- 1",
            "pmx-editor-mcp-12x",
            "pmx-editor-mcp-1.2",
            "pmx-editor-mcp-1,234",
            "pmx-editor-mcp-0",
            "pmx-editor-mcp-01",
            "pmx-editor-mcp-2147483648",
            "pmx-editor-mcp-99999999999999999999",
            "pmx-editor-mcp-١٢٣",
            "PMX-EDITOR-MCP-12",
            "mcp-pmx-editor-mcp-12",
            "lsass",
        };

        [Fact]
        public void ListeningDiscoveryNameMatchesContract()
        {
            Assert.Equal("PMX_EDITOR_MCP_TEST_PIPE", PipeTargetResolver.TestPipeEnvironmentVariableName);
            Assert.Equal("PmxEditor_x64", PipeTargetResolver.EditorProcessName);
            Assert.Equal("pmx-editor-mcp-", PipeTargetResolver.PipeNamePrefix);
            Assert.Equal(@"\\.\pipe\", PipeTargetResolver.PipeDirectory);
        }

        [Fact]
        public void PipeNameIsDerivedFromEditorProcessId()
        {
            Assert.Equal("pmx-editor-mcp-1234", PipeTargetResolver.PipeNameForProcess(1234));
        }

        [Fact]
        public void ExplicitTargetIsUsedWithoutEnumeratingListeners()
        {
            string resolved = PipeTargetResolver.Resolve(
                "pmx-editor-mcp-9", Entries("pmx-editor-mcp-1234", "pmx-editor-mcp-5678"), new int[] { 1234, 5678 });

            Assert.Equal("pmx-editor-mcp-9", resolved);
        }

        [Fact]
        public void ListenerEnumerationHappensOnlyWithoutExplicitTarget()
        {
            // 空文字列は「指定が無い」ではなく「空の名前を指定した」として扱い、黙って
            // 自動発見へ落とさない(設定の誤りを隠さないため)。
            string resolved = PipeTargetResolver.Resolve(
                string.Empty, Entries("pmx-editor-mcp-1234"), new int[] { 1234 });

            Assert.Equal(string.Empty, resolved);
        }

        [Fact]
        public void SingleListeningHostBecomesTarget()
        {
            string resolved = PipeTargetResolver.Resolve(
                null, Entries("pmx-editor-mcp-1234"), new int[] { 1234 });

            Assert.Equal("pmx-editor-mcp-1234", resolved);
        }

        [Fact]
        public void TargetIsReturnedAsPipeNameNotDirectoryEntry()
        {
            // 列挙で得られる項目はディレクトリを含む形なので、そのまま返すと接続に使えない。
            string resolved = PipeTargetResolver.Resolve(
                null, Entries("pmx-editor-mcp-1234"), new int[] { 1234 });

            Assert.DoesNotContain(PipeTargetResolver.PipeDirectory, resolved);
        }

        [Theory]
        [MemberData(nameof(NamesThatAreNotHostListeners))]
        public void OnlyHostListenersAreCandidatesAmongOtherEntries(string notHostPipeName)
        {
            // パイプディレクトリには無関係な名前が多数並ぶ。落とさなければ、ホストが1つしか
            // 待ち受けていなくても複数と数えてしまう。接頭辞を見るだけの絞り込みでは、続きが
            // プロセスIDになっていない紛らわしい名前を落とせない。
            string resolved = PipeTargetResolver.Resolve(
                null, Entries(notHostPipeName, "pmx-editor-mcp-1234"), new int[] { 1234 });

            Assert.Equal("pmx-editor-mcp-1234", resolved);
        }

        [Fact]
        public void TargetIsResolvedWithMultipleEditorsButOneListener()
        {
            // 数えるべきは接続できる相手であって、エディタの数ではない。プラグインを配置して
            // いないエディタや、ホストを停止したエディタは接続先の候補にならない。
            string resolved = PipeTargetResolver.Resolve(
                null, Entries("pmx-editor-mcp-5678"), new int[] { 1234, 5678, 9012 });

            Assert.Equal("pmx-editor-mcp-5678", resolved);
        }

        [Fact]
        public void NoEditorAndNoListenerYieldsStartupPromptError()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(null, Entries("lsass"), new int[0]));

            Assert.Equal(BridgeErrorCodes.NoEditor, error.Code);
            Assert.Equal(
                "PMXエディタが起動していない。PMXエディタ(PmxEditor_x64.exe)を起動してから呼び出す。",
                error.Message);
        }

        [Theory]
        [InlineData(new int[] { 1234 })]
        [InlineData(new int[] { 1234, 5678, 9012 })]
        public void EditorWithoutListenerYieldsStatusCheckPrompt(int[] editorProcessIds)
        {
            // エディタが起動していないという案内は、この状況では事実に反する。プラグインが
            // 配置されていない・停止されている・設定が不正で開始しなかった、を区別できる
            // 唯一の場所はエディタのメニューなので、そこへ導く。エディタの数はこの判断に
            // 関わらない——何個起動していても、待ち受けていなければ確かめる先は同じである。
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(null, Entries("lsass"), editorProcessIds));

            Assert.Equal(BridgeErrorCodes.NoHost, error.Code);
            Assert.Equal(
                "PMXエディタは起動しているが、待ち受けているホストがない。エディタのプラグイン"
                    + "メニュー「PMX Editor MCP」で稼働状態を確かめる。",
                error.Message);
        }

        [Fact]
        public void MultipleListenersYieldErrorStatingAmbiguityAndCandidates()
        {
            // 本文の全体を固定する。先頭だけを見る検査では、候補の後ろに設定作業を促す段落を
            // 足した本文も通ってしまう。候補はプロセスIDの昇順に並べる——パイプの列挙順は
            // 保証されないので、並べ替えないと同じ状況でも本文が呼び出しごとに変わる。桁数の
            // 違う値を混ぜる。同じ桁数だけでは、名前の文字列順に並べる実装と区別できない。
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(
                    null,
                    Entries("pmx-editor-mcp-30", "lsass", "pmx-editor-mcp-2", "pmx-editor-mcp-10"),
                    new int[] { 2, 10, 30 }));

            Assert.Equal(BridgeErrorCodes.MultipleHosts, error.Code);
            Assert.Equal(MultipleHostsMessage, error.Message);
        }

        [Theory]
        [InlineData("環境変数")]
        [InlineData("登録")]
        [InlineData("設定")]
        public void MultipleListenerGuidanceDoesNotDemandConfiguration(string forbidden)
        {
            // この本文を読むのは呼び出し元のエージェントで、ブリッジの起動設定を書き換える
            // 立場にない。設定作業を促す案内は、その場で実行できない指示になる。本文全体の
            // 一致とは別に置く——本文を書き直すときに、期待値ごと設定の案内へ戻すのを防ぐ。
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(
                    null,
                    Entries("pmx-editor-mcp-5678", "pmx-editor-mcp-1234"),
                    new int[] { 1234, 5678 }));

            Assert.DoesNotContain(forbidden, error.Message);
        }

        [Theory]
        [InlineData("pmx-editor-mcp-1", 1)]
        [InlineData("pmx-editor-mcp-1234", 1234)]
        [InlineData("pmx-editor-mcp-2147483647", int.MaxValue)]
        public void ProcessIdIsReadFromHostListenerEntry(string pipeName, int expected)
        {
            Assert.Equal(expected, PipeTargetResolver.ProcessIdOf(PipeTargetResolver.PipeDirectory + pipeName));
        }

        [Theory]
        [MemberData(nameof(NamesThatAreNotHostListeners))]
        public void NonHostListenerEntryIsNotACandidate(string pipeName)
        {
            Assert.True(PipeTargetResolver.ProcessIdOf(PipeTargetResolver.PipeDirectory + pipeName) < 0);
        }

        [Fact]
        public void NameOutsidePipeDirectoryIsNotACandidate()
        {
            Assert.True(PipeTargetResolver.ProcessIdOf("pmx-editor-mcp-1234") < 0);
        }

        [Theory]
        [InlineData("pmx-editor-mcp-9")]
        [InlineData("")]
        public void TargetIsPinnedOnlyByTestOnlyEnvironmentVariable(string configured)
        {
            // 空の名前も「指定が無い」ではなく指定として扱い、黙って待受の列挙へ落とさない。
            List<string> readNames = new List<string>();
            bool enumerated = false;
            bool countedEditors = false;

            string resolved = PipeTargetResolver.ResolveFrom(
                name =>
                {
                    readNames.Add(name);
                    return name == PipeTargetResolver.TestPipeEnvironmentVariableName ? configured : null;
                },
                directory =>
                {
                    enumerated = true;
                    return Entries("pmx-editor-mcp-1234");
                },
                processName =>
                {
                    countedEditors = true;
                    return new int[] { 1234 };
                });

            Assert.Equal(configured, resolved);

            // 読むのはテスト専用の名前だけとする。ほかの名前も読んでいる実装は、値の比較だけ
            // では気付けない。
            Assert.Equal(new string[] { PipeTargetResolver.TestPipeEnvironmentVariableName }, readNames);

            // 接続先が決まっているなら数えない。多数並ぶパイプもプロセスも並べる理由がない。
            Assert.False(enumerated);
            Assert.False(countedEditors);
        }

        [Fact]
        public void WithoutEnvironmentVariableTargetComesFromEnumeratedListeners()
        {
            string enumeratedDirectory = null;
            bool countedEditors = false;

            string resolved = PipeTargetResolver.ResolveFrom(
                name => null,
                directory =>
                {
                    enumeratedDirectory = directory;
                    return Entries("lsass", "pmx-editor-mcp-1234");
                },
                processName =>
                {
                    countedEditors = true;
                    return new int[] { 1234 };
                });

            Assert.Equal("pmx-editor-mcp-1234", resolved);

            // 列挙先を内部で固定した実装だと、別のディレクトリを見ていても検出できない。
            Assert.Equal(PipeTargetResolver.PipeDirectory, enumeratedDirectory);

            // エディタの列挙は待受が無いときの案内を分けるためだけのものなので、接続先が
            // 決まるなら走らせない。
            Assert.False(countedEditors);
        }

        [Fact]
        public void MultipleListenersYieldAmbiguityErrorWithoutCountingEditors()
        {
            bool countedEditors = false;

            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.ResolveFrom(
                    name => null,
                    directory => Entries(
                        "pmx-editor-mcp-30", "lsass", "pmx-editor-mcp-2", "pmx-editor-mcp-10"),
                    processName =>
                    {
                        countedEditors = true;
                        return new int[] { 2, 10, 30 };
                    }));

            Assert.Equal(BridgeErrorCodes.MultipleHosts, error.Code);
            Assert.Equal(MultipleHostsMessage, error.Message);

            // 決められないと分かるのも待受だけで済むので、ここでもエディタは数えない。
            Assert.False(countedEditors);
        }

        [Theory]
        [InlineData(new int[0], BridgeErrorCodes.NoEditor)]
        [InlineData(new int[] { 1234 }, BridgeErrorCodes.NoHost)]
        [InlineData(new int[] { 1234, 5678 }, BridgeErrorCodes.NoHost)]
        public void NoListenerCountsEditorsToChooseGuidance(int[] editorProcessIds, string expectedCode)
        {
            string searchedProcessName = null;

            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.ResolveFrom(
                    name => null,
                    directory => Entries("lsass"),
                    processName =>
                    {
                        searchedProcessName = processName;
                        return editorProcessIds;
                    }));

            Assert.Equal(expectedCode, error.Code);

            // 検索するプロセス名を内部で固定した実装だと、別の名前を見ていても検出できない。
            Assert.Equal(PipeTargetResolver.EditorProcessName, searchedProcessName);
        }

        [Theory]
        [InlineData("接続先の指定")]
        [InlineData("待ち受けているパイプ")]
        [InlineData("起動しているPMXエディタ")]
        public void UnreadableSourcesBecomeReturnableFailure(string material)
        {
            // 材料はOSから取るので、権限やハンドルの都合で失敗しうる。素通しすると要求元へ
            // 返せない異常になり、呼び出し元は何が起きたか分からないまま止まる。
            InvalidOperationException refused = new InvalidOperationException("調べられない。");

            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.ResolveFrom(
                    name => material == "接続先の指定" ? throw refused : null,
                    directory => material == "待ち受けているパイプ"
                        ? throw refused
                        : Entries("lsass"),
                    processName => throw refused));

            Assert.Equal(BridgeErrorCodes.ConnectFailed, error.Code);
            Assert.Equal(
                material + "を調べられなかったため接続先を決められない: " + refused.Message,
                error.Message);
        }

        /// <summary>パイプ名の並びを、ディレクトリを列挙したときの項目の形へ直す。</summary>
        private static string[] Entries(params string[] pipeNames)
        {
            string[] entries = new string[pipeNames.Length];
            for (int index = 0; index < pipeNames.Length; index++)
            {
                entries[index] = PipeTargetResolver.PipeDirectory + pipeNames[index];
            }

            return entries;
        }
    }
}
