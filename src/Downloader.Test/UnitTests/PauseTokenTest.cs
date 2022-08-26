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
        public void TestPauseTaskWithPauseToken()
        {
            // arrange
            var cts = new CancellationTokenSource();
            var pts = new PauseTokenSource();
            Counter = 0;
            var expectedCount = 0;

            // act
            pts.Pause();
            Task.Run(() => IncreaseAsync(pts.Token, cts.Token));
            for (var i = 0; i < 100; i++)
            {
                Assert.IsTrue(expectedCount >= Counter);
                pts.Resume();
                while (pts.IsPaused || expectedCount == Counter)
                    ;
                pts.Pause();
                while (pts.IsPaused == false)
                    ;
                Interlocked.Exchange(ref expectedCount, Counter+1);
                Thread.Sleep(1);
            }
            cts.Cancel();
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
