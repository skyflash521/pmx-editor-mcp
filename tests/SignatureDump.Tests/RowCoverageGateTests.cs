using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>能力対応表の行が実機の検査に覆われることの照合。</summary>
    public sealed class RowCoverageGateTests
    {
        private const string Bone = "PEPlugin.Pmx.IPXBone";

        private const string Body = "PEPlugin.Pmx.IPXBody";

        private const string Args = "PXCPlugin.Event.PXEventArgs+ViewObjectSelected";

        private const string Wipe = Bone + ".Wipe()";

        private const string Reference = Body + ".Bone()";

        private const string Selected = Args + ".Bone()";

        [Fact]
        public void ARowIsCoveredByTheToolItNames()
        {
            Require(Row(Wipe), Signature(Wipe, Bone, "Wipe", "System.Void", MemberKind.Method),
                named: "model_wipe_bone", examined: "model_wipe_bone");
        }

        [Fact]
        public void ARowWithoutAnyCaseForItsToolIsNotCovered()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Row(Wipe),
                    Signature(Wipe, Bone, "Wipe", "System.Void", MemberKind.Method),
                    named: "model_wipe_bone",
                    examined: "model_list_bones"));

            Assert.Contains(Wipe, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ACaseThatDoesNotNameTheRowDoesNotCoverAToolOnlyThatRowNames()
        {
            SignatureRecord signature =
                Signature(Wipe, Bone, "Wipe", "System.Void", MemberKind.Method);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RowCoverageGate.Require(
                    Row(Wipe),
                    Signatures(signature),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { signature.Key, "model_wipe_bone" },
                    },
                    new Dictionary<string, ComposedTool>(StringComparer.Ordinal),
                    CommonAssignmentJsonReader.Read(@"{ ""assignments"": [] }"),
                    Roles(),
                    new HashSet<string>(StringComparer.Ordinal),
                    new[] { Case("model_wipe_bone") }));

            Assert.Contains(Wipe, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ACaseThatOnlyLooksForSomethingToCallDoesNotCoverTheRow()
        {
            SignatureRecord signature =
                Signature(Wipe, Bone, "Wipe", "System.Void", MemberKind.Method);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => RowCoverageGate.Require(
                    Row(Wipe),
                    Signatures(signature),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { signature.Key, "model_wipe_bone" },
                    },
                    new Dictionary<string, ComposedTool>(StringComparer.Ordinal),
                    CommonAssignmentJsonReader.Read(@"{ ""assignments"": [] }"),
                    Roles(),
                    new HashSet<string>(StringComparer.Ordinal),
                    new[] { Dispatched("model_wipe_bone") }));

            Assert.Contains(Wipe, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowIsCoveredByTheToolItIsEmbeddedIn()
        {
            Require(
                Row(Reference, "model_list_bodies"),
                Signature(Reference, Body, "Bone", Bone),
                examined: "model_list_bodies");
        }

        [Fact]
        public void ARowOnTheWayToAnInstanceIsCoveredByThatInstancesTool()
        {
            Require(
                Row(Reference),
                Signature(Reference, Body, "Bone", Bone),
                examined: "model_list_bones",
                traversed: true);
        }

        [Fact]
        public void ARowThatOnlyPointsAtAnInstanceIsNotCoveredByThatInstancesTool()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => Require(
                    Row(Reference),
                    Signature(Reference, Body, "Bone", Bone),
                    examined: "model_list_bones"));

            Assert.Contains(Reference, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnEventArgumentIsCoveredByTheToolThatTakesEventsOut()
        {
            Require(
                Row(Selected, "view_object_selected"),
                Signature(Selected, Args, "Bone", Bone),
                examined: "view_poll_events");
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            ToolMap map = Row(Wipe);
            IDictionary<string, SignatureRecord> signatures = Signatures(
                Signature(Wipe, Bone, "Wipe", "System.Void", MemberKind.Method));
            IDictionary<string, string> named =
                new Dictionary<string, string>(StringComparer.Ordinal);
            IDictionary<string, ComposedTool> composed =
                new Dictionary<string, ComposedTool>(StringComparer.Ordinal);
            CommonAssignmentTable assignments =
                CommonAssignmentJsonReader.Read(@"{ ""assignments"": [] }");
            TypeRoleTable roles = Roles();
            ISet<string> traversed = new HashSet<string>(StringComparer.Ordinal);
            E2eCase[] cases = new E2eCase[0];

            Assert.Throws<ArgumentNullException>(
                () => RowCoverageGate.Require(
                    null, signatures, named, composed, assignments, roles, traversed, cases));
            Assert.Throws<ArgumentNullException>(
                () => RowCoverageGate.Require(
                    map, null, named, composed, assignments, roles, traversed, cases));
            Assert.Throws<ArgumentNullException>(
                () => RowCoverageGate.Require(
                    map, signatures, null, composed, assignments, roles, traversed, cases));
            Assert.Throws<ArgumentNullException>(
                () => RowCoverageGate.Require(
                    map, signatures, named, null, assignments, roles, traversed, cases));
            Assert.Throws<ArgumentNullException>(
                () => RowCoverageGate.Require(
                    map, signatures, named, composed, null, roles, traversed, cases));
            Assert.Throws<ArgumentNullException>(
                () => RowCoverageGate.Require(
                    map, signatures, named, composed, assignments, null, traversed, cases));
            Assert.Throws<ArgumentNullException>(
                () => RowCoverageGate.Require(
                    map, signatures, named, composed, assignments, roles, null, cases));
            Assert.Throws<ArgumentNullException>(
                () => RowCoverageGate.Require(
                    map, signatures, named, composed, assignments, roles, traversed, null));
        }

        /// <summary>行1件の表を、ツールの名前と検査の在り処を差し替えて照合する。</summary>
        private static void Require(
            ToolMap map,
            SignatureRecord signature,
            string named = null,
            string examined = null,
            bool traversed = false)
        {
            Dictionary<string, string> tools =
                new Dictionary<string, string>(StringComparer.Ordinal);
            if (named != null)
            {
                tools.Add(signature.Key, named);
            }

            RowCoverageGate.Require(
                map,
                Signatures(signature),
                tools,
                new Dictionary<string, ComposedTool>(StringComparer.Ordinal)
                {
                    { "view_poll_events", new ComposedTool(true, "溜まったイベントを取り出す") },
                },
                CommonAssignmentJsonReader.Read(@"{ ""assignments"": [] }"),
                Roles(),
                traversed
                    ? new HashSet<string>(new[] { signature.Key }, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
                new[]
                {
                    Case(
                        examined ?? "model_list_bones",
                        string.Equals(examined, named, StringComparison.Ordinal)
                            ? signature.Key
                            : string.Empty),
                });
        }

        private static IDictionary<string, SignatureRecord> Signatures(SignatureRecord signature)
        {
            return new Dictionary<string, SignatureRecord>(StringComparer.Ordinal)
            {
                { signature.Key, signature },
            };
        }

        private static ToolMap Row(string key, params string[] embeddedIn)
        {
            return new ToolMap(new[]
            {
                new ToolMapRow(
                    key,
                    ToolMapEditKind.Read,
                    null,
                    "持っているものを返すだけである。",
                    null,
                    null,
                    embeddedIn.Length == 0 ? null : embeddedIn),
            });
        }

        /// <summary>振る舞いを確かめる検査。覆いに数えるのはこちらだけである。</summary>
        private static E2eCase Case(string tool, string rowKey = "")
        {
            return new E2eCase(
                rowKey,
                string.Empty,
                string.Empty,
                tool,
                "呼び出して成功すること",
                new Dictionary<string, object>(StringComparer.Ordinal),
                E2eExpectation.Called,
                null);
        }

        /// <summary>呼び先が在ることしか見ない検査。</summary>
        private static E2eCase Dispatched(string tool)
        {
            return new E2eCase(
                string.Empty,
                string.Empty,
                string.Empty,
                tool,
                "呼び先が在ること",
                new Dictionary<string, object>(StringComparer.Ordinal),
                E2eExpectation.Dispatched,
                null);
        }

        private static SignatureRecord Signature(
            string key,
            string declaringType,
            string memberName,
            string valueType,
            MemberKind memberKind = MemberKind.Property)
        {
            return new SignatureRecord(
                key,
                declaringType,
                memberKind,
                memberName,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                true,
                false,
                OperationDirection.Read);
        }

        private static TypeRoleTable Roles()
        {
            return new TypeRoleTable(
                new[]
                {
                    new TypeRoleRecord(
                        Bone, TypeRole.OperationTarget, "骨である。", "bone", "bones",
                        CapabilityOwner.Model),
                    new TypeRoleRecord(
                        Body, TypeRole.OperationTarget, "剛体である。", "body", "bodies",
                        CapabilityOwner.Model),
                    new TypeRoleRecord(Args, TypeRole.EventArgs, "イベントの引数である。"),
                },
                new HandleIssuanceRecord[0],
                new ElementCollectionRecord[0]);
        }
    }
}
