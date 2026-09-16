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

        /// <summary>数として覚える印。引数へ差し込めるのはこの印と数の並びの印だけである。</summary>
        public const string NumberShape = "number";

        /// <summary>数の並びとして覚える印。1回の呼び出しで並びを返すツールの応答が当たる。</summary>
        public const string NumbersShape = "numbers";

        /// <summary>写した大きさとして覚える印。画像の期待だけが指せる。</summary>
        public const string SizeShape = "size";

        /// <summary>返った画像そのものを覚える印。</summary>
        public const string ImageShape = "image";

        private const string SchemaResource =
            "PmxEditorMcp.SignatureDump.AcceptanceScenarioSchema.json";

        /// <summary>引数の形を確かめるとき、覚えた値の代わりに差し込む数。</summary>
        private const int SubstitutedNumber = 1;

        /// <summary>ツールを呼ぶ段の種別。</summary>
        private const string ToolStepKind = "tool";

        /// <summary>エディタとホストを操作する段の種別。</summary>
        private const string ControlStep = "control";

        /// <summary>置き場を用意する・確かめる段の種別。</summary>
        private const string FileStep = "file";

        /// <summary>応答を作る相手を起こし直す段の種別。</summary>
        private const string ServerStep = "server";

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
        /// 実物の定義が立てる期待の形と、エディタとホストの操作・置き場・応答を作る相手の
        /// 起こし直しの各段が頼む行いの種類が、突き合わせの題材にも在ることを求める。実行器が
        /// それらを突き合わせているかを見る照合は題材で走るので、題材に無いものは、実行器が
        /// 見ていなくても気づけないまま通る。
        /// </summary>
        public static void RequireCoveredByStub(JsonNode scenarios, JsonNode stub)
        {
            if (scenarios == null)
            {
                throw new ArgumentNullException(nameof(scenarios));
            }

            if (stub == null)
            {
                throw new ArgumentNullException(nameof(stub));
            }

            RequireCovered("期待の形", Forms(scenarios), Forms(stub));
            RequireCovered(
                "操作の種類", Actions(scenarios, ControlStep), Actions(stub, ControlStep));
            RequireCovered(
                "置き場の段の種類", Actions(scenarios, FileStep), Actions(stub, FileStep));
            RequireCovered(
                "サーバーの段の種類", Actions(scenarios, ServerStep), Actions(stub, ServerStep));
        }

        /// <summary>覆えていないものがあれば <see cref="InvalidOperationException"/>。</summary>
        private static void RequireCovered(string what, ISet<string> wanted, ISet<string> held)
        {
            string[] missing = wanted
                .Where(one => !held.Contains(one))
                .OrderBy(one => one, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length != 0)
            {
                throw new InvalidOperationException(
                    "突き合わせの題材に" + what + "が無い: " + string.Join("・", missing));
            }
        }

        /// <summary>
        /// その定義が立てる期待の形。1つの期待が2つの形を立てることがあるので、期待の名前では
        /// なく中身で決める——名前ごとに1つへ決めると、2つ立てた段の片方が数えられないまま残る。
        /// </summary>
        private static ISet<string> Forms(JsonNode defined)
        {
            HashSet<string> forms = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonNode step in Steps(defined))
            {
                JsonNode expect = step["expect"];
                if (expect == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, JsonNode> one in expect.AsObject())
                {
                    foreach (string form in FormsOf(one.Key, one.Value))
                    {
                        forms.Add(form);
                    }
                }
            }

            return forms;
        }

        /// <summary>1つの期待が立てる形。</summary>
        private static IEnumerable<string> FormsOf(string name, JsonNode value)
        {
            if (name == "notice")
            {
                yield return value["editor"] != null ? "notice" : "notice.changed";
                yield break;
            }

            if (name == "image")
            {
                if (value["capturedAs"] != null)
                {
                    yield return "image";
                }

                if (value["differsFrom"] != null)
                {
                    yield return "image.differsFrom";
                }

                yield break;
            }

            if (name == "values")
            {
                foreach (JsonNode one in value.AsArray())
                {
                    if (one["equals"] != null)
                    {
                        yield return "values";
                    }

                    if (one["absent"] != null)
                    {
                        yield return "values.absent";
                    }

                    if (one["present"] != null)
                    {
                        yield return "values.present";
                    }
                }

                yield break;
            }

            yield return name;
        }

        /// <summary>その定義が、指した種別の段で頼む行いの種類。</summary>
        private static ISet<string> Actions(JsonNode defined, string kind)
        {
            return new HashSet<string>(
                Steps(defined)
                    .Where(step => (string)step["kind"] == kind)
                    .Select(step => (string)step["action"]),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// その定義が成功を期待するツールの名前。実機へ投げる検査の覆いを数える側が、生成器の
        /// 組む事例と並べてこれを読む。
        /// </summary>
        public static ISet<string> SucceedingTools(JsonNode scenarios)
        {
            if (scenarios == null)
            {
                throw new ArgumentNullException(nameof(scenarios));
            }

            return new HashSet<string>(
                Steps(scenarios)
                    .Where(step => (string)step["kind"] == ToolStepKind && Succeeds(step))
                    .Select(step => (string)step["tool"]),
                StringComparer.Ordinal);
        }

        /// <summary>その段が、呼び出しの成功を期待しているか。</summary>
        private static bool Succeeds(JsonNode step)
        {
            JsonNode expect = step["expect"];

            return expect != null
                && expect["ok"] != null
                && expect["ok"].GetValue<bool>();
        }

        /// <summary>その定義が並べる段。</summary>
        private static IEnumerable<JsonNode> Steps(JsonNode defined)
        {
            return defined["scenarios"].AsArray().SelectMany(one => one["steps"].AsArray());
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
                if (kind == ToolStepKind)
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
            References(arguments, where, recorded, NumberShape, NumbersShape);
            EvaluationResults evaluated = schema.Evaluate(
                Element(Substitute(arguments, recorded)),
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
            if (image == null)
            {
                return;
            }

            if (image["capturedAs"] != null)
            {
                Recorded(
                    image["capturedAs"].GetValue<string>(), where, recorded, SizeShape, "大きさ");
            }

            if (image["differsFrom"] != null)
            {
                Recorded(
                    image["differsFrom"].GetValue<string>(), where, recorded, ImageShape, "画像");
            }
        }

        /// <summary>
        /// 指した名前が、その印で覚えたものか。<paramref name="what"/> は印が指すものの呼び名で、
        /// 「まだ覚えていない〇〇」「〇〇でないもの」の両方に収まる単体の名詞を渡す。
        /// </summary>
        private static void Recorded(
            string name,
            string where,
            IDictionary<string, string> recorded,
            string shape,
            string what)
        {
            string held;
            if (!recorded.TryGetValue(name, out held))
            {
                throw Broken(where + " がまだ覚えていない" + what + "を指している: " + name);
            }

            if (held != shape)
            {
                throw Broken(where + " が" + what + "でないものを指している: " + name);
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
            JsonNode node,
            string where,
            IDictionary<string, string> recorded,
            params string[] shapes)
        {
            if (node == null)
            {
                return;
            }

            if (node is JsonArray)
            {
                foreach (JsonNode item in (JsonArray)node)
                {
                    References(item, where, recorded, shapes);
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

                if (Array.IndexOf(shapes, remembered) < 0)
                {
                    throw Broken(
                        where + " が " + string.Join("・", shapes)
                            + " として覚えていない値を差し込んでいる: " + name);
                }

                return;
            }

            foreach (KeyValuePair<string, JsonNode> member in members)
            {
                References(member.Value, where, recorded, shapes);
            }
        }

        /// <summary>
        /// 覚えた値を差し込む組を、その印の値へ置き換えた写し。数の並びとして覚えた値は、1件だけ
        /// 持つ並びへ置き換える——並びを取る入力へ数を1つ置くと、形が違うとして落ちる。
        /// </summary>
        private static JsonNode Substitute(
            JsonNode node, IDictionary<string, string> recorded)
        {
            if (node is JsonArray)
            {
                JsonArray items = new JsonArray();
                foreach (JsonNode item in (JsonArray)node)
                {
                    items.Add(Substitute(item, recorded));
                }

                return items;
            }

            JsonObject members = node as JsonObject;
            if (members == null)
            {
                return node == null ? null : JsonNode.Parse(node.ToJsonString());
            }

            JsonNode reference = members[ReferenceName];
            if (reference != null)
            {
                string remembered;
                recorded.TryGetValue(reference.GetValue<string>(), out remembered);

                return remembered == NumbersShape
                    ? (JsonNode)new JsonArray(JsonValue.Create(SubstitutedNumber))
                    : JsonValue.Create(SubstitutedNumber);
            }

            JsonObject copied = new JsonObject();
            foreach (KeyValuePair<string, JsonNode> member in members)
            {
                copied.Add(member.Key, Substitute(member.Value, recorded));
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
