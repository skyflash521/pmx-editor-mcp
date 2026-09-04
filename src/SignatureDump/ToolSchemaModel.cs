using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>項目がどこから来たか。</summary>
    public enum ItemOrigin
    {
        /// <summary>SDKのシグネチャの入力の引数。</summary>
        SdkIn,

        /// <summary>SDKのシグネチャの出力の引数。</summary>
        SdkOut,

        /// <summary>SDKのシグネチャの入出力の引数。</summary>
        SdkRef,

        /// <summary>SDKの戻り値。</summary>
        SdkReturn,

        /// <summary>ホストの側が決める入力。</summary>
        HostInput,

        /// <summary>ホストが組み立てて載せる応答の項目。</summary>
        HostOutput,
    }

    /// <summary>値の取りうる範囲。少なくとも一方を持つ。</summary>
    public sealed class ValueBounds
    {
        public ValueBounds(double? minimum, double? maximum)
        {
            if (!minimum.HasValue && !maximum.HasValue)
            {
                throw new ArgumentException("範囲は下限と上限の少なくとも一方を持つ。", nameof(minimum));
            }

            if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
            {
                throw new ArgumentException("範囲の下限が上限を超えている。", nameof(maximum));
            }

            Minimum = minimum;
            Maximum = maximum;
        }

        public double? Minimum { get; }

        public double? Maximum { get; }
    }

    /// <summary>
    /// 入出力の項目1件。形は綴り・組・要素の3つのうち1つで表し、取らない項目は null で置く。
    /// </summary>
    public sealed class SchemaItem
    {
        public SchemaItem(
            string shape,
            IList<SchemaItem> members,
            SchemaItem element,
            string name,
            ItemOrigin origin,
            bool? required,
            object defaultValue,
            bool hasDefault,
            ValueBounds bounds,
            bool? nullable,
            string source,
            bool injected,
            int? maxItems,
            int? minItems)
        {
            Shape = shape;
            Members = members == null ? null : new ReadOnlyCollection<SchemaItem>(members);
            Element = element;
            Name = name;
            Origin = origin;
            Required = required;
            Default = defaultValue;
            HasDefault = hasDefault;
            Bounds = bounds;
            Nullable = nullable;
            Source = source;
            Injected = injected;
            MaxItems = maxItems;
            MinItems = minItems;
        }

        /// <summary>値の表現の綴り。組と配列では null。</summary>
        public string Shape { get; }

        /// <summary>組の中の項目。組でなければ null。</summary>
        public IList<SchemaItem> Members { get; }

        /// <summary>要素の項目。配列でなければ null。</summary>
        public SchemaItem Element { get; }

        /// <summary>項目の名前。応答の値そのものと配列の要素では null。</summary>
        public string Name { get; }

        public ItemOrigin Origin { get; }

        /// <summary>入力に現れる項目が必須かどうか。まとまりに入る項目では null。</summary>
        public bool? Required { get; }

        /// <summary>省略したときに使う値。持たない項目では null。</summary>
        public object Default { get; }

        /// <summary>既定を持つか。null そのものを既定にできるので、値の有無とは別に持つ。</summary>
        public bool HasDefault { get; }

        /// <summary>値の取りうる範囲。持たない項目では null。</summary>
        public ValueBounds Bounds { get; }

        /// <summary>null を許すか。書かない項目では null。</summary>
        public bool? Nullable { get; }

        /// <summary>SDKに由来する既定か範囲の転記元。持たない項目では null。</summary>
        public string Source { get; }

        /// <summary>ホストが自分で入れる引数か。</summary>
        public bool Injected { get; }

        /// <summary>要素数の上限。配列でなければ null。</summary>
        public int? MaxItems { get; }

        /// <summary>空にできない項目が持つ1。ほかでは null。</summary>
        public int? MinItems { get; }

        /// <summary>この項目と、その内側の項目をすべて並べたもの。</summary>
        public IEnumerable<SchemaItem> WithNested
        {
            get
            {
                yield return this;
                IEnumerable<SchemaItem> inner = (Members ?? new SchemaItem[0])
                    .Concat(Element == null ? new SchemaItem[0] : new[] { Element });
                foreach (SchemaItem item in inner.SelectMany(i => i.WithNested))
                {
                    yield return item;
                }
            }
        }
    }

    /// <summary>同時には持てない項目のまとまり。</summary>
    public sealed class SchemaChoice
    {
        public SchemaChoice(IList<string> names, bool required)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            Names = new ReadOnlyCollection<string>(names);
            Required = required;
        }

        public IList<string> Names { get; }

        /// <summary>まとまりのうち必ず1つが要るか。</summary>
        public bool Required { get; }
    }

    /// <summary>入力の呼び分け1件。</summary>
    public sealed class SchemaBranch
    {
        public SchemaBranch(
            string branch,
            string selectorName,
            object selectorValue,
            IList<SchemaItem> inputs,
            IList<SchemaChoice> choices)
        {
            PropertyRecord.RequireText(branch, nameof(branch));
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (choices == null)
            {
                throw new ArgumentNullException(nameof(choices));
            }

            Branch = branch;
            SelectorName = selectorName;
            SelectorValue = selectorValue;
            Inputs = new ReadOnlyCollection<SchemaItem>(inputs);
            Choices = new ReadOnlyCollection<SchemaChoice>(choices);
        }

        public string Branch { get; }

        /// <summary>分岐を選ぶ項目の名前。値で分かれない分岐では null。</summary>
        public string SelectorName { get; }

        /// <summary>分岐を選ぶ値。値で分かれない分岐では null。</summary>
        public object SelectorValue { get; }

        public IList<SchemaItem> Inputs { get; }

        public IList<SchemaChoice> Choices { get; }
    }

    /// <summary>イベントの取り出しが返す種別の分岐1件。</summary>
    public sealed class SchemaPayload
    {
        public SchemaPayload(string type, IList<SchemaItem> members)
        {
            PropertyRecord.RequireText(type, nameof(type));
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            Type = type;
            Members = new ReadOnlyCollection<SchemaItem>(members);
        }

        public string Type { get; }

        public IList<SchemaItem> Members { get; }
    }

    /// <summary>一覧を返すツールが持つ件数の既定と最大。</summary>
    public sealed class ListingLimits
    {
        public ListingLimits(int limitDefault, int limitMaximum)
        {
            if (limitDefault < 1)
            {
                throw new ArgumentException("件数は1以上でなければならない。", nameof(limitDefault));
            }

            if (limitMaximum < 1)
            {
                throw new ArgumentException("件数は1以上でなければならない。", nameof(limitMaximum));
            }

            if (limitDefault > limitMaximum)
            {
                throw new ArgumentException("件数の既定が最大を超えている。", nameof(limitDefault));
            }

            LimitDefault = limitDefault;
            LimitMaximum = limitMaximum;
        }

        public int LimitDefault { get; }

        public int LimitMaximum { get; }
    }

    /// <summary>スキーマ正本の項目1件。</summary>
    public sealed class ToolSchema
    {
        public ToolSchema(
            string tool,
            IList<SchemaBranch> branches,
            SchemaItem output,
            ListingLimits listing,
            IList<SchemaPayload> payloads)
        {
            PropertyRecord.RequireText(tool, nameof(tool));
            if (branches == null)
            {
                throw new ArgumentNullException(nameof(branches));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            Tool = tool;
            Branches = new ReadOnlyCollection<SchemaBranch>(branches);
            Output = output;
            Listing = listing;
            Payloads = payloads == null ? null : new ReadOnlyCollection<SchemaPayload>(payloads);
        }

        public string Tool { get; }

        public IList<SchemaBranch> Branches { get; }

        public SchemaItem Output { get; }

        /// <summary>一覧を返すツールだけが持つ。</summary>
        public ListingLimits Listing { get; }

        /// <summary>イベントの取り出しだけが持つ。</summary>
        public IList<SchemaPayload> Payloads { get; }

        /// <summary>このツールが持つ項目を、入力も応答も内側もすべて並べたもの。</summary>
        public IEnumerable<SchemaItem> AllItems
        {
            get
            {
                return Branches.SelectMany(b => b.Inputs)
                    .Concat(new[] { Output })
                    .Concat(Payloads == null
                        ? new SchemaItem[0]
                        : Payloads.SelectMany(p => p.Members))
                    .SelectMany(i => i.WithNested);
            }
        }
    }

    /// <summary>スキーマ正本。</summary>
    public sealed class ToolSchemaTable
    {
        public ToolSchemaTable(IList<ToolSchema> tools)
        {
            if (tools == null)
            {
                throw new ArgumentNullException(nameof(tools));
            }

            Tools = new ReadOnlyCollection<ToolSchema>(tools);
        }

        public IList<ToolSchema> Tools { get; }
    }
}
