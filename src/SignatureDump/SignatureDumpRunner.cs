using System;
using System.IO;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>実行1回ぶんの配線。引数の検査・アセンブリの読み込み・列挙・書き出しを順に行う。</summary>
    public static class SignatureDumpRunner
    {
        public const int ExitSuccess = 0;

        /// <summary>引数が足りない・多いときの終了コード。</summary>
        public const int ExitInvalidArguments = 2;

        public const int ExitAssemblyUnavailable = 3;

        /// <summary>
        /// 実行する。引数はPMXエディタ導入ディレクトリと書き出し先パスの2つ。結果は書き出し先へ
        /// BOMなしUTF-8で書く。成功したときは要約を <paramref name="output"/> へ書き、
        /// <paramref name="error"/> には何も書かない。失敗したときはその説明を
        /// <paramref name="error"/> へ書き、<paramref name="output"/> には何も書かない。
        /// </summary>
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            throw new NotImplementedException();
        }
    }
}
