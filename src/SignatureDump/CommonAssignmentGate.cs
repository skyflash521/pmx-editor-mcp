using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 共通契約割当が規則どおりかを検査する。割当の意味そのものは測れないので、機械で確かめられる
    /// 範囲——行キーが提供対象に実在すること、常駐アクセスオブジェクトの取得と解放・破棄が漏れなく
    /// 表に在ること、束縛が列挙から導いたものと一致すること、束縛したスロットがその割当で使える
    /// ものであること、解放がツールへの束縛で対象名が揃っていること——に限る。
    /// </summary>
    public static class CommonAssignmentGate
    {
        private const string ConnectFlow = "connect";

        /// <summary>どの行でも使えるスロット。</summary>
        private static readonly BindingSlot[] Shared =
        {
            BindingSlot.InjectedConnector, BindingSlot.OwningObject,
        };

        /// <summary>ハンドル解放の流れで使えるスロット。この流れはツールが受け持つ。</summary>
        private static readonly BindingSlot[] ReleaseSlots = { BindingSlot.TargetHandle };

        /// <summary>内部フローごとに使えるスロット。</summary>
        private static readonly Dictionary<string, BindingSlot[]> FlowSlots =
            new Dictionary<string, BindingSlot[]>(StringComparer.Ordinal)
            {
                {
                    "duplicateEdit",
                    new[]
                    {
                        BindingSlot.PmxClone, BindingSlot.UpdateKind, BindingSlot.UpdateIndices,
                        BindingSlot.UndoLock,
                    }
                },
                { "stateRead", new[] { BindingSlot.PmxClone } },
                {
                    ConnectFlow,
                    new[]
                    {
                        BindingSlot.RunArgsClone, BindingSlot.ModulePath,
                        BindingSlot.ResidentObject,
                    }
                },
            };

        /// <summary>
        /// 規則に反していれば <see cref="InvalidOperationException"/>。<paramref name="provided"/> は
        /// 提供対象のシグネチャの行キー、<paramref name="residentObjects"/> には
        /// <see cref="CommonAssignmentEvidence.ResidentObjectSignatures"/> の結果を、
        /// <paramref name="releases"/> には
        /// <see cref="CommonAssignmentEvidence.ReleaseSignatures"/> の結果を、
        /// <paramref name="bindings"/> には <see cref="CommonAssignmentEvidence.Bindings"/> の結果を
        /// 渡すこと。
        /// </summary>
        public static void Require(
            CommonAssignmentTable table,
            ISet<string> provided,
            ISet<string> residentObjects,
            ISet<string> releases,
            IDictionary<string, SlotBinding> bindings)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (provided == null)
            {
                throw new ArgumentNullException(nameof(provided));
            }

            if (residentObjects == null)
            {
                throw new ArgumentNullException(nameof(residentObjects));
            }

            if (releases == null)
            {
                throw new ArgumentNullException(nameof(releases));
            }

            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            RequireSignaturesAreProvided(table.Assignments, provided);
            RequireResidentObjectsAreConnectFlows(table.Assignments, residentObjects);
            RequireReleasesAreListed(table.Assignments, releases);
            RequireBindingsMatchTheEvidence(table.Assignments, bindings);
            RequireSlotsBelongToTheAssignment(table.Assignments);
            RequireOneToolForTheReleases(table.Assignments, releases);
        }

        private static void RequireSignaturesAreProvided(
            IList<CommonAssignmentRecord> records, ISet<string> provided)
        {
            foreach (CommonAssignmentRecord record in records)
            {
                if (!provided.Contains(record.SignatureKey))
                {
                    throw new InvalidOperationException(
                        "提供対象に無い行キーが在る: " + record.SignatureKey);
                }
            }
        }

        /// <summary>
        /// 常駐アクセスオブジェクトの取得は独立したツールを作らないので、1件でも表から漏れると
        /// そのシグネチャの割当先が決まらない。
        /// </summary>
        private static void RequireResidentObjectsAreConnectFlows(
            IList<CommonAssignmentRecord> records, ISet<string> residentObjects)
        {
            Dictionary<string, CommonAssignmentRecord> listed =
                records.ToDictionary(r => r.SignatureKey, StringComparer.Ordinal);
            foreach (string key in residentObjects.OrderBy(k => k, StringComparer.Ordinal))
            {
                CommonAssignmentRecord record;
                if (!listed.TryGetValue(key, out record))
                {
                    throw new InvalidOperationException(
                        "常駐アクセスオブジェクトを返すのに表に無い: " + key);
                }

                if (record.Assignment != CommonAssignmentKind.InternalFlow
                    || !string.Equals(record.Target, ConnectFlow, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "常駐アクセスオブジェクトを返すのに接続初期化でない: " + key
                            + "(表: " + record.Assignment + " " + record.Target + ")");
                }
            }
        }

        /// <summary>
        /// 解放・破棄も独立したツールを作らないので、1件でも表から漏れるとそのシグネチャの割当先が
        /// 決まらない。
        /// </summary>
        private static void RequireReleasesAreListed(
            IList<CommonAssignmentRecord> records, ISet<string> releases)
        {
            HashSet<string> listed = new HashSet<string>(
                records.Select(r => r.SignatureKey), StringComparer.Ordinal);
            foreach (string key in releases.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!listed.Contains(key))
                {
                    throw new InvalidOperationException("解放・破棄なのに表に無い: " + key);
                }
            }
        }

        /// <summary>
        /// 割当ごとに使えるスロットを限る。フローの取り違えは、そのフローに無いスロットを束縛して
        /// いることで表に出る。
        /// </summary>
        private static void RequireSlotsBelongToTheAssignment(IList<CommonAssignmentRecord> records)
        {
            foreach (CommonAssignmentRecord record in records)
            {
                BindingSlot[] flow;
                if (record.Assignment == CommonAssignmentKind.InternalFlow)
                {
                    if (!FlowSlots.TryGetValue(record.Target, out flow))
                    {
                        throw new InvalidOperationException(
                            "内部フローへの割当の対象名でない: " + record.SignatureKey
                                + "(表: " + record.Target + ")");
                    }
                }
                else if (record.Assignment == CommonAssignmentKind.Tool)
                {
                    flow = ReleaseSlots;
                }
                else
                {
                    flow = new BindingSlot[0];
                }

                foreach (BindingSlot slot in Slots(record.SlotBinding))
                {
                    if (!Shared.Contains(slot) && !flow.Contains(slot))
                    {
                        throw new InvalidOperationException(
                            "その割当で使えないスロットを束縛している: " + record.SignatureKey
                                + "(" + record.Assignment + " " + record.Target + " / " + slot + ")");
                    }
                }
            }
        }

        /// <summary>
        /// 解放・破棄と、解放の対象を束縛する行は、ツールが受け持つ。ホストが自分で呼ぶ流れへ
        /// 書き替えると、台帳の失効と切り離された解放になる。対象名は書き手が書く語なので、綴りの
        /// 揺れを一つにそろえる。
        /// </summary>
        private static void RequireOneToolForTheReleases(
            IList<CommonAssignmentRecord> records, ISet<string> releases)
        {
            string target = null;
            foreach (CommonAssignmentRecord record in records
                .Where(r => releases.Contains(r.SignatureKey)
                    || Slots(r.SlotBinding).Contains(BindingSlot.TargetHandle)))
            {
                if (record.Assignment != CommonAssignmentKind.Tool)
                {
                    throw new InvalidOperationException(
                        "解放なのにツールへの束縛でない: " + record.SignatureKey
                            + "(表: " + record.Assignment + ")");
                }

                if (target == null)
                {
                    target = record.Target;
                }
                else if (!string.Equals(target, record.Target, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "解放の行の対象名が揃っていない: " + record.SignatureKey
                            + "(表: " + record.Target + " / ほかの行: " + target + ")");
                }
            }
        }

        private static IEnumerable<BindingSlot> Slots(SlotBinding binding)
        {
            List<BindingSlot> slots = new List<BindingSlot>(binding.Parameters.Values);
            if (binding.Returned.HasValue)
            {
                slots.Add(binding.Returned.Value);
            }

            if (binding.Receiver.HasValue)
            {
                slots.Add(binding.Receiver.Value);
            }

            return slots;
        }

        private static void RequireBindingsMatchTheEvidence(
            IList<CommonAssignmentRecord> records, IDictionary<string, SlotBinding> bindings)
        {
            foreach (CommonAssignmentRecord record in records)
            {
                SlotBinding expected;
                if (!bindings.TryGetValue(record.SignatureKey, out expected))
                {
                    throw new InvalidOperationException(
                        "束縛を導けない行キーが在る: " + record.SignatureKey);
                }

                if (!record.SlotBinding.SameAs(expected))
                {
                    throw new InvalidOperationException(
                        "束縛が列挙から決まるものと合わない: " + record.SignatureKey
                            + "(表: " + record.SlotBinding + " / 決まる束縛: " + expected + ")");
                }
            }
        }
    }
}
