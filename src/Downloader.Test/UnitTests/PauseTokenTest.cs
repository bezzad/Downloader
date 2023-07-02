using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class PauseTokenTest
    {
        private PauseTokenSource _pauseTokenSource;
        private volatile int actualPauseCount = 0;

        [TestInitialize]
        public void Initialize()
        {
            _pauseTokenSource = new PauseTokenSource();
        }

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

        [TestMethod]
        public async Task TestPauseAndResume()
        {
            // Verify that a task is not paused initially
            Assert.IsFalse(_pauseTokenSource.IsPaused);

            // Pause the token source
            _pauseTokenSource.Pause();

            // Verify that a task is paused
            Assert.IsTrue(_pauseTokenSource.IsPaused);

            // Create a task that waits while the token source is paused
            var pauseTask = Task.Run(async () => {
                await _pauseTokenSource.WaitWhilePausedAsync();
                Assert.IsFalse(_pauseTokenSource.IsPaused);
            });

            // Wait for a short period of time to ensure that the task is paused
            await Task.Delay(100);
            Assert.IsTrue(pauseTask.Status == TaskStatus.WaitingForActivation);

            // Resume the token source
            _pauseTokenSource.Resume();

            // Wait for the task to complete
            await pauseTask;
        }

        [TestMethod]
        public async Task TestResumeWithoutPause()
        {
            // Verify that a task is not paused initially
            Assert.IsFalse(_pauseTokenSource.IsPaused);

            // Resume the token source without pausing first
            _pauseTokenSource.Resume();

            // Verify that a task is still not paused
            Assert.IsFalse(_pauseTokenSource.IsPaused);

            // Create a task that waits while the token source is paused
            var pauseTask = Task.Run(async () => {
                await _pauseTokenSource.WaitWhilePausedAsync();
                Assert.IsFalse(_pauseTokenSource.IsPaused);
            });

            // Wait for a short period of time to ensure that the task is not paused
            await Task.Delay(100);
            Assert.IsTrue(pauseTask.IsCompleted);
        }

        [TestMethod]
        public async Task TestMultiplePauses()
        {
            // Verify that a task is not paused initially
            Assert.IsFalse(_pauseTokenSource.IsPaused);

            // Pause the token source multiple times
            _pauseTokenSource.Pause();
            _pauseTokenSource.Pause();

            // Verify that a task is paused
            Assert.IsTrue(_pauseTokenSource.IsPaused);

            // Create a task that waits while the token source is paused
            var pauseTask = Task.Run(async () =>
            {
                await _pauseTokenSource.WaitWhilePausedAsync();
                Assert.IsFalse(_pauseTokenSource.IsPaused);
            });

            // Wait for a short period of time to ensure that the task is paused
            await Task.Delay(100);
            Assert.IsTrue(pauseTask.Status == TaskStatus.WaitingForActivation);

            // Resume the token source once
            _pauseTokenSource.Resume();

            // Wait for a short period of time to ensure that the task is still paused
            await Task.Delay(100);
            Assert.IsTrue(pauseTask.Status == TaskStatus.RanToCompletion);

            // Resume the token source again
            _pauseTokenSource.Resume();

            // Wait for the task to complete
            await pauseTask;

            // Verify that the task completed successfully
            Assert.IsTrue(pauseTask.Status == TaskStatus.RanToCompletion);
        }
    }
}
