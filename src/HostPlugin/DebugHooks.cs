using System;

namespace PmxEditorMcp
{
    /// <summary>検査からだけ使う入口を開くかどうかの設定。環境変数を設定して起動したときだけ開く。</summary>
    public static class DebugHooks
    {
        public const string EnvironmentVariableName = "PMX_EDITOR_MCP_DEBUG_HOOKS";

        /// <summary>入口を開く値。これ以外はすべて閉じたままとする。</summary>
        public const string EnabledValue = "1";

        public static bool ReadFromEnvironment()
        {
            return IsEnabled(Environment.GetEnvironmentVariable(EnvironmentVariableName));
        }

        /// <summary>
        /// 環境変数の値から、入口を開くかどうかを決める。開くのは値がちょうど開く値のときだけで、
        /// 未設定・空・前後の空白・大小の違いはいずれも閉じたままとする。
        /// </summary>
        public static bool IsEnabled(string rawValue)
        {
            return string.Equals(rawValue, EnabledValue, StringComparison.Ordinal);
        }
    }
}
