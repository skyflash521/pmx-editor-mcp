using System;
using System.Collections.Generic;
using System.Drawing;
using PEPlugin.Pmd;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ValueInputTests
    {
        [Flags]
        private enum Marks
        {
            None = 0,

            First = 1,

            Second = 2,
        }

        private enum Sides
        {
            Front = 0,

            Back = 1,
        }

        [Theory]
        [InlineData(typeof(bool), true)]
        [InlineData(typeof(int), 7)]
        [InlineData(typeof(string), "文字")]
        public void ScalarsAreReadAsTheyAre(Type declared, object json)
        {
            Assert.Equal(json, Read(declared, json));
        }

        [Fact]
        public void NumbersAreReadIntoTheTypeThatWasAskedFor()
        {
            Assert.Equal((byte)7, Read(typeof(byte), 7));
            Assert.Equal(0.5f, Read(typeof(float), 0.5d));
            Assert.Equal(0.5d, Read(typeof(double), 0.5d));
        }

        [Theory]
        [InlineData(typeof(byte), 1.5d)]
        [InlineData(typeof(int), 1.5d)]
        public void ANumberWithAFractionIsRefusedWhereTheTypeHoldsWholeNumbers(Type declared, object json)
        {
            Refused(declared, json, "整数でない");
        }

        [Theory]
        [InlineData(typeof(byte), 256)]
        [InlineData(typeof(byte), -1)]
        [InlineData(typeof(int), 2147483648L)]
        public void ANumberOutsideWhatTheTypeHoldsIsRefused(Type declared, object json)
        {
            Refused(declared, json, "範囲を超えて");
        }

        [Fact]
        public void ANumberThatIsNotFiniteIsRefused()
        {
            Refused(typeof(float), double.NaN, "有限でない");
        }

        [Fact]
        public void SomethingThatIsNotANumberIsRefused()
        {
            Refused(typeof(int), "7", "数値でない");
        }

        [Fact]
        public void SomethingThatIsNotTextIsRefused()
        {
            Refused(typeof(string), 7, "文字列でない");
        }

        [Fact]
        public void SomethingThatIsNotTrueOrFalseIsRefused()
        {
            Refused(typeof(bool), 1, "真偽でない");
        }

        [Fact]
        public void AVersionIsReadFromItsText()
        {
            Assert.Equal(new Version(2, 7, 3), Read(typeof(Version), "2.7.3"));
        }

        [Fact]
        public void TextThatIsNotAVersionIsRefused()
        {
            Refused(typeof(Version), "二・七・三", "標準の表記でない");
        }

        [Fact]
        public void NothingIsReadWhereTheValueMayBeMissing()
        {
            Assert.Null(Read(typeof(int?), null));
            Assert.Null(Read(typeof(string), null));
        }

        [Fact]
        public void NothingIsRefusedWhereTheValueIsRequired()
        {
            Refused(typeof(int), null, "値が無い");
        }

        [Fact]
        public void ATypeThatIsNotAValueIsNotRead()
        {
            object value;
            string code;
            string message;
            Assert.False(ValueInput.TryFromJson(
                typeof(ValueInputTests), null, out value, out code, out message));
            Assert.Null(code);
            Assert.Null(message);
        }

        [Fact]
        public void AnEnumIsReadFromTheNameOfItsMember()
        {
            Assert.Equal(Sides.Back, Read(typeof(Sides), "Back"));
        }

        [Fact]
        public void CombinedMarksAreReadFromTheNamesSpelledOut()
        {
            Assert.Equal(Marks.First | Marks.Second, Read(typeof(Marks), "First, Second"));
        }

        [Fact]
        public void ANameThatNoMemberCarriesIsRefused()
        {
            Refused(typeof(Sides), "Side", "当てはまる列挙子の無い名前");
        }

        [Fact]
        public void NamesSpelledOutForAnEnumThatDoesNotCombineAreRefused()
        {
            Refused(typeof(Sides), "Front, Back", "組み合わせを許さない列挙");
        }

        [Fact]
        public void ANumberInsteadOfANameIsRefused()
        {
            Refused(typeof(Sides), 1, "文字列で渡す");
        }

        [Fact]
        public void ComponentsAreReadInTheOrderTheTypeDecides()
        {
            V3 read = (V3)Read(typeof(V3), new object[] { 1d, 2d, 3d });
            Assert.Equal(1f, read.X);
            Assert.Equal(2f, read.Y);
            Assert.Equal(3f, read.Z);
        }

        [Fact]
        public void ComponentsOfTheDrawingLibraryAreReadTheSameWay()
        {
            SlimDX.Quaternion read =
                (SlimDX.Quaternion)Read(typeof(SlimDX.Quaternion), new object[] { 1d, 2d, 3d, 4d });
            Assert.Equal(1f, read.X);
            Assert.Equal(4f, read.W);
        }

        [Fact]
        public void AMatrixIsReadRowAfterRow()
        {
            object[] items = new object[16];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = (double)i;
            }

            M read = (M)Read(typeof(M), items);
            Assert.Equal(1f, read.M12);
            Assert.Equal(4f, read.M21);
        }

        [Fact]
        public void AValueOfAnInterfaceIsBuiltAsTheTypeThatCarriesIt()
        {
            IPEVector3 point = (IPEVector3)Read(typeof(IPEVector3), new object[] { 1d, 2d, 3d });
            Assert.Equal(2f, point.Y);
            IPEQuaternion turn = (IPEQuaternion)Read(typeof(IPEQuaternion), new object[] { 1d, 2d, 3d, 4d });
            Assert.Equal(4f, turn.W);
        }

        [Fact]
        public void ComponentsOfTheWrongCountAreRefused()
        {
            Refused(typeof(V3), new object[] { 1d, 2d }, "3 つ並べた配列でない");
            Refused(typeof(V3), "1,2,3", "3 つ並べた配列でない");
        }

        [Fact]
        public void AColorIsReadFromThreeOrFourComponents()
        {
            Assert.Equal(
                Color.FromArgb(255, 255, 0, 0), Read(typeof(Color), new object[] { 1d, 0d, 0d }));
            Assert.Equal(
                Color.FromArgb(0, 255, 0, 0), Read(typeof(Color), new object[] { 1d, 0d, 0d, 0d }));
        }

        [Fact]
        public void AColorComponentOutsideTheAllowedRangeIsRefused()
        {
            Refused(typeof(Color), new object[] { 1.5d, 0d, 0d }, "0以上1以下でない");
        }

        [Fact]
        public void AColorOfTheWrongCountIsRefused()
        {
            Refused(typeof(Color), new object[] { 1d, 0d }, "3つか4つ");
        }

        [Fact]
        public void ASizeAndAPointAndARectangleAreReadInTheirOwnOrder()
        {
            Assert.Equal(new Size(3, 4), Read(typeof(Size), new object[] { 3, 4 }));
            Assert.Equal(new Point(3, 4), Read(typeof(Point), new object[] { 3, 4 }));
            Assert.Equal(
                new Rectangle(1, 2, 3, 4), Read(typeof(Rectangle), new object[] { 1, 2, 3, 4 }));
        }

        [Fact]
        public void AFontIsReadFromItsFamilyAndSizeAndStyle()
        {
            using (Font font = (Font)Read(typeof(Font), Described(FontFamily.GenericSansSerif.Name, 9d, "Bold")))
            {
                Assert.Equal(FontFamily.GenericSansSerif.Name, font.FontFamily.Name);
                Assert.Equal(9f, font.SizeInPoints);
                Assert.Equal(FontStyle.Bold, font.Style);
            }
        }

        [Fact]
        public void AFontOfAFamilyThatIsNotInstalledIsRefused()
        {
            Refused(typeof(Font), Described("在るはずのない書体", 9d, "Bold"), "導入されていない書体名");
        }

        [Fact]
        public void AFontWithNoSizeLeftIsRefused()
        {
            Refused(typeof(Font), Described(FontFamily.GenericSansSerif.Name, 0d, "Bold"), "0以下");
        }

        [Fact]
        public void AFontWithANameThatIsNotKnownIsRefused()
        {
            Dictionary<string, object> described =
                Described(FontFamily.GenericSansSerif.Name, 9d, "Bold");
            described["weight"] = 700;
            Refused(typeof(Font), described, "知らない項目");
        }

        [Fact]
        public void SomethingThatIsNotAGroupOfNamesIsNotAFont()
        {
            Refused(typeof(Font), "書体", "組で渡す");
        }

        [Fact]
        public void ABrushIsBuiltFromTheColorItIsGiven()
        {
            using (SolidBrush brush = (SolidBrush)Read(typeof(Brush), new object[] { 0d, 1d, 0d }))
            {
                Assert.Equal(Color.FromArgb(255, 0, 255, 0), brush.Color);
            }
        }

        [Fact]
        public void AnImageIsReadFromThePackedPicture()
        {
            using (Bitmap sent = new Bitmap(4, 4))
            {
                string packed = ImageTransfer.Encode(sent, ImageTransfer.DefaultMaxLongSide).Base64;
                using (Bitmap read = (Bitmap)Read(typeof(Bitmap), packed))
                {
                    Assert.Equal(sent.Width, read.Width);
                    Assert.Equal(sent.Height, read.Height);
                }
            }
        }

        [Fact]
        public void SomethingThatIsNotAPictureIsRefused()
        {
            object value;
            string code;
            string message;
            Assert.False(ValueInput.TryFromJson(typeof(Bitmap), "----", out value, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.NotNull(message);
        }

        [Fact]
        public void BytesInALineAreReadFromOneString()
        {
            Assert.Equal(new byte[] { 1, 2, 3 }, Read(typeof(byte[]), "AQID"));
            Assert.Equal(new List<byte> { 1, 2, 3 }, Assert.IsType<List<byte>>(Read(typeof(IList<byte>), "AQID")));
        }

        [Fact]
        public void TextThatIsNotPackedBytesIsRefused()
        {
            Refused(typeof(byte[]), "----", "Base64として読めない");
        }

        [Fact]
        public void OtherElementsAreReadOneByOne()
        {
            Assert.Equal(new[] { 1, 2 }, Read(typeof(int[]), new object[] { 1, 2 }));
            Assert.Equal(
                new List<Sides> { Sides.Front, Sides.Back },
                Read(typeof(IList<Sides>), new object[] { "Front", "Back" }));
        }

        [Fact]
        public void AnElementThatCannotBeReadStopsTheWholeSequence()
        {
            Refused(typeof(int[]), new object[] { 1, "2" }, "数値でない");
        }

        [Fact]
        public void SomethingThatIsNotASequenceIsRefused()
        {
            Refused(typeof(int[]), 1, "配列でない");
        }

        [Fact]
        public void NothingIsHandedBackWhenAPictureLaterInTheSequenceIsRefused()
        {
            using (Bitmap sent = new Bitmap(4, 4))
            {
                string packed = ImageTransfer.Encode(sent, ImageTransfer.DefaultMaxLongSide).Base64;
                object value;
                string code;
                string message;
                Assert.False(ValueInput.TryFromJson(
                    typeof(Bitmap[]), new object[] { packed, "----" },
                    out value, out code, out message));
                Assert.Equal(ToolEnvelope.InvalidArgument, code);
                Assert.Null(value);
            }
        }

        [Fact]
        public void TheLargestNumberASingleHoldsIsReadBackFromWhatWasWritten()
        {
            object written;
            IList<string> warnings;
            string code;
            string message;
            Assert.True(ValueShape.TryToJson(
                typeof(float), float.MaxValue, ImageTransfer.DefaultMaxLongSide,
                out written, out warnings, out code, out message));
            Assert.Equal(float.MaxValue, Read(typeof(float), double.Parse(
                ((float)written).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Fact]
        public void AnyJsonValueIsKeptAsItStands()
        {
            Dictionary<string, object> loose = new Dictionary<string, object>();
            loose["名前"] = "値";
            loose["並び"] = new object[] { true, null, 1 };
            Assert.Same(loose, Read(typeof(object), loose));
        }

        [Fact]
        public void SomethingThatIsNotAJsonValueIsRefusedEvenDeepInside()
        {
            Dictionary<string, object> loose = new Dictionary<string, object>();
            loose["並び"] = new object[] { new ValueInputTests() };
            Refused(typeof(object), loose, "JSONの値の形をしていない");
        }

        [Fact]
        public void APairWhoseNameIsNotTextIsRefused()
        {
            Dictionary<int, object> loose = new Dictionary<int, object>();
            loose[1] = "値";
            Refused(typeof(object), loose, "名前が文字列でない");
        }

        [Fact]
        public void AReferenceToAValueIsReadLikeTheValueItself()
        {
            Assert.Equal(7, Read(typeof(int).MakeByRefType(), 7));
        }

        [Fact]
        public void NoTypeToReadStops()
        {
            object value;
            string code;
            string message;
            Assert.Throws<ArgumentNullException>(() => ValueInput.TryFromJson(
                null, 1, out value, out code, out message));
        }

        private static Dictionary<string, object> Described(string family, object size, string style)
        {
            Dictionary<string, object> described = new Dictionary<string, object>(StringComparer.Ordinal);
            described["family"] = family;
            described["size"] = size;
            described["style"] = style;

            return described;
        }

        private static object Read(Type declared, object json)
        {
            object value;
            string code;
            string message;
            Assert.True(ValueInput.TryFromJson(declared, json, out value, out code, out message));
            Assert.Null(code);
            Assert.Null(message);

            return value;
        }

        private static void Refused(Type declared, object json, string expected)
        {
            object value;
            string code;
            string message;
            Assert.False(ValueInput.TryFromJson(declared, json, out value, out code, out message));
            Assert.Equal(ToolEnvelope.InvalidArgument, code);
            Assert.Contains(expected, message);
            Assert.Null(value);
        }
    }
}
