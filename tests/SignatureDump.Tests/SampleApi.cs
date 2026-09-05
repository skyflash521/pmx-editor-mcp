using System;
using System.Collections.Generic;

namespace PmxEditorMcp.SignatureDump.Tests.Sample
{
    // この名前空間の型は、列挙器が扱う経路を1か所へ集めた題材である。公開していない型を
    // 混ぜてあるのは、列挙の母集合から外れることを確かめるためである。

    public interface ISampleRoot
    {
        int RootValue { get; }
    }

    // 2段の継承。継承している型を推移的に記録するかどうかは、一段の継承では区別できない。
    public interface ISampleBase : ISampleRoot
    {
        int BaseValue { get; }
    }

    public interface ISampleAux
    {
        int AuxValue { get; }
    }

    // 継承の宣言順(Base のあと Aux)と表記の昇順(Aux のあと Base)がずれるように並べてある。
    public interface ISampleApi : ISampleBase, ISampleAux
    {
        int Value { get; set; }

        string ReadOnlyName { get; }

        int WriteOnlyLevel { set; }

        event EventHandler Changed;

        void SetThing(int index, string text);

        bool TryGet(int index, out string text);

        void Swap(ref int a, ref int b);

        int GetCount();

        // 戻り値を持ち取得の名前で始まるが入出力引数を持つ。出力引数だけを見る判定では読み取りへ
        // 誤って倒れる。
        bool GetState(ref int value);

        void Fill(int[] values, IList<string> names);

        void Pack(byte[] data);

        IList<DateTime> Stamps();

        void Apply();

        void Walk(SampleProc step);

        string this[Guid key] { get; }

        // 引数の列が同じで総称型引数の数だけが違うオーバーロード。行キーが両者を区別できる
        // ことを確かめるために置く。
        void Apply<T>();

        // 総称型引数が引数の型と戻り値の型の両方に現れる。行キーは戻り値の型を含まないので、
        // 型の表記を引数と戻り値へ正しく当てているかはこの経路でしか確かめられない。
        T Convert<T>(T value);
    }

    public sealed class SampleData
    {
        public int Field;

        public static readonly string Marker = "sample";

        // 定数は静的かつ書き換え不能だが、読み取り専用フィールドとは別の印で表される。片方だけを
        // 見る実装は、もう片方を書き換え可能にしてしまう。
        public const string Tag = "tag";

        public SampleData()
        {
        }

        public SampleData(int seed)
        {
            Field = seed;
            Computed = seed;
        }

        public int Computed { get; private set; }

        public static int Total { get; set; }

        public static SampleData Create(int seed)
        {
            return new SampleData(seed);
        }

        public void Reset(int seed = 3)
        {
            Field = seed;
        }

        // 演算子はアクセサーと同じく言語が特別な名前を与えるが、行にする側である。特別な名前の
        // メソッドを一律に除いた実装は、ここで行を落とす。
        public static SampleData operator +(SampleData left, SampleData right)
        {
            return new SampleData(left.Field + right.Field);
        }

        // 戻り値の型だけが違う変換演算子の組。言語がこの多重定義を許すのは変換演算子だけで、
        // SDKにも実在する。戻り値を見ない行キーは、この2つを同じ行にしてしまう。
        public static implicit operator int(SampleData value)
        {
            return value.Field;
        }

        public static implicit operator long(SampleData value)
        {
            return value.Field;
        }
    }

    // 抽象クラスなので、明示的に宣言しないコンストラクタは公開されない。
    public abstract class SampleBaseClass : ISampleRoot
    {
        public int Level { get; set; }

        public int RootValue
        {
            get { return 0; }
        }
    }

    // 基底クラスとインターフェースの両方を持ち、基底クラス経由でもインターフェースを継承する。
    public sealed class SampleDerived : SampleBaseClass, ISampleAux
    {
        public int AuxValue { get; }
    }

    // 宣言順・値の順・名前の順がすべて違うように並べてある。値の順や名前の順で並べる実装は、
    // 宣言順という契約から外れていてもこの題材でしか見分けられない。
    public enum SampleKind
    {
        Second = 2,
        First = 1,
    }

    // 組み合わせを許す列挙。許さない SampleKind と並べておかないと、可否を見ない実装が通る。
    [Flags]
    public enum SampleFlags
    {
        None = 0,
        Left = 1,
        Right = 2,
    }

    public delegate int SampleProc(int x);

    public static class SampleOuter
    {
        public sealed class SampleNested
        {
            public int Nested { get; set; }
        }
    }

    public sealed class SampleGeneric<T>
    {
        public T Value { get; set; }
    }

    // 構造体だけが取る分類を通す題材。
    public struct SampleValue
    {
        public int X;
    }

    // 入れ子の総称型。型引数がどの段のものかを表記が保てているかは、この形でしか確かめられない。
    // 段をまたいで平らに並べる表記では、外側と内側の型引数が区別できなくなる。
    public sealed class SampleOuterGeneric<TOuter>
    {
        public sealed class SampleInnerGeneric<TInner>
        {
            public TOuter Outer { get; set; }

            public TInner Inner { get; set; }
        }
    }

    internal interface IHiddenApi
    {
        void Nope();
    }

    // 外側が公開でないので、入れ子の型は公開でも母集合に入らない。公開かどうかを入れ子の型の
    // 修飾子だけで判定する実装は、ここで余分な型を拾う。
    internal static class HiddenOuter
    {
        public sealed class VisibleNested
        {
            public int Value { get; set; }
        }
    }
}

namespace PmxEditorMcp.SignatureDump.Tests.OtherSample
{
    // 別の名前空間にも公開型を置く。列挙の母集合はアセンブリ全体なので、特定の名前空間だけを
    // 見る実装はここで落ちる。
    public interface IOtherApi
    {
        int OtherValue { get; }
    }
}
