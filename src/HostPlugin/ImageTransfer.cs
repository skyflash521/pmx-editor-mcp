using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace PmxEditorMcp
{
    /// <summary>送り出す形にした画像。縮小したときはその旨の警告を伴う。</summary>
    public sealed class EncodedImage
    {
        public EncodedImage(string base64, IList<string> warnings)
        {
            if (base64 == null)
            {
                throw new ArgumentNullException(nameof(base64));
            }

            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            Base64 = base64;
            Warnings = new ReadOnlyCollection<string>(warnings);
        }

        /// <summary>PNGを詰めた文字列。</summary>
        public string Base64 { get; }

        /// <summary>縮小したときに添える警告。縮小していなければ空。</summary>
        public IList<string> Warnings { get; }
    }

    /// <summary>
    /// 画像をJSONの値としてやり取りする形へ写す。送り出すのはPNGを詰めた文字列で、受け取るのは
    /// PNGとBMPに限る。
    /// </summary>
    public static class ImageTransfer
    {
        /// <summary>送り出す画像の長辺の上限の既定。</summary>
        public const int DefaultMaxLongSide = 512;

        /// <summary>長辺の上限として受理する最小。</summary>
        public const int MinimumMaxLongSide = 256;

        /// <summary>長辺の上限として受理する最大。</summary>
        public const int MaximumMaxLongSide = 2048;

        /// <summary>受け取る画像の辺の上限。</summary>
        public const int MaximumInputSide = 4096;

        /// <summary>
        /// 送り出す形にする。長辺が上限を超えるときは縦横比を保って縮める。上限が受理する範囲の
        /// 外なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public static EncodedImage Encode(Bitmap image, int maxLongSide)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (maxLongSide < MinimumMaxLongSide || maxLongSide > MaximumMaxLongSide)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxLongSide),
                    maxLongSide,
                    MinimumMaxLongSide.ToString(CultureInfo.InvariantCulture) + " 以上 "
                        + MaximumMaxLongSide.ToString(CultureInfo.InvariantCulture) + " 以下でなければならない。");
            }

            Size original = image.Size;
            if (Math.Max(original.Width, original.Height) <= maxLongSide)
            {
                return new EncodedImage(ToPng(image), new string[0]);
            }

            Size reduced = Fit(original, maxLongSide);
            using (Bitmap smaller = Resize(image, reduced))
            {
                return new EncodedImage(
                    ToPng(smaller),
                    new[] { "画像を縮めた: " + Describe(original) + " から " + Describe(reduced) });
            }
        }

        /// <summary>
        /// 受け取った文字列を画像に直す。読めない・形式が違う・辺が上限を超えるときは偽を返し、
        /// <paramref name="reason"/> にその訳を置く。
        /// </summary>
        public static bool TryDecode(string base64, out Bitmap image, out string reason)
        {
            if (base64 == null)
            {
                throw new ArgumentNullException(nameof(base64));
            }

            image = null;
            reason = null;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                reason = "画像がBase64として読めない。";
                return false;
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                Image decoded;
                try
                {
                    decoded = Image.FromStream(stream);
                }
                catch (Exception exception) when (IsDecodeFailure(exception))
                {
                    reason = "画像として読めない。";
                    return false;
                }

                using (decoded)
                {
                    if (!IsAccepted(decoded.RawFormat))
                    {
                        reason = "画像はPNGかBMPでなければならない。";
                        return false;
                    }

                    if (decoded.Width > MaximumInputSide || decoded.Height > MaximumInputSide)
                    {
                        reason = "画像の辺は "
                            + MaximumInputSide.ToString(CultureInfo.InvariantCulture)
                            + " までとする。";
                        return false;
                    }

                    image = Resize(decoded, decoded.Size);
                    return true;
                }
            }
        }

        /// <summary>長辺を上限にそろえ、短辺を縦横比で求める。1を下回らせない。</summary>
        private static Size Fit(Size original, int maxLongSide)
        {
            if (original.Width >= original.Height)
            {
                return new Size(maxLongSide, Shorter(original.Height, original.Width, maxLongSide));
            }

            return new Size(Shorter(original.Width, original.Height, maxLongSide), maxLongSide);
        }

        private static int Shorter(int side, int longSide, int maxLongSide)
        {
            return Math.Max(
                1,
                (int)Math.Round(
                    side * (double)maxLongSide / longSide, MidpointRounding.AwayFromZero));
        }

        /// <summary>指定の大きさの32bitのARGBへ描き直す。</summary>
        private static Bitmap Resize(Image image, Size size)
        {
            Bitmap copy = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(copy))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(image, new Rectangle(Point.Empty, size));
                }
            }
            catch
            {
                copy.Dispose();
                throw;
            }

            return copy;
        }

        /// <summary>
        /// 画像として読めなかったことを表す例外かどうかを判定する。壊れた画像で確かめられたのは
        /// <see cref="ArgumentException"/> だけだが、GDI+ の失敗はどの型で来るかが一つに定まらないので、
        /// 残る2つもここで受ける。
        /// </summary>
        private static bool IsDecodeFailure(Exception exception)
        {
            return exception is ArgumentException
                || exception is OutOfMemoryException
                || exception is ExternalException;
        }

        private static bool IsAccepted(ImageFormat format)
        {
            return format.Equals(ImageFormat.Png) || format.Equals(ImageFormat.Bmp);
        }

        private static string ToPng(Bitmap image)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                image.Save(stream, ImageFormat.Png);

                return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static string Describe(Size size)
        {
            return size.Width.ToString(CultureInfo.InvariantCulture)
                + "x" + size.Height.ToString(CultureInfo.InvariantCulture);
        }
    }
}
