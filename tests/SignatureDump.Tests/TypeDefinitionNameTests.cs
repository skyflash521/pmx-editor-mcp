using System;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeDefinitionNameTests
    {
        [Fact]
        public void ANameWithoutTypeArgumentsIsUnchanged()
        {
            Assert.Equal("N.IThing", TypeDefinitionName.Of("N.IThing"));
            Assert.Equal("N.Outer+Inner", TypeDefinitionName.Of("N.Outer+Inner"));
        }

        [Fact]
        public void TypeArgumentsBecomeTheirCount()
        {
            Assert.Equal("N.Box<1>", TypeDefinitionName.Of("N.Box<System.Int32>"));
            Assert.Equal("N.Pair<2>", TypeDefinitionName.Of("N.Pair<System.Int32,System.String>"));
        }

        /// <summary>
        /// 鍵へ写したものをもう一度写しても同じ鍵になる。正本が鍵の形で持つ型名を引き当ての側と
        /// そろえるとき、写しが二度掛かる。
        /// </summary>
        [Fact]
        public void AKeyIsUnchangedByWritingItAgain()
        {
            Assert.Equal("N.Box<1>", TypeDefinitionName.Of("N.Box<1>"));
            Assert.Equal("N.Pair<2>", TypeDefinitionName.Of("N.Pair<2>"));
            Assert.Equal("N.Many<12>", TypeDefinitionName.Of("N.Many<12>"));
            Assert.Equal("N.Pair<2>", TypeDefinitionName.OfElement("N.Pair<2>[]"));
        }

        [Fact]
        public void TheOpenDefinitionAndAClosedTypeShareTheSameKey()
        {
            Assert.Equal(TypeDefinitionName.Of("N.Box<T>"), TypeDefinitionName.Of("N.Box<System.Int32>"));
        }

        [Fact]
        public void TypesDifferingOnlyInTheNumberOfArgumentsKeepDifferentKeys()
        {
            Assert.NotEqual(
                TypeDefinitionName.Of("N.Box<System.Int32>"),
                TypeDefinitionName.Of("N.Box<System.Int32,System.String>"));
        }

        [Fact]
        public void ArgumentsInsideAnArrayRankOrANestedTypeAreNotCounted()
        {
            Assert.Equal("N.Box<1>", TypeDefinitionName.Of("N.Box<System.Int32[,]>"));
            Assert.Equal("N.Box<1>", TypeDefinitionName.Of("N.Box<N.Pair<System.Int32,System.String>>"));
        }

        [Fact]
        public void EachLevelKeepsItsOwnCount()
        {
            Assert.Equal("N.Outer<1>+Inner<2>", TypeDefinitionName.Of("N.Outer<T>+Inner<U,V>"));
        }

        [Fact]
        public void ArgumentsAreReturnedForEveryLevel()
        {
            Assert.Equal(
                new[] { "System.Int32", "System.String" },
                TypeDefinitionName.Arguments("N.Outer<System.Int32>+Inner<System.String>").ToArray());
        }

        [Fact]
        public void ANameWithoutTypeArgumentsHasNoArguments()
        {
            Assert.Empty(TypeDefinitionName.Arguments("N.IThing"));
        }

        [Fact]
        public void ArgumentsKeepTheirOwnTypeArguments()
        {
            Assert.Equal(
                new[] { "N.Pair<System.Int32,System.String>" },
                TypeDefinitionName.Arguments("N.Box<N.Pair<System.Int32,System.String>>").ToArray());
        }

        [Fact]
        public void BothEntryPointsRequireText()
        {
            Assert.Throws<ArgumentNullException>(() => TypeDefinitionName.Of(null));
            Assert.Throws<ArgumentException>(() => TypeDefinitionName.Of(" "));
            Assert.Throws<ArgumentNullException>(() => TypeDefinitionName.Arguments(null));
            Assert.Throws<ArgumentException>(() => TypeDefinitionName.Arguments(" "));
        }
    }
}
