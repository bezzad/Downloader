namespace Downloader.Test.IntegrationTests;

/// <summary>
/// Reproduces the "resume restarts from 0%" scenarios reported from Downloader.Desktop:
/// a partially downloaded file whose attempt hits a network timeout or a failing /
/// half-responsive server must never lose the already-downloaded chunk positions,
/// and a later retry must continue from the last position instead of 0%.
/// </summary>
[Collection("Sequential")]
public class ResumeAfterFailureTest(ITestOutputHelper output) : BaseTestClass(output)
{
    // ~1MB, deliberately NOT a multiple of 256×4: the dummy server's content repeats every
    // 256 bytes, so 256-aligned chunk offsets would hide bytes written at wrong positions.
    private const int Size = 1024 * 1024 + 4;

    private static DownloadConfiguration NewConfig() => new() {
        ChunkCount = 4,
        ParallelCount = 4,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0, // allow the 1MB file to be chunked
        MaxTryAgainOnFailure = 2,  // exhaust per-chunk retries quickly
        BlockTimeout = 500,        // keep retry backoff short for the test
        BufferBlockSize = 8192
    };

    /// <summary>
    /// The server dies mid-stream (503 or 504) after ~900KB of a 1MB file has been served.
    /// The download must fail, but the chunk positions already received must survive inside
    /// the package so the next attempt resumes instead of restarting from 0%.
    /// </summary>
    [Theory(Timeout = 60_000)]
    [InlineData(false)] // server drops the connection with 503 mid-stream
    [InlineData(true)]  // server times out (504) mid-stream
    public async Task FailedDownloadMustNotWipeDownloadedChunks(bool timeout)
    {
        // arrange
        const int failureOffset = 900_000;
        string url = timeout
            ? DummyFileHelper.GetFileWithTimeoutAfterOffset(Size, failureOffset)
            : DummyFileHelper.GetFileWithFailureAfterOffset(Size, failureOffset);
        await using DownloadService downloader = new(NewConfig(), LogFactory);
        object sync = new();
        long maxReceived = 0;
        long resetTo = -1; // first progress value observed after a backward jump
        downloader.DownloadProgressChanged += (_, e) => {
            lock (sync)
            {
                // Parallel chunk events can be delivered slightly out of order, so tolerate small
                // backward jitter; a real chunk-state wipe falls back by hundreds of KB.
                if (resetTo < 0 && e.ReceivedBytesSize < maxReceived - 128 * 1024)
                    resetTo = e.ReceivedBytesSize;
                maxReceived = Math.Max(maxReceived, e.ReceivedBytesSize);
            }
        };

        // act — must fail, the server always dies at failureOffset
        await downloader.DownloadFileTaskAsync(url);

        // assert
        Output.WriteLine($"maxReceived={maxReceived}, resetTo={resetTo}, " +
                         $"final={downloader.Package.ReceivedBytesSize}, status={downloader.Package.Status}");
        Assert.Equal(DownloadStatus.Failed, downloader.Package.Status);
        Assert.True(resetTo < 0,
            $"Progress was reset while downloading: fell from {maxReceived} back to {resetTo} bytes");
        Assert.True(downloader.Package.ReceivedBytesSize > 0,
            "All downloaded bytes were lost after the failure; a retry would restart from 0%");
    }

    /// <summary>
    /// Stop/resume many times on a healthy server: every resume must continue from the last
    /// received byte count, never fall back to 0%, and the final file must be intact.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task StopResumeManyTimesNeverRestartsFromZero()
    {
        // arrange
        string path = GetTempNoFilename();
        string url = DummyFileHelper.GetFileWithNameUrl(Path.GetFileName(path), Size);
        await using DownloadService downloader = new(NewConfig(), LogFactory);
        long sessionStart = 0;
        bool cancelOnProgress = true;
        downloader.DownloadProgressChanged += (_, e) => {
            if (cancelOnProgress && e.ReceivedBytesSize - Interlocked.Read(ref sessionStart) > 150_000)
                downloader.CancelAsync();
        };

        // act — first attempt, cancelled mid-way
        await downloader.DownloadFileTaskAsync(url, path);

        List<string> history = [$"first: 0 -> {downloader.Package.ReceivedBytesSize} ({downloader.Package.Status})"];
        int attempts = 0;
        while (!downloader.Package.IsSaveComplete && attempts++ < 8)
        {
            long before = downloader.Package.ReceivedBytesSize;
            Interlocked.Exchange(ref sessionStart, before);
            cancelOnProgress = attempts <= 3; // cancel the first 3 resumes, then let it finish

            await downloader.DownloadFileTaskAsync(downloader.Package); // resume

            history.Add($"attempt {attempts}: {before} -> {downloader.Package.ReceivedBytesSize} " +
                        $"({downloader.Package.Status})");
            if (!downloader.Package.IsSaveComplete) // completion clears the chunks (=0), skip check
            {
                Assert.True(downloader.Package.ReceivedBytesSize >= before,
                    "Progress went backward across resumes: " + string.Join("; ", history));
            }
        }

        // assert
        Output.WriteLine(string.Join(Environment.NewLine, history));
        Assert.True(downloader.Package.IsSaveComplete,
            "Download never completed after resumes: " + string.Join("; ", history));
        Assert.True(DummyData.GenerateOrderedBytes(Size).AreEqual(File.OpenRead(downloader.Package.FileName)),
            "Downloaded bytes do not match expected content");
        File.Delete(downloader.Package.FileName);
    }

    /// <summary>
    /// The exact Desktop scenario: a partial download is resumed while the server/internet is
    /// unreachable → that attempt fails. Retrying once the server is back must continue from
    /// the last saved position, not from 0%.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ResumeAfterServerUnreachableKeepsProgress()
    {
        // arrange
        string path = GetTempNoFilename();
        string url = DummyFileHelper.GetFileWithNameUrl(Path.GetFileName(path), Size);
        const string deadUrl = "http://127.0.0.1:1/dead.bin"; // nothing listens here → connection refused
        await using DownloadService downloader = new(NewConfig(), LogFactory);
        bool cancelOnProgress = true;
        long resumedTransfer = 0;
        bool countTransfer = false;
        downloader.DownloadProgressChanged += (_, e) => {
            if (countTransfer)
                Interlocked.Add(ref resumedTransfer, e.ProgressedByteSize);
            if (cancelOnProgress && e.ReceivedBytesSize > 300_000)
                downloader.CancelAsync();
        };

        // act 1 — partial download, then stop
        await downloader.DownloadFileTaskAsync(url, path);
        cancelOnProgress = false;
        long beforeOutage = downloader.Package.ReceivedBytesSize;
        Assert.True(beforeOutage > 0, "Test setup failed: nothing was downloaded before cancelling");

        // act 2 — resume while the server is unreachable → must fail but keep the progress
        await downloader.DownloadFileTaskAsync(downloader.Package, deadUrl);
        Output.WriteLine($"after outage attempt: status={downloader.Package.Status}, " +
                         $"received={downloader.Package.ReceivedBytesSize} (before={beforeOutage})");
        Assert.Equal(DownloadStatus.Failed, downloader.Package.Status);
        Assert.Equal(beforeOutage, downloader.Package.ReceivedBytesSize);

        // act 3 — server is back: resume must transfer only the remaining bytes
        countTransfer = true;
        await downloader.DownloadFileTaskAsync(downloader.Package, url);

        // assert
        long transferred = Interlocked.Read(ref resumedTransfer);
        Output.WriteLine($"resumed transfer={transferred}, remaining was={Size - beforeOutage}");
        Assert.True(downloader.Package.IsSaveComplete);
        Assert.True(DummyData.GenerateOrderedBytes(Size).AreEqual(File.OpenRead(downloader.Package.FileName)),
            "Downloaded bytes do not match expected content");
        Assert.True(transferred <= Size - beforeOutage + 128 * 1024,
            $"Resume restarted from scratch: re-transferred {transferred} bytes " +
            $"instead of the remaining {Size - beforeOutage}");
        File.Delete(downloader.Package.FileName);
    }

    /// <summary>
    /// Resume against a server that transiently rejects Range requests with 503 (a flaky
    /// CDN/proxy during resume). The attempt may fail, but it must not discard the downloaded
    /// half — a later retry against the recovered server must finish from the last position.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task ResumeWhenRangeRequestsTransientlyFailKeepsProgress()
    {
        // arrange
        string filename = Path.GetRandomFileName();
        string path = Path.Combine(Path.GetTempPath(), filename);
        string url = DummyFileHelper.GetFileWithNameUrl(filename, Size);
        // Same content, but every Range request is answered 503 while a plain GET succeeds.
        string failRangeUrl = DummyFileHelper.GetFileFailingOnRangeRequestsUrl(filename, Size);
        await using DownloadService downloader = new(NewConfig(), LogFactory);
        bool cancelOnProgress = true;
        long resumedTransfer = 0;
        bool countTransfer = false;
        downloader.DownloadProgressChanged += (_, e) => {
            if (countTransfer)
                Interlocked.Add(ref resumedTransfer, e.ProgressedByteSize);
            if (cancelOnProgress && e.ReceivedBytesSize > 400_000)
                downloader.CancelAsync();
        };

        // act 1 — partial download, then stop
        await downloader.DownloadFileTaskAsync(url, path);
        cancelOnProgress = false;
        long beforeResume = downloader.Package.ReceivedBytesSize;
        Assert.True(beforeResume > 0, "Test setup failed: nothing was downloaded before cancelling");

        // act 2 — resume against the range-rejecting server → fails, but keeps the progress
        await downloader.DownloadFileTaskAsync(downloader.Package, failRangeUrl);
        Output.WriteLine($"after range-rejection attempt: status={downloader.Package.Status}, " +
                         $"received={downloader.Package.ReceivedBytesSize} (before={beforeResume})");
        Assert.Equal(DownloadStatus.Failed, downloader.Package.Status);
        Assert.True(downloader.Package.ReceivedBytesSize >= beforeResume,
            $"Range-rejection attempt wiped the progress: {downloader.Package.ReceivedBytesSize} " +
            $"of previously {beforeResume} bytes left");

        // act 3 — the server recovered: retry must transfer only the remaining bytes
        long beforeRetry = downloader.Package.ReceivedBytesSize;
        countTransfer = true;
        await downloader.DownloadFileTaskAsync(downloader.Package, url);

        // assert
        long transferred = Interlocked.Read(ref resumedTransfer);
        Output.WriteLine($"retry transfer={transferred}, remaining was={Size - beforeRetry}");
        Assert.True(downloader.Package.IsSaveComplete,
            $"Download did not complete; status={downloader.Package.Status}");
        Assert.True(DummyData.GenerateOrderedBytes(Size).AreEqual(File.OpenRead(downloader.Package.FileName)),
            "Resumed file content is corrupted");
        Assert.True(transferred <= Size - beforeRetry + 128 * 1024,
            $"Retry restarted from scratch: re-transferred {transferred} bytes " +
            $"instead of the remaining {Size - beforeRetry}");
        File.Delete(downloader.Package.FileName);
    }

    /// <summary>
    /// Resume against a server that no longer advertises range support. Restarting is then
    /// unavoidable, but the multi-chunk state left in the package must be rebuilt correctly —
    /// the file must never be silently assembled from bytes written at wrong offsets.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task ResumeAgainstNoRangeServerMustNotCorruptFile()
    {
        // arrange
        string filename = Path.GetRandomFileName();
        string path = Path.Combine(Path.GetTempPath(), filename);
        string url = DummyFileHelper.GetFileWithNameUrl(filename, Size);
        string noRangeUrl = DummyFileHelper.GetFileWithNoAcceptRangeUrl(filename, Size);
        await using DownloadService downloader = new(NewConfig(), LogFactory);
        bool cancelOnProgress = true;
        downloader.DownloadProgressChanged += (_, e) => {
            if (cancelOnProgress && e.ReceivedBytesSize > 400_000)
                downloader.CancelAsync();
        };

        // act 1 — partial multi-chunk download, then stop
        await downloader.DownloadFileTaskAsync(url, path);
        cancelOnProgress = false;
        Assert.True(downloader.Package.ReceivedBytesSize > 0,
            "Test setup failed: nothing was downloaded before cancelling");

        // act 2 — resume, but now the server reports no range support
        await downloader.DownloadFileTaskAsync(downloader.Package, noRangeUrl);

        // assert
        Output.WriteLine($"status={downloader.Package.Status}");
        Assert.True(downloader.Package.IsSaveComplete,
            $"Download did not complete; status={downloader.Package.Status}");
        Assert.True(DummyData.GenerateOrderedBytes(Size).AreEqual(File.OpenRead(downloader.Package.FileName)),
            "File was silently corrupted after resuming against a no-range server");
        File.Delete(downloader.Package.FileName);
    }
}
