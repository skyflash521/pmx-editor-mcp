using System;
using System.CodeDom.Compiler;
using System.Linq;
using Microsoft.CSharp;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    internal static class CompiledLiteral
    {
        public const string Awkward = "前\u2028後\u2029\r\n\"引用\"\\終";

        /// <summary>
        /// <paramref name="source"/> の中で <paramref name="before"/> の直後から
        /// <paramref name="after"/> の直前までをリテラルとしてコンパイルし、その値を返す。
        /// コンパイルできなければテストを落とす。
        /// </summary>
        public static string Between(string source, string before, string after)
        {
            int start = source.IndexOf(before, StringComparison.Ordinal);
            Assert.True(start >= 0, "前置きが見つからない: " + before);
            start += before.Length;
            int end = source.IndexOf(after, start, StringComparison.Ordinal);
            Assert.True(end >= 0, "後置きが見つからない: " + after);

            return ValueOf(source.Substring(start, end - start));
        }

        private static string ValueOf(string literal)
        {
            using (CSharpCodeProvider compiler = new CSharpCodeProvider())
            {
                CompilerResults built = compiler.CompileAssemblyFromSource(
                    new CompilerParameters { GenerateInMemory = true },
                    "public static class Holder { public const string Value = " + literal + "; }");
                Assert.False(
                    built.Errors.HasErrors,
                    "リテラルをコンパイルできない: "
                        + string.Join(" / ", built.Errors.Cast<CompilerError>().Select(e => e.ErrorText)));

                return (string)built.CompiledAssembly.GetType("Holder").GetField("Value").GetValue(null);
            }
        }
    }
}
