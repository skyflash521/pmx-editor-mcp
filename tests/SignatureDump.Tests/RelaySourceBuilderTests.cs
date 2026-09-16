using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>
    /// 行キーからSDKのメンバーを直接呼ぶ本文の組み立て。解決できない行は中継を作らず、その行だけを
    /// 未解決として並べる。
    /// </summary>
    public sealed class RelaySourceBuilderTests
    {
        private const string SdkVersion = "0.0.8.9";

        private const string Digest = "8f14e45fceea167a5a36dedd4bea2543";

        private static readonly IDictionary<string, ReceiverPath> NoReceivers =
            new Dictionary<string, ReceiverPath>(StringComparer.Ordinal);

        [Fact]
        public void AMethodIsCalledOnTheReceiverCastToItsDeclaringType()
        {
            RelaySource source = Build("PEPlugin.Pmx.IPXPmxConnector.LockUndo()");

            Assert.Equal(new[] { "PEPlugin.Pmx.IPXPmxConnector.LockUndo()" }, source.Resolved);
            Assert.Empty(source.Unresolved);
            Assert.Contains(
                "Call(() => ((global::PEPlugin.Pmx.IPXPmxConnector)target).LockUndo())", source.Text);
        }

        [Fact]
        public void AStaticMethodIsCalledOnItsDeclaringTypeWithTheArgumentsInOrder()
        {
            RelaySource source = Build("Sdk.Helper.Join(System.String,System.Int32)");

            Assert.Contains(
                "global::Sdk.Helper.Join((global::System.String)arguments[0], (global::System.Int32)arguments[1])",
                source.Text);
        }

        [Fact]
        public void ANestedTypeIsSpelledWithDotsSoTheCompilerTakesIt()
        {
            RelaySource source = Build("Sdk.Outer+Inner.Close()");

            Assert.Contains("((global::Sdk.Outer.Inner)target).Close()", source.Text);
        }

        [Fact]
        public void APropertyThatCanBeBothReadAndWrittenBranchesOnTheArgumentCount()
        {
            RelaySource source = Build("Sdk.Type.Name");

            Assert.Contains(
                "arguments.Length == 0 ? (object)((global::Sdk.Type)target).Name"
                    + " : Call(() => ((global::Sdk.Type)target).Name = (global::System.String)arguments[0])",
                source.Text);
        }

        [Fact]
        public void APropertyThatCanOnlyBeReadHasNoWritingBranch()
        {
            RelaySource source = Build(Property("Sdk.Type.Count", "System.Int32", true, false));

            Assert.Contains("calls.Add(\"Sdk.Type.Count\", (target, arguments) =>"
                + " ((global::Sdk.Type)target).Count);", source.Text);
        }

        [Fact]
        public void ARowWhoseMemberTheSdkDoesNotCarryIsLeftUnresolvedAndTheOthersAreStillBuilt()
        {
            RelaySource source = RelaySourceBuilder.Build(
                new[] { "Sdk.Type.Absent()", "PEPlugin.Pmx.IPXPmxConnector.LockUndo()" },
                Inventory(Method("PEPlugin.Pmx.IPXPmxConnector.LockUndo()", "System.Void")),
                SdkVersion,
                new string[0],
                Digest,
                NoReceivers);

            Assert.Equal(new[] { "PEPlugin.Pmx.IPXPmxConnector.LockUndo()" }, source.Resolved);
            Assert.Equal(new[] { "Sdk.Type.Absent()" }, source.Unresolved);
            Assert.Contains("\"Sdk.Type.Absent()\",", source.Text);
            Assert.Contains("((global::PEPlugin.Pmx.IPXPmxConnector)target).LockUndo()", source.Text);
        }

        [Fact]
        public void AMemberThatThisGenerationCannotSpellIsLeftUnresolved()
        {
            Assert.Equal(
                new[] { "Sdk.Type.Take(System.Int32)" },
                Build(new SignatureRecord(
                    "Sdk.Type.Take(System.Int32)",
                    "Sdk.Type",
                    MemberKind.Method,
                    "Take",
                    false,
                    0,
                    new[] { new ParameterRecord("value", "System.Int32", ParameterDirection.Ref, false) },
                    "System.Void",
                    false,
                    false,
                    OperationDirection.Write)).Unresolved);
        }

        /// <summary>
        /// イベントのメンバーは呼ぶ相手ではなく、起きたことが溜め場へ入る先である。中継を作れない
        /// のは当たり前なので、作れなかった行として数えない。
        /// </summary>
        [Fact]
        public void AnEventMemberIsNeitherBuiltNorLeftUnresolved()
        {
            RelaySource source = Build(new SignatureRecord(
                "Sdk.Listener.MouseClick()",
                "Sdk.Listener",
                MemberKind.Event,
                "MouseClick",
                false,
                0,
                new ParameterRecord[0],
                "System.EventHandler",
                false,
                false,
                OperationDirection.Read));

            Assert.Empty(source.Unresolved);
            Assert.Empty(source.Resolved);
            Assert.Equal(new[] { "Sdk.Listener.MouseClick()" }, source.Notified);
        }

        [Fact]
        public void AMethodWithOutputArgumentsTakesThemIntoLocalsAndReturnsThem()
        {
            RelaySource source = Build(new SignatureRecord(
                "Sdk.Type.Split(System.Int32,out System.String,out System.String)",
                "Sdk.Type",
                MemberKind.Method,
                "Split",
                false,
                0,
                new[]
                {
                    new ParameterRecord("at", "System.Int32", ParameterDirection.In, false),
                    new ParameterRecord("left", "System.String", ParameterDirection.Out, false),
                    new ParameterRecord("right", "System.String", ParameterDirection.Out, false),
                },
                "System.Void",
                false,
                false,
                OperationDirection.Write));

            Assert.Empty(source.Unresolved);
            Assert.Contains("global::System.String left;", source.Text);
            Assert.Contains(
                "((global::Sdk.Type)target).Split((global::System.Int32)arguments[0], out left,"
                    + " out right);",
                source.Text);
            Assert.Contains("return new object[] { left, right };", source.Text);
        }

        [Fact]
        public void AMethodThatReturnsAValueAndAlsoWritesOutputArgumentsIsLeftUnresolved()
        {
            Assert.Equal(
                new[] { "Sdk.Type.Take(out System.Int32)" },
                Build(new SignatureRecord(
                    "Sdk.Type.Take(out System.Int32)",
                    "Sdk.Type",
                    MemberKind.Method,
                    "Take",
                    false,
                    0,
                    new[]
                    {
                        new ParameterRecord("value", "System.Int32", ParameterDirection.Out, false),
                    },
                    "System.Boolean",
                    false,
                    false,
                    OperationDirection.Write)).Unresolved);
        }

        [Fact]
        public void TheVersionUsedForGenerationIsCarriedInTheText()
        {
            Assert.Contains("internal const string SdkVersion = \"0.0.8.9\";", Build("Sdk.Type.Name").Text);
        }

        [Fact]
        public void TheDigestOfTheTableIsCarriedInTheText()
        {
            Assert.Contains(
                "internal const string ToolMapDigest = \"" + Digest + "\";",
                Build("Sdk.Type.Name").Text);
        }

        [Fact]
        public void TheEnumsThatTakeNamesSpelledOutAreCarriedInTheText()
        {
            RelaySource source = RelaySourceBuilder.Build(
                new string[0],
                Inventory(),
                SdkVersion,
                new[] { "Sdk.Second", "Sdk.First", "Sdk.First" },
                Digest,
                NoReceivers);

            int first = source.Text.IndexOf("\"Sdk.First\",", StringComparison.Ordinal);
            int second = source.Text.IndexOf("\"Sdk.Second\",", StringComparison.Ordinal);

            Assert.True(first > 0 && first < second, "列挙の綴りが序数昇順で1度ずつ並んでいない。");
        }

        [Fact]
        public void TheSameRowGivenTwiceIsBuiltOnce()
        {
            RelaySource source = RelaySourceBuilder.Build(
                new[] { "Sdk.Type.Name", "Sdk.Type.Name" },
                Inventory(Property("Sdk.Type.Name", "System.String", true, true)),
                SdkVersion,
                new string[0],
                Digest,
                NoReceivers);

            Assert.Equal(new[] { "Sdk.Type.Name" }, source.Resolved);
            Assert.Equal(1, Occurrences(source.Text, "calls.Add(\"Sdk.Type.Name\""));
        }

        private static RelaySource Build(string rowKey)
        {
            return Build(Known().Single(s => string.Equals(s.Key, rowKey, StringComparison.Ordinal)));
        }

        [Fact]
        public void AReceiverRootedAtTheStartupArgumentsComesFromTheResidentConnection()
        {
            Assert.Contains(
                "receivers.Add(\"Sdk.View\", connection => connection.RunArgs.Host.View);",
                Receivers("Sdk.View", "PEPlugin.IPERunArgs", "Host.View"));
        }

        [Fact]
        public void AReceiverRootedAtTheCPluginArgumentsAsksTheResidentConnectionForThem()
        {
            Assert.Contains(
                "receivers.Add(\"Sdk.View\", connection => connection.UseRunArgs().Connector);",
                Receivers("Sdk.View", "PXCPlugin.IPXCPluginRunArgs", "Connector"));
        }

        [Fact]
        public void AStepThatTakesTheInjectedConnectorIsGivenTheHeldOne()
        {
            Assert.Contains(
                "receivers.Add(\"Sdk.Events\", connection =>"
                    + " global::PXCPlugin.PXCBridge.CreateEventConnector(connection.Use()));",
                Receivers("Sdk.Events", "PXCPlugin.PXCBridge", "CreateEventConnector()"));
        }

        [Fact]
        public void ARootTheResidentConnectionCannotGiveStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Receivers("Sdk.View", "Sdk.Root", string.Empty));

            Assert.StartsWith("受け手を得られない接続の根:", error.Message, StringComparison.Ordinal);
        }

        private static string Receivers(string type, string root, string steps)
        {
            return RelaySourceBuilder.Build(
                new string[0],
                Inventory(),
                SdkVersion,
                new string[0],
                Digest,
                new Dictionary<string, ReceiverPath>(StringComparer.Ordinal)
                {
                    { type, new ReceiverPath(root, steps) },
                }).Text;
        }

        private static RelaySource Build(SignatureRecord signature)
        {
            return RelaySourceBuilder.Build(
                new[] { signature.Key },
                Inventory(signature),
                SdkVersion,
                new string[0],
                Digest,
                NoReceivers);
        }

        /// <summary>題材のシグネチャ。呼び出しの形が分かれる並びを1つずつ持つ。</summary>
        private static IList<SignatureRecord> Known()
        {
            return new[]
            {
                Method("PEPlugin.Pmx.IPXPmxConnector.LockUndo()", "System.Void"),
                new SignatureRecord(
                    "Sdk.Helper.Join(System.String,System.Int32)",
                    "Sdk.Helper",
                    MemberKind.Method,
                    "Join",
                    true,
                    0,
                    new[]
                    {
                        new ParameterRecord("text", "System.String", ParameterDirection.In, false),
                        new ParameterRecord("count", "System.Int32", ParameterDirection.In, false),
                    },
                    "System.String",
                    false,
                    false,
                    OperationDirection.Read),
                Method("Sdk.Outer+Inner.Close()", "System.Void"),
                Property("Sdk.Type.Name", "System.String", true, true),
            };
        }

        private static SignatureRecord Method(string key, string valueType)
        {
            int open = key.IndexOf('(');
            string head = key.Substring(0, open);
            int dot = head.LastIndexOf('.');

            return new SignatureRecord(
                key,
                head.Substring(0, dot),
                MemberKind.Method,
                head.Substring(dot + 1),
                false,
                0,
                new ParameterRecord[0],
                valueType,
                false,
                false,
                OperationDirection.Write);
        }

        private static SignatureRecord Property(string key, string valueType, bool canRead, bool canWrite)
        {
            int dot = key.LastIndexOf('.');

            return new SignatureRecord(
                key,
                key.Substring(0, dot),
                MemberKind.Property,
                key.Substring(dot + 1),
                false,
                0,
                new ParameterRecord[0],
                valueType,
                canRead,
                canWrite,
                canWrite ? OperationDirection.Write : OperationDirection.Read);
        }

        private static InventoryRecord Inventory(params SignatureRecord[] signatures)
        {
            return new InventoryRecord(
                "PEPlugin",
                SdkVersion,
                new TypeRecord[0],
                new TypeRecord[0],
                signatures.Length == 0 ? Known() : signatures);
        }

        private static int Occurrences(string text, string part)
        {
            int count = 0;
            for (int at = text.IndexOf(part, StringComparison.Ordinal);
                at >= 0;
                at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }
    }
}
