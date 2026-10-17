using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>能力台帳の非対応記載を、公開シグネチャの集合として確定する。</summary>
    public static class ExcludedBaselineBuilder
    {
        public static IList<ExcludedBaselineEntry> Build(
            IList<CapabilityRecord> ledger,
            IList<SignatureRecord> signatures,
            IList<FrozenExclusion> frozen)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (frozen == null)
            {
                throw new ArgumentNullException(nameof(frozen));
            }

            ILookup<string, CapabilityRecord> capabilities = ledger.ToLookup(c => c.Id, StringComparer.Ordinal);
            HashSet<string> taken = new HashSet<string>(StringComparer.Ordinal);
            List<ExcludedBaselineEntry> entries = new List<ExcludedBaselineEntry>();

            foreach (FrozenExclusion exclusion in frozen
                .OrderBy(e => e.CapabilityId, StringComparer.Ordinal))
            {
                CapabilityRecord[] recorded = capabilities[exclusion.CapabilityId].ToArray();
                if (recorded.Length == 0)
                {
                    throw Malformed(exclusion.CapabilityId, "台帳に無い", signatures.Count);
                }

                if (recorded.Length > 1)
                {
                    // 同じ能力が何度も書かれていると、どの記載を根拠にしたのかが定まらない。
                    throw Malformed(exclusion.CapabilityId, "台帳に何度も現れる", signatures.Count);
                }

                RequireRecorded(recorded[0], exclusion, signatures.Count);

                SortedSet<string> keys = new SortedSet<string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, Func<SignatureRecord, bool>> selector in
                    Selectors(recorded[0], exclusion, signatures.Count))
                {
                    string[] found = signatures.Where(selector.Value).Select(s => s.Key).ToArray();
                    if (found.Length == 0)
                    {
                        throw Malformed(
                            exclusion.CapabilityId, "指す先が無い: " + selector.Key, signatures.Count);
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
                        throw Malformed(
                            exclusion.CapabilityId, "他の能力と重なる: " + key, signatures.Count);
                    }
                }

                entries.Add(new ExcludedBaselineEntry(
                    exclusion.CapabilityId, Array.AsReadOnly(keys.ToArray())));
            }

            return entries.AsReadOnly();
        }

        private static void RequireRecorded(
            CapabilityRecord capability, FrozenExclusion exclusion, int count)
        {
            if (capability.Status != exclusion.Status)
            {
                throw Malformed(
                    capability.Id, "台帳の分類が凍結の前提と違う: " + capability.Status, count);
            }

            if (!string.Equals(capability.Target, exclusion.Target, StringComparison.Ordinal))
            {
                throw Malformed(capability.Id, "台帳の対象が凍結の前提と違う: " + capability.Target, count);
            }

            if (exclusion.Remarks != null
                && !string.Equals(capability.Remarks, exclusion.Remarks, StringComparison.Ordinal))
            {
                throw Malformed(capability.Id, "台帳の備考が凍結の前提と違う: " + capability.Remarks, count);
            }
        }

        private static IEnumerable<KeyValuePair<string, Func<SignatureRecord, bool>>> Selectors(
            CapabilityRecord capability, FrozenExclusion exclusion, int count)
        {
            foreach (string key in exclusion.Signatures)
            {
                yield return new KeyValuePair<string, Func<SignatureRecord, bool>>(
                    key, s => string.Equals(s.Key, key, StringComparison.Ordinal));
            }

            foreach (string type in exclusion.Types)
            {
                yield return new KeyValuePair<string, Func<SignatureRecord, bool>>(
                    type, s => string.Equals(s.DeclaringType, type, StringComparison.Ordinal));
            }

            if (exclusion.Signatures.Count != 0 || exclusion.Types.Count != 0)
            {
                yield break;
            }

            if (capability.TargetKind != CapabilityTargetKind.Pattern)
            {
                throw Malformed(capability.Id, "選び方を挙げず、まとめて指す書き方でもない", count);
            }

            string prefix;
            try
            {
                prefix = LedgerPattern.Prefix(capability);
            }
            catch (InvalidOperationException exception)
            {
                throw Malformed(capability.Id, exception.Message, count);
            }

            yield return new KeyValuePair<string, Func<SignatureRecord, bool>>(
                prefix, s => s.DeclaringType.StartsWith(prefix, StringComparison.Ordinal));
            if (LedgerPattern.CoversCreation(capability))
            {
                yield return new KeyValuePair<string, Func<SignatureRecord, bool>>(
                    LedgerPattern.BuilderType + " → " + prefix,
                    s => string.Equals(s.DeclaringType, LedgerPattern.BuilderType, StringComparison.Ordinal)
                        && LedgerPattern.Covers(capability, s));
            }
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
    }
}
