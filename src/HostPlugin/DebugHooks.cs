using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 検査からだけ使う入口を開くかどうかの設定。配布物は開いた版と同じで、環境変数を設定して
    /// 起動したときだけ開く。
    /// </summary>
    public static class DebugHooks
    {
        /// <summary>入口を開くかどうかを与える環境変数の名前。</summary>
        public const string EnvironmentVariableName = "PMX_EDITOR_MCP_DEBUG_HOOKS";

        /// <summary>入口を開く値。これ以外はすべて閉じたままとする。</summary>
        public const string EnabledValue = "1";

        /// <summary>環境変数の現在値から、入口を開くかどうかを読む。</summary>
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
