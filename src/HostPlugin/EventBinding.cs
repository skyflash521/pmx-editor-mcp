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

    /// <summary>イベント固有の値を、応答へ載せる組へ直す。</summary>
    public delegate IDictionary<string, object> PayloadReader(object args);

    /// <summary>
    /// リスナの型ごとの受け手の掛け方と、イベント種別ごとの値の読み方。どちらも開発時に組み立てた
    /// 結び付きで、配布物は実行時リフレクションを使わない。
    /// </summary>
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

        /// <summary>リスナの型の名前から、受け手の掛け方へ。</summary>
        public IDictionary<string, EventAttach> Attachments { get; }

        /// <summary>イベント種別の識別子から、値の読み方へ。</summary>
        public IDictionary<string, PayloadReader> Payloads { get; }
    }
}
