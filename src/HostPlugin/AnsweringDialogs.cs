using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class AnsweringDialogs
    {
        private static readonly Dictionary<IntPtr, Answering> Owners = new Dictionary<IntPtr, Answering>();

        /// <summary>
        /// <paramref name="owner"/> を持ち主とするダイアログ(#32770)と、Name が <paramref name="forms"/> のどれかに当たる
        /// WinForms のフォームは(<paramref name="owner"/> が <see cref="IntPtr.Zero"/> ならすべてのウィンドウは)、<see cref="GiveBack"/> したものを除き、<see cref="Remove"/> までの間、人の応答を
        /// 待つ表示に数えない。どのスレッドからも呼べる。
        /// </summary>
        public static void Add(IntPtr owner, IEnumerable<string> forms)
        {
            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            lock (Owners)
            {
                Owners[owner] = new Answering(forms);
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
        public static bool Hides(IntPtr owner, IntPtr window, bool isDialog)
        {
            string form = FormName(window);
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
                        || (form != null && entry.Value.Forms.Contains(form)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>どのスレッドからも呼べる。<paramref name="window"/> がこのプロセスの WinForms の部品なら、その Name。</summary>
        public static string FormName(IntPtr window)
        {
            Control control = Control.FromHandle(window);

            return control == null ? null : control.Name;
        }

        private sealed class Answering
        {
            internal Answering(IEnumerable<string> forms)
            {
                Forms = new HashSet<string>(forms, StringComparer.Ordinal);
            }

            internal HashSet<string> Forms { get; }

            internal HashSet<IntPtr> GivenBack { get; } = new HashSet<IntPtr>();
        }
    }
}
