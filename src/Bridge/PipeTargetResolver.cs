using System;
using System.Collections.Generic;

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

        /// <summary>エディタのプロセスIDからホストの待受パイプ名を作る。</summary>
        public static string PipeNameForProcess(int processId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 接続先のパイプ名を決める。<paramref name="configuredPipeName"/> が null のときだけ
        /// <paramref name="editorProcessIds"/> から自動で決める。
        /// </summary>
        public static string Resolve(string configuredPipeName, IReadOnlyList<int> editorProcessIds)
        {
            throw new NotImplementedException();
        }

        /// <summary>環境変数の現在値と起動中のPMXエディタから接続先のパイプ名を決める。</summary>
        public static string ResolveFromEnvironment()
        {
            throw new NotImplementedException();
        }
    }
}
