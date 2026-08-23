using System;
using System.Globalization;
using System.Text;

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

        /// <summary>無効な理由へ載せる、環境変数の値の最大文字数。</summary>
        private const int ReasonValueLengthLimit = 64;

        private ResponseBudget(int chars, string invalidReason)
        {
            Chars = chars;
            InvalidReason = invalidReason;
        }

        /// <summary>設定が有効かどうか。</summary>
        public bool IsValid => InvalidReason == null;

        /// <summary>応答サイズ予算の文字数。設定が無効なときは 0 とし、既定値へは落とさない。</summary>
        public int Chars { get; }

        /// <summary>
        /// 設定が無効な理由。<see cref="IsValid"/> が偽のときだけ意味を持ち、ログと状態表示に用いる。
        /// </summary>
        public string InvalidReason { get; }

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
            if (rawValue == null)
            {
                return new ResponseBudget(DefaultChars, null);
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

            return new ResponseBudget(chars, null);
        }

        private static ResponseBudget Invalid(string rawValue, string requirement)
        {
            return new ResponseBudget(
                0,
                "環境変数 " + EnvironmentVariableName + " の値「" + Describe(rawValue) + "」は受理できない。" + requirement);
        }

        /// <summary>
        /// 環境変数の値を理由へ載せられる形にする。理由はログの1行とメッセージボックスに出るため、
        /// 制御文字は符号位置の表記へ置き換え、長い値は切り詰めて元の長さを添える。
        /// </summary>
        private static string Describe(string rawValue)
        {
            int shownLength = Math.Min(rawValue.Length, ReasonValueLengthLimit);
            StringBuilder described = new StringBuilder(shownLength);
            for (int index = 0; index < shownLength; index++)
            {
                char character = rawValue[index];
                if (char.IsControl(character))
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
