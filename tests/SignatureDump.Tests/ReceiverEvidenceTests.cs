using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>受け手の型へ至る道。接続の根ごとに辿って決める。</summary>
    public sealed class ReceiverEvidenceTests
    {
        private const string RunArgs = "PEPlugin.IPERunArgs";

        private const string CRunArgs = "PXCPlugin.IPXCPluginRunArgs";

        private const string Bridge = "PXCPlugin.PXCBridge";

        private const string Connector = "PXCPlugin.IPXCPluginConnector";

        private const string Host = "Sdk.Host";

        private const string View = "Sdk.View";

        private const string Events = "Sdk.Events";

        private const string Sub = "Sdk.Sub";

        [Fact]
        public void ARootIsItsOwnReceiverWithNoStep()
        {
            ReceiverPath path = Resolve(RunArgs)[RunArgs];

            Assert.Equal(RunArgs, path.Root);
            Assert.Equal(string.Empty, path.Steps);
        }

        [Fact]
        public void ATypeReachedThroughPropertiesCarriesTheirNames()
        {
            ReceiverPath path = Resolve(View)[View];

            Assert.Equal(RunArgs, path.Root);
            Assert.Equal("Host.View", path.Steps);
        }

        [Fact]
        public void AStepThatTakesTheInjectedConnectorKeepsItsBrackets()
        {
            ReceiverPath path = Resolve(Events)[Events];

            Assert.Equal(Bridge, path.Root);
            Assert.Equal("CreateEventConnector()", path.Steps);
        }

        [Fact]
        public void ATypeThatNoRootReachesHasNoPath()
        {
            Assert.Empty(Resolve("Sdk.Absent"));
        }

        [Fact]
        public void TheShortestPathWins()
        {
            ReceiverPath path = Resolve(Sub)[Sub];

            Assert.Equal(CRunArgs, path.Root);
            Assert.Equal("Connector.Sub", path.Steps);
        }

        [Fact]
        public void ATypeWithNoPathOfItsOwnGoesThroughTheOneTypeThatCanBeReached()
        {
            ReceiverPath path = Through("Sdk.Absent", View)["Sdk.Absent"];

            Assert.Equal(RunArgs, path.Root);
            Assert.Equal("Host.View", path.Steps);
        }

        [Fact]
        public void ACandidateThatNoRootReachesLeavesTheTypeWithoutAPath()
        {
            Assert.Empty(Through("Sdk.Absent", "Sdk.Elsewhere"));
        }

        [Fact]
        public void TwoCandidatesThatCanBothBeReachedStop()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Through("Sdk.Absent", View, Sub));

            Assert.Contains("Sdk.Absent", error.Message);
        }

        [Fact]
        public void ACandidateThatCannotBeReachedDoesNotMakeTheChoiceAmbiguous()
        {
            ReceiverPath path = Through("Sdk.Absent", View, "Sdk.Elsewhere")["Sdk.Absent"];

            Assert.Equal("Host.View", path.Steps);
        }

        [Fact]
        public void ACandidateIsNotAnsweredUnlessItWasAsked()
        {
            Assert.Equal(new[] { "Sdk.Absent" }, Through("Sdk.Absent", View).Keys);
        }

        [Fact]
        public void ATypeThatHasItsOwnPathIgnoresTheCandidates()
        {
            ReceiverPath path = Through(Sub, View)[Sub];

            Assert.Equal(CRunArgs, path.Root);
            Assert.Equal("Connector.Sub", path.Steps);
        }

        private static IDictionary<string, ReceiverPath> Resolve(params string[] types)
        {
            return ReceiverEvidence.Resolve(Inventory(), types);
        }

        /// <summary>
        /// <paramref name="type"/> の受け手を <paramref name="candidates"/> のどれかを通して求める。
        /// </summary>
        private static IDictionary<string, ReceiverPath> Through(
            string type, params string[] candidates)
        {
            return ReceiverEvidence.Resolve(
                Inventory(),
                new[] { type },
                new Dictionary<string, ISet<string>>(StringComparer.Ordinal)
                {
                    { type, new HashSet<string>(candidates, StringComparer.Ordinal) },
                });
        }

        private static InventoryRecord Inventory()
        {
            return new InventoryRecord(
                "PEPlugin",
                "0.0.8.9",
                new TypeRecord[0],
                new TypeRecord[0],
                new[]
                {
                    Property(RunArgs, "Host", Host),
                    Property(Host, "View", View),
                    Property(View, "Sub", Sub),
                    Property(CRunArgs, "Connector", Connector),
                    Property(Connector, "Sub", Sub),
                    Property(Sub, "Depth", "System.Int32"),
                    Taking(Bridge, "CreateEventConnector", Events),
                    Property(Events, "Count", "System.Int32"),
                });
        }

        /// <summary>引数を取らない取得プロパティ。辿れる一歩になる。</summary>
        private static SignatureRecord Property(string owner, string member, string valueType)
        {
            return new SignatureRecord(
                owner + "." + member + "()",
                owner,
                MemberKind.Property,
                member,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                true,
                false,
                OperationDirection.Read);
        }

        /// <summary>自動注入コネクタだけを取るメソッド。これも辿れる一歩になる。</summary>
        private static SignatureRecord Taking(string owner, string member, string valueType)
        {
            return new SignatureRecord(
                owner + "." + member + "(" + Connector + ")",
                owner,
                MemberKind.Method,
                member,
                true,
                0,
                new[]
                {
                    new ParameterRecord("connector", Connector, ParameterDirection.In, false),
                },
                valueType,
                false,
                false,
                OperationDirection.Read);
        }
    }
}
