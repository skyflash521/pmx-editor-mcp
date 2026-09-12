using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// 呼ぶ前に確かめることを満たしているかを判じる。満たさないまま呼ぶと、エディタが人の応答を
    /// 待つ表示を出したり、押されているキーで結果が変わったりして、呼び出し側が頼んだとおりの
    /// ことが起きない。
    /// </summary>
    public static class PreconditionGate
    {
        /// <summary>
        /// 満たしていれば真。満たしていなければ偽で、断る理由を <paramref name="message"/> に
        /// 持たせる。<paramref name="counted"/> は種別ごとに数えたもの——いま選ばれているものの数か、
        /// 取り消せる編集の数——で、数えられなかったときは null とする。数えられなかったことと0件は
        /// 別で、前者を後者として扱うと、直し方の分からない断り方になる。
        /// <paramref name="modified"/> は修飾キーが押されているかどうか。
        /// </summary>
        public static bool TryAccept(
            PreconditionKind kind, int? counted, bool modified, out string message)
        {
            message = null;
            if (kind == PreconditionKind.SavedEdits)
            {
                return TryClosable(counted, out message);
            }

            if (kind != PreconditionKind.PickedObjects)
            {
                return true;
            }

            int? picked = counted;
            if (picked == null)
            {
                message = "いま選ばれているものを数えられなかったので、呼んでよいかを確かめられない。";

                return false;
            }

            if (modified)
            {
                message = "修飾キーが押されている間は、取り込み方がその押し方で変わるので呼べない。"
                    + "キーを放してから呼ぶ。";

                return false;
            }

            if (picked <= 0)
            {
                message = "取り込む対象が1つも選ばれていない。"
                    + "ビューで対象を選んでから呼ぶ。選ばれていないまま呼ぶと、"
                    + "エディタが一覧を空にしてよいかを尋ねる表示を出して止まる。";

                return false;
            }

            return true;
        }

        /// <summary>
        /// 閉じてよいか。閉じても表示が出ないとこちらから言えるのは、取り消せる編集が残っていない
        /// ときだけなので、残っている間は閉じない。出すかどうかを決めているのはエディタが持つ
        /// 保存済みの印との差で、その印は読めず、プラグインからの保存でも変わらない。
        /// </summary>
        private static bool TryClosable(int? undoable, out string message)
        {
            message = null;
            if (undoable == null)
            {
                message = "取り消せる編集の数を読めなかったので、閉じてよいかを確かめられない。";

                return false;
            }

            if (undoable > 0)
            {
                message = "取り消せる編集が残っているので閉じない。"
                    + "残っていると、エディタが未保存の編集について尋ねる表示を出して止まることがあり、"
                    + "出ないと確かめられるのは0件のときだけである。"
                    + "エディタ側で閉じるか、編集を取り消してから呼ぶ。";

                return false;
            }

            return true;
        }
    }
}
