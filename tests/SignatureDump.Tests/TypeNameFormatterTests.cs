using System;
using System.Collections.Generic;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeNameFormatterTests
    {
        [Fact]
        public void 組み込み型は名前空間つきの表記になる()
        {
            Assert.Equal("System.Int32", TypeNameFormatter.Format(typeof(int)));
            Assert.Equal("System.String", TypeNameFormatter.Format(typeof(string)));
            Assert.Equal("System.Void", TypeNameFormatter.Format(typeof(void)));
        }

        [Fact]
        public void 配列は要素型に角括弧が付く()
        {
            Assert.Equal("System.Int32[]", TypeNameFormatter.Format(typeof(int[])));
            Assert.Equal("System.Int32[][]", TypeNameFormatter.Format(typeof(int[][])));
            Assert.Equal("System.Int32[,]", TypeNameFormatter.Format(typeof(int[,])));
        }

        [Fact]
        public void 参照渡しは末尾にアンパサンドが付く()
        {
            Type byRef = typeof(int).MakeByRefType();

            Assert.Equal("System.Int32&", TypeNameFormatter.Format(byRef));
        }

        [Fact]
        public void 総称型は山括弧で型引数を並べる()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<System.String>",
                TypeNameFormatter.Format(typeof(IList<string>)));
            Assert.Equal(
                "System.Collections.Generic.IDictionary<System.String,System.Int32>",
                TypeNameFormatter.Format(typeof(IDictionary<string, int>)));
        }

        [Fact]
        public void 総称型の定義は型引数の名前を並べる()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<T>",
                TypeNameFormatter.Format(typeof(IList<>)));
        }

        [Fact]
        public void 入れ子の型は外側と内側をプラスでつなぐ()
        {
            Assert.Equal(
                "PmxEditorMcp.SignatureDump.Tests.Sample.SampleOuter+SampleNested",
                TypeNameFormatter.Format(typeof(SampleOuter.SampleNested)));
        }

        [Fact]
        public void 利用者定義の総称型の定義も型引数の名前を並べる()
        {
            Assert.Equal(
                "PmxEditorMcp.SignatureDump.Tests.Sample.SampleGeneric<T>",
                TypeNameFormatter.Format(typeof(SampleGeneric<>)));
        }

        [Fact]
        public void 総称型引数は名前で表す()
        {
            Type parameter = typeof(SampleGeneric<>).GetGenericArguments()[0];

            Assert.Equal("T", TypeNameFormatter.Format(parameter));
        }

        [Fact]
        public void 総称型の引数にも同じ規則が再帰する()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<System.Int32[]>",
                TypeNameFormatter.Format(typeof(IList<int[]>)));
        }

        [Fact]
        public void 型を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => TypeNameFormatter.Format(null));
        }
    }
}
