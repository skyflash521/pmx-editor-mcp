using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public class ValueRepresentationRuleTests
    {
        private const string EnumTypeName = "PEPlugin.Pmx.MorphKind";

        private const string ReferencedEnumTypeName = "System.Windows.Forms.MouseButtons";

        private const string RoleTypeName = "PEPlugin.Pmx.IPXVertex";

        private const string InterfaceNamedLikeAnEnumTypeName = "PEPlugin.Pmx.SphereKind";

        [Theory]
        [InlineData(ValueRepresentationKind.Number, "number")]
        [InlineData(ValueRepresentationKind.Boolean, "boolean")]
        [InlineData(ValueRepresentationKind.Text, "text")]
        [InlineData(ValueRepresentationKind.Base64, "base64")]
        [InlineData(ValueRepresentationKind.EnumName, "enum_name")]
        [InlineData(ValueRepresentationKind.NumberArray, "number_array")]
        [InlineData(ValueRepresentationKind.Color, "color")]
        [InlineData(ValueRepresentationKind.Size, "size")]
        [InlineData(ValueRepresentationKind.Point, "point")]
        [InlineData(ValueRepresentationKind.Rectangle, "rectangle")]
        [InlineData(ValueRepresentationKind.Font, "font")]
        [InlineData(ValueRepresentationKind.Brush, "brush")]
        [InlineData(ValueRepresentationKind.Image, "image")]
        [InlineData(ValueRepresentationKind.Json, "json")]
        [InlineData(ValueRepresentationKind.Null, "null_value")]
        public void EachKindHasItsOwnSpelling(ValueRepresentationKind kind, string expected)
        {
            Assert.Equal(expected, ValueRepresentation.Of(kind).Identifier);
        }

        [Fact]
        public void AnArrayOfNumbersAndTheNumberArrayKindHaveDifferentSpellings()
        {
            Assert.Equal(
                "array_of_number",
                ValueRepresentation.ArrayOf(
                    ValueRepresentation.Of(ValueRepresentationKind.Number)).Identifier);
            Assert.Equal(
                "number_array",
                ValueRepresentation.Of(ValueRepresentationKind.NumberArray).Identifier);
            Assert.Equal(
                "array_of_number_array",
                ValueRepresentation.ArrayOf(
                    ValueRepresentation.Of(ValueRepresentationKind.NumberArray)).Identifier);
        }

        [Fact]
        public void ANullableRepresentationHasItsOwnSpelling()
        {
            Assert.Equal(
                "nullable_number",
                ValueRepresentation.Of(ValueRepresentationKind.Number).AsNullable().Identifier);
            Assert.Equal(
                "array_of_nullable_number",
                ValueRepresentation.ArrayOf(
                    ValueRepresentation.Of(
                        ValueRepresentationKind.Number).AsNullable()).Identifier);
        }

        [Fact]
        public void MakingANullableRepresentationNullableAgainKeepsOneSpelling()
        {
            Assert.Equal(
                "nullable_text",
                ValueRepresentation.Of(
                    ValueRepresentationKind.Text).AsNullable().AsNullable().Identifier);
        }

        [Fact]
        public void ArrayOfWithoutAnElementThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ValueRepresentation.ArrayOf(null));
        }

        [Fact]
        public void SpellingAKindOutsideTheClosedSetThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ValueRepresentation.Of((ValueRepresentationKind)999).Identifier);
        }

        [Theory]
        [InlineData("System.Boolean", ValueRepresentationKind.Boolean)]
        [InlineData("System.Byte", ValueRepresentationKind.Number)]
        [InlineData("System.Int32", ValueRepresentationKind.Number)]
        [InlineData("System.Single", ValueRepresentationKind.Number)]
        [InlineData("System.Double", ValueRepresentationKind.Number)]
        [InlineData("System.String", ValueRepresentationKind.Text)]
        [InlineData("System.Version", ValueRepresentationKind.Text)]
        [InlineData("System.Object", ValueRepresentationKind.Json)]
        [InlineData("System.Void", ValueRepresentationKind.Null)]
        public void ScalarTypesHaveTheirOwnRepresentation(string typeName, ValueRepresentationKind expected)
        {
            Assert.Equal(expected, Classify(typeName).Kind);
        }

        [Theory]
        [InlineData("PEPlugin.SDX.V2")]
        [InlineData("PEPlugin.SDX.V3")]
        [InlineData("PEPlugin.SDX.V4")]
        [InlineData("PEPlugin.SDX.Q")]
        [InlineData("PEPlugin.SDX.M")]
        [InlineData("SlimDX.Vector2")]
        [InlineData("SlimDX.Vector3")]
        [InlineData("SlimDX.Vector4")]
        [InlineData("SlimDX.Quaternion")]
        [InlineData("SlimDX.Matrix")]
        public void SdxNumericTypesBecomeNumberArrays(string typeName)
        {
            Assert.Equal(ValueRepresentationKind.NumberArray, Classify(typeName).Kind);
        }

        [Theory]
        [InlineData("PEPlugin.Pmd.IPEVector2")]
        [InlineData("PEPlugin.Pmd.IPEVector3")]
        [InlineData("PEPlugin.Pmd.IPEVector4")]
        [InlineData("PEPlugin.Pmd.IPEQuaternion")]
        [InlineData("PEPlugin.Pmd.IPEMatrix")]
        public void PmdNumericTypesBecomeNumberArrays(string typeName)
        {
            Assert.Equal(ValueRepresentationKind.NumberArray, Classify(typeName).Kind);
        }

        [Theory]
        [InlineData("System.Drawing.Color", ValueRepresentationKind.Color)]
        [InlineData("SlimDX.Color3", ValueRepresentationKind.Color)]
        [InlineData("SlimDX.Color4", ValueRepresentationKind.Color)]
        [InlineData("System.Drawing.Size", ValueRepresentationKind.Size)]
        [InlineData("System.Drawing.Point", ValueRepresentationKind.Point)]
        [InlineData("System.Drawing.Rectangle", ValueRepresentationKind.Rectangle)]
        [InlineData("System.Drawing.Font", ValueRepresentationKind.Font)]
        [InlineData("System.Drawing.Brush", ValueRepresentationKind.Brush)]
        [InlineData("System.Drawing.Bitmap", ValueRepresentationKind.Image)]
        public void DrawingTypesHaveTheirOwnRepresentation(string typeName, ValueRepresentationKind expected)
        {
            Assert.Equal(expected, Classify(typeName).Kind);
        }

        [Fact]
        public void EnumTypesBecomeTheirMemberName()
        {
            Assert.Equal(ValueRepresentationKind.EnumName, Classify(EnumTypeName).Kind);
        }

        [Fact]
        public void EnumTypesDeclaredOutsideTheAssemblyAreAlsoRead()
        {
            Assert.Equal(ValueRepresentationKind.EnumName, Classify(ReferencedEnumTypeName).Kind);
        }

        [Fact]
        public void ATypeNamedLikeAnEnumButClassifiedOtherwiseHasNoRepresentation()
        {
            ValueRepresentation representation;

            Assert.False(Rule().TryClassify(InterfaceNamedLikeAnEnumTypeName, out representation));
        }

        [Fact]
        public void ByteArraysBecomeOneBase64TextInsteadOfAnArrayOfNumbers()
        {
            ValueRepresentation representation = Classify("System.Byte[]");

            Assert.Equal(ValueRepresentationKind.Base64, representation.Kind);
            Assert.False(representation.IsArray);
        }

        [Fact]
        public void ListsOfBytesAlsoBecomeOneBase64Text()
        {
            Assert.Equal(
                "base64",
                Classify("System.Collections.Generic.IList<System.Byte>").Identifier);
        }

        [Fact]
        public void ListsOfMultiDimensionalArraysAreNotValuesEither()
        {
            Assert.False(Rule().TryClassify(
                "System.Collections.Generic.IList<System.Byte[,]>", out ValueRepresentation _));
        }

        [Fact]
        public void NestedByteArraysWrapTheBase64OfEachInnerArray()
        {
            Assert.Equal("array_of_base64", Classify("System.Byte[][]").Identifier);
        }

        [Fact]
        public void ArraysWrapTheRepresentationOfTheirElement()
        {
            ValueRepresentation representation = Classify("System.Int32[]");

            Assert.True(representation.IsArray);
            Assert.Equal(ValueRepresentationKind.Number, representation.Element.Kind);
            Assert.Equal("array_of_number", representation.Identifier);
        }

        [Theory]
        [InlineData("System.Byte[,]")]
        [InlineData("System.String[,]")]
        [InlineData("System.Int32[,,]")]
        [InlineData("System.Byte[,]&")]
        public void MultiDimensionalArraysAreNotValues(string typeName)
        {
            Assert.False(Rule().TryClassify(typeName, out ValueRepresentation _));
        }

        [Fact]
        public void NestedArraysWrapOnceForEachLevel()
        {
            Assert.Equal("array_of_array_of_number", Classify("System.Int32[][]").Identifier);
        }

        [Fact]
        public void ListsWrapTheRepresentationOfTheirElement()
        {
            ValueRepresentation representation =
                Classify("System.Collections.Generic.IList<System.String>");

            Assert.True(representation.IsArray);
            Assert.Equal(ValueRepresentationKind.Text, representation.Element.Kind);
        }

        [Fact]
        public void NullableValueTypesKeepTheRepresentationOfTheirArgument()
        {
            ValueRepresentation representation = Classify("System.Nullable<System.Int32>");

            Assert.Equal(ValueRepresentationKind.Number, representation.Kind);
            Assert.True(representation.IsNullable);
        }

        [Theory]
        [InlineData("System.Int32&", "number")]
        [InlineData("System.Byte[]&", "base64")]
        [InlineData("System.Collections.Generic.IList<System.Byte>&", "base64")]
        [InlineData("System.Nullable<System.Int32>&", "nullable_number")]
        [InlineData("System.Nullable<System.Int32>[]&", "array_of_nullable_number")]
        public void ByReferenceArgumentsKeepTheRepresentationOfTheirType(
            string typeName, string expected)
        {
            Assert.Equal(expected, Classify(typeName).Identifier);
        }

        [Fact]
        public void TypesTheTableDoesNotCoverHaveNoRepresentation()
        {
            ValueRepresentation representation;

            Assert.False(Rule().TryClassify(RoleTypeName, out representation));
            Assert.Null(representation);
        }

        [Fact]
        public void ArraysAndListsOfATypeWithoutARepresentationHaveNoRepresentationEither()
        {
            ValueRepresentation representation;

            Assert.False(Rule().TryClassify(RoleTypeName + "[]", out representation));
            Assert.False(Rule().TryClassify(
                "System.Collections.Generic.IList<" + RoleTypeName + ">", out representation));
        }

        [Fact]
        public void ListsTakingMoreThanOneArgumentAreNotTreatedAsSequences()
        {
            ValueRepresentation representation;

            Assert.False(Rule().TryClassify(
                "System.Collections.Generic.IList<System.Int32,System.String>", out representation));
        }

        [Fact]
        public void NullableOfATypeWithoutARepresentationHasNoRepresentationEither()
        {
            ValueRepresentation representation;

            Assert.False(Rule().TryClassify(
                "System.Nullable<" + RoleTypeName + ">", out representation));
            Assert.Null(representation);
        }

        [Fact]
        public void ClassifyingNullThrows()
        {
            ValueRepresentation representation;

            Assert.Throws<ArgumentNullException>(() => Rule().TryClassify(null, out representation));
        }

        [Fact]
        public void CreatingWithoutAnInventoryThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ValueRepresentationRule.Create(null));
        }

        private static ValueRepresentation Classify(string typeName)
        {
            ValueRepresentation representation;
            Assert.True(Rule().TryClassify(typeName, out representation), typeName);

            return representation;
        }

        private static ValueRepresentationRule Rule()
        {
            return ValueRepresentationRule.Create(Inventory());
        }

        private static InventoryRecord Inventory()
        {
            return new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord>
                {
                    Type(EnumTypeName, TypeKind.Enum),
                    Type(RoleTypeName, TypeKind.Interface),
                    Type(InterfaceNamedLikeAnEnumTypeName, TypeKind.Interface),
                },
                new List<TypeRecord> { Type(ReferencedEnumTypeName, TypeKind.Enum) },
                new List<SignatureRecord>());
        }

        private static TypeRecord Type(string name, TypeKind kind)
        {
            return new TypeRecord(
                name,
                kind,
                false,
                kind == TypeKind.Interface,
                false,
                new ReadOnlyCollection<string>(new List<string>()),
                new ReadOnlyCollection<string>(new List<string>()));
        }
    }
}
