using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>呼ぶ前に確かめることの種別。</summary>
    public enum PreconditionKind
    {
        /// <summary>確かめることは無い。</summary>
        None,

        /// <summary>
        /// いま選ばれている対象を相手にする。選ばれているものが無いとエディタが人の応答を待つ表示を
        /// 出し、押されている修飾キーで相手の決まり方が変わる。
        /// </summary>
        PickedObjects,

        /// <summary>
        /// 取り消せる編集が残っていると、エディタが人の応答を待つ表示を出すことがある。出すかどうかは
        /// エディタが持つ保存済みの印との差で決まり、その印は読めない。プラグインからの保存もその印を
        /// 更新しないので、こちらから出ないと言えるのは、取り消せる編集が残っていないときだけである。
        /// </summary>
        SavedEdits,

        /// <summary>
        /// PMXビューの絞込が持つ一覧を相手にする。この一覧は絞込の窓を一度表示するまで組まれず、
        /// 組まれていない間、読み取りは並んでいる項目が無いまま空を返し、書き込みはどの項目へも
        /// 届かない。項目の数は呼ぶ前に読める。
        /// </summary>
        ListedParts,

        /// <summary>
        /// 操作を1つ取り消すか、やり直す。取り消せる操作・やり直せる操作が残っていないと、エディタは
        /// 何もせずに戻る。残っている数は呼ぶ前に読める。
        /// </summary>
        UndoHistory,

        /// <summary>
        /// TransformView のビューで選んでいるボーンを、入力欄の値だけ動かす。ビューでボーンが
        /// 選ばれていないと、エディタは何もせずに戻る。動かす量は押されている修飾キーで向きと倍率が
        /// 変わる。ビューの選択は読めず、呼ぶ前に読めるのは一覧で選ばれているボーンの位置だけである。
        /// 表示されているボーンを一覧で選ぶと、ビューの選択も同じボーンになる。
        /// </summary>
        TransformedBone,

        /// <summary>
        /// 画面のボタンの処理をそのまま呼ぶ。その処理は押されている修飾キーを読み、頂点編集では編集の量と向きが、
        /// 頂点ガイドの選択ではいまの選択との合わせ方が変わる。
        /// </summary>
        HeldModifiers,
    }

    /// <summary>
    /// 呼ぶ前に確かめること。確かめる材料は、名前で挙げた読み取りのツールから得る。名前で持つのは、
    /// 同じ呼び出しをここでもう一度組み立てないためで、名前から呼び出しへ解くのは登録のときとする
    /// ——解いたものを使えば、確かめるのと本体を呼ぶのは同じUIスレッドの一区切りに収まる。
    /// </summary>
    public sealed class ToolPrecondition
    {
        public ToolPrecondition(
            PreconditionKind kind, IEnumerable<string> reading, IEnumerable<string> counting)
            : this(kind, reading, counting, new string[0])
        {
        }

        /// <param name="guarded">確かめる呼び分けの行キー。空ならそのツールのどの呼び分けでも確かめる。</param>
        public ToolPrecondition(
            PreconditionKind kind,
            IEnumerable<string> reading,
            IEnumerable<string> counting,
            IEnumerable<string> guarded)
        {
            if (guarded == null)
            {
                throw new ArgumentNullException(nameof(guarded));
            }

            if (reading == null)
            {
                throw new ArgumentNullException(nameof(reading));
            }

            if (counting == null)
            {
                throw new ArgumentNullException(nameof(counting));
            }

            Kind = kind;
            Reading = new ReadOnlyCollection<string>(reading.ToList());
            Counting = new ReadOnlyCollection<string>(counting.ToList());
            Guarded = new ReadOnlyCollection<string>(guarded.ToList());
        }

        /// <summary>確かめることの種別。</summary>
        public PreconditionKind Kind { get; }

        /// <summary>確かめる材料を得る読み取りのツールの名前。別の受け手から読むものが入る。</summary>
        public IList<string> Reading { get; }

        /// <summary>
        /// 確かめる材料を得る行キー。呼ぶ先と同じ受け手の上で読むものが入るので、受け手を解き直さない。
        /// </summary>
        public IList<string> Counting { get; }

        /// <summary>確かめる呼び分けの行キー。空ならそのツールのどの呼び分けでも確かめる。</summary>
        public IList<string> Guarded { get; }
    }
}
