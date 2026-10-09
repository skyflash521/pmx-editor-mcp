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
        public const string PingName = "ping";

        public const string SdkStatusName = "sdk_status";

        public const string FindToolName = "find_tool";

        public const string FindToolTextsParameter = "texts";

        public const int FindToolDefaultLimit = 50;

        public const int FindToolMinimumLimit = 1;

        public const int FindToolMaximumLimit = int.MaxValue;

        public const string ListEditorsName = "list_editors";

        public const string SelectEditorName = "select_editor";

        public const string SelectEditorProcessIdParameter = "processId";

        public const string LargeTextName = "debug_large_text";

        /// <summary>引数の名前と型と必須かどうか、既定と取りうる範囲を書き、説明は持たない。</summary>
        public static string InputSchema(string name)
        {
            switch (name)
            {
                case FindToolName:
                    return "{\"type\":\"object\",\"properties\":{\"" + FindToolTextsParameter
                        + "\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},"
                        + "\"limit\":{\"type\":[\"integer\",\"null\"],\"default\":" + FindToolDefaultLimit
                        + ",\"minimum\":" + FindToolMinimumLimit + ",\"maximum\":" + FindToolMaximumLimit + "},"
                        + "\"offset\":{\"type\":[\"integer\",\"null\"],\"default\":0,\"minimum\":0}},"
                        + "\"required\":[\"" + FindToolTextsParameter + "\"],"
                        + "\"additionalProperties\":false}";
                case SelectEditorName:
                    return "{\"type\":\"object\",\"properties\":{\"" + SelectEditorProcessIdParameter
                        + "\":{\"type\":\"integer\"}},\"required\":[\""
                        + SelectEditorProcessIdParameter + "\"],\"additionalProperties\":false}";
                case LargeTextName:
                    return "{\"type\":\"object\",\"properties\":{\"chars\":{\"type\":\"integer\"}},"
                        + "\"required\":[\"chars\"],\"additionalProperties\":false}";
                default:
                    return "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";
            }
        }

        /// <summary>その入口の開き方で公開する、名前から説明文を引く表。</summary>
        public static IDictionary<string, string> Descriptions(bool debugHooks)
        {
            Dictionary<string, string> descriptions =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { PingName, "ホストが応答することを確かめる。" },
                    {
                        SdkStatusName,
                        "ホストが稼働しているSDKのバージョン・中継を作った時点のSDKのバージョン・中継を作れなかった"
                            + "行・呼び出しの失敗で無効にした行・稼働しているSDKでは組み立てられず断るツール・"
                            + "ホストがツールとして答える名前を"
                            + "返す。その名前に " + PingName + " と " + SdkStatusName
                            + " は入らない——どちらもホストの接続自身が受け持つ。"
                    },
                    {
                        FindToolName,
                        "語を含むツールを返す。" + FindToolTextsParameter
                            + " に並べた語のどれかを名前か説明文に含むツールの名前を、"
                            + "名前の昇順で並べる。" + FindToolTextsParameter
                            + " は空の配列も空の語も受け付けない。"
                            + "語は日本語で、PMXエディタで一般的に使われている語にする。"
                            + "エディタの表記が英語の語は、その表記(Joint など)でも読み(ジョイント など)でも当たる。"
                            + "大文字小文字と全角半角と仮名の種類は区別しない。エディタが起動して"
                            + "いなくても答える。やりたいことの言葉から、それを行うツールへ渡る"
                            + "ときに使う。当たりが多いときは total に総数を返し、"
                            + "limit と応答の枠で返しきれなかった残りがあるときは nextOffset を"
                            + "返す。その値を offset へ渡すと続きが読める。"
                    },
                    {
                        ListEditorsName,
                        "接続先に選べるPMXエディタを、プロセスIDの昇順に全部返す。1件ごとに processId・"
                            + "ホストが待ち受けているか(listening)・ウィンドウのタイトル(title)・"
                            + SelectEditorName + " で選んだ接続先か(selected)・いま繋いでいるか"
                            + "(connected)を返す。エディタが複数動いているときに、どれを接続先に"
                            + "するかを見分けるために使う。エディタが起動していなくても答える。"
                    },
                    {
                        SelectEditorName,
                        "ツールの呼び出しを送るPMXエディタを、" + SelectEditorProcessIdParameter
                            + " にそのプロセスIDを渡して選ぶ。選んだエディタへ繋いでから返り、以後の"
                            + "呼び出しはそのエディタへだけ送る。選んだエディタが終了しても、ほかの"
                            + "エディタへは繋がない。エディタが複数動いているときは、呼び出しの前に"
                            + "これで接続先を選ぶ。待ち受けているホストが無いエディタは選べない。"
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
