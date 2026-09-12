using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace PmxEditorMcp
{
    /// <summary>
    /// 溜まったイベントを古い順に取り出すツール。1つのSDKメンバーへ写らないので、能力対応表の行を
    /// 持たずここが受け持つ。値の枠に収まる件数だけを1回で返し、1件だけでも収まらないイベントは
    /// 捨てて数える——捨てなければ、そのイベントより先へ列が進まなくなる。
    /// </summary>
    public static class EventPoll
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "view_poll_events";

        /// <summary>取り出す件数の上限を受け取る入力の名前。</summary>
        public const string LimitName = "limit";

        private const string EventsName = "events";

        private const string DroppedName = "dropped";

        private const string RemainingName = "remaining";

        private const string SeqName = "seq";

        private const string TypeName = "type";

        private const string SourceHandleName = "sourceHandle";

        private const string PayloadName = "payload";

        /// <summary>並びの中で、イベント1件の手前に置く区切りの文字数。</summary>
        private const int SeparatorChars = 1;

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        /// <summary>
        /// 取り出した並びを包む分の文字数。並びを空にして、数の項目を採りうるいちばん長い値で
        /// 書いたものを採る——この枠を差し引いてから並びの件数を決めないと、収めたはずの応答が
        /// 値の枠を超えて、取り出したイベントが呼び出す側へ届かないまま消える。
        /// </summary>
        private static readonly int Wrapper = Serializer.Serialize(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { EventsName, new object[0] },
                { DroppedName, int.MaxValue },
                { RemainingName, int.MaxValue },
            }).Length;

        /// <summary>ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            methods.Add(ToolName, Poll);
        }

        private static object Poll(McpMethodContext context)
        {
            int limit;
            string code;
            string message;
            if (!TryLimit(context, out limit, out code, out message))
            {
                return ToolEnvelope.Failure(code, message);
            }

            int room = ResponseSize.ValueChars(context.BudgetChars) - Wrapper;
            List<IDictionary<string, object>> written = new List<IDictionary<string, object>>();
            int used = 0;
            EventDrainResult drained = context.Events.Drain(limit, queued =>
            {
                IDictionary<string, object> item = Item(queued);
                int size = Serializer.Serialize(item).Length + SeparatorChars;
                if (used + size <= room)
                {
                    used += size;
                    written.Add(item);

                    return EventFit.Take;
                }

                return written.Count == 0 ? EventFit.Drop : EventFit.Stop;
            });

            return ToolEnvelope.Success(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { EventsName, written.ToArray() },
                { DroppedName, drained.Dropped },
                { RemainingName, drained.Remaining },
            });
        }

        private static IDictionary<string, object> Item(QueuedEvent queued)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { SeqName, queued.Seq },
                { TypeName, queued.Type },
                { SourceHandleName, queued.SourceHandle },
                { PayloadName, queued.Payload },
            };
        }

        private static bool TryLimit(
            McpMethodContext context, out int limit, out string code, out string message)
        {
            limit = EventQueue.DefaultLimit;
            code = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(LimitName, out given))
            {
                return true;
            }

            if (!ValueInput.IsNumber(given))
            {
                code = ToolEnvelope.InvalidArgument;
                message = LimitName + " は整数でなければならない。";

                return false;
            }

            double taken = Convert.ToDouble(given, CultureInfo.InvariantCulture);
            if (taken != Math.Floor(taken) || taken < 1 || taken > EventQueue.MaxLimit)
            {
                code = ToolEnvelope.InvalidArgument;
                message = LimitName + " は1以上 " + EventQueue.MaxLimit + " 以下の整数である。";

                return false;
            }

            limit = (int)taken;

            return true;
        }
    }
}
