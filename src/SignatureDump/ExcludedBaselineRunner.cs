using System;
using System.IO;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 凍結した除外の組を書き出す実行1回ぶんの配線。台帳と、その時点のSDKの公開シグネチャの
    /// 両方を読んで確定するので、どちらかが欠けても食い違っても書き出さない。
    /// </summary>
    public static class ExcludedBaselineRunner
    {
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            throw new NotImplementedException();
        }
    }
}
