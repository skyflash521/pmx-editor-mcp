using System;
using System.IO;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 台帳と公開APIの突き合わせは工程ごとに別の入力と出力を持つので、下位コマンドの名前で
    /// 実行を振り分ける。
    /// </summary>
    public static class CommandRunner
    {
        /// <summary>SDKの公開APIを列挙して書き出す。</summary>
        public const string SignaturesCommand = "signatures";

        /// <summary>台帳の非対応記載を公開シグネチャの集合として凍結して書き出す。</summary>
        public const string ExcludedBaselineCommand = "excluded-baseline";

        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            throw new NotImplementedException();
        }
    }
}
