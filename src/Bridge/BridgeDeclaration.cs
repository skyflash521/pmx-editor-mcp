using System;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// ツール定義へ応答サイズの宣言を付けるかどうかの設定。付けないのは検査の対照を取るときだけで、
    /// 検査からだけ使う入口を開いた起動でしか効かない。
    /// </summary>
    public static class BridgeDeclaration
    {
        public const string EnvironmentVariableName = "PMX_EDITOR_MCP_DECLARE_META";

        public const string SuppressedValue = "0";

        public static bool ReadFromEnvironment()
        {
            return IsDeclared(
                Environment.GetEnvironmentVariable(BridgeDebugHooks.EnvironmentVariableName),
                Environment.GetEnvironmentVariable(EnvironmentVariableName));
        }

        /// <summary>
        /// 環境変数の値から、宣言を付けるかどうかを決める。止めるのは入口を開いた起動で止める値を
        /// 与えたときだけで、それ以外はすべて付ける。
        /// </summary>
        public static bool IsDeclared(string debugHooksValue, string declareValue)
        {
            if (!BridgeDebugHooks.IsEnabled(debugHooksValue))
            {
                return true;
            }

            return !string.Equals(declareValue, SuppressedValue, StringComparison.Ordinal);
        }
    }
}
