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

        /// <summary>
        /// 読み返した項目が、書いた値のまま読めること。どの項目が何を持つはずかは
        /// <see cref="E2eCase.Expected"/> が持つ。
        /// </summary>
        Reads,

        /// <summary>
        /// 呼び出しが呼び先まで届くこと。成功するか、人の応答を待つ表示が出たことを戻り値で
        /// 知らせるかのどちらかであればよい——表示が出るかどうかはエディタの状態で決まり、
        /// 出たときにそれを知らせるのが決められた振る舞いである。
        /// </summary>
        Called,

        /// <summary>
        /// 呼び先まで届いたうえで、渡した値を呼び先が断ること。断る理由の綴りは
        /// <see cref="E2eCase.Code"/> が、その理由を見分ける文面は <see cref="E2eCase.Says"/> が
        /// 持つ。入口で断られる呼び出しと違い、この断りは呼び先まで届いた証しになる。
        /// </summary>
        Denied,
    }

    /// <summary>読み返して確かめる項目と、その項目が持つはずの値。</summary>
    public sealed class E2eExpectedMember
    {
        public E2eExpectedMember(string member, object value)
        {
            if (string.IsNullOrEmpty(member))
            {
                throw new ArgumentException("読み返す項目の名前が要る。", nameof(member));
            }

            Member = member;
            Value = value;
        }

        /// <summary>読み返す項目の名前。</summary>
        public string Member { get; }

        /// <summary>その項目が持つはずの値。関連が無いことは null で表す。</summary>
        public object Value { get; }
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
            string view = null,
            string produces = null,
            IDictionary<string, string> borrowed = null,
            E2eExpectedMember expected = null,
            string says = null)
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
            Produces = produces;
            Borrowed = borrowed == null
                ? null
                : new ReadOnlyDictionary<string, string>(borrowed);
            Expected = expected;
            Says = says;
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

        /// <summary>
        /// 返った値を覚えておく名前。あとの検査がこの名前で借りる。覚えない検査は null。
        /// </summary>
        public string Produces { get; }

        /// <summary>
        /// 引数の名前から、覚えた値の名前へ。借りた値は1件の並びとして渡る。借りない検査は null。
        /// </summary>
        public IDictionary<string, string> Borrowed { get; }

        /// <summary>読み返して確かめる項目。読み返す検査だけが持ち、ほかは null。</summary>
        public E2eExpectedMember Expected { get; }

        /// <summary>
        /// 断る理由を見分ける文面。呼び先が断ることを確かめる検査だけが持ち、ほかは null。
        /// 綴りだけでは、狙った理由とほかの失敗を見分けられない。
        /// </summary>
        public string Says { get; }
    }
}
