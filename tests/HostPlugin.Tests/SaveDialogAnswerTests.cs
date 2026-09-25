using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(ModalWindowCollection.Name)]
    public sealed class SaveDialogAnswerTests
    {
        [Fact]
        public void ADialogClosedJustBeforeStopCountsAsAnswered()
        {
            string answered = null;
            string written = Path.Combine(Path.GetTempPath(), "pmx-editor-mcp-tests-" + Guid.NewGuid().ToString("N") + ".pmx");
            Exception caught = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    using (Form owner = new Form
                    {
                        ShowInTaskbar = false,
                        StartPosition = FormStartPosition.Manual,
                        Location = new Point(-32000, -32000),
                    })
                    {
                        owner.Show();
                        SaveDialogAnswer answer = SaveDialogAnswer.Start(
                            owner.Handle, written, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1500));
                        using (SaveFileDialog dialog = new SaveFileDialog())
                        {
                            dialog.ShowDialog(owner);
                        }

                        answered = answer.Stop();
                    }
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
            {
                throw new InvalidOperationException("STAのスレッドで落ちた。", caught);
            }

            Assert.Null(answered);
        }
    }
}
