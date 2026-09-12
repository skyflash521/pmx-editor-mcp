using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 台帳が発行したハンドルを解放するツール。1つのSDKメンバーへ写らないので、能力対応表の行を
    /// 持たず、ここが受け持つ。指したハンドルとその依存子をまとめて失効させる。
    /// </summary>
    public static class HandleRelease
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "session_release_handle";

        /// <summary>解放するハンドルを受け取る入力の名前。</summary>
        public const string HandlesName = "handles";

        /// <summary>ツールを表へ足す。</summary>
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
                return ToolEnvelope.Failure(code, message);
            }

            HandleReleaseResult released;
            if (!context.Handles.TryReleaseAll(
                resolved.Handles.Select(id => (int)id), out released))
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
            object[] items = context.Params.TryGetValue(HandlesName, out value)
                ? value as object[]
                : null;
            if (items == null)
            {
                message = HandlesName + " はハンドルの配列でなければならない。";

                return false;
            }

            List<long> taken = new List<long>();
            foreach (object item in items)
            {
                if (!ValueInput.IsNumber(item))
                {
                    message = HandlesName + " は整数の配列でなければならない。";

                    return false;
                }

                double written = Convert.ToDouble(item, CultureInfo.InvariantCulture);
                if (written != Math.Floor(written) || written < long.MinValue
                    || written > long.MaxValue)
                {
                    message = HandlesName + " は整数の配列でなければならない。";

                    return false;
                }

                taken.Add((long)written);
            }

            code = null;
            handles = taken;

            return true;
        }
    }
}
