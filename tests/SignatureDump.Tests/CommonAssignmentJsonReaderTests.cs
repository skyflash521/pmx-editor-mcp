using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class CommonAssignmentJsonReaderTests
    {
        [Fact]
        public void AnAssignmentIsReadWithItsTargetAndBasis()
        {
            CommonAssignmentRecord record = Assert.Single(
                Read(Item("N.A.Release()", "tool", "session_release_handle")));

            Assert.Equal("N.A.Release()", record.SignatureKey);
            Assert.Equal(CommonAssignmentKind.Tool, record.Assignment);
            Assert.Equal("session_release_handle", record.Target);
            Assert.Equal("N.A.Release() の根拠。", record.Basis);
        }

        [Fact]
        public void EveryAssignmentNameIsRead()
        {
            IList<CommonAssignmentRecord> records = Read(
                Item("N.A.Release()", "tool", "session_release_handle"),
                Item("N.B.LockUndo()", "commonArg", "suppressUndo"),
                Item("N.C.Update()", "internalFlow", "duplicateEdit"));

            Assert.Equal(
                new[]
                {
                    CommonAssignmentKind.Tool,
                    CommonAssignmentKind.CommonArg,
                    CommonAssignmentKind.InternalFlow,
                },
                records.Select(r => r.Assignment));
        }

        [Fact]
        public void EveryInternalFlowNameIsRead()
        {
            foreach (string flow in new[] { "duplicateEdit", "stateRead", "connect" })
            {
                Assert.Equal(flow, Assert.Single(Read(Item("N.A.M()", "internalFlow", flow))).Target);
            }
        }

        [Fact]
        public void AnUnknownAssignmentStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(Item("N.A.M()", "internal", "connect")));

            Assert.Contains("割当", error.Message);
        }

        [Fact]
        public void TheReleaseFlowIsNotAnInternalFlowTarget()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(Item("N.A.M()", "internalFlow", "releaseHandle")));

            Assert.Contains("対象名でない", error.Message);
        }

        [Fact]
        public void AnUnknownInternalFlowStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(Item("N.A.M()", "internalFlow", "startup")));

            Assert.Contains("対象名でない", error.Message);
        }

        [Fact]
        public void AToolNameIsNotCheckedAgainstTheInternalFlows()
        {
            Assert.Equal(
                "startup", Assert.Single(Read(Item("N.A.M()", "tool", "startup"))).Target);
        }

        [Fact]
        public void AToolNameThatIsNotASnakeCaseWordStops()
        {
            foreach (string target in new[] { "SessionRelease", "session release", "_release" })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => Read(Item("N.A.M()", "tool", target)));

                Assert.Contains("target", error.Message);
            }
        }

        [Fact]
        public void ACommonArgumentNameThatIsNotACamelCaseWordStops()
        {
            foreach (string target in new[] { "SuppressUndo", "suppress_undo", "suppress undo" })
            {
                FormatException error = Assert.Throws<FormatException>(
                    () => Read(Item("N.A.M()", "commonArg", target)));

                Assert.Contains("target", error.Message);
            }
        }

        [Fact]
        public void ACamelCaseCommonArgumentNameIsRead()
        {
            Assert.Equal(
                "suppressUndo",
                Assert.Single(Read(Item("N.A.M()", "commonArg", "suppressUndo"))).Target);
        }

        [Fact]
        public void EverySlotNameIsRead()
        {
            Dictionary<string, BindingSlot> slots = new Dictionary<string, BindingSlot>
            {
                { "pmxClone", BindingSlot.PmxClone },
                { "updateKind", BindingSlot.UpdateKind },
                { "updateIndices", BindingSlot.UpdateIndices },
                { "undoLock", BindingSlot.UndoLock },
                { "runArgsClone", BindingSlot.RunArgsClone },
                { "modulePath", BindingSlot.ModulePath },
                { "residentObject", BindingSlot.ResidentObject },
                { "targetHandle", BindingSlot.TargetHandle },
                { "owningObject", BindingSlot.OwningObject },
                { "injectedConnector", BindingSlot.InjectedConnector },
            };
            foreach (KeyValuePair<string, BindingSlot> slot in slots)
            {
                CommonAssignmentRecord record = Assert.Single(Read(
                    "{\"signatureKey\":\"N.A.M()\",\"assignment\":\"internalFlow\""
                        + ",\"target\":\"connect\",\"slotBinding\":{\"return\":\"" + slot.Key
                        + "\",\"parameters\":{}},\"basis\":\"根拠。\"}"));

                Assert.Equal(slot.Value, record.SlotBinding.Returned);
            }
        }

        [Fact]
        public void TheBindingOfEveryPlaceIsRead()
        {
            CommonAssignmentRecord record = Assert.Single(Read(
                "{\"signatureKey\":\"N.A.M(System.String)\",\"assignment\":\"internalFlow\""
                    + ",\"target\":\"connect\",\"slotBinding\":{\"return\":\"runArgsClone\""
                    + ",\"receiver\":\"owningObject\",\"parameters\":{\"path\":\"modulePath\"}}"
                    + ",\"basis\":\"根拠。\"}"));

            Assert.Equal(BindingSlot.RunArgsClone, record.SlotBinding.Returned);
            Assert.Equal(BindingSlot.OwningObject, record.SlotBinding.Receiver);
            Assert.Equal(BindingSlot.ModulePath, record.SlotBinding.Parameters["path"]);
        }

        [Fact]
        public void ABindingWithoutAReturnOrAReceiverIsRead()
        {
            CommonAssignmentRecord record = Assert.Single(Read(Item("N.A.M()", "tool", "t")));

            Assert.Null(record.SlotBinding.Returned);
            Assert.Null(record.SlotBinding.Receiver);
            Assert.Empty(record.SlotBinding.Parameters);
        }

        [Fact]
        public void ABindingWithoutTheParametersStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"signatureKey\":\"N.A.M()\",\"assignment\":\"tool\",\"target\":\"t\""
                        + ",\"slotBinding\":{},\"basis\":\"根拠。\"}"));

            Assert.Contains("parameters", error.Message);
        }

        [Fact]
        public void AnUnknownSlotStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"signatureKey\":\"N.A.M()\",\"assignment\":\"tool\",\"target\":\"t\""
                        + ",\"slotBinding\":{\"receiver\":\"holder\",\"parameters\":{}}"
                        + ",\"basis\":\"根拠。\"}"));

            Assert.Contains("スロット", error.Message);
        }

        [Fact]
        public void AnUnknownMemberStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(
                    "{\"signatureKey\":\"N.A.M()\",\"assignment\":\"tool\",\"target\":\"t\""
                        + ",\"slotBinding\":{\"parameters\":{}},\"basis\":\"根拠。\",\"note\":\"x\"}"));

            Assert.Contains("note", error.Message);
        }

        [Fact]
        public void AMissingMemberStops()
        {
            foreach (string name in new[]
            {
                "signatureKey", "assignment", "target", "slotBinding", "basis",
            })
            {
                string item = "{\"signatureKey\":\"N.A.M()\",\"assignment\":\"tool\""
                    + ",\"target\":\"t\",\"slotBinding\":{\"parameters\":{}},\"basis\":\"根拠。\"}";
                FormatException error = Assert.Throws<FormatException>(
                    () => Read(Without(item, name)));

                Assert.Contains(name, error.Message);
            }
        }

        [Fact]
        public void AssignmentsOutOfOrdinalOrderStop()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(Item("N.B.M()", "tool", "t"), Item("N.A.M()", "tool", "t")));

            Assert.Contains("昇順", error.Message);
        }

        [Fact]
        public void TheSameSignatureKeyTwiceStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Read(Item("N.A.M()", "tool", "t"), Item("N.A.M()", "tool", "t")));

            Assert.Contains("二度", error.Message);
        }

        [Fact]
        public void ATableWithoutTheAssignmentsStops()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => CommonAssignmentJsonReader.Read("{}"));

            Assert.Contains("assignments", error.Message);
        }

        [Fact]
        public void TextThatIsNotJsonStops()
        {
            Assert.Throws<FormatException>(() => CommonAssignmentJsonReader.Read("{"));
        }

        [Fact]
        public void TheJsonIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => CommonAssignmentJsonReader.Read(null));
        }

        [Fact]
        public void AnEmptyTableIsRead()
        {
            Assert.Empty(CommonAssignmentJsonReader.Read("{\"assignments\":[]}").Assignments);
        }

        private static string Without(string item, string name)
        {
            int at = item.IndexOf("\"" + name + "\":", StringComparison.Ordinal);
            int end = item.IndexOf(',', at);
            if (end < 0)
            {
                return item.Substring(0, at - 1) + "}";
            }

            return item.Substring(0, at) + item.Substring(end + 1);
        }

        private static string Item(string signatureKey, string assignment, string target)
        {
            return "{\"signatureKey\":\"" + signatureKey + "\",\"assignment\":\"" + assignment
                + "\",\"target\":\"" + target + "\",\"slotBinding\":{\"parameters\":{}}"
                + ",\"basis\":\"" + signatureKey + " の根拠。\"}";
        }

        private static IList<CommonAssignmentRecord> Read(params string[] items)
        {
            return CommonAssignmentJsonReader.Read(
                "{\"assignments\":[" + string.Join(",", items) + "]}").Assignments;
        }
    }
}
