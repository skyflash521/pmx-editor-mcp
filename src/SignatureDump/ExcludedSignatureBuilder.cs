using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

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

        private const string PmdModelTypeName = "PEPlugin.Pmd.IPEPmd";

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

            InventoryAmbiguity.Require(inventory);

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
                    s => !frozen.ContainsKey(s.Key)
                        && ClassifiableTypes(s).SelectMany(Components).Any(streams.Contains));

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

                if (UsesDelegateValue(signature))
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

                if (TakesOrReturnsPmdModel(signature))
                {
                    return ExcludedSignatureRecord.FromCategory(
                        signature.Key, ExclusionCategory.PmdModel, string.Empty);
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

            /// <summary>
            /// イベントはハンドラ型がデリゲートでも購読の仕組みで扱うので、宣言そのものは対象に
            /// しない。値としてデリゲートを受け渡すシグネチャだけを見る。
            /// </summary>
            private bool UsesDelegateValue(SignatureRecord signature)
            {
                if (signature.MemberKind == MemberKind.Event)
                {
                    return signature.Parameters
                        .Where(p => !p.IsTypeArgument)
                        .SelectMany(p => Components(p.TypeName))
                        .Any(delegates.Contains);
                }

                return ClassifiableTypes(signature).SelectMany(Components).Any(delegates.Contains);
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

                string substituted = WithCounterparts(declared[i].TypeName);
                if (substituted == declared[i].TypeName || substituted != candidate[i].TypeName)
                {
                    return false;
                }

                replaced++;
            }

            return replaced != 0;
        }

        /// <summary>
        /// 対応表の組をPMX版へ置き換えた表記。閉じた総称型や配列の内側に現れるPMD型も置き換えるので、
        /// 型を包んで受け渡す形でも同じ位置の対応が取れる。名前の一部が偶然一致するだけの型を
        /// 巻き込まないよう、型名の区切りに挟まれた出現だけを置き換える。
        /// </summary>
        private static string WithCounterparts(string typeName)
        {
            string substituted = typeName;
            foreach (KeyValuePair<string, string> pair in PmxCounterparts)
            {
                substituted = Substitute(substituted, pair.Key, pair.Value);
            }

            return substituted;
        }

        private static string Substitute(string typeName, string from, string to)
        {
            StringBuilder builder = new StringBuilder();
            int index = 0;
            while (index < typeName.Length)
            {
                int found = typeName.IndexOf(from, index, StringComparison.Ordinal);
                if (found < 0)
                {
                    builder.Append(typeName, index, typeName.Length - index);
                    break;
                }

                builder.Append(typeName, index, found - index);
                bool bounded = IsLeftBoundary(typeName, found - 1)
                    && IsRightBoundary(typeName, found + from.Length);
                builder.Append(bounded ? to : from);
                index = found + from.Length;
            }

            return builder.ToString();
        }

        private static bool IsLeftBoundary(string typeName, int index)
        {
            if (index < 0)
            {
                return true;
            }

            char c = typeName[index];

            return c == '<' || c == ',';
        }

        /// <summary>
        /// 右隣の山括弧はその型自身が総称型である印で、包みの区切りではない。対応表が持つのは
        /// 非総称の組なので、総称型を巻き込まないよう境界として認めない。
        /// </summary>
        private static bool IsRightBoundary(string typeName, int index)
        {
            if (index >= typeName.Length)
            {
                return true;
            }

            char c = typeName[index];

            return c == '>' || c == ',' || c == '[' || c == ']' || c == '&';
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
            return signature.Parameters
                .Where(p => !p.IsTypeArgument)
                .SelectMany(p => Components(p.TypeName))
                .Any(t => string.Equals(t, CPluginTypeName, StringComparison.Ordinal));
        }

        /// <summary>
        /// PMDモデル本体は非対応なので、値の表現も、実体を得る提供対象の経路も無い。配列で受け渡す
        /// 形も同じ理由で扱えない。
        /// </summary>
        private static bool TakesOrReturnsPmdModel(SignatureRecord signature)
        {
            return ValueAndParameterTypes(signature)
                .SelectMany(Components)
                .Any(t => string.Equals(t, PmdModelTypeName, StringComparison.Ordinal));
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
                .Select(p => WithoutByReferenceMark(p.TypeName));
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

        /// <summary>
        /// 包みを外した型名と、閉じた総称型の各段の引数を再帰的に集める。総称型そのものの表記は
        /// 列挙が記録する形なので残し、引数の数を落とした定義名は別の型と当たるので加えない。
        /// </summary>
        private static IEnumerable<string> Components(string typeName)
        {
            string name = WithoutArrayMark(WithoutByReferenceMark(typeName));
            List<string> names = new List<string> { name };
            foreach (string argument in TypeDefinitionName.Arguments(name))
            {
                names.AddRange(Components(argument));
            }

            return names;
        }

        /// <summary>要素の型で分類するので、配列の次元は落とす。</summary>
        private static string WithoutArrayMark(string typeName)
        {
            string name = typeName;
            while (name.EndsWith("]", StringComparison.Ordinal))
            {
                int open = name.LastIndexOf('[');
                if (open < 0 || name.Skip(open + 1).Take(name.Length - open - 2).Any(c => c != ','))
                {
                    break;
                }

                name = name.Substring(0, open);
            }

            return name;
        }

        private static string WithoutByReferenceMark(string typeName)
        {
            return typeName.EndsWith("&", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - 1)
                : typeName;
        }
    }
}
