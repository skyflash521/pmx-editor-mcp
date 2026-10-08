using System;

namespace PmxEditorMcp
{
    public sealed class DeclinedUiInvoker : IUiInvoker
    {
        public static DeclinedUiInvoker Instance { get; } = new DeclinedUiInvoker();

        private DeclinedUiInvoker()
        {
        }

        public UiInvocation TryInvokeOnUi(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return UiInvocation.Declined;
        }
    }
}
