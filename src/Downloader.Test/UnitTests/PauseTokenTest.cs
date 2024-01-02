using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Downloader.Test.UnitTests;

public class PauseTokenTest
{
    private PauseTokenSource _pauseTokenSource;
    private volatile int actualPauseCount = 0;

    public PauseTokenTest()
    {
        _pauseTokenSource = new PauseTokenSource();
    }

    [Fact]
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
        var _ = Task.WhenAll(tasks);

        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(1);
            tasksAlreadyPaused &= (actualPauseCount == expectedCount);
            pts.Resume();
            checkTokenStateIsNotPaused |= pts.IsPaused;
            await Task.Delay(1);
            pts.Pause();
            checkTokenStateIsPaused &= pts.IsPaused;
            await Task.Delay(1);
            hasRunningTask &= (actualPauseCount > expectedCount);
            expectedCount = actualPauseCount;
        }
        cts.Cancel();

        // assert
        Assert.True(expectedCount >= actualPauseCount, $"Expected: {expectedCount}, Actual: {actualPauseCount}");
        Assert.True(pts.IsPaused);
        Assert.True(checkTokenStateIsPaused);
        Assert.False(checkTokenStateIsNotPaused);
        Assert.True(tasksAlreadyPaused);
        Assert.True(hasRunningTask);
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

    [Fact]
    public async Task TestPauseAndResume()
    {
        // Verify that a task is not paused initially
        Assert.False(_pauseTokenSource.IsPaused);

        // Pause the token source
        _pauseTokenSource.Pause();

        // Verify that a task is paused
        Assert.True(_pauseTokenSource.IsPaused);

        // Create a task that waits while the token source is paused
        var pauseTask = Task.Run(async () => {
            await _pauseTokenSource.WaitWhilePausedAsync();
            Assert.False(_pauseTokenSource.IsPaused);
        });

        // Wait for a short period of time to ensure that the task is paused
        await Task.Delay(100);
        Assert.True(pauseTask.Status == TaskStatus.WaitingForActivation);

        // Resume the token source
        _pauseTokenSource.Resume();

        // Wait for the task to complete
        await pauseTask;
    }

    [Fact]
    public async Task TestResumeWithoutPause()
    {
        // Verify that a task is not paused initially
        Assert.False(_pauseTokenSource.IsPaused);

        // Resume the token source without pausing first
        _pauseTokenSource.Resume();

        // Verify that a task is still not paused
        Assert.False(_pauseTokenSource.IsPaused);

        // Create a task that waits while the token source is paused
        var pauseTask = Task.Run(async () => {
            await _pauseTokenSource.WaitWhilePausedAsync();
            Assert.False(_pauseTokenSource.IsPaused);
        });

        // Wait for a short period of time to ensure that the task is not paused
        await Task.Delay(100);
        Assert.True(pauseTask.IsCompleted);
    }

    [Fact]
    public async Task TestMultiplePauses()
    {
        // Verify that a task is not paused initially
        Assert.False(_pauseTokenSource.IsPaused);

        // Pause the token source multiple times
        _pauseTokenSource.Pause();
        _pauseTokenSource.Pause();
        _pauseTokenSource.Pause();

        // Verify that a task is paused
        Assert.True(_pauseTokenSource.IsPaused);

        // Create a task that waits while the token source is paused
        var pauseTask = Task.Run(async () => {
            await _pauseTokenSource.WaitWhilePausedAsync();
            Assert.False(_pauseTokenSource.IsPaused);
        });

        // Wait for a short period of time to ensure that the task is paused
        await Task.Delay(100);
        Assert.True(pauseTask.Status == TaskStatus.WaitingForActivation);

        // Resume the token source once
        _pauseTokenSource.Resume();

        // Wait for the task to complete
        await pauseTask;
        Assert.True(pauseTask.Status == TaskStatus.RanToCompletion, $"pauseTask status is: {pauseTask.Status}");
    }
}