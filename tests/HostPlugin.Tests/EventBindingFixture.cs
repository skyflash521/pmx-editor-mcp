using System;
using System.Collections.Generic;

namespace PmxEditorMcp.Tests
{
    /// <summary>イベントの結び付きを持たない題材。イベントを扱わない試験はこれを渡す。</summary>
    internal static class EventBindingFixture
    {
        public static EventBindingTable Empty()
        {
            return new EventBindingTable(
                new Dictionary<string, EventAttach>(StringComparer.Ordinal),
                new Dictionary<string, PayloadReader>(StringComparer.Ordinal));
        }
    }
}
