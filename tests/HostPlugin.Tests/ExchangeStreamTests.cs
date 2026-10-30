using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public class ExchangeStreamTests
    {
        [Fact]
        public async Task AReadAheadHasFinishedByTheTimeItReturns()
        {
            ExchangeStream stream = new ExchangeStream(Encoding.UTF8.GetBytes("ab"));
            byte[] buffer = new byte[2];

            Task<int> read = stream.ReadAsync(buffer, 0, buffer.Length);

            Assert.True(read.IsCompleted);
            Assert.Equal(2, await read);
        }

        [Fact]
        public async Task AReadAheadAtTheEndHasFinishedWithNothing()
        {
            ExchangeStream stream = new ExchangeStream(new byte[0]);

            Task<int> read = stream.ReadAsync(new byte[1], 0, 1);

            Assert.True(read.IsCompleted);
            Assert.Equal(0, await read);
        }
    }
}
