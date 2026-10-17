using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    public static class ToolFailure
    {
        public static IDictionary<string, object> Unavailable(UiInvocation invocation = null)
        {
            if (invocation == null || invocation.Unavailable == null)
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.NotApplicable, "いまは要求を受け付けていない。");
            }

            return ToolEnvelope.Failure(
                invocation.DidStart ? ToolEnvelope.PromptShown : ToolEnvelope.NotStarted,
                invocation.Unavailable);
        }

        /// <summary>
        /// SDKの呼び出しが落ちたことを返す。失敗した位置から決まる、エディタの状態を添える。
        /// </summary>
        public static IDictionary<string, object> Failed(Exception failure, EditStage stage)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return ToolEnvelope.Failure(
                ToolEnvelope.OperationFailed,
                failure.Message + " " + EditOutcome.Describe(EditOutcome.Resolve(stage)));
        }
    }
}
