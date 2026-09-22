// 画面を撮る行が返す画像は、その呼び出しが作ったものなので、応答へ詰めたら手放す。

using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 呼び出しが描いて作る画像を返す行。ほかの行が返す画像は、返した相手が持ち続けるものなので
    /// ここには入らない。
    /// </summary>
    public static class DrawnImages
    {
        private static readonly HashSet<string> Rows = new HashSet<string>(StringComparer.Ordinal)
        {
            "PEPlugin.View.IPEPMDViewConnector.GetClientImage()",
            "PEPlugin.View.IPESubViewConnector.GetClientImage()",
            "PEPlugin.View.IPETransformViewConnector.GetClientImage()",
        };

        /// <summary>その行が返す画像を、応答へ詰めたあと手放してよいか。</summary>
        public static bool Owns(string rowKey)
        {
            return rowKey != null && Rows.Contains(rowKey);
        }

        /// <summary>その行が返した値が持ち物なら手放す。</summary>
        public static void Release(string rowKey, object value)
        {
            IDisposable held = Owns(rowKey) ? value as IDisposable : null;
            if (held != null)
            {
                held.Dispose();
            }
        }
    }
}
