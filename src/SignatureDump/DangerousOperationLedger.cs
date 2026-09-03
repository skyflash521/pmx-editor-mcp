using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力台帳の備考から、危険操作に当たると記した箇所を読む。備考は人が書く文なので、決まった形の
    /// 断りだけを拾い、それ以外は読まない。
    /// </summary>
    public static class DangerousOperationLedger
    {
        private static readonly Regex Noted = new Regex(
            @"危険操作\(([^)]+)\)。該当は ([^。]+)。", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, DangerKind> Kinds =
            new Dictionary<string, DangerKind>(StringComparer.Ordinal)
            {
                { "エディタ終了", DangerKind.Shutdown },
                { "上書き保存", DangerKind.Overwrite },
                { "モデル初期化", DangerKind.Reset },
            };

        /// <summary>
        /// 記した箇所を行キーから種別へ引く形で返す。種別の名前が知らないものだったときと、
        /// 該当のシグネチャがその能力に無かったときは例外。
        /// </summary>
        public static IDictionary<string, DangerKind> Read(
            IList<CapabilityRecord> ledger, LedgerPopulation population, InventoryRecord inventory)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (population == null)
            {
                throw new ArgumentNullException(nameof(population));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            Dictionary<string, SignatureRecord> byKey = inventory.Signatures.ToDictionary(
                s => s.Key, StringComparer.Ordinal);
            Dictionary<string, DangerKind> noted =
                new Dictionary<string, DangerKind>(StringComparer.Ordinal);

            foreach (CapabilityRecord capability in ledger)
            {
                if (capability.Remarks == null)
                {
                    continue;
                }

                foreach (Match match in Noted.Matches(capability.Remarks))
                {
                    DangerKind kind = Kind(capability, match.Groups[1].Value);
                    noted.Add(Resolve(capability, match.Groups[2].Value, population, byKey), kind);
                }
            }

            return noted;
        }

        private static DangerKind Kind(CapabilityRecord capability, string name)
        {
            DangerKind kind;
            if (!Kinds.TryGetValue(name, out kind))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} の備考が知らない種別を記している: {1}",
                    capability.Id,
                    name));
            }

            return kind;
        }

        /// <summary>該当の綴りを、その能力が受け持つシグネチャの行キーへ直す。</summary>
        private static string Resolve(
            CapabilityRecord capability,
            string spelled,
            LedgerPopulation population,
            IDictionary<string, SignatureRecord> byKey)
        {
            string[] found = population.Owners
                .Where(o => o.Value.Contains(capability.Id))
                .Select(o => o.Key)
                .Where(k => string.Equals(Spelling(byKey, k), spelled, StringComparison.Ordinal))
                .ToArray();

            if (found.Length != 1)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} の備考が指す該当を1つに決められない: {1}(見つかった数 {2})",
                    capability.Id,
                    spelled,
                    found.Length));
            }

            return found[0];
        }

        private static string Spelling(IDictionary<string, SignatureRecord> byKey, string key)
        {
            SignatureRecord signature;

            return byKey.TryGetValue(key, out signature)
                ? key.Substring(signature.DeclaringType.Length + 1)
                : key;
        }
    }
}
