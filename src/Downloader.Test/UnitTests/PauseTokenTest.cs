namespace Downloader.Test.UnitTests;

public class PauseTokenTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private readonly PauseTokenSource _pauseTokenSource = new();
    private volatile int _actualPauseCount;

    [Fact]
    public async Task TestPauseTaskWithPauseToken()
    {
        // arrange
        CancellationTokenSource cts = new();
        PauseTokenSource pts = new();
        int expectedCount = 0;
        bool checkTokenStateIsNotPaused = false;
        bool checkTokenStateIsPaused = true;
        bool hasRunningTask = true;
        bool tasksAlreadyPaused = true;

        // act
        pts.Pause();
        Task[] tasks = [
            IncreaseAsync(pts.Token, cts.Token),
            IncreaseAsync(pts.Token, cts.Token),
            IncreaseAsync(pts.Token, cts.Token),
            IncreaseAsync(pts.Token, cts.Token),
            IncreaseAsync(pts.Token, cts.Token)
        ];
        _ = Task.WhenAll(tasks);

        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(1);
            tasksAlreadyPaused &= (_actualPauseCount == expectedCount);
            pts.Resume();
            checkTokenStateIsNotPaused |= pts.IsPaused;
            await Task.Delay(1);
            pts.Pause();
            checkTokenStateIsPaused &= pts.IsPaused;
            await Task.Delay(1);
            hasRunningTask &= (_actualPauseCount > expectedCount);
            Interlocked.Exchange(ref expectedCount, _actualPauseCount);
        }
        await cts.CancelAsync();
        await Task.Delay(10);
        
        // assert
        Assert.True(expectedCount >= _actualPauseCount, $"Expected: {expectedCount}, Actual: {_actualPauseCount}");
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
            Interlocked.Increment(ref _actualPauseCount);
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
        Task pauseTask = Task.Run(async () => {
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
        Task pauseTask = Task.Run(async () => {
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
        Task pauseTask = Task.Run(async () => {
            await _pauseTokenSource.WaitWhilePausedAsync();
            Assert.False(_pauseTokenSource.IsPaused);
        });

        // Wait for a short period of time to ensure that the task is paused
        await Task.Delay(100);
        Assert.Equal(TaskStatus.WaitingForActivation, pauseTask.Status);

        // Resume the token source once
        _pauseTokenSource.Resume();

        // Wait for the task to complete
        await pauseTask;
        Assert.True(pauseTask.Status == TaskStatus.RanToCompletion, $"pauseTask status is: {pauseTask.Status}");
    }
}