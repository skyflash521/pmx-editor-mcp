// エディタの VMD の読み込みは、区分の切れ目で途切れたファイルも受け付ける。

using System;
using System.Text;

namespace PmxEditorMcp
{
    internal static class VmdFile
    {
        private static readonly string[] Headers = { "Vocaloid Motion Data 0002", "Vocaloid Motion Data file" };

        private const int HeaderLength = 30;

        private const int ModelNameLength = 20;

        private const int BoneKeyLength = 111;

        private const int MorphKeyLength = 23;

        private const int CameraKeyLength = 61;

        private const int LightKeyLength = 28;

        private const int SelfShadowKeyLength = 9;

        private const int VisibleIkKeyLength = 9;

        private const int VisibleIkEntryLength = 21;

        /// <summary>
        /// エディタが書くヘッダーのどれかで始まり、ボーン・モーフ・カメラ・照明・セルフ影・IK表示の区分が過不足なく
        /// 並んでいれば真。
        /// </summary>
        internal static bool IsWhole(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < HeaderLength + ModelNameLength)
            {
                return false;
            }

            string header = Encoding.ASCII.GetString(bytes, 0, HeaderLength).Split('\0')[0];
            if (Array.FindIndex(Headers, known => string.Equals(known, header, StringComparison.OrdinalIgnoreCase)) < 0)
            {
                return false;
            }

            // エディタの書き出しは IK 表示の区分をモーフの次に置き、読み込みは最後に読む。
            return Walks(bytes, true) || Walks(bytes, false);
        }

        private static bool Walks(byte[] bytes, bool visibleIkThird)
        {
            long at = HeaderLength + ModelNameLength;
            if (!Fixed(bytes, ref at, BoneKeyLength) || !Fixed(bytes, ref at, MorphKeyLength))
            {
                return false;
            }

            if (visibleIkThird && !VisibleIk(bytes, ref at))
            {
                return false;
            }

            if (!Fixed(bytes, ref at, CameraKeyLength)
                || !Fixed(bytes, ref at, LightKeyLength)
                || !Fixed(bytes, ref at, SelfShadowKeyLength))
            {
                return false;
            }

            if (!visibleIkThird && !VisibleIk(bytes, ref at))
            {
                return false;
            }

            return at == bytes.Length;
        }

        private static bool Fixed(byte[] bytes, ref long at, int keyLength)
        {
            int count;
            if (!TryCount(bytes, ref at, out count))
            {
                return false;
            }

            at += (long)count * keyLength;

            return at <= bytes.Length;
        }

        private static bool VisibleIk(byte[] bytes, ref long at)
        {
            int count;
            if (!TryCount(bytes, ref at, out count))
            {
                return false;
            }

            for (int key = 0; key < count; key++)
            {
                long entries = at + VisibleIkKeyLength - 4;
                int held;
                if (!TryCount(bytes, ref entries, out held))
                {
                    return false;
                }

                at = entries + (long)held * VisibleIkEntryLength;
                if (at > bytes.Length)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryCount(byte[] bytes, ref long at, out int count)
        {
            count = 0;
            if (at + 4 > bytes.Length)
            {
                return false;
            }

            count = BitConverter.ToInt32(bytes, (int)at);
            at += 4;

            return count >= 0;
        }
    }
}
