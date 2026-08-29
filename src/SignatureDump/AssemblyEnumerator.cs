using System;
using System.Reflection;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// アセンブリの公開APIをリフレクションで列挙する。母集合は <see cref="Type.IsVisible"/> が
    /// 真の型とし、入れ子の公開型を落とさない。外側が公開でない入れ子の型は、入れ子の側が公開でも
    /// 母集合に入らない。
    ///
    /// 行にするのは、各型が自分で宣言する公開メンバーのうち、メソッド・プロパティ・フィールド・
    /// イベント・コンストラクタの5種類だけである。プロパティとイベントの取得・設定・追加・削除の
    /// アクセサーはメソッドの形で現れるが、そのプロパティ・イベントの行が表すので別の行にしない。
    /// 入れ子の型もメンバーの形で現れるが、型として記録するので行にしない。演算子のように、
    /// 言語が特別な名前を与えるメソッドでも、アクセサーでなければ行にする。
    ///
    /// 上の5種類のうち、次の閉じた集合だけは行にしない。
    /// 列挙型では、値の記憶域 <c>value__</c> と列挙子のフィールド。値の集合は
    /// <see cref="TypeRecord.EnumMembers"/> が持つので落ちない。
    /// デリゲート型では、コンストラクタと <c>BeginInvoke</c> と <c>EndInvoke</c>。どのデリゲート
    /// にも同じ形で現れ、その型固有の引数と戻り値は <c>Invoke</c> が持つ。
    /// これ以外はすべて行にする。デリゲートの <c>Invoke</c> も、クラスが明示的に宣言しない
    /// 公開コンストラクタも行にする。
    /// </summary>
    public static class AssemblyEnumerator
    {
        public static InventoryRecord Enumerate(Assembly assembly)
        {
            throw new NotImplementedException();
        }
    }
}
