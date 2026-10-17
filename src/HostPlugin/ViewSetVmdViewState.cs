// ComposedScreen に載る合成ツール。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    public static class ViewSetVmdViewState
    {
        public const string ToolName = "view_set_vmd_view_state";

        private const string StopToolName = "view_stop_vmd_view";

        /// <summary><paramref name="forms"/> は開いているウィンドウを返す。UIスレッドで呼ぶ。</summary>
        public static void AddTo(McpMethodTable methods, ComposedScreen screen, Func<IEnumerable<Form>> forms)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screen));
            }

            if (forms == null)
            {
                throw new ArgumentNullException(nameof(forms));
            }

            methods.Add(
                ToolName,
                screen.Method(
                    new List<string>
                    {
                        VmdViewControls.FrameName,
                        VmdViewControls.PlayRangeStartName,
                        VmdViewControls.PlayRangeEndName,
                        VmdViewControls.LoopName,
                        VmdViewControls.StartAtCurrentFrameName,
                    },
                    ScreenNeeds.None,
                    ScreenRefreshKind.None,
                    (context, parts) => Run(context, forms)));
        }

        private static ComposedEditResult Run(McpMethodContext context, Func<IEnumerable<Form>> forms)
        {
            int? frame;
            int? start;
            int? end;
            bool? loop;
            bool? startAtCurrent;
            string message;
            if (!TryIndex(context, VmdViewControls.FrameName, out frame, out message)
                || !TryIndex(context, VmdViewControls.PlayRangeStartName, out start, out message)
                || !TryIndex(context, VmdViewControls.PlayRangeEndName, out end, out message)
                || !TryFlag(context, VmdViewControls.LoopName, out loop, out message)
                || !TryFlag(context, VmdViewControls.StartAtCurrentFrameName, out startAtCurrent, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.InvalidArgument, message);
            }

            VmdViewControls controls;
            if (!VmdViewControls.TryOpen(forms(), out controls, out message))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, message);
            }

            string busy = controls.Busy;
            if (busy != null)
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, busy);
            }

            if (controls.Playing)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "VMDView が再生中で変えられない。" + StopToolName + " で止めてから呼ぶ。");
            }

            if (controls.HasVmeEvent)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.NotApplicable,
                    "VMDView に VME のイベントが設定されていて、どの項目も変えられない。"
                        + "VMDView を閉じて " + ViewLoadVmdView.ToolName + " で開き直すと変えられる。");
            }

            int currentStart;
            int currentEnd;
            bool startReadable = controls.TryStart(out currentStart);
            bool endReadable = controls.TryEnd(out currentEnd);
            if ((start == null && !startReadable) || (end == null && !endReadable))
            {
                return ComposedEditResult.Refuse(ToolEnvelope.NotApplicable, VmdViewControls.UnreadableRange);
            }

            int last = controls.Frame.Maximum;
            int newStart = start ?? currentStart;
            int newEnd = end ?? currentEnd;
            if (frame != null && frame.Value > last)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    VmdViewControls.FrameName + " " + frame.Value + " は最後のフレーム " + last + " を超える。");
            }

            if ((start != null || end != null) && (newEnd > last || newStart > newEnd))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    "再生範囲 " + newStart + " から " + newEnd + " は、最後のフレーム " + last
                        + " 以下で、始まりが終わり以下でなければならない。");
            }

            if (start != null)
            {
                controls.RangeStart.Text = newStart.ToString(CultureInfo.InvariantCulture);
            }

            if (end != null)
            {
                controls.RangeEnd.Text = newEnd.ToString(CultureInfo.InvariantCulture);
            }

            if (loop != null)
            {
                controls.Loop.Checked = loop.Value;
            }

            if (startAtCurrent != null)
            {
                controls.StartAtCurrent.Checked = startAtCurrent.Value;
            }

            if (frame != null)
            {
                controls.Frame.Value = frame.Value;
            }

            return ComposedEditResult.Complete(controls.State());
        }

        /// <summary>渡されていないか空なら <paramref name="number"/> は null。</summary>
        private static bool TryIndex(McpMethodContext context, string name, out int? number, out string message)
        {
            number = null;
            int taken = -1;
            if (!ComposedInput.TryNumber(context, name, 0, ref taken, out message))
            {
                return false;
            }

            if (taken >= 0)
            {
                number = taken;
            }

            return true;
        }

        /// <summary>渡されていなければ <paramref name="flag"/> は null。</summary>
        private static bool TryFlag(McpMethodContext context, string name, out bool? flag, out string message)
        {
            flag = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(name, out given))
            {
                return true;
            }

            if (!(given is bool))
            {
                message = name + " は真偽でなければならない。";

                return false;
            }

            flag = (bool)given;

            return true;
        }
    }
}
