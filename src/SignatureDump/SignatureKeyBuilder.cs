using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 行キーを組み立てる。行キーは宣言型・メンバー名・総称型引数の数・引数の型と向きの列で
    /// 決まり、同名オーバーロードを引数の列で区別する。引数の列が同じで総称型引数の数だけが違う
    /// オーバーロードが実在するため、その数も行キーに含める。
    ///
    /// 変換演算子だけは、これらがすべて同じで戻り値の型だけが違うオーバーロードを言語が許す。
    /// SDKにも実在する(同じ型から2種類の描画ライブラリの型へ変換する組)ため、変換演算子の行
    /// キーには戻り値の型も付ける。付ける対象はメンバー名で閉じており、衝突の有無では変えない。
    /// </summary>
    public static class SignatureKeyBuilder
    {
        public const string ConstructorName = ".ctor";

        /// <summary>戻り値の型も行キーに含めるメンバー名。</summary>
        public static readonly ReadOnlyCollection<string> ConversionOperatorNames =
            Array.AsReadOnly(new[] { "op_Implicit", "op_Explicit" });

        /// <summary><paramref name="valueType"/> は変換演算子のときだけ行キーへ現れる。</summary>
        public static string Build(
            string declaringType,
            string memberName,
            int genericArity,
            IList<ParameterRecord> parameters,
            string valueType)
        {
            throw new NotImplementedException();
        }
    }
}
