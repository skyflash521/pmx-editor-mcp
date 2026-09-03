using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ImageTransferTests
    {
        [Fact]
        public void TheLimitsAreTheContract()
        {
            Assert.Equal(512, ImageTransfer.DefaultMaxLongSide);
            Assert.Equal(256, ImageTransfer.MinimumMaxLongSide);
            Assert.Equal(2048, ImageTransfer.MaximumMaxLongSide);
            Assert.Equal(4096, ImageTransfer.MaximumInputSide);
        }

        [Fact]
        public void AnImageThatFitsIsSentAsItIs()
        {
            using (Bitmap image = Solid(300, 200))
            {
                EncodedImage encoded = ImageTransfer.Encode(image, ImageTransfer.DefaultMaxLongSide);

                Assert.Empty(encoded.Warnings);
                Assert.Equal(new Size(300, 200), SizeOf(encoded.Base64));
            }
        }

        [Fact]
        public void AnImageAtTheLimitIsSentAsItIs()
        {
            using (Bitmap image = Solid(ImageTransfer.MinimumMaxLongSide, 10))
            {
                EncodedImage encoded = ImageTransfer.Encode(image, ImageTransfer.MinimumMaxLongSide);

                Assert.Empty(encoded.Warnings);
                Assert.Equal(
                    new Size(ImageTransfer.MinimumMaxLongSide, 10), SizeOf(encoded.Base64));
            }
        }

        [Fact]
        public void ALongerImageIsReducedKeepingItsRatio()
        {
            using (Bitmap image = Solid(1024, 768))
            {
                EncodedImage encoded = ImageTransfer.Encode(image, 512);

                Assert.Equal(new Size(512, 384), SizeOf(encoded.Base64));
            }
        }

        [Fact]
        public void ATallImageIsReducedByItsHeight()
        {
            using (Bitmap image = Solid(768, 1024))
            {
                EncodedImage encoded = ImageTransfer.Encode(image, 512);

                Assert.Equal(new Size(384, 512), SizeOf(encoded.Base64));
            }
        }

        [Fact]
        public void AShortSideAtTheMidpointRoundsUp()
        {
            using (Bitmap image = Solid(1024, 333))
            {
                EncodedImage encoded = ImageTransfer.Encode(image, 512);

                Assert.Equal(new Size(512, 167), SizeOf(encoded.Base64));
            }
        }

        [Fact]
        public void AVeryThinImageKeepsAtLeastOnePixel()
        {
            using (Bitmap image = Solid(4000, 1))
            {
                EncodedImage encoded = ImageTransfer.Encode(image, 256);

                Assert.Equal(new Size(256, 1), SizeOf(encoded.Base64));
            }
        }

        [Fact]
        public void AReducedImageSaysBothSizes()
        {
            using (Bitmap image = Solid(1024, 768))
            {
                EncodedImage encoded = ImageTransfer.Encode(image, 512);

                string warning = Assert.Single(encoded.Warnings);
                Assert.Contains("1024x768", warning, StringComparison.Ordinal);
                Assert.Contains("512x384", warning, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ALongSideOutsideTheRangeStops()
        {
            using (Bitmap image = Solid(10, 10))
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => ImageTransfer.Encode(image, ImageTransfer.MinimumMaxLongSide - 1));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => ImageTransfer.Encode(image, ImageTransfer.MaximumMaxLongSide + 1));
            }
        }

        [Fact]
        public void ThePngOfAnImageIsRead()
        {
            Bitmap image;
            string reason;

            Assert.True(ImageTransfer.TryDecode(Encoded(20, 10, ImageFormat.Png), out image, out reason));
            using (image)
            {
                Assert.Null(reason);
                Assert.Equal(new Size(20, 10), image.Size);
                Assert.Equal(PixelFormat.Format32bppArgb, image.PixelFormat);
            }
        }

        [Fact]
        public void TheBmpOfAnImageIsRead()
        {
            Bitmap image;
            string reason;

            Assert.True(ImageTransfer.TryDecode(Encoded(20, 10, ImageFormat.Bmp), out image, out reason));
            using (image)
            {
                Assert.Equal(PixelFormat.Format32bppArgb, image.PixelFormat);
            }
        }

        [Fact]
        public void AnotherFormatIsRefused()
        {
            Bitmap image;
            string reason;

            Assert.False(
                ImageTransfer.TryDecode(Encoded(20, 10, ImageFormat.Jpeg), out image, out reason));
            Assert.Null(image);
            Assert.Contains("PNG", reason, StringComparison.Ordinal);
        }

        [Fact]
        public void TextThatIsNotBase64IsRefused()
        {
            Bitmap image;
            string reason;

            Assert.False(ImageTransfer.TryDecode("これはBase64ではない", out image, out reason));
            Assert.Null(image);
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }

        [Fact]
        public void BytesThatAreNotAnImageAreRefused()
        {
            Bitmap image;
            string reason;

            Assert.False(
                ImageTransfer.TryDecode(Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }), out image, out reason));
            Assert.Null(image);
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }

        [Fact]
        public void ABrokenImageIsRefused()
        {
            Bitmap image;
            string reason;
            byte[] broken = Convert.FromBase64String(Encoded(20, 10, ImageFormat.Png));
            for (int index = 8; index < broken.Length; index++)
            {
                broken[index] = 0;
            }

            Assert.False(ImageTransfer.TryDecode(Convert.ToBase64String(broken), out image, out reason));
            Assert.Null(image);
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }

        [Fact]
        public void AnImageOverTheInputLimitIsRefused()
        {
            Bitmap image;
            string reason;

            Assert.False(
                ImageTransfer.TryDecode(
                    Encoded(ImageTransfer.MaximumInputSide + 1, 1, ImageFormat.Png),
                    out image,
                    out reason));
            Assert.Null(image);
            Assert.Contains(
                ImageTransfer.MaximumInputSide.ToString(), reason, StringComparison.Ordinal);
        }

        [Fact]
        public void AnImageAtTheInputLimitIsRead()
        {
            Bitmap image;
            string reason;

            Assert.True(
                ImageTransfer.TryDecode(
                    Encoded(ImageTransfer.MaximumInputSide, 1, ImageFormat.Png), out image, out reason));
            image.Dispose();
        }

        [Fact]
        public void TheInputsAreRequired()
        {
            Bitmap image;
            string reason;

            Assert.Throws<ArgumentNullException>(() => ImageTransfer.Encode(null, 512));
            Assert.Throws<ArgumentNullException>(
                () => ImageTransfer.TryDecode(null, out image, out reason));
        }

        /// <summary>指定の大きさの、中身が一色の画像。</summary>
        private static Bitmap Solid(int width, int height)
        {
            Bitmap image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.FromArgb(255, 10, 20, 30));
            }

            return image;
        }

        /// <summary>指定の形式で詰めた画像。</summary>
        private static string Encoded(int width, int height, ImageFormat format)
        {
            using (Bitmap image = Solid(width, height))
            using (MemoryStream stream = new MemoryStream())
            {
                image.Save(stream, format);

                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static Size SizeOf(string base64)
        {
            using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(base64), false))
            using (Image image = Image.FromStream(stream))
            {
                Assert.Equal(ImageFormat.Png, image.RawFormat);

                return image.Size;
            }
        }
    }
}
