using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    internal static class GeneratedRows
    {
        /// <summary>
        /// <paramref name="build"/> が読み込まれたSDKの型かメンバーへ届かずに落ちたときは、行を加えず
        /// <paramref name="unresolved"/> へ名前を足す。ほかの失敗はそのまま投げる。
        /// </summary>
        internal static void Add<T>(
            IDictionary<string, T> rows, ICollection<string> unresolved, string name, Func<T> build)
        {
            T built;
            try
            {
                built = build();
            }
            catch (Exception exception) when (SdkRelayTable.IsResolutionFailure(exception))
            {
                unresolved.Add(name);

                return;
            }

            rows.Add(name, built);
        }
    }
}
