using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public class ValueShapeGateTests
    {
        private const string EnumTypeName = "PEPlugin.Pmx.MorphKind";

        [Fact]
        public void TheTableThatFollowsTheRulePasses()
        {
            ValueShapeGate.Require(
                Rows(Row("System.Int32", "number"), Row(EnumTypeName, "enum_name"), Row("System.Nullable<1>", null)),
                Mapped("System.Int32", EnumTypeName, "System.Nullable<1>"),
                Rule());
        }

        [Fact]
        public void ATypeMissingFromTheTableStops()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => ValueShapeGate.Require(
                    Rows(Row("System.Int32", "number")),
                    Mapped("System.Int32", EnumTypeName),
                    Rule()));

            Assert.Contains(EnumTypeName, failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ATypeThatIsNotUsedStops()
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => ValueShapeGate.Require(
                    Rows(Row("System.Int32", "number"), Row("System.Boolean", "boolean")),
                    Mapped("System.Int32"),
                    Rule()));

            Assert.Contains("System.Boolean", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASpellingThatDisagreesWithTheRuleStops()
        {
            Assert.Throws<InvalidOperationException>(
                () => ValueShapeGate.Require(
                    Rows(Row("System.Int32", "text")), Mapped("System.Int32"), Rule()));
        }

        [Fact]
        public void ASpellingOnAWrappingTypeStops()
        {
            Assert.Throws<InvalidOperationException>(
                () => ValueShapeGate.Require(
                    Rows(Row("System.Nullable<1>", "number")),
                    Mapped("System.Nullable<1>"),
                    Rule()));
        }

        [Fact]
        public void AMissingSpellingOnAClassifiedTypeStops()
        {
            Assert.Throws<InvalidOperationException>(
                () => ValueShapeGate.Require(
                    Rows(Row("System.Int32", null)), Mapped("System.Int32"), Rule()));
        }

        [Fact]
        public void TheSameTypeTwiceStops()
        {
            Assert.Throws<InvalidOperationException>(
                () => ValueShapeGate.Require(
                    Rows(Row("System.Int32", "number"), Row("System.Int32", "number")),
                    Mapped("System.Int32"),
                    Rule()));
        }

        [Fact]
        public void TheInputsAreRequired()
        {
            IList<ValueShapeRow> rows = Rows(Row("System.Int32", "number"));

            Assert.Throws<ArgumentNullException>(
                () => ValueShapeGate.Require(null, Mapped("System.Int32"), Rule()));
            Assert.Throws<ArgumentNullException>(() => ValueShapeGate.Require(rows, null, Rule()));
            Assert.Throws<ArgumentNullException>(
                () => ValueShapeGate.Require(rows, Mapped("System.Int32"), null));
        }

        private static ValueShapeRow Row(string typeName, string shape)
        {
            return new ValueShapeRow(typeName, shape);
        }

        private static IList<ValueShapeRow> Rows(params ValueShapeRow[] rows)
        {
            return new List<ValueShapeRow>(rows);
        }

        private static ISet<string> Mapped(params string[] types)
        {
            return new HashSet<string>(types, StringComparer.Ordinal);
        }

        private static ValueRepresentationRule Rule()
        {
            return ValueRepresentationRule.Create(new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new List<TypeRecord> { Type(EnumTypeName) },
                new List<TypeRecord>(),
                new List<SignatureRecord>()));
        }

        private static TypeRecord Type(string name)
        {
            return new TypeRecord(
                name,
                TypeKind.Enum,
                false,
                false,
                false,
                new ReadOnlyCollection<string>(new List<string>()),
                new ReadOnlyCollection<string>(new List<string>()));
        }
    }
}
