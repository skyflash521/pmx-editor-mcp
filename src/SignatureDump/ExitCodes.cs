namespace PmxEditorMcp.SignatureDump
{
    /// <summary>どの下位コマンドでも同じ意味を持つ終了コード。</summary>
    public static class ExitCodes
    {
        public const int Success = 0;

        /// <summary>下位コマンドの指定や引数の数が合わないとき。</summary>
        public const int InvalidArguments = 2;

        /// <summary>読み込む対象——SDKのアセンブリや能力台帳——が無い、または読めないとき。</summary>
        public const int InputUnavailable = 3;

        public const int WriteFailed = 4;

        /// <summary>読み込めた入力どうしが食い違い、結果を確定できないとき。</summary>
        public const int Unresolved = 5;
    }
}
