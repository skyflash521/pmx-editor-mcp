using System;

namespace PmxEditorMcp
{
    /// <summary>取り返しの付かない呼び出しの種別。</summary>
    public enum DangerKind
    {
        /// <summary>取り消せるので確認は要らない。</summary>
        None,

        /// <summary>エディタそのものを終わらせる。</summary>
        Shutdown,

        /// <summary>ファイルへ書き込む。</summary>
        Overwrite,

        /// <summary>開いているPMXの中身を一度に空にする。</summary>
        Reset,
    }

    /// <summary>
    /// 取り返しの付かない呼び出しを、確認が無いまま実行させない。要否の判定をここに集めるのは、
    /// ツールごとに判断すると、同じ種別でも確認を求める側と求めない側に分かれるからである。
    /// </summary>
    public static class ConfirmGate
    {
        /// <summary>
        /// 確認が要るか。対象を選べる初期化だけは要否が対象で分かれるので、この入口では扱わず
        /// <see cref="ClearNeedsConfirm"/> が受け持つ。
        /// </summary>
        public static bool NeedsConfirm(DangerKind kind)
        {
            switch (kind)
            {
                case DangerKind.None:
                    return false;

                case DangerKind.Shutdown:
                case DangerKind.Overwrite:
                case DangerKind.Reset:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "知らない種別。");
            }
        }

        /// <summary>
        /// 対象を選べる初期化で確認が要るか。空にするのがエディタの開いているPMXのときだけ要る。
        /// 対象を指定して呼ぶときに空になるのはメモリの上の生成物なので、要らない。
        /// </summary>
        public static bool ClearNeedsConfirm(bool emptiesOpenPmx)
        {
            return emptiesOpenPmx;
        }

        /// <summary>
        /// 確認が要る呼び出しに <paramref name="confirm"/> が無ければ偽を返し、断る内容を渡す。
        /// 真を返したときは <paramref name="code"/> も <paramref name="message"/> も持たない。
        /// </summary>
        public static bool TryPass(DangerKind kind, bool confirm, out string code, out string message)
        {
            bool needs = NeedsConfirm(kind);
            code = null;
            message = null;
            if (confirm || !needs)
            {
                return true;
            }

            code = ToolEnvelope.ConfirmRequired;
            message = Describe(kind) + "。取り返しが付かないので、confirm を真にして呼ぶ。";

            return false;
        }

        /// <summary>対象を選べる初期化について、<see cref="TryPass"/> と同じことを行う。</summary>
        public static bool TryPassClear(
            bool emptiesOpenPmx, bool confirm, out string code, out string message)
        {
            if (!ClearNeedsConfirm(emptiesOpenPmx))
            {
                code = null;
                message = null;
                return true;
            }

            return TryPass(DangerKind.Reset, confirm, out code, out message);
        }

        private static string Describe(DangerKind kind)
        {
            switch (kind)
            {
                case DangerKind.Shutdown:
                    return "エディタを終わらせる";

                case DangerKind.Overwrite:
                    return "ファイルへ書き込む";

                case DangerKind.Reset:
                    return "開いているPMXの中身を空にする";

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "知らない種別。");
            }
        }
    }
}
