using System;
using PmxEditorMcp;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class RequestBudgetTests
    {
        [Fact]
        public void TheBudgetLeavesRoomUnderHalfOfWhatTheChannelCarries()
        {
            Assert.Equal(8000000, RequestBudget.Bytes);
            Assert.True(RequestBudget.Bytes < MessageChannel.DefaultMaxMessageBytes / 2);
        }

        [Fact]
        public void SizeIsCountedInUtf8Bytes()
        {
            Assert.Equal(3, RequestBudget.Measure("あ"));
            Assert.Equal(1, RequestBudget.Measure("a"));
            Assert.Equal(0, RequestBudget.Measure(string.Empty));
        }

        [Fact]
        public void CountingSomethingThatIsNotThereStops()
        {
            Assert.Throws<ArgumentNullException>(() => RequestBudget.Measure(null));
        }

        [Fact]
        public void ARequestWithinTheBudgetPasses()
        {
            Assert.True(RequestBudget.TryPass(
                new string('a', RequestBudget.Bytes), code: out string code, message: out string message));
            Assert.Null(code);
            Assert.Null(message);
        }

        [Fact]
        public void ARequestOverTheBudgetIsRefusedWithTheRequestCode()
        {
            Assert.False(RequestBudget.TryPass(
                new string('a', RequestBudget.Bytes + 1),
                code: out string code,
                message: out string message));
            Assert.Equal(ToolEnvelope.RequestTooLarge, code);
        }

        [Fact]
        public void TheRefusalNamesTheSizeAndTheBudget()
        {
            RequestBudget.TryPass(
                new string('a', RequestBudget.Bytes + 1), code: out string _, message: out string message);

            Assert.Contains((RequestBudget.Bytes + 1).ToString(), message);
            Assert.Contains(RequestBudget.Bytes.ToString(), message);
        }

        [Fact]
        public void ARequestIsMeasuredAfterEncodingRatherThanByItsLength()
        {
            // 1文字が3バイトになる文字だけの要求は、文字数が予算の3分の1でも超える。
            string request = new string('あ', RequestBudget.Bytes / 3 + 1);

            Assert.False(RequestBudget.TryPass(request, code: out string code, message: out string _));
            Assert.Equal(ToolEnvelope.RequestTooLarge, code);
        }

        [Fact]
        public void PassingSomethingThatIsNotThereStops()
        {
            Assert.Throws<ArgumentNullException>(
                () => RequestBudget.TryPass(null, code: out string _, message: out string _));
        }
    }
}
