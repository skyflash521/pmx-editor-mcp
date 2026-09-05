using System;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// ツール定義へ応答サイズの宣言を付けるかどうかの設定。付けないのは検査の対照を取るときだけで、
    /// 検査からだけ使う入口を開いた起動でしか効かない——通常の配布と運用で、設定を1つ置き違えた
    /// だけで契約が破れることを防ぐ。
    /// </summary>
    public static class BridgeDeclaration
    {
        /// <summary>検査からだけ使う入口を開くかどうかを与える環境変数の名前。</summary>
        public const string DebugHooksVariableName = "PMX_EDITOR_MCP_DEBUG_HOOKS";

        /// <summary>宣言を付けるかどうかを与える環境変数の名前。</summary>
        public const string EnvironmentVariableName = "PMX_EDITOR_MCP_DECLARE_META";

        /// <summary>入口を開く値。</summary>
        public const string DebugHooksEnabledValue = "1";

        /// <summary>宣言を止める値。</summary>
        public const string SuppressedValue = "0";

        /// <summary>環境変数の現在値から、宣言を付けるかどうかを読む。</summary>
        public static bool ReadFromEnvironment()
        {
            return IsDeclared(
                Environment.GetEnvironmentVariable(DebugHooksVariableName),
                Environment.GetEnvironmentVariable(EnvironmentVariableName));
        }

        /// <summary>
        /// 環境変数の値から、宣言を付けるかどうかを決める。止めるのは入口を開いた起動で止める値を
        /// 与えたときだけで、それ以外はすべて付ける。
        /// </summary>
        public static bool IsDeclared(string debugHooksValue, string declareValue)
        {
            if (!string.Equals(debugHooksValue, DebugHooksEnabledValue, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.Equals(declareValue, SuppressedValue, StringComparison.Ordinal);
        }
    }
}
