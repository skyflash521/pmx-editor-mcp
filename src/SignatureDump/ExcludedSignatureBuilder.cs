using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 提供対象から除く公開シグネチャを一元に決める。生成側も対応表側もこの一覧だけを見るので、
    /// 除外の判断が二重にならない。
    /// </summary>
    public static class ExcludedSignatureBuilder
    {
        private const string StreamTypeName = "System.IO.Stream";

        private const string CPluginTypeName = "PXCPlugin.IPXCPlugin";

        private const string PmdNamespacePart = ".Pmd.";

        /// <summary>PMD型に対応するPMX型。一次資料で対応を確かめた組だけを持つ。</summary>
        private static readonly Dictionary<string, string> PmxCounterparts =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "PEPlugin.Pmd.IPEPmd", "PEPlugin.Pmx.IPXPmx" },
            };

        /// <summary>
        /// 除外を行キーの昇順で返す。ベースライン正本に無いStreamシグネチャを見つけたときと、
        /// ベースライン正本の行キーが列挙と食い違うときは <see cref="InvalidOperationException"/>。
        /// </summary>
        public static IList<ExcludedSignatureRecord> Build(
            IList<ExcludedBaselineEntry> baseline, InventoryRecord inventory)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(nameof(baseline));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            Resolver resolver = new Resolver(inventory, Freeze(baseline, inventory));
            resolver.RequireStreamsFrozen();

            List<ExcludedSignatureRecord> records = new List<ExcludedSignatureRecord>();
            foreach (SignatureRecord signature in inventory.Signatures)
            {
                ExcludedSignatureRecord record = resolver.Resolve(signature);
                if (record != null)
                {
                    records.Add(record);
                }
            }

            return new ReadOnlyCollection<ExcludedSignatureRecord>(
                records.OrderBy(r => r.Key, StringComparer.Ordinal).ToList());
        }

        /// <summary>行キーから能力IDを引ける形へ直す。</summary>
        private static Dictionary<string, string> Freeze(
            IList<ExcludedBaselineEntry> baseline, InventoryRecord inventory)
        {
            HashSet<string> keys = new HashSet<string>(
                inventory.Signatures.Select(s => s.Key), StringComparer.Ordinal);
            Dictionary<string, string> frozen = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (ExcludedBaselineEntry entry in baseline)
            {
                foreach (string key in entry.Signatures)
                {
                    if (!keys.Contains(key))
                    {
                        throw new InvalidOperationException(
                            "ベースライン正本の行キーが列挙に無い: " + key + "(" + entry.CapabilityId + ")");
                    }

                    if (frozen.ContainsKey(key))
                    {
                        throw new InvalidOperationException(
                            "ベースライン正本が同じ行キーを二重に持つ: " + key);
                    }

                    frozen.Add(key, entry.CapabilityId);
                }
            }

            return frozen;
        }

        /// <summary>1つの列挙とベースライン正本の組について、除外を判定する。</summary>
        private sealed class Resolver
        {
            private readonly InventoryRecord inventory;

            private readonly Dictionary<string, string> frozen;

            private readonly HashSet<string> delegates;

            private readonly HashSet<string> streams;

            private readonly Dictionary<string, ExcludedSignatureRecord> resolved =
                new Dictionary<string, ExcludedSignatureRecord>(StringComparer.Ordinal);

            private readonly HashSet<string> deciding = new HashSet<string>(StringComparer.Ordinal);

            public Resolver(InventoryRecord inventory, Dictionary<string, string> frozen)
            {
                this.inventory = inventory;
                this.frozen = frozen;

                IEnumerable<TypeRecord> types = inventory.Types.Concat(inventory.ReferencedTypes);
                delegates = new HashSet<string>(
                    types.Where(t => t.Kind == TypeKind.Delegate).Select(t => t.Name),
                    StringComparer.Ordinal);
                streams = new HashSet<string>(
                    types.Where(t => t.BaseTypes.Contains(StreamTypeName, StringComparer.Ordinal))
                        .Select(t => t.Name),
                    StringComparer.Ordinal)
                { StreamTypeName };
            }

            /// <summary>
            /// 形式が同じかどうかは一次資料でしか決まらないので、Streamを扱うシグネチャを除外するか
            /// 残すかは機械で決められない。ベースライン正本に無いものが在れば止める。
            /// </summary>
            public void RequireStreamsFrozen()
            {
                SignatureRecord found = inventory.Signatures.FirstOrDefault(
                    s => !frozen.ContainsKey(s.Key) && ClassifiableTypes(s).Any(streams.Contains));

                if (found != null)
                {
                    throw new InvalidOperationException(
                        "ベースライン正本に無いStreamシグネチャを見つけた: " + found.Key);
                }
            }

            /// <summary>除外しないシグネチャでは null を返す。</summary>
            public ExcludedSignatureRecord Resolve(SignatureRecord signature)
            {
                ExcludedSignatureRecord record;
                if (resolved.TryGetValue(signature.Key, out record))
                {
                    return record;
                }

                if (!deciding.Add(signature.Key))
                {
                    throw new InvalidOperationException(
                        "除外の判定が互いを根拠にしている: " + signature.Key);
                }

                try
                {
                    record = Decide(signature);
                    resolved.Add(signature.Key, record);
                    return record;
                }
                finally
                {
                    deciding.Remove(signature.Key);
                }
            }

            private ExcludedSignatureRecord Decide(SignatureRecord signature)
            {
                string capabilityId;
                if (frozen.TryGetValue(signature.Key, out capabilityId))
                {
                    return ExcludedSignatureRecord.FromBaseline(signature.Key, capabilityId);
                }

                if (TakesCPlugin(signature))
                {
                    return ExcludedSignatureRecord.FromCategory(
                        signature.Key, ExclusionCategory.CPluginArgument, string.Empty);
                }

                if (TakesDelegate(signature))
                {
                    return ExcludedSignatureRecord.FromCategory(
                        signature.Key, ExclusionCategory.Delegate, string.Empty);
                }

                if (TouchesPmd(signature))
                {
                    string alternative = FindPmxAlternative(signature);
                    if (alternative != null)
                    {
                        return ExcludedSignatureRecord.FromCategory(
                            signature.Key, ExclusionCategory.Pmd, alternative);
                    }
                }

                if (signature.MemberKind == MemberKind.Constructor)
                {
                    string factory = FindFactory(signature);
                    if (factory != null)
                    {
                        return ExcludedSignatureRecord.FromCategory(
                            signature.Key, ExclusionCategory.ConstructorDuplicate, factory);
                    }
                }

                return null;
            }

            /// <summary>PMD版に対応するPMX版を探す。</summary>
            private string FindPmxAlternative(SignatureRecord signature)
            {
                foreach (SignatureRecord candidate in inventory.Signatures)
                {
                    if (candidate.Key == signature.Key
                        || candidate.DeclaringType != signature.DeclaringType
                        || candidate.MemberName != signature.MemberName
                        || candidate.GenericArity != signature.GenericArity
                        || candidate.ValueType != signature.ValueType
                        || candidate.ValueTypeIsTypeArgument != signature.ValueTypeIsTypeArgument
                        || candidate.Parameters.Count != signature.Parameters.Count
                        || !MatchesByCounterpart(signature.Parameters, candidate.Parameters)
                        || Resolve(candidate) != null)
                    {
                        continue;
                    }

                    return candidate.Key;
                }

                return null;
            }

            /// <summary>公開コンストラクタの代わりになる生成メンバーを探す。</summary>
            private string FindFactory(SignatureRecord constructor)
            {
                SignatureRecord found = inventory.Signatures.FirstOrDefault(
                    s => IsFactoryShape(s, constructor.DeclaringType) && Resolve(s) == null);

                return found == null ? null : found.Key;
            }

            private bool TakesDelegate(SignatureRecord signature)
            {
                return signature.Parameters.Any(p => !p.IsTypeArgument && delegates.Contains(p.TypeName));
            }
        }

        private static bool MatchesByCounterpart(
            IList<ParameterRecord> declared, IList<ParameterRecord> candidate)
        {
            int replaced = 0;
            for (int i = 0; i < declared.Count; i++)
            {
                if (declared[i].Direction != candidate[i].Direction)
                {
                    return false;
                }

                if (declared[i].IsTypeArgument != candidate[i].IsTypeArgument)
                {
                    return false;
                }

                if (declared[i].TypeName == candidate[i].TypeName)
                {
                    continue;
                }

                string counterpart;
                if (!PmxCounterparts.TryGetValue(declared[i].TypeName, out counterpart)
                    || counterpart != candidate[i].TypeName)
                {
                    return false;
                }

                replaced++;
            }

            return replaced != 0;
        }

        private static bool IsFactoryShape(SignatureRecord signature, string createdType)
        {
            return signature.MemberKind == MemberKind.Method
                && !signature.ValueTypeIsTypeArgument
                && signature.ValueType == createdType
                && signature.DeclaringType != createdType;
        }

        private static bool TakesCPlugin(SignatureRecord signature)
        {
            return signature.Parameters.Any(p => !p.IsTypeArgument && p.TypeName == CPluginTypeName);
        }

        private static bool TouchesPmd(SignatureRecord signature)
        {
            return ValueAndParameterTypes(signature)
                .Any(t => t.IndexOf(PmdNamespacePart, StringComparison.Ordinal) >= 0);
        }

        /// <summary>
        /// 総称型引数は宣言ごとに別の型で、分類を持たない。表記が名前空間を持たない型と重なるので、
        /// 分類を引く前に取り除く。
        /// </summary>
        private static IEnumerable<string> ClassifiableTypes(SignatureRecord signature)
        {
            IEnumerable<string> parameters = signature.Parameters
                .Where(p => !p.IsTypeArgument)
                .Select(p => p.TypeName);
            IEnumerable<string> value = signature.ValueTypeIsTypeArgument
                ? new string[0]
                : new[] { WithoutByReferenceMark(signature.ValueType) };

            return parameters.Concat(value);
        }

        private static IEnumerable<string> ValueAndParameterTypes(SignatureRecord signature)
        {
            return signature.Parameters.Select(p => p.TypeName)
                .Concat(new[] { signature.ValueType })
                .Select(WithoutByReferenceMark);
        }

        private static string WithoutByReferenceMark(string typeName)
        {
            return typeName.EndsWith("&", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - 1)
                : typeName;
        }
    }
}
