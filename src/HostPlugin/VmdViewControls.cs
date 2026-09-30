using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace PmxEditorMcp
{
    /// <summary>UIスレッドで使う。</summary>
    internal sealed class VmdViewControls
    {
        internal const string FormName = "VmdViewLib.VMDViewForm";

        internal const string FrameName = "frame";

        internal const string LastFrameName = "lastFrame";

        internal const string PlayingName = "playing";

        internal const string PlayRangeStartName = "playRangeStart";

        internal const string PlayRangeEndName = "playRangeEnd";

        internal const string LoopName = "loop";

        internal const string StartAtCurrentFrameName = "startAtCurrentFrame";

        private static readonly string[] GroupPath = { "pnlControlGroup" };

        private static readonly string[] FramePath = { "pnlControlGroup", "pnlControlWithoutPlay", "trkFrame" };

        private static readonly string[] RangeStartPath =
            { "pnlControlGroup", "pnlControlWithoutPlay", "txtPlayRangeSt" };

        private static readonly string[] RangeEndPath =
            { "pnlControlGroup", "pnlControlWithoutPlay", "txtPlayRangeEd" };

        private static readonly string[] LoopPath = { "pnlControlGroup", "pnlControlWithoutPlay", "chkLoop" };

        private static readonly string[] StartAtCurrentPath =
            { "pnlControlGroup", "pnlControlWithoutPlay", "chkCurStart" };

        private static readonly string[] FileMenuPath = { "menuStrip1", "MenuItem_File" };

        private static readonly string[] FrameRateMenuPath =
            { "menuStrip1", "MenuItem_Info", "MenuItem_FPS_Frame30" };

        private VmdViewControls(
            Form form,
            Control group,
            TrackBar frame,
            TextBox rangeStart,
            TextBox rangeEnd,
            CheckBox loop,
            CheckBox startAtCurrent,
            ToolStripItem fileMenu,
            ToolStripItem frameRateMenu)
        {
            Form = form;
            Group = group;
            Frame = frame;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
            Loop = loop;
            StartAtCurrent = startAtCurrent;
            FileMenu = fileMenu;
            FrameRateMenu = frameRateMenu;
        }

        internal Form Form { get; }

        internal Control Group { get; }

        internal TrackBar Frame { get; }

        internal TextBox RangeStart { get; }

        internal TextBox RangeEnd { get; }

        internal CheckBox Loop { get; }

        internal CheckBox StartAtCurrent { get; }

        /// <summary>再生中はこのメニューが使えなくなる。</summary>
        internal ToolStripItem FileMenu { get; }

        internal ToolStripItem FrameRateMenu { get; }

        internal bool HasVmeEvent
        {
            get { return !FrameRateMenu.Enabled; }
        }

        internal bool Playing
        {
            get { return Busy == null && !FileMenu.Enabled; }
        }

        internal string Busy
        {
            get
            {
                if (!Form.Enabled)
                {
                    return "VMDView の映像出力のダイアログが表示されている。閉じてから呼ぶ。";
                }

                if (!Group.Enabled)
                {
                    return "VMDView が Fixモーションを作っている。作り終わってから呼ぶ。";
                }

                return null;
            }
        }

        /// <summary>開いていないか部品が見つからなければ偽を返し、その事情を <paramref name="message"/> に返す。</summary>
        internal static bool TryOpen(
            IEnumerable<Form> forms, out VmdViewControls controls, out string message)
        {
            controls = null;
            Form form = UiLive.Shown(forms, FormName);
            if (form == null)
            {
                message = "VMDView が開いていない。" + ViewLoadVmdView.ToolName + " で開いてから呼ぶ。";

                return false;
            }

            List<UiMenu> menus = new List<UiMenu>();
            Control group = UiLive.Find(form, GroupPath, menus) as Control;
            TrackBar frame = UiLive.Find(form, FramePath, menus) as TrackBar;
            TextBox rangeStart = UiLive.Find(form, RangeStartPath, menus) as TextBox;
            TextBox rangeEnd = UiLive.Find(form, RangeEndPath, menus) as TextBox;
            CheckBox loop = UiLive.Find(form, LoopPath, menus) as CheckBox;
            CheckBox startAtCurrent = UiLive.Find(form, StartAtCurrentPath, menus) as CheckBox;
            ToolStripItem fileMenu = UiLive.Find(form, FileMenuPath, menus) as ToolStripItem;
            ToolStripItem frameRateMenu = UiLive.Find(form, FrameRateMenuPath, menus) as ToolStripItem;
            if (group == null || frame == null || rangeStart == null || rangeEnd == null || loop == null
                || startAtCurrent == null || fileMenu == null || frameRateMenu == null)
            {
                message = "VMDView の再生の部品が見つからない。";

                return false;
            }

            message = null;
            controls = new VmdViewControls(form, group, frame, rangeStart, rangeEnd, loop, startAtCurrent, fileMenu, frameRateMenu);

            return true;
        }

        internal bool TryStart(out int start)
        {
            return int.TryParse(RangeStart.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out start);
        }

        internal bool TryEnd(out int end)
        {
            return int.TryParse(RangeEnd.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out end);
        }

        /// <summary>再生範囲の欄が整数でなければ null。</summary>
        internal IDictionary<string, object> State()
        {
            int start;
            int end = 0;
            if (!TryStart(out start) || !TryEnd(out end))
            {
                return null;
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { FrameName, Frame.Value },
                { LastFrameName, Frame.Maximum },
                { PlayingName, Playing },
                { PlayRangeStartName, start },
                { PlayRangeEndName, end },
                { LoopName, Loop.Checked },
                { StartAtCurrentFrameName, StartAtCurrent.Checked },
            };
        }

        internal const string UnreadableRange =
            "VMDView の再生範囲の欄が整数でない。" + ViewSetVmdViewState.ToolName
            + " へ " + PlayRangeStartName + " と " + PlayRangeEndName + " を両方渡すと直る。";
    }
}
