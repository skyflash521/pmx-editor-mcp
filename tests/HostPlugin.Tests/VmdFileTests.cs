using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class VmdFileTests
    {
        [Fact]
        public void AFileWithEverySectionWrittenOutIsWhole()
        {
            Assert.True(VmdFile.IsWhole(Bytes(visibleIkThird: false, bones: 2, morphs: 1, cameras: 1, iks: 1)));
        }

        [Fact]
        public void AFileWithTheVisibleIkSectionThirdIsWholeToo()
        {
            Assert.True(VmdFile.IsWhole(Bytes(visibleIkThird: true, bones: 1, morphs: 0, cameras: 2, iks: 1)));
        }

        [Fact]
        public void AFileCutShortIsNotWhole()
        {
            byte[] whole = Bytes(visibleIkThird: false, bones: 2, morphs: 1, cameras: 1, iks: 1);

            Assert.False(VmdFile.IsWhole(whole.Take(whole.Length - 1).ToArray()));
            Assert.False(VmdFile.IsWhole(whole.Take(50 + 4 + 111 * 2).ToArray()));
            Assert.False(VmdFile.IsWhole(new byte[0]));
        }

        [Theory]
        [InlineData("Vocaloid Motion Data file")]
        [InlineData("vocaloid motion data 0002")]
        public void AFileKeepingTheHeaderItWasReadWithIsWhole(string header)
        {
            byte[] whole = Bytes(visibleIkThird: true, bones: 1, morphs: 1, cameras: 0, iks: 0);
            Array.Clear(whole, 0, 30);
            Encoding.ASCII.GetBytes(header).CopyTo(whole, 0);

            Assert.True(VmdFile.IsWhole(whole));
        }

        [Fact]
        public void AFileWithAnotherHeaderIsNotWhole()
        {
            byte[] whole = Bytes(visibleIkThird: false, bones: 0, morphs: 0, cameras: 0, iks: 0);
            whole[0] = (byte)'X';

            Assert.False(VmdFile.IsWhole(whole));
        }

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
