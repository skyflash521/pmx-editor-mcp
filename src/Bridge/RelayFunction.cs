using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// ビルド時に組み立てた定義をそのまま名乗り、同じ名前のホストのメソッドへ中継する。名前と説明と
    /// 入力の形は本文が持つので、委譲先の形から読み取らない。
    /// </summary>
    internal sealed class RelayFunction : AIFunction
    {
        private readonly GeneratedToolDefinition _definition;

        private readonly HostIpcClient _client;

        private readonly JsonElement _schema;

        internal RelayFunction(GeneratedToolDefinition definition, HostIpcClient client)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _definition = definition;
            _client = client;
            _schema = JsonDocument.Parse(definition.InputSchema).RootElement.Clone();
        }

        public override string Name
        {
            get { return _definition.Name; }
        }

        public override string Description
        {
            get { return _definition.Description; }
        }

        public override JsonElement JsonSchema
        {
            get { return _schema; }
        }

        protected override async ValueTask<object> InvokeCoreAsync(
            AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            return await BridgeTools
                .RelayEnvelopeAsync(
                    _client,
                    _definition.Name,
                    Parameters(arguments),
                    _definition.ReturnsImage,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>受け取った引数を要求の組へ写す。値はJSONのまま渡し、形はホストが確かめる。</summary>
        private static JsonObject Parameters(AIFunctionArguments arguments)
        {
            if (arguments == null)
            {
                return null;
            }

            JsonObject parameters = new JsonObject();
            foreach (KeyValuePair<string, object> argument in arguments)
            {
                parameters[argument.Key] = Node(argument.Value);
            }

            return parameters.Count == 0 ? null : parameters;
        }

        private static JsonNode Node(object value)
        {
            if (value == null)
            {
                return null;
            }

            JsonNode node = value as JsonNode;
            if (node != null)
            {
                return node.DeepClone();
            }

            if (value is JsonElement)
            {
                return JsonSerializer.SerializeToNode((JsonElement)value);
            }

            return JsonSerializer.SerializeToNode(value);
        }
    }
}
