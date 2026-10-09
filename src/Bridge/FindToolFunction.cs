using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using PmxEditorMcp.SignatureDump;

namespace PmxEditorMcp.Bridge
{
    internal sealed class FindToolFunction : AIFunction
    {
        private const string LimitParameter = "limit";

        private const string OffsetParameter = "offset";

        private static readonly JsonElement Schema = JsonDocument
            .Parse(FixedToolTable.InputSchema(FixedToolTable.FindToolName))
            .RootElement
            .Clone();

        private readonly IList<ToolMatch.Entry> _entries;

        private readonly string _description;

        private readonly HostIpcClient _client;

        internal FindToolFunction(IList<ToolMatch.Entry> entries, string description, HostIpcClient client)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _entries = entries;
            _description = description;
            _client = client;
        }

        public override string Name
        {
            get { return FixedToolTable.FindToolName; }
        }

        public override string Description
        {
            get { return _description; }
        }

        public override JsonElement JsonSchema
        {
            get { return Schema; }
        }

        protected override ValueTask<object> InvokeCoreAsync(
            AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            CallToolResult result = ToolEnvelopeResult.From(
                ToolSearch.Answer(
                    Argument<string[]>(arguments, FixedToolTable.FindToolTextsParameter),
                    Argument<int?>(arguments, LimitParameter),
                    Argument<int?>(arguments, OffsetParameter),
                    _entries,
                    _client.BudgetChars),
                string.Empty,
                _client.BudgetChars,
                false);

            return new ValueTask<object>(result);
        }

        /// <summary>
        /// 渡された引数を型へ読む。渡されていなければ型の既定値を返す。型に読めなければ
        /// <see cref="JsonException"/> を投げる。
        /// </summary>
        private static T Argument<T>(AIFunctionArguments arguments, string name)
        {
            object value;
            if (arguments == null || !arguments.TryGetValue(name, out value) || value == null)
            {
                return default(T);
            }

            JsonElement element = value is JsonElement
                ? (JsonElement)value
                : JsonSerializer.SerializeToElement(value);

            return element.Deserialize<T>();
        }
    }
}
