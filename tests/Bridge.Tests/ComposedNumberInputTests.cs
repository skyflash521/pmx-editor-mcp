using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class ComposedNumberInputTests
    {
        private const string IntegerReads = "integer";

        [Fact]
        public void EveryNumberInputOfAComposedToolTakesTheTypeTheHostReads()
        {
            IDictionary<string, string> reads = HostReads();
            IDictionary<string, JsonObject> schemas = GeneratedToolDefinitions.Create()
                .ToDictionary(d => d.Name, d => SchemaRefs.Inlined(JsonNode.Parse(d.InputSchema)).AsObject(), StringComparer.Ordinal);
            List<string> wrong = new List<string>();
            foreach (string input in ComposedNumberInputs())
            {
                string read;
                if (!reads.TryGetValue(input, out read))
                {
                    wrong.Add(input + ": ホストの読み取りの表に無い");

                    continue;
                }

                string tool = input.Substring(0, input.IndexOf(' '));
                string path = input.Substring(input.IndexOf(' ') + 1);
                IList<JsonObject> forms = FormsAt(schemas[tool], path);
                if (forms.Count == 0)
                {
                    wrong.Add(input + ": ツール定義に形が無い");

                    continue;
                }

                string wanted = read == IntegerReads ? "integer" : "number";
                foreach (JsonObject form in forms.Where(f => !Types(f).Contains(wanted)
                    || Types(f).Contains(read == IntegerReads ? "number" : "integer")))
                {
                    wrong.Add(input + ": ホストは " + read + " で読むが、ツール定義は " + form.ToJsonString());
                }
            }

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void EveryNumberInputOfAComposedToolStaysWithinTheRangeTheHostAccepts()
        {
            IDictionary<string, JsonObject> rows = HostRows("inputs");
            IDictionary<string, JsonObject> schemas = Schemas();
            List<string> wrong = new List<string>();
            foreach (string input in ComposedNumberInputs())
            {
                JsonObject row;
                if (!rows.TryGetValue(input, out row))
                {
                    wrong.Add(input + ": ホストの読み取りの表に無い");

                    continue;
                }

                double? least = Number(row, "least");
                double? most = Number(row, "most");
                if (least == null || most == null)
                {
                    wrong.Add(input + ": ホストの読み取りの表に、受け取る下限と上限が無い");

                    continue;
                }

                bool leastExcluded = row["leastExcluded"] != null && row["leastExcluded"].GetValue<bool>();
                foreach (JsonObject form in Forms(schemas, input, wrong))
                {
                    double? minimum = Number(form, "minimum");
                    double? exclusiveMinimum = Number(form, "exclusiveMinimum");
                    bool floored = leastExcluded
                        ? (minimum.HasValue && minimum.Value > least.Value)
                            || (exclusiveMinimum.HasValue && exclusiveMinimum.Value >= least.Value)
                        : (minimum.HasValue && minimum.Value >= least.Value)
                            || (exclusiveMinimum.HasValue && exclusiveMinimum.Value >= least.Value);
                    double? maximum = Number(form, "maximum");
                    bool ceiled = maximum.HasValue && maximum.Value <= most.Value;
                    if (!floored || !ceiled)
                    {
                        wrong.Add(input + ": ホストは " + (leastExcluded ? "(" : "[") + least + ", " + most
                            + "] を受け取るが、ツール定義は " + form.ToJsonString());
                    }
                }
            }

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void EveryNumberListOfAComposedToolTakesTheLengthTheHostAccepts()
        {
            IDictionary<string, JsonObject> rows = HostRows("lists");
            IDictionary<string, JsonObject> schemas = Schemas();
            List<string> wrong = new List<string>();
            foreach (string input in ComposedNumberLists())
            {
                JsonObject row;
                if (!rows.TryGetValue(input, out row))
                {
                    wrong.Add(input + ": ホストの並びの読み取りの表に無い");

                    continue;
                }

                double? least = Number(row, "least");
                double? most = Number(row, "most");
                foreach (JsonObject form in Forms(schemas, input, wrong))
                {
                    double? minItems = Number(form, "minItems");
                    double? maxItems = Number(form, "maxItems");
                    if ((least.HasValue && !(minItems.HasValue && minItems.Value >= least.Value))
                        || (most.HasValue && !(maxItems.HasValue && maxItems.Value <= most.Value)))
                    {
                        wrong.Add(input + ": ホストは " + (least.HasValue ? least.ToString() : "0") + " 個から "
                            + (most.HasValue ? most.ToString() : "上限なし") + " 個を受け取るが、ツール定義は "
                            + form.ToJsonString());
                    }
                }
            }

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        private static IDictionary<string, JsonObject> Schemas()
        {
            return GeneratedToolDefinitions.Create()
                .ToDictionary(d => d.Name, d => SchemaRefs.Inlined(JsonNode.Parse(d.InputSchema)).AsObject(), StringComparer.Ordinal);
        }

        private static IList<JsonObject> Forms(IDictionary<string, JsonObject> schemas, string input, IList<string> wrong)
        {
            string tool = input.Substring(0, input.IndexOf(' '));
            string path = input.Substring(input.IndexOf(' ') + 1);
            IList<JsonObject> forms = FormsAt(schemas[tool], path);
            if (forms.Count == 0)
            {
                wrong.Add(input + ": ツール定義に形が無い");
            }

            return forms;
        }

        private static double? Number(JsonObject holder, string name)
        {
            JsonNode value = holder[name];

            return value == null ? (double?)null : value.GetValue<double>();
        }

        private static IList<string> Types(JsonObject form)
        {
            JsonNode type = form["type"];
            if (type is JsonArray types)
            {
                return types.Select(t => t.GetValue<string>()).ToList();
            }

            return type == null ? new string[0] : new[] { type.GetValue<string>() };
        }

        private static IList<JsonObject> FormsAt(JsonObject schema, string path)
        {
            List<string> steps = new List<string>();
            foreach (string part in path.Split('.'))
            {
                string name = part;
                int elements = 0;
                while (name.EndsWith("[]", StringComparison.Ordinal))
                {
                    name = name.Substring(0, name.Length - 2);
                    elements++;
                }

                steps.Add("." + name);
                steps.AddRange(Enumerable.Repeat("[]", elements));
            }

            IEnumerable<JsonObject> current = Holders(schema);
            foreach (string step in steps)
            {
                current = step == "[]"
                    ? current.SelectMany(f => Alternatives(f["items"]))
                    : current.SelectMany(f => Alternatives(
                        f["properties"] is JsonObject properties ? properties[step.Substring(1)] : null));
            }

            return current.Where(f => f.Count > 0).ToList();
        }

        private static IEnumerable<JsonObject> Holders(JsonObject schema)
        {
            yield return schema;
            foreach (JsonObject nested in Alternatives(schema["anyOf"]).Concat(
                (schema["allOf"] as JsonArray ?? new JsonArray())
                    .Select(r => r["then"] as JsonObject)
                    .Where(t => t != null)
                    .SelectMany(t => new[] { t }.Concat(Alternatives(t["anyOf"])))))
            {
                yield return nested;
            }
        }

        private static IEnumerable<JsonObject> Alternatives(JsonNode node)
        {
            if (node is JsonArray many)
            {
                return many.SelectMany(Alternatives);
            }

            if (!(node is JsonObject form))
            {
                return new JsonObject[0];
            }

            JsonArray any = form["anyOf"] as JsonArray ?? form["oneOf"] as JsonArray;

            return any == null || form.ContainsKey("type") ? new[] { form } : any.SelectMany(Alternatives);
        }

        private static IList<string> ComposedNumberInputs()
        {
            return ComposedInputs(false);
        }

        private static IList<string> ComposedNumberLists()
        {
            return ComposedInputs(true);
        }

        private static IList<string> ComposedInputs(bool lists)
        {
            JsonNode composed = JsonNode.Parse(File.ReadAllText(Catalog("authored", "common-contract.json")))["composedTools"];
            ISet<string> tools = new HashSet<string>(
                composed is JsonObject named
                    ? named.Select(p => p.Key)
                    : composed.AsArray().Select(t => t["tool"].GetValue<string>()),
                StringComparer.Ordinal);
            HashSet<string> inputs = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonNode tool in JsonNode.Parse(File.ReadAllText(Catalog("authored", "tool-schemas.json")))["tools"].AsArray())
            {
                string name = tool["tool"].GetValue<string>();
                if (!tools.Contains(name))
                {
                    continue;
                }

                foreach (JsonNode input in tool["branches"].AsArray().SelectMany(b => b["inputs"].AsArray()))
                {
                    if (input["injected"] == null || !input["injected"].GetValue<bool>())
                    {
                        Collect(name, input, input["name"].GetValue<string>(), inputs, lists);
                    }
                }
            }

            return inputs.OrderBy(i => i, StringComparer.Ordinal).ToList();
        }

        private static void Collect(string tool, JsonNode item, string path, ISet<string> inputs, bool lists)
        {
            if (!lists && IsNumber(item))
            {
                inputs.Add(tool + " " + path);
            }

            foreach (JsonNode member in item["members"] as JsonArray ?? new JsonArray())
            {
                Collect(tool, member, path + "." + member["name"].GetValue<string>(), inputs, lists);
            }

            if (item["element"] != null)
            {
                if (lists && IsNumber(item["element"]))
                {
                    inputs.Add(tool + " " + path);
                }

                Collect(tool, item["element"], path + "[]", inputs, lists);
            }
        }

        private static bool IsNumber(JsonNode item)
        {
            return item["shape"] != null && item["shape"].GetValue<string>() == "number";
        }

        private static IDictionary<string, string> HostReads()
        {
            return HostRows("inputs").ToDictionary(
                r => r.Key, r => r.Value["reads"].GetValue<string>(), StringComparer.Ordinal);
        }

        private static IDictionary<string, JsonObject> HostRows(string part)
        {
            JsonArray rows = JsonNode.Parse(File.ReadAllText(Catalog("observed", "composed-number-reads.json")))[part] as JsonArray
                ?? new JsonArray();

            return rows.ToDictionary(
                r => r["tool"].GetValue<string>() + " " + r["input"].GetValue<string>(),
                r => r.AsObject(),
                StringComparer.Ordinal);
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
