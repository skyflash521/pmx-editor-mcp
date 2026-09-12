using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// イベント固有の値の1項目を、応答へ載せられる形へ直す。溜め場へ入れるところで直すので、
    /// 取り出しはSDKの値を持たない——受け手はエディタのUIスレッドで走り、取り出しは別のスレッドから
    /// 来る。
    /// </summary>
    public static class EventPayload
    {
        /// <summary>写せない型なら <see cref="InvalidOperationException"/>。</summary>
        public static object Of<T>(T value)
        {
            object json;
            System.Collections.Generic.IList<string> written;
            string code;
            string message;
            if (!ValueShape.TryToJson(
                typeof(T), value, ImageTransfer.DefaultMaxLongSide,
                out json, out written, out code, out message))
            {
                throw new InvalidOperationException(
                    "イベントの値を写せない: " + typeof(T).FullName + " " + message);
            }

            return json;
        }
    }
}
