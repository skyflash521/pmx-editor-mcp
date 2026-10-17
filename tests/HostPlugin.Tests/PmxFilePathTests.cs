using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    /// <summary>
    /// エディタの反映は、渡したモデルのファイルパスを現在のモデルへ写さない。
    /// </summary>
    [Collection(TimedCollection.Name)]
    public sealed class PmxFilePathTests : IDisposable
    {
        private readonly GeneratedToolFixture _fixture = new GeneratedToolFixture();

        public void Dispose()
        {
            _fixture.Dispose();
        }

        [Fact]
        public void TheFilePathWrittenToTheCurrentModelIsReadBack()
        {
            _fixture.Model.FilePath = @"C:\models\a.pmx";

            IDictionary<string, object> envelope = _fixture.Call(
                "model_update_pmxes",
                ComposedEditFixture.Given(
                    ToolDispatch.ValueName,
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "filePath", @"C:\models\b.pmx" },
                    }));

            Assert.True(
                Equals(envelope["ok"], true),
                "成功でない包み: "
                    + (Equals(envelope["ok"], true) ? string.Empty : ComposedEditFixture.Message(envelope)));
            Assert.Equal(@"C:\models\b.pmx", _fixture.Model.FilePath);
            IDictionary<string, object> listed = _fixture.Call(
                "model_list_pmxes",
                ComposedEditFixture.Given(ToolDispatch.FieldsName, new object[] { "filePath" }));
            IDictionary<string, object> value = (IDictionary<string, object>)listed["value"];
            IDictionary<string, object> first =
                (IDictionary<string, object>)((object[])value[ToolDispatch.ItemsName])[0];
            Assert.Equal(@"C:\models\b.pmx", first["filePath"]);
        }
    }
}
