using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Json.Schema;
using PmxEditorMcp.Bridge;
using PmxEditorMcp.SignatureDump;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class ShippedToolDefinitionTests
    {
        private const string ConfirmName = "confirm";

        private const string SuppressName = "suppressUndo";

        private const string PmxHandleName = "pmxHandle";

        private const string ClearingOnlyTheModelItsHandlePointsAt = "model_clear_pmx";

        private const string UndeclaredArgumentName = "argumentNoToolDeclares";

        private const int MostArgumentsPerInput = 4;

        private const int MostValueCombinationsPerArgumentSet = 16;

        private const double BeyondInt32 = 2147483648d;

        private const double BeyondSingle = 1e39d;

        private static readonly Lazy<HostTypes> KnownHostTypes = new Lazy<HostTypes>(() => new HostTypes());

        [Fact]
        public void EveryInputTheSchemaTakesMeetsABranchOfTheHost()
        {
            IDictionary<string, JsonObject> sources = Sources();
            GeneratedToolDefinition[] definitions = GeneratedToolDefinitions.Create().ToArray();
            Assert.Equal(
                new string[0],
                definitions.Select(d => d.Name).Where(n => !sources.ContainsKey(n)).ToArray());
            IList<string>[] found = new IList<string>[definitions.Length];
            Parallel.For(0, definitions.Length, at =>
            {
                found[at] = Violations(definitions[at], Branches(sources[definitions[at].Name]));
            });
            List<string> violations = found.SelectMany(f => f).ToList();

            Assert.True(
                violations.Count == 0,
                "スキーマが通すのに、ホストのどの呼び分けにも当てはまらない入力が "
                    + violations.Count + " 件ある:\n"
                    + string.Join("\n", violations.Take(30)));
        }

        private static IList<string> Violations(GeneratedToolDefinition definition, IList<HostBranch> branches)
        {
            List<string> violations = new List<string>();
            JsonSchema schema = JsonSchema.FromText(definition.InputSchema);
            JsonObject properties = JsonNode.Parse(definition.InputSchema)["properties"].AsObject();
            HashSet<string> tried = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<JsonObject> inputs = Inputs(properties, branches, false)
                .Concat(Inputs(properties, branches, true));
            foreach (JsonObject input in inputs)
            {
                string written = input.ToJsonString();
                if (!tried.Add(written) || branches.Any(b => b.Fits(input)) || !Takes(schema, written))
                {
                    continue;
                }

                violations.Add(definition.Name + " " + written);
            }

            return violations;
        }

        [Fact]
        public void EveryInputWhoseHostTypeIsNotDerivedFallsInAKindLeftUnchecked()
        {
            HostTypeCensus census = new HostTypeCensus();
            foreach (JsonObject source in Sources().Values)
            {
                foreach (JsonNode branch in source["branches"].AsArray())
                {
                    KnownHostTypes.Value.Rules(Text(source, "tool"), branch.AsObject(), census);
                }
            }

            Assert.True(
                census.Underived.Count == 0,
                "ホストが受け取る型を導けず、照合しない区分にも当たらない入力が "
                    + census.Underived.Count + " 件ある("
                    + string.Join("・", census.Kinds.Select(k => k.Key + " " + k.Value + " 件"))
                    + "):\n"
                    + string.Join("\n", census.Underived.Take(30)));
        }

        [Fact]
        public void CreatingAMotionWithOnlyTheMorphNamesIsNotTaken()
        {
            JsonSchema schema = SchemaOf("motion_create_vmd");

            Assert.True(Takes(schema, "{\"boneNames\":[\"a\"],\"morphNames\":[\"b\"]}"));
            Assert.False(Takes(schema, "{\"morphNames\":[\"b\"]}"));
            Assert.False(Takes(schema, "{\"boneNames\":[\"a\"]}"));
        }

        [Fact]
        public void ClearingAModelTakesItsHandleAndNoConfirmation()
        {
            JsonSchema schema = SchemaOf(ClearingOnlyTheModelItsHandlePointsAt);

            Assert.True(Takes(schema, "{\"pmxHandle\":1}"));
            Assert.False(Takes(schema, "{}"));
            Assert.False(Takes(schema, "{\"confirm\":true}"));
            Assert.False(Takes(schema, "{\"pmxHandle\":1,\"confirm\":true}"));
        }

        [Fact]
        public void ListingBonesByPositionAndByHandleAtOnceIsNotTaken()
        {
            JsonSchema schema = SchemaOf("model_list_bones");

            Assert.True(Takes(schema, "{\"indices\":[0]}"));
            Assert.True(Takes(schema, "{\"handles\":[1]}"));
            Assert.False(Takes(schema, "{\"indices\":[0],\"handles\":[1]}"));
            Assert.False(Takes(schema, "{\"all\":true,\"handles\":[1]}"));
        }

        [Fact]
        public void EveryListingTakesALimitWithItsBoundsAndDefault()
        {
            IDictionary<string, JsonObject> sources = Sources();
            List<string> missing = new List<string>();
            foreach (GeneratedToolDefinition definition in GeneratedToolDefinitions.Create())
            {
                JsonObject source = sources[definition.Name];
                bool takesLimit = source["branches"].AsArray().Any(
                    b => b["inputs"].AsArray().Any(i => Text(i, "name") == "limit"));
                if (!IsListing(source["output"]) || !takesLimit)
                {
                    continue;
                }

                JsonNode limit = JsonNode.Parse(definition.InputSchema)["properties"]["limit"];
                IList<JsonObject> forms = Alternatives(limit);
                if (forms.Count == 0
                    || !forms.All(f => f.ContainsKey("minimum")
                        && f.ContainsKey("maximum")
                        && f.ContainsKey("default")))
                {
                    missing.Add(definition.Name + " " + limit.ToJsonString());
                }
            }

            Assert.True(
                missing.Count == 0,
                "limit に下限・上限・既定の揃わない一覧が " + missing.Count + " 件ある:\n"
                    + string.Join("\n", missing));
        }

        [Fact]
        public void EveryShippedDescriptionFitsTheLimit()
        {
            List<string> over = new List<string>();
            foreach (KeyValuePair<string, string> tool in ShippedDescriptions(true))
            {
                int bytes = Encoding.UTF8.GetByteCount(tool.Value);
                if (bytes > ToolDescriptionRule.LimitBytes)
                {
                    over.Add(tool.Key + "(" + bytes + "バイト)");
                }
            }

            Assert.True(
                over.Count == 0, "説明文が上限のバイト数を超える: " + string.Join("・", over));
        }

        [Fact]
        public void TheShippedToolNamesDoNotRepeat()
        {
            string[] names = GeneratedToolDefinitions.Create().Select(d => d.Name)
                .Concat(FixedToolTable.Descriptions(true).Keys)
                .ToArray();

            Assert.Equal(
                new string[0],
                names.GroupBy(n => n, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToArray());
        }

        [Fact]
        public void ToolsSharingAnActionWordInAGroupAreToldApartByTheirFirstLine()
        {
            Regex head = new Regex("^対象 (\\S+) / 動作 (\\S+) / 出所 (\\S+)$");
            Regex action = new Regex(" / 動作 (\\S+)");
            List<string> clashes = new List<string>();
            foreach (IGrouping<string, KeyValuePair<string, string>> sharing in
                GeneratedToolDefinitions.Create()
                    .Select(d => new KeyValuePair<string, string>(d.Name, FirstLine(d.Description)))
                    .Where(d => action.IsMatch(d.Value))
                    .GroupBy(
                        d => d.Key.Substring(0, d.Key.IndexOf('_')) + " "
                            + action.Match(d.Value).Groups[1].Value,
                        StringComparer.Ordinal)
                    .Where(g => g.Count() > 1))
            {
                foreach (KeyValuePair<string, string> tool in sharing.Where(t => !head.IsMatch(t.Value)))
                {
                    clashes.Add(tool.Key + " の先頭が対象と出所を持たない: " + tool.Value);
                }

                foreach (IGrouping<string, KeyValuePair<string, string>> same in sharing
                    .GroupBy(t => t.Value, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1))
                {
                    clashes.Add(string.Join("・", same.Select(t => t.Key)) + " の先頭が同じ: " + same.Key);
                }
            }

            Assert.True(clashes.Count == 0, string.Join("\n", clashes));
        }

        [Fact]
        public void EachTaskSearchedWithFindToolFindsExactlyItsTools()
        {
            List<ToolSearch.Entry> entries = ShippedDescriptions(false)
                .Select(d => new ToolSearch.Entry(d.Key, d.Value))
                .ToList();
            JsonObject tasks = JsonNode.Parse(File.ReadAllText(Authored("discovery-tasks.json")))
                .AsObject();
            List<string> mismatches = new List<string>();
            foreach (JsonNode task in tasks["tasks"].AsArray())
            {
                HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonNode search in task["searches"].AsArray())
                {
                    IEnumerable<string> hit = null;
                    foreach (string term in search.AsArray().Select(t => t.GetValue<string>()))
                    {
                        IList<string> named = ToolSearch.Found(term, entries);
                        hit = hit == null ? named : hit.Intersect(named, StringComparer.Ordinal).ToList();
                    }

                    found.UnionWith(hit ?? new string[0]);
                }

                string[] expected = task["tools"].AsArray().Select(t => t.GetValue<string>())
                    .OrderBy(t => t, StringComparer.Ordinal).ToArray();
                string[] actual = found.OrderBy(t => t, StringComparer.Ordinal).ToArray();
                if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                {
                    mismatches.Add(
                        task["task"].GetValue<string>() + ": 足りない "
                            + string.Join("・", expected.Except(actual, StringComparer.Ordinal))
                            + " / 余計 "
                            + string.Join("・", actual.Except(expected, StringComparer.Ordinal)));
                }
            }

            Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
        }

        [Fact]
        public void NoToolIsDangerousOnlyForSomeTargets()
        {
            IDictionary<string, JsonObject> sources = Sources();
            ISet<string> confirmedByTheHost = ToolsTheHostConfirms();
            GeneratedToolDefinition[] definitions = GeneratedToolDefinitions.Create().ToArray();
            string[] found = new string[definitions.Length];
            Parallel.For(0, definitions.Length, at =>
            {
                GeneratedToolDefinition definition = definitions[at];
                JsonSchema schema = JsonSchema.FromText(definition.InputSchema);
                JsonObject properties =
                    JsonNode.Parse(definition.InputSchema)["properties"].AsObject();
                bool takenOnlyWithConfirm = false;
                bool takenWithoutConfirm = false;
                HashSet<string> tried = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonObject input in Inputs(properties, Branches(sources[definition.Name]), false))
                {
                    input.Remove(ConfirmName);
                    string written = input.ToJsonString();
                    if (!tried.Add(written))
                    {
                        continue;
                    }

                    JsonObject confirmed = input.DeepClone().AsObject();
                    confirmed[ConfirmName] = true;
                    if (Takes(schema, written))
                    {
                        takenWithoutConfirm = true;
                    }
                    else if (Takes(schema, confirmed))
                    {
                        takenOnlyWithConfirm = true;
                    }
                }

                if (takenOnlyWithConfirm && takenWithoutConfirm)
                {
                    found[at] = definition.Name + ": 確認を要る入力と要らない入力の両方を通す";
                }
                else if (confirmedByTheHost.Contains(definition.Name) && !takenOnlyWithConfirm)
                {
                    found[at] = definition.Name + ": ホストが確認を要るのに、確認を要る入力を通さない";
                }
            });
            List<string> violations = found.Where(f => f != null).ToList();

            Assert.True(violations.Count == 0, string.Join("\n", violations));
        }

        [Fact]
        public void TheBridgeBudgetDefaultIsTheOneTheContractWrites()
        {
            JsonNode budgets = JsonNode.Parse(File.ReadAllText(Authored("common-contract.json")))["budgets"];

            Assert.Equal(BridgeBudget.DefaultChars, budgets["responseDefaultChars"].GetValue<int>());
        }

        private static IList<KeyValuePair<string, string>> ShippedDescriptions(bool debugHooks)
        {
            return GeneratedToolDefinitions.Create()
                .Select(d => new KeyValuePair<string, string>(d.Name, d.Description))
                .Concat(FixedToolTable.Descriptions(debugHooks))
                .ToList();
        }

        private static string FirstLine(string text)
        {
            int end = text.IndexOf('\n');

            return end < 0 ? text : text.Substring(0, end);
        }

        private static bool IsListing(JsonNode output)
        {
            JsonNode answer = output;
            while (answer != null
                && answer["members"] == null
                && Text(answer, "origin") == "hostOutput"
                && answer["element"] != null
                && Text(answer["element"], "origin") == "hostOutput")
            {
                answer = answer["element"];
            }

            JsonArray members = answer == null ? null : answer["members"] as JsonArray;

            return members != null
                && members.Any(m => Text(m, "name") == "total")
                && members.Any(m => Text(m, "name") == "items" && m["element"] != null);
        }

        private static IList<JsonObject> Alternatives(JsonNode node)
        {
            if (!(node is JsonObject item))
            {
                return new JsonObject[0];
            }

            JsonArray any = item["anyOf"] as JsonArray ?? item["oneOf"] as JsonArray;

            return any == null
                ? new[] { item }
                : any.SelectMany(Alternatives).ToList();
        }

        private static string Text(JsonNode node, string name)
        {
            JsonNode value = node == null ? null : node[name];

            return value == null ? null : value.GetValue<string>();
        }

        private static JsonSchema SchemaOf(string tool)
        {
            return JsonSchema.FromText(
                GeneratedToolDefinitions.Create().Single(d => d.Name == tool).InputSchema);
        }

        private static bool Takes(JsonSchema schema, string input)
        {
            using (JsonDocument document = JsonDocument.Parse(input))
            {
                return schema.Evaluate(document.RootElement).IsValid;
            }
        }

        private static bool Takes(JsonSchema schema, JsonObject input)
        {
            return Takes(schema, input.ToJsonString());
        }

        private static IEnumerable<JsonObject> Inputs(
            JsonObject properties, IList<HostBranch> branches, bool offType)
        {
            string[] names = properties.Select(p => p.Key).ToArray();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string[]> sets = new List<string[]>();
            Assert.False(
                properties.ContainsKey(UndeclaredArgumentName),
                "宣言していない引数として使う名前をスキーマが宣言している: " + UndeclaredArgumentName);
            foreach (string[] set in Subsets(names, offType ? 0 : MostArgumentsPerInput))
            {
                sets.Add(set);
            }

            foreach (HostBranch branch in branches)
            {
                IEnumerable<IEnumerable<string>> picks = new[] { Enumerable.Empty<string>() };
                foreach (HostChoice choice in branch.Choices.Where(c => c.Required))
                {
                    IEnumerable<string> alternatives = offType ? choice.Names : choice.Names.Take(1);
                    picks = picks.SelectMany(p => alternatives.Select(n => p.Concat(new[] { n })));
                }

                foreach (IEnumerable<string> pick in picks.ToList())
                {
                    string[] basis = branch.Required
                        .Concat(pick)
                        .Concat(branch.Selector == null ? new string[0] : new[] { branch.Selector })
                        .Concat(offType ? new[] { ConfirmName } : new string[0])
                        .Where(properties.ContainsKey)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    foreach (string[] extra in Subsets(names.Except(basis).ToArray(), offType ? 1 : 2))
                    {
                        string[] set = basis.Concat(extra).ToArray();
                        sets.Add(set);
                        sets.Add(set.Concat(new[] { UndeclaredArgumentName }).ToArray());
                    }

                    if (offType)
                    {
                        foreach (string missing in basis)
                        {
                            sets.Add(basis.Where(n => n != missing).ToArray());
                        }
                    }
                }
            }

            Dictionary<string, IList<JsonNode>> choices = names
                .Concat(new[] { UndeclaredArgumentName })
                .ToDictionary(n => n, n => Values(properties[n], offType), StringComparer.Ordinal);
            foreach (string[] set in sets)
            {
                string key = string.Join(",", set.OrderBy(n => n, StringComparer.Ordinal));
                if (!seen.Add(key))
                {
                    continue;
                }

                foreach (JsonObject input in Filled(set, choices))
                {
                    yield return input;
                }
            }
        }

        private static IEnumerable<string[]> Subsets(string[] names, int most)
        {
            yield return new string[0];
            List<string[]> previous = new List<string[]> { new string[0] };
            for (int size = 1; size <= most && size <= names.Length; size++)
            {
                List<string[]> next = new List<string[]>();
                foreach (string[] set in previous)
                {
                    int from = set.Length == 0 ? 0 : Array.IndexOf(names, set[set.Length - 1]) + 1;
                    for (int at = from; at < names.Length; at++)
                    {
                        string[] grown = set.Concat(new[] { names[at] }).ToArray();
                        next.Add(grown);
                        yield return grown;
                    }
                }

                previous = next;
            }
        }

        private static IEnumerable<JsonObject> Filled(string[] set, IDictionary<string, IList<JsonNode>> choices)
        {
            List<JsonObject> made = new List<JsonObject> { new JsonObject() };
            foreach (string name in set)
            {
                List<JsonObject> next = new List<JsonObject>();
                foreach (JsonObject partial in made)
                {
                    foreach (JsonNode value in choices[name])
                    {
                        if (next.Count >= MostValueCombinationsPerArgumentSet)
                        {
                            break;
                        }

                        JsonObject grown = partial.DeepClone().AsObject();
                        grown[name] = value == null ? null : value.DeepClone();
                        next.Add(grown);
                    }
                }

                made = next;
            }

            JsonObject first = new JsonObject();
            foreach (string name in set)
            {
                first[name] = choices[name][0] == null ? null : choices[name][0].DeepClone();
            }

            foreach (string name in set)
            {
                foreach (JsonNode value in choices[name].Skip(1))
                {
                    JsonObject varied = first.DeepClone().AsObject();
                    varied[name] = value == null ? null : value.DeepClone();
                    made.Add(varied);
                }
            }

            return made;
        }

        private static IList<JsonNode> Values(JsonNode shape, bool offType)
        {
            JsonObject form = shape as JsonObject;
            if (form == null)
            {
                return new JsonNode[] { JsonValue.Create(0) };
            }

            if (form.ContainsKey("const"))
            {
                return new[] { form["const"] };
            }

            JsonArray any = form["anyOf"] as JsonArray ?? form["oneOf"] as JsonArray;
            if (any != null)
            {
                return any.SelectMany(a => Values(a, offType))
                    .GroupBy(v => v == null ? "null" : v.ToJsonString(), StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();
            }

            IList<string> types = TypesOf(form);
            if (types.Count == 0)
            {
                return new JsonNode[]
                {
                    JsonValue.Create(0), JsonValue.Create("a"), JsonValue.Create(true), null,
                    new JsonArray(JsonValue.Create(0)), new JsonObject(),
                };
            }

            return types.SelectMany(t => ValuesOfType(form, t, offType)).ToList();
        }

        private static IEnumerable<JsonNode> ValuesOfType(JsonObject form, string type, bool offType)
        {
            switch (type)
            {
                case "string":
                    return offType
                        ? new JsonNode[] { JsonValue.Create("a"), JsonValue.Create(0), JsonValue.Create(string.Empty) }
                        : new JsonNode[] { JsonValue.Create("a") };

                case "number":
                case "integer":
                    double number = form["minimum"] != null ? form["minimum"].GetValue<double>() : 0;
                    double? most = form["maximum"] != null ? form["maximum"].GetValue<double>() : (double?)null;
                    if (most.HasValue && number > most.Value)
                    {
                        number = most.Value;
                    }

                    double fraction = most.HasValue && number + 0.5 > most.Value ? number - 0.5 : number + 0.5;

                    if (!offType)
                    {
                        return new JsonNode[] { JsonValue.Create(number) };
                    }

                    List<JsonNode> numbers = new List<JsonNode>
                    {
                        JsonValue.Create(number), JsonValue.Create(fraction), JsonValue.Create("a"),
                    };
                    if (!most.HasValue || most.Value >= BeyondInt32)
                    {
                        numbers.Add(JsonValue.Create(BeyondInt32));
                    }

                    if (!most.HasValue || most.Value >= BeyondSingle)
                    {
                        numbers.Add(JsonValue.Create(BeyondSingle));
                    }

                    if (form["minimum"] == null || form["minimum"].GetValue<double>() <= -1)
                    {
                        numbers.Add(JsonValue.Create(-1));
                    }

                    return numbers;

                case "boolean":
                    return offType
                        ? new JsonNode[] { JsonValue.Create(true), JsonValue.Create(0), JsonValue.Create(false) }
                        : new JsonNode[] { JsonValue.Create(true) };

                case "null":
                    return new JsonNode[] { null };

                case "array":
                    int count = form["minItems"] != null ? form["minItems"].GetValue<int>() : 1;
                    if (form["maxItems"] != null && count > form["maxItems"].GetValue<int>())
                    {
                        count = form["maxItems"].GetValue<int>();
                    }

                    bool unique = form["uniqueItems"] != null && form["uniqueItems"].GetValue<bool>();
                    IList<JsonNode> items = Values(form["items"], offType)
                        .Where(i => !unique || !IsNumber(i) || Math.Abs(NumberOf(i)) < BeyondSingle)
                        .ToList();
                    List<JsonNode> arrays = items.Select(item => Repeated(item, count)).ToList();
                    if (offType)
                    {
                        int? least = form["minItems"] != null ? form["minItems"].GetValue<int>() : (int?)null;
                        int? longest = form["maxItems"] != null ? form["maxItems"].GetValue<int>() : (int?)null;
                        if (!least.HasValue || least.Value == 0)
                        {
                            arrays.Add(new JsonArray());
                        }

                        if (!longest.HasValue || longest.Value > count)
                        {
                            arrays.Add(Repeated(items[0], count + 1));
                        }

                        if (count > 1 || !longest.HasValue || longest.Value >= 2)
                        {
                            arrays.Add(Repeated(items[0], Math.Max(count, 2)));
                        }

                        JsonArray forms = form["items"] is JsonObject itemForm
                            ? itemForm["anyOf"] as JsonArray ?? itemForm["oneOf"] as JsonArray
                            : null;
                        List<JsonNode> distinct = (forms == null
                                ? items
                                : forms.Select(f => Values(f, false).First()))
                            .GroupBy(i => i == null ? "null" : i.ToJsonString(), StringComparer.Ordinal)
                            .Select(g => g.First())
                            .Take(3)
                            .ToList();
                        if (distinct.Count > 1 && (!longest.HasValue || longest.Value >= distinct.Count))
                        {
                            arrays.Add(new JsonArray(distinct.Select(i => i == null ? null : i.DeepClone()).ToArray()));
                        }
                    }

                    return arrays;

                case "object":
                    JsonObject members = new JsonObject();
                    JsonObject declared = form["properties"] as JsonObject ?? new JsonObject();
                    foreach (JsonNode name in form["required"] as JsonArray ?? new JsonArray())
                    {
                        JsonNode value = Values(declared[name.GetValue<string>()], offType).First();
                        members[name.GetValue<string>()] = value == null ? null : value.DeepClone();
                    }

                    List<JsonNode> objects = new List<JsonNode> { members };
                    foreach (KeyValuePair<string, JsonNode> member in declared)
                    {
                        IEnumerable<JsonNode> others =
                            Values(member.Value, offType).Skip(members.ContainsKey(member.Key) ? 1 : 0);
                        foreach (JsonNode value in others)
                        {
                            JsonObject varied = members.DeepClone().AsObject();
                            varied[member.Key] = value == null ? null : value.DeepClone();
                            objects.Add(varied);
                        }
                    }

                    if (offType)
                    {
                        JsonObject extra = members.DeepClone().AsObject();
                        extra[UndeclaredArgumentName] = 0;
                        objects.Add(extra);
                    }

                    return objects;

                default:
                    throw new InvalidOperationException("知らない型: " + type);
            }
        }

        private static JsonNode Repeated(JsonNode item, int count)
        {
            JsonArray array = new JsonArray();
            for (int at = 0; at < count; at++)
            {
                array.Add(item == null ? null : item.DeepClone());
            }

            return array;
        }

        private static IList<string> TypesOf(JsonObject form)
        {
            JsonNode type = form["type"];
            if (type is JsonArray types)
            {
                return types.Select(t => t.GetValue<string>()).ToList();
            }

            return type == null ? new string[0] : new[] { type.GetValue<string>() };
        }

        private static IList<HostBranch> Branches(JsonObject source)
        {
            return source["branches"].AsArray()
                .Select(b => new HostBranch(
                    b.AsObject(),
                    KnownHostTypes.Value.Rules(Text(source, "tool"), b.AsObject(), new HostTypeCensus()),
                    KnownHostTypes.Value.Common(Text(source, "tool"), b.AsObject()),
                    !KnownHostTypes.Value.IsConnectorUpdate(Text(source, "tool"))))
                .ToList();
        }

        private static bool Any(JsonNode value)
        {
            return true;
        }

        private static bool IsNumber(JsonNode value)
        {
            return value != null && value.GetValueKind() == JsonValueKind.Number;
        }

        private static double NumberOf(JsonNode value)
        {
            return double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static bool IsSingle(JsonNode value)
        {
            return IsNumber(value) && Math.Abs(NumberOf(value)) <= float.MaxValue;
        }

        private static bool IsText(JsonNode value)
        {
            return value != null && value.GetValueKind() == JsonValueKind.String;
        }

        private static bool IsFilledText(JsonNode value)
        {
            return IsText(value) && value.GetValue<string>().Length > 0;
        }

        private static bool IsBoolean(JsonNode value)
        {
            return value != null
                && (value.GetValueKind() == JsonValueKind.True || value.GetValueKind() == JsonValueKind.False);
        }

        private static bool IsTrue(JsonNode value)
        {
            return value != null && value.GetValueKind() == JsonValueKind.True;
        }

        private static bool IsBase64(JsonNode value)
        {
            if (!IsText(value))
            {
                return false;
            }

            try
            {
                Convert.FromBase64String(value.GetValue<string>());

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool IsVersion(JsonNode value)
        {
            Version version;

            return IsText(value) && Version.TryParse(value.GetValue<string>(), out version);
        }

        private static Func<JsonNode, bool> IsIntegerIn(double least, double most)
        {
            return value =>
            {
                if (!IsNumber(value))
                {
                    return false;
                }

                double number = NumberOf(value);

                return number == Math.Floor(number) && number >= least && number <= most;
            };
        }

        private static readonly Func<JsonNode, bool> IsHandle = IsIntegerIn(long.MinValue, long.MaxValue);

        private static readonly Func<JsonNode, bool> IsIndex = IsIntegerIn(int.MinValue, int.MaxValue);

        private static readonly Func<JsonNode, bool> IsPosition = IsIntegerIn(0, int.MaxValue);

        private static Func<JsonNode, bool> NullOr(Func<JsonNode, bool> rule)
        {
            return value => value == null || rule(value);
        }

        private static bool IsNull(JsonNode value)
        {
            return value == null;
        }

        private static Func<JsonNode, bool> ArrayOf(Func<JsonNode, bool> element)
        {
            return value => value is JsonArray items && items.All(i => element(i));
        }

        private static Func<JsonNode, bool> FilledDistinctArrayOf(Func<JsonNode, bool> element)
        {
            return value => value is JsonArray items
                && items.Count > 0
                && items.All(i => element(i))
                && items.Select(i => i == null ? "null" : NumberOrText(i)).Distinct(StringComparer.Ordinal).Count()
                    == items.Count;
        }

        private static string NumberOrText(JsonNode value)
        {
            return IsNumber(value)
                ? NumberOf(value).ToString("R", CultureInfo.InvariantCulture)
                : value.ToJsonString();
        }

        private static Func<JsonNode, bool> NumbersOf(int length, Func<JsonNode, bool> each)
        {
            return value => value is JsonArray items && items.Count == length && items.All(i => each(i));
        }

        private static bool IsColor(JsonNode value)
        {
            return value is JsonArray items
                && (items.Count == 3 || items.Count == 4)
                && items.All(i => IsSingle(i) && NumberOf(i) >= 0 && NumberOf(i) <= 1);
        }

        private static Func<JsonNode, bool> Named(IList<string> names, bool combinable)
        {
            return value =>
            {
                if (!IsText(value))
                {
                    return false;
                }

                string[] parts = value.GetValue<string>().Split(new[] { ", " }, StringSplitOptions.None);

                return (parts.Length == 1 || combinable) && parts.All(p => names.Contains(p, StringComparer.Ordinal));
            };
        }

        private sealed class HostTypeCensus
        {
            public const string OverloadOfTheSameNames = "引数の名前まで同じ多重定義で型が1つに決まらない入力";

            public const string HostNumberNotToldIntegerOrReal = "合成ツールが読む数で、整数か実数か・範囲を照合しない入力";

            public const string HostTextNotToldChoices = "合成ツールが読む文字列で、選べる値を照合しない入力";

            public const string HostBooleanNotTold = "合成ツールが読む真偽で、真だけを受け取るかを照合しない入力";

            public const string FontFamilyNotInstalled = "書体の組(書体名が導入済みかを照合しない)";

            public const string ImageNotDecoded = "画像(Base64 までを照合し、画像として読めるかを照合しない)";

            public const string Checked = "型を照合する入力";

            public IDictionary<string, int> Kinds { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

            public IList<string> Underived { get; } = new List<string>();

            public void Count(string kind)
            {
                int counted;
                Kinds.TryGetValue(kind, out counted);
                Kinds[kind] = counted + 1;
            }
        }

        private sealed class HostTypes
        {
            private const string ConnectorRole = "connector";

            private const string HandleRole = "handleTarget";

            private const string ElementRole = "operationTarget";

            private const string DtoRole = "dto";

            private const string UpdateAction = "update";

            private static readonly string[] SdkArgumentNames = { "args", "argsList" };

            private static readonly string[] PropertyValueNames = { "value", "values" };

            private static readonly string[] RangeNames = { "range", "parentRange" };

            private static readonly string[] PositionListNames = { "indices", "parentIndices" };

            private static readonly string[] HandleListNames = { "handles", "parentHandles" };

            private static readonly string[] WholeNames = { "all", "parentAll", "selected" };

            private readonly IDictionary<string, string> _roles;

            private readonly IDictionary<string, int> _components;

            private readonly IDictionary<string, KeyValuePair<string, string>> _sources;

            private readonly IDictionary<string, ISet<string>> _fields;

            private readonly SdkMetadata _sdk;

            private readonly ISet<string> _rows;

            public HostTypes()
            {
                _rows = new HashSet<string>(
                    JsonNode.Parse(File.ReadAllText(Authored("tool-map.json")))["rows"].AsArray()
                        .Select(r => Text(r, "signatureKey")),
                    StringComparer.Ordinal);
                _roles = JsonNode.Parse(File.ReadAllText(Authored("type-roles.json")))["types"].AsArray()
                    .ToDictionary(t => Text(t, "typeName"), t => Text(t, "role"), StringComparer.Ordinal);
                JsonNode contract = JsonNode.Parse(File.ReadAllText(Authored("common-contract.json")));
                _components = contract["components"].AsArray()
                    .ToDictionary(c => Text(c, "typeName"), c => c["count"].GetValue<int>(), StringComparer.Ordinal);
                Regex head = new Regex("^対象 \\S+ / 動作 (\\S+) / 出所 (\\S+)$");
                _sources = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
                foreach (GeneratedToolDefinition definition in GeneratedToolDefinitions.Create())
                {
                    Match match = head.Match(FirstLine(definition.Description));
                    if (match.Success)
                    {
                        _sources[definition.Name] = new KeyValuePair<string, string>(
                            match.Groups[2].Value, Squeezed(match.Groups[1].Value));
                    }
                }

                _fields = Sources().ToDictionary(
                    s => s.Key, s => (ISet<string>)ListedFields(s.Value["output"]), StringComparer.Ordinal);
                _sdk = new SdkMetadata(SdkAssemblyPaths());
                Confirmed = ToolsTheHostConfirms();
                _bindings = HostBinding.Read(HostBindingPath());
            }

            private readonly IDictionary<string, HostBinding> _bindings;

            public ISet<string> Confirmed { get; }

            public Func<JsonObject, bool> Common(string tool, JsonObject branch)
            {
                HostBinding binding;
                bool dispatched = _bindings.TryGetValue(tool, out binding);
                bool suppressible = dispatched
                    ? !(binding.Section == "elements" && binding.ElementKind == "Add")
                    : branch["inputs"].AsArray().Any(i => Text(i, "name") == SuppressName);
                bool duplicates = !dispatched || binding.Edit == "DuplicateEdit";
                bool confirms = Confirmed.Contains(tool);

                return input =>
                {
                    if (confirms ? !IsTrue(input[ConfirmName]) : input.ContainsKey(ConfirmName))
                    {
                        return false;
                    }

                    if (!input.ContainsKey(SuppressName))
                    {
                        return true;
                    }

                    JsonNode suppress = input[SuppressName];

                    return suppressible
                        && IsBoolean(suppress)
                        && (!IsTrue(suppress) || (duplicates && !input.ContainsKey(PmxHandleName)));
                };
            }

            public IDictionary<string, Func<JsonNode, bool>> Rules(
                string tool, JsonObject branch, HostTypeCensus census)
            {
                List<JsonObject> inputs = branch["inputs"].AsArray().Select(i => i.AsObject()).ToList();
                List<JsonObject> positional = inputs
                    .Where(i => Text(i, "origin") == null)
                    .Concat(inputs
                        .Where(i => SdkArgumentNames.Contains(Text(i, "name")) && Text(i, "origin") != null)
                        .Select(i => (i["element"] ?? i)["members"] as JsonArray)
                        .Where(m => m != null)
                        .Select(m => m.Select(n => n.AsObject()).Where(n => Text(n, "origin") == null).ToList())
                        .FirstOrDefault() ?? new List<JsonObject>())
                    .ToList();
                Dictionary<string, Func<JsonNode, bool>> sdk =
                    new Dictionary<string, Func<JsonNode, bool>>(StringComparer.Ordinal);
                if (positional.Count > 0)
                {
                    string gap;
                    IList<string> types = ParameterTypes(
                        tool,
                        positional.Select(p => Text(p, "name")).ToList(),
                        branch["selector"] == null ? null : Text(branch["selector"], "value"),
                        out gap);
                    bool connectorUpdate = IsConnectorUpdate(tool);
                    for (int at = 0; at < positional.Count; at++)
                    {
                        JsonObject item = positional[at];
                        Func<JsonNode, bool> rule = types == null
                            ? null
                            : (connectorUpdate ? PropertyRule(types[at], census) : ArgumentRule(item, types[at], census));
                        if (item["injected"] == null || !item["injected"].GetValue<bool>())
                        {
                            Tally(census, rule, gap, tool, Text(item, "name"), types == null ? null : types[at]);
                        }

                        sdk[Text(item, "name")] = rule ?? Any;
                    }
                }

                Dictionary<string, Func<JsonNode, bool>> rules =
                    new Dictionary<string, Func<JsonNode, bool>>(StringComparer.Ordinal);
                foreach (JsonObject input in inputs)
                {
                    if (input["injected"] != null && input["injected"].GetValue<bool>())
                    {
                        continue;
                    }

                    string name = Text(input, "name");
                    rules[name] = Text(input, "origin") == null
                        ? sdk[name]
                        : Declared(
                            tool,
                            input,
                            name,
                            null,
                            false,
                            SdkArgumentNames.Contains(name),
                            PropertyValueNames.Contains(name),
                            branch["selector"] == null ? null : Text(branch["selector"], "value"),
                            sdk,
                            census);
                }

                return rules;
            }

            public bool IsConnectorUpdate(string tool)
            {
                KeyValuePair<string, string> source;

                return _sources.TryGetValue(tool, out source)
                    && Role(source.Key) == ConnectorRole
                    && source.Value == UpdateAction
                    && !_sdk.Methods(source.Key).Any(m => Squeezed(m.Name) == UpdateAction);
            }

            private static void Tally(
                HostTypeCensus census,
                Func<JsonNode, bool> rule,
                string gap,
                string tool,
                string name,
                string type)
            {
                if (rule != null)
                {
                    census.Count(HostTypeCensus.Checked);
                    if (type != null && Plain(type) == "System.Drawing.Font")
                    {
                        census.Count(HostTypeCensus.FontFamilyNotInstalled);
                    }

                    if (type != null && Plain(type) == "System.Drawing.Bitmap")
                    {
                        census.Count(HostTypeCensus.ImageNotDecoded);
                    }
                }
                else if (gap != null)
                {
                    census.Count(gap);
                }
                else
                {
                    census.Underived.Add(
                        tool + " " + name + (type == null ? "(型が決まらない)" : "(型 " + type + ")"));
                }
            }

            private Func<JsonNode, bool> Declared(
                string tool,
                JsonObject item,
                string name,
                string container,
                bool inArray,
                bool carriesArguments,
                bool carriesProperties,
                string itemType,
                IDictionary<string, Func<JsonNode, bool>> sdk,
                HostTypeCensus census)
            {
                if (Text(item, "origin") == null)
                {
                    if (carriesArguments && sdk.ContainsKey(name))
                    {
                        return sdk[name];
                    }

                    if (carriesProperties)
                    {
                        KeyValuePair<string, string> source;
                        IList<string> bound = FieldTypes(tool, itemType, new[] { name });
                        IList<string> types = bound != null
                            ? bound
                            : _sources.TryGetValue(tool, out source)
                                ? _sdk.PropertyTypes(source.Key, name)
                                : new string[0];
                        List<Func<JsonNode, bool>> rules = types.Select(t => PropertyRule(t, census)).ToList();
                        Func<JsonNode, bool> rule = rules.Count == 0 || rules.Any(r => r == null)
                            ? null
                            : (rules.Count == 1 ? rules[0] : value => rules.Any(r => r(value)));
                        Tally(census, rule, null, tool, name, types.Count == 1 ? types[0] : null);

                        return rule ?? Any;
                    }

                    census.Underived.Add(tool + " " + name + "(正本が形を書かず、引数にもプロパティにも当たらない)");

                    return Any;
                }

                bool top = container == null && !inArray;
                if (top && name == "assignments" && item["element"] != null)
                {
                    census.Count(HostTypeCensus.Checked);

                    return Assignments(item["element"]["members"].AsArray());
                }

                if (top && PositionListNames.Contains(name) && item["element"] != null)
                {
                    census.Count(HostTypeCensus.Checked);

                    return FilledDistinctArrayOf(IsPosition);
                }

                if (top && HandleListNames.Contains(name) && item["element"] != null)
                {
                    census.Count(HostTypeCensus.Checked);

                    return FilledDistinctArrayOf(IsHandle);
                }

                bool dispatched = _bindings.ContainsKey(tool);
                if (top && name == "itemType" && dispatched && _bindings[tool].Items.Count > 0)
                {
                    census.Count(HostTypeCensus.Checked);
                    IList<string> items = _bindings[tool].Items;

                    return value => IsText(value) && items.Contains(value.GetValue<string>(), StringComparer.Ordinal);
                }

                if (top && name == "fields" && dispatched && item["element"] != null)
                {
                    census.Count(HostTypeCensus.Checked);
                    ISet<string> readable = _fields[tool];

                    return FilledDistinctArrayOf(v => IsText(v) && readable.Contains(v.GetValue<string>()));
                }

                if (top && RangeNames.Contains(name) && item["members"] != null)
                {
                    census.Count(HostTypeCensus.Checked);

                    return value => value is JsonObject given
                        && given.ContainsKey("start") && IsPosition(given["start"])
                        && given.ContainsKey("count") && IsIntegerIn(1, int.MaxValue)(given["count"]);
                }

                if (item["element"] != null)
                {
                    return ArrayOf(Declared(
                        tool,
                        item["element"].AsObject(),
                        name,
                        container,
                        true,
                        carriesArguments,
                        carriesProperties,
                        itemType,
                        sdk,
                        census));
                }

                if (item["members"] != null)
                {
                    Dictionary<string, Func<JsonNode, bool>> members =
                        new Dictionary<string, Func<JsonNode, bool>>(StringComparer.Ordinal);
                    List<string> required = new List<string>();
                    foreach (JsonObject member in item["members"].AsArray().Select(m => m.AsObject()))
                    {
                        string memberName = Text(member, "name");
                        members[memberName] = Declared(
                            tool,
                            member,
                            memberName,
                            name,
                            false,
                            carriesArguments,
                            carriesProperties,
                            itemType,
                            sdk,
                            census);
                        bool sdkArgument = carriesArguments && Text(member, "origin") == null;
                        if (sdkArgument || (member["required"] != null && member["required"].GetValue<bool>()))
                        {
                            required.Add(memberName);
                        }
                    }

                    return value => value is JsonObject given
                        && given.All(g => members.ContainsKey(g.Key) && members[g.Key](g.Value))
                        && required.All(given.ContainsKey);
                }

                switch (Text(item, "shape"))
                {
                    case "boolean":
                        if (top && WholeNames.Contains(name))
                        {
                            census.Count(HostTypeCensus.Checked);
                            return IsTrue;
                        }

                        if (top && name == "runs")
                        {
                            census.Count(HostTypeCensus.Checked);
                            return IsBoolean;
                        }

                        census.Count(HostTypeCensus.HostBooleanNotTold);
                        return IsBoolean;

                    case "text":
                        if (top && name == "nameContains" && dispatched)
                        {
                            census.Count(HostTypeCensus.Checked);
                            return IsFilledText;
                        }

                        census.Count(HostTypeCensus.HostTextNotToldChoices);
                        return IsText;

                    case "number":
                        Func<JsonNode, bool> number = NumberRule(name, container, inArray, dispatched);
                        census.Count(
                            number == null ? HostTypeCensus.HostNumberNotToldIntegerOrReal : HostTypeCensus.Checked);

                        return number ?? IsNumber;

                    default:
                        census.Underived.Add(tool + " " + name + "(正本の形 " + item.ToJsonString() + ")");
                        return Any;
                }
            }

            private static Func<JsonNode, bool> Assignments(JsonArray members)
            {
                ISet<string> names = new HashSet<string>(members.Select(m => Text(m, "name")), StringComparer.Ordinal);

                return value =>
                {
                    if (!(value is JsonArray items) || items.Count == 0)
                    {
                        return false;
                    }

                    HashSet<string> kinds = new HashSet<string>(StringComparer.Ordinal);
                    HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JsonNode item in items)
                    {
                        if (!(item is JsonObject one) || !one.All(p => names.Contains(p.Key)))
                        {
                            return false;
                        }

                        bool byIndex = one.ContainsKey("parentIndex") && IsIndex(one["parentIndex"]);
                        bool byHandle = one.ContainsKey("parentHandle") && IsHandle(one["parentHandle"]);
                        if (byIndex == byHandle)
                        {
                            return false;
                        }

                        kinds.Add(byIndex ? "index" : "handle");
                        if (!(one["handles"] is JsonArray handles) || handles.Count == 0
                            || !handles.All(h => IsHandle(h) && used.Add(NumberOrText(h))))
                        {
                            return false;
                        }
                    }

                    return kinds.Count == 1;
                };
            }

            private static Func<JsonNode, bool> NumberRule(
                string name, string container, bool inArray, bool dispatched)
            {
                if (inArray)
                {
                    return name == "handles" ? IsHandle : null;
                }

                switch (name)
                {
                    case "parentHandle":
                        return IsHandle;

                    case "parentIndex":
                        return IsIndex;
                }

                if (container != null)
                {
                    return null;
                }

                switch (name)
                {
                    case "pmxHandle":
                    case "basePmxHandle":
                        return IsHandle;

                    case "offset":
                        return dispatched ? IsPosition : null;

                    case "limit":
                    case "count":
                        return dispatched ? IsIntegerIn(1, int.MaxValue) : null;

                    default:
                        return null;
                }
            }

            private IList<string> ParameterTypes(
                string tool, IList<string> names, string selected, out string gap)
            {
                gap = null;
                IList<string> bound = IsConnectorUpdate(tool)
                    ? FieldTypes(tool, null, names)
                    : ArgumentTypes(tool, names, selected);
                if (bound != null)
                {
                    return bound;
                }

                KeyValuePair<string, string> source;
                if (!_sources.TryGetValue(tool, out source))
                {
                    return null;
                }

                if (IsConnectorUpdate(tool))
                {
                    List<string> properties = names.Select(n => _sdk.PropertyType(source.Key, n)).ToList();

                    return properties.Any(p => p == null) ? null : properties;
                }

                List<IList<SdkParameter>> methods = _sdk.Methods(source.Key)
                    .Where(m => Squeezed(m.Name) == source.Value)
                    .Select(m => (IList<SdkParameter>)m.Parameters
                        .Where(p => !p.IsOut && Role(p.Type) != ConnectorRole)
                        .ToList())
                    .ToList();
                if (methods.Count == 0)
                {
                    return null;
                }

                List<IList<string>> named = Distinct(methods
                    .Where(m => m.Select(p => p.Name).SequenceEqual(names, StringComparer.Ordinal)));
                if (named.Count > 1)
                {
                    named = Distinct(_sdk.Methods(source.Key)
                        .Where(m => Squeezed(m.Name) == source.Value
                            && _rows.Contains(source.Key + "." + m.Name + "(" + string.Join(
                                ",",
                                m.Parameters.Select(p => p.IsOut ? "out " + p.Type.TrimEnd('&') : p.Type)) + ")"))
                        .Select(m => (IList<SdkParameter>)m.Parameters
                            .Where(p => !p.IsOut && Role(p.Type) != ConnectorRole)
                            .ToList())
                        .Where(m => m.Select(p => p.Name).SequenceEqual(names, StringComparer.Ordinal)));
                }

                if (named.Count > 1 && selected != null)
                {
                    List<int> differing = Enumerable.Range(0, names.Count)
                        .Where(at => named.Select(n => n[at]).Distinct(StringComparer.Ordinal).Count() > 1)
                        .ToList();
                    named = named.Where(n => differing.All(at => Spelling(n[at]) == selected)).ToList();
                }

                if (named.Count == 1)
                {
                    return named[0];
                }

                if (named.Count > 1)
                {
                    gap = HostTypeCensus.OverloadOfTheSameNames;
                    return null;
                }

                List<IList<string>> counted = Distinct(methods.Where(m => m.Count == names.Count));

                return counted.Count == 1 ? counted[0] : null;
            }

            /// <summary>1つに決まらなければ null。</summary>
            private IList<string> ArgumentTypes(string tool, IList<string> names, string selected)
            {
                HostBinding binding;
                if (!_bindings.TryGetValue(tool, out binding))
                {
                    return null;
                }

                List<IList<string>> found = binding.Calls
                    .Where(c => selected == null || c.Selector == null || c.Selector == selected)
                    .Select(c => c.Arguments
                        .Select(a => new KeyValuePair<string, string>(a.Key, SdkSpelled(a.Value)))
                        .Where(a => Role(a.Value) != ConnectorRole)
                        .ToList())
                    .Where(a => a.Select(p => p.Key).SequenceEqual(names, StringComparer.Ordinal))
                    .Select(a => (IList<string>)a.Select(p => p.Value).ToList())
                    .GroupBy(t => string.Join(",", t), StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();

                return found.Count == 1 ? found[0] : null;
            }

            /// <summary>1つでも引けなければ null。</summary>
            private IList<string> FieldTypes(string tool, string itemType, IList<string> names)
            {
                HostBinding binding;
                IDictionary<string, string> fields;
                if (!_bindings.TryGetValue(tool, out binding)
                    || !(binding.Fields.TryGetValue(itemType ?? string.Empty, out fields)
                        || binding.Fields.TryGetValue(string.Empty, out fields)))
                {
                    return null;
                }

                List<string> types = names.Select(n => fields.TryGetValue(n, out string type) ? SdkSpelled(type) : null)
                    .ToList();

                return types.Any(t => t == null) ? null : types;
            }

            private string SdkSpelled(string written)
            {
                if (written.EndsWith("[]", StringComparison.Ordinal))
                {
                    return SdkSpelled(written.Substring(0, written.Length - 2)) + "[]";
                }

                int open = written.IndexOf('<');
                if (open >= 0 && written.EndsWith(">", StringComparison.Ordinal))
                {
                    return written.Substring(0, open) + "<"
                        + string.Join(
                            ",",
                            written.Substring(open + 1, written.Length - open - 2).Split(',').Select(SdkSpelled))
                        + ">";
                }

                if (_sdk.Knows(written))
                {
                    return written;
                }

                for (int dot = written.LastIndexOf('.'); dot > 0; dot = written.LastIndexOf('.', dot - 1))
                {
                    string nested = written.Substring(0, dot) + "+" + written.Substring(dot + 1).Replace('.', '+');
                    if (_sdk.Knows(nested))
                    {
                        return nested;
                    }
                }

                return written;
            }

            private static string Spelling(string type)
            {
                switch (type)
                {
                    case "System.Byte[]":
                        return "base64";

                    case "System.String":
                        return "text";

                    case "System.Int32":
                    case "System.Single":
                    case "System.Double":
                        return "number";

                    default:
                        return null;
                }
            }

            private static List<IList<string>> Distinct(IEnumerable<IList<SdkParameter>> methods)
            {
                return methods
                    .Select(m => (IList<string>)m.Select(p => p.Type).ToList())
                    .GroupBy(t => string.Join(",", t), StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();
            }

            private string Role(string type)
            {
                string role;

                return _roles.TryGetValue(Plain(type), out role) ? role : null;
            }

            private static string Plain(string type)
            {
                return type.TrimEnd('&');
            }

            private static string ElementOf(string type)
            {
                if (type.EndsWith("[]", StringComparison.Ordinal))
                {
                    return type.Substring(0, type.Length - 2);
                }

                return Generic(type, "System.Collections.Generic.IList");
            }

            private Func<JsonNode, bool> ArgumentRule(JsonObject item, string type, HostTypeCensus census)
            {
                string plain = Plain(type);
                string element = ElementOf(plain);
                bool array = plain.EndsWith("[]", StringComparison.Ordinal);
                switch (Role(element ?? plain))
                {
                    case HandleRole:
                        if (element == null)
                        {
                            return IsHandle;
                        }

                        return array ? ArrayOf(IsHandle) : null;

                    case ElementRole:
                        return element == null ? NullOr(IsIndex) : (Func<JsonNode, bool>)IsNull;

                    case DtoRole:
                        if (element == null)
                        {
                            return Built(item["members"] as JsonArray, plain, census);
                        }

                        Func<JsonNode, bool> one = array && item["element"] != null
                            ? Built(item["element"]["members"] as JsonArray, element, census)
                            : null;

                        return one == null ? null : ArrayOf(one);
                }

                return ValueRule(plain);
            }

            private Func<JsonNode, bool> PropertyRule(string type, HostTypeCensus census)
            {
                string plain = Plain(type);
                string element = ElementOf(plain);
                switch (Role(element ?? plain))
                {
                    case ElementRole:
                        return element == null ? NullOr(IsIndex) : (Func<JsonNode, bool>)IsNull;

                    case HandleRole:
                    case DtoRole:
                        return null;
                }

                return ValueRule(plain);
            }

            private Func<JsonNode, bool> Built(JsonArray declared, string type, HostTypeCensus census)
            {
                if (declared == null)
                {
                    return null;
                }

                Dictionary<string, Func<JsonNode, bool>> members =
                    new Dictionary<string, Func<JsonNode, bool>>(StringComparer.Ordinal);
                foreach (JsonNode member in declared)
                {
                    string name = Text(member, "name");
                    string memberType = _sdk.MemberType(type, name);
                    Func<JsonNode, bool> rule = memberType == null ? null : ValueRule(memberType);
                    if (rule == null)
                    {
                        return null;
                    }

                    members[name] = rule;
                }

                return value => value is JsonObject given
                    && given.All(g => members.ContainsKey(g.Key) && members[g.Key](g.Value))
                    && members.Keys.All(given.ContainsKey);
            }

            private Func<JsonNode, bool> ValueRule(string type)
            {
                string underlying = Generic(type, "System.Nullable");
                if (underlying != null)
                {
                    Func<JsonNode, bool> inner = ValueCore(underlying);

                    return inner == null ? null : NullOr(inner);
                }

                Func<JsonNode, bool> core = ValueCore(type);
                bool? valueType = IsValueType(type);
                if (core == null || !valueType.HasValue)
                {
                    return null;
                }

                return valueType.Value ? core : NullOr(core);
            }

            private bool? IsValueType(string type)
            {
                if (ElementOf(type) != null)
                {
                    return false;
                }

                switch (type)
                {
                    case "System.Boolean":
                    case "System.Byte":
                    case "System.Int32":
                    case "System.Single":
                    case "System.Double":
                        return true;

                    case "System.String":
                    case "System.Object":
                    case "System.Version":
                        return false;
                }

                return _sdk.IsValueType(type);
            }

            private Func<JsonNode, bool> ValueCore(string type)
            {
                string element = ElementOf(type);
                if (element != null)
                {
                    if (element == "System.Byte")
                    {
                        return IsBase64;
                    }

                    Func<JsonNode, bool> each = ValueRule(element);

                    return each == null ? null : ArrayOf(each);
                }

                if (_sdk.IsEnum(type))
                {
                    return Named(_sdk.EnumNames(type), _sdk.IsFlags(type));
                }

                int length;
                if (_components.TryGetValue(type, out length))
                {
                    return NumbersOf(length, IsSingle);
                }

                switch (type)
                {
                    case "System.Boolean":
                        return IsBoolean;

                    case "System.Byte":
                        return IsIntegerIn(byte.MinValue, byte.MaxValue);

                    case "System.Int32":
                        return IsIndex;

                    case "System.Single":
                        return IsSingle;

                    case "System.Double":
                        return IsNumber;

                    case "System.String":
                        return IsText;

                    case "System.Version":
                        return IsVersion;

                    case "System.Object":
                        return Any;

                    case "System.Drawing.Color":
                    case "System.Drawing.Brush":
                        return IsColor;

                    case "System.Drawing.Point":
                    case "System.Drawing.Size":
                        return NumbersOf(2, IsIndex);

                    case "System.Drawing.Rectangle":
                        return NumbersOf(4, IsIndex);

                    case "System.Drawing.Bitmap":
                        return IsBase64;

                    case "System.Drawing.Font":
                        Func<JsonNode, bool> style = Named(
                            _sdk.EnumNames("System.Drawing.FontStyle"), _sdk.IsFlags("System.Drawing.FontStyle"));
                        string[] names = { "family", "size", "style" };

                        return value => value is JsonObject font
                            && font.All(p => names.Contains(p.Key))
                            && IsFilledText(font["family"])
                            && IsSingle(font["size"]) && NumberOf(font["size"]) > 0
                            && style(font["style"]);

                    default:
                        return null;
                }
            }

            private static string Generic(string type, string definition)
            {
                string opening = definition + "<";

                return type.StartsWith(opening, StringComparison.Ordinal) && type.EndsWith(">", StringComparison.Ordinal)
                    ? type.Substring(opening.Length, type.Length - opening.Length - 1)
                    : null;
            }

            private static ISet<string> ListedFields(JsonNode output)
            {
                JsonNode answer = output;
                while (answer != null && answer["members"] == null && answer["element"] != null)
                {
                    answer = answer["element"];
                }

                JsonArray members = answer == null ? null : answer["members"] as JsonArray;
                JsonNode items = members == null
                    ? null
                    : members.FirstOrDefault(m => Text(m, "name") == "items");
                JsonArray listed = items == null || items["element"] == null
                    ? null
                    : items["element"]["members"] as JsonArray;

                return new HashSet<string>(
                    listed == null ? new string[0] : listed.Select(m => Text(m, "name")),
                    StringComparer.Ordinal);
            }

            private static string Squeezed(string member)
            {
                return member.Replace("_", string.Empty).ToLowerInvariant();
            }
        }

        private static string HostBindingPath()
        {
            string path = typeof(ShippedToolDefinitionTests).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "HostBindingPath")
                .Select(a => a.Value)
                .SingleOrDefault();
            Assert.True(path != null && File.Exists(path), "ホストのツールの結び付きが無い: " + path);

            return path;
        }

        private sealed class HostBinding
        {
            private static readonly Regex EntryPattern = new Regex(
                "(calls|aggregations|elements|preconditions)\\.Add\\(\"([a-z0-9_]+)\", ");

            private static readonly Regex EditPattern = new Regex("EditKind\\.([A-Za-z]+)");

            private static readonly Regex ElementPattern = new Regex("ToolElementKind\\.([A-Za-z]+)");

            private static readonly Regex ItemPattern = new Regex("new ToolItem\\(\"([a-z0-9_]+)\"");

            private static readonly Regex CallPattern = new Regex("new ToolCall\\(\"");

            private static readonly Regex ArgumentsPattern = new Regex("new ToolArgument\\[\\] \\{");

            private static readonly Regex ArgumentPattern =
                new Regex("new ToolArgument\\(\"([A-Za-z0-9_]+)\", typeof\\(global::([^)]+)\\)");

            private static readonly Regex SelectorPattern = new Regex("selectorValue: \"([^\"]+)\"");

            private static readonly Regex FieldSetPattern = new Regex("new ToolFieldSet\\((?:null|\"([a-z0-9_]+)\")");

            private static readonly Regex FieldPattern =
                new Regex("new ToolField\\(\"([A-Za-z0-9_]+)\", \"[^\"]*\", typeof\\(global::([^)]+)\\)");

            private HostBinding(
                string section,
                string edit,
                string elementKind,
                IList<string> items,
                IList<HostCall> calls,
                IDictionary<string, IDictionary<string, string>> fields)
            {
                Section = section;
                Edit = edit;
                ElementKind = elementKind;
                Items = items;
                Calls = calls;
                Fields = fields;
            }

            public string Section { get; }

            public string Edit { get; }

            public string ElementKind { get; }

            public IList<string> Items { get; }

            /// <summary>型は C# の綴りのまま。</summary>
            public IList<HostCall> Calls { get; }

            /// <summary>要素の型を分けない組は空の名前で引く。</summary>
            public IDictionary<string, IDictionary<string, string>> Fields { get; }

            public static IDictionary<string, HostBinding> Read(string path)
            {
                string text = File.ReadAllText(path);
                MatchCollection entries = EntryPattern.Matches(text);
                Dictionary<string, HostBinding> bindings =
                    new Dictionary<string, HostBinding>(StringComparer.Ordinal);
                for (int at = 0; at < entries.Count; at++)
                {
                    Match entry = entries[at];
                    if (entry.Groups[1].Value == "preconditions")
                    {
                        continue;
                    }

                    int end = at + 1 < entries.Count ? entries[at + 1].Index : text.Length;
                    string body = text.Substring(entry.Index, end - entry.Index);
                    Match edit = EditPattern.Match(body);
                    Match element = ElementPattern.Match(body);
                    bindings[entry.Groups[2].Value] = new HostBinding(
                        entry.Groups[1].Value,
                        edit.Success ? edit.Groups[1].Value : null,
                        element.Success ? element.Groups[1].Value : null,
                        ItemPattern.Matches(body).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList(),
                        entry.Groups[1].Value == "calls" ? CallsIn(body) : new HostCall[0],
                        entry.Groups[1].Value == "aggregations"
                            ? FieldsIn(body)
                            : new Dictionary<string, IDictionary<string, string>>(StringComparer.Ordinal));
                }

                return bindings;
            }

            private static IList<HostCall> CallsIn(string body)
            {
                List<int> starts = CallPattern.Matches(body).Cast<Match>().Select(m => m.Index).ToList();
                List<HostCall> calls = new List<HostCall>();
                for (int at = 0; at < starts.Count; at++)
                {
                    string call = body.Substring(
                        starts[at], (at + 1 < starts.Count ? starts[at + 1] : body.Length) - starts[at]);
                    MatchCollection lists = ArgumentsPattern.Matches(call);
                    string arguments = lists.Count < 2
                        ? string.Empty
                        : call.Substring(lists[0].Index, lists[1].Index - lists[0].Index);
                    Match selector = SelectorPattern.Match(call);
                    calls.Add(new HostCall(
                        selector.Success ? selector.Groups[1].Value : null,
                        ArgumentPattern.Matches(arguments).Cast<Match>()
                            .Select(m => new KeyValuePair<string, string>(m.Groups[1].Value, m.Groups[2].Value))
                            .ToList()));
                }

                return calls;
            }

            private static IDictionary<string, IDictionary<string, string>> FieldsIn(string body)
            {
                Dictionary<string, IDictionary<string, string>> sets =
                    new Dictionary<string, IDictionary<string, string>>(StringComparer.Ordinal);
                IDictionary<string, string> current = null;
                foreach (Match found in FieldSetPattern.Matches(body).Cast<Match>()
                    .Concat(FieldPattern.Matches(body).Cast<Match>())
                    .OrderBy(m => m.Index))
                {
                    if (found.Value.StartsWith("new ToolFieldSet", StringComparison.Ordinal))
                    {
                        current = new Dictionary<string, string>(StringComparer.Ordinal);
                        sets[found.Groups[1].Success ? found.Groups[1].Value : string.Empty] = current;
                    }
                    else if (current != null)
                    {
                        current[found.Groups[1].Value] = found.Groups[2].Value;
                    }
                }

                return sets;
            }
        }

        private sealed class HostCall
        {
            public HostCall(string selector, IList<KeyValuePair<string, string>> arguments)
            {
                Selector = selector;
                Arguments = arguments;
            }

            public string Selector { get; }

            public IList<KeyValuePair<string, string>> Arguments { get; }
        }

        internal static IList<string> SdkAssemblyPaths()
        {
            string editor = typeof(ShippedToolDefinitionTests).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .Cast<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "PmxEditorDir")
                .Select(a => a.Value)
                .SingleOrDefault();
            Assert.False(string.IsNullOrEmpty(editor), "テストのアセンブリに PmxEditorDir が無い。");
            string framework = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Microsoft.NET",
                "Framework64",
                "v4.0.30319");
            string[] paths =
            {
                Path.Combine(editor, "Lib", "PEPlugin", "PEPlugin.dll"),
                Path.Combine(editor, "Lib", "SlimDX", "x64", "SlimDX.dll"),
                Path.Combine(framework, "System.Drawing.dll"),
                Path.Combine(framework, "System.Windows.Forms.dll"),
            };
            foreach (string path in paths)
            {
                Assert.True(File.Exists(path), "型を読むアセンブリが無い: " + path);
            }

            return paths;
        }

        internal sealed class SdkParameter
        {
            public SdkParameter(string name, string type, bool isOut)
            {
                Name = name;
                Type = type;
                IsOut = isOut;
            }

            public string Name { get; }

            public string Type { get; }

            public bool IsOut { get; }
        }

        internal sealed class SdkMethod
        {
            public SdkMethod(string name, IList<SdkParameter> parameters)
            {
                Name = name;
                Parameters = parameters;
            }

            public string Name { get; }

            public IList<SdkParameter> Parameters { get; }
        }

        private sealed class SdkMember
        {
            public SdkMember(string name, string type, bool writable)
            {
                Name = name;
                Type = type;
                Writable = writable;
            }

            public string Name { get; }

            public string Type { get; }

            public bool Writable { get; }
        }

        private sealed class SdkType
        {
            public SdkType(
                string name,
                bool primary,
                string baseType,
                bool isFlags,
                IList<string> enumNames,
                IList<string> interfaces,
                IList<SdkMember> members,
                IList<SdkMethod> methods)
            {
                Name = name;
                Primary = primary;
                BaseType = baseType;
                IsFlags = isFlags;
                EnumNames = enumNames;
                Interfaces = interfaces;
                Members = members;
                Methods = methods;
            }

            public string Name { get; }

            public bool Primary { get; }

            public string BaseType { get; }

            public bool IsEnum
            {
                get { return BaseType == "System.Enum"; }
            }

            public bool IsFlags { get; }

            public IList<string> EnumNames { get; }

            public IList<string> Interfaces { get; }

            public IList<SdkMember> Members { get; }

            public IList<SdkMethod> Methods { get; }
        }

        internal sealed class SdkMetadata
        {
            private readonly Dictionary<string, SdkType> _types =
                new Dictionary<string, SdkType>(StringComparer.Ordinal);

            public SdkMetadata(IList<string> paths)
            {
                for (int at = 0; at < paths.Count; at++)
                {
                    Read(paths[at], at == 0);
                }
            }

            private void Read(string path, bool primary)
            {
                using (FileStream stream = File.OpenRead(path))
                using (PEReader image = new PEReader(stream))
                {
                    MetadataReader reader = image.GetMetadataReader();
                    SdkTypeNames names = new SdkTypeNames();
                    foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
                    {
                        TypeDefinition definition = reader.GetTypeDefinition(handle);
                        string name = names.GetTypeFromDefinition(reader, handle, 0);
                        if (_types.ContainsKey(name))
                        {
                            continue;
                        }

                        string baseType = definition.BaseType.IsNil ? null : names.Of(reader, definition.BaseType);
                        bool isFlags = definition.GetCustomAttributes()
                            .Select(reader.GetCustomAttribute)
                            .Any(a => AttributeType(reader, names, a) == "System.FlagsAttribute");
                        List<string> interfaces = definition.GetInterfaceImplementations()
                            .Select(i => names.Of(reader, reader.GetInterfaceImplementation(i).Interface))
                            .Concat(baseType == null ? new string[0] : new[] { baseType })
                            .ToList();
                        List<FieldDefinition> fields = definition.GetFields().Select(reader.GetFieldDefinition).ToList();
                        List<string> enumNames = fields
                            .Where(f => (f.Attributes & FieldAttributes.Literal) != 0
                                && (f.Attributes & FieldAttributes.Static) != 0)
                            .Select(f => reader.GetString(f.Name))
                            .ToList();
                        List<SdkMember> members = definition.GetProperties()
                            .Select(reader.GetPropertyDefinition)
                            .Select(p => new SdkMember(
                                reader.GetString(p.Name),
                                p.DecodeSignature(names, null).ReturnType,
                                !p.GetAccessors().Setter.IsNil))
                            .Concat(fields
                                .Where(f => (f.Attributes & FieldAttributes.Public) != 0
                                    && (f.Attributes & FieldAttributes.Static) == 0)
                                .Select(f => new SdkMember(
                                    reader.GetString(f.Name),
                                    f.DecodeSignature(names, null),
                                    (f.Attributes & FieldAttributes.InitOnly) == 0)))
                            .ToList();
                        List<SdkMethod> methods = new List<SdkMethod>();
                        foreach (MethodDefinition method in definition.GetMethods().Select(reader.GetMethodDefinition))
                        {
                            if ((method.Attributes & MethodAttributes.SpecialName) != 0)
                            {
                                continue;
                            }

                            MethodSignature<string> signature = method.DecodeSignature(names, null);
                            string[] parameterNames = new string[signature.ParameterTypes.Length];
                            bool[] outs = new bool[signature.ParameterTypes.Length];
                            foreach (Parameter parameter in method.GetParameters().Select(reader.GetParameter))
                            {
                                if (parameter.SequenceNumber == 0 || parameter.SequenceNumber > parameterNames.Length)
                                {
                                    continue;
                                }

                                parameterNames[parameter.SequenceNumber - 1] = reader.GetString(parameter.Name);
                                outs[parameter.SequenceNumber - 1] =
                                    (parameter.Attributes & ParameterAttributes.Out) != 0;
                            }

                            methods.Add(new SdkMethod(
                                reader.GetString(method.Name),
                                signature.ParameterTypes
                                    .Select((t, at) => new SdkParameter(
                                        parameterNames[at],
                                        t,
                                        outs[at] && t.EndsWith("&", StringComparison.Ordinal)))
                                    .ToList()));
                        }

                        _types[name] = new SdkType(
                            name, primary, baseType, isFlags, enumNames, interfaces, members, methods);
                    }
                }
            }

            private static string AttributeType(MetadataReader reader, SdkTypeNames names, CustomAttribute attribute)
            {
                switch (attribute.Constructor.Kind)
                {
                    case HandleKind.MemberReference:
                        MemberReference reference = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);

                        return reference.Parent.Kind == HandleKind.TypeReference
                            || reference.Parent.Kind == HandleKind.TypeDefinition
                            ? names.Of(reader, (EntityHandle)reference.Parent)
                            : null;

                    case HandleKind.MethodDefinition:
                        MethodDefinition method = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);

                        return names.GetTypeFromDefinition(reader, method.GetDeclaringType(), 0);

                    default:
                        return null;
                }
            }

            public bool IsEnum(string type)
            {
                SdkType found;

                return _types.TryGetValue(type, out found) && found.IsEnum;
            }

            public bool IsFlags(string type)
            {
                SdkType found;

                return _types.TryGetValue(type, out found) && found.IsFlags;
            }

            public IList<string> EnumNames(string type)
            {
                SdkType found;

                return _types.TryGetValue(type, out found) ? found.EnumNames : new string[0];
            }

            public bool Knows(string type)
            {
                return _types.ContainsKey(type);
            }

            public bool? IsValueType(string type)
            {
                SdkType found;
                if (!_types.TryGetValue(type, out found))
                {
                    return null;
                }

                return found.BaseType == "System.ValueType" || found.BaseType == "System.Enum";
            }

            public IEnumerable<SdkMethod> Methods(string type)
            {
                return Lineage(type).SelectMany(t => t.Methods)
                    .Concat(Outside(type).SelectMany(OutsideMethods));
            }

            public string PropertyType(string type, string name)
            {
                IList<string> found = PropertyTypes(type, name);

                return found.Count == 1 ? found[0] : null;
            }

            public IList<string> PropertyTypes(string type, string name)
            {
                List<string> found = MemberTypes(Lineage(type), name, false);
                if (found.Count == 0)
                {
                    SdkType head;
                    ISet<string> shared = _types.TryGetValue(type, out head)
                        ? new HashSet<string>(
                            head.Interfaces.Where(i => _types.ContainsKey(i) && _types[i].Primary),
                            StringComparer.Ordinal)
                        : new HashSet<string>(StringComparer.Ordinal);
                    found = MemberTypes(
                        _types.Values
                            .Where(t => t.Primary && Lineage(t.Name).Skip(1).Any(l => shared.Contains(l.Name)))
                            .SelectMany(t => Lineage(t.Name)),
                        name,
                        false);
                }

                return found;
            }

            public string MemberType(string type, string name)
            {
                List<string> found = MemberTypes(Lineage(type), name, true);

                return found.Count == 1 ? found[0] : null;
            }

            private static List<string> MemberTypes(IEnumerable<SdkType> types, string name, bool writable)
            {
                return types
                    .SelectMany(t => t.Members)
                    .Where(m => Squeezed(m.Name) == Squeezed(name) && (!writable || m.Writable))
                    .Select(m => m.Type)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            private static string Squeezed(string name)
            {
                return name.Replace("_", string.Empty).ToLowerInvariant();
            }

            private IEnumerable<string> Outside(string type)
            {
                return Lineage(type).SelectMany(t => t.Interfaces).Where(i => !_types.ContainsKey(i)).Distinct();
            }

            private static IEnumerable<SdkMethod> OutsideMethods(string type)
            {
                int open = type.IndexOf('<');
                if (open < 0 || !type.EndsWith(">", StringComparison.Ordinal))
                {
                    return new SdkMethod[0];
                }

                string[] arguments = type.Substring(open + 1, type.Length - open - 2).Split(',');
                Type definition = Type.GetType(type.Substring(0, open) + "`" + arguments.Length);
                if (definition == null)
                {
                    return new SdkMethod[0];
                }

                return definition.GetMethods().Select(m => new SdkMethod(
                    m.Name,
                    m.GetParameters()
                        .Select(p => new SdkParameter(
                            p.Name,
                            p.ParameterType.IsGenericParameter
                                ? arguments[p.ParameterType.GenericParameterPosition]
                                : p.ParameterType.FullName,
                            p.IsOut))
                        .ToList()));
            }

            private IEnumerable<SdkType> Lineage(string type)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                Queue<string> pending = new Queue<string>(new[] { type });
                while (pending.Count > 0)
                {
                    string next = pending.Dequeue();
                    SdkType found;
                    if (!seen.Add(next) || !_types.TryGetValue(next, out found) || next == "System.Object")
                    {
                        continue;
                    }

                    yield return found;
                    foreach (string inherited in found.Interfaces)
                    {
                        pending.Enqueue(inherited);
                    }
                }
            }
        }

        internal sealed class SdkTypeNames : ISignatureTypeProvider<string, object>
        {
            public string Of(MetadataReader reader, EntityHandle handle)
            {
                switch (handle.Kind)
                {
                    case HandleKind.TypeDefinition:
                        return GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0);

                    case HandleKind.TypeReference:
                        return GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0);

                    case HandleKind.TypeSpecification:
                        return GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)handle, 0);

                    default:
                        throw new InvalidOperationException("型を指さない: " + handle.Kind);
                }
            }

            public string GetArrayType(string elementType, ArrayShape shape)
            {
                return elementType + "[" + new string(',', shape.Rank - 1) + "]";
            }

            public string GetByReferenceType(string elementType)
            {
                return elementType + "&";
            }

            public string GetFunctionPointerType(MethodSignature<string> signature)
            {
                return "method";
            }

            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            {
                return genericType + "<" + string.Join(",", typeArguments) + ">";
            }

            public string GetGenericMethodParameter(object genericContext, int index)
            {
                return "!!" + index;
            }

            public string GetGenericTypeParameter(object genericContext, int index)
            {
                return "!" + index;
            }

            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
            {
                return unmodifiedType;
            }

            public string GetPinnedType(string elementType)
            {
                return elementType;
            }

            public string GetPointerType(string elementType)
            {
                return elementType + "*";
            }

            public string GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return "System." + typeCode;
            }

            public string GetSZArrayType(string elementType)
            {
                return elementType + "[]";
            }

            public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                TypeDefinition definition = reader.GetTypeDefinition(handle);
                string name = WithoutArity(reader.GetString(definition.Name));
                if (definition.IsNested)
                {
                    return GetTypeFromDefinition(reader, definition.GetDeclaringType(), rawTypeKind) + "+" + name;
                }

                string space = reader.GetString(definition.Namespace);

                return space.Length == 0 ? name : space + "." + name;
            }

            public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                TypeReference reference = reader.GetTypeReference(handle);
                string name = WithoutArity(reader.GetString(reference.Name));
                if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
                {
                    return GetTypeFromReference(reader, (TypeReferenceHandle)reference.ResolutionScope, rawTypeKind)
                        + "+" + name;
                }

                string space = reader.GetString(reference.Namespace);

                return space.Length == 0 ? name : space + "." + name;
            }

            public string GetTypeFromSpecification(
                MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
            }

            private static string WithoutArity(string name)
            {
                int tick = name.IndexOf('`');

                return tick < 0 ? name : name.Substring(0, tick);
            }
        }

        private sealed class HostBranch
        {
            public HostBranch(
                JsonObject branch,
                IDictionary<string, Func<JsonNode, bool>> rules,
                Func<JsonObject, bool> common,
                bool argumentsRequired)
            {
                List<string> names = new List<string>();
                List<string> required = new List<string>();
                foreach (JsonNode input in branch["inputs"].AsArray())
                {
                    if (input["injected"] != null && input["injected"].GetValue<bool>())
                    {
                        continue;
                    }

                    names.Add(Text(input, "name"));
                    if ((input["required"] != null && input["required"].GetValue<bool>())
                        || (argumentsRequired && Text(input, "origin") == null))
                    {
                        required.Add(Text(input, "name"));
                    }
                }

                Names = names;
                Required = required;
                Rules = rules;
                Common = common;
                Choices = (branch["choices"] as JsonArray ?? new JsonArray())
                    .Select(c => new HostChoice(
                        c["names"].AsArray().Select(n => n.GetValue<string>()).ToList(),
                        c["required"].GetValue<bool>()))
                    .ToList();
                JsonNode selector = branch["selector"];
                Selector = selector == null ? null : Text(selector, "name");
                SelectorValue = selector == null ? null : Text(selector, "value");
            }

            public IList<string> Names { get; }

            public IList<string> Required { get; }

            public IDictionary<string, Func<JsonNode, bool>> Rules { get; }

            public Func<JsonObject, bool> Common { get; }

            public IList<HostChoice> Choices { get; }

            public string Selector { get; }

            public string SelectorValue { get; }

            public bool Fits(JsonObject input)
            {
                ISet<string> given = new HashSet<string>(
                    input.Select(p => p.Key), StringComparer.Ordinal);
                given.Remove(ConfirmName);
                given.Remove(SuppressName);
                if (!Common(input)
                    || !Required.All(given.Contains)
                    || !given.All(Names.Contains)
                    || !given.All(n => Rules[n](input[n])))
                {
                    return false;
                }

                foreach (HostChoice choice in Choices)
                {
                    int taken = choice.Names.Count(given.Contains);
                    if (taken > 1 || (choice.Required && taken == 0))
                    {
                        return false;
                    }
                }

                if (Selector == null)
                {
                    return true;
                }

                JsonValue value = input[Selector] as JsonValue;
                string chosen;

                return value != null
                    && value.TryGetValue(out chosen)
                    && string.Equals(chosen, SelectorValue, StringComparison.Ordinal);
            }
        }

        private sealed class HostChoice
        {
            public HostChoice(IList<string> names, bool required)
            {
                Names = names;
                Required = required;
            }

            public IList<string> Names { get; }

            public bool Required { get; }
        }

        private static IDictionary<string, JsonObject> Sources()
        {
            return JsonNode.Parse(File.ReadAllText(Authored("tool-schemas.json")))["tools"].AsArray()
                .Select(t => t.AsObject())
                .ToDictionary(t => Text(t, "tool"), t => t, StringComparer.Ordinal);
        }

        private static ISet<string> ToolsTheHostConfirms()
        {
            Regex noted = new Regex("危険操作\\([^)]+\\)。該当は ([^(。]+)");
            Regex head = new Regex("^対象 \\S+ / 動作 (\\S+) / 出所 (\\S+)$");
            ILookup<string, string> toolsByTypeAndMember = GeneratedToolDefinitions.Create()
                .Select(d => new KeyValuePair<string, Match>(d.Name, head.Match(FirstLine(d.Description))))
                .Where(d => d.Value.Success)
                .ToLookup(
                    d => TypeAndMember(
                        d.Value.Groups[2].Value.Substring(d.Value.Groups[2].Value.LastIndexOf('.') + 1),
                        d.Value.Groups[1].Value),
                    d => d.Key,
                    StringComparer.Ordinal);
            HashSet<string> tools = new HashSet<string>(StringComparer.Ordinal);
            JsonArray capabilities =
                JsonNode.Parse(File.ReadAllText(Observed("capability-ledger.json")))["capabilities"].AsArray();
            foreach (JsonNode capability in capabilities)
            {
                string remarks = Text(capability, "remarks");
                if (remarks == null)
                {
                    continue;
                }

                string type = Text(capability, "target").Split('.')[0];
                foreach (Match match in noted.Matches(remarks))
                {
                    string key = TypeAndMember(type, match.Groups[1].Value);
                    Assert.True(
                        toolsByTypeAndMember.Contains(key),
                        "台帳が危険操作と記したメンバーのツールが見つからない: " + Text(capability, "id") + " " + key);
                    tools.UnionWith(toolsByTypeAndMember[key]);
                }
            }

            JsonArray overwriting =
                JsonNode.Parse(File.ReadAllText(Authored("common-contract.json")))["overwritingTools"].AsArray();
            tools.UnionWith(overwriting.Select(t => Text(t, "tool")));
            tools.Remove(ClearingOnlyTheModelItsHandlePointsAt);

            return tools;
        }

        private static string TypeAndMember(string type, string member)
        {
            return type + " " + member.Replace("_", string.Empty).ToLowerInvariant();
        }

        private static string Authored(string name)
        {
            return Catalog("authored", name);
        }

        private static string Observed(string name)
        {
            return Catalog("observed", name);
        }

        private static string Catalog(string part, string name)
        {
            for (DirectoryInfo at = new DirectoryInfo(AppContext.BaseDirectory); at != null; at = at.Parent)
            {
                string path = Path.Combine(at.FullName, "catalog", part, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("正本が見つからない: " + part + "/" + name);
        }
    }
}
