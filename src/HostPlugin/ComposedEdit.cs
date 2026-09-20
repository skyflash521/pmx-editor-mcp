using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>組み立てたツールの中身が返す結末。値を返すか、断る内容を返すかのどちらか。</summary>
    public sealed class ComposedEditResult
    {
        private ComposedEditResult(bool done, object value, string code, string message)
        {
            IsDone = done;
            Value = value;
            Code = code;
            Message = message;
        }

        /// <summary>済んだか。偽なら断っている。</summary>
        public bool IsDone { get; }

        /// <summary>済んだときに返す中身。</summary>
        public object Value { get; }

        /// <summary>断ったときの誤りの符号。</summary>
        public string Code { get; }

        /// <summary>断ったときの説明。</summary>
        public string Message { get; }

        /// <summary>済んだ結末を作る。</summary>
        public static ComposedEditResult Complete(object value)
        {
            throw new NotImplementedException();
        }

        /// <summary>断る結末を作る。</summary>
        public static ComposedEditResult Refuse(string code, string message)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 1メンバーへ写らない組み立てのツールを、生成したツールと同じ複製編集の経路へ乗せる枠。
    /// どのPMXを相手にするかの解決・UIスレッドへの委譲・まとめての反映・失敗したときの状態の
    /// 言い方を引き受けるので、ツールの側は複製を受け取って変えるところだけを書く。
    /// </summary>
    public sealed class ComposedEdit
    {
        private readonly PmxSession _session;

        private readonly UndoBarrier _barrier;

        /// <summary>複製編集の流れと、Undoの前置きの包みを与えて生成する。</summary>
        public ComposedEdit(PmxSession session, UndoBarrier barrier)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (barrier == null)
            {
                throw new ArgumentNullException(nameof(barrier));
            }

            _session = session;
            _barrier = barrier;
        }

        /// <summary>
        /// <paramref name="body"/> を複製編集の経路へ乗せた呼び出しにする。
        /// <paramref name="known"/> はそのツールが受け取る項目の名前で、どのPMXを相手にするかの
        /// 指定とUndoの抑止の頼みはここが足す。<paramref name="body"/> はUIスレッドの上で、
        /// 相手にするPMXを受け取って呼ばれる。
        /// </summary>
        public McpMethod Method(
            IList<string> known, Func<McpMethodContext, object, ComposedEditResult> body)
        {
            throw new NotImplementedException();
        }
    }
}
