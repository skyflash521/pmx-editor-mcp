using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using PmxEditorMcp.SignatureDump;

namespace PmxEditorMcp.Bridge
{
    /// <summary>接続先に選べるPMXエディタの1件。</summary>
    public sealed class EditorSurveyEntry
    {
        /// <summary>プロセスIDと、ホストが待ち受けているかと、ウィンドウのタイトルを与えて生成する。</summary>
        public EditorSurveyEntry(int processId, bool listening, string title)
        {
            ProcessId = processId;
            Listening = listening;
            Title = title;
        }

        /// <summary>エディタのプロセスID。</summary>
        public int ProcessId { get; }

        /// <summary>そのエディタのホストが待ち受けているか。</summary>
        public bool Listening { get; }

        /// <summary>エディタのメインウィンドウのタイトル。読めなければ空。</summary>
        public string Title { get; }
    }

    /// <summary>
    /// ホストの待受パイプ名を決める。接続先が選ばれていればそれだけを相手にし、選ばれていなければ
    /// 待ち受けているホストが1つのときだけそれを接続先にする。決められないときは要求元へ返せる
    /// エラーにする。
    /// </summary>
    public static class PipeTargetResolver
    {
        /// <summary>
        /// テスト専用。接続先のパイプ名を固定する環境変数の名前。実機ではホストが待ち受けて
        /// いると自動発見が曖昧になるため、テストはこの指定で対象を固定する。利用者向けの
        /// 接続先の選び分けにはこの環境変数を用いない。
        /// </summary>
        public const string TestPipeEnvironmentVariableName = "PMX_EDITOR_MCP_TEST_PIPE";

        /// <summary>
        /// テスト専用。接続先を選んでいないとき、候補をこのフォルダの実行ファイルから動くエディタに
        /// 限る環境変数の名前。実機の検査が、ほかのエディタが動いている中で自分の起こしたエディタ
        /// だけを相手にするために使う。
        /// </summary>
        public const string TestEditorDirectoryEnvironmentVariableName = "PMX_EDITOR_MCP_TEST_EDITOR_DIR";

        /// <summary>待受が無いときの案内を分けるために数えるPMXエディタのプロセス名。</summary>
        public const string EditorProcessName = "PmxEditor_x64";

        /// <summary>エディタの導入フォルダの中で、ホストが置かれる場所。</summary>
        public const string HostRelativePath = @"_plugin\User\PmxEditorMcp.dll";

        /// <summary>ホストの待受パイプ名の接頭辞。この後ろにエディタのプロセスIDが続く。</summary>
        public const string PipeNamePrefix = "pmx-editor-mcp-";

        /// <summary>待ち受けているパイプが並ぶディレクトリ。ここを列挙して接続先を探す。</summary>
        public const string PipeDirectory = @"\\.\pipe\";

        private const int NotAHostPipe = -1;

        /// <summary>エディタのプロセスIDからホストの待受パイプ名を作る。</summary>
        public static string PipeNameForProcess(int processId)
        {
            return PipeNamePrefix + processId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 接続先のパイプ名を決める。<paramref name="configuredPipeName"/> が null のときだけ
        /// <paramref name="pipeDirectoryEntries"/> から決める。項目は
        /// <see cref="PipeDirectory"/> を列挙した結果をそのまま渡してよく、ホストの待受パイプで
        /// ないものはここで落とす。<paramref name="editorProcessIds"/> は待ち受けているホストが
        /// 無いときの案内を分けるためだけに使う。
        /// </summary>
        public static string Resolve(
            string configuredPipeName,
            IReadOnlyList<string> pipeDirectoryEntries,
            IReadOnlyList<int> editorProcessIds)
        {
            if (configuredPipeName != null)
            {
                return configuredPipeName;
            }

            if (pipeDirectoryEntries == null)
            {
                throw new ArgumentNullException(nameof(pipeDirectoryEntries));
            }

            if (editorProcessIds == null)
            {
                throw new ArgumentNullException(nameof(editorProcessIds));
            }

            return Decide(HostPipeNamesIn(pipeDirectoryEntries), () => editorProcessIds);
        }

        /// <summary>
        /// 待ち受けているホストから接続先のパイプ名を決める。<paramref name="selectedPipeName"/> が
        /// null でなければ、それが待ち受けているときだけそれを返す。
        /// </summary>
        public static string ResolveFromRunningHosts(string selectedPipeName)
        {
            return ResolveFrom(
                Environment.GetEnvironmentVariable,
                Directory.GetFiles,
                FindEditorProcessIds,
                selectedPipeName);
        }

        /// <summary>
        /// 接続先を決める材料の取り方を差し替えて解決する。実機のパイプとプロセスに依存せずに
        /// 経路を確かめられるようにするための入口。
        /// </summary>
        internal static string ResolveFrom(
            Func<string, string> readEnvironmentVariable,
            Func<string, IReadOnlyList<string>> enumeratePipeDirectory,
            Func<string, IReadOnlyList<int>> findEditorProcessIds)
        {
            return ResolveFrom(
                readEnvironmentVariable, enumeratePipeDirectory, findEditorProcessIds, null);
        }

        /// <summary>
        /// 選んだ接続先を与えて解決する。選んだ接続先が待ち受けていなければ、ほかの待受が在っても
        /// そちらへ移さずエラーにする。
        /// </summary>
        internal static string ResolveFrom(
            Func<string, string> readEnvironmentVariable,
            Func<string, IReadOnlyList<string>> enumeratePipeDirectory,
            Func<string, IReadOnlyList<int>> findEditorProcessIds,
            string selectedPipeName)
        {
            return ResolveFrom(
                readEnvironmentVariable,
                enumeratePipeDirectory,
                findEditorProcessIds,
                selectedPipeName,
                ExecutablePathOfProcess);
        }

        /// <summary>
        /// プロセスIDから実行ファイルの道を引く処理も差し替えて解決する。道を引くのは、候補を
        /// 限るフォルダが与えられたときだけである。
        /// </summary>
        internal static string ResolveFrom(
            Func<string, string> readEnvironmentVariable,
            Func<string, IReadOnlyList<string>> enumeratePipeDirectory,
            Func<string, IReadOnlyList<int>> findEditorProcessIds,
            string selectedPipeName,
            Func<int, string> executablePathOf)
        {
            if (selectedPipeName != null)
            {
                IReadOnlyList<string> listening = HostPipeNamesIn(
                    TakeMaterial(() => enumeratePipeDirectory(PipeDirectory), "待ち受けているパイプ"));
                foreach (string pipeName in listening)
                {
                    if (string.Equals(pipeName, selectedPipeName, StringComparison.Ordinal))
                    {
                        return selectedPipeName;
                    }
                }

                throw SelectedNotListening(
                    selectedPipeName,
                    TakeMaterial(
                        () => findEditorProcessIds(EditorProcessName), "起動しているPMXエディタ"));
            }

            string configuredPipeName = TakeMaterial(
                () => readEnvironmentVariable(TestPipeEnvironmentVariableName), "接続先の指定");

            if (configuredPipeName != null)
            {
                // 接続先が決まっているなら数えるものが無い。列挙には無関係なパイプが多数並ぶ
                // ので、要らない呼び出しはここで打ち切る。
                return configuredPipeName;
            }

            string confinedDirectory = TakeMaterial(
                () => readEnvironmentVariable(TestEditorDirectoryEnvironmentVariableName), "候補の限り");
            Func<int, bool> admits = processId => confinedDirectory == null
                || RunsFrom(executablePathOf(processId), confinedDirectory);

            List<string> listeningPipeNames = new List<string>();
            foreach (string pipeName in HostPipeNamesIn(
                TakeMaterial(() => enumeratePipeDirectory(PipeDirectory), "待ち受けているパイプ")))
            {
                if (admits(ProcessIdOf(PipeDirectory + pipeName)))
                {
                    listeningPipeNames.Add(pipeName);
                }
            }

            return Decide(
                listeningPipeNames,
                () => Admitted(
                    TakeMaterial(
                        () => findEditorProcessIds(EditorProcessName), "起動しているPMXエディタ"),
                    admits));
        }

        private static IReadOnlyList<int> Admitted(IReadOnlyList<int> processIds, Func<int, bool> admits)
        {
            List<int> admitted = new List<int>();
            foreach (int processId in processIds)
            {
                if (admits(processId))
                {
                    admitted.Add(processId);
                }
            }

            return admitted;
        }

        /// <summary>その実行ファイルが、そのフォルダの直下に置かれているか。大文字小文字は区別しない。</summary>
        private static bool RunsFrom(string executablePath, string directory)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                return false;
            }

            try
            {
                string placed = Path.GetDirectoryName(Path.GetFullPath(executablePath));
                string wanted = Path.GetFullPath(directory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(placed, wanted, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        /// <summary>そのプロセスの実行ファイルの道。読めなければ空を返す。</summary>
        private static string ExecutablePathOfProcess(int processId)
        {
            try
            {
                using Process running = Process.GetProcessById(processId);
                return ExecutablePathOf(running);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 接続先に選べるエディタを並べる。待ち受けているホストの持ち主と、導入フォルダにホストを
        /// 置いたエディタの両方を、プロセスIDの昇順に1件ずつ返す。
        /// </summary>
        public static IReadOnlyList<EditorSurveyEntry> SurveyRunningEditors()
        {
            return SurveyFrom(Directory.GetFiles, FindEditorProcessIds, WindowTitleOf);
        }

        /// <summary>
        /// 並べる材料の取り方を差し替えて並べる。実機のパイプとプロセスに依存せずに確かめられる
        /// ようにするための入口。
        /// </summary>
        internal static IReadOnlyList<EditorSurveyEntry> SurveyFrom(
            Func<string, IReadOnlyList<string>> enumeratePipeDirectory,
            Func<string, IReadOnlyList<int>> findEditorProcessIds,
            Func<int, string> windowTitleOf)
        {
            SortedDictionary<int, bool> listeningByProcessId = new SortedDictionary<int, bool>();
            foreach (string entry in TakeMaterial(
                () => enumeratePipeDirectory(PipeDirectory), "待ち受けているパイプ"))
            {
                int processId = ProcessIdOf(entry);
                if (processId >= 0)
                {
                    listeningByProcessId[processId] = true;
                }
            }

            foreach (int processId in TakeMaterial(
                () => findEditorProcessIds(EditorProcessName), "起動しているPMXエディタ"))
            {
                if (!listeningByProcessId.ContainsKey(processId))
                {
                    listeningByProcessId[processId] = false;
                }
            }

            List<EditorSurveyEntry> surveyed = new List<EditorSurveyEntry>();
            foreach (KeyValuePair<int, bool> found in listeningByProcessId)
            {
                surveyed.Add(new EditorSurveyEntry(found.Key, found.Value, windowTitleOf(found.Key)));
            }

            return surveyed;
        }

        /// <summary>そのプロセスのメインウィンドウのタイトル。読めなければ空を返す。</summary>
        private static string WindowTitleOf(int processId)
        {
            try
            {
                using Process editor = Process.GetProcessById(processId);
                return editor.MainWindowTitle ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 接続先を決める材料を取る。材料はいずれもOSから取るので、失敗の種類を数え上げられ
        /// ない。素通しすると要求元へ返せない異常になってしまうので、どの材料を取れなかったか
        /// を添えて、結果として返せる失敗にする。
        /// </summary>
        private static T TakeMaterial<T>(Func<T> take, string material)
        {
            try
            {
                return take();
            }
            catch (BridgeException)
            {
                throw;
            }
            catch (Exception error)
            {
                throw new BridgeException(
                    BridgeErrorCodes.ConnectFailed,
                    material + "を調べられなかったため接続先を決められない: " + error.Message);
            }
        }

        /// <summary>
        /// パイプディレクトリの項目からエディタのプロセスIDを読む。ホストの待受パイプで
        /// なければ負の値を返す。
        /// </summary>
        internal static int ProcessIdOf(string pipeDirectoryEntry)
        {
            if (pipeDirectoryEntry == null
                || !pipeDirectoryEntry.StartsWith(PipeDirectory, StringComparison.Ordinal))
            {
                return NotAHostPipe;
            }

            string pipeName = pipeDirectoryEntry.Substring(PipeDirectory.Length);
            if (!pipeName.StartsWith(PipeNamePrefix, StringComparison.Ordinal))
            {
                return NotAHostPipe;
            }

            string processIdText = pipeName.Substring(PipeNamePrefix.Length);
            if (!IsProcessIdText(processIdText))
            {
                return NotAHostPipe;
            }

            int processId;
            if (!int.TryParse(processIdText, NumberStyles.None, CultureInfo.InvariantCulture, out processId))
            {
                // 桁があふれた。ホストが名乗れる値ではない。
                return NotAHostPipe;
            }

            return processId;
        }

        /// <summary>
        /// 待受の数で接続先を決める。エディタを数えるのは待受が無いときの案内を分けるため
        /// だけなので、答えが出る場合まで数えさせない。
        /// </summary>
        private static string Decide(
            IReadOnlyList<string> listeningPipeNames, Func<IReadOnlyList<int>> editorProcessIds)
        {
            if (listeningPipeNames.Count == 1)
            {
                return listeningPipeNames[0];
            }

            if (listeningPipeNames.Count == 0)
            {
                throw NotListening(editorProcessIds());
            }

            throw new BridgeException(
                BridgeErrorCodes.MultipleHosts, DescribeCandidates(listeningPipeNames));
        }

        private static BridgeException NotListening(IReadOnlyList<int> editorProcessIds)
        {
            if (editorProcessIds.Count == 0)
            {
                return new BridgeException(
                    BridgeErrorCodes.NoEditor,
                    "接続先になるPMXエディタが見つからない。PMXエディタの導入フォルダへ "
                        + HostRelativePath + " を置き、同じフォルダの "
                        + EditorProcessName + ".exe を起動してから呼び出す。");
            }

            // ホストを置いたエディタは在るのに待ち受けていない。メニューから停止した・設定が
            // 不正で開始しなかった、のどちらなのかは外から区別できないので、稼働状態を確かめ
            // られる場所だけを示す。
            return new BridgeException(
                BridgeErrorCodes.NoHost,
                "PMXエディタは起動しているが、待ち受けているホストがない。エディタのプラグイン"
                    + "メニュー「PMX Editor MCP」で稼働状態を確かめる。");
        }

        /// <summary>選んだ接続先が待ち受けていないことを、エディタが残っているかで分けて伝える。</summary>
        private static BridgeException SelectedNotListening(
            string selectedPipeName, IReadOnlyList<int> editorProcessIds)
        {
            int processId = ProcessIdOf(PipeDirectory + selectedPipeName);
            string described = processId < 0
                ? selectedPipeName
                : "プロセスID " + processId.ToString(CultureInfo.InvariantCulture);

            if (processId >= 0 && Contains(editorProcessIds, processId))
            {
                return new BridgeException(
                    BridgeErrorCodes.NoHost,
                    "接続先に選んだPMXエディタ(" + described + ")は起動しているが、ホストが"
                        + "待ち受けていない。エディタのプラグインメニュー「PMX Editor MCP」で稼働"
                        + "状態を確かめる。");
            }

            return new BridgeException(
                BridgeErrorCodes.NoEditor,
                "接続先に選んだPMXエディタ(" + described + ")が起動していない。ほかのエディタへは"
                    + "繋がない。" + FixedToolTable.SelectEditorName + " で接続先を選び直す。");
        }

        private static bool Contains(IReadOnlyList<int> values, int wanted)
        {
            foreach (int value in values)
            {
                if (value == wanted)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 複数のホストが待ち受けているときの説明を作る。読むのは呼び出し元のエージェントなので、
        /// 決められない事実と候補に、接続先を選ぶツールを添える。
        /// </summary>
        private static string DescribeCandidates(IReadOnlyList<string> listeningPipeNames)
        {
            StringBuilder described = new StringBuilder();
            described.Append("ホストが ")
                .Append(listeningPipeNames.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" つ待ち受けているため接続先を1つに決められない。")
                .Append(FixedToolTable.SelectEditorName)
                .Append(" へ対象のエディタのプロセスIDを渡して接続先を決める。どれが対象かは ")
                .Append(FixedToolTable.ListEditorsName)
                .Append(" のウィンドウのタイトルで見分ける。待ち受けているホスト:");

            foreach (string pipeName in listeningPipeNames)
            {
                described.Append('\n').Append(pipeName);
            }

            return described.ToString();
        }

        /// <summary>
        /// 列挙した項目からホストの待受パイプ名だけを取り出す。候補にするのは待受パイプの名前が
        /// 在るホストで、そのホストが今すぐ新しい接続を受けられるかどうかは見ない。
        ///
        /// 候補はプロセスIDの昇順に並べる——パイプの列挙順は保証されないので、並べ替えないと
        /// 同じ状況でも案内の本文が呼び出しごとに変わる。
        /// </summary>
        private static IReadOnlyList<string> HostPipeNamesIn(IReadOnlyList<string> pipeDirectoryEntries)
        {
            List<KeyValuePair<int, string>> found = new List<KeyValuePair<int, string>>();
            foreach (string entry in pipeDirectoryEntries)
            {
                int processId = ProcessIdOf(entry);
                if (processId >= 0)
                {
                    found.Add(new KeyValuePair<int, string>(
                        processId, entry.Substring(PipeDirectory.Length)));
                }
            }

            found.Sort((left, right) => left.Key.CompareTo(right.Key));

            string[] pipeNames = new string[found.Count];
            for (int index = 0; index < found.Count; index++)
            {
                pipeNames[index] = found[index].Value;
            }

            return pipeNames;
        }

        /// <summary>
        /// ホストが名乗るプロセスIDの書き方かを見る。ホストは自分のプロセスIDを十進で書く
        /// だけなので、符号・空白・桁区切り・ASCII以外の数字は現れない。先頭に0が付く形も、
        /// プロセスIDに割り当てられない0そのものも同じく現れない。
        /// </summary>
        private static bool IsProcessIdText(string text)
        {
            if (text.Length == 0 || text[0] == '0')
            {
                return false;
            }

            foreach (char character in text)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// その実行ファイルの導入フォルダにホストが置かれているか。同じ名前のエディタは別の導入
        /// フォルダからも動き、ホストの置かれていない導入フォルダのエディタは待ち受けることが
        /// ない。
        /// </summary>
        internal static bool HostsInstalledBeside(string editorExecutablePath)
        {
            if (string.IsNullOrEmpty(editorExecutablePath))
            {
                return false;
            }

            string directory;
            try
            {
                directory = Path.GetDirectoryName(editorExecutablePath);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            return File.Exists(Path.Combine(directory, HostRelativePath));
        }

        private static IReadOnlyList<int> FindEditorProcessIds(string processName)
        {
            Process[] editors = Process.GetProcessesByName(processName);
            try
            {
                List<int> processIds = new List<int>();
                foreach (Process editor in editors)
                {
                    if (HostsInstalledBeside(ExecutablePathOf(editor)))
                    {
                        processIds.Add(editor.Id);
                    }
                }

                return processIds;
            }
            finally
            {
                foreach (Process editor in editors)
                {
                    editor.Dispose();
                }
            }
        }

        /// <summary>
        /// そのプロセスの実行ファイルの道。読めなければ空を返す。
        /// </summary>
        private static string ExecutablePathOf(Process editor)
        {
            try
            {
                return editor.MainModule == null ? string.Empty : editor.MainModule.FileName;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
