using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>実機の検査が1件で確かめること。</summary>
    public enum E2eExpectation
    {
        /// <summary>値を返して成功すること。</summary>
        Success,

        /// <summary>断ること。断る理由の綴りは <see cref="E2eCase.Code"/> が持つ。</summary>
        Refusal,

        /// <summary>
        /// 呼び先が在ること。実装の無い行はホストが未知のメソッドとして断るので、そこだけが落ちる。
        /// </summary>
        Dispatched,

        /// <summary>
        /// 返す絵が、写し取ったビューの姿と合うこと。合う相手は <see cref="E2eCase.View"/> が持ち、
        /// 別のビューを返す行はその写しと合わないことを確かめる。
        /// </summary>
        ViewImage,
    }

    /// <summary>実機のエディタへ1件だけ投げる検査。</summary>
    public sealed class E2eCase
    {
        public E2eCase(
            string rowKey,
            string editKind,
            string connectionPath,
            string tool,
            string purpose,
            IDictionary<string, object> arguments,
            E2eExpectation expectation,
            string code,
            string view = null)
        {
            RowKey = rowKey;
            EditKind = editKind;
            ConnectionPath = connectionPath;
            Tool = tool;
            Purpose = purpose;
            Arguments = new ReadOnlyDictionary<string, object>(
                arguments ?? new Dictionary<string, object>(StringComparer.Ordinal));
            Expectation = expectation;
            Code = code;
            View = view;
        }

        /// <summary>能力対応表の行キー。合否はこの単位でも数える。</summary>
        public string RowKey { get; }

        /// <summary>編集の流れ。合否はこの単位でも数える。</summary>
        public string EditKind { get; }

        /// <summary>接続の根から受け手の型へ至る経路。辿り着けない型では空。</summary>
        public string ConnectionPath { get; }

        /// <summary>呼ぶツールの名前。</summary>
        public string Tool { get; }

        /// <summary>この1件が確かめること。落ちたときに何を見ていたかが分かる言葉にする。</summary>
        public string Purpose { get; }

        /// <summary>渡す引数。</summary>
        public IDictionary<string, object> Arguments { get; }

        /// <summary>確かめる結末。</summary>
        public E2eExpectation Expectation { get; }

        /// <summary>断ることを確かめるとき、その理由の綴り。成功を確かめるときは null。</summary>
        public string Code { get; }

        /// <summary>返す絵が写すビューの名前。絵を確かめる検査だけが持ち、ほかは null。</summary>
        public string View { get; }
    }
}
