using System;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>値をJSONへ写すときの表現の種類。</summary>
    public enum ValueRepresentationKind
    {
        Number,

        Boolean,

        Text,

        /// <summary>バイト列を要素ごとに並べず、ひとつの文字列へ詰めたもの。</summary>
        Base64,

        EnumName,

        /// <summary>成分を並べた数値配列。要素を並べた配列とは別の種類として扱う。</summary>
        NumberArray,

        /// <summary>0〜1の数値を並べた色。</summary>
        Color,

        /// <summary>幅と高さ。</summary>
        Size,

        Point,

        /// <summary>位置と大きさ。</summary>
        Rectangle,

        /// <summary>書体・大きさ・スタイル。</summary>
        Font,

        /// <summary>色から構築する単色ブラシ。</summary>
        Brush,

        /// <summary>PNG画像。</summary>
        Image,

        /// <summary>任意のJSON値。</summary>
        Json,

        /// <summary>値を持たない。</summary>
        Null,
    }

    /// <summary>
    /// 1つの型のJSON表現。配列とnull許容は要素の表現を包む形で表す。<see cref="Identifier"/> は
    /// オーバーロードを分割する接尾辞に使うので、包み方まで含めて1つの表現に1つの綴りが対応する。
    /// </summary>
    public sealed class ValueRepresentation
    {
        private const string ArrayPrefix = "array_of_";

        private const string NullablePrefix = "nullable_";

        private ValueRepresentation(ValueRepresentationKind kind, ValueRepresentation element, bool isNullable)
        {
            Kind = kind;
            Element = element;
            IsNullable = isNullable;
        }

        /// <summary>配列のときは要素の種類と同じ。</summary>
        public ValueRepresentationKind Kind { get; }

        /// <summary>配列のときの要素の表現。配列でなければ null。</summary>
        public ValueRepresentation Element { get; }

        /// <summary>値が無いときに null を許すなら true。</summary>
        public bool IsNullable { get; }

        public bool IsArray
        {
            get { return Element != null; }
        }

        /// <summary>機械用の識別子。</summary>
        public string Identifier
        {
            get
            {
                string body = IsArray ? ArrayPrefix + Element.Identifier : Name(Kind);

                return IsNullable ? NullablePrefix + body : body;
            }
        }

        public static ValueRepresentation Of(ValueRepresentationKind kind)
        {
            return new ValueRepresentation(kind, null, false);
        }

        public static ValueRepresentation ArrayOf(ValueRepresentation element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return new ValueRepresentation(element.Kind, element, false);
        }

        /// <summary>同じ表現で null を許す形を作る。</summary>
        public ValueRepresentation AsNullable()
        {
            return IsNullable ? this : new ValueRepresentation(Kind, Element, true);
        }

        private static string Name(ValueRepresentationKind kind)
        {
            switch (kind)
            {
                case ValueRepresentationKind.Number:
                    return "number";
                case ValueRepresentationKind.Boolean:
                    return "boolean";
                case ValueRepresentationKind.Text:
                    return "text";
                case ValueRepresentationKind.Base64:
                    return "base64";
                case ValueRepresentationKind.EnumName:
                    return "enum_name";
                case ValueRepresentationKind.NumberArray:
                    return "number_array";
                case ValueRepresentationKind.Color:
                    return "color";
                case ValueRepresentationKind.Size:
                    return "size";
                case ValueRepresentationKind.Point:
                    return "point";
                case ValueRepresentationKind.Rectangle:
                    return "rectangle";
                case ValueRepresentationKind.Font:
                    return "font";
                case ValueRepresentationKind.Brush:
                    return "brush";
                case ValueRepresentationKind.Image:
                    return "image";
                case ValueRepresentationKind.Json:
                    return "json";
                case ValueRepresentationKind.Null:
                    return "null_value";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
