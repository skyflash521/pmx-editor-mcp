using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 受入シナリオの定義が、実行器の解せる形をしていて、書いたツールと引数が登録される定義に
    /// 合い、指す要求が要求仕様書に実在することを照合する。
    /// </summary>
    public static class AcceptanceScenarioGate
    {
        /// <summary>覚えた値を差し込む組の鍵。実行器の側と同じ綴りである。</summary>
        public const string ReferenceName = "$from";

        /// <summary>数として覚える印。引数へ差し込めるのはこの印を持つものだけである。</summary>
        public const string NumberShape = "number";

        /// <summary>写した大きさとして覚える印。絵の期待だけが指せる。</summary>
        public const string SizeShape = "size";

        private const string SchemaResource =
            "PmxEditorMcp.SignatureDump.AcceptanceScenarioSchema.json";

        /// <summary>引数の形を確かめるとき、覚えた値の代わりに差し込む数。</summary>
        private const int SubstitutedNumber = 1;

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static JsonNode Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            JsonNode read;
            try
            {
                read = JsonNode.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new FormatException("JSONとして読めない。", exception);
            }

            EvaluationResults evaluated = Schema().Evaluate(
                Element(read), new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!evaluated.IsValid)
            {
                throw new FormatException(
                    "受入シナリオの定義が実行器の解せる形をしていない。" + Describe(evaluated));
            }

            return read;
        }

        /// <summary>
        /// 合わなければ <see cref="InvalidOperationException"/>。
        /// <paramref name="definitions"/> は登録されるツール定義、<paramref name="fixedTools"/> は
        /// 引数を取らない固定のツールの名前である。
        /// </summary>
        public static void Require(
            JsonNode scenarios,
            IList<ToolDefinition> definitions,
            ISet<string> fixedTools,
            RequirementNames requirements)
        {
            if (scenarios == null)
            {
                throw new ArgumentNullException(nameof(scenarios));
            }

            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            if (fixedTools == null)
            {
                throw new ArgumentNullException(nameof(fixedTools));
            }

            if (requirements == null)
            {
                throw new ArgumentNullException(nameof(requirements));
            }

            IDictionary<string, JsonSchema> schemas = Schemas(definitions, fixedTools);
            ISet<string> named = new HashSet<string>(
                requirements.Tasks.Concat(requirements.Conditions), StringComparer.Ordinal);
            ISet<string> covered = new HashSet<string>(StringComparer.Ordinal);
            ISet<int> ids = new HashSet<int>();

            // 覚えた値はシナリオをまたいで残るので、名前の照合も並び順のまま通して行う。
            IDictionary<string, string> recorded =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonNode scenario in scenarios["scenarios"].AsArray())
            {
                int id = scenario["id"].GetValue<int>();
                if (!ids.Add(id))
                {
                    throw Broken("シナリオの番号が二度現れる: " + id);
                }

                foreach (JsonNode requirement in scenario["requirements"].AsArray())
                {
                    string name = requirement.GetValue<string>();
                    if (!named.Contains(name))
                    {
                        throw Broken(
                            "シナリオ" + id + " が要求仕様書に無い要求を指している: " + name);
                    }

                    covered.Add(name);
                }

                Steps(scenario, id, schemas, recorded);
            }

            string[] missing = requirements.Tasks.Where(t => !covered.Contains(t)).ToArray();
            if (missing.Length != 0)
            {
                throw Broken(
                    "どのシナリオも当たっていない作業がある: " + string.Join("・", missing));
            }
        }

        private static void Steps(
            JsonNode scenario,
            int id,
            IDictionary<string, JsonSchema> schemas,
            IDictionary<string, string> recorded)
        {
            JsonArray steps = scenario["steps"].AsArray();
            for (int at = 0; at < steps.Count; at++)
            {
                JsonNode step = steps[at];
                string where = "シナリオ" + id + " の段 " + (at + 1);
                string kind = step["kind"].GetValue<string>();
                if (kind == "tool")
                {
                    ToolStep(step, where, schemas, recorded);
                }
                else if (kind == "control")
                {
                    References(step["editor"], where, recorded, NumberShape);
                }

                Record(step, where, recorded);
            }
        }

        private static void ToolStep(
            JsonNode step,
            string where,
            IDictionary<string, JsonSchema> schemas,
            IDictionary<string, string> recorded)
        {
            string tool = step["tool"].GetValue<string>();
            JsonSchema schema;
            if (!schemas.TryGetValue(tool, out schema))
            {
                throw Broken(where + " が登録されていないツールを呼んでいる: " + tool);
            }

            JsonNode arguments = step["arguments"];
            References(arguments, where, recorded, NumberShape);
            EvaluationResults evaluated = schema.Evaluate(
                Element(Substitute(arguments)),
                new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!evaluated.IsValid)
            {
                throw Broken(
                    where + " の引数が " + tool + " の入力スキーマを満たさない。" + Describe(evaluated));
            }

            Expect(step["expect"], where, recorded);
        }

        private static void Expect(
            JsonNode expect, string where, IDictionary<string, string> recorded)
        {
            References(expect["notice"], where, recorded, NumberShape);
            References(expect["events"], where, recorded, NumberShape);

            JsonNode image = expect["image"];
            if (image == null || image["capturedAs"] == null)
            {
                return;
            }

            string name = image["capturedAs"].GetValue<string>();
            string shape;
            if (!recorded.TryGetValue(name, out shape))
            {
                throw Broken(where + " がまだ写していない大きさを指している: " + name);
            }

            if (shape != SizeShape)
            {
                throw Broken(where + " が写した大きさでないものを指している: " + name);
            }
        }

        /// <summary>覚える値を控える。同じ名前を二度覚えさせない。</summary>
        private static void Record(
            JsonNode step, string where, IDictionary<string, string> recorded)
        {
            JsonNode record = step["record"];
            if (record == null)
            {
                return;
            }

            string name = record["name"].GetValue<string>();
            if (recorded.ContainsKey(name))
            {
                throw Broken(where + " が覚えている名前をもう一度覚えようとしている: " + name);
            }

            recorded.Add(name, record["shape"].GetValue<string>());
        }

        /// <summary>差し込む値の名前が、先に覚えた印と合うか。</summary>
        private static void References(
            JsonNode node, string where, IDictionary<string, string> recorded, string shape)
        {
            if (node == null)
            {
                return;
            }

            if (node is JsonArray)
            {
                foreach (JsonNode item in (JsonArray)node)
                {
                    References(item, where, recorded, shape);
                }

                return;
            }

            JsonObject members = node as JsonObject;
            if (members == null)
            {
                return;
            }

            JsonNode reference = members[ReferenceName];
            if (reference != null)
            {
                string name = reference.GetValue<string>();
                string remembered;
                if (!recorded.TryGetValue(name, out remembered))
                {
                    throw Broken(where + " がまだ覚えていない値を差し込んでいる: " + name);
                }

                if (remembered != shape)
                {
                    throw Broken(
                        where + " が " + shape + " として覚えていない値を差し込んでいる: " + name);
                }

                return;
            }

            foreach (KeyValuePair<string, JsonNode> member in members)
            {
                References(member.Value, where, recorded, shape);
            }
        }

        /// <summary>覚えた値を差し込む組を、その印の値へ置き換えた写し。</summary>
        private static JsonNode Substitute(JsonNode node)
        {
            if (node is JsonArray)
            {
                JsonArray items = new JsonArray();
                foreach (JsonNode item in (JsonArray)node)
                {
                    items.Add(Substitute(item));
                }

                return items;
            }

            JsonObject members = node as JsonObject;
            if (members == null)
            {
                return node == null ? null : JsonNode.Parse(node.ToJsonString());
            }

            if (members[ReferenceName] != null)
            {
                return JsonValue.Create(SubstitutedNumber);
            }

            JsonObject copied = new JsonObject();
            foreach (KeyValuePair<string, JsonNode> member in members)
            {
                copied.Add(member.Key, Substitute(member.Value));
            }

            return copied;
        }

        /// <summary>ツールの名前から入力スキーマを引く表。固定のツールは引数を取らない。</summary>
        private static IDictionary<string, JsonSchema> Schemas(
            IList<ToolDefinition> definitions, ISet<string> fixedTools)
        {
            Dictionary<string, JsonSchema> schemas =
                new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
            foreach (ToolDefinition definition in definitions)
            {
                schemas.Add(definition.Name, JsonSchema.FromText(definition.InputSchema));
            }

            foreach (string name in fixedTools)
            {
                schemas[name] = JsonSchema.FromText(
                    "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}");
            }

            return schemas;
        }

        /// <summary>照合へ渡せる形。組み立てた木をそのまま渡せないので、綴り直して読み込む。</summary>
        private static JsonElement Element(JsonNode node)
        {
            using (JsonDocument document = JsonDocument.Parse(node.ToJsonString()))
            {
                return document.RootElement.Clone();
            }
        }

        private static JsonSchema Schema()
        {
            using (Stream stream = typeof(AcceptanceScenarioGate).GetTypeInfo().Assembly
                .GetManifestResourceStream(SchemaResource))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("形の宣言が組み込まれていない。");
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return JsonSchema.FromText(reader.ReadToEnd());
                }
            }
        }

        private static string Describe(EvaluationResults evaluated)
        {
            List<string> said = new List<string>();
            foreach (EvaluationResults detail in evaluated.Details)
            {
                if (detail.Errors == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, string> error in detail.Errors)
                {
                    said.Add(detail.InstanceLocation + " " + error.Key + ": " + error.Value);
                }
            }

            return string.Join(" / ", said.Distinct(StringComparer.Ordinal).Take(10));
        }

        private static InvalidOperationException Broken(string message)
        {
            return new InvalidOperationException(message);
        }
    }
}
