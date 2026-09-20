using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>消した要素を指していた口の始末の仕方。</summary>
    public enum RelatedHandling
    {
        /// <summary>何も触らない。指す先を失った口はそのまま残る。</summary>
        Keep,

        /// <summary>指す先を失った口を直す。ウェイトは残るボーンへ移し、直せない口は落とす。</summary>
        Repair,

        /// <summary>直すことに加えて、消した要素だけが使っていた要素も一緒に消す。</summary>
        Cascade,
    }

    /// <summary>
    /// 並びから消えた要素を指したままの口を片付ける。PMXの要素はIndexではなくオブジェクトの参照で
    /// 繋がるので、並びから外しただけでは他の要素が指したままになる。指す先を失った口は、その口が
    /// 空を取れるなら空にし、取れないなら口を持つ要素ごと落とす。頂点のウェイトだけは落とせない
    /// ので、残っている祖先のボーンへ移し、同じボーンが重なったら重みを足してまとめる。
    /// </summary>
    public static class ReferenceCleanup
    {
        /// <summary>始末の仕方を受け取る入力の名前。</summary>
        public const string RelatedName = "related";

        /// <summary>何も触らない。</summary>
        public const string Keep = "keep";

        /// <summary>指す先を失った口を直す。</summary>
        public const string Repair = "repair";

        /// <summary>直すことに加えて、消した要素だけが使っていた要素も消す。</summary>
        public const string Cascade = "cascade";

        /// <summary>受け取れる値。スキーマが並べる順。</summary>
        public static IList<string> Names
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>入力から始末の仕方を読む。省かれていれば <see cref="RelatedHandling.Repair"/>。</summary>
        public static bool TryResolve(object given, out RelatedHandling handling, out string message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 指した要素を消したとき、それだけが使っていたために一緒に消える要素を、種類の名前ごとに
        /// 集める。消す前のPMXを渡す。
        /// </summary>
        public static IDictionary<string, IList<object>> Following(
            object pmx, ElementKind kind, ICollection<object> removed)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// PMX全体を見て、並びに居ない要素を指したままの口を直す。直した口の数を返す。
        /// </summary>
        public static int Sweep(object pmx)
        {
            throw new NotImplementedException();
        }
    }
}
