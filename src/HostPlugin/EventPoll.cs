using System;
using System.Collections.Generic;
using System.Globalization;

namespace PmxEditorMcp
{
    /// <summary>
    /// 溜まったイベントを古い順に取り出すツール。値の枠に収まる件数だけを1回で返し、1件だけでも
    /// 収まらないイベントは捨てて数える。
    /// </summary>
    public static class EventPoll
    {
        public const string ToolName = "view_poll_events";

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

        /// <summary>
        /// 取り出した並びを包む分の文字数。並びを空にして、数の項目を採りうるいちばん長い値で
        /// 書いたものを採る。
        /// </summary>
        private static readonly int Wrapper = ResponseSize.Serializer.Serialize(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { EventsName, new object[0] },
                { DroppedName, int.MaxValue },
                { RemainingName, int.MaxValue },
            }).Length;

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
                int size = ResponseSize.Serializer.Serialize(item).Length + SeparatorChars;
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
