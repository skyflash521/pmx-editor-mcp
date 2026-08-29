using System;
using System.Collections.Generic;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class TypeNameFormatterTests
    {
        [Fact(Skip = "impl pending: 組み込み型を名前空間つきの表記へ写す")]
        public void 組み込み型は名前空間つきの表記になる()
        {
            Assert.Equal("System.Int32", TypeNameFormatter.Format(typeof(int)));
            Assert.Equal("System.String", TypeNameFormatter.Format(typeof(string)));
            Assert.Equal("System.Void", TypeNameFormatter.Format(typeof(void)));
        }

        [Fact(Skip = "impl pending: 配列を要素型の表記に角括弧を付けて写す")]
        public void 配列は要素型に角括弧が付く()
        {
            Assert.Equal("System.Int32[]", TypeNameFormatter.Format(typeof(int[])));
            Assert.Equal("System.Int32[][]", TypeNameFormatter.Format(typeof(int[][])));
            Assert.Equal("System.Int32[,]", TypeNameFormatter.Format(typeof(int[,])));
        }

        [Fact(Skip = "impl pending: 参照渡しの型を末尾のアンパサンドで表す")]
        public void 参照渡しは末尾にアンパサンドが付く()
        {
            Type byRef = typeof(int).MakeByRefType();

            Assert.Equal("System.Int32&", TypeNameFormatter.Format(byRef));
        }

        [Fact(Skip = "impl pending: 総称型を山括弧つきの表記へ写す")]
        public void 総称型は山括弧で型引数を並べる()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<System.String>",
                TypeNameFormatter.Format(typeof(IList<string>)));
            Assert.Equal(
                "System.Collections.Generic.IDictionary<System.String,System.Int32>",
                TypeNameFormatter.Format(typeof(IDictionary<string, int>)));
        }

        [Fact(Skip = "impl pending: 総称型の定義を型引数の名前つきで写す")]
        public void 総称型の定義は型引数の名前を並べる()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<T>",
                TypeNameFormatter.Format(typeof(IList<>)));
        }

        [Fact(Skip = "impl pending: 入れ子の公開型を外側と内側をつないだ表記へ写す")]
        public void 入れ子の型は外側と内側をプラスでつなぐ()
        {
            Assert.Equal(
                "PmxEditorMcp.SignatureDump.Tests.Sample.SampleOuter+SampleNested",
                TypeNameFormatter.Format(typeof(SampleOuter.SampleNested)));
        }

        [Fact(Skip = "impl pending: 利用者が定義した総称型の定義も型引数の名前つきで写す")]
        public void 利用者定義の総称型の定義も型引数の名前を並べる()
        {
            Assert.Equal(
                "PmxEditorMcp.SignatureDump.Tests.Sample.SampleGeneric<T>",
                TypeNameFormatter.Format(typeof(SampleGeneric<>)));
        }

        [Fact(Skip = "impl pending: 総称型引数そのものを名前で写す")]
        public void 総称型引数は名前で表す()
        {
            Type parameter = typeof(SampleGeneric<>).GetGenericArguments()[0];

            Assert.Equal("T", TypeNameFormatter.Format(parameter));
        }

        [Fact(Skip = "impl pending: 総称型の引数に配列や入れ子を含む場合も再帰して写す")]
        public void 総称型の引数にも同じ規則が再帰する()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<System.Int32[]>",
                TypeNameFormatter.Format(typeof(IList<int[]>)));
        }

        [Fact(Skip = "impl pending: 型が指定されていないときは例外にする")]
        public void 型を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => TypeNameFormatter.Format(null));
        }
    }
}
