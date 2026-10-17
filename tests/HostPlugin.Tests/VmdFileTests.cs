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
            Assert.True(VmdFile.IsWhole(VmdSamples.Bytes(visibleIkThird: false, bones: 2, morphs: 1, cameras: 1, iks: 1)));
        }

        [Fact]
        public void AFileWithTheVisibleIkSectionThirdIsWholeToo()
        {
            Assert.True(VmdFile.IsWhole(VmdSamples.Bytes(visibleIkThird: true, bones: 1, morphs: 0, cameras: 2, iks: 1)));
        }

        [Fact]
        public void AFileCutShortIsNotWhole()
        {
            byte[] whole = VmdSamples.Bytes(visibleIkThird: false, bones: 2, morphs: 1, cameras: 1, iks: 1);

            Assert.False(VmdFile.IsWhole(whole.Take(whole.Length - 1).ToArray()));
            Assert.False(VmdFile.IsWhole(whole.Take(50 + 4 + 111 * 2).ToArray()));
            Assert.False(VmdFile.IsWhole(new byte[0]));
        }

        [Theory]
        [InlineData("Vocaloid Motion Data file")]
        [InlineData("vocaloid motion data 0002")]
        public void AFileKeepingTheHeaderItWasReadWithIsWhole(string header)
        {
            byte[] whole = VmdSamples.Bytes(visibleIkThird: true, bones: 1, morphs: 1, cameras: 0, iks: 0);
            Array.Clear(whole, 0, 30);
            Encoding.ASCII.GetBytes(header).CopyTo(whole, 0);

            Assert.True(VmdFile.IsWhole(whole));
        }

        [Fact]
        public void AFileWithAnotherHeaderIsNotWhole()
        {
            byte[] whole = VmdSamples.Bytes(visibleIkThird: false, bones: 0, morphs: 0, cameras: 0, iks: 0);
            whole[0] = (byte)'X';

            Assert.False(VmdFile.IsWhole(whole));
        }
    }
}
