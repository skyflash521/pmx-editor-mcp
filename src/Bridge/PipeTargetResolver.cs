using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// ホストの待受パイプ名を決める。待ち受けているホストが1つならそれを接続先にし、
    /// 決められないときは要求元へ返せるエラーにする。
    /// </summary>
    public static class PipeTargetResolver
    {
        /// <summary>
        /// テスト専用。接続先のパイプ名を固定する環境変数の名前。実機ではホストが待ち受けて
        /// いると自動発見が曖昧になるため、テストはこの指定で対象を固定する。利用者向けの
        /// 接続先の選び分けにはこの環境変数を用いない。
        /// </summary>
        public const string TestPipeEnvironmentVariableName = "PMX_EDITOR_MCP_TEST_PIPE";

        /// <summary>待受が無いときの案内を分けるために数えるPMXエディタのプロセス名。</summary>
        public const string EditorProcessName = "PmxEditor_x64";

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

        /// <summary>待ち受けているホストから接続先のパイプ名を決める。</summary>
        public static string ResolveFromRunningHosts()
        {
            return ResolveFrom(
                Environment.GetEnvironmentVariable, Directory.GetFiles, FindEditorProcessIds);
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
            string configuredPipeName = TakeMaterial(
                () => readEnvironmentVariable(TestPipeEnvironmentVariableName), "接続先の指定");

            if (configuredPipeName != null)
            {
                // 接続先が決まっているなら数えるものが無い。列挙には無関係なパイプが多数並ぶ
                // ので、要らない呼び出しはここで打ち切る。
                return configuredPipeName;
            }

            IReadOnlyList<string> listeningPipeNames = HostPipeNamesIn(
                TakeMaterial(() => enumeratePipeDirectory(PipeDirectory), "待ち受けているパイプ"));

            return Decide(
                listeningPipeNames,
                () => TakeMaterial(
                    () => findEditorProcessIds(EditorProcessName), "起動しているPMXエディタ"));
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
                    "PMXエディタが起動していない。PMXエディタ(" + EditorProcessName
                        + ".exe)を起動してから呼び出す。");
            }

            // エディタは在るのに待ち受けていない。プラグインを配置していない・メニューから
            // 停止した・設定が不正で開始しなかった、のどれなのかは外から区別できないので、
            // 稼働状態を確かめられる場所だけを示す。
            return new BridgeException(
                BridgeErrorCodes.NoHost,
                "PMXエディタは起動しているが、待ち受けているホストがない。エディタのプラグイン"
                    + "メニュー「PMX Editor MCP」で稼働状態を確かめる。");
        }

        /// <summary>
        /// 複数のホストが待ち受けているときの説明を作る。読むのは呼び出し元のエージェントで、
        /// ブリッジの起動設定を書き換える立場にないので、決められない事実と候補だけを伝える。
        /// </summary>
        private static string DescribeCandidates(IReadOnlyList<string> listeningPipeNames)
        {
            StringBuilder described = new StringBuilder();
            described.Append("ホストが ")
                .Append(listeningPipeNames.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" つ待ち受けているため接続先を1つに決められない。どのエディタを対象に")
                .Append("するかを利用者に確かめる。待ち受けているホスト:");

            foreach (string pipeName in listeningPipeNames)
            {
                described.Append('\n').Append(pipeName);
            }

            return described.ToString();
        }

        /// <summary>
        /// 列挙した項目からホストの待受パイプ名だけを取り出す。候補にするのは待受パイプの名前が
        /// 在るホストで、そのホストが今すぐ新しい接続を受けられるかどうかは見ない——ホストは
        /// 同時接続を1本に限るので、別の接続が使っている間も名前は残る。これを候補から外すと、
        /// 対象にしたいエディタが塞がっているというだけで別のエディタへ黙って繋いでしまう。
        /// 塞がっている相手を指した場合は、接続の待機上限を超えた失敗として原因が伝わる。
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

        private static IReadOnlyList<int> FindEditorProcessIds(string processName)
        {
            Process[] editors = Process.GetProcessesByName(processName);
            try
            {
                int[] processIds = new int[editors.Length];
                for (int index = 0; index < editors.Length; index++)
                {
                    processIds[index] = editors[index].Id;
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
    }
}
