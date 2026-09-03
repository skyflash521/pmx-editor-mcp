using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class CommonAssignmentGateTests
    {
        private const string Key = "N.IOwner.Get()";

        [Fact]
        public void ATableThatMatchesTheEvidencePasses()
        {
            CommonAssignmentGate.Require(
                Table(Record(Key, CommonAssignmentKind.InternalFlow, "connect", Bound())),
                Keys(Key),
                Keys(Key),
                Keys(),
                Bindings(Bound()));
        }

        [Fact]
        public void ASignatureOutsideTheProvidedStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, "connect", Bound())),
                    Keys(),
                    Keys(),
                    Keys(),
                    Bindings(Bound())));

            Assert.Contains(Key, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AResidentObjectTheTableOmitsStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(), Keys(Key), Keys(Key), Keys(), Bindings(Bound())));

            Assert.Contains(Key, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AResidentObjectAssignedToAnotherFlowStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, "stateRead", Bound())),
                    Keys(Key),
                    Keys(Key),
                    Keys(),
                    Bindings(Bound())));

            Assert.Contains("接続初期化でない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AResidentObjectAssignedToAToolStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.Tool, "connect", Bound())),
                    Keys(Key),
                    Keys(Key),
                    Keys(),
                    Bindings(Bound())));

            Assert.Contains("接続初期化でない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ABindingThatDiffersFromTheEvidenceStops()
        {
            SlotBinding written = new SlotBinding(
                BindingSlot.PmxClone, BindingSlot.OwningObject, Empty());

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, "connect", written)),
                    Keys(Key),
                    Keys(Key),
                    Keys(),
                    Bindings(Bound())));

            Assert.Contains("束縛", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ABindingWithAnExtraParameterStops()
        {
            SlotBinding written = new SlotBinding(
                BindingSlot.ResidentObject,
                BindingSlot.OwningObject,
                new Dictionary<string, BindingSlot> { { "path", BindingSlot.ModulePath } });

            Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, "connect", written)),
                    Keys(Key),
                    Keys(Key),
                    Keys(),
                    Bindings(Bound())));
        }

        [Fact]
        public void ABindingThatIsNotDerivedStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.Tool, "t", Bound())),
                    Keys(Key),
                    Keys(),
                    Keys(),
                    new Dictionary<string, SlotBinding>(StringComparer.Ordinal)));

            Assert.Contains("導けない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            CommonAssignmentTable table = Table();

            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentGate.Require(
                    null, Keys(), Keys(), Keys(), Bindings(Bound())));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentGate.Require(
                    table, null, Keys(), Keys(), Bindings(Bound())));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentGate.Require(
                    table, Keys(), null, Keys(), Bindings(Bound())));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentGate.Require(
                    table, Keys(), Keys(), null, Bindings(Bound())));
            Assert.Throws<ArgumentNullException>(
                () => CommonAssignmentGate.Require(table, Keys(), Keys(), Keys(), null));
        }

        [Fact]
        public void AReleaseTheTableOmitsStops()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(), Keys(), Keys(), Keys(Key), Bindings(Bound())));

            Assert.Contains("解放・破棄なのに表に無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASlotThatTheFlowDoesNotHaveStops()
        {
            SlotBinding written = new SlotBinding(BindingSlot.RunArgsClone, null, Empty());

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, "stateRead", written)),
                    Keys(Key),
                    Keys(),
                    Keys(),
                    Bindings(written)));

            Assert.Contains("使えないスロット", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void EveryFlowKeepsItsOwnSlots()
        {
            Dictionary<string, BindingSlot> flows = new Dictionary<string, BindingSlot>
            {
                { "duplicateEdit", BindingSlot.UpdateKind },
                { "stateRead", BindingSlot.PmxClone },
                { "connect", BindingSlot.ResidentObject },
            };
            foreach (KeyValuePair<string, BindingSlot> flow in flows)
            {
                SlotBinding written = new SlotBinding(flow.Value, null, Empty());

                CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, flow.Key, written)),
                    Keys(Key),
                    Keys(),
                    Keys(),
                    Bindings(written));
            }
        }

        [Fact]
        public void ATargetHandleOutsideAToolStops()
        {
            SlotBinding written = new SlotBinding(null, BindingSlot.TargetHandle, Empty());

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, "stateRead", written)),
                    Keys(Key),
                    Keys(),
                    Keys(),
                    Bindings(written)));

            Assert.Contains("使えないスロット", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AReleaseOutsideAToolStops()
        {
            SlotBinding written = new SlotBinding(null, BindingSlot.OwningObject, Empty());

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, "stateRead", written)),
                    Keys(Key),
                    Keys(),
                    Keys(Key),
                    Bindings(written)));

            Assert.Contains("ツールへの束縛でない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnInternalFlowThatIsNotATargetNameStops()
        {
            SlotBinding written = new SlotBinding(null, BindingSlot.OwningObject, Empty());

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(Record(Key, CommonAssignmentKind.InternalFlow, "releaseHandle", written)),
                    Keys(Key),
                    Keys(),
                    Keys(),
                    Bindings(written)));

            Assert.Contains("対象名でない", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TargetNamesThatDifferAmongTheReleasesStop()
        {
            SlotBinding written = new SlotBinding(null, BindingSlot.TargetHandle, Empty());
            Dictionary<string, SlotBinding> bindings =
                new Dictionary<string, SlotBinding>(StringComparer.Ordinal)
                {
                    { Key, written },
                    { "N.IOther.Release()", written },
                };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => CommonAssignmentGate.Require(
                    Table(
                        Record(Key, CommonAssignmentKind.Tool, "session_release_handle", written),
                        Record(
                            "N.IOther.Release()",
                            CommonAssignmentKind.Tool,
                            "session_release_handles",
                            written)),
                    Keys(Key, "N.IOther.Release()"),
                    Keys(),
                    Keys(),
                    bindings));

            Assert.Contains("揃っていない", error.Message, StringComparison.Ordinal);
        }

        private static SlotBinding Bound()
        {
            return new SlotBinding(BindingSlot.ResidentObject, BindingSlot.OwningObject, Empty());
        }

        private static IDictionary<string, BindingSlot> Empty()
        {
            return new Dictionary<string, BindingSlot>(StringComparer.Ordinal);
        }

        private static IDictionary<string, SlotBinding> Bindings(SlotBinding binding)
        {
            return new Dictionary<string, SlotBinding>(StringComparer.Ordinal)
            {
                { Key, binding },
            };
        }

        private static ISet<string> Keys(params string[] keys)
        {
            return new HashSet<string>(keys, StringComparer.Ordinal);
        }

        private static CommonAssignmentTable Table(params CommonAssignmentRecord[] records)
        {
            return new CommonAssignmentTable(records.ToList());
        }

        private static CommonAssignmentRecord Record(
            string signatureKey,
            CommonAssignmentKind assignment,
            string target,
            SlotBinding binding)
        {
            return new CommonAssignmentRecord(
                signatureKey, assignment, target, binding, signatureKey + " の根拠。");
        }
    }
}
