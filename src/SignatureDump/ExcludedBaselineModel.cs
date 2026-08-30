using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳がすでに非対応と記していた能力と、その能力が指す公開シグネチャの組。除外を後から
    /// 広げられないよう、この組を凍結して残す。
    /// </summary>
    public sealed class ExcludedBaselineEntry
    {
        public ExcludedBaselineEntry(string capabilityId, IList<string> signatures)
        {
            CapabilityId = capabilityId;
            Signatures = signatures;
        }

        public string CapabilityId { get; }

        /// <summary>行キーの昇順。</summary>
        public IList<string> Signatures { get; }
    }
}
