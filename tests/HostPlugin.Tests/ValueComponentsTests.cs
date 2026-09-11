using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PEPlugin.Pmd;
using PEPlugin.SDX;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// 成分を並べる型は、読みと組み立てで同じ並びを使う。型ごとに書き下ろす並びなので、どの型でも
    /// 組み立てたものがそのまま読み返せることを見る。
    /// </summary>
    public sealed class ValueComponentsTests
    {
        private const int MaxLongSide = ImageTransfer.DefaultMaxLongSide;

        public static IEnumerable<object[]> ComponentTypes()
        {
            yield return new object[] { typeof(V2), 2 };
            yield return new object[] { typeof(V3), 3 };
            yield return new object[] { typeof(V4), 4 };
            yield return new object[] { typeof(Q), 4 };
            yield return new object[] { typeof(M), 16 };
            yield return new object[] { typeof(IPEVector3), 3 };
            yield return new object[] { typeof(IPEQuaternion), 4 };
            yield return new object[] { typeof(SlimDX.Vector3), 3 };
            yield return new object[] { typeof(SlimDX.Quaternion), 4 };
            yield return new object[] { typeof(SlimDX.Matrix), 16 };
        }

        [Theory]
        [MemberData(nameof(ComponentTypes))]
        public void WhatWasBuiltFromTheComponentsIsReadBackInTheSameOrder(Type declared, int count)
        {
            object[] items = Enumerable.Range(1, count).Select(n => (object)(double)n).ToArray();

            object value;
            string code;
            string message;
            Assert.True(
                ValueInput.TryFromJson(declared, items, out value, out code, out message),
                declared.FullName + ": " + message);

            object json;
            IList<string> warnings;
            Assert.True(
                ValueShape.TryToJson(declared, value, MaxLongSide, out json, out warnings, out code, out message),
                declared.FullName + ": " + message);

            Assert.Equal(
                Enumerable.Range(1, count).Select(n => (object)(float)n).ToArray(), json);
        }

        [Theory]
        [MemberData(nameof(ComponentTypes))]
        public void TheNamesOfTheComponentsAreAsManyAsTheComponents(Type declared, int count)
        {
            IList<string> names;
            Assert.True(ValueShape.TryComponentNames(declared, out names));
            Assert.Equal(count, names.Count);
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        }

        /// <summary>
        /// 成分名と読み書きの並びは別々に書き下ろすので、ずれても往復では気づけない。名前が指す
        /// メンバーを引いて、その位置の数と合うことまで見る。名前で引くのはこの検査だけで、
        /// 配布物はしない。
        /// </summary>
        [Theory]
        [MemberData(nameof(ComponentTypes))]
        public void EachNameCarriesTheNumberAtItsOwnPosition(Type declared, int count)
        {
            object[] items = Enumerable.Range(1, count).Select(n => (object)(double)n).ToArray();

            object value;
            string code;
            string message;
            Assert.True(
                ValueInput.TryFromJson(declared, items, out value, out code, out message),
                declared.FullName + ": " + message);

            IList<string> names;
            Assert.True(ValueShape.TryComponentNames(declared, out names));

            for (int i = 0; i < count; i++)
            {
                Assert.Equal((float)(i + 1), MemberValue(value, names[i]));
            }
        }

        private static object MemberValue(object value, string name)
        {
            Type target = value.GetType();
            PropertyInfo property = target.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(value, null);
            }

            FieldInfo field = target.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.True(field != null, target.FullName + " が成分 " + name + " を持たない。");

            return field.GetValue(value);
        }

        [Fact]
        public void ATypeThatDoesNotLineUpComponentsHasNoNames()
        {
            IList<string> names;
            Assert.False(ValueShape.TryComponentNames(typeof(string), out names));
            Assert.Null(names);
        }
    }
}
