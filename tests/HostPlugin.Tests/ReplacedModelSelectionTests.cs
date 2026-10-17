using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    [Collection(TimedCollection.Name)]
    public sealed class ReplacedModelSelectionTests : IDisposable
    {
        private readonly GeneratedToolFixture _fixture = new GeneratedToolFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Theory]
        [InlineData("session_open_pmx_file", "OpenPMXFile", true)]
        [InlineData("session_open_pmd_file", "OpenPMDFile", true)]
        [InlineData("session_import_x_file", "ImportXFile", true)]
        [InlineData("session_initialize_pmx", "InitializePMX", false)]
        [InlineData("session_initialize_pmd", "InitializePMD", false)]
        public void EverySelectionIsClearedAfterTheModelIsReplaced(string tool, string member, bool withPath)
        {
            Select();

            IDictionary<string, object> envelope = Call(tool, withPath);

            Succeeded(envelope);
            Assert.Equal(new[] { member }, _fixture.Form.Replaced);
            AssertNothingSelected();
        }

        [Fact]
        public void TheViewIsRepaintedAfterTheSelectionIsCleared()
        {
            Select();

            Call("session_open_pmx_file", true);

            Assert.True(_fixture.View.Repaints > 0);
            Assert.Equal(0, _fixture.View.SelectedAtLastRepaint);
        }

        [Fact]
        public void TheSelectionStaysWhenTheEditorDidNotReplaceTheModel()
        {
            Select();
            _fixture.Form.ReplaceSucceeds = false;

            Call("session_open_pmx_file", true);

            Assert.Equal(new[] { 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        [Fact]
        public void ReadingAFileIntoTheCurrentModelClearsEverySelection()
        {
            Select();

            Succeeded(_fixture.Call(
                "model_from_file_pmx",
                ComposedEditFixture.Given("path", @"C:\models\a.pmx")));

            AssertNothingSelected();
            Assert.Equal(0, _fixture.View.SelectedAtLastRepaint);
        }

        [Fact]
        public void AFailedRepaintAfterTheSelectionIsClearedIsTold()
        {
            Select();
            Succeeded(_fixture.Call(
                "model_from_file_pmx", ComposedEditFixture.Given("path", @"C:\models.pmx")));
            int painted = _fixture.View.Repaints;
            Select();
            _fixture.View.PaintsAllowed = painted + painted - 1;

            IDictionary<string, object> envelope = _fixture.Call(
                "model_from_file_pmx", ComposedEditFixture.Given("path", @"C:\models.pmx"));

            Succeeded(envelope);
            Assert.Contains(
                ScreenRefresh.NotShownWarning,
                ((object[])envelope[ToolEnvelope.WarningsName]).Cast<string>());
        }

        [Fact]
        public void ReadingAFileIntoAHandleLeavesTheSelection()
        {
            Select();
            IDictionary<string, object> made = _fixture.Call("model_pmx");
            object issued = made["value"];
            System.Collections.IList row = issued as System.Collections.IList;
            long handle = Convert.ToInt64(row == null ? issued : row[0]);

            Succeeded(_fixture.Call(
                "model_from_file_pmx",
                ComposedEditFixture.Given("path", @"C:\models\a.pmx"),
                ComposedEditFixture.Given(PmxSession.HandleName, handle)));

            Assert.Equal(new[] { 1, 2 }, _fixture.View.Selected[ElementKinds.Vertex]);
        }

        private static void Succeeded(IDictionary<string, object> envelope)
        {
            Assert.True(
                Equals(envelope["ok"], true),
                Equals(envelope["ok"], true) ? string.Empty : ComposedEditFixture.Message(envelope));
        }

        private void AssertNothingSelected()
        {
            foreach (KeyValuePair<string, int[]> held in _fixture.View.Selected)
            {
                Assert.True(held.Value.Length == 0, held.Key + " の選択が残っている。");
            }
        }

        private void Select()
        {
            _fixture.View.Selected[ElementKinds.Vertex] = new[] { 1, 2 };
            _fixture.View.Selected[ElementKinds.Face] = new[] { 0, 1, 2 };
            _fixture.View.Selected[ElementKinds.Bone] = new[] { 0 };
            _fixture.View.Selected[ElementKinds.Body] = new[] { 0 };
            _fixture.View.Selected[ElementKinds.Joint] = new[] { 0 };
        }

        private IDictionary<string, object> Call(string tool, bool withPath)
        {
            List<KeyValuePair<string, object>> given = new List<KeyValuePair<string, object>>();
            if (withPath)
            {
                given.Add(new KeyValuePair<string, object>("path", @"C:\models\a.pmx"));
            }
            else
            {
                given.Add(new KeyValuePair<string, object>(ToolDispatch.ConfirmName, true));
            }

            return _fixture.Call(tool, given.ToArray());
        }
    }
}
