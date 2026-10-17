using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class UiModelRegistrationTests
    {
        [Fact]
        public void APositionIsCheckedAgainstTheModelAtRegistrationNotTheCurrentOne()
        {
            using (GeneratedToolFixture fixture = new GeneratedToolFixture())
            {
                PositionPremise.Fill(fixture.Model);
                IDictionary<string, object> registered = fixture.Call(
                    "session_register_ui_model",
                    ComposedEditFixture.Given("name", "題材"),
                    ComposedEditFixture.Given("m", new object[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }),
                    ComposedEditFixture.Given("visible", false),
                    ComposedEditFixture.Given("transform", false));
                Assert.True(
                    (bool)registered["ok"],
                    "登録できない: " + (registered.ContainsKey("error") ? ComposedEditFixture.Message(registered) : string.Empty));
                object[] issued = (object[])registered["value"];
                IDictionary<string, object> first = issued[0] as IDictionary<string, object>;
                object[] handles = { first == null ? issued[0] : first.Values.First() };
                for (int more = 0; more < 3; more++)
                {
                    fixture.Model.Bone.Add(new FakeBone("追加" + more));
                }

                IDictionary<string, object> inside = fixture.Call(
                    "view_get_transformed_bone_position_ui_model",
                    ComposedEditFixture.Given("handles", handles),
                    ComposedEditFixture.Given(
                        "args",
                        new Dictionary<string, object> { { "bx", PositionPremise.Counts["bone"] - 1 } }));
                Assert.NotEqual(ToolEnvelope.IndexOutOfRange, inside.ContainsKey("error") ? ComposedEditFixture.Code(inside) : null);
                IDictionary<string, object> past = fixture.Call(
                    "view_get_transformed_bone_position_ui_model",
                    ComposedEditFixture.Given("handles", handles),
                    ComposedEditFixture.Given(
                        "args",
                        new Dictionary<string, object> { { "bx", PositionPremise.Counts["bone"] } }));

                Assert.Equal(ToolEnvelope.IndexOutOfRange, ComposedEditFixture.Code(past));
            }
        }
    }
}
