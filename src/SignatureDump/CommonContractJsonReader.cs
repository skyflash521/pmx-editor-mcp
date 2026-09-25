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

    /// <summary>要素を親の並びへ加える前に、親へ揃える値。</summary>
    public sealed class ParentValues
    {
        public ParentValues(string parentTool, string member, string value)
        {
            PropertyRecord.RequireText(parentTool, nameof(parentTool));
            PropertyRecord.RequireText(member, nameof(member));
            PropertyRecord.RequireText(value, nameof(value));

            ParentTool = parentTool;
            Member = member;
            Value = value;
        }

        /// <summary>親を書き換えるツールの名前。</summary>
        public string ParentTool { get; }

        /// <summary>親へ揃える項目の名前。</summary>
        public string Member { get; }

        /// <summary>その項目へ入れる値。</summary>
        public string Value { get; }
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
            ISet<string> drawnImages,
            ISet<string> overwritingTools,
            IDictionary<string, ISet<string>> unkeptMembers,
            IDictionary<string, ISet<string>> targetedMembers,
            IDictionary<string, ParentValues> parentValues,
            SizeBudgets budgets)
        {
            OverwritingTools = new HashSet<string>(overwritingTools, StringComparer.Ordinal);
            Spellings = new ReadOnlyCollection<ValueSpellingRow>(spellings);
            Types = new ReadOnlyCollection<ValueShapeRow>(types);
            Components = new ReadOnlyDictionary<string, int>(components);
            ComposedTools = new ReadOnlyDictionary<string, ComposedTool>(composedTools);
            ViewImages = new ReadOnlyDictionary<string, string>(viewImages);
            DrawnImages = new HashSet<string>(drawnImages, StringComparer.Ordinal);
            UnkeptMembers = new ReadOnlyDictionary<string, ISet<string>>(unkeptMembers);
            TargetedMembers =
                new ReadOnlyDictionary<string, ISet<string>>(targetedMembers);
            ParentValues = new ReadOnlyDictionary<string, ParentValues>(parentValues);
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

        /// <summary>ビューを写さず、呼び出しが描いた画像を返すツールの名前。</summary>
        public ISet<string> DrawnImages { get; }

        /// <summary>ファイルへ書き込むので確認を要する合成ツールの名前。</summary>
        public ISet<string> OverwritingTools { get; }

        /// <summary>
        /// 値を書き換えるツールの名前から、書いてもモデルが持ち続けない項目の名前へ。持ち主の
        /// 状態によって捨てられる項目がどれかは、呼び先の型からは決まらないのでここが決める。
        /// </summary>
        public IDictionary<string, ISet<string>> UnkeptMembers { get; }

        /// <summary>
        /// 値を書き換えるツールの名前から、要素を並びへ加える前に指す先を埋める項目の名前へ。
        /// 指す先を持たないまま加えると書き戻しで捨てられる要素がどれかは、呼び先の型からは
        /// 決まらないのでここが決める。
        /// </summary>
        public IDictionary<string, ISet<string>> TargetedMembers { get; }

        /// <summary>
        /// 要素を親の並びへ加えるツールの名前から、加える前に親へ揃える値へ。親の側が
        /// 揃っていないと加えられない要素だけが持つ。
        /// </summary>
        public IDictionary<string, ParentValues> ParentValues { get; }

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

        private const string DrawnImagesName = "drawnImages";

        private const string OverwritingToolsName = "overwritingTools";

        private const string ViewName = "view";

        private const string UnkeptMembersName = "unkeptMembers";

        private const string TargetedMembersName = "targetedMembers";

        private const string ParentValuesName = "parentValues";

        private const string ParentToolName = "parentTool";

        private const string MemberName = "member";

        private const string ValueName = "value";

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
            JsonForm.Member(DrawnImagesName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(ToolName, JsonForm.Text())),
                ToolName,
                allowEmpty: true)),
            JsonForm.Member(OverwritingToolsName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(ToolName, JsonForm.Text())),
                ToolName,
                allowEmpty: true)),
            JsonForm.Member(UnkeptMembersName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(ToolName, JsonForm.Text()),
                    JsonForm.Member(MembersName, JsonForm.Array(JsonForm.Text())),
                    JsonForm.Member(BasisName, JsonForm.Text())),
                ToolName,
                allowEmpty: true)),
            JsonForm.Member(TargetedMembersName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(ToolName, JsonForm.Text()),
                    JsonForm.Member(MembersName, JsonForm.Array(JsonForm.Text())),
                    JsonForm.Member(BasisName, JsonForm.Text())),
                ToolName,
                allowEmpty: true)),
            JsonForm.Member(ParentValuesName, JsonForm.Array(
                JsonForm.Object(
                    JsonForm.Member(ToolName, JsonForm.Text()),
                    JsonForm.Member(ParentToolName, JsonForm.Text()),
                    JsonForm.Member(MemberName, JsonForm.Text()),
                    JsonForm.Member(ValueName, JsonForm.Text()),
                    JsonForm.Member(BasisName, JsonForm.Text())),
                ToolName,
                allowEmpty: true)),
            JsonForm.Member(BudgetsName, JsonForm.Object(
                JsonForm.Member(ResponseDefaultCharsName, JsonForm.Count()),
                JsonForm.Member(WarningRoomCharsName, JsonForm.Count()),
                JsonForm.Member(RequestBytesName, JsonForm.Count()),
                JsonForm.Member(StructureTokenLimitName, JsonForm.Count()))));

        /// <summary>
        /// 形が違えば <see cref="FormatException"/>。ファイルへ書き込むと名指したツールが合成ツールに
        /// 無いときも同じ。
        /// </summary>
        public static CommonContractTable Read(string json)
        {
            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            IDictionary<string, object> budgets =
                (IDictionary<string, object>)root[BudgetsName];
            ISet<string> composed = new HashSet<string>(
                Rows(root, ComposedToolsName).Select(r => (string)r[ToolName]), StringComparer.Ordinal);
            string[] stray = Rows(root, OverwritingToolsName)
                .Select(r => (string)r[ToolName])
                .Where(t => !composed.Contains(t))
                .ToArray();
            if (stray.Length > 0)
            {
                throw new FormatException(
                    OverwritingToolsName + " が合成ツールでないものを名指している: " + string.Join("・", stray));
            }

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
                new HashSet<string>(
                    Rows(root, DrawnImagesName).Select(r => (string)r[ToolName]),
                    StringComparer.Ordinal),
                new HashSet<string>(
                    Rows(root, OverwritingToolsName).Select(r => (string)r[ToolName]),
                    StringComparer.Ordinal),
                Rows(root, UnkeptMembersName).ToDictionary(
                    r => (string)r[ToolName],
                    r => (ISet<string>)new HashSet<string>(
                        ((IList<object>)r[MembersName]).Cast<string>(), StringComparer.Ordinal),
                    StringComparer.Ordinal),
                Rows(root, TargetedMembersName).ToDictionary(
                    r => (string)r[ToolName],
                    r => (ISet<string>)new HashSet<string>(
                        ((IList<object>)r[MembersName]).Cast<string>(), StringComparer.Ordinal),
                    StringComparer.Ordinal),
                Rows(root, ParentValuesName).ToDictionary(
                    r => (string)r[ToolName],
                    r => new ParentValues(
                        (string)r[ParentToolName],
                        (string)r[MemberName],
                        (string)r[ValueName]),
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
