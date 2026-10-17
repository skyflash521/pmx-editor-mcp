using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>題材の共通契約の正本を組み立てる。足さなかった表は最小の1件で埋める。</summary>
    internal sealed class CommonContractJsonBuilder
    {
        private readonly SortedDictionary<string, int> _spellings =
            new SortedDictionary<string, int>(StringComparer.Ordinal);

        private readonly SortedDictionary<string, int> _components =
            new SortedDictionary<string, int>(StringComparer.Ordinal);

        private readonly SortedDictionary<string, string> _composed =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        private readonly SortedDictionary<string, string> _shapes =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        private readonly SortedDictionary<string, string> _views =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        private readonly SortedSet<string> _drawn = new SortedSet<string>(StringComparer.Ordinal);

        private readonly SortedSet<string> _overwriting = new SortedSet<string>(StringComparer.Ordinal);

        private readonly SortedDictionary<string, string> _unkept =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        private readonly SortedDictionary<string, string> _targeted =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        private readonly SortedDictionary<string, string> _parents =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        private int _responseDefaultChars = 100000;

        private int _warningRoomChars = 2000;

        private int _requestBytes = 8000000;

        private int _structureTokenLimit = 200000;

        public CommonContractJsonBuilder AddSpelling(string spelling, int assumedChars)
        {
            _spellings[spelling] = assumedChars;

            return this;
        }

        /// <summary>綴りが1つに決まらない包む型は、表現に null を渡す。</summary>
        public CommonContractJsonBuilder AddType(string typeName, string shape)
        {
            _shapes[typeName] = shape;

            return this;
        }

        public CommonContractJsonBuilder AddComponent(string typeName, int count)
        {
            _components[typeName] = count;

            return this;
        }

        public CommonContractJsonBuilder AddComposedTool(string tool, bool branching, string duty)
        {
            _composed[tool] = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"tool\":{0},\"branching\":{1},\"duty\":{2}}}",
                Quoted(tool),
                branching ? "true" : "false",
                Quoted(duty));

            return this;
        }

        public CommonContractJsonBuilder AddDrawnImage(string tool)
        {
            _drawn.Add(tool);

            return this;
        }

        public CommonContractJsonBuilder AddOverwritingTool(string tool)
        {
            _overwriting.Add(tool);

            return this;
        }

        public CommonContractJsonBuilder AddViewImage(string tool, string view)
        {
            _views[tool] = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"tool\":{0},\"view\":{1}}}",
                Quoted(tool),
                Quoted(view));

            return this;
        }

        public CommonContractJsonBuilder AddUnkeptMember(string tool, string member, string basis)
        {
            _unkept[tool] = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"tool\":{0},\"members\":[{1}],\"basis\":{2}}}",
                Quoted(tool),
                Quoted(member),
                Quoted(basis));

            return this;
        }

        /// <summary>要素を親の並びへ加える前に、親へ揃える値を1つ足す。</summary>
        public CommonContractJsonBuilder AddParentValue(
            string tool, string parentTool, string member, string value, string basis)
        {
            _parents[tool] = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"tool\":{0},\"parentTool\":{1},\"member\":{2},\"value\":{3},\"basis\":{4}}}",
                Quoted(tool),
                Quoted(parentTool),
                Quoted(member),
                Quoted(value),
                Quoted(basis));

            return this;
        }

        /// <summary>要素を並びへ加える前に指す先を埋める項目を1つ足す。</summary>
        public CommonContractJsonBuilder AddTargetedMember(
            string tool, string member, string basis)
        {
            _targeted[tool] = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"tool\":{0},\"members\":[{1}],\"basis\":{2}}}",
                Quoted(tool),
                Quoted(member),
                Quoted(basis));

            return this;
        }

        public CommonContractJsonBuilder WithBudgets(
            int responseDefaultChars, int warningRoomChars, int requestBytes, int structureTokenLimit)
        {
            _responseDefaultChars = responseDefaultChars;
            _warningRoomChars = warningRoomChars;
            _requestBytes = requestBytes;
            _structureTokenLimit = structureTokenLimit;

            return this;
        }

        public override string ToString()
        {
            List<string> types = _shapes
                .Select(shape => string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"typeName\":{0},\"shape\":{1}}}",
                    Quoted(shape.Key),
                    shape.Value == null ? "null" : Quoted(shape.Value)))
                .ToList();
            StringBuilder builder = new StringBuilder("{\"spellings\":[")
                .Append(string.Join(",", Filled(
                    _spellings.Select(s => Default(s.Key, s.Value)).ToList(),
                    Default("number", 11))))
                .Append("],\"types\":[")
                .Append(string.Join(",", Filled(types, Type("System.Int32", "number"))))
                .Append("],\"components\":[")
                .Append(string.Join(",", Filled(
                    _components
                        .Select(c => string.Format(
                            CultureInfo.InvariantCulture,
                            "{{\"typeName\":{0},\"count\":{1}}}",
                            Quoted(c.Key),
                            c.Value))
                        .ToList(),
                    Component("System.Numerics.Vector3", 3))))
                .Append("],\"composedTools\":[")
                .Append(string.Join(",", Filled(
                    _composed.Values.ToList(), Composed("session_release_handle"))))
                .Append("],\"viewImages\":[")
                .Append(string.Join(",", _views.Values))
                .Append("],\"drawnImages\":[")
                .Append(string.Join(",", _drawn.Select(t => "{\"tool\":" + Quoted(t) + "}")))
                .Append("],\"overwritingTools\":[")
                .Append(string.Join(",", _overwriting.Select(t => "{\"tool\":" + Quoted(t) + "}")))
                .Append("],\"unkeptMembers\":[")
                .Append(string.Join(",", _unkept.Values))
                .Append("],\"targetedMembers\":[")
                .Append(string.Join(",", _targeted.Values))
                .Append("],\"parentValues\":[")
                .Append(string.Join(",", _parents.Values))
                .Append("],\"budgets\":{")
                .AppendFormat(
                    CultureInfo.InvariantCulture,
                    "\"responseDefaultChars\":{0},\"warningRoomChars\":{1},"
                        + "\"requestBytes\":{2},\"structureTokenLimit\":{3}",
                    _responseDefaultChars,
                    _warningRoomChars,
                    _requestBytes,
                    _structureTokenLimit)
                .Append("}}");

            return builder.ToString();
        }

        private static IList<string> Filled(IList<string> items, string fallback)
        {
            return items.Count == 0 ? new[] { fallback } : items;
        }

        private static string Default(string spelling, int assumedChars)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"spelling\":{0},\"assumedChars\":{1}}}",
                Quoted(spelling),
                assumedChars);
        }

        private static string Type(string typeName, string shape)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"typeName\":{0},\"shape\":{1}}}",
                Quoted(typeName),
                Quoted(shape));
        }

        private static string Component(string typeName, int count)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"typeName\":{0},\"count\":{1}}}",
                Quoted(typeName),
                count);
        }

        private static string Composed(string tool)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"tool\":{0},\"branching\":false,\"duty\":\"題材\"}}",
                Quoted(tool));
        }

        private static string Quoted(string text)
        {
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
