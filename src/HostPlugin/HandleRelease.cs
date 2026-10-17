using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp
{
    public static class HandleRelease
    {
        public const string ToolName = "session_release_handle";

        public const string HandlesName = "handles";

        public static void AddTo(McpMethodTable methods)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            methods.Add(ToolName, Release);
        }

        private static object Release(McpMethodContext context)
        {
            IList<long> given;
            string code;
            string message;
            if (!TryHandles(context, out given, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            ResolvedTargets resolved;
            if (!TargetSelection.TryResolve(
                new TargetRequest(null, null, null, null, given),
                TargetForm.Handles,
                0,
                id => id >= int.MinValue && id <= int.MaxValue
                    && context.Handles.IsValid((int)id),
                out resolved,
                out code,
                out message,
                TargetNames.Element))
            {
                return ToolEnvelope.Failure(
                    code, string.Equals(code, ToolEnvelope.InvalidHandle, StringComparison.Ordinal)
                        ? Unusable(context, given)
                        : message);
            }

            HandleReleaseResult released;
            if (!context.Handles.TryReleaseAll(
                resolved.Handles.Select(id => (int)id), context.Ui, out released))
            {
                return ToolEnvelope.Failure(
                    ToolEnvelope.InvalidHandle, HandlesName + " に使えないハンドルがある。");
            }

            return ToolEnvelope.Success(
                released.Invalidated.Cast<object>().ToArray(),
                released.Failed
                    .Select(id => "解放が例外で終わったハンドルがある: "
                        + id.ToString(CultureInfo.InvariantCulture))
                    .ToList());
        }

        private static string Unusable(McpMethodContext context, IList<long> given)
        {
            IList<string> reasons = given
                .Where(id => !(id >= int.MinValue && id <= int.MaxValue && context.Handles.IsValid((int)id)))
                .Select(id => id.ToString(CultureInfo.InvariantCulture) + (id > 0 && id <= context.Handles.LastIssuedId
                    ? "(発行済みだが、すでに手放されている。リストへ加えたハンドルは、加えた時点で手放される)"
                    : "(発行されていない)"))
                .ToList();

            return HandlesName + " に使えないハンドルがある: " + string.Join("・", reasons)
                + "。何も解放していない。使えるハンドルだけで呼び直す。";
        }

        /// <summary>解放するハンドルの並び。整数の配列でなければ断る。</summary>
        private static bool TryHandles(
            McpMethodContext context, out IList<long> handles, out string code, out string message)
        {
            handles = null;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            string unknown = context.Params.Keys
                .Where(n => !string.Equals(n, HandlesName, StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unknown != null)
            {
                message = "知らない項目を渡している: " + unknown;

                return false;
            }

            object value;
            context.Params.TryGetValue(HandlesName, out value);
            if (!TargetInput.TryHandleList(value, HandlesName, out handles, out message))
            {
                return false;
            }

            code = null;

            return true;
        }
    }
}
