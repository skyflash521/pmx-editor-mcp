using System;
using System.Collections.Generic;
using System.Drawing;
using PEPlugin.Pmd;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ValueShapeTests
    {
        private const int MaxLongSide = ImageTransfer.DefaultMaxLongSide;

        [Flags]
        private enum Marks
        {
            None = 0,

            First = 1,

            Second = 2,

            Fourth = 4,
        }

        private enum Sides
        {
            Front = 0,

            Back = 1,
        }

        [Theory]
        [InlineData(typeof(bool), true)]
        [InlineData(typeof(byte), (byte)7)]
        [InlineData(typeof(int), 7)]
        [InlineData(typeof(float), 0.5f)]
        [InlineData(typeof(double), 0.5d)]
        [InlineData(typeof(string), "文字")]
        public void ScalarsAreWrittenAsTheyAre(Type declared, object value)
        {
            Assert.Equal(value, Write(declared, value));
        }

        [Fact]
        public void AVersionIsWrittenAsItsText()
        {
            Assert.Equal("2.7.3", Write(typeof(Version), new Version(2, 7, 3)));
        }

        [Fact]
        public void NothingToReturnIsWrittenAsNull()
        {
            Assert.Null(Write(typeof(void), null));
        }

        [Fact]
        public void AMissingValueIsWrittenAsNull()
        {
            Assert.Null(Write(typeof(int?), null));
            Assert.Null(Write(typeof(string), null));
        }

        [Fact]
        public void APresentValueOfANullableIsWrittenLikeTheValueItself()
        {
            Assert.Equal(7, Write(typeof(int?), 7));
        }

        [Fact]
        public void AValueTypeCannotBeWrittenWithoutAValue()
        {
            object json;
            IList<string> warnings;
            string code;
            string message;
            Assert.False(ValueShape.TryToJson(
                typeof(int), null, MaxLongSide, out json, out warnings, out code, out message));
            Assert.Null(code);
        }

        [Fact]
        public void ATypeThatIsNotAValueIsNotWritten()
        {
            NotAValue(typeof(ValueShapeTests), new ValueShapeTests());
        }

        [Fact]
        public void ATypeThatIsNotAValueIsNotWrittenEvenWithNothingToWrite()
        {
            NotAValue(typeof(ValueShapeTests), null);
            NotAValue(typeof(int[,]), null);
        }

        [Fact]
        public void ASequenceOfWhatIsNotAValueIsNotASequenceOfValues()
        {
            NotAValue(typeof(ValueShapeTests[]), new ValueShapeTests[0]);
            NotAValue(typeof(IList<ValueShapeTests>), new List<ValueShapeTests>());
        }

        [Fact]
        public void AnEnumIsWrittenAsTheNameOfItsMember()
        {
            Assert.Equal("Back", Write(typeof(Sides), Sides.Back));
        }

        [Fact]
        public void AValueOfAnEnumWithNoNameIsRefused()
        {
            Refused(typeof(Sides), (Sides)99, "列挙子の名前");
        }

        [Fact]
        public void CombinedMarksAreWrittenAsTheNamesInTheOrderOfTheirValues()
        {
            Assert.Equal("First, Second", Write(typeof(Marks), Marks.Second | Marks.First));
            Assert.Equal("First, Fourth", Write(typeof(Marks), Marks.First | Marks.Fourth));
        }

        [Fact]
        public void MarksWithAPartThatNoMemberCoversAreRefused()
        {
            Refused(typeof(Marks), (Marks)(1 | 8), "当てはまる列挙子の名前が無い");
        }

        [Fact]
        public void ComponentsAreWrittenInTheOrderTheTypeDecides()
        {
            Assert.Equal(new object[] { 1f, 2f }, Write(typeof(V2), new V2(1f, 2f)));
            Assert.Equal(new object[] { 1f, 2f, 3f }, Write(typeof(V3), new V3(1f, 2f, 3f)));
            Assert.Equal(new object[] { 1f, 2f, 3f, 4f }, Write(typeof(V4), new V4(1f, 2f, 3f, 4f)));
            Assert.Equal(new object[] { 1f, 2f, 3f, 4f }, Write(typeof(Q), new Q(1f, 2f, 3f, 4f)));
        }

        [Fact]
        public void ComponentsOfTheDrawingLibraryAreWrittenInTheSameOrder()
        {
            Assert.Equal(
                new object[] { 1f, 2f, 3f },
                Write(typeof(SlimDX.Vector3), new SlimDX.Vector3(1f, 2f, 3f)));
            Assert.Equal(
                new object[] { 1f, 2f, 3f, 4f },
                Write(typeof(SlimDX.Quaternion), new SlimDX.Quaternion(1f, 2f, 3f, 4f)));
        }

        [Fact]
        public void ComponentsAreReadThroughTheTypeTheValueIsDeclaredAs()
        {
            Assert.Equal(new object[] { 1f, 2f, 3f }, Write(typeof(IPEVector3), new Coordinates()));
            Assert.Equal(new object[] { 1f, 2f, 3f, 4f }, Write(typeof(IPEQuaternion), new Turn()));
        }

        [Fact]
        public void AMatrixIsWrittenRowAfterRow()
        {
            M matrix = new M();
            matrix.M12 = 5f;
            matrix.M21 = 6f;
            object[] written = (object[])Write(typeof(M), matrix);
            Assert.Equal(16, written.Length);
            Assert.Equal(5f, written[1]);
            Assert.Equal(6f, written[4]);
        }

        [Fact]
        public void AMatrixOfTheDrawingLibraryIsWrittenRowAfterRow()
        {
            SlimDX.Matrix matrix = new SlimDX.Matrix();
            matrix.M12 = 5f;
            matrix.M21 = 6f;
            object[] written = (object[])Write(typeof(SlimDX.Matrix), matrix);
            Assert.Equal(16, written.Length);
            Assert.Equal(5f, written[1]);
            Assert.Equal(6f, written[4]);
        }

        [Fact]
        public void AComponentThatIsNotFiniteIsRefused()
        {
            Refused(typeof(V3), new V3(1f, float.NaN, 3f), "有限でない");
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void ANumberThatIsNotFiniteIsRefused(float value)
        {
            Refused(typeof(float), value, "有限でない");
        }

        [Fact]
        public void AColorIsWrittenWithFourComponents()
        {
            Assert.Equal(
                new object[] { 1f, 0f, 0f, 1f }, Write(typeof(Color), Color.FromArgb(255, 255, 0, 0)));
        }

        [Fact]
        public void ASizeAndAPointAndARectangleAreWrittenInTheirOwnOrder()
        {
            Assert.Equal(new object[] { 3, 4 }, Write(typeof(Size), new Size(3, 4)));
            Assert.Equal(new object[] { 3, 4 }, Write(typeof(Point), new Point(3, 4)));
            Assert.Equal(
                new object[] { 1, 2, 3, 4 }, Write(typeof(Rectangle), new Rectangle(1, 2, 3, 4)));
        }

        [Fact]
        public void AFontIsWrittenAsItsFamilyAndSizeAndStyle()
        {
            using (Font font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold))
            {
                Dictionary<string, object> written = (Dictionary<string, object>)Write(typeof(Font), font);
                Assert.Equal(font.FontFamily.Name, written["family"]);
                Assert.Equal(9f, written["size"]);
                Assert.Equal("Bold", written["style"]);
            }
        }

        [Fact]
        public void TheSizeOfAFontIsWrittenInPointsWhateverTheFontMeasuresItself()
        {
            using (Font measured = new Font(FontFamily.GenericSansSerif, 9f))
            using (Font pixels = new Font(FontFamily.GenericSansSerif, measured.GetHeight(), GraphicsUnit.Pixel))
            {
                Dictionary<string, object> written = (Dictionary<string, object>)Write(typeof(Font), pixels);
                Assert.NotEqual(pixels.Size, written["size"]);
                Assert.Equal(pixels.SizeInPoints, written["size"]);
            }
        }

        [Fact]
        public void ASolidBrushIsWrittenAsItsColor()
        {
            using (Brush brush = new SolidBrush(Color.FromArgb(0, 0, 255, 0)))
            {
                Assert.Equal(new object[] { 0f, 1f, 0f, 0f }, Write(typeof(Brush), brush));
            }
        }

        [Fact]
        public void ABrushThatIsNotSolidIsRefused()
        {
            using (Brush brush = new System.Drawing.Drawing2D.HatchBrush(
                System.Drawing.Drawing2D.HatchStyle.Cross, Color.Red, Color.Blue))
            {
                Refused(typeof(Brush), brush, "単色でない");
            }
        }

        [Fact]
        public void BytesInALineArePackedIntoOneString()
        {
            Assert.Equal("AQID", Write(typeof(byte[]), new byte[] { 1, 2, 3 }));
            Assert.Equal("AQID", Write(typeof(IList<byte>), new List<byte> { 1, 2, 3 }));
        }

        [Fact]
        public void OtherElementsAreWrittenOneByOne()
        {
            Assert.Equal(new object[] { 1, 2 }, Write(typeof(int[]), new[] { 1, 2 }));
            Assert.Equal(
                new object[] { "Front", "Back" },
                Write(typeof(IList<Sides>), new List<Sides> { Sides.Front, Sides.Back }));
        }

        [Fact]
        public void AnElementThatCannotBeWrittenStopsTheWholeSequence()
        {
            Refused(typeof(float[]), new[] { 1f, float.NaN }, "有限でない");
        }

        [Fact]
        public void ArraysOfMoreThanOneDimensionAreNotValues()
        {
            NotAValue(typeof(int[,]), new int[1, 1]);
            NotAValue(typeof(byte[,]), new byte[1, 1]);
        }

        [Fact]
        public void AnImageIsWrittenAsThePackedPicture()
        {
            using (Bitmap image = new Bitmap(4, 4))
            {
                object json;
                IList<string> warnings;
                string code;
                string message;
                Assert.True(ValueShape.TryToJson(
                    typeof(Bitmap), image, MaxLongSide, out json, out warnings, out code, out message));
                Assert.Equal(ImageTransfer.Encode(image, MaxLongSide).Base64, json);
                Assert.Empty(warnings);
            }
        }

        [Fact]
        public void ShrinkingAnImageIsToldInTheWarnings()
        {
            using (Bitmap image = new Bitmap(ImageTransfer.MinimumMaxLongSide + 1, 4))
            {
                object json;
                IList<string> warnings;
                string code;
                string message;
                Assert.True(ValueShape.TryToJson(
                    typeof(Bitmap), image, ImageTransfer.MinimumMaxLongSide,
                    out json, out warnings, out code, out message));
                Assert.NotEmpty(warnings);
            }
        }

        [Fact]
        public void AnyJsonValueIsWrittenAsItStands()
        {
            Dictionary<string, object> loose = new Dictionary<string, object>();
            loose["名前"] = "値";
            loose["数"] = 1;
            loose["並び"] = new object[] { true, null };
            Dictionary<string, object> written = (Dictionary<string, object>)Write(typeof(object), loose);
            Assert.Equal("値", written["名前"]);
            Assert.Equal(1, written["数"]);
            Assert.Equal(new object[] { true, null }, written["並び"]);
        }

        [Fact]
        public void EveryNumberThatJsonCanCarryIsWrittenAsANumber()
        {
            Assert.Equal(1L, Write(typeof(object), 1L));
            Assert.Equal((short)1, Write(typeof(object), (short)1));
            Assert.Equal(1m, Write(typeof(object), 1m));
            Assert.Equal(1UL, Write(typeof(object), 1UL));
        }

        [Fact]
        public void ANumberThatIsNotFiniteIsRefusedAsAnyJsonValueToo()
        {
            Refused(typeof(object), double.NegativeInfinity, "有限でない");
        }

        [Fact]
        public void AnObjectThatIsNotAJsonValueIsRefused()
        {
            Refused(typeof(object), new ValueShapeTests(), "写せない");
        }

        [Fact]
        public void APairWhoseNameIsNotTextIsRefused()
        {
            Dictionary<int, object> loose = new Dictionary<int, object>();
            loose[1] = "値";
            Refused(typeof(object), loose, "名前が文字列でない");
        }

        [Fact]
        public void AReferenceToAValueIsWrittenLikeTheValueItself()
        {
            Assert.Equal(7, Write(typeof(int).MakeByRefType(), 7));
        }

        [Fact]
        public void NoTypeToWriteStops()
        {
            object json;
            IList<string> warnings;
            string code;
            string message;
            Assert.Throws<ArgumentNullException>(() => ValueShape.TryToJson(
                null, 1, MaxLongSide, out json, out warnings, out code, out message));
        }

        /// <summary>成分を明示的な実装で持たせ、実行時の型からは引けない題材とする。</summary>
        private sealed class Coordinates : IPEVector3
        {
            float IPEVector3.X
            {
                get { return 1f; }

                set { }
            }

            float IPEVector3.Y
            {
                get { return 2f; }

                set { }
            }

            float IPEVector3.Z
            {
                get { return 3f; }

                set { }
            }

            float IPEVector3.R
            {
                get { return 0f; }

                set { }
            }

            float IPEVector3.G
            {
                get { return 0f; }

                set { }
            }

            float IPEVector3.B
            {
                get { return 0f; }

                set { }
            }

            public object Clone()
            {
                return new Coordinates();
            }
        }

        private sealed class Turn : IPEQuaternion
        {
            float IPEQuaternion.X
            {
                get { return 1f; }

                set { }
            }

            float IPEQuaternion.Y
            {
                get { return 2f; }

                set { }
            }

            float IPEQuaternion.Z
            {
                get { return 3f; }

                set { }
            }

            float IPEQuaternion.W
            {
                get { return 4f; }

                set { }
            }

            public object Clone()
            {
                return new Turn();
            }
        }

        private static object Write(Type declared, object value)
        {
            object json;
            IList<string> warnings;
            string code;
            string message;
            Assert.True(ValueShape.TryToJson(
                declared, value, MaxLongSide, out json, out warnings, out code, out message));
            Assert.Null(code);
            Assert.Null(message);
            Assert.Empty(warnings);

            return json;
        }

        private static void NotAValue(Type declared, object value)
        {
            object json;
            IList<string> warnings;
            string code;
            string message;
            Assert.False(ValueShape.TryToJson(
                declared, value, MaxLongSide, out json, out warnings, out code, out message));
            Assert.Null(code);
            Assert.Null(message);
            Assert.Null(json);
        }

        private static void Refused(Type declared, object value, string expected)
        {
            object json;
            IList<string> warnings;
            string code;
            string message;
            Assert.False(ValueShape.TryToJson(
                declared, value, MaxLongSide, out json, out warnings, out code, out message));
            Assert.Equal(ToolEnvelope.NotApplicable, code);
            Assert.Contains(expected, message);
            Assert.Null(json);
        }
    }
}
