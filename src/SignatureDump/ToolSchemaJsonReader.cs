using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>スキーマ正本を読む。</summary>
    public static class ToolSchemaJsonReader
    {
        private const string ToolsName = "tools";

        private const string ToolName = "tool";

        private const string BranchesName = "branches";

        private const string OutputName = "output";

        private const string ListingName = "listing";

        private const string PayloadsName = "payloads";

        private const string BranchName = "branch";

        private const string SelectorName = "selector";

        private const string InputsName = "inputs";

        private const string ChoicesName = "choices";

        private const string NamesName = "names";

        private const string TypeName = "type";

        private const string MembersName = "members";

        private const string LimitDefaultName = "limitDefault";

        private const string LimitMaximumName = "limitMaximum";

        private const string ShapeName = "shape";

        private const string ElementName = "element";

        private const string NameName = "name";

        private const string OriginName = "origin";

        private const string RequiredName = "required";

        private const string DefaultName = "default";

        private const string BoundsName = "bounds";

        private const string NullableName = "nullable";

        private const string SourceName = "source";

        private const string InjectedName = "injected";

        private const string MaxItemsName = "maxItems";

        private const string MinItemsName = "minItems";

        private const string MinimumName = "minimum";

        private const string MaximumName = "maximum";

        private const string ValueName = "value";

        /// <summary>イベント1件が持つ、種別ごとに形の変わる値の項目。</summary>
        private const string PayloadName = "payload";

        private static readonly Regex SnakeCaseName = new Regex(
            "^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        private static readonly Regex MemberName = new Regex(
            "^[a-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, ItemOrigin> Origins =
            new Dictionary<string, ItemOrigin>(StringComparer.Ordinal)
            {
                { "sdkIn", ItemOrigin.SdkIn },
                { "sdkOut", ItemOrigin.SdkOut },
                { "sdkRef", ItemOrigin.SdkRef },
                { "sdkReturn", ItemOrigin.SdkReturn },
                { "hostInput", ItemOrigin.HostInput },
                { "hostOutput", ItemOrigin.HostOutput },
            };

        /// <summary>SDKに由来する既定と範囲。転記元の一次資料の記載を伴う。</summary>
        private static readonly ItemOrigin[] FromSdk =
        {
            ItemOrigin.SdkIn, ItemOrigin.SdkOut, ItemOrigin.SdkRef, ItemOrigin.SdkReturn,
        };

        /// <summary>
        /// ツールを書かれた順に返す。名前が昇順に重複なく並ぶことと、項目が形をちょうど1つで表す
        /// ことを求める。例外は、イベントの取り出しの応答が持つ `payload` の項目だけで、そちらは
        /// 形を持たず、`payloads` が種別ごとに定める。形が違えば <see cref="FormatException"/>。
        /// </summary>
        public static ToolSchemaTable Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object parsed;
            try
            {
                parsed = new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("JSONとして読めない。", exception);
            }

            List<ToolSchema> tools = new List<ToolSchema>();
            string previous = null;
            foreach (object item in Array(Members(parsed, ToolsName)[ToolsName], ToolsName))
            {
                ToolSchema tool = ReadTool(item);
                RequireAscending(previous, tool.Tool);
                previous = tool.Tool;
                tools.Add(tool);
            }

            return new ToolSchemaTable(tools);
        }

        private static ToolSchema ReadTool(object item)
        {
            Dictionary<string, object> members = Members(
                item,
                new[] { ToolName, BranchesName, OutputName },
                new[] { ListingName, PayloadsName });

            List<SchemaBranch> branches = new List<SchemaBranch>();
            List<string> names = new List<string>();
            foreach (object each in Array(members[BranchesName], BranchesName))
            {
                SchemaBranch branch = ReadBranch(each);
                if (names.Contains(branch.Branch, StringComparer.Ordinal))
                {
                    throw new FormatException("同じ呼び分けが二度現れる: " + branch.Branch);
                }

                names.Add(branch.Branch);
                branches.Add(branch);
            }

            if (branches.Count == 0)
            {
                throw new FormatException(BranchesName + " は1件以上でなければならない。");
            }

            bool polls = members.ContainsKey(PayloadsName);
            if (polls && members.ContainsKey(ListingName))
            {
                throw new FormatException(
                    "イベントの取り出しは一覧の規則の対象外なので " + ListingName + " を持たない。");
            }

            try
            {
                return new ToolSchema(
                    Name(members[ToolName], ToolName),
                    branches,
                    ReadItem(members[OutputName], false, false, polls),
                    members.ContainsKey(ListingName) ? ReadListing(members[ListingName]) : null,
                    members.ContainsKey(PayloadsName) ? ReadPayloads(members[PayloadsName]) : null);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static SchemaBranch ReadBranch(object item)
        {
            Dictionary<string, object> members = Members(
                item, new[] { BranchName, InputsName }, new[] { SelectorName, ChoicesName });

            List<SchemaItem> inputs = ReadItems(members[InputsName], InputsName, true);
            List<SchemaChoice> choices = members.ContainsKey(ChoicesName)
                ? ReadChoices(members[ChoicesName], inputs)
                : new List<SchemaChoice>();
            RequireChosenOrRequired(inputs, choices);
            string selectorName = null;
            object selectorValue = null;
            if (members.ContainsKey(SelectorName))
            {
                Dictionary<string, object> selector = Members(
                    members[SelectorName], NameName, ValueName);
                selectorName = Member(selector[NameName], NameName);
                if (!inputs.Any(i => string.Equals(i.Name, selectorName, StringComparison.Ordinal)))
                {
                    throw new FormatException("分岐を選ぶ項目が入力に無い: " + selectorName);
                }

                selectorValue = selector[ValueName];
                RequireLiteral(selectorValue);
            }

            try
            {
                return new SchemaBranch(
                    Member(members[BranchName], BranchName), selectorName, selectorValue,
                    inputs, choices);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        /// <summary>
        /// まとまりは、その分岐の入力に実在する2件以上の項目を、まとまりをまたいで重ねずに並べる。
        /// </summary>
        private static List<SchemaChoice> ReadChoices(object value, IList<SchemaItem> inputs)
        {
            List<SchemaChoice> choices = new List<SchemaChoice>();
            List<string> taken = new List<string>();
            foreach (object item in Array(value, ChoicesName))
            {
                Dictionary<string, object> members = Members(item, NamesName, RequiredName);
                List<string> names = new List<string>();
                foreach (object each in Array(members[NamesName], NamesName))
                {
                    string name = Member(each, NamesName);
                    if (!inputs.Any(i => string.Equals(i.Name, name, StringComparison.Ordinal)))
                    {
                        throw new FormatException("まとまりが入力に無い項目を並べている: " + name);
                    }

                    if (taken.Contains(name, StringComparer.Ordinal))
                    {
                        throw new FormatException("同じ項目が二つのまとまりに現れる: " + name);
                    }

                    taken.Add(name);
                    names.Add(name);
                }

                if (names.Count < 2)
                {
                    throw new FormatException(NamesName + " は2件以上でなければならない。");
                }

                choices.Add(new SchemaChoice(names, Flag(members[RequiredName], RequiredName)));
            }

            return choices;
        }

        /// <summary>
        /// 呼び出す側が渡す項目だけが要不要を持つことを、入れ子まで含めて求める。まとまりに入る項目は
        /// まとまりの側が、`injected` の項目はホストが、それぞれ要不要を決める。
        /// </summary>
        private static void RequireChosenOrRequired(
            IList<SchemaItem> inputs, IList<SchemaChoice> choices)
        {
            List<string> chosen = choices.SelectMany(c => c.Names).ToList();
            foreach (SchemaItem item in inputs)
            {
                RequirePassedByCaller(item, chosen.Contains(item.Name, StringComparer.Ordinal));
                foreach (SchemaItem nested in item.WithNested.Skip(1).Where(i => i.Name != null))
                {
                    RequirePassedByCaller(nested, false);
                }
            }
        }

        private static void RequirePassedByCaller(SchemaItem item, bool chosen)
        {
            if ((!chosen && !item.Injected) != item.Required.HasValue)
            {
                throw new FormatException(
                    "呼び出す側が渡す項目だけが " + RequiredName + " を持つ: " + item.Name);
            }
        }

        private static List<SchemaPayload> ReadPayloads(object value)
        {
            List<SchemaPayload> payloads = new List<SchemaPayload>();
            List<string> types = new List<string>();
            foreach (object item in Array(value, PayloadsName))
            {
                Dictionary<string, object> members = Members(item, TypeName, MembersName);
                string type = Text(members[TypeName], TypeName);
                if (types.Contains(type, StringComparer.Ordinal))
                {
                    throw new FormatException("同じ分岐が二度現れる: " + type);
                }

                types.Add(type);
                payloads.Add(new SchemaPayload(
                    type, ReadItems(members[MembersName], MembersName, false)));
            }

            if (payloads.Count == 0)
            {
                throw new FormatException(PayloadsName + " は1件以上でなければならない。");
            }

            return payloads;
        }

        private static ListingLimits ReadListing(object value)
        {
            Dictionary<string, object> members = Members(value, LimitDefaultName, LimitMaximumName);
            try
            {
                return new ListingLimits(
                    Count(members[LimitDefaultName], LimitDefaultName),
                    Count(members[LimitMaximumName], LimitMaximumName));
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static List<SchemaItem> ReadItems(
            object value, string name, bool input, bool polls = false)
        {
            List<SchemaItem> items = new List<SchemaItem>();
            List<string> names = new List<string>();
            foreach (object item in Array(value, name))
            {
                SchemaItem read = ReadItem(item, true, input, polls);
                if (names.Contains(read.Name, StringComparer.Ordinal))
                {
                    throw new FormatException("同じ項目の名前が二度現れる: " + read.Name);
                }

                names.Add(read.Name);
                items.Add(read);
            }

            return items;
        }

        /// <summary>
        /// 応答の値そのものと配列の要素は名前を持たない。イベントの payload は形を分岐の側が
        /// 持つので、3つの表し方のどれも持たない。
        /// </summary>
        private static SchemaItem ReadItem(
            object item, bool named, bool input, bool polls = false)
        {
            Dictionary<string, object> members = Members(
                item,
                named ? new[] { NameName, OriginName } : new[] { OriginName },
                new[]
                {
                    ShapeName, MembersName, ElementName, RequiredName, DefaultName, BoundsName,
                    NullableName, SourceName, InjectedName, MaxItemsName, MinItemsName,
                });

            string name = named ? Member(members[NameName], NameName) : null;
            string[] forms = { ShapeName, MembersName, ElementName };
            int shapes = forms.Count(members.ContainsKey);
            bool payload = polls && string.Equals(name, PayloadName, StringComparison.Ordinal);
            if (shapes != (payload ? 0 : 1))
            {
                throw new FormatException(
                    "項目の形は3つのうち1つでなければならない(イベントの `payload` だけが持たない): "
                        + Written(members, NameName));
            }

            bool namedInput = named && input;
            if (members.ContainsKey(RequiredName) && !namedInput)
            {
                throw new FormatException(
                    "呼び出す側が渡す項目だけが " + RequiredName + " を持つ: "
                        + Written(members, NameName));
            }

            if (members.ContainsKey(InjectedName) && !namedInput)
            {
                throw new FormatException(
                    "名前のある入力の項目だけが " + InjectedName + " を持つ: "
                        + Written(members, NameName));
            }

            ItemOrigin origin = Lookup(Origins, members[OriginName], OriginName);
            bool fromSdk = FromSdk.Contains(origin);
            bool hasValue = members.ContainsKey(DefaultName) || members.ContainsKey(BoundsName);
            if (fromSdk && hasValue != members.ContainsKey(SourceName))
            {
                throw new FormatException(
                    "SDKに由来する既定と範囲は転記元を伴う: " + Written(members, NameName));
            }

            if (!fromSdk && members.ContainsKey(SourceName))
            {
                throw new FormatException(
                    "共通契約が定める値は転記元を持たない: " + Written(members, NameName));
            }

            if (members.ContainsKey(ElementName) != members.ContainsKey(MaxItemsName))
            {
                throw new FormatException(
                    "要素を並べる項目だけが要素数の上限を持つ: " + Written(members, NameName));
            }

            object defaultValue = null;
            if (members.ContainsKey(DefaultName))
            {
                defaultValue = members[DefaultName];
                RequireLiteral(defaultValue);
            }

            return new SchemaItem(
                members.ContainsKey(ShapeName) ? Text(members[ShapeName], ShapeName) : null,
                members.ContainsKey(MembersName)
                    ? ReadItems(members[MembersName], MembersName, input, polls)
                    : null,
                members.ContainsKey(ElementName)
                    ? ReadItem(members[ElementName], false, input, polls)
                    : null,
                name,
                origin,
                members.ContainsKey(RequiredName)
                    ? Flag(members[RequiredName], RequiredName)
                    : (bool?)null,
                defaultValue,
                members.ContainsKey(DefaultName),
                members.ContainsKey(BoundsName) ? ReadBounds(members[BoundsName]) : null,
                members.ContainsKey(NullableName)
                    ? Flag(members[NullableName], NullableName)
                    : (bool?)null,
                members.ContainsKey(SourceName) ? Text(members[SourceName], SourceName) : null,
                members.ContainsKey(InjectedName) && Flag(members[InjectedName], InjectedName),
                members.ContainsKey(MaxItemsName) ? Count(members[MaxItemsName], MaxItemsName) : (int?)null,
                members.ContainsKey(MinItemsName) ? Empty(members[MinItemsName]) : (int?)null);
        }

        private static ValueBounds ReadBounds(object value)
        {
            Dictionary<string, object> members = Members(
                value, new string[0], new[] { MinimumName, MaximumName });
            try
            {
                return new ValueBounds(
                    members.ContainsKey(MinimumName) ? Number(members[MinimumName], MinimumName) : (double?)null,
                    members.ContainsKey(MaximumName) ? Number(members[MaximumName], MaximumName) : (double?)null);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        /// <summary>既定はJSONのリテラルで、演算も参照も持たない。入れ子の中も同じとする。</summary>
        private static void RequireLiteral(object value)
        {
            object[] items = value as object[];
            if (items != null)
            {
                foreach (object item in items)
                {
                    RequireLiteral(item);
                }

                return;
            }

            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in members)
            {
                if (!MemberName.IsMatch(pair.Key))
                {
                    throw new FormatException("値の項目の名前でない: " + pair.Key);
                }

                RequireLiteral(pair.Value);
            }
        }

        private static string Written(Dictionary<string, object> members, string name)
        {
            object value;
            return members.TryGetValue(name, out value) ? value as string ?? "名前無し" : "名前無し";
        }

        private static int Empty(object value)
        {
            int count = Count(value, MinItemsName);
            if (count != 1)
            {
                throw new FormatException(MinItemsName + " は1でなければならない。");
            }

            return count;
        }

        private static int Count(object value, string name)
        {
            if (!(value is int))
            {
                throw new FormatException(name + " は整数でなければならない。");
            }

            int count = (int)value;
            if (count < 1)
            {
                throw new FormatException(name + " は1以上でなければならない。");
            }

            return count;
        }

        private static double Number(object value, string name)
        {
            if (value is int)
            {
                return (int)value;
            }

            if (value is long)
            {
                return (long)value;
            }

            if (value is double)
            {
                return (double)value;
            }

            if (value is decimal)
            {
                return (double)(decimal)value;
            }

            throw new FormatException(name + " は数値でなければならない。");
        }

        private static bool Flag(object value, string name)
        {
            if (!(value is bool))
            {
                throw new FormatException(name + " は真偽でなければならない。");
            }

            return (bool)value;
        }

        private static string Member(object value, string name)
        {
            string text = Text(value, name);
            if (!MemberName.IsMatch(text))
            {
                throw new FormatException(
                    name + " は小文字で始まり、英数字だけからなる語でなければならない: " + text);
            }

            return text;
        }

        private static string Name(object value, string name)
        {
            string text = Text(value, name);
            if (!SnakeCaseName.IsMatch(text))
            {
                throw new FormatException(
                    name + " は小文字で始まり、小文字と数字と下線だけからなる語でなければならない: "
                        + text);
            }

            return text;
        }

        private static TValue Lookup<TValue>(
            Dictionary<string, TValue> table, object value, string name)
        {
            string text = Text(value, name);
            TValue found;
            if (!table.TryGetValue(text, out found))
            {
                throw new FormatException("知らない " + name + ": " + text);
            }

            return found;
        }

        private static void RequireAscending(string previous, string current)
        {
            if (previous == null)
            {
                return;
            }

            int order = string.CompareOrdinal(previous, current);
            if (order == 0)
            {
                throw new FormatException("同じツールの名前が二度現れる: " + current);
            }

            if (order > 0)
            {
                throw new FormatException("序数の昇順で並んでいない: " + current);
            }
        }

        private static object[] Array(object value, string name)
        {
            object[] items = value as object[];
            if (items == null)
            {
                throw new FormatException(name + " は項目の並びでなければならない。");
            }

            return items;
        }

        private static Dictionary<string, object> Members(object value, params string[] names)
        {
            return Members(value, names, new string[0]);
        }

        /// <summary>
        /// 求める項目と持てる項目だけを持つ対象として読む。余分な項目を黙って捨てると、正本の形が
        /// 崩れても気づけない。
        /// </summary>
        private static Dictionary<string, object> Members(
            object value, string[] names, string[] optional)
        {
            Dictionary<string, object> members = value as Dictionary<string, object>;
            if (members == null)
            {
                throw new FormatException("項目の組でなければならない。");
            }

            foreach (string name in names)
            {
                if (!members.ContainsKey(name))
                {
                    throw new FormatException("項目が無い: " + name);
                }
            }

            foreach (string name in members.Keys)
            {
                if (!names.Contains(name, StringComparer.Ordinal)
                    && !optional.Contains(name, StringComparer.Ordinal))
                {
                    throw new FormatException("知らない項目がある: " + name);
                }
            }

            return members;
        }

        private static string Text(object value, string name)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                throw new FormatException(
                    name + " は空でない文字列でなければならない(空白だけも不可)。");
            }

            return text;
        }
    }
}
