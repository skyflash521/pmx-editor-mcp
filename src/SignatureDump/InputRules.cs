using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 入力の定義を、ホストが実際に受け取る値まで狭めるための材料。SDKに由来する項目が写す型と、
    /// その型の値の決まりと、ホストが一律の呼び分けで受け取るツールと、合成ツールの数と文字の入力と並びを
    /// ホストがどう読むかを持つ。
    /// </summary>
    public sealed class InputRules
    {
        public InputRules(
            IDictionary<SchemaItem, string> sdkTypes,
            IDictionary<string, IList<string>> enumNames,
            ISet<string> flagEnums,
            IDictionary<string, int> components,
            ISet<string> handledTypes,
            ISet<string> positionedTypes,
            ISet<string> dispatched,
            ComposedReads composedReads,
            ComposedTextReads composedTexts)
        {
            if (sdkTypes == null)
            {
                throw new ArgumentNullException(nameof(sdkTypes));
            }

            if (enumNames == null)
            {
                throw new ArgumentNullException(nameof(enumNames));
            }

            if (flagEnums == null)
            {
                throw new ArgumentNullException(nameof(flagEnums));
            }

            if (components == null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            if (handledTypes == null)
            {
                throw new ArgumentNullException(nameof(handledTypes));
            }

            if (positionedTypes == null)
            {
                throw new ArgumentNullException(nameof(positionedTypes));
            }

            if (dispatched == null)
            {
                throw new ArgumentNullException(nameof(dispatched));
            }

            if (composedReads == null)
            {
                throw new ArgumentNullException(nameof(composedReads));
            }

            if (composedTexts == null)
            {
                throw new ArgumentNullException(nameof(composedTexts));
            }

            SdkTypes = new ReadOnlyDictionary<SchemaItem, string>(sdkTypes);
            EnumNames = new ReadOnlyDictionary<string, IList<string>>(enumNames);
            FlagEnums = new HashSet<string>(flagEnums, StringComparer.Ordinal);
            Components = new ReadOnlyDictionary<string, int>(components);
            HandledTypes = new HashSet<string>(handledTypes, StringComparer.Ordinal);
            PositionedTypes = new HashSet<string>(positionedTypes, StringComparer.Ordinal);
            Dispatched = new HashSet<string>(dispatched, StringComparer.Ordinal);
            ComposedReads = composedReads;
            ComposedTexts = composedTexts;
        }

        /// <summary>SDKに由来する項目から、その項目が写す型の名前へ。</summary>
        public IDictionary<SchemaItem, string> SdkTypes { get; }

        /// <summary>列挙型の名前から、その値の名前へ。</summary>
        public IDictionary<string, IList<string>> EnumNames { get; }

        /// <summary>値を読点区切りで組み合わせられる列挙型の名前。</summary>
        public ISet<string> FlagEnums { get; }

        /// <summary>成分の並びで写す型の名前から、成分の数へ。</summary>
        public IDictionary<string, int> Components { get; }

        /// <summary>値をハンドルの番号で写す型の名前。</summary>
        public ISet<string> HandledTypes { get; }

        /// <summary>値を要素の位置で写す型の名前。</summary>
        public ISet<string> PositionedTypes { get; }

        /// <summary>ホストが一律の呼び分けで受け取るツールの名前。</summary>
        public ISet<string> Dispatched { get; }

        /// <summary>
        /// 合成ツールの数の入力と数の並びを、ホストがどう読むか。入力はツールの名前と、入力の中の位置
        /// (組の項目は点で、並びの要素は [] で区切る)を空白1つで区切って綴る。
        /// </summary>
        public ComposedReads ComposedReads { get; }

        /// <summary>合成ツールの文字の入力と文字の並びを、ホストがどう読むか。綴りは数の入力と同じ。</summary>
        public ComposedTextReads ComposedTexts { get; }
    }
}
