using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>UIスレッドで行う画面の操作と、その間にエディタが出したダイアログの片付けの結末。</summary>
    internal sealed class UiAnswering
    {
        private static readonly TimeSpan PromptReadLimit = TimeSpan.FromSeconds(1);

        private UiAnswering(string failure, IList<string> notices)
        {
            Failure = failure;
            Notices = notices;
        }

        /// <summary>閉じたダイアログのうち、操作の失敗を伝えていたもの。無ければ null。</summary>
        internal string Failure { get; }

        /// <summary>ボタン1つで閉じたメッセージボックスの本文。出た順に並ぶ。</summary>
        internal IList<string> Notices { get; }

        /// <summary>UIスレッドで呼ぶ。エディタが人の応答を待つ表示を出していれば、そのタイトルと本文。出していなければ null。</summary>
        internal static string Waiting()
        {
            return new DesktopModalWindowProbe(PromptReadLimit).TryDescribe();
        }

        /// <summary>UIスレッドで呼ぶ。<paramref name="operate"/> の間に出たダイアログを片付ける。</summary>
        internal static UiAnswering Around(Action operate)
        {
            DialogAnswer answer = DialogAnswer.StartAcknowledging(DialogAnswer.Limit);
            string failure;
            try
            {
                operate();
            }
            finally
            {
                failure = answer.Stop();
            }

            return new UiAnswering(failure, answer.Agreed);
        }

        /// <summary>操作が例外で終わったときの事情に、閉じたダイアログの本文を添えた文。</summary>
        internal string Thrown(Exception exception)
        {
            return Told(Failure == null ? exception.Message : exception.Message + " " + Failure, Notices);
        }

        /// <summary>失敗の事情に、それまでに閉じたメッセージボックスの本文を添えた文。</summary>
        internal static string Told(string failure, IList<string> notices)
        {
            return notices.Count == 0
                ? failure
                : failure + " それまでに閉じたメッセージボックス: " + string.Join(" / ", notices.Select(n => "「" + n + "」"));
        }
    }
}
