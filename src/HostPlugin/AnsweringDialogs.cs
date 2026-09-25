using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    public static class AnsweringDialogs
    {
        private static readonly Dictionary<IntPtr, HashSet<IntPtr>> GivenBack =
            new Dictionary<IntPtr, HashSet<IntPtr>>();

        /// <summary>
        /// <paramref name="owner"/> を持ち主とするダイアログ(#32770)は、<see cref="GiveBack"/> したものを除き、
        /// <see cref="Remove"/> までの間、人の応答を待つ表示に数えない。どのスレッドからも呼べる。
        /// </summary>
        public static void Add(IntPtr owner)
        {
            lock (GivenBack)
            {
                GivenBack[owner] = new HashSet<IntPtr>();
            }
        }

        public static void GiveBack(IntPtr owner, IntPtr dialog)
        {
            lock (GivenBack)
            {
                HashSet<IntPtr> dialogs;
                if (GivenBack.TryGetValue(owner, out dialogs))
                {
                    dialogs.Add(dialog);
                }
            }
        }

        public static void Remove(IntPtr owner)
        {
            lock (GivenBack)
            {
                GivenBack.Remove(owner);
            }
        }

        public static bool Hides(IntPtr owner, IntPtr dialog)
        {
            lock (GivenBack)
            {
                HashSet<IntPtr> dialogs;

                return GivenBack.TryGetValue(owner, out dialogs) && !dialogs.Contains(dialog);
            }
        }
    }
}
