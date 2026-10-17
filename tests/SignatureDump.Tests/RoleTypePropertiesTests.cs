using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PmxEditorMcp.SignatureDump.Tests.Sample;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class RoleTypePropertiesTests
    {
        private const string N = "PmxEditorMcp.SignatureDump.Tests.Sample.";

        [Fact]
        public void DeclaredAndInheritedReadablePropertiesAreListed()
        {
            Assert.Equal(
                new[]
                {
                    N + "ISampleApi.Item",
                    N + "ISampleApi.ReadOnlyName",
                    N + "ISampleApi.Value",
                    N + "ISampleAux.AuxValue",
                    N + "ISampleBase.BaseValue",
                    N + "ISampleRoot.RootValue",
                },
                Keys(Enumerate(typeof(ISampleApi))));
        }

        [Fact]
        public void APropertyWithoutAPublicGetterIsLeftOut()
        {
            Assert.DoesNotContain(
                N + "ISampleApi.WriteOnlyLevel", Keys(Enumerate(typeof(ISampleApi))));
        }

        [Fact]
        public void StaticAndPrivatelySetPropertiesAreListed()
        {
            IList<PropertyRecord> records = Enumerate(typeof(SampleData));

            Assert.Contains(N + "SampleData.Computed", Keys(records));
            Assert.Contains(N + "SampleData.Total", Keys(records));
        }

        [Fact]
        public void FieldsAreLeftOut()
        {
            Assert.DoesNotContain(N + "SampleData.Field", Keys(Enumerate(typeof(SampleData))));
        }

        [Fact]
        public void PropertiesInheritedFromABaseClassAreListed()
        {
            IList<string> keys = Keys(Enumerate(typeof(SampleDerived)));

            Assert.Contains(N + "SampleDerived.AuxValue", keys);
            Assert.Contains(N + "SampleBaseClass.Level", keys);
            Assert.Contains(N + "SampleBaseClass.RootValue", keys);
        }

        [Fact]
        public void AStaticPropertyInheritedFromABaseClassIsListed()
        {
            IList<PropertyRecord> records = RoleTypeProperties.Enumerate(
                new HashSet<string>(new[] { "System.Text.UTF8Encoding" }, StringComparer.Ordinal),
                new[] { typeof(UTF8Encoding) });

            Assert.Contains("System.Text.Encoding.UTF8", Keys(records));
        }

        [Fact]
        public void TheOpenDefinitionAndAClosedTypeAreSeparateItems()
        {
            IList<string> keys = Keys(RoleTypeProperties.Enumerate(
                new HashSet<string>(
                    new[] { "System.Collections.Generic.IList<1>" }, StringComparer.Ordinal),
                new[] { typeof(IList<>), typeof(IList<int>) }));

            Assert.Contains("System.Collections.Generic.IList<T>.Item", keys);
            Assert.Contains("System.Collections.Generic.IList<System.Int32>.Item", keys);
        }

        [Fact]
        public void TheSameNameWithADifferentPropertyTypeStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Enumerate(typeof(SampleIndexers)));

            Assert.Contains("Item", error.Message);
        }

        [Fact]
        public void ATypeOutsideTheRoleSetIsSkipped()
        {
            Assert.Empty(RoleTypeProperties.Enumerate(
                new HashSet<string>(new[] { N + "ISampleApi" }, StringComparer.Ordinal),
                new[] { typeof(SampleData) }));
        }

        [Fact]
        public void TheSameDeclaringTypeAndNameAppearsOnce()
        {
            IList<PropertyRecord> records = RoleTypeProperties.Enumerate(
                new HashSet<string>(
                    new[] { N + "ISampleApi", N + "ISampleBase" }, StringComparer.Ordinal),
                new[] { typeof(ISampleApi), typeof(ISampleBase) });

            Assert.Single(Keys(records), k => k == N + "ISampleRoot.RootValue");
        }

        [Fact]
        public void ThePropertyTypeIsRecorded()
        {
            PropertyRecord record = Enumerate(typeof(ISampleApi))
                .Single(r => r.MemberName == "ReadOnlyName");

            Assert.Equal(N + "ISampleApi", record.DeclaringType);
            Assert.Equal("System.String", record.PropertyType);
        }

        [Fact]
        public void AGenericTypeIsMatchedByItsDefinitionKey()
        {
            IList<PropertyRecord> records = RoleTypeProperties.Enumerate(
                new HashSet<string>(
                    new[] { "System.Collections.Generic.IList<1>" }, StringComparer.Ordinal),
                new[] { typeof(IList<int>) });

            Assert.Contains("System.Collections.Generic.IList<System.Int32>.Item", Keys(records));
            Assert.Contains("System.Collections.Generic.ICollection<System.Int32>.Count", Keys(records));
        }

        [Fact]
        public void EnumerateRequiresBothArguments()
        {
            Assert.Throws<ArgumentNullException>(
                () => RoleTypeProperties.Enumerate(null, new[] { typeof(ISampleApi) }));
            Assert.Throws<ArgumentNullException>(
                () => RoleTypeProperties.Enumerate(
                    new HashSet<string>(StringComparer.Ordinal), null));
            Assert.Throws<ArgumentException>(
                () => RoleTypeProperties.Enumerate(
                    new HashSet<string>(new[] { N + "ISampleApi" }, StringComparer.Ordinal),
                    new Type[] { null }));
        }

        [Fact]
        public void APropertyRecordRequiresAllThreeParts()
        {
            Assert.Throws<ArgumentNullException>(() => new PropertyRecord(null, "A", "System.Int32"));
            Assert.Throws<ArgumentException>(() => new PropertyRecord("N.IThing", " ", "System.Int32"));
            Assert.Throws<ArgumentException>(() => new PropertyRecord("N.IThing", "A", " "));
        }

        private sealed class SampleIndexers
        {
            public int this[int index]
            {
                get { return index; }
            }

            public string this[string key]
            {
                get { return key; }
            }
        }

        private static IList<PropertyRecord> Enumerate(Type type)
        {
            return RoleTypeProperties.Enumerate(
                new HashSet<string>(
                    new[] { TypeDefinitionName.Of(TypeNameFormatter.Format(type)) },
                    StringComparer.Ordinal),
                new[] { type });
        }

        private static IList<string> Keys(IEnumerable<PropertyRecord> records)
        {
            return records.Select(r => r.DeclaringType + "." + r.MemberName).ToList();
        }
    }
}
