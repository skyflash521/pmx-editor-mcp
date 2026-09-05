using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>合成ツールの1件。</summary>
    public sealed class ComposedTool
    {
        public ComposedTool(bool branching, string duty)
        {
            if (duty == null)
            {
                throw new ArgumentNullException(nameof(duty));
            }

            if (duty.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", nameof(duty));
            }

            Branching = branching;
            Duty = duty;
        }

        /// <summary>入出力の形がイベントの分岐を持つかどうか。</summary>
        public bool Branching { get; }

        /// <summary>受け持つこと。説明文の先頭の1行になる。</summary>
        public string Duty { get; }
    }

    /// <summary>
    /// 共通契約仕様書から合成ツールの名前と、その形がイベントの分岐を持つかどうかと、受け持つことを
    /// 読む。
    /// </summary>
    public static class ComposedToolDocument
    {
        public const string SectionHeading = "### 合成ツール";

        private const string Branching = "持つ";

        private const string NotBranching = "持たない";

        /// <summary>仕様書の本文から表を読む。表が無いか行が読めなければ例外。</summary>
        public static IDictionary<string, ComposedTool> Read(string text)
        {
            Dictionary<string, ComposedTool> tools =
                new Dictionary<string, ComposedTool>(StringComparer.Ordinal);
            foreach (string[] cells in SpecificationTable.Rows(text, SectionHeading, 3))
            {
                string line = string.Join(" | ", cells);
                string tool = SpecificationTable.Quoted(cells[0], line);
                if (tools.ContainsKey(tool))
                {
                    throw new InvalidOperationException("同じツールが二度現れる: " + tool);
                }

                if (cells[2].Length == 0)
                {
                    throw new InvalidOperationException("受け持つことの欄が空である: " + line);
                }

                tools.Add(tool, new ComposedTool(Branches(cells[1], line), cells[2]));
            }

            if (tools.Count == 0)
            {
                throw new InvalidOperationException("表に行が無い: " + SectionHeading);
            }

            return tools;
        }

        private static bool Branches(string cell, string line)
        {
            if (string.Equals(cell, Branching, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(cell, NotBranching, StringComparison.Ordinal))
            {
                return false;
            }

            throw new InvalidOperationException("分岐の欄が知らない語である: " + line);
        }
    }
}
