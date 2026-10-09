using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PmxEditorMcp
{
    /// <summary>
    /// ホストの起動で作る大きな表。作り始めをそろえて並べて走らせ、使う側が取るときに出来上がりを
    /// 待つ。
    /// </summary>
    internal sealed class HostTables
    {
        private readonly Task<SdkRelayTable> _relay;

        private readonly Task<Dictionary<string, IList<ToolCall>>> _calls;

        private readonly Task<Dictionary<string, ToolFields>> _aggregations;

        private readonly Task<Dictionary<string, ToolElements>> _elements;

        private readonly Task<Dictionary<string, ToolPrecondition>> _preconditions;

        private readonly Task<Dictionary<string, EventAttach>> _attachments;

        private readonly Task<Dictionary<string, PayloadReader>> _payloads;

        private readonly List<string> _unresolvedCalls = new List<string>();

        private readonly List<string> _unresolvedAggregations = new List<string>();

        private readonly List<string> _unresolvedElements = new List<string>();

        private HostTables()
        {
            _relay = Task.Run(() => GeneratedSdkRelay.Create());
            _calls = Task.Run(() => GeneratedTools.Calls(_unresolvedCalls));
            _aggregations = Task.Run(() => GeneratedTools.Aggregations(_unresolvedAggregations));
            _elements = Task.Run(() => GeneratedTools.Elements(_unresolvedElements));
            _preconditions = Task.Run(() => GeneratedTools.Preconditions());
            _attachments = Task.Run(() => GeneratedTools.Attachments());
            _payloads = Task.Run(() => GeneratedTools.Payloads());
        }

        internal static HostTables Start()
        {
            return new HostTables();
        }

        internal SdkRelayTable Relay
        {
            get { return _relay.GetAwaiter().GetResult(); }
        }

        internal Dictionary<string, IList<ToolCall>> Calls
        {
            get { return _calls.GetAwaiter().GetResult(); }
        }

        internal Dictionary<string, ToolFields> Aggregations
        {
            get { return _aggregations.GetAwaiter().GetResult(); }
        }

        internal Dictionary<string, ToolElements> Elements
        {
            get { return _elements.GetAwaiter().GetResult(); }
        }

        internal Dictionary<string, ToolPrecondition> Preconditions
        {
            get { return _preconditions.GetAwaiter().GetResult(); }
        }

        internal Dictionary<string, EventAttach> Attachments
        {
            get { return _attachments.GetAwaiter().GetResult(); }
        }

        internal Dictionary<string, PayloadReader> Payloads
        {
            get { return _payloads.GetAwaiter().GetResult(); }
        }

        internal IList<string> UnresolvedTools
        {
            get
            {
                _calls.GetAwaiter().GetResult();
                _aggregations.GetAwaiter().GetResult();
                _elements.GetAwaiter().GetResult();

                return _unresolvedCalls.Concat(_unresolvedAggregations).Concat(_unresolvedElements).ToList();
            }
        }
    }
}
