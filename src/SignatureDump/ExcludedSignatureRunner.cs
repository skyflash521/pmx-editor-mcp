using System;
using System.IO;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 除外一覧を書き出す実行1回ぶんの配線。凍結した組と、その時点のSDKの公開シグネチャの両方を
    /// 読んで確定するので、どちらかが欠けても食い違っても書き出さない。
    /// </summary>
    public static class ExcludedSignatureRunner
    {
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            throw new NotImplementedException();
        }
    }
}
