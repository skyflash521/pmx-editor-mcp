using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 組み立てたツールが <c>operation</c> で受け取る操作の読み取り。受け取れる値はツールごとに
    /// 違うので、名前の並びを渡して引く。
    /// </summary>
    public static class ComposedOperation
    {
        /// <summary>操作を受け取る入力の名前。</summary>
        public const string OperationName = "operation";

        /// <summary>入力から操作を読む。受け取れない値なら偽で、断る内容を渡す。</summary>
        public static bool TryTake(
            McpMethodContext context,
            IList<string> operations,
            out string operation,
            out string code,
            out string message)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            context.Params.TryGetValue(OperationName, out given);
            operation = given as string;
            if (operation == null
                || !operations.Contains(operation, StringComparer.Ordinal))
            {
                operation = null;
                message = OperationName + " は次のどれかでなければならない: "
                    + string.Join("・", operations.ToArray());

                return false;
            }

            code = null;

            return true;
        }
    }
}
