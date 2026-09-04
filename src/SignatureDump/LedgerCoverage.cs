using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>照合が通ったときの内訳。</summary>
    public sealed class LedgerCoverageResult
    {
        public LedgerCoverageResult(
            int publicTypes,
            int ledgerTypes,
            int outOfScopeTypes,
            int publicSignatures,
            int population,
            int outOfScopeSignatures,
            int excluded,
            int provided)
        {
            PublicTypes = publicTypes;
            LedgerTypes = ledgerTypes;
            OutOfScopeTypes = outOfScopeTypes;
            PublicSignatures = publicSignatures;
            Population = population;
            OutOfScopeSignatures = outOfScopeSignatures;
            Excluded = excluded;
            Provided = provided;
        }

        public int PublicTypes { get; }

        /// <summary>台帳の行が指す型。</summary>
        public int LedgerTypes { get; }

        /// <summary>型単位の対象外。</summary>
        public int OutOfScopeTypes { get; }

        public int PublicSignatures { get; }

        /// <summary>台帳の母集合。</summary>
        public int Population { get; }

        /// <summary>シグネチャ単位の対象外。型単位の対象外が宣言するものは含まない。</summary>
        public int OutOfScopeSignatures { get; }

        public int Excluded { get; }

        /// <summary>提供と分類した行が指すシグネチャから除外一覧を引いたもの。</summary>
        public int Provided { get; }
    }

    /// <summary>
    /// 台帳と公開APIの逆向きの照合と、除外一覧の照合。公開型と公開シグネチャの全件が、台帳の側か
    /// 対象外一覧の側のどちらかにちょうど1回現れることを求める。
    /// </summary>
    public static class LedgerCoverage
    {
        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static LedgerCoverageResult Verify(
            IList<CapabilityRecord> ledger,
            InventoryRecord inventory,
            IList<ExcludedSignatureRecord> excluded,
            LedgerOutOfScopeRecord outOfScope)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (excluded == null)
            {
                throw new ArgumentNullException(nameof(excluded));
            }

            if (outOfScope == null)
            {
                throw new ArgumentNullException(nameof(outOfScope));
            }

            LedgerPopulation population = LedgerPopulation.Resolve(ledger, inventory);
            OutOfScopeClassifier classifier = new OutOfScopeClassifier(inventory, population.Signatures);

            ISet<string> outOfScopeTypes = Names(outOfScope);
            VerifyTypes(inventory, population, outOfScopeTypes, outOfScope, classifier);

            ISet<string> declaredByOutOfScopeTypes = new HashSet<string>(
                inventory.Signatures
                    .Where(s => outOfScopeTypes.Contains(s.DeclaringType))
                    .Select(s => s.Key),
                StringComparer.Ordinal);
            VerifySignatures(
                inventory, population, declaredByOutOfScopeTypes, outOfScope, classifier);

            VerifyRecordedCounts(ledger, population, excluded);

            int provided = VerifyOwners(ledger, population, excluded);

            return new LedgerCoverageResult(
                inventory.Types.Count,
                population.Types.Count,
                outOfScope.Types.Count,
                inventory.Signatures.Count,
                population.Signatures.Count,
                outOfScope.Signatures.Count,
                excluded.Count,
                provided);
        }

        private static ISet<string> Names(LedgerOutOfScopeRecord outOfScope)
        {
            return new HashSet<string>(outOfScope.Types.Select(t => t.Name), StringComparer.Ordinal);
        }

        private static void VerifyTypes(
            InventoryRecord inventory,
            LedgerPopulation population,
            ISet<string> outOfScopeTypes,
            LedgerOutOfScopeRecord outOfScope,
            OutOfScopeClassifier classifier)
        {
            ISet<string> all = new HashSet<string>(
                inventory.Types.Select(t => t.Name), StringComparer.Ordinal);

            RequireDisjoint(population.Types, outOfScopeTypes, "台帳の行が指す型", "型単位の対象外");
            RequireSame(
                all,
                Union(population.Types, outOfScopeTypes),
                "公開型",
                "台帳の行が指す型と型単位の対象外の和");

            foreach (OutOfScopeTypeEntry entry in outOfScope.Types)
            {
                if (!all.Contains(entry.Name))
                {
                    throw Mismatch("対象外一覧の型が公開型に無い: " + entry.Name);
                }

                OutOfScopeReason? computed = classifier.ClassifyType(entry.Name);
                if (computed == null)
                {
                    throw Mismatch("対象外にできる理由が無い型: " + entry.Name);
                }

                if (computed.Value != entry.Reason)
                {
                    throw Mismatch(string.Format(
                        CultureInfo.InvariantCulture,
                        "型の理由が算出値と違う: {0} 記載={1} 算出={2}",
                        entry.Name,
                        entry.Reason,
                        computed.Value));
                }
            }
        }

        private static void VerifySignatures(
            InventoryRecord inventory,
            LedgerPopulation population,
            ISet<string> declaredByOutOfScopeTypes,
            LedgerOutOfScopeRecord outOfScope,
            OutOfScopeClassifier classifier)
        {
            ISet<string> listed = new HashSet<string>(
                outOfScope.Signatures.Select(s => s.Key), StringComparer.Ordinal);
            ISet<string> all = new HashSet<string>(
                inventory.Signatures.Select(s => s.Key), StringComparer.Ordinal);

            RequireDisjoint(
                population.Signatures, declaredByOutOfScopeTypes, "母集合", "型単位の対象外が宣言するもの");
            RequireDisjoint(population.Signatures, listed, "母集合", "シグネチャ単位の対象外");
            RequireDisjoint(
                declaredByOutOfScopeTypes,
                listed,
                "型単位の対象外が宣言するもの",
                "シグネチャ単位の対象外");
            RequireSame(
                all,
                Union(Union(population.Signatures, declaredByOutOfScopeTypes), listed),
                "公開シグネチャ",
                "母集合と対象外の和");

            Dictionary<string, SignatureRecord> byKey =
                inventory.Signatures.ToDictionary(s => s.Key, StringComparer.Ordinal);
            foreach (OutOfScopeSignatureEntry entry in outOfScope.Signatures)
            {
                SignatureRecord signature;
                if (!byKey.TryGetValue(entry.Key, out signature))
                {
                    throw Mismatch("対象外一覧のシグネチャが公開シグネチャに無い: " + entry.Key);
                }

                OutOfScopeReason? computed = classifier.ClassifySignature(signature);
                if (computed == null)
                {
                    throw Mismatch("対象外にできる理由が無いシグネチャ: " + entry.Key);
                }

                if (computed.Value != entry.Reason)
                {
                    throw Mismatch(string.Format(
                        CultureInfo.InvariantCulture,
                        "シグネチャの理由が算出値と違う: {0} 記載={1} 算出={2}",
                        entry.Key,
                        entry.Reason,
                        computed.Value));
                }
            }
        }

        /// <summary>
        /// 台帳が備考へ書いた非対応件数と、その行が指すシグネチャのうち除外一覧に載る数を
        /// 突き合わせる。件数を書けるのは、分類が提供でその数が1件以上の行だけとする。
        /// </summary>
        private static void VerifyRecordedCounts(
            IList<CapabilityRecord> ledger,
            LedgerPopulation population,
            IList<ExcludedSignatureRecord> excluded)
        {
            ISet<string> removed = new HashSet<string>(
                excluded.Select(e => e.Key), StringComparer.Ordinal);
            Dictionary<string, int> actual = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ISet<string>> owned in population.Owners)
            {
                if (!removed.Contains(owned.Key))
                {
                    continue;
                }

                foreach (string id in owned.Value)
                {
                    int current;
                    actual[id] = actual.TryGetValue(id, out current) ? current + 1 : 1;
                }
            }

            foreach (CapabilityRecord row in ledger)
            {
                int counted;
                if (!actual.TryGetValue(row.Id, out counted))
                {
                    counted = 0;
                }

                int recorded;
                bool written = TryReadCount(row, out recorded);
                bool expected = row.Status == CapabilityStatus.Provided && counted > 0;

                if (written != expected)
                {
                    throw Mismatch(string.Format(
                        CultureInfo.InvariantCulture,
                        expected
                            ? "{0} は除外一覧に {1} 件あるのに非対応件数を書いていない"
                            : "{0} は非対応件数を書ける行ではない",
                        row.Id,
                        counted));
                }

                if (written && recorded != counted)
                {
                    throw Mismatch(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} の非対応件数が除外一覧と合わない: 台帳={1} 除外一覧={2}",
                        row.Id,
                        recorded,
                        counted));
                }
            }
        }

        /// <summary>
        /// 備考が非対応件数を書いていれば真とし、その数を返す。書いていなければ偽で、このとき
        /// <paramref name="value"/> は0になる。接頭辞が先頭以外に現れる備考と、数を伴わない
        /// 接頭辞は、書いたつもりで検査を素通りするので不合格にする。
        /// </summary>
        private static bool TryReadCount(CapabilityRecord row, out int value)
        {
            const string Prefix = "非対応件数:";
            value = 0;
            string remarks = row.Remarks ?? string.Empty;
            if (!remarks.StartsWith(Prefix, StringComparison.Ordinal))
            {
                if (remarks.IndexOf(Prefix, StringComparison.Ordinal) >= 0)
                {
                    throw Mismatch(row.Id + " の非対応件数が備考の先頭にない: " + remarks);
                }

                return false;
            }

            string rest = remarks.Substring(Prefix.Length).TrimStart(' ');
            int end = 0;
            while (end < rest.Length && rest[end] >= '0' && rest[end] <= '9')
            {
                end++;
            }

            if (end == 0 || !int.TryParse(
                    rest.Substring(0, end), NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                throw Mismatch(row.Id + " の非対応件数が数になっていない: " + remarks);
            }

            string tail = rest.Substring(end);
            if (tail.Length != 0 && tail[0] != '。')
            {
                throw Mismatch(row.Id + " の非対応件数が数で終わっていない: " + remarks);
            }

            if (tail.IndexOf(Prefix, StringComparison.Ordinal) >= 0)
            {
                throw Mismatch(row.Id + " の備考に非対応件数が二度現れる: " + remarks);
            }

            return true;
        }

        /// <summary>
        /// 提供対象のシグネチャが属する提供行の担当が1つに定まることを確かめ、その件数を返す。
        /// </summary>
        private static int VerifyOwners(
            IList<CapabilityRecord> ledger,
            LedgerPopulation population,
            IList<ExcludedSignatureRecord> excluded)
        {
            Dictionary<string, CapabilityRecord> rows =
                new Dictionary<string, CapabilityRecord>(StringComparer.Ordinal);
            foreach (CapabilityRecord row in ledger)
            {
                if (rows.ContainsKey(row.Id))
                {
                    throw Mismatch("同じ能力IDが台帳に二度現れる: " + row.Id);
                }

                rows[row.Id] = row;
            }

            ISet<string> removed = new HashSet<string>(
                excluded.Select(e => e.Key), StringComparer.Ordinal);

            int provided = 0;
            foreach (KeyValuePair<string, ISet<string>> owned in population.Owners)
            {
                if (removed.Contains(owned.Key))
                {
                    continue;
                }

                ISet<CapabilityOwner> owners = new HashSet<CapabilityOwner>(
                    owned.Value
                        .Select(id => rows[id])
                        .Where(r => r.Status == CapabilityStatus.Provided)
                        .Select(r => r.Owner));
                if (owners.Count == 0)
                {
                    continue;
                }

                if (owners.Count > 1)
                {
                    throw Mismatch("提供対象の担当が1つに定まらない: " + owned.Key);
                }

                provided++;
            }

            return provided;
        }

        private static ISet<string> Union(ISet<string> left, ISet<string> right)
        {
            HashSet<string> union = new HashSet<string>(left, StringComparer.Ordinal);
            union.UnionWith(right);
            return union;
        }

        private static void RequireDisjoint(
            ISet<string> left, ISet<string> right, string leftName, string rightName)
        {
            HashSet<string> both = new HashSet<string>(left, StringComparer.Ordinal);
            both.IntersectWith(right);
            if (both.Count > 0)
            {
                throw Mismatch(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} と {1} が重なる: {2}",
                    leftName,
                    rightName,
                    Sample(both)));
            }
        }

        private static void RequireSame(
            ISet<string> expected, ISet<string> actual, string expectedName, string actualName)
        {
            HashSet<string> missing = new HashSet<string>(expected, StringComparer.Ordinal);
            missing.ExceptWith(actual);
            HashSet<string> extra = new HashSet<string>(actual, StringComparer.Ordinal);
            extra.ExceptWith(expected);

            if (missing.Count == 0 && extra.Count == 0)
            {
                return;
            }

            StringBuilder message = new StringBuilder();
            message.Append(expectedName).Append(" と ").Append(actualName).Append(" が一致しない。");
            if (missing.Count > 0)
            {
                message.Append(actualName).Append(" に無い: ").Append(Sample(missing)).Append("。");
            }

            if (extra.Count > 0)
            {
                message.Append(expectedName).Append(" に無い: ").Append(Sample(extra)).Append("。");
            }

            throw Mismatch(message.ToString());
        }

        private static string Sample(ICollection<string> identifiers)
        {
            return string.Join(
                " / ", identifiers.OrderBy(i => i, StringComparer.Ordinal).ToArray());
        }

        private static InvalidOperationException Mismatch(string message)
        {
            return new InvalidOperationException(message);
        }
    }
}
