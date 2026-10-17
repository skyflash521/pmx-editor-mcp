using System;
using System.Collections.Generic;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeNameFormatterTests
    {
        [Fact]
        public void BuiltInTypesAreWrittenWithTheirNamespace()
        {
            Assert.Equal("System.Int32", TypeNameFormatter.Format(typeof(int)));
            Assert.Equal("System.String", TypeNameFormatter.Format(typeof(string)));
            Assert.Equal("System.Void", TypeNameFormatter.Format(typeof(void)));
        }

        [Fact]
        public void ArraysAppendBracketsToTheElementType()
        {
            Assert.Equal("System.Int32[]", TypeNameFormatter.Format(typeof(int[])));
            Assert.Equal("System.Int32[][]", TypeNameFormatter.Format(typeof(int[][])));
            Assert.Equal("System.Int32[,]", TypeNameFormatter.Format(typeof(int[,])));
        }

        [Fact]
        public void ByReferenceAppendsAnAmpersand()
        {
            Type byRef = typeof(int).MakeByRefType();

            Assert.Equal("System.Int32&", TypeNameFormatter.Format(byRef));
        }

        [Fact]
        public void GenericTypesListTypeArgumentsInAngleBrackets()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<System.String>",
                TypeNameFormatter.Format(typeof(IList<string>)));
            Assert.Equal(
                "System.Collections.Generic.IDictionary<System.String,System.Int32>",
                TypeNameFormatter.Format(typeof(IDictionary<string, int>)));
        }

        [Fact]
        public void GenericDefinitionsListTypeParameterNames()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<T>",
                TypeNameFormatter.Format(typeof(IList<>)));
        }

        [Fact]
        public void NestedTypesJoinOuterAndInnerWithPlus()
        {
            Assert.Equal(
                "PmxEditorMcp.SignatureDump.Tests.Sample.SampleOuter+SampleNested",
                TypeNameFormatter.Format(typeof(SampleOuter.SampleNested)));
        }

        [Fact]
        public void UserDefinedGenericDefinitionsAlsoListTypeParameterNames()
        {
            Assert.Equal(
                "PmxEditorMcp.SignatureDump.Tests.Sample.SampleGeneric<T>",
                TypeNameFormatter.Format(typeof(SampleGeneric<>)));
        }

        [Fact]
        public void GenericParametersAreWrittenByName()
        {
            Type parameter = typeof(SampleGeneric<>).GetGenericArguments()[0];

            Assert.Equal("T", TypeNameFormatter.Format(parameter));
        }

        [Fact]
        public void TheSameRuleRecursesIntoTypeArguments()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<System.Int32[]>",
                TypeNameFormatter.Format(typeof(IList<int[]>)));
        }

        [Fact]
        public void MissingTypeThrows()
        {
            Assert.Throws<ArgumentNullException>(() => TypeNameFormatter.Format(null));
        }
    }
}
