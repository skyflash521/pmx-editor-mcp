using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public class PipeTargetResolverTests
    {
        private const string MultipleHostsMessage =
            "ホストが 3 つ待ち受けているため接続先を1つに決められない。select_editor へ対象の"
                + "エディタのプロセスIDを渡して接続先を決める。どれが対象かは list_editors の"
                + "ウィンドウのタイトルで見分ける。待ち受けているホスト:\n"
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
            string resolved = PipeTargetResolver.Resolve(
                null, Entries("pmx-editor-mcp-1234"), new int[] { 1234 });

            Assert.DoesNotContain(PipeTargetResolver.PipeDirectory, resolved);
        }

        [Theory]
        [MemberData(nameof(NamesThatAreNotHostListeners))]
        public void OnlyHostListenersAreCandidatesAmongOtherEntries(string notHostPipeName)
        {
            string resolved = PipeTargetResolver.Resolve(
                null, Entries(notHostPipeName, "pmx-editor-mcp-1234"), new int[] { 1234 });

            Assert.Equal("pmx-editor-mcp-1234", resolved);
        }

        [Fact]
        public void TargetIsResolvedWithMultipleEditorsButOneListener()
        {
            string resolved = PipeTargetResolver.Resolve(
                null, Entries("pmx-editor-mcp-5678"), new int[] { 1234, 5678, 9012 });

            Assert.Equal("pmx-editor-mcp-5678", resolved);
        }

        [Fact]
        public void EditorWithHostBesideItCounts()
        {
            string installed = Directory.CreateTempSubdirectory().FullName;
            try
            {
                string placed = Path.Combine(installed, PipeTargetResolver.HostRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(placed));
                File.WriteAllText(placed, string.Empty);

                Assert.True(PipeTargetResolver.HostsInstalledBeside(
                    Path.Combine(installed, PipeTargetResolver.EditorProcessName + ".exe")));
            }
            finally
            {
                Directory.Delete(installed, true);
            }
        }

        [Fact]
        public void EditorWithoutHostBesideItDoesNotCount()
        {
            string installed = Directory.CreateTempSubdirectory().FullName;
            try
            {
                Assert.False(PipeTargetResolver.HostsInstalledBeside(
                    Path.Combine(installed, PipeTargetResolver.EditorProcessName + ".exe")));
            }
            finally
            {
                Directory.Delete(installed, true);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void UnreadableEditorPathDoesNotCount(string editorExecutablePath)
        {
            Assert.False(PipeTargetResolver.HostsInstalledBeside(editorExecutablePath));
        }

        [Fact]
        public void NoEditorAndNoListenerYieldsStartupPromptError()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.Resolve(null, Entries("lsass"), new int[0]));

            Assert.Equal(BridgeErrorCodes.NoEditor, error.Code);
            Assert.Equal(
                "接続先になるPMXエディタが見つからない。PMXエディタの導入フォルダへ "
                    + @"_plugin\User\PmxEditorMcp.dll を置き、同じフォルダの "
                    + "PmxEditor_x64.exe を起動してから呼び出す。",
                error.Message);
        }

        [Theory]
        [InlineData(new int[] { 1234 })]
        [InlineData(new int[] { 1234, 5678, 9012 })]
        public void EditorWithoutListenerYieldsStatusCheckPrompt(int[] editorProcessIds)
        {
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
            // 桁数の違う値を混ぜる。同じ桁数だけでは、名前の文字列順に並べる実装と区別できない。
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

        [Fact]
        public void SelectedTargetIsUsedAmongMultipleListeners()
        {
            string resolved = PipeTargetResolver.ResolveFrom(
                name => null,
                directory => Entries("pmx-editor-mcp-30", "pmx-editor-mcp-2", "pmx-editor-mcp-10"),
                processName => new int[] { 2, 10, 30 },
                "pmx-editor-mcp-10");

            Assert.Equal("pmx-editor-mcp-10", resolved);
        }

        [Fact]
        public void SelectedTargetOutranksTestOnlyEnvironmentVariable()
        {
            string resolved = PipeTargetResolver.ResolveFrom(
                name => name == PipeTargetResolver.TestPipeEnvironmentVariableName
                    ? "pmx-editor-mcp-30"
                    : null,
                directory => Entries("pmx-editor-mcp-30", "pmx-editor-mcp-10"),
                processName => new int[] { 10, 30 },
                "pmx-editor-mcp-10");

            Assert.Equal("pmx-editor-mcp-10", resolved);
        }

        [Fact]
        public void SelectedEditorThatEndedIsNotReplacedByTheOnlyOtherListener()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.ResolveFrom(
                    name => null,
                    directory => Entries("pmx-editor-mcp-30"),
                    processName => new int[] { 30 },
                    "pmx-editor-mcp-10"));

            Assert.Equal(BridgeErrorCodes.NoEditor, error.Code);
            Assert.Equal(
                "接続先に選んだPMXエディタ(プロセスID 10)が起動していない。ほかのエディタへは"
                    + "繋がない。select_editor で接続先を選び直す。",
                error.Message);
        }

        [Fact]
        public void SelectedEditorWhoseHostStoppedIsNotReplacedByAnotherListener()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.ResolveFrom(
                    name => null,
                    directory => Entries("pmx-editor-mcp-30"),
                    processName => new int[] { 10, 30 },
                    "pmx-editor-mcp-10"));

            Assert.Equal(BridgeErrorCodes.NoHost, error.Code);
            Assert.Equal(
                "接続先に選んだPMXエディタ(プロセスID 10)は起動しているが、ホストが待ち受けて"
                    + "いない。エディタのプラグインメニュー「PMX Editor MCP」で稼働状態を確かめる。",
                error.Message);
        }

        [Fact]
        public void ConfinedDirectoryLeavesOnlyItsEditorsAsCandidates()
        {
            string resolved = PipeTargetResolver.ResolveFrom(
                name => name == PipeTargetResolver.TestEditorDirectoryEnvironmentVariableName
                    ? @"C:\mine"
                    : null,
                directory => Entries("pmx-editor-mcp-10", "pmx-editor-mcp-30"),
                processName => new int[] { 10, 30 },
                null,
                ExecutablesIn(10, @"C:\other", 30, @"C:\Mine\"));

            Assert.Equal("pmx-editor-mcp-30", resolved);
        }

        [Fact]
        public void ConfinedDirectoryCountsOnlyItsEditorsForGuidance()
        {
            BridgeException error = Assert.Throws<BridgeException>(
                () => PipeTargetResolver.ResolveFrom(
                    name => name == PipeTargetResolver.TestEditorDirectoryEnvironmentVariableName
                        ? @"C:\mine"
                        : null,
                    directory => Entries("pmx-editor-mcp-10"),
                    processName => new int[] { 10 },
                    null,
                    ExecutablesIn(10, @"C:\other")));

            Assert.Equal(BridgeErrorCodes.NoEditor, error.Code);
        }

        [Fact]
        public void WithoutConfinedDirectoryNoExecutableIsRead()
        {
            string resolved = PipeTargetResolver.ResolveFrom(
                name => null,
                directory => Entries("pmx-editor-mcp-10"),
                processName => new int[] { 10 },
                null,
                processId => throw new InvalidOperationException("読まれてはならない。"));

            Assert.Equal("pmx-editor-mcp-10", resolved);
        }

        /// <summary>プロセスIDと、その実行ファイルを置いたフォルダを交互に並べて、道を引く処理にする。</summary>
        private static Func<int, string> ExecutablesIn(params object[] pairs)
        {
            Dictionary<int, string> paths = new Dictionary<int, string>();
            for (int index = 0; index < pairs.Length; index += 2)
            {
                paths[(int)pairs[index]] = Path.Combine(
                    (string)pairs[index + 1], PipeTargetResolver.EditorProcessName + ".exe");
            }

            return processId => paths.TryGetValue(processId, out string path) ? path : string.Empty;
        }

        [Fact]
        public void SurveyListsListeningHostsAndEditorsInProcessIdOrder()
        {
            IReadOnlyList<EditorSurveyEntry> surveyed = PipeTargetResolver.SurveyFrom(
                directory => Entries("pmx-editor-mcp-30", "lsass", "pmx-editor-mcp-2"),
                processName => new int[] { 30, 10, 2 },
                processId => "タイトル" + processId.ToString(CultureInfo.InvariantCulture));

            Assert.Equal(new int[] { 2, 10, 30 }, surveyed.Select(entry => entry.ProcessId).ToArray());
            Assert.Equal(
                new bool[] { true, false, true }, surveyed.Select(entry => entry.Listening).ToArray());
            Assert.Equal(
                new string[] { "タイトル2", "タイトル10", "タイトル30" },
                surveyed.Select(entry => entry.Title).ToArray());
        }

        [Fact]
        public void SurveyListsListenerWhoseEditorWasNotCounted()
        {
            // 数えるのは導入フォルダにホストを置いたエディタだけで、待ち受けているパイプの
            // 持ち主がそこに入るとは限らない。
            IReadOnlyList<EditorSurveyEntry> surveyed = PipeTargetResolver.SurveyFrom(
                directory => Entries("pmx-editor-mcp-7"),
                processName => new int[0],
                processId => string.Empty);

            EditorSurveyEntry only = Assert.Single(surveyed);
            Assert.Equal(7, only.ProcessId);
            Assert.True(only.Listening);
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
