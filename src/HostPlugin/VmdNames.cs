using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// SDKは名前の並びの空の名前と重なる名前を飛ばして、残りの名前へ0から番号を振り直す。キーが持つ番号は
    /// 付け替わらないので、置いた後は名前の並びの位置とキーの番号が食い違い、キーが別の名前のものとして
    /// 読める。
    /// </summary>
    public static class VmdNames
    {
        public const string SetBoneNamesKey = "PEPlugin.Vmd.IPEVmd.SetBoneNames(System.String[])";

        public const string SetMorphNamesKey = "PEPlugin.Vmd.IPEVmd.SetMorphNames(System.String[])";

        public const string CreateVmdKey = "PEPlugin.IPEBuilder.CreateVmd(System.String[],System.String[])";

        /// <summary>名前を置く呼び出しでなければ確かめずに通す。</summary>
        public static bool TryCall(
            string rowKey, object[] args, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            if (args == null)
            {
                return true;
            }

            if (string.Equals(rowKey, SetBoneNamesKey, StringComparison.Ordinal))
            {
                return TryReplace("ボーン名", First(args, 0), out message);
            }

            if (string.Equals(rowKey, SetMorphNamesKey, StringComparison.Ordinal))
            {
                return TryReplace("モーフ名", First(args, 0), out message);
            }

            if (string.Equals(rowKey, CreateVmdKey, StringComparison.Ordinal))
            {
                return TryAccept("ボーン名", First(args, 0), out message)
                    && TryAccept("モーフ名", First(args, 1), out message);
            }

            return true;
        }

        private static string[] First(object[] args, int at)
        {
            return args.Length > at ? args[at] as string[] : null;
        }

        private static bool TryReplace(string kind, string[] names, out string message)
        {
            message = null;
            if (names == null)
            {
                message = kind + "の並びが無いので呼べない。名前の表を空にしてから並びを読むため、"
                    + "表が空になったまま残る。名前の並びを渡す。";

                return false;
            }

            return TryAccept(kind, names, out message);
        }

        private static bool TryAccept(string kind, string[] names, out string message)
        {
            message = null;
            if (names == null)
            {
                return true;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int at = 0; at < names.Length; at++)
            {
                if (string.IsNullOrEmpty(names[at]))
                {
                    message = kind + "の " + at + " 番目が空なので呼べない。空の名前は飛ばされ、"
                        + "後ろの名前の番号が詰まって、キーが持つ番号と食い違う。"
                        + "空にせず、実在する名前を並べる。";

                    return false;
                }

                if (!seen.Add(names[at]))
                {
                    message = kind + "の " + at + " 番目が前と重なっている: " + names[at]
                        + "。重なる名前は飛ばされ、後ろの名前の番号が詰まって、キーが持つ番号と"
                        + "食い違う。重ならない名前を並べる。";

                    return false;
                }
            }

            return true;
        }
    }
}
