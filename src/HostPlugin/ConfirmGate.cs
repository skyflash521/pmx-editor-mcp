using System;

namespace PmxEditorMcp
{
    public enum DangerKind
    {
        None,

        /// <summary>エディタそのものを終わらせる。</summary>
        Shutdown,

        /// <summary>ファイルへ書き込む。</summary>
        Overwrite,

        /// <summary>開いているPMXの中身を一度に空にする。</summary>
        Reset,
    }

    public static class ConfirmGate
    {
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
