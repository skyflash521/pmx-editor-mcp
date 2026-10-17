using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>起きたイベントを溜め場へ渡す。</summary>
    /// <param name="type">イベント種別の識別子。</param>
    /// <param name="args">イベント固有の値。値を持たないイベントでは null。</param>
    public delegate void EventSink(string type, object args);

    /// <summary>
    /// リスナの公開イベントすべてへ受け手を掛ける。戻り値は掛けた受け手を外す手順で、リスナの
    /// ハンドルが失効するときに呼ぶ。
    /// </summary>
    public delegate Action EventAttach(object listener, EventSink sink);

    public delegate IDictionary<string, object> PayloadReader(object args);

    public sealed class EventBindingTable
    {
        public EventBindingTable(
            IDictionary<string, EventAttach> attachments,
            IDictionary<string, PayloadReader> payloads)
        {
            if (attachments == null)
            {
                throw new ArgumentNullException(nameof(attachments));
            }

            if (payloads == null)
            {
                throw new ArgumentNullException(nameof(payloads));
            }

            Attachments = attachments;
            Payloads = payloads;
        }

        public IDictionary<string, EventAttach> Attachments { get; }

        public IDictionary<string, PayloadReader> Payloads { get; }
    }
}
