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

        private const string PartsType = "PEPlugin.View.IPEPartsSelectConnector";

        private const string TransformType = "PEPlugin.View.IPETransformViewConnector";

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

        [Theory]
        [InlineData("GetCheckedMaterialIndices")]
        [InlineData("SetCheckedMaterialIndices")]
        [InlineData("GetCheckedBoneIndices")]
        [InlineData("SetCheckedBoneIndices")]
        [InlineData("GetCheckedExpressionIndices")]
        [InlineData("SetCheckedExpressionIndices")]
        public void TouchingTheNarrowingListNeedsItemsOnIt(string memberName)
        {
            PreconditionKind kind;

            Assert.True(
                PreconditionRule.TryClassify(Signature(PartsType, memberName), out kind));
            Assert.Equal(PreconditionKind.ListedParts, kind);
        }

        [Theory]
        [InlineData("GetCheckedMaterialIndices", "MaterialItemsCount")]
        [InlineData("SetCheckedMaterialIndices", "MaterialItemsCount")]
        [InlineData("GetCheckedBoneIndices", "BoneItemsCount")]
        [InlineData("SetCheckedBoneIndices", "BoneItemsCount")]
        [InlineData("GetCheckedExpressionIndices", "ExpressionItemsCount")]
        [InlineData("SetCheckedExpressionIndices", "ExpressionItemsCount")]
        public void WhatIsOnTheNarrowingListIsReadFromItsOwnCount(
            string memberName, string countName)
        {
            Assert.Equal(
                PartsType + "." + countName + "()",
                PreconditionRule.Listed(
                    Signature(PartsType, memberName),
                    new[]
                    {
                        Signature(PartsType, "MaterialItemsCount"),
                        Signature(PartsType, "BoneItemsCount"),
                        Signature(PartsType, "ExpressionItemsCount"),
                        Signature(ViewType, countName),
                    }));
        }

        [Fact]
        public void AMemberThatTouchesNoNarrowingListHasNoCount()
        {
            Assert.Null(
                PreconditionRule.Listed(
                    Signature(PartsType, "RangeBegin"),
                    new[] { Signature(PartsType, "MaterialItemsCount") }));
            Assert.Null(
                PreconditionRule.Listed(
                    Signature(ViewType, "GetCheckedMaterialIndices"),
                    new[] { Signature(PartsType, "MaterialItemsCount") }));
        }

        [Theory]
        [InlineData("Undo", "UndoCount")]
        [InlineData("Redo", "RedoCount")]
        public void GoingBackOrForwardNeedsHistoryLeftAndReadsItsCount(string memberName, string countName)
        {
            PreconditionKind kind;

            Assert.True(PreconditionRule.TryClassify(Signature(FormType, memberName), out kind));
            Assert.Equal(PreconditionKind.UndoHistory, kind);
            Assert.Equal(
                FormType + "." + countName + "()",
                PreconditionRule.CountingOf(
                    Signature(FormType, memberName),
                    new[]
                    {
                        Signature(FormType, "UndoCount"),
                        Signature(FormType, "RedoCount"),
                        Signature(ViewType, countName),
                    }));
        }

        [Theory]
        [InlineData("BoneRotate")]
        [InlineData("BoneTranslate")]
        [InlineData("BoneScaling")]
        public void MovingTheTransformViewsBoneNeedsABoneChosenAndReadsWhichOne(string memberName)
        {
            PreconditionKind kind;

            Assert.True(PreconditionRule.TryClassify(Signature(TransformType, memberName), out kind));
            Assert.Equal(PreconditionKind.TransformedBone, kind);
            Assert.Equal(
                TransformType + ".SelectedBoneIndex()",
                PreconditionRule.CountingOf(
                    Signature(TransformType, memberName),
                    new[]
                    {
                        Signature(TransformType, "SelectedMorphIndex"),
                        Signature(TransformType, "SelectedBoneIndex"),
                        Signature(ViewType, "SelectedBoneIndex"),
                    }));
        }

        [Fact]
        public void ClosingAndTheNarrowingListReadTheirCountsAndOtherMembersReadNothing()
        {
            SignatureRecord[] signatures =
            {
                Signature(FormType, "UndoCount"),
                Signature(PartsType, "BoneItemsCount"),
            };

            Assert.Equal(
                FormType + ".UndoCount()",
                PreconditionRule.CountingOf(Signature(FormType, "Close"), signatures));
            Assert.Equal(
                PartsType + ".BoneItemsCount()",
                PreconditionRule.CountingOf(Signature(PartsType, "SetCheckedBoneIndices"), signatures));
            Assert.Null(PreconditionRule.CountingOf(Signature(GuideType, "GetSelectedCurrentVertex"), signatures));
            Assert.Null(PreconditionRule.CountingOf(Signature(FormType, "UndoCount"), signatures));
        }

        [Fact]
        public void EveryArgumentIsRequired()
        {
            PreconditionKind kind;

            Assert.Throws<ArgumentNullException>(
                () => PreconditionRule.TryClassify(null, out kind));
            Assert.Throws<ArgumentNullException>(() => PreconditionRule.Picked(null));
            Assert.Throws<ArgumentNullException>(() => PreconditionRule.Counting(null));
            Assert.Throws<ArgumentNullException>(
                () => PreconditionRule.Listed(null, new SignatureRecord[0]));
            Assert.Throws<ArgumentNullException>(
                () => PreconditionRule.Listed(Signature(PartsType, "BoneItemsCount"), null));
            Assert.Throws<ArgumentNullException>(
                () => PreconditionRule.CountingOf(null, new SignatureRecord[0]));
            Assert.Throws<ArgumentNullException>(
                () => PreconditionRule.CountingOf(Signature(FormType, "Undo"), null));
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
