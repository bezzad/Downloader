namespace Downloader.Test.IntegrationTests.IssuesTest;

/// <summary>
/// A download must ALWAYS report a terminal state.
/// <para>
/// <see cref="DownloadService.StartDownload"/> ends in one of four branches — cancelled, completed,
/// failed, or "unexpected state". The last one only logged a warning and returned, so no
/// <see cref="AbstractDownloadService.DownloadFileCompleted"/> event was ever raised. An
/// event-driven consumer has no other way to learn the download ended: the awaited task simply
/// finishes and the caller's row sits "downloading" for ever, with no error, no file and nothing to
/// retry. Found from Downloader.Desktop issue #9, where a row stayed Running indefinitely against a
/// server that refused its requests.
/// </para>
/// <para>
/// The state that reaches that branch is reachable through the public API: pausing a download at the
/// moment its chunks finish leaves <c>Status = Paused</c> with no chunk error, which is neither
/// cancelled, completed, nor failed.
/// </para>
/// </summary>
[Collection("Sequential")]
public class CompletionSignalTest(ITestOutputHelper output) : BaseTestClass(output)
{
    /// <summary>
    /// Pausing exactly as the last bytes arrive must still produce a completion event. Whatever state
    /// the download lands in, the caller has to be told it is over.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task PausingAtTheFinishLineStillReportsCompletion()
    {
        // arrange — one chunk, so "the last progress event" really is the end of the transfer.
        int size = DummyFileHelper.FileSize1Kb;
        string url = DummyFileHelper.GetFileUrl(size);
        DownloadConfiguration config = new() {
            ChunkCount = 1,
            ParallelDownload = false,
            MinimumSizeOfChunking = 0
        };
        using DownloadService downloader = new(config);

        TaskCompletionSource<AsyncCompletedEventArgs> completed = new();
        downloader.DownloadFileCompleted += (_, e) => completed.TrySetResult(e);
        // Pause once the bytes are in: the chunk loop is finishing, and the terminal-state check that
        // follows sees Paused rather than Running.
        downloader.DownloadProgressChanged += (_, e) => {
            if (e.ProgressPercentage >= 100)
                downloader.Pause();
        };

        // act
        await downloader.DownloadFileTaskAsync(url);

        // assert — the download is over one way or another, and the caller was told so. Before the fix
        // this timed out: the task returned and the event never came.
        Task finished = await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(finished == completed.Task,
            "the download ended without raising DownloadFileCompleted — a consumer waiting on the "
            + "event has no way to know it is over");
    }

    /// <summary>
    /// The ordinary paths must be unaffected: a successful download still reports exactly one
    /// completion, with no error and not flagged as cancelled.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ASuccessfulDownloadStillReportsCompletionExactlyOnce()
    {
        int size = DummyFileHelper.FileSize1Kb;
        string url = DummyFileHelper.GetFileUrl(size);
        using DownloadService downloader = new(new DownloadConfiguration { ChunkCount = 2 });

        int completions = 0;
        AsyncCompletedEventArgs last = null;
        downloader.DownloadFileCompleted += (_, e) => {
            Interlocked.Increment(ref completions);
            last = e;
        };

        await using Stream stream = await downloader.DownloadFileTaskAsync(url);

        Assert.Equal(1, completions);
        Assert.NotNull(last);
        Assert.Null(last.Error);
        Assert.False(last.Cancelled);
        Assert.Equal(size, stream.Length);
    }

    /// <summary>And a cancelled download reports its own terminal state, once.</summary>
    [Fact(Timeout = 60_000)]
    public async Task ACancelledDownloadStillReportsCompletionExactlyOnce()
    {
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        using DownloadService downloader = new(new DownloadConfiguration { ChunkCount = 1 });

        int completions = 0;
        AsyncCompletedEventArgs last = null;
        downloader.DownloadFileCompleted += (_, e) => {
            Interlocked.Increment(ref completions);
            last = e;
        };
        downloader.DownloadStarted += (_, _) => downloader.CancelAsync();

        await downloader.DownloadFileTaskAsync(url);

        Assert.Equal(1, completions);
        Assert.NotNull(last);
        Assert.True(last.Cancelled);
    }
}