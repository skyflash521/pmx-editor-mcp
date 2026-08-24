using System;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// 応答サイズ予算の設定。ブリッジは環境変数から文字数を読み、ツール定義へ載せるとともに
    /// handshake 応答の値と照合する。構文と有効範囲はホスト側と同一で、構文違反・範囲外のときは
    /// 既定値へ落とさず無効な設定として扱う。
    /// </summary>
    public sealed class BridgeBudget
    {
        /// <summary>応答サイズ予算の文字数を与える環境変数の名前。</summary>
        public const string EnvironmentVariableName = "PMX_EDITOR_MCP_BUDGET_CHARS";

        /// <summary>環境変数が未設定のときに用いる文字数。</summary>
        public const int DefaultChars = 100000;

        /// <summary>受理する文字数の下限。</summary>
        public const int MinimumChars = 10000;

        /// <summary>受理する文字数の上限。</summary>
        public const int MaximumChars = 500000;

        /// <summary>設定が受理できないときにブリッジのプロセスが返す終了コード。</summary>
        public const int InvalidExitCode = 2;

        private BridgeBudget(int chars, string invalidReason)
        {
            throw new NotImplementedException();
        }

        /// <summary>設定が有効かどうか。</summary>
        public bool IsValid => throw new NotImplementedException();

        /// <summary>応答サイズ予算の文字数。設定が無効なときは 0 とし、既定値へは落とさない。</summary>
        public int Chars => throw new NotImplementedException();

        /// <summary>
        /// 設定が無効な理由。<see cref="IsValid"/> が偽のときだけ意味を持つ。標準エラー出力へ
        /// 1行として書き出すため、値に含まれる制御文字はそのまま載せない。
        /// </summary>
        public string InvalidReason => throw new NotImplementedException();

        /// <summary>環境変数の現在値から設定を読む。</summary>
        public static BridgeBudget ReadFromEnvironment()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 環境変数の値から設定を読む。<paramref name="rawValue"/> が null のときは未設定として
        /// 既定値を採る。
        /// </summary>
        public static BridgeBudget Read(string rawValue)
        {
            throw new NotImplementedException();
        }
    }
}
