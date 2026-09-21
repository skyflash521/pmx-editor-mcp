using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>要求仕様書が並べる要求の名前。</summary>
    public sealed class RequirementNames
    {
        public RequirementNames(IList<string> tasks, IList<string> conditions)
        {
            if (tasks == null)
            {
                throw new ArgumentNullException(nameof(tasks));
            }

            if (conditions == null)
            {
                throw new ArgumentNullException(nameof(conditions));
            }

            Tasks = new ReadOnlyCollection<string>(tasks);
            Conditions = new ReadOnlyCollection<string>(conditions);
        }

        /// <summary>作業の要求の節の題。</summary>
        public IList<string> Tasks { get; }

        /// <summary>作業に依らない要件の節の題。</summary>
        public IList<string> Conditions { get; }
    }

    /// <summary>
    /// 要求仕様書から要求の名前を読む。要求は文が正本なので、受入シナリオが指す先が実在するかは
    /// この文書に突き合わせて判じる。
    /// </summary>
    public static class RequirementDocumentReader
    {
        private const string TaskSection = "## 作業の要求";

        private const string ConditionSection = "## 作業に依らない要件";

        private const string Heading = "### ";

        /// <summary>節が見つからなければ <see cref="FormatException"/>。</summary>
        public static RequirementNames Read(string markdown)
        {
            if (markdown == null)
            {
                throw new ArgumentNullException(nameof(markdown));
            }

            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            List<string> tasks = new List<string>();
            List<string> conditions = new List<string>();
            string section = null;
            foreach (string line in lines)
            {
                string text = line.Trim();
                if (text.StartsWith("## ", StringComparison.Ordinal))
                {
                    section = text;
                    continue;
                }

                if (!text.StartsWith(Heading, StringComparison.Ordinal))
                {
                    continue;
                }

                string name = text.Substring(Heading.Length).Trim();
                if (section == TaskSection)
                {
                    tasks.Add(name);
                }
                else if (section == ConditionSection)
                {
                    conditions.Add(name);
                }
            }

            if (tasks.Count == 0)
            {
                throw new FormatException(TaskSection + " に要求の節が無い。");
            }

            if (conditions.Count == 0)
            {
                throw new FormatException(ConditionSection + " に要件の節が無い。");
            }

            return new RequirementNames(tasks, conditions);
        }
    }
}
