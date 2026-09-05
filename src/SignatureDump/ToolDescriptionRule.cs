using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>説明文へ索引語として並べる項目。</summary>
    public sealed class IndexTerm
    {
        public IndexTerm(string name, string japaneseName)
        {
            Name = Required(name, nameof(name));
            JapaneseName = Required(japaneseName, nameof(japaneseName));
        }

        /// <summary>項目の名前。載せきれなかったときはこの名前で記録する。</summary>
        public string Name { get; }

        /// <summary>日本語名。利用者の語彙からツールへ至る索引になる。</summary>
        public string JapaneseName { get; }

        private static string Required(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", name);
            }

            return value;
        }
    }

    /// <summary>1つのツールの説明文を組み立てる材料。</summary>
    public sealed class ToolDescriptionMaterial
    {
        public ToolDescriptionMaterial(
            string tool,
            string group,
            string actionWord,
            string qualifier,
            string elementNoun,
            string typeName,
            string contractNote,
            string sourceNote,
            IList<IndexTerm> indexTerms)
        {
            Tool = Required(tool, nameof(tool));
            Group = Required(group, nameof(group));
            ActionWord = Required(actionWord, nameof(actionWord));
            Qualifier = qualifier;
            ElementNoun = Required(elementNoun, nameof(elementNoun));
            TypeName = Required(typeName, nameof(typeName));
            ContractNote = contractNote;
            SourceNote = sourceNote;
            IndexTerms = new ReadOnlyCollection<IndexTerm>(
                indexTerms == null ? new List<IndexTerm>() : new List<IndexTerm>(indexTerms));
        }

        public string Tool { get; }

        /// <summary>群のプレフィクス。</summary>
        public string Group { get; }

        /// <summary>ツール名から群と出所修飾を除いた動作の語。</summary>
        public string ActionWord { get; }

        /// <summary>後置した出所修飾。付いていなければ null。</summary>
        public string Qualifier { get; }

        /// <summary>対象。型役割表の要素名詞。</summary>
        public string ElementNoun { get; }

        /// <summary>出所。SDKの型の名前。</summary>
        public string TypeName { get; }

        /// <summary>台帳の契約注記。持たなければ null。</summary>
        public string ContractNote { get; }

        /// <summary>一次資料の記載。持たなければ null。</summary>
        public string SourceNote { get; }

        /// <summary>集約したツールが並べる索引語。集約していなければ空。</summary>
        public IList<IndexTerm> IndexTerms { get; }

        private static string Required(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", name);
            }

            return value;
        }
    }

    /// <summary>組み立てた説明文と、上限に入りきらなかった項目。</summary>
    public sealed class ToolDescription
    {
        public ToolDescription(string text, IList<string> dropped)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Text = text;
            Dropped = new ReadOnlyCollection<string>(
                dropped == null ? new List<string>() : new List<string>(dropped));
        }

        public string Text { get; }

        /// <summary>載せきれなかった項目の名前。</summary>
        public IList<string> Dropped { get; }
    }

    /// <summary>
    /// ツールの説明文を組み立てる。説明文はクライアントの検索の入力なので、対象・動作・出所を
    /// 先頭にこの順で置き、続けて契約注記・一次資料の記載・索引語を載せる。
    /// </summary>
    public static class ToolDescriptionRule
    {
        /// <summary>説明文の上限。UTF-8のバイト数で数える。</summary>
        public const int LimitBytes = 2000;

        private const string TargetLabel = "対象 ";

        private const string ActionLabel = " / 動作 ";

        private const string SourceLabel = " / 出所 ";

        private const string ContractNoteLabel = "契約注記: ";

        private const string SourceNoteLabel = "一次資料: ";

        private const string IndexLabel = "索引語: ";

        private const string TermSeparator = "・";

        /// <summary>
        /// 説明文を組み立てる。索引語まで載せて上限を超えるときは、まず名前を落として日本語名だけに
        /// し、それでも超えるときは先頭から入るところまで載せて残りを <see cref="ToolDescription.Dropped"/>
        /// へ回す。
        /// </summary>
        public static ToolDescription Compose(ToolDescriptionMaterial material)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            string head = Head(material);
            if (material.IndexTerms.Count == 0)
            {
                return new ToolDescription(head, null);
            }

            string full = WithTerms(head, material.IndexTerms, t => t.Name + "(" + t.JapaneseName + ")");
            if (Bytes(full) <= LimitBytes)
            {
                return new ToolDescription(full, null);
            }

            return Truncated(head, material.IndexTerms);
        }

        // 先頭の1行。対象・動作・出所をこの順に置く。
        private static string Head(ToolDescriptionMaterial material)
        {
            StringBuilder built = new StringBuilder()
                .Append(TargetLabel).Append(material.ElementNoun)
                .Append(ActionLabel).Append(material.ActionWord)
                .Append(SourceLabel).Append(material.TypeName);
            Line(built, ContractNoteLabel, material.ContractNote);
            Line(built, SourceNoteLabel, material.SourceNote);
            return built.ToString();
        }

        private static void Line(StringBuilder built, string label, string note)
        {
            if (note != null && note.Trim().Length != 0)
            {
                built.Append('\n').Append(label).Append(note);
            }
        }

        private static string WithTerms(
            string head, IList<IndexTerm> terms, Func<IndexTerm, string> written)
        {
            StringBuilder built = new StringBuilder(head).Append('\n').Append(IndexLabel);
            for (int index = 0; index < terms.Count; index++)
            {
                if (index != 0)
                {
                    built.Append(TermSeparator);
                }

                built.Append(written(terms[index]));
            }

            return built.ToString();
        }

        private static ToolDescription Truncated(string head, IList<IndexTerm> terms)
        {
            List<IndexTerm> kept = new List<IndexTerm>();
            string text = head;
            for (int index = 0; index < terms.Count; index++)
            {
                kept.Add(terms[index]);
                string candidate = WithTerms(head, kept, t => t.JapaneseName);
                if (Bytes(candidate) > LimitBytes)
                {
                    kept.RemoveAt(kept.Count - 1);
                    break;
                }

                text = candidate;
            }

            List<string> dropped = new List<string>();
            for (int index = kept.Count; index < terms.Count; index++)
            {
                dropped.Add(terms[index].Name);
            }

            return new ToolDescription(text, dropped);
        }

        private static int Bytes(string text)
        {
            return Encoding.UTF8.GetByteCount(text);
        }
    }
}
