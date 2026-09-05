using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ツールの名前を規則から組み立て、群のなかで衝突する動作の語を数える。名前を作るのも衝突を
    /// 数えるのも同じ分け方を見るので、ここ1つに置く。
    /// </summary>
    public static class ToolNameRule
    {
        private const char Separator = '_';

        /// <summary>メンバー名を動作の語へ写す。大文字の区切りを下線へ置き換えて小文字にする。</summary>
        public static string ActionWord(string memberName)
        {
            RequireText(memberName, nameof(memberName));

            StringBuilder built = new StringBuilder();
            for (int index = 0; index < memberName.Length; index++)
            {
                char letter = memberName[index];
                if (char.IsUpper(letter) && built.Length != 0 && Breaks(memberName, index))
                {
                    built.Append(Separator);
                }

                built.Append(char.ToLower(letter, CultureInfo.InvariantCulture));
            }

            return built.ToString();
        }

        /// <summary>群・動作の語・出所修飾から名前を組み立てる。出所修飾は常に後置する。</summary>
        public static string Compose(string group, string actionWord, string qualifier)
        {
            RequireText(group, nameof(group));
            RequireText(actionWord, nameof(actionWord));

            string composed = group + Separator + actionWord;
            return qualifier == null || qualifier.Length == 0
                ? composed
                : composed + Separator + qualifier;
        }

        /// <summary>同じ群で2件以上に現れる動作の語を、群ごとに返す。</summary>
        public static IDictionary<string, ISet<string>> Colliding(
            IEnumerable<KeyValuePair<string, string>> actionWords)
        {
            if (actionWords == null)
            {
                throw new ArgumentNullException(nameof(actionWords));
            }

            Dictionary<string, Dictionary<string, int>> counted =
                new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> word in actionWords)
            {
                RequireText(word.Key, nameof(actionWords));
                RequireText(word.Value, nameof(actionWords));

                Dictionary<string, int> inGroup;
                if (!counted.TryGetValue(word.Key, out inGroup))
                {
                    inGroup = new Dictionary<string, int>(StringComparer.Ordinal);
                    counted.Add(word.Key, inGroup);
                }

                int seen;
                inGroup[word.Value] = inGroup.TryGetValue(word.Value, out seen) ? seen + 1 : 1;
            }

            Dictionary<string, ISet<string>> colliding =
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Dictionary<string, int>> group in counted)
            {
                HashSet<string> repeated = new HashSet<string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, int> word in group.Value)
                {
                    if (word.Value >= 2)
                    {
                        repeated.Add(word.Key);
                    }
                }

                if (repeated.Count != 0)
                {
                    colliding.Add(group.Key, repeated);
                }
            }

            return colliding;
        }

        // 大文字が語の切れ目かどうか。小文字か数字の後ろと、大文字の並びの最後の1つが切れ目になる。
        private static bool Breaks(string memberName, int index)
        {
            char previous = memberName[index - 1];
            if (previous == Separator)
            {
                return false;
            }

            if (!char.IsUpper(previous))
            {
                return true;
            }

            return index + 1 < memberName.Length && char.IsLower(memberName[index + 1]);
        }

        private static void RequireText(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", name);
            }
        }
    }
}
