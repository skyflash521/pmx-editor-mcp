using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 応答サイズ予算の設定。ホストは環境変数から文字数を読み、handshake 応答へ載せる。
    /// 構文違反・範囲外のときは既定値へ落とさず、無効な設定として扱う。
    /// </summary>
    public sealed class ResponseBudget
    {
        /// <summary>応答サイズ予算の文字数を与える環境変数の名前。</summary>
        public const string EnvironmentVariableName = "PMX_EDITOR_MCP_BUDGET_CHARS";

        /// <summary>環境変数が未設定のときに用いる文字数。</summary>
        public const int DefaultChars = 100000;

        /// <summary>受理する文字数の下限。</summary>
        public const int MinimumChars = 10000;

        /// <summary>受理する文字数の上限。</summary>
        public const int MaximumChars = 500000;

        /// <summary>設定が有効かどうか。</summary>
        public bool IsValid => throw new NotImplementedException();

        /// <summary>応答サイズ予算の文字数。設定が無効なときは 0 とし、既定値へは落とさない。</summary>
        public int Chars => throw new NotImplementedException();

        /// <summary>
        /// 設定が無効な理由。<see cref="IsValid"/> が偽のときだけ意味を持ち、ログと状態表示に用いる。
        /// </summary>
        public string InvalidReason => throw new NotImplementedException();

        /// <summary>環境変数の現在値から設定を読む。</summary>
        public static ResponseBudget ReadFromEnvironment()
        {
            return Read(Environment.GetEnvironmentVariable(EnvironmentVariableName));
        }

        /// <summary>
        /// 環境変数の値から設定を読む。<paramref name="rawValue"/> が null のときは未設定として既定値を採る。
        /// </summary>
        public static ResponseBudget Read(string rawValue)
        {
            throw new NotImplementedException();
        }
    }
}
