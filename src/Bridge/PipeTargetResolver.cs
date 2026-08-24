using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// ホストの待受パイプ名を決める。環境変数での明示指定を優先し、無ければ起動中の
    /// PMXエディタから自動で決める。決められないときは要求元へ返せるエラーにする。
    /// </summary>
    public static class PipeTargetResolver
    {
        /// <summary>接続先のパイプ名を明示するための環境変数の名前。</summary>
        public const string EnvironmentVariableName = "PMX_EDITOR_MCP_PIPE";

        /// <summary>自動発見で数えるPMXエディタのプロセス名。</summary>
        public const string EditorProcessName = "PmxEditor_x64";

        private const string PipeNamePrefix = "pmx-editor-mcp-";

        /// <summary>エディタのプロセスIDからホストの待受パイプ名を作る。</summary>
        public static string PipeNameForProcess(int processId)
        {
            return PipeNamePrefix + processId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 接続先のパイプ名を決める。<paramref name="configuredPipeName"/> が null のときだけ
        /// <paramref name="editorProcessIds"/> から自動で決める。
        /// </summary>
        public static string Resolve(string configuredPipeName, IReadOnlyList<int> editorProcessIds)
        {
            if (configuredPipeName != null)
            {
                return configuredPipeName;
            }

            if (editorProcessIds == null)
            {
                throw new ArgumentNullException(nameof(editorProcessIds));
            }

            if (editorProcessIds.Count == 0)
            {
                throw new BridgeException(
                    BridgeErrorCodes.NoEditor,
                    "PMXエディタが起動していない。PMXエディタ(" + EditorProcessName
                        + ".exe)を起動してから呼び出す。");
            }

            if (editorProcessIds.Count == 1)
            {
                return PipeNameForProcess(editorProcessIds[0]);
            }

            throw new BridgeException(BridgeErrorCodes.MultipleEditors, DescribeCandidates(editorProcessIds));
        }

        /// <summary>環境変数の現在値と起動中のPMXエディタから接続先のパイプ名を決める。</summary>
        public static string ResolveFromEnvironment()
        {
            string configuredPipeName = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (configuredPipeName != null)
            {
                // 明示指定があるならエディタを数える必要がない。呼び出しのたびにプロセスを
                // 列挙しないよう、ここで打ち切る。
                return configuredPipeName;
            }

            return Resolve(null, FindEditorProcessIds());
        }

        private static IReadOnlyList<int> FindEditorProcessIds()
        {
            Process[] editors = Process.GetProcessesByName(EditorProcessName);
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

        /// <summary>
        /// 複数起動しているときの説明を作る。候補はプロセスIDの昇順に並べる——プロセスの
        /// 列挙順は保証されないので、並べ替えないと同じ状況でも本文が呼び出しごとに変わる。
        /// </summary>
        private static string DescribeCandidates(IReadOnlyList<int> editorProcessIds)
        {
            int[] sortedProcessIds = new int[editorProcessIds.Count];
            for (int index = 0; index < sortedProcessIds.Length; index++)
            {
                sortedProcessIds[index] = editorProcessIds[index];
            }

            Array.Sort(sortedProcessIds);

            StringBuilder described = new StringBuilder();
            described.Append("PMXエディタが ")
                .Append(sortedProcessIds.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" つ起動しているため接続先を1つに決められない。環境変数 ")
                .Append(EnvironmentVariableName)
                .Append(" で接続先のパイプ名を指定する。候補:");

            foreach (int processId in sortedProcessIds)
            {
                described.Append('\n').Append(PipeNameForProcess(processId));
            }

            return described.ToString();
        }
    }
}
