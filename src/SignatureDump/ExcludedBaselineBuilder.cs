using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳の非対応記載を、公開シグネチャの集合として確定する。台帳の備考は後から書き足せる
    /// ので、確定した集合の側を正本にして、以後の除外がこの集合を超えて広がらないようにする。
    /// </summary>
    public static class ExcludedBaselineBuilder
    {
        /// <summary>生成メンバーを持つ型。台帳が生成対象の型でまとめて指す能力がある。</summary>
        private const string BuilderType = "PEPlugin.IPEBuilder";

        /// <summary>
        /// 台帳の各記載が指す先の決め方。記載は名前を挙げる書き方とまとめて指す書き方が混ざるので、
        /// 選び方の種類ごとに分けて持ち、種類ごとに指す先があることを確かめられるようにする。
        /// </summary>
        private static readonly Selector[] Selectors =
        {
            Selector.Key("CAP-114", "PEPlugin.View.IPEPMDViewConnector.BootupVmdView(PEPlugin.Pmd.IPEPmd,PEPlugin.Vmd.IPEVmd)"),
            Selector.Key("CAP-269", "PXCPlugin.IPXSystemControl.GetCPluginInfo(PXCPlugin.IPXCPlugin)"),
            Selector.Key("CAP-304", "PXCPlugin.UIModel.IPXUIModel.SetAutoRelease(PXCPlugin.IPXCPlugin)"),
            Selector.Key("CAP-339", "PEPlugin.Pmx.IPXPmx.FromStream(System.IO.Stream)"),
            Selector.Key("CAP-339", "PEPlugin.Pmx.IPXPmx.ToStream(System.IO.Stream)"),
            Selector.Key("CAP-390", "PEPlugin.IPEBuilder.CreateVmd(PEPlugin.Pmd.IPEPmd)"),
            Selector.Key("CAP-390", "PEPlugin.IPEBuilder.CreateVmd(PEPlugin.Pmd.IPEPmd,System.String)"),
            Selector.Key("CAP-398", "PEPlugin.IPEBuilder.CreateVme(PEPlugin.Pmd.IPEPmd)"),
            Selector.Type("CAP-459", "PEPlugin.IPEPlugin"),
            Selector.Type("CAP-459", "PEPlugin.PEPluginClass"),
            Selector.Type("CAP-459", "PEPlugin.PEPluginOption"),
            Selector.Type("CAP-459", "PEPlugin.IPERunArgs"),
            Selector.Type("CAP-459", "PEPlugin.PECheckResult"),
            Selector.Type("CAP-461", "PEPlugin.PEStaticBuilder"),
            Selector.Type("CAP-461", "PEPlugin.IPEShortBuilder"),
            Selector.Type("CAP-462", "PEPlugin.IPECheckerPlugin"),
            Selector.Type("CAP-462", "PEPlugin.IPEImportPlugin"),
            Selector.Type("CAP-462", "PEPlugin.IPEExportPlugin"),
            Selector.Namespace("CAP-463", "PEPlugin.Pmd."),
            Selector.Created("CAP-463", "PEPlugin.Pmd."),
            Selector.Type("CAP-465", "PXCPlugin.RegisterBase"),
            Selector.Type("CAP-465", "PXCPlugin.IPXCPlugin"),
            Selector.Type("CAP-465", "PXCPlugin.PXCPluginClass"),
            Selector.Namespace("CAP-466", "PEPlugin.SDX."),
        };

        public static IList<ExcludedBaselineEntry> Build(
            IList<CapabilityRecord> ledger, IList<SignatureRecord> signatures)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            HashSet<string> capabilities = new HashSet<string>(ledger.Select(c => c.Id), StringComparer.Ordinal);
            HashSet<string> taken = new HashSet<string>(StringComparer.Ordinal);
            List<ExcludedBaselineEntry> entries = new List<ExcludedBaselineEntry>();

            foreach (IGrouping<string, Selector> group in Selectors
                .GroupBy(s => s.CapabilityId, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                if (!capabilities.Contains(group.Key))
                {
                    throw Malformed(group.Key, "台帳に無い", signatures.Count);
                }

                SortedSet<string> keys = new SortedSet<string>(StringComparer.Ordinal);
                foreach (Selector selector in group)
                {
                    string[] found = signatures.Where(selector.Matches).Select(s => s.Key).ToArray();
                    if (found.Length == 0)
                    {
                        throw Malformed(group.Key, "指す先が無い: " + selector.Value, signatures.Count);
                    }

                    foreach (string key in found)
                    {
                        keys.Add(key);
                    }
                }

                foreach (string key in keys)
                {
                    if (!taken.Add(key))
                    {
                        throw Malformed(group.Key, "他の能力と重なる: " + key, signatures.Count);
                    }
                }

                entries.Add(new ExcludedBaselineEntry(group.Key, Array.AsReadOnly(keys.ToArray())));
            }

            return entries.AsReadOnly();
        }

        /// <summary>
        /// 突き合わせた件数を添える。台帳の側が合わないのか、渡された公開シグネチャが空なのかで
        /// 直し方が違うのに、能力と理由だけでは読み手が区別できない。
        /// </summary>
        private static InvalidOperationException Malformed(string capabilityId, string reason, int count)
        {
            return new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "{0} の非対応記載が{1}(突き合わせたシグネチャ: {2} 件)",
                capabilityId,
                reason,
                count));
        }

        private sealed class Selector
        {
            private readonly Func<SignatureRecord, string, bool> match;

            private Selector(string capabilityId, string value, Func<SignatureRecord, string, bool> match)
            {
                CapabilityId = capabilityId;
                Value = value;
                this.match = match;
            }

            public string CapabilityId { get; }

            public string Value { get; }

            /// <summary>行キーを1つだけ挙げたもの。</summary>
            public static Selector Key(string capabilityId, string key)
            {
                return new Selector(
                    capabilityId, key, (s, v) => string.Equals(s.Key, v, StringComparison.Ordinal));
            }

            /// <summary>型の名前を挙げたもの。その型が宣言する全メンバーを指す。</summary>
            public static Selector Type(string capabilityId, string typeName)
            {
                return new Selector(
                    capabilityId, typeName, (s, v) => string.Equals(s.DeclaringType, v, StringComparison.Ordinal));
            }

            /// <summary>名前空間でまとめて指したもの。</summary>
            public static Selector Namespace(string capabilityId, string prefix)
            {
                return new Selector(
                    capabilityId, prefix, (s, v) => s.DeclaringType.StartsWith(v, StringComparison.Ordinal));
            }

            /// <summary>
            /// まとめて指した名前空間の型を作る生成メンバー。生成メンバーは名前空間の外にあるので、
            /// 宣言型では選べず、作る型の側から選ぶ。
            /// </summary>
            public static Selector Created(string capabilityId, string prefix)
            {
                return new Selector(
                    capabilityId,
                    prefix,
                    (s, v) => string.Equals(s.DeclaringType, BuilderType, StringComparison.Ordinal)
                        && s.ValueType.StartsWith(v, StringComparison.Ordinal));
            }

            public bool Matches(SignatureRecord signature)
            {
                return match(signature, Value);
            }
        }
    }
}
