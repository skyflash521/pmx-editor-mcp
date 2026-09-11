using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>メンバー参照を見た結果。</summary>
    public sealed class ReflectionScan
    {
        public ReflectionScan(IList<string> found, IList<int> unreadable)
        {
            Found = found;
            Unreadable = unreadable;
        }

        /// <summary>名前で型やメンバーを引く経路を持つ参照。識別子の序数昇順。</summary>
        public IList<string> Found { get; }

        /// <summary>綴りを取れなかったメンバー参照の行。</summary>
        public IList<int> Unreadable { get; }
    }

    /// <summary>
    /// 配布物が実行時リフレクションを持たないことを見る。見るのはアセンブリが参照している
    /// メンバーで、名前で型やメンバーを引く経路が1つでもあれば落とす。
    /// </summary>
    public static class ReflectionFreeGate
    {
        /// <summary>総称の変数へ器を当てるときに試す数の上限。</summary>
        private const int MaxGenericArity = 16;

        /// <summary>
        /// 名前で引く経路を作る型。ここに載る型のメンバーは、<see cref="Allowed"/> に挙げたものを
        /// 除いてすべて落とす——名前で引くメンバーを1つずつ数え上げる形では、数え落としたものが
        /// そのまま抜け道になる。
        /// </summary>
        private static readonly ReadOnlyCollection<string> GatedTypes =
            Array.AsReadOnly(new[]
            {
                "System.Type",
                "System.Activator",
                "System.AppDomain",
                "System.Delegate",
                "System.MulticastDelegate",
            });

        /// <summary>名前で引く経路を作る型の綴りの頭。扱いは <see cref="GatedTypes"/> と同じ。</summary>
        private static readonly ReadOnlyCollection<string> GatedPrefixes =
            Array.AsReadOnly(new[]
            {
                "System.Reflection.",
                "System.Linq.Expressions.",
                "System.CodeDom.",
                "System.Runtime.CompilerServices.CallSite",
                "Microsoft.CSharp.RuntimeBinder.",
            });

        /// <summary>
        /// 名前で引く経路を作るメンバー。上の型と綴りの頭の外で宣言されていても落とすものを並べる。
        /// </summary>
        private static readonly ReadOnlyCollection<string> ForbiddenMembers =
            Array.AsReadOnly(new[]
            {
                "GetType",
                "GetMethod",
                "GetMethods",
                "GetProperty",
                "GetProperties",
                "GetField",
                "GetFields",
                "GetMember",
                "GetMembers",
                "GetEvent",
                "GetEvents",
                "GetConstructor",
                "GetConstructors",
                "InvokeMember",
            });

        /// <summary>
        /// 上の型と綴りの頭に当たるもののうち、名前で型やメンバーを引かないので通すもの。型そのものの
        /// 形を見るだけの経路と、コンパイラが言語の機能のために置く経路である。
        /// </summary>
        private static readonly ReadOnlyCollection<string> Allowed =
            Array.AsReadOnly(new[]
            {
                "System.Delegate.Combine",
                "System.Delegate.Remove",
                "System.Reflection.Assembly.GetName",
                "System.Reflection.AssemblyName.get_Version",
                "System.Type.GetArrayRank",
                "System.Type.GetElementType",
                "System.Type.GetGenericArguments",
                "System.Type.GetGenericTypeDefinition",
                "System.Type.GetTypeFromHandle",
                "System.Type.get_Assembly",
                "System.Type.get_FullName",
                "System.Type.get_IsArray",
                "System.Type.get_IsByRef",
                "System.Type.get_IsEnum",
                "System.Type.get_IsGenericType",
                "System.Type.get_IsValueType",
                "System.Type.op_Equality",
                "System.Type.op_Inequality",
            });

        /// <summary>
        /// 参照の綴りのうち、名前で引く経路を持つものを識別子の序数昇順で返す。綴りは宣言型と
        /// メンバー名を点でつないだもの。
        /// </summary>
        public static IList<string> Find(IEnumerable<string> references)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            SortedSet<string> found = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string reference in references)
            {
                if (IsForbidden(reference))
                {
                    found.Add(reference);
                }
            }

            return found.ToList();
        }

        /// <summary>
        /// メタデータのメンバー参照の表を1行ずつ引く。行が尽きたところで終わる。綴りを取れない行は
        /// 判じずに数える——判じられなかったものを通せば、この検査が保証するのは参照の一部だけになる。
        /// 見るのはメタデータが持つ外部メンバーの参照で、本体の命令を読み解かない——命令の長さを
        /// 解さずに読むと、引数の並びを命令と取り違える。
        /// </summary>
        public static ReflectionScan Scan(Module module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            const int MemberRefTable = 0x0A000000;

            SortedSet<string> found = new SortedSet<string>(StringComparer.Ordinal);
            List<int> unreadable = new List<int>();

            for (int row = 1; ; row++)
            {
                MemberInfo member;
                try
                {
                    member = module.ResolveMember(MemberRefTable | row);
                }
                catch (ArgumentOutOfRangeException)
                {
                    break;
                }
                catch (ArgumentException)
                {
                    // 総称の変数を含む綴りは、その変数に何を当てるかを添えないと引けない。何を
                    // 当てても宣言型とメンバーの名前は変わらないので、変数の数だけ器を渡す。
                    member = TryResolveWithContext(module, MemberRefTable | row);
                    if (member == null)
                    {
                        unreadable.Add(row);
                        continue;
                    }
                }

                string reference = member.DeclaringType == null
                    ? member.Name
                    : member.DeclaringType.FullName + "." + member.Name;

                if (IsForbidden(reference) && !IsAttributeConstructor(member))
                {
                    found.Add(reference);
                }
            }

            return new ReflectionScan(found.ToList(), unreadable);
        }

        /// <summary>
        /// 総称の変数へ器を当てて引き直す。引けなければ null。当てる数は、綴りが使いうる変数の数を
        /// 超えるところまで増やす。
        /// </summary>
        private static MemberInfo TryResolveWithContext(Module module, int token)
        {
            for (int arity = 1; arity <= MaxGenericArity; arity++)
            {
                Type[] context = Enumerable.Repeat(typeof(object), arity).ToArray();
                try
                {
                    return module.ResolveMember(token, context, context);
                }
                catch (ArgumentException)
                {
                }
            }

            return null;
        }

        private static bool IsForbidden(string reference)
        {
            string declaringType;
            string member;
            Split(reference, out declaringType, out member);

            if (ForbiddenMembers.Contains(member, StringComparer.Ordinal))
            {
                return true;
            }

            return IsGated(declaringType) && !Allowed.Contains(reference, StringComparer.Ordinal);
        }

        /// <summary>
        /// 属性を組み立てる呼び出しかどうか。属性は組み立てられた先で何も引かないので、上の型と
        /// 綴りの頭に当たっても通す——ビルドが置く属性は増えうるので、1つずつ挙げない。
        /// 綴りではなく継いだ先で判じる——綴りで判じると、属性でない型が同じ綴りを名乗れる。
        /// </summary>
        public static bool IsAttributeConstructor(MemberInfo member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            return member is ConstructorInfo
                && member.DeclaringType != null
                && typeof(Attribute).IsAssignableFrom(member.DeclaringType);
        }

        /// <summary>綴りを宣言型とメンバー名へ分ける。コンストラクタの名前は点で始まる。</summary>
        private static void Split(string reference, out string declaringType, out string member)
        {
            int separator = reference.LastIndexOf('.');
            if (separator > 0 && reference[separator - 1] == '.')
            {
                separator--;
            }

            declaringType = separator < 0 ? string.Empty : reference.Substring(0, separator);
            member = separator < 0 ? reference : reference.Substring(separator + 1);
        }

        private static bool IsGated(string declaringType)
        {
            return GatedTypes.Contains(declaringType, StringComparer.Ordinal)
                || GatedPrefixes.Any(p => declaringType.StartsWith(p, StringComparison.Ordinal));
        }
    }
}
