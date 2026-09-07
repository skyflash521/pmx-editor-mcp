using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 検査がイベントを積むための入口。キューの契約——連番・取りこぼし・残り・発生元——は、
    /// キューへイベントが入らないと確かめられないので、実イベントと同じ積み方を通す。
    /// </summary>
    public static class DebugEventInjection
    {
        /// <summary>この入口のメソッド名。MCPのツールとしては公開しない。</summary>
        public const string MethodName = "debug_enqueue_event";

        private const string TypeParameterName = "type";

        private const string SourceHandleParameterName = "sourceHandle";

        private const string PayloadParameterName = "payload";

        /// <summary>
        /// 入口が開いているときだけ表へ足す。閉じているときは足さないので、要求は未知のメソッド
        /// として返る。
        /// </summary>
        public static void AddTo(McpMethodTable methods, bool enabled)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (enabled)
            {
                methods.Add(MethodName, Enqueue);
            }
        }

        /// <summary>積んだイベントの連番を返す。呼んだ側はこれで自分が積んだ分を見分ける。</summary>
        public static object Enqueue(McpMethodContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            object payload;
            context.Params.TryGetValue(PayloadParameterName, out payload);
            QueuedEvent queued = context.Events.Enqueue(
                RequestParameter.Text(context.Params, TypeParameterName),
                RequestParameter.PositiveInteger(context.Params, SourceHandleParameterName),
                payload);

            return new Dictionary<string, object>(StringComparer.Ordinal) { { "seq", queued.Seq } };
        }
    }
}
