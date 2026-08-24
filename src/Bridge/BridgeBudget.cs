using System;
using System.Globalization;
using System.Text;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// 応答サイズ予算の設定。ブリッジは環境変数から文字数を読み、ツール定義へ載せるとともに
    /// handshake 応答の値と照合する。構文と有効範囲はホスト側と同一で、構文違反・範囲外のときは
    /// 既定値へ落とさず無効な設定として扱う。判定はホスト側と同じ規則だが、対象フレームワークの
    /// 違う別プロジェクトのため実装は共有できない。
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

        private const int ReasonValueLengthLimit = 64;

        private BridgeBudget(int chars, string invalidReason)
        {
            Chars = chars;
            InvalidReason = invalidReason;
        }

        /// <summary>設定が有効かどうか。</summary>
        public bool IsValid => InvalidReason == null;

        /// <summary>応答サイズ予算の文字数。設定が無効なときは 0 とし、既定値へは落とさない。</summary>
        public int Chars { get; }

        /// <summary>
        /// 設定が無効な理由。<see cref="IsValid"/> が偽のときだけ意味を持つ。標準エラー出力へ
        /// 1行として書き出すため、値に含まれる制御文字はそのまま載せない。
        /// </summary>
        public string InvalidReason { get; }

        /// <summary>環境変数の現在値から設定を読む。</summary>
        public static BridgeBudget ReadFromEnvironment()
        {
            return Read(Environment.GetEnvironmentVariable(EnvironmentVariableName));
        }

        /// <summary>
        /// 環境変数の値から設定を読む。<paramref name="rawValue"/> が null のときは未設定として
        /// 既定値を採る。
        /// </summary>
        public static BridgeBudget Read(string rawValue)
        {
            if (rawValue == null)
            {
                return new BridgeBudget(DefaultChars, null);
            }

            if (!IsStrictDecimal(rawValue))
            {
                return Invalid(
                    rawValue,
                    "ASCII数字のみからなる10進表記で指定する(符号・前後の空白・先頭のゼロ・ASCII以外の数字は受理しない)。");
            }

            int chars;
            bool parsed = int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out chars);
            if (!parsed || chars < MinimumChars || chars > MaximumChars)
            {
                return Invalid(
                    rawValue,
                    MinimumChars.ToString(CultureInfo.InvariantCulture) + " 以上 " +
                    MaximumChars.ToString(CultureInfo.InvariantCulture) + " 以下の範囲で指定する。");
            }

            return new BridgeBudget(chars, null);
        }

        private static BridgeBudget Invalid(string rawValue, string requirement)
        {
            return new BridgeBudget(
                0,
                "環境変数 " + EnvironmentVariableName + " の値「" + Describe(rawValue) + "」は受理できない。" + requirement);
        }

        /// <summary>
        /// 環境変数の値を理由へ載せられる形にする。理由は標準エラー出力の1行に収めるため、
        /// 行を割りうる文字は符号位置の表記へ置き換え、長い値は切り詰めて元の長さを添える。
        /// </summary>
        private static string Describe(string rawValue)
        {
            int shownLength = Math.Min(rawValue.Length, ReasonValueLengthLimit);
            StringBuilder described = new StringBuilder(shownLength);
            for (int index = 0; index < shownLength; index++)
            {
                char character = rawValue[index];
                if (NeedsEscape(character))
                {
                    described.Append("<U+")
                        .Append(((int)character).ToString("X4", CultureInfo.InvariantCulture))
                        .Append(">");
                }
                else
                {
                    described.Append(character);
                }
            }

            if (rawValue.Length > shownLength)
            {
                described.Append("…(全 ")
                    .Append(rawValue.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(" 文字)");
            }

            return described.ToString();
        }

        /// <summary>
        /// 符号位置の表記へ置き換える文字かどうか。行区切りと段落区切りは制御文字に分類されないので、
        /// 制御文字の判定だけでは理由が1行に収まらない。
        /// </summary>
        private static bool NeedsEscape(char character)
        {
            return char.IsControl(character)
                || character == (char)0x2028
                || character == (char)0x2029;
        }

        /// <summary>符号・空白・先頭のゼロ・ASCII以外の数字を許さない10進表記かどうかを判定する。</summary>
        private static bool IsStrictDecimal(string value)
        {
            if (value.Length == 0)
            {
                return false;
            }

            if (value.Length > 1 && value[0] == '0')
            {
                return false;
            }

            foreach (char character in value)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
