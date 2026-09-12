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
        /// 持たせる。<paramref name="picked"/> はいま選ばれているものの数で、数えられなかったときは
        /// null とする——数えられなかったことと0件は別で、前者を後者として扱うと、選び直しても
        /// 直らない断り方になる。<paramref name="modified"/> は修飾キーが押されているかどうか。
        /// </summary>
        public static bool TryAccept(
            PreconditionKind kind, int? picked, bool modified, out string message)
        {
            message = null;
            if (kind != PreconditionKind.PickedObjects)
            {
                return true;
            }

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
    }
}
