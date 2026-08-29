using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class SignatureKeyBuilderTests
    {
        private static ParameterRecord Param(string name, string typeName, ParameterDirection direction)
        {
            return new ParameterRecord(name, typeName, direction, false);
        }

        private static string Build(
            string memberName, int genericArity, IList<ParameterRecord> parameters, string valueType)
        {
            return SignatureKeyBuilder.Build("N.IThing", memberName, genericArity, parameters, valueType);
        }

        [Fact]
        public void 引数がないメンバーは空の括弧が付く()
        {
            Assert.Equal(
                "N.IThing.Count()",
                Build("Count", 0, new List<ParameterRecord>(), "System.Int32"));
        }

        [Fact]
        public void 引数の型は宣言順にカンマで並ぶ()
        {
            string key = Build(
                "SetThing",
                0,
                new List<ParameterRecord>
                {
                    Param("index", "System.Int32", ParameterDirection.In),
                    Param("text", "System.String", ParameterDirection.In),
                },
                "System.Void");

            Assert.Equal("N.IThing.SetThing(System.Int32,System.String)", key);
        }

        [Fact]
        public void 出力引数と入出力引数は向きの語が前に付く()
        {
            string key = Build(
                "TryGet",
                0,
                new List<ParameterRecord>
                {
                    Param("index", "System.Int32", ParameterDirection.In),
                    Param("text", "System.String", ParameterDirection.Out),
                    Param("state", "System.Int32", ParameterDirection.Ref),
                },
                "System.Boolean");

            Assert.Equal("N.IThing.TryGet(System.Int32,out System.String,ref System.Int32)", key);
        }

        [Fact]
        public void 総称型引数の数が行キーに現れる()
        {
            Assert.Equal(
                "N.IThing.Apply<2>()",
                Build("Apply", 2, new List<ParameterRecord>(), "System.Void"));
        }

        [Fact]
        public void 総称型引数の数だけが違うオーバーロードは別の行キーになる()
        {
            Assert.NotEqual(
                Build("Apply", 0, new List<ParameterRecord>(), "System.Void"),
                Build("Apply", 1, new List<ParameterRecord>(), "System.Void"));
        }

        [Fact]
        public void 向きだけが違うオーバーロードは別の行キーになる()
        {
            Assert.NotEqual(
                Build(
                    "Apply",
                    0,
                    new List<ParameterRecord> { Param("v", "System.Int32", ParameterDirection.In) },
                    "System.Void"),
                Build(
                    "Apply",
                    0,
                    new List<ParameterRecord> { Param("v", "System.Int32", ParameterDirection.Ref) },
                    "System.Void"));
        }

        [Fact]
        public void 変換演算子は戻り値の型が行キーに現れる()
        {
            List<ParameterRecord> parameters =
                new List<ParameterRecord> { Param("value", "N.IThing", ParameterDirection.In) };

            Assert.Equal(
                "N.IThing.op_Implicit(N.IThing):System.Int32",
                Build("op_Implicit", 0, parameters, "System.Int32"));
            Assert.Equal(
                "N.IThing.op_Explicit(N.IThing):System.Int64",
                Build("op_Explicit", 0, parameters, "System.Int64"));
        }

        [Fact]
        public void 戻り値の型だけが違う変換演算子は別の行キーになる()
        {
            List<ParameterRecord> parameters =
                new List<ParameterRecord> { Param("value", "N.IThing", ParameterDirection.In) };

            Assert.NotEqual(
                Build("op_Implicit", 0, parameters, "System.Int32"),
                Build("op_Implicit", 0, parameters, "System.Int64"));
        }

        [Fact]
        public void 変換演算子でなければ戻り値の型は行キーに現れない()
        {
            List<ParameterRecord> parameters = new List<ParameterRecord>
            {
                Param("left", "N.IThing", ParameterDirection.In),
                Param("right", "N.IThing", ParameterDirection.In),
            };

            Assert.Equal(
                "N.IThing.op_Addition(N.IThing,N.IThing)",
                Build("op_Addition", 0, parameters, "N.IThing"));
            Assert.Equal(
                Build("GetValue", 0, new List<ParameterRecord>(), "System.Int32"),
                Build("GetValue", 0, new List<ParameterRecord>(), "System.String"));
        }

        [Fact]
        public void 引数名は行キーに影響しない()
        {
            Assert.Equal(
                Build(
                    "Apply",
                    0,
                    new List<ParameterRecord> { Param("a", "System.Int32", ParameterDirection.In) },
                    "System.Void"),
                Build(
                    "Apply",
                    0,
                    new List<ParameterRecord> { Param("b", "System.Int32", ParameterDirection.In) },
                    "System.Void"));
        }

        [Fact]
        public void 必須の引数を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(
                () => SignatureKeyBuilder.Build(null, "M", 0, new List<ParameterRecord>(), "System.Void"));
            Assert.Throws<ArgumentNullException>(
                () => SignatureKeyBuilder.Build("N.IThing", null, 0, new List<ParameterRecord>(), "System.Void"));
            Assert.Throws<ArgumentNullException>(
                () => SignatureKeyBuilder.Build("N.IThing", "M", 0, null, "System.Void"));
            Assert.Throws<ArgumentNullException>(
                () => SignatureKeyBuilder.Build(
                    "N.IThing", "op_Implicit", 0, new List<ParameterRecord>(), null));
        }

        [Fact]
        public void 総称型引数の数が負なら例外になる()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SignatureKeyBuilder.Build("N.IThing", "M", -1, new List<ParameterRecord>(), "System.Void"));
        }
    }
}
