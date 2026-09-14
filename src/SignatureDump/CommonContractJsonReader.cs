using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    public sealed class ValueSpellingRow
    {
        public ValueSpellingRow(string spelling, int assumedChars)
        {
            PropertyRecord.RequireText(spelling, nameof(spelling));
            Spelling = spelling;
            AssumedChars = assumedChars;
        }

        public string Spelling { get; }

        public int AssumedChars { get; }
    }

    public sealed class ValueShapeRow
    {
        public ValueShapeRow(string typeName, string shape)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            TypeName = typeName;
            Shape = shape;
        }

        public string TypeName { get; }

        /// <summary>要素の表現を包む型では綴りが1つに決まらないので null。</summary>
        public string Shape { get; }
    }

    public sealed class ComposedTool
    {
        public ComposedTool(bool branching, string duty)
        {
            PropertyRecord.RequireText(duty, nameof(duty));
            Branching = branching;
            Duty = duty;
        }

        /// <summary>入出力の形がイベントの分岐を持つかどうか。</summary>
        public bool Branching { get; }

        /// <summary>受け持つこと。説明文の先頭の1行になる。</summary>
        public string Duty { get; }
    }

    /// <summary>要求と応答の大きさに置く取り決め。</summary>
    public sealed class SizeBudgets
    {
        public SizeBudgets(
            int responseDefaultChars,
            int warningRoomChars,
            int requestBytes,
            int structureTokenLimit)
        {
            ResponseDefaultChars = responseDefaultChars;
            WarningRoomChars = warningRoomChars;
            RequestBytes = requestBytes;
            StructureTokenLimit = structureTokenLimit;
        }

        public int ResponseDefaultChars { get; }

        public int WarningRoomChars { get; }

        public int RequestBytes { get; }

        public int StructureTokenLimit { get; }
    }

    /// <summary>ツールの入出力に共通して掛かる決めごとの正本。</summary>
    public sealed class CommonContractTable
    {
        public CommonContractTable(
            IList<ValueSpellingRow> spellings,
            IList<ValueShapeRow> types,
            IDictionary<string, int> components,
            IDictionary<string, ComposedTool> composedTools,
            IDictionary<string, string> viewImages,
            IDictionary<string, ISet<string>> unkeptMembers,
            SizeBudgets budgets)
        {
            Spellings = new ReadOnlyCollection<ValueSpellingRow>(spellings);
            Types = new ReadOnlyCollection<ValueShapeRow>(types);
            Components = new ReadOnlyDictionary<string, int>(components);
            ComposedTools = new ReadOnlyDictionary<string, ComposedTool>(composedTools);
            ViewImages = new ReadOnlyDictionary<string, string>(viewImages);
            UnkeptMembers = new ReadOnlyDictionary<string, ISet<string>>(unkeptMembers);
            Budgets = budgets;
        }

        public IList<ValueSpellingRow> Spellings { get; }

        public IList<ValueShapeRow> Types { get; }

        /// <summary>数値の配列で写す型の、成分の数。</summary>
        public IDictionary<string, int> Components { get; }

        public IDictionary<string, ComposedTool> ComposedTools { get; }

        /// <summary>
        /// ビューの画像を返すツールの名前から、そのビューの名前へ。画像がどのビューのものかは
        /// 呼び先の型からは決まらないので、ここが決める。
        /// </summary>
        public IDictionary<string, string> ViewImages { get; }

        /// <summary>
        /// 値を書き換えるツールの名前から、書いてもモデルが持ち続けない項目の名前へ。持ち主の
        /// 状態によって捨てられる項目がどれかは、呼び先の型からは決まらないのでここが決める。
        /// </summary>
        public IDictionary<string, ISet<string>> UnkeptMembers { get; }

        public SizeBudgets Budgets { get; }

        /// <summary>綴りの閉じた集合。綴りを名乗る値はここに実在するかで確かめる。</summary>
        public ISet<string> SpellingNames()
        {
            return new HashSet<string>(Spellings.Select(s => s.Spelling), StringComparer.Ordinal);
        }

        /// <summary>綴りから想定の文字数を引く表。</summary>
        public IDictionary<string, int> AssumedChars()
        {
            return Spellings.ToDictionary(
                s => s.Spelling, s => s.AssumedChars, StringComparer.Ordinal);
        }
    }

    public static class CommonContractJsonReader
    {
        private const string SpellingsName = "spellings";

        private const string TypesName = "types";

        private const string ComponentsName = "components";

        private const string ComposedToolsName = "composedTools";

        private const string ViewImagesName = "viewImages";

        private const string ViewName = "view";

        private const string UnkeptMembersName = "unkeptMembers";

        private const string MembersName = "members";

        private const string BasisName = "basis";

        private const string BudgetsName = "budgets";

        private const string SpellingName = "spelling";

        private const string AssumedCharsName = "assumedChars";

        private const string TypeNameName = "typeName";

        private const string ShapeName = "shape";

        private const string CountName = "count";

        private const string ToolName = "tool";

        private const string BranchingName = "branching";

        private const string DutyName = "duty";

        private const string ResponseDefaultCharsName = "responseDefaultChars";

        private const string WarningRoomCharsName = "warningRoomChars";

        private const string RequestBytesName = "requestBytes";

        private const string StructureTokenLimitName = "structureTokenLimit";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(SpellingsName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(SpellingName, JsonForm.Text()),
                    JsonForm.Member(AssumedCharsName, JsonForm.Count())),
                SpellingName)),
            JsonForm.Member(TypesName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(TypeNameName, JsonForm.Text()),
                    JsonForm.Member(ShapeName, JsonForm.OrNull(JsonForm.Text()))),
                TypeNameName)),
            JsonForm.Member(ComponentsName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(TypeNameName, JsonForm.Text()),
                    JsonForm.Member(CountName, JsonForm.Count())),
                TypeNameName)),
            JsonForm.Member(ComposedToolsName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(ToolName, JsonForm.Text()),
                    JsonForm.Member(BranchingName, JsonForm.Flag()),
                    JsonForm.Member(DutyName, JsonForm.Text())),
                ToolName)),
            JsonForm.Member(ViewImagesName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(ToolName, JsonForm.Text()),
                    JsonForm.Member(ViewName, JsonForm.Text())),
                ToolName,
                allowEmpty: true)),
            JsonForm.Member(UnkeptMembersName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(ToolName, JsonForm.Text()),
                    JsonForm.Member(MembersName, JsonForm.Array(JsonForm.Text())),
                    JsonForm.Member(BasisName, JsonForm.Text())),
                ToolName,
                allowEmpty: true)),
            JsonForm.Member(BudgetsName, JsonForm.Object(
                JsonForm.Member(ResponseDefaultCharsName, JsonForm.Count()),
                JsonForm.Member(WarningRoomCharsName, JsonForm.Count()),
                JsonForm.Member(RequestBytesName, JsonForm.Count()),
                JsonForm.Member(StructureTokenLimitName, JsonForm.Count()))));

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static CommonContractTable Read(string json)
        {
            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            IDictionary<string, object> budgets =
                (IDictionary<string, object>)root[BudgetsName];

            return new CommonContractTable(
                Rows(root, SpellingsName)
                    .Select(r => new ValueSpellingRow(
                        (string)r[SpellingName], (int)r[AssumedCharsName]))
                    .ToList(),
                Rows(root, TypesName)
                    .Select(r => new ValueShapeRow(
                        (string)r[TypeNameName], (string)r[ShapeName]))
                    .ToList(),
                Rows(root, ComponentsName).ToDictionary(
                    r => (string)r[TypeNameName],
                    r => (int)r[CountName],
                    StringComparer.Ordinal),
                Rows(root, ComposedToolsName).ToDictionary(
                    r => (string)r[ToolName],
                    r => new ComposedTool((bool)r[BranchingName], (string)r[DutyName]),
                    StringComparer.Ordinal),
                Rows(root, ViewImagesName).ToDictionary(
                    r => (string)r[ToolName],
                    r => (string)r[ViewName],
                    StringComparer.Ordinal),
                Rows(root, UnkeptMembersName).ToDictionary(
                    r => (string)r[ToolName],
                    r => (ISet<string>)new HashSet<string>(
                        ((IList<object>)r[MembersName]).Cast<string>(), StringComparer.Ordinal),
                    StringComparer.Ordinal),
                new SizeBudgets(
                    (int)budgets[ResponseDefaultCharsName],
                    (int)budgets[WarningRoomCharsName],
                    (int)budgets[RequestBytesName],
                    (int)budgets[StructureTokenLimitName]));
        }

        private static IEnumerable<IDictionary<string, object>> Rows(
            IDictionary<string, object> root, string name)
        {
            return ((object[])root[name]).Cast<IDictionary<string, object>>();
        }
    }
}
