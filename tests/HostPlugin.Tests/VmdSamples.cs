using System;
using System.Collections.Generic;
using System.Text;

namespace PmxEditorMcp.Tests
{
    /// <summary>検査の題材にする VMD の中身。</summary>
    internal static class VmdSamples
    {
        /// <summary>
        /// ボーン・モーフ・カメラ・照明1件・セルフ影1件・IK表示の各区分を並べた VMD の中身。IK表示の各キーは
        /// IKを1つずつ持つ。
        /// </summary>
        internal static byte[] Bytes(bool visibleIkThird, int bones, int morphs, int cameras, int iks)
        {
            List<byte> bytes = new List<byte>();
            byte[] header = new byte[30];
            Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002").CopyTo(header, 0);
            bytes.AddRange(header);
            bytes.AddRange(new byte[20]);
            Section(bytes, bones, 111);
            Section(bytes, morphs, 23);
            if (visibleIkThird)
            {
                VisibleIk(bytes, iks);
            }

            Section(bytes, cameras, 61);
            Section(bytes, 1, 28);
            Section(bytes, 1, 9);
            if (!visibleIkThird)
            {
                VisibleIk(bytes, iks);
            }

            return bytes.ToArray();
        }

        private static void Section(List<byte> bytes, int count, int size)
        {
            bytes.AddRange(BitConverter.GetBytes(count));
            bytes.AddRange(new byte[count * size]);
        }

        private static void VisibleIk(List<byte> bytes, int count)
        {
            bytes.AddRange(BitConverter.GetBytes(count));
            for (int at = 0; at < count; at++)
            {
                bytes.AddRange(new byte[5]);
                bytes.AddRange(BitConverter.GetBytes(1));
                bytes.AddRange(new byte[21]);
            }
        }
    }
}
