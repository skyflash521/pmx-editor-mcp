using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型を、行キーとJSONの双方で使う一意な表記へ写す。入れ子は <c>+</c>、参照渡しは末尾の
    /// <c>&amp;</c>、総称型は山括弧で表す。
    /// </summary>
    public static class TypeNameFormatter
    {
        public static string Format(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type.IsByRef)
            {
                return Format(type.GetElementType()) + "&";
            }

            if (type.IsPointer)
            {
                return Format(type.GetElementType()) + "*";
            }

            if (type.IsArray)
            {
                return Format(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (type.IsGenericType)
            {
                return FormatGeneric(type);
            }

            return NameOf(type);
        }

        private static string NameOf(Type type)
        {
            return type.FullName ?? type.Name;
        }

        // 総称型引数は、入れ子のどの段のものかまで表さないと一意にならない。段をまたいで平らに
        // 並べると、外側が総称の入れ子型と、外側が非総称で内側が2つの型引数を持つ入れ子型が同じ
        // 表記になる。リフレクションは各段の型引数へ外側のぶんも含めて返すので、段ごとの数の差を
        // その段自身の型引数として切り出す。
        private static string FormatGeneric(Type type)
        {
            Type definition = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();
            Type[] arguments = type.GetGenericArguments();

            List<Type> levels = new List<Type>();
            for (Type level = definition; level != null; level = level.DeclaringType)
            {
                levels.Insert(0, level);
            }

            List<string> parts = new List<string>();
            int consumed = 0;
            foreach (Type level in levels)
            {
                int total = level.IsGenericType ? level.GetGenericArguments().Length : 0;
                int own = total - consumed;
                string part = StripArity(level.Name);
                if (own > 0)
                {
                    part += "<" + string.Join(
                        ",", arguments.Skip(consumed).Take(own).Select(Format)) + ">";
                    consumed += own;
                }

                parts.Add(part);
            }

            string joined = string.Join("+", parts);
            return string.IsNullOrEmpty(definition.Namespace) ? joined : definition.Namespace + "." + joined;
        }

        // 総称型の名前は、リフレクション上では型引数の数を表す接尾辞を持つ。数は山括弧の中身から
        // 分かるので、表記へは残さない。
        private static string StripArity(string name)
        {
            int tick = name.IndexOf('`');
            while (tick >= 0)
            {
                int end = tick + 1;
                while (end < name.Length && char.IsDigit(name[end]))
                {
                    end++;
                }

                name = name.Remove(tick, end - tick);
                tick = name.IndexOf('`');
            }

            return name;
        }
    }
}
