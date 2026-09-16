using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PEPlugin.SDX;
using PXCPlugin.Event;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>イベント固有の値を応答へ載せる組へ直す、種別ごとの読み方。</summary>
    public sealed class EventPayloadTests
    {
        private static readonly IDictionary<string, PayloadReader> Readers =
            GeneratedTools.Payloads();

        private static readonly string[] Mouse =
            { "button", "clicks", "x", "y", "delta", "vPoint", "location" };

        private static readonly string[] Drag =
            { "button", "clicks", "x", "y", "delta", "vPoint", "location", "stX", "stY", "vDrag" };

        [Fact]
        public void EveryBranchOfThePollingToolHasAWayToReadItsValue()
        {
            Assert.Equal(21, Readers.Count);
        }

        [Theory]
        [InlineData("view_mouse_click")]
        [InlineData("view_mouse_double_click")]
        [InlineData("view_mouse_down")]
        [InlineData("view_mouse_move")]
        [InlineData("view_mouse_up")]
        [InlineData("view_mouse_wheel")]
        public void AMouseOnTheViewCarriesWhereAndHowItWasPressed(string branch)
        {
            IDictionary<string, object> read = Readers[branch](
                new PXEventArgs.ViewMouse(MouseButtons.Left, 2, 30, 40, 120, new V3(1f, 2f, 3f)));

            Names(Mouse, read);
            Pressed(read, "Left", 2, 30, 40, 120, 1f, 2f, 3f);
        }

        [Theory]
        [InlineData("ui_model_mouse_click")]
        [InlineData("ui_model_mouse_double_click")]
        [InlineData("ui_model_mouse_down")]
        [InlineData("ui_model_mouse_enter")]
        [InlineData("ui_model_mouse_leave")]
        [InlineData("ui_model_mouse_over")]
        [InlineData("ui_model_mouse_up")]
        public void AMouseOnAUiModelCarriesTheSameAsOnTheView(string branch)
        {
            IDictionary<string, object> read = Readers[branch](
                new PXEventArgs.UIModelMouse(
                    MouseButtons.Right, 1, 5, 6, 240, new V3(7f, 8f, 9f)));

            Names(Mouse, read);
            Pressed(read, "Right", 1, 5, 6, 240, 7f, 8f, 9f);
        }

        [Theory]
        [InlineData("ui_model_mouse_drag")]
        [InlineData("ui_model_mouse_drag_end")]
        public void ADragCarriesWhereItStartedAndHowFarItWentOnTopOfTheMouse(string branch)
        {
            IDictionary<string, object> read = Readers[branch](
                new PXEventArgs.UIModelMouseDrag(
                    MouseButtons.Middle, 3, 5, 6, 60, new V3(1f, 1f, 1f),
                    11, 12, new V3(2f, 3f, 4f)));

            Names(Drag, read);
            Pressed(read, "Middle", 3, 5, 6, 60, 1f, 1f, 1f);
            Assert.Equal(11, read["stX"]);
            Assert.Equal(12, read["stY"]);
            Assert.Equal(new object[] { 2f, 3f, 4f }, (IEnumerable<object>)read["vDrag"]);
        }

        [Fact]
        public void WhatWasSelectedCarriesEveryKindThatWasPicked()
        {
            IDictionary<string, object> read = Readers["view_object_selected"](
                new PXEventArgs.ViewObjectSelected(true, false, true, false, true));

            Names(new[] { "vertex", "face", "bone", "body", "joint" }, read);
            Assert.Equal(true, read["vertex"]);
            Assert.Equal(false, read["face"]);
            Assert.Equal(true, read["bone"]);
            Assert.Equal(false, read["body"]);
            Assert.Equal(true, read["joint"]);
        }

        [Theory]
        [InlineData("view_key_down")]
        [InlineData("view_key_up")]
        public void AKeyCarriesWhichKeyAndWhatWasHeldWithIt(string branch)
        {
            IDictionary<string, object> read = Readers[branch](
                new KeyEventArgs(Keys.A | Keys.Control | Keys.Shift));

            Names(new[] { "keyCode", "keyValue", "alt", "control", "shift" }, read);
            Assert.Equal("A", read["keyCode"]);
            Assert.Equal((int)Keys.A, read["keyValue"]);
            Assert.Equal(false, read["alt"]);
            Assert.Equal(true, read["control"]);
            Assert.Equal(true, read["shift"]);
        }

        [Theory]
        [InlineData("view_model_updated")]
        [InlineData("view_redo")]
        [InlineData("view_undo")]
        public void AnEventWithoutAValueCarriesNothing(string branch)
        {
            Assert.Empty(Readers[branch](null));
        }

        [Fact]
        public void EveryListenerTypeHasAWayToAttachToIt()
        {
            Assert.Equal(
                new[]
                {
                    typeof(IPXUIModelEventListener).FullName,
                    typeof(IPXViewEventListener).FullName,
                },
                GeneratedTools.Attachments().Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        /// <summary>その組が持つ項目が、宣言した並びとちょうど同じであることを求める。</summary>
        private static void Names(string[] expected, IDictionary<string, object> read)
        {
            Assert.Equal(
                expected.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                read.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        /// <summary>マウスの値が、押した様子と指した場所をそのまま持つことを求める。</summary>
        private static void Pressed(
            IDictionary<string, object> read,
            string button,
            int clicks,
            int x,
            int y,
            int delta,
            float pointX,
            float pointY,
            float pointZ)
        {
            Assert.Equal(button, read["button"]);
            Assert.Equal(clicks, read["clicks"]);
            Assert.Equal(x, read["x"]);
            Assert.Equal(y, read["y"]);
            Assert.Equal(delta, read["delta"]);
            Assert.Equal(
                new object[] { pointX, pointY, pointZ }, (IEnumerable<object>)read["vPoint"]);
            Assert.Equal(new object[] { x, y }, (IEnumerable<object>)read["location"]);
        }
    }
}
