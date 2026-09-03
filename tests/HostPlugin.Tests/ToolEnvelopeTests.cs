using System;
using System.Collections.Generic;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ToolEnvelopeTests
    {
        [Fact]
        public void ASuccessCarriesTheValue()
        {
            IDictionary<string, object> envelope = ToolEnvelope.Success(42);

            Assert.Equal(true, envelope["ok"]);
            Assert.Equal(42, envelope["value"]);
            Assert.False(envelope.ContainsKey("error"));
            Assert.False(envelope.ContainsKey("warnings"));
        }

        [Fact]
        public void ASuccessWithoutAValueCarriesNull()
        {
            IDictionary<string, object> envelope = ToolEnvelope.Success(null);

            Assert.Null(envelope["value"]);
        }

        [Fact]
        public void AFailureCarriesTheCodeAndTheMessage()
        {
            IDictionary<string, object> envelope = ToolEnvelope.Failure(
                ToolEnvelope.InvalidArgument, "値が範囲の外にある。");

            Assert.Equal(false, envelope["ok"]);
            Assert.False(envelope.ContainsKey("value"));
            IDictionary<string, object> error =
                Assert.IsType<Dictionary<string, object>>(envelope["error"]);
            Assert.Equal(ToolEnvelope.InvalidArgument, error["code"]);
            Assert.Equal("値が範囲の外にある。", error["message"]);
        }

        [Fact]
        public void EveryErrorCodeIsAccepted()
        {
            foreach (string code in ToolEnvelope.ErrorCodes)
            {
                IDictionary<string, object> envelope = ToolEnvelope.Failure(code, "説明。");
                IDictionary<string, object> error =
                    Assert.IsType<Dictionary<string, object>>(envelope["error"]);

                Assert.Equal(code, error["code"]);
            }
        }

        [Fact]
        public void TheErrorCodesAreTheClosedSet()
        {
            Assert.Equal(
                new[]
                {
                    "TOOL_INDEX_OUT_OF_RANGE", "TOOL_INVALID_ARGUMENT", "TOOL_INVALID_HANDLE",
                    "TOOL_CONFIRM_REQUIRED", "TOOL_NOT_APPLICABLE", "TOOL_OPERATION_FAILED",
                    "TOOL_RESPONSE_TOO_LARGE", "TOOL_REQUEST_TOO_LARGE",
                },
                ToolEnvelope.ErrorCodes);
        }

        [Fact]
        public void AnUnknownErrorCodeStops()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => ToolEnvelope.Failure("TOOL_UNKNOWN", "説明。"));

            Assert.Contains("エラーコード", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AFailureWithoutAMessageStops()
        {
            Assert.Throws<ArgumentNullException>(
                () => ToolEnvelope.Failure(ToolEnvelope.NotApplicable, null));
            Assert.Throws<ArgumentException>(
                () => ToolEnvelope.Failure(ToolEnvelope.NotApplicable, "  "));
            Assert.Throws<ArgumentNullException>(() => ToolEnvelope.Failure(null, "説明。"));
        }

        [Fact]
        public void WarningsAreCarriedOnBothShapes()
        {
            string[] warnings = { "表示の更新に失敗した。" };

            Assert.Equal(warnings, ToolEnvelope.Success(1, warnings)["warnings"]);
            Assert.Equal(
                warnings,
                ToolEnvelope.Failure(ToolEnvelope.OperationFailed, "説明。", warnings)["warnings"]);
        }

        [Fact]
        public void NoWarningIsCarriedWhenThereIsNone()
        {
            Assert.False(ToolEnvelope.Success(1, new string[0]).ContainsKey("warnings"));
            Assert.False(ToolEnvelope.Success(1, null).ContainsKey("warnings"));
        }

        [Fact]
        public void AnEmptyWarningStops()
        {
            Assert.Throws<ArgumentException>(() => ToolEnvelope.Success(1, new[] { "  " }));
            Assert.Throws<ArgumentException>(() => ToolEnvelope.Success(1, new string[] { null }));
        }
    }
}
