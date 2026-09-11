using System.Linq;
using System.Reflection;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 配布物が名前で型やメンバーを引いていないことの判じ方。名前で引ける型のメンバーは挙げた
    /// ものだけを通し、それ以外の型のメンバーは名前で引くものだけを落とす。
    /// </summary>
    public sealed class ReflectionFreeGateTests
    {
        [Theory]
        [InlineData("System.Type.GetMethod")]
        [InlineData("System.Type.GetProperty")]
        [InlineData("System.Type.GetField")]
        [InlineData("System.Object.GetType")]
        [InlineData("System.Reflection.PropertyInfo.GetValue")]
        [InlineData("System.Reflection.FieldInfo.SetValue")]
        [InlineData("System.Reflection.MethodBase.Invoke")]
        [InlineData("System.Reflection.MemberInfo.IsDefined")]
        [InlineData("System.Reflection.Assembly.Load")]
        [InlineData("System.Activator.CreateInstance")]
        [InlineData("System.AppDomain.Load")]
        public void APathThatLooksAMemberUpByNameIsCaught(string reference)
        {
            Assert.Equal(new[] { reference }, ReflectionFreeGate.Find(new[] { reference }));
        }

        /// <summary>
        /// 名前で引ける型のメンバーは、挙げていなければ落とす。1つずつ数え上げる形では、数え
        /// 落としたものが抜け道になる。
        /// </summary>
        [Theory]
        [InlineData("System.Type.GetInterface")]
        [InlineData("System.Type.GetNestedType")]
        [InlineData("System.Type.InvokeMember")]
        [InlineData("System.Delegate.CreateDelegate")]
        [InlineData("System.Reflection.TypeInfo.GetDeclaredMethod")]
        [InlineData("System.Linq.Expressions.Expression.Call")]
        [InlineData("System.CodeDom.Compiler.CodeDomProvider.CompileAssemblyFromSource")]
        [InlineData("Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember")]
        [InlineData("System.Runtime.CompilerServices.CallSite`1.Create")]
        public void AMemberOfATypeThatCanLookUpByNameIsCaughtUnlessItIsListed(string reference)
        {
            Assert.Equal(new[] { reference }, ReflectionFreeGate.Find(new[] { reference }));
        }

        [Theory]
        [InlineData("System.Array.CreateInstance")]
        [InlineData("System.Enum.GetNames")]
        [InlineData("System.Object.ToString")]
        [InlineData("PEPlugin.Pmx.IPXPmxConnector.LockUndo")]
        public void APathOnATypeThatCannotLookUpByNameIsLeftAlone(string reference)
        {
            Assert.Empty(ReflectionFreeGate.Find(new[] { reference }));
        }

        [Theory]
        [InlineData("System.Type.get_FullName")]
        [InlineData("System.Type.get_IsEnum")]
        [InlineData("System.Type.GetElementType")]
        [InlineData("System.Type.GetArrayRank")]
        [InlineData("System.Type.GetGenericArguments")]
        [InlineData("System.Type.get_Assembly")]
        [InlineData("System.Reflection.Assembly.GetName")]
        [InlineData("System.Delegate.Combine")]
        public void AListedPathThatOnlyLooksAtTheShapeOfATypeIsAllowed(string reference)
        {
            Assert.Empty(ReflectionFreeGate.Find(new[] { reference }));
        }

        /// <summary>属性は組み立てられた先で何も引かないので、ビルドが置いたぶんも通す。</summary>
        [Fact]
        public void BuildingAnAttributeIsAllowed()
        {
            Assert.True(ReflectionFreeGate.IsAttributeConstructor(
                typeof(AssemblyTitleAttribute).GetConstructors().First()));
        }

        /// <summary>
        /// 綴りが Attribute で終わるだけの型は属性ではない。綴りで判じると、名前で引ける型が同じ
        /// 綴りを名乗るだけで通ってしまう。
        /// </summary>
        [Fact]
        public void BuildingSomethingThatOnlySpellsLikeAnAttributeIsNotAllowed()
        {
            Assert.False(ReflectionFreeGate.IsAttributeConstructor(
                typeof(NotAnAttribute).GetConstructors().First()));
        }

        [Fact]
        public void AMemberThatIsNotAConstructorIsNotTakenForBuildingAnAttribute()
        {
            Assert.False(ReflectionFreeGate.IsAttributeConstructor(
                typeof(AssemblyTitleAttribute).GetProperty(nameof(AssemblyTitleAttribute.Title))));
        }

        [Fact]
        public void TheSamePathSeenTwiceIsReportedOnceAndTheOrderIsByIdentifier()
        {
            Assert.Equal(
                new[] { "System.Activator.CreateInstance", "System.Type.GetMethod" },
                ReflectionFreeGate.Find(new[]
                {
                    "System.Type.GetMethod",
                    "System.Activator.CreateInstance",
                    "System.Type.GetMethod",
                }));
        }

        [Fact]
        public void AReferenceWithoutADeclaringTypeIsJudgedByItsMemberName()
        {
            Assert.Equal(new[] { "GetMethod" }, ReflectionFreeGate.Find(new[] { "GetMethod", "Invoke" }));
        }

        /// <summary>綴りだけが属性に似ている型。継いだ先で判じていることを見るための題材。</summary>
        private sealed class NotAnAttribute
        {
        }
    }
}
