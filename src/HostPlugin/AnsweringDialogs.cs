using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    public static class AnsweringDialogs
    {
        private static readonly Dictionary<IntPtr, Answering> Owners = new Dictionary<IntPtr, Answering>();

        /// <summary>
        /// <paramref name="owner"/> を持ち主とするダイアログ(#32770)と、題が <paramref name="captions"/> の
        /// どれかに当たる表示は(<paramref name="owner"/> が <see cref="IntPtr.Zero"/> ならすべての表示は)、<see cref="GiveBack"/> したものを除き、<see cref="Remove"/> までの間、人の応答を
        /// 待つ表示に数えない。どのスレッドからも呼べる。
        /// </summary>
        public static void Add(IntPtr owner, IEnumerable<string> captions)
        {
            if (captions == null)
            {
                throw new ArgumentNullException(nameof(captions));
            }

            lock (Owners)
            {
                Owners[owner] = new Answering(captions);
            }
        }

        public static void Add(IntPtr owner)
        {
            Add(owner, new string[0]);
        }

        public static void GiveBack(IntPtr owner, IntPtr dialog)
        {
            lock (Owners)
            {
                Answering answering;
                if (Owners.TryGetValue(owner, out answering))
                {
                    answering.GivenBack.Add(dialog);
                }
            }
        }

        public static void Remove(IntPtr owner)
        {
            lock (Owners)
            {
                Owners.Remove(owner);
            }
        }

        /// <summary><paramref name="isDialog"/> は <paramref name="window"/> が #32770 かどうか。</summary>
        public static bool Hides(IntPtr owner, IntPtr window, string caption, bool isDialog)
        {
            lock (Owners)
            {
                foreach (KeyValuePair<IntPtr, Answering> entry in Owners)
                {
                    if (entry.Value.GivenBack.Contains(window))
                    {
                        continue;
                    }

                    if (entry.Key == IntPtr.Zero
                        || (entry.Key == owner && isDialog)
                        || (caption != null && entry.Value.Captions.Contains(caption)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class Answering
        {
            internal Answering(IEnumerable<string> captions)
            {
                Captions = new HashSet<string>(captions, StringComparer.Ordinal);
            }

            internal HashSet<string> Captions { get; }

            internal HashSet<IntPtr> GivenBack { get; } = new HashSet<IntPtr>();
        }
    }
}
