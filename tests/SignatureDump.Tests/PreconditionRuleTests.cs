using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>呼ぶ前に確かめることを持つシグネチャの決め方。</summary>
    public sealed class PreconditionRuleTests
    {
        private const string GuideType = "PEPlugin.View.IPEVertexGuideConnector";

        private const string ViewType = "PEPlugin.View.IPEPMDViewConnector";

        private const string FormType = "PEPlugin.Form.IPEFormConnector";

        [Fact]
        public void TakingWhatIsPickedNeedsSomethingPicked()
        {
            PreconditionKind kind;

            Assert.True(
                PreconditionRule.TryClassify(
                    Signature(GuideType, "GetSelectedCurrentVertex"), out kind));
            Assert.Equal(PreconditionKind.PickedObjects, kind);
        }

        [Fact]
        public void TheSameMemberNameOnAnotherTypeNeedsNothing()
        {
            PreconditionKind kind;

            Assert.False(
                PreconditionRule.TryClassify(
                    Signature(ViewType, "GetSelectedCurrentVertex"), out kind));
            Assert.Equal(PreconditionKind.None, kind);
        }

        [Fact]
        public void AnotherMemberOnTheSameTypeNeedsNothing()
        {
            PreconditionKind kind;

            Assert.False(
                PreconditionRule.TryClassify(Signature(GuideType, "SelectVertex"), out kind));
        }

        [Fact]
        public void WhatIsPickedIsReadFromTheViewsSelectionMembers()
        {
            IList<string> picked = PreconditionRule.Picked(
                new[]
                {
                    Signature(ViewType, "GetSelectedVertexIndices"),
                    Signature(ViewType, "GetSelectedBodyIndices"),
                    Signature(ViewType, "GetVertexIndices"),
                    Signature("PXCPlugin.IPXCPluginConnector", "GetSelectedVertexIndices"),
                });

            Assert.Equal(
                new[]
                {
                    ViewType + ".GetSelectedBodyIndices()",
                    ViewType + ".GetSelectedVertexIndices()",
                },
                picked);
        }

        [Fact]
        public void ClosingTheEditorNeedsNothingLeftToUndo()
        {
            PreconditionKind kind;

            Assert.True(
                PreconditionRule.TryClassify(Signature(FormType, "Close"), out kind));
            Assert.Equal(PreconditionKind.SavedEdits, kind);
        }

        [Fact]
        public void WhatCanBeUndoneIsReadFromTheFormsCount()
        {
            Assert.Equal(
                FormType + ".UndoCount()",
                PreconditionRule.Counting(
                    new[]
                    {
                        Signature(ViewType, "UndoCount"),
                        Signature(FormType, "UndoCount"),
                        Signature(FormType, "Undo"),
                    }));
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            PreconditionKind kind;

            Assert.Throws<ArgumentNullException>(
                () => PreconditionRule.TryClassify(null, out kind));
            Assert.Throws<ArgumentNullException>(() => PreconditionRule.Picked(null));
            Assert.Throws<ArgumentNullException>(() => PreconditionRule.Counting(null));
        }

        private static SignatureRecord Signature(string declaringType, string memberName)
        {
            return new SignatureRecord(
                declaringType + "." + memberName + "()",
                declaringType,
                MemberKind.Method,
                memberName,
                false,
                0,
                new List<ParameterRecord>(),
                "System.Void",
                false,
                false,
                OperationDirection.Read);
        }
    }
}
