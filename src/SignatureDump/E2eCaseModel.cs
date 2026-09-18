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
        /// 返す画像が、写し取ったビューの姿と合うこと。どのビューを写して合わせるかは
        /// <see cref="E2eCase.View"/> が持つ。
        /// </summary>
        ViewImage,

        /// <summary>
        /// 読み返した項目が、書いた値のまま読めること。どの項目が何を持つはずかは
        /// <see cref="E2eCase.Expected"/> が持つ。
        /// </summary>
        Reads,

        /// <summary>
        /// 呼ぶ前に読んだものと違うものが読めること。何と比べるかは <see cref="E2eCase.Differs"/>
        /// が持つ。どこがどう変わるかまでは述べない行のために在る——変わったことだけが、その行の
        /// 述べる効果である。
        /// </summary>
        Changed,

        /// <summary>
        /// 呼び出しが呼び先まで届くこと。人の応答を待つ表示が出たことを知らせる応答は、届いたと
        /// 数えない。
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
            string says = null,
            string writes = null,
            string differs = null,
            string checks = null)
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
            Writes = writes;
            Differs = differs;
            Checks = checks;
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

        /// <summary>返す画像が写すビューの名前。画像を確かめる検査だけが持ち、ほかは null。</summary>
        public string View { get; }

        /// <summary>
        /// 返った値を覚えておく名前。あとの検査はこの名前から借りる。覚えない検査は null。
        /// </summary>
        public string Produces { get; }

        /// <summary>
        /// 引数の中の道から、借りる元の道へ。どちらも斜線で区切る。借りる元は覚えた値の名前で
        /// 始まり、続く段はその値の中を指す——指した先の値が、引数の道の行き着く位置へ入る。
        /// 借りない検査は null。
        /// </summary>
        public IDictionary<string, string> Borrowed { get; }

        /// <summary>読み返して確かめる項目。読み返す検査だけが持ち、ほかは null。</summary>
        public E2eExpectedMember Expected { get; }

        /// <summary>
        /// 断る理由を見分ける文面。呼び先が断ることを確かめる検査だけが持ち、ほかは null。
        /// 綴りだけでは、狙った理由とほかの失敗を見分けられない。
        /// </summary>
        public string Says { get; }

        /// <summary>
        /// 書いた先のパスを渡す引数の名前。呼び出しが成功したあと、その位置にファイルが在ることを
        /// 確かめる。ファイルを書かない検査は null。
        /// </summary>
        public string Writes { get; }

        /// <summary>
        /// 呼ぶ前に読んだものを覚えておいた名前。読めたものがその値と違うことを確かめる。
        /// 読み比べない検査は null。
        /// </summary>
        public string Differs { get; }

        /// <summary>
        /// この検査が確かめる事後条件の識別子。確かめない検査は null。行の覆いを数える検査が
        /// これを読む——どの検査がどの宣言を確かめたのかは、結末や引数の形からは見分けられない。
        /// 実機へ投げる綴りには出さない。実行器はどの宣言のために呼ぶのかを知らずに済む。
        /// </summary>
        public string Checks { get; }
    }
}
