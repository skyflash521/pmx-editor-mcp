using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace PmxEditorMcp.Bridge.Tests
{
    public sealed class ComposedTextInputTests
    {
        private const string WindowForms = "windowForms";

        [Fact]
        public void EveryTextInputOfAComposedToolStaysWithinTheValuesTheHostAccepts()
        {
            IDictionary<string, JsonObject> rows = HostRows("inputs");
            IDictionary<string, JsonObject> schemas = Schemas();
            IList<string> forms = WindowFormNames();
            List<string> wrong = new List<string>();
            foreach (string input in ComposedInputs(false))
            {
                JsonObject row;
                if (!rows.TryGetValue(input, out row))
                {
                    wrong.Add(input + ": ホストの読み取りの表に無い");

                    continue;
                }

                IList<string> choices = Choices(row, forms);
                int least = row["minLength"] == null ? 0 : row["minLength"].GetValue<int>();
                foreach (JsonObject form in Forms(schemas, input, wrong))
                {
                    IList<string> declared = form["enum"] is JsonArray listed
                        ? listed.Where(v => v != null).Select(v => v.GetValue<string>()).ToList()
                        : null;
                    if (choices != null)
                    {
                        if (declared == null || declared.Any(v => !choices.Contains(v, StringComparer.Ordinal)))
                        {
                            wrong.Add(input + ": ホストは " + string.Join("・", choices) + " だけを受け取るが、ツール定義は "
                                + form.ToJsonString());
                        }

                        continue;
                    }

                    if (least == 0)
                    {
                        continue;
                    }

                    bool bounded = declared != null
                        ? declared.All(v => v.Length >= least)
                        : form["minLength"] != null && form["minLength"].GetValue<int>() >= least;
                    if (!bounded)
                    {
                        wrong.Add(input + ": ホストは " + least + " 文字未満を断るが、ツール定義は " + form.ToJsonString());
                    }
                }
            }

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void EveryTextListOfAComposedToolTakesTheLengthTheHostAccepts()
        {
            IDictionary<string, JsonObject> rows = HostRows("lists");
            IDictionary<string, JsonObject> schemas = Schemas();
            List<string> wrong = new List<string>();
            foreach (string input in ComposedInputs(true))
            {
                JsonObject row;
                if (!rows.TryGetValue(input, out row))
                {
                    wrong.Add(input + ": ホストの並びの読み取りの表に無い");

                    continue;
                }

                int least = row["least"] == null ? 0 : row["least"].GetValue<int>();
                foreach (JsonObject form in Forms(schemas, input, wrong))
                {
                    if (least > 0 && !(form["minItems"] != null && form["minItems"].GetValue<int>() >= least))
                    {
                        wrong.Add(input + ": ホストは " + least + " 個未満を断るが、ツール定義は " + form.ToJsonString());
                    }
                }
            }

            Assert.True(wrong.Count == 0, wrong.Count + " 件:\n" + string.Join("\n", wrong));
        }

        private static IList<string> Choices(JsonObject row, IList<string> forms)
        {
            if (row["choicesFrom"] != null)
            {
                Assert.Equal(WindowForms, row["choicesFrom"].GetValue<string>());

                return forms;
            }

            return row["choices"] is JsonArray choices ? choices.Select(c => c.GetValue<string>()).ToList() : null;
        }

        private static IList<string> WindowFormNames()
        {
            return JsonNode.Parse(File.ReadAllText(Catalog("observed", "ui-structure.json")))["windows"].AsArray()
                .Select(w => w["form"].GetValue<string>())
                .ToList();
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
            if (!lists && IsText(item))
            {
                inputs.Add(tool + " " + path);
            }

            foreach (JsonNode member in item["members"] as JsonArray ?? new JsonArray())
            {
                Collect(tool, member, path + "." + member["name"].GetValue<string>(), inputs, lists);
            }

            if (item["element"] != null)
            {
                if (lists && IsText(item["element"]))
                {
                    inputs.Add(tool + " " + path);
                }

                Collect(tool, item["element"], path + "[]", inputs, lists);
            }
        }

        private static bool IsText(JsonNode item)
        {
            return item["shape"] != null && item["shape"].GetValue<string>() == "text";
        }

        private static IDictionary<string, JsonObject> HostRows(string part)
        {
            JsonArray rows = JsonNode.Parse(File.ReadAllText(Catalog("observed", "composed-text-reads.json")))[part] as JsonArray
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
