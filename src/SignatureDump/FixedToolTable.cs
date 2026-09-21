using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 組み立てた定義とは別に、ブリッジが自分で登録するツールの名前と説明文。検査からだけ使う
    /// 入口が開いているかで、公開するものが変わる。
    /// </summary>
    public static class FixedToolTable
    {
        /// <summary>ホストが応答することを確かめるツールの名前。</summary>
        public const string PingName = "ping";

        /// <summary>稼働しているSDKと中継の状態を返すツールの名前。</summary>
        public const string SdkStatusName = "sdk_status";

        /// <summary>語からツールを引くツールの名前。</summary>
        public const string FindToolName = "find_tool";

        /// <summary>そのツールへ渡す、探す語の引数の名前。</summary>
        public const string FindToolTextParameter = "text";

        /// <summary>指定した文字数のテキストを返す、検査からだけ使うツールの名前。</summary>
        public const string LargeTextName = "debug_large_text";

        /// <summary>その入口の開き方で公開する、名前から説明文を引く表。</summary>
        public static IDictionary<string, string> Descriptions(bool debugHooks)
        {
            Dictionary<string, string> descriptions =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { PingName, "ホストが応答することを確かめる。" },
                    {
                        SdkStatusName,
                        "ホストが稼働しているSDKの版・中継を作った時点のSDKの版・中継を作れなかった"
                            + "行・呼び出しの失敗で無効にした行・ホストがツールとして答える名前を"
                            + "返す。その名前に " + PingName + " と " + SdkStatusName
                            + " は入らない——どちらもホストの接続自身が受け持つ。"
                    },
                    {
                        FindToolName,
                        "語を含むツールを全部返す。" + FindToolTextParameter
                            + " を名前と説明文へ当て、当たったツールの名前を名前の昇順で並べる。"
                            + "大文字小文字と全角半角と仮名の種類は区別しない。エディタが起動して"
                            + "いなくても答える。やりたいことの言葉から、それを行うツールへ渡る"
                            + "ときに使う。当たりが多いときは total に総数を返し、"
                            + "limit と応答の枠で返しきれなかった残りがあるときは nextOffset を"
                            + "返す。その値を offset へ渡すと続きが読める。"
                    },
                };
            if (debugHooks)
            {
                descriptions.Add(
                    LargeTextName, "指定した文字数のテキストをホストから受け取る。検査に使う。");
            }

            return descriptions;
        }
    }
}
