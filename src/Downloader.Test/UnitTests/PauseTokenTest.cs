using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class PauseTokenTest
    {
        private volatile int Counter;

        [TestMethod]
        public async Task TestPauseTaskWithPauseToken()
        {
            // arrange
            var cts = new CancellationTokenSource();
            var pts = new PauseTokenSource();
            Counter = 0;
            var expectedCount = 0;
            
            // act
            pts.Pause();
            var _ = Task.WhenAll(IncreaseAsync(pts.Token, cts.Token),
                IncreaseAsync(pts.Token, cts.Token),
                IncreaseAsync(pts.Token, cts.Token),
                IncreaseAsync(pts.Token, cts.Token),
                IncreaseAsync(pts.Token, cts.Token))
                .ConfigureAwait(false);

            for (var i = 0; i < 10; i++)
            {
                Assert.IsTrue(expectedCount >= Counter, $"Expected: {expectedCount}, Actual: {Counter}");
                pts.Resume();
                Assert.IsFalse(pts.IsPaused);
                pts.Pause();
                Assert.IsTrue(pts.IsPaused);                
                Interlocked.Exchange(ref expectedCount, Counter + 4);
                await Task.Delay(10);
            }
            cts.Cancel();

            // assert
            Assert.IsTrue(expectedCount >= Counter, $"Expected: {expectedCount}, Actual: {Counter}");
            Assert.IsTrue(pts.IsPaused);
        }

        private async Task IncreaseAsync(PauseToken pause, CancellationToken cancel)
        {
            while (cancel.IsCancellationRequested == false)
            {
                await pause.WaitWhilePausedAsync();
                Interlocked.Increment(ref Counter);
                await Task.Delay(1);
            }
        }
    }
}
