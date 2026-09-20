using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// Undoの記録まわりの前置きを済ませてからツールを呼ぶ包み。止めることを頼めない分類が頼んで
    /// いれば断り、止めたまま戻せていないものがあれば、まず戻しにいき、戻らなければ分類ごとの
    /// 決まりで断るか警告を添える。生成したツールと組み立てたツールが同じ前置きを通るよう、
    /// 包み方はここだけが持つ。
    /// </summary>
    public sealed class UndoBarrier
    {
        /// <summary>抑止を頼む共通引数の名前。</summary>
        public const string SuppressName = "suppressUndo";

        private readonly UndoRecovery _recovery;

        /// <summary>止めたままの記録を戻しにいく窓口を与えて生成する。</summary>
        public UndoBarrier(UndoRecovery recovery)
        {
            if (recovery == null)
            {
                throw new ArgumentNullException(nameof(recovery));
            }

            _recovery = recovery;
        }

        /// <summary>抑止を頼まれているかを読む。値の形が違えば偽で、断る内容を渡す。</summary>
        public static bool TrySuppress(
            McpMethodContext context, out bool suppress, out string code, out string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 包みへ知らせを載せる。成功した呼び出しには警告として足し、失敗した呼び出しには誤りの
        /// 説明へ足す。
        /// </summary>
        public static object Noted(object envelope, IList<string> notices)
        {
            throw new NotImplementedException();
        }

        /// <summary>前置きを済ませてから <paramref name="inner"/> を呼ぶ呼び出しにする。</summary>
        public McpMethod Guard(EditKind kind, McpMethod inner)
        {
            throw new NotImplementedException();
        }
    }
}
