using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 要素の種類1つ。<c>kind</c> が取る値ごとに、並びの在りかと、要素の作り方と複製の仕方を持つ。
    /// 並びを持つ相手は、PMXが直に並べる種類ではPMXそのもの、そうでなければ親の要素である。
    /// </summary>
    public sealed class ElementKind
    {
        private readonly Func<object, IList<object>> _items;

        private readonly Action<object, IList<object>> _replace;

        private readonly Func<object, object, string, object> _create;

        private readonly Func<object, object> _clone;

        private readonly IList<string> _variants;

        internal ElementKind(
            string name,
            ElementKind owner,
            Func<object, IList<object>> items,
            Action<object, IList<object>> replace,
            Func<object, object, string, object> create,
            Func<object, object> clone,
            IList<string> variants)
        {
            Name = name;
            Owner = owner;
            _items = items;
            _replace = replace;
            _create = create;
            _clone = clone;
            _variants = new ReadOnlyCollection<string>(variants ?? new string[0]);
        }

        /// <summary><c>kind</c> が取る値。</summary>
        public string Name { get; }

        /// <summary>並びを持つ親の種類。PMXが直に並べる種類では null。</summary>
        public ElementKind Owner { get; }

        /// <summary>
        /// 種類だけでは作る形が決まらないときに、<c>variant</c> が取れる値。決まる種類では空。
        /// </summary>
        public IList<string> Variants
        {
            get { return _variants; }
        }

        /// <summary>その相手が並べている要素。並びそのものではなく写しを返す。</summary>
        public IList<object> Items(object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            return _items(owner);
        }

        /// <summary>その相手の並びを、渡した順の並びで置き換える。</summary>
        public void Replace(object owner, IList<object> items)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            _replace(owner, items);
        }

        /// <summary>
        /// この種類の要素を1つ作る。<paramref name="variant"/> は、種類だけでは形が決まらないときに
        /// どれを作るかを指す値で、決まる種類では null を渡す。作れない種類では null を返す。
        /// </summary>
        public object Create(object builder, object owner, string variant)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return _create(builder, owner, variant);
        }

        /// <summary>その要素の複製。</summary>
        public object CloneOf(object item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return _clone(item);
        }
    }

    /// <summary>
    /// 組み立てたツールが <c>kind</c> で受け取る要素の種類の表。名前から種類を引き、知らない名前は
    /// 受け取れる名前を並べて断る。
    /// </summary>
    public static class ElementKinds
    {
        /// <summary>種類を受け取る入力の名前。</summary>
        public const string KindName = "kind";

        /// <summary>種類だけでは作る形が決まらないときに、どれを作るかを受け取る入力の名前。</summary>
        public const string VariantName = "variant";

        /// <summary>PMXが直に並べる頂点。</summary>
        public const string Vertex = "vertex";

        /// <summary>材質が並べる面。</summary>
        public const string Face = "face";

        /// <summary>PMXが直に並べる材質。</summary>
        public const string Material = "material";

        /// <summary>PMXが直に並べるボーン。</summary>
        public const string Bone = "bone";

        /// <summary>ボーンのIKが並べるリンク。</summary>
        public const string IkLink = "ikLink";

        /// <summary>PMXが直に並べるモーフ。</summary>
        public const string Morph = "morph";

        /// <summary>モーフが並べるオフセット。</summary>
        public const string MorphOffset = "morphOffset";

        /// <summary>PMXが直に並べる表示枠。</summary>
        public const string Node = "node";

        /// <summary>表示枠が並べる要素。</summary>
        public const string NodeItem = "nodeItem";

        /// <summary>PMXが直に並べる剛体。</summary>
        public const string Body = "body";

        /// <summary>PMXが直に並べるJoint。</summary>
        public const string Joint = "joint";

        /// <summary>PMXが直に並べるSoftBody。</summary>
        public const string SoftBody = "softBody";

        /// <summary>SoftBodyが並べるアンカー。</summary>
        public const string SoftBodyAnchor = "softBodyAnchor";

        /// <summary>表示枠要素のうち、ボーンを指すもの。</summary>
        public const string BoneVariant = "bone";

        /// <summary>表示枠要素のうち、モーフを指すもの。</summary>
        public const string MorphVariant = "morph";

        /// <summary>受け取れる種類の名前。スキーマが並べる順。</summary>
        public static IList<string> Names
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>名前から種類を引く。知らない名前なら偽で、断る説明を渡す。</summary>
        public static bool TryResolve(object given, out ElementKind kind, out string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>その種類の並びを持つ相手を、PMXから集める。親を持たない種類ではPMX自身。</summary>
        public static IList<object> Owners(object pmx, ElementKind kind)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// <c>kind</c> と親の指定を読み、その種類の並びを持つ相手を解く。親を持たない種類ではPMX自身
    /// ひとつが相手で、親の指定は受け取らない。
    /// </summary>
    public static class ElementScope
    {
        /// <summary>
        /// 解いた相手を渡す。<paramref name="kind"/> が親を持つ種類のときだけ、親の指定を読む。
        /// 解けなければ偽で、断る内容を渡す。
        /// </summary>
        public static bool TryTake(
            McpMethodContext context,
            object pmx,
            out ElementKind kind,
            out IList<object> owners,
            out string code,
            out string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>ここが読む項目の名前。種類の指定と、親の集合の指定。</summary>
        public static IList<string> Names
        {
            get { throw new NotImplementedException(); }
        }
    }
}
