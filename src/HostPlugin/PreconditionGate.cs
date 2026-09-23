using System;
using System.Collections.Generic;

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
            PreconditionKind kind,
            int? counted,
            bool modified,
            out string message)
        {
            return TryAccept(kind, counted, modified, null, out message);
        }

        /// <param name="pointed">
        /// その一覧を位置で指す呼び出しが渡した位置。位置で指さない呼び出しでは null。
        /// </param>
        public static bool TryAccept(
            PreconditionKind kind,
            int? counted,
            bool modified,
            IList<long> pointed,
            out string message)
        {
            message = null;
            if (kind == PreconditionKind.SavedEdits)
            {
                return TryClosable(counted, out message);
            }

            if (kind == PreconditionKind.ListedParts)
            {
                return TryListed(counted, out message) && TryInside(counted, pointed, out message);
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
        /// 絞込の一覧を相手にしてよいか。<paramref name="listed"/> はその一覧に並んでいる項目の数で、
        /// 数えられなかったときは null とする。
        /// </summary>
        private static bool TryListed(int? listed, out string message)
        {
            message = null;
            if (listed == null)
            {
                message = "絞込の一覧に並んでいる項目の数を読めなかったので、"
                    + "呼んでよいかを確かめられない。"
                    + "この数を読む呼び出しが、このバージョンのSDKでは中継を持たないか組み立てられなかった。"
                    + "sdk_status で稼働しているSDKのバージョンと中継を作れなかった行を読む。";

                return false;
            }

            if (listed <= 0)
            {
                message = "PMXビューの絞込の一覧に項目が1つも並んでいないので呼べない。"
                    + "この一覧は絞込の窓を一度表示するまで組まれず、"
                    + "組まれていない間は読み取りが空を返し、書き込みはどの項目へも届かない。"
                    + "view_update_parts_select_window へ visible に真を渡して"
                    + "絞込の窓を開いてから呼ぶ。"
                    + "開いても項目が並ばないなら、モデルにその種類の要素が無い。";

                return false;
            }

            return true;
        }

        private static bool TryInside(int? listed, IList<long> pointed, out string message)
        {
            message = null;
            if (listed == null || pointed == null)
            {
                return true;
            }

            foreach (long at in pointed)
            {
                if (at < 0 || at >= listed.Value)
                {
                    message = "絞込の一覧に並んでいない位置を指している: " + at
                        + "。並んでいるのは 0 から " + (listed.Value - 1) + " までの "
                        + listed.Value + " 件で、この一覧はモデルの要素の数が変わっても"
                        + "組み直されない。"
                        + "view_update_model を挟むと組み直せる——ただし絞込の窓が表示されている"
                        + "ときに限る。";

                    return false;
                }
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
                message = "取り消せる編集の数を読めなかったので、閉じてよいかを確かめられない。"
                    + "この数を読む呼び出しが、このバージョンのSDKでは中継を持たないか組み立てられなかった。"
                    + "sdk_status で稼働しているSDKのバージョンと中継を作れなかった行を読む。";

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
