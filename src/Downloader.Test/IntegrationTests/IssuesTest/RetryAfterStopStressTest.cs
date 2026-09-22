using System.Runtime.ExceptionServices;

namespace Downloader.Test.IntegrationTests.IssuesTest;

/// <summary>
/// Stress harness for the "NullReferenceException on a retry after a stop" report: a download that
/// actually succeeds (100%, file on disk) reports DownloadFileCompleted with an NRE.
/// Mirrors what Downloader.Desktop does: cancel -> release (dispose off-stack from inside the
/// completion handler) -> drop the reference -> fresh service, same url, same folder.
/// <para>
/// The default run walks every axis once (cancel point x chunk count x parallel on/off x server
/// variant x throttled/instant). For a longer soak set <c>NRE_STRESS_ITERATIONS</c> (and
/// <c>NRE_STRESS_LANES</c> for how many sequences run at once).
/// </para>
/// </summary>
[Collection("Sequential")]
public class RetryAfterStopStressTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private enum CancelWhen { Immediately, AfterFirstBytes, AtHalf, AtCompletion, RandomDelay }

    private sealed record Axis(int ChunkCount, bool Parallel, CancelWhen Cancel, string ServerVariant,
        long SpeedLimit = 0);

    private static string UrlFor(string variant, string name, int size) => variant switch {
        "range" => DummyFileHelper.GetFileWithNameUrl(name, size),
        "norange" => DummyFileHelper.GetFileWithNoAcceptRangeUrl(name, size),
        "noheader" => DummyFileHelper.GetFileWithoutHeaderUrl(name, size),
        _ => throw new ArgumentOutOfRangeException(nameof(variant))
    };

    private readonly List<string> _nres = [];
    private readonly Lock _gate = new();
    private int _nreCount;
    private int _firstCompleted;
    private int _firstStopped;
    private int _firstOther;
    private int _retryCompleted;
    private int _retryTransferred;
    private int _retrySkipped;
    private int _retryResumesPartial;

    [Fact(Timeout = 1_800_000)]
    public async Task StressRetryAfterStop()
    {
        const int size = 64 * 1024;
        int iterations = int.TryParse(Environment.GetEnvironmentVariable("NRE_STRESS_ITERATIONS"), out int it)
            ? it
            : 180; // one pass over every axis
        int lanes = int.TryParse(Environment.GetEnvironmentVariable("NRE_STRESS_LANES"), out int l) ? l : 4;

        EventHandler<FirstChanceExceptionEventArgs> firstChance = (_, e) => {
            if (e.Exception is not NullReferenceException)
                return;

            Interlocked.Increment(ref _nreCount);
            lock (_gate)
            {
                if (_nres.Count < 5)
                    _nres.Add(new StackTrace(e.Exception, true).ToString() + "\n--- throw site ---\n" +
                              new StackTrace(true));
            }
        };
        AppDomain.CurrentDomain.FirstChanceException += firstChance;

        List<Axis> axes = [];
        foreach (string variant in new[] { "range", "norange", "noheader" })
        foreach (int cc in new[] { 8, 2, 1 })
        foreach (bool parallel in new[] { true, false })
        foreach (CancelWhen when in Enum.GetValues<CancelWhen>())
        foreach (long speed in new[] { 0L, 48L * 1024 })
            axes.Add(new Axis(cc, parallel, when, variant, speed));

        int failures = 0;
        int next = -1;
        try
        {
            Task[] workers = Enumerable.Range(0, lanes).Select(_ => Task.Run(async () => {
                while (true)
                {
                    int i = Interlocked.Increment(ref next);
                    if (i >= iterations || Volatile.Read(ref failures) >= 3)
                        return;

                    Axis axis = axes[i % axes.Count];
                    string folder = Path.Combine(Path.GetTempPath(), "nre-stress", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(folder);
                    string url = UrlFor(axis.ServerVariant, $"stress{i}.bin", size);
                    try
                    {
                        await RunAttemptOneAndCancel(axis, url, folder);
                        if (Directory.EnumerateFiles(folder, "*.download").Any(f => new FileInfo(f).Length > 0))
                            Interlocked.Increment(ref _retryResumesPartial);
                        AsyncCompletedEventArgs second = await RunRetry(axis, url, folder);
                        if (second?.Error is not null)
                        {
                            Interlocked.Increment(ref failures);
                            lock (_gate)
                            {
                                Output.WriteLine($"[FAIL] iteration {i} axis={axis}");
                                Output.WriteLine($"   {second.Error.GetType().Name}: {second.Error.Message}");
                                Output.WriteLine($"   stack: {second.Error.StackTrace}");
                            }
                        }
                    }
                    finally
                    {
                        try { Directory.Delete(folder, true); }
                        catch { /* best effort */ }
                    }
                }
            })).ToArray();

            await Task.WhenAll(workers);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= firstChance;
        }

        lock (_gate)
        {
            Output.WriteLine($"iterations={iterations} lanes={lanes} surfacedFailures={failures} " +
                             $"firstChanceNREs={_nreCount}");
            Output.WriteLine($"attempt1: completed={_firstCompleted} stopped={_firstStopped} other={_firstOther}");
            Output.WriteLine($"retry: completed={_retryCompleted} transferredBytes={_retryTransferred} " +
                             $"skippedByPolicy={_retrySkipped} resumedPartial={_retryResumesPartial}");
            foreach (string s in _nres)
                Output.WriteLine("=== NRE ===\n" + s);
        }

        Assert.Equal(0, _nreCount);
        Assert.Equal(0, failures);
    }

    private async Task RunAttemptOneAndCancel(Axis axis, string url, string folder)
    {
        DownloadService first = new(NewConfig(axis));
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // Exactly Downloader.Desktop's release: dispose off-stack from inside the completion handler,
        // dropping the reference immediately.
        first.DownloadFileCompleted += (_, e) => {
            if (first.Status == DownloadStatus.Completed) Interlocked.Increment(ref _firstCompleted);
            else if (e.Cancelled) Interlocked.Increment(ref _firstStopped);
            else Interlocked.Increment(ref _firstOther);
            _ = Task.Run(async () => {
                try { await first.DisposeAsync(); }
                catch { /* best effort */ }
            });
            done.TrySetResult();
        };
        first.DownloadProgressChanged += (_, e) => {
            switch (axis.Cancel)
            {
                case CancelWhen.AfterFirstBytes when e.ReceivedBytesSize > 0:
                case CancelWhen.AtHalf when e.ProgressPercentage >= 50:
                case CancelWhen.AtCompletion when e.ProgressPercentage >= 100:
                    first.CancelAsync();
                    break;
            }
        };

        Task download = first.DownloadFileTaskAsync(url, new DirectoryInfo(folder));
        if (axis.Cancel == CancelWhen.Immediately)
            first.CancelAsync();
        else if (axis.Cancel == CancelWhen.RandomDelay)
        {
            // Sample the whole timeline — including the size probe (GET bytes=0-0) and the instant
            // the last chunk lands — instead of only the points a progress event can name.
            int delayUs = Random.Shared.Next(0, axis.SpeedLimit > 0 ? 1_600_000 : 40_000);
            _ = Task.Run(async () => {
                long until = Stopwatch.GetTimestamp() + (delayUs * (Stopwatch.Frequency / 1_000_000));
                while (Stopwatch.GetTimestamp() < until)
                    await Task.Yield();
                first.CancelAsync();
            });
        }

        try { await download; }
        catch { /* the stop may surface as an exception; the sequence continues either way */ }

        await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(15)));
    }

    private async Task<AsyncCompletedEventArgs> RunRetry(Axis axis, string url, string folder)
    {
        DownloadService second = new(NewConfig(axis));
        AsyncCompletedEventArgs result = null;
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        second.DownloadFileCompleted += (_, e) => {
            result = e;
            if (second.Status == DownloadStatus.Completed) Interlocked.Increment(ref _retryCompleted);
            if ((e.UserState as DownloadPackage)?.ReceivedBytesSize > 0) Interlocked.Increment(ref _retryTransferred);
            if (e.Cancelled && e.Error is null && second.Status != DownloadStatus.Completed)
                Interlocked.Increment(ref _retrySkipped);
            _ = Task.Run(async () => {
                try { await second.DisposeAsync(); }
                catch { /* best effort */ }
            });
            done.TrySetResult();
        };

        try { await second.DownloadFileTaskAsync(url, new DirectoryInfo(folder)); }
        catch (Exception ex)
        {
            lock (_gate)
                Output.WriteLine($"   retry threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }

        await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        return result;
    }

    private static DownloadConfiguration NewConfig(Axis axis) => new() {
        // Downloader.Desktop's defaults, which is where the report comes from.
        ChunkCount = axis.ChunkCount,
        ParallelDownload = axis.Parallel,
        ParallelCount = 0,
        BufferBlockSize = 8192,
        MaxTryAgainOnFailure = 5,
        BlockTimeout = 5000,
        MinimumSizeOfChunking = 512,
        // A throttled attempt runs for seconds instead of milliseconds: the stop lands mid-transfer
        // (so the retry really resumes a partial .download file) and the once-per-second resume
        // metadata write happens more than once, which is what a slow CI machine does naturally.
        MaximumBytesPerSecond = axis.SpeedLimit,
        EnableAutoResumeDownload = true,
        ClearPackageOnCompletionWithFailure = false,
        FileExistPolicy = FileExistPolicy.IgnoreDownload
    };
}
