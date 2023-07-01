using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class PauseTokenTest
    {
        private volatile int actualPauseCount = 0;

        [TestMethod]
        public async Task TestPauseTaskWithPauseToken()
        {
            // arrange
            var cts = new CancellationTokenSource();
            var pts = new PauseTokenSource();
            var expectedCount = 0;
            var checkTokenStateIsNotPaused = false;
            var checkTokenStateIsPaused = true;
            var hasRunningTask = true;
            var tasksAlreadyPaused = true;

            // act
            pts.Pause();
            var tasks = new Task[] {
                IncreaseAsync(pts.Token, cts.Token),
                IncreaseAsync(pts.Token, cts.Token),
                IncreaseAsync(pts.Token, cts.Token),
                IncreaseAsync(pts.Token, cts.Token),
                IncreaseAsync(pts.Token, cts.Token)
            };
            var _ = Task.WhenAll(tasks).ConfigureAwait(false);

            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(1).ConfigureAwait(false);
                tasksAlreadyPaused &= (actualPauseCount == expectedCount);
                pts.Resume();
                checkTokenStateIsNotPaused |= pts.IsPaused;
                await Task.Delay(1).ConfigureAwait(false);
                pts.Pause();
                checkTokenStateIsPaused &= pts.IsPaused;
                await Task.Delay(1).ConfigureAwait(false);
                hasRunningTask &= (actualPauseCount > expectedCount);
                expectedCount = actualPauseCount;
            }
            cts.Cancel();

            // assert
            Assert.IsTrue(expectedCount >= actualPauseCount, $"Expected: {expectedCount}, Actual: {actualPauseCount}");
            Assert.IsTrue(pts.IsPaused);
            Assert.IsTrue(checkTokenStateIsPaused);
            Assert.IsFalse(checkTokenStateIsNotPaused);
            Assert.IsTrue(tasksAlreadyPaused);
            Assert.IsTrue(hasRunningTask);
        }

        private async Task IncreaseAsync(PauseToken pause, CancellationToken cancel)
        {
            while (cancel.IsCancellationRequested == false)
            {
                await pause.WaitWhilePausedAsync();
                actualPauseCount++;
                await Task.Yield();
            }
        }
    }
}
