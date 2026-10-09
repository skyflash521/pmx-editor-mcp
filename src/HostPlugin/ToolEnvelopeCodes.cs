using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp
{
    /// <summary>ツールの包みの項目の名前と、ツールが返しうるエラーコード。ホスト以外のアセンブリにもリンクされる。</summary>
    public static partial class ToolEnvelope
    {
        /// <summary>範囲外の位置を指した。</summary>
        public const string IndexOutOfRange = "TOOL_INDEX_OUT_OF_RANGE";

        /// <summary>引数の値が不正。</summary>
        public const string InvalidArgument = "TOOL_INVALID_ARGUMENT";

        /// <summary>ハンドルが不正。</summary>
        public const string InvalidHandle = "TOOL_INVALID_HANDLE";

        /// <summary>危険操作の確認が無い。</summary>
        public const string ConfirmRequired = "TOOL_CONFIRM_REQUIRED";

        /// <summary>現在の状態・提供範囲で適用できない。</summary>
        public const string NotApplicable = "TOOL_NOT_APPLICABLE";

        /// <summary>UIスレッドが空かず、呼び出しを始めていない。同じ要求を投げ直してよい。</summary>
        public const string NotStarted = "TOOL_NOT_STARTED";

        /// <summary>人の応答を待つ表示が出ていて進められないことを断る綴り。</summary>
        public const string PromptShown = "TOOL_PROMPT_SHOWN";

        /// <summary>実行に失敗した。</summary>
        public const string OperationFailed = "TOOL_OPERATION_FAILED";

        /// <summary>応答が応答サイズ予算に収まらない。</summary>
        public const string ResponseTooLarge = "TOOL_RESPONSE_TOO_LARGE";

        /// <summary>要求が要求サイズ予算に収まらない。</summary>
        public const string RequestTooLarge = "TOOL_REQUEST_TOO_LARGE";

        public const string OkName = "ok";

        public const string ValueName = "value";

        public const string ErrorName = "error";

        public const string CodeName = "code";

        public const string MessageName = "message";

        public const string WarningsName = "warnings";

        private static readonly ReadOnlyCollection<string> Codes = new ReadOnlyCollection<string>(
            new[]
            {
                IndexOutOfRange, InvalidArgument, InvalidHandle, ConfirmRequired, NotApplicable,
                NotStarted, PromptShown, OperationFailed, ResponseTooLarge, RequestTooLarge,
            });

        /// <summary>ツールが返しうるエラーコード。閉じた集合とする。</summary>
        public static IList<string> ErrorCodes
        {
            get { return Codes; }
        }
    }
}
