using System;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>除外を許す根拠の種類。</summary>
    public enum ExclusionQualification
    {
        /// <summary>ベースライン正本が凍結した組に含まれる。</summary>
        Baseline,

        /// <summary>確定した除外カテゴリの述語を満たす。</summary>
        Category,
    }

    /// <summary>
    /// 述語で機械検査する除外カテゴリ。恣意的な除外を入れられないよう、ここに無い理由では除外
    /// できない。
    /// </summary>
    public enum ExclusionCategory
    {
        /// <summary>カテゴリを根拠にしない。</summary>
        None,

        /// <summary>PMDデータ型を扱い、同じ操作のPMX版が提供対象に在る。</summary>
        Pmd,

        /// <summary>Cプラグイン実装本体のインターフェースを引数に取る。</summary>
        CPluginArgument,

        /// <summary>デリゲート型を引数に取る。</summary>
        Delegate,

        /// <summary>同じ型を返す生成メンバーが提供対象に在る公開コンストラクタ。</summary>
        ConstructorDuplicate,
    }

    /// <summary>提供対象から除く公開シグネチャ1件。根拠ごとのファクトリメソッドで作る。</summary>
    public sealed class ExcludedSignatureRecord
    {
        private ExcludedSignatureRecord(
            string key,
            ExclusionQualification qualification,
            string capabilityId,
            ExclusionCategory category,
            string alternative)
        {
            Key = key;
            Qualification = qualification;
            CapabilityId = capabilityId;
            Category = category;
            Alternative = alternative;
        }

        public string Key { get; }

        public ExclusionQualification Qualification { get; }

        /// <summary>ベースライン正本の能力ID。カテゴリを根拠にするときは空。</summary>
        public string CapabilityId { get; }

        /// <summary>ベースライン正本を根拠にするときは <see cref="ExclusionCategory.None"/>。</summary>
        public ExclusionCategory Category { get; }

        /// <summary>代替になる提供対象のシグネチャ。求めないカテゴリと資格では空。</summary>
        public string Alternative { get; }

        /// <summary>ベースライン正本が凍結した組を根拠とする1件を作る。</summary>
        public static ExcludedSignatureRecord FromBaseline(string key, string capabilityId)
        {
            RequireText(key, nameof(key));
            RequireText(capabilityId, nameof(capabilityId));

            return new ExcludedSignatureRecord(
                key, ExclusionQualification.Baseline, capabilityId, ExclusionCategory.None, string.Empty);
        }

        /// <summary>
        /// カテゴリの述語を根拠とする1件を作る。代替の存在を除外の条件とするカテゴリでは
        /// <paramref name="alternative"/> が必須で、条件としないカテゴリでは空でなければならない。
        /// </summary>
        public static ExcludedSignatureRecord FromCategory(
            string key, ExclusionCategory category, string alternative)
        {
            RequireText(key, nameof(key));
            if (alternative == null)
            {
                throw new ArgumentNullException(nameof(alternative));
            }

            if (alternative.Length != 0 && alternative.Trim().Length == 0)
            {
                throw new ArgumentException("空白だけにできない。", nameof(alternative));
            }

            bool hasAlternative = alternative.Length != 0;
            if (RequiresAlternative(category) != hasAlternative)
            {
                throw new ArgumentException(
                    "カテゴリ " + category + " と代替の有無が噛み合わない。", nameof(alternative));
            }

            if (hasAlternative && alternative == key)
            {
                throw new ArgumentException("除外するシグネチャ自身は代替にならない。", nameof(alternative));
            }

            return new ExcludedSignatureRecord(
                key, ExclusionQualification.Category, string.Empty, category, alternative);
        }

        /// <summary>
        /// 代替の存在を除外の条件とするカテゴリなら true。除外の根拠にできないカテゴリでは例外にする。
        /// </summary>
        private static bool RequiresAlternative(ExclusionCategory category)
        {
            switch (category)
            {
                case ExclusionCategory.Pmd:
                case ExclusionCategory.ConstructorDuplicate:
                    return true;
                case ExclusionCategory.CPluginArgument:
                case ExclusionCategory.Delegate:
                    return false;
                default:
                    throw new ArgumentException(
                        "除外の根拠にできないカテゴリ: " + category, nameof(category));
            }
        }

        private static void RequireText(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", name);
            }
        }
    }
}
