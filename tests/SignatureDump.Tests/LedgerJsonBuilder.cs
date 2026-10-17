using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>テストが読ませる能力台帳の正本を組み立てる。</summary>
    internal sealed class LedgerJsonBuilder
    {
        private readonly List<Dictionary<string, object>> capabilities =
            new List<Dictionary<string, object>>();

        public LedgerJsonBuilder Add(
            string id,
            string category,
            string target,
            string status,
            string owner,
            string remarks)
        {
            this.capabilities.Add(new Dictionary<string, object>
            {
                { "id", id },
                { "category", category },
                { "target", target },
                { "status", status },
                { "owner", owner },
                { "remarks", remarks },
            });

            return this;
        }

        /// <summary>分類が提供で担当がモデルの、備考を持たない行。</summary>
        public LedgerJsonBuilder Add(string id, string target)
        {
            return this.Add(id, "標本", target, "提供", "モデル", string.Empty);
        }

        /// <summary>名前空間をまとめて指す、分類が非対応の2行。</summary>
        public LedgerJsonBuilder AddNamespaceRows()
        {
            return this
                .Add("CAP-463", "標本", "PEPlugin.Pmd.* のまとめ", "非対応", string.Empty, string.Empty)
                .Add("CAP-466", "標本", "PEPlugin.SDX.* のまとめ", "非対応", string.Empty, string.Empty);
        }

        public override string ToString()
        {
            Dictionary<string, object> document = new Dictionary<string, object>
            {
                {
                    "source", new Dictionary<string, object>
                    {
                        { "distribution", "標本" },
                        { "assembly", "PEPlugin.dll" },
                        { "assemblyVersion", "0.0.0.0" },
                        { "framework", ".NET Framework 4.0" },
                    }
                },
                { "capabilities", this.capabilities },
            };

            return new JavaScriptSerializer().Serialize(document);
        }
    }
}
