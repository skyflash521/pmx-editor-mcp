// 面の選択を受け渡すSDKのメンバーの中継に、画面が数える位置と面の通し番号の換算を掛ける。

using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 面の選択をそのまま受け渡す中継を包み、外から渡す位置と外へ返す位置を面の通し番号にする。
    /// </summary>
    public static class ScreenFacePlaces
    {
        /// <summary>画面から面の位置を受け取る行。</summary>
        public static IList<string> Reading
        {
            get
            {
                return new[]
                {
                    "PEPlugin.View.IPEPMDViewConnector.GetSelectedFaceIndices()",
                    "PXCPlugin.IPXCPluginConnector.GetSelectedFaceIndices()",
                };
            }
        }

        /// <summary>画面へ面の位置を渡す行。</summary>
        public static IList<string> Writing
        {
            get
            {
                return new[]
                {
                    "PEPlugin.View.IPEPMDViewConnector.SetSelectedFaceIndices(System.Int32[])",
                    "PXCPlugin.IPXCPluginConnector.SetSelectedFaceIndices(System.Int32[])",
                };
            }
        }

        /// <summary>表の中の面の選択の行を、換算を掛けたものへ置き換える。ほかの行は触らない。</summary>
        public static void Fit(IDictionary<string, SdkCall> calls)
        {
            if (calls == null)
            {
                throw new ArgumentNullException(nameof(calls));
            }

            foreach (string rowKey in Writing)
            {
                SdkCall held;
                if (calls.TryGetValue(rowKey, out held))
                {
                    SdkCall inner = held;
                    calls[rowKey] = (target, arguments) => inner(
                        target,
                        new object[] { ViewSelection.Spread(Places(arguments)) });
                }
            }

            foreach (string rowKey in Reading)
            {
                SdkCall held;
                if (calls.TryGetValue(rowKey, out held))
                {
                    SdkCall inner = held;
                    calls[rowKey] = (target, arguments) =>
                        ViewSelection.Gather((int[])inner(target, arguments) ?? new int[0]).ToArray();
                }
            }
        }

        /// <summary>渡された引数が持つ位置。渡されていなければ何も選ばないものとして扱う。</summary>
        private static int[] Places(object[] arguments)
        {
            return arguments == null || arguments.Length == 0
                ? new int[0]
                : (int[])arguments[0] ?? new int[0];
        }
    }
}
