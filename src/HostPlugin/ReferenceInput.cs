using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 指すだけのリストへ加える参照先の位置を解く。加えるツールは対象の指し方を持たず、この並びで
    /// 参照先を受け取る。位置は参照先のリストの中を指し、並べた順で末尾へ加わる。
    /// </summary>
    public static class ReferenceInput
    {
        /// <summary>参照先の位置を並べる項目の名前。</summary>
        public const string Name = "refIndices";

        /// <summary>
        /// <paramref name="refIndices"/> を解く。<paramref name="referencedCount"/> は参照先の
        /// リストの件数である。同じ位置を二度並べることは許す——参照は対象ではなく値なので、同じ
        /// 要素を二度指す指定にはならない。
        /// </summary>
        public static bool TryResolve(
            IList<int> refIndices,
            int referencedCount,
            out IList<int> resolved,
            out string code,
            out string message)
        {
            if (referencedCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(referencedCount), referencedCount, "0以上でなければならない。");
            }

            resolved = null;
            code = null;
            message = null;
            if (refIndices == null)
            {
                code = ToolEnvelope.InvalidArgument;
                message = Name + " が無い。加える参照先が決まらない。";

                return false;
            }

            if (!CheckEmpty(refIndices, Name, out code, out message)
                || !CheckWithin(refIndices, referencedCount, Name, out code, out message))
            {
                return false;
            }

            resolved = refIndices.ToArray();

            return true;
        }

        /// <summary>空の並びを断る。空だと加えるものが決まらない。</summary>
        private static bool CheckEmpty(
            IList<int> positions, string name, out string code, out string message)
        {
            code = null;
            message = null;
            if (positions.Count > 0)
            {
                return true;
            }

            code = ToolEnvelope.InvalidArgument;
            message = name + " が空である。空だと加えるものが決まらない。";

            return false;
        }

        /// <summary>位置が参照先のリストの中にあることを見る。</summary>
        internal static bool CheckWithin(
            IList<int> positions,
            int referencedCount,
            string name,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            foreach (int index in positions)
            {
                if (!PositionInput.TryWithin(
                    index, referencedCount, name, out code, out message, "参照先のリストの件数"))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
