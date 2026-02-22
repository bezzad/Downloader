using Downloader.Exceptions;
using Downloader.Serializer;

namespace Downloader.Test.IntegrationTests;

[Collection("Sequential")]
public class DownloadServiceTest : DownloadService
{
    private readonly ITestOutputHelper TestOutputHelper;
    private string Filename { get; set; }

    public DownloadServiceTest(ITestOutputHelper testOutputHelper)
    {
        Filename = Path.GetRandomFileName();
        TestOutputHelper = testOutputHelper;
        // Create an ILoggerFactory that logs to the ITestOutputHelper
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => {
            builder.AddProvider(new TestOutputLoggerProvider(testOutputHelper));
        });
        AddLogger(loggerFactory.CreateLogger(GetType()));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Package?.ClearChunks();
        if (Package?.Storage != null)
            await Package.Storage.DisposeAsync();

        if (!string.IsNullOrWhiteSpace(Filename))
            File.Delete(Filename);
    }

    private DownloadConfiguration GetDefaultConfig()
    {
        return new DownloadConfiguration {
            BufferBlockSize = 1024,
            ChunkCount = 8,
            ParallelCount = 4,
            ParallelDownload = true,
            MaxTryAgainOnFailure = 5,
            MinimumSizeOfChunking = 0,
            BlockTimeout = 3000,
            RequestConfiguration = new RequestConfiguration {
                ConnectTimeout = 3000,
                AllowAutoRedirect = true,
                KeepAlive = false,
                UserAgent = "test",
            }
        };
    }

    [Fact]
    public async Task CancelAsyncTest()
    {
        // arrange
        AsyncCompletedEventArgs eventArgs = null;
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        DownloadStarted += (_, _) => CancelAsync();
        DownloadFileCompleted += (_, e) => eventArgs = e;

        // act
        await DownloadFileTaskAsync(address);

        // assert
        Assert.True(IsCancelled);
        Assert.NotNull(eventArgs);
        Assert.True(eventArgs.Cancelled);
        Assert.Equal(typeof(TaskCanceledException), eventArgs.Error?.GetType());
    }

    [Fact]
    public async Task CancelTaskAsyncTest()
    {
        // arrange
        AsyncCompletedEventArgs eventArgs = null;
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        DownloadStarted += async (_, _) => await CancelTaskAsync();
        DownloadFileCompleted += (_, e) => eventArgs = e;

        // act
        await DownloadFileTaskAsync(address);

        // assert
        Assert.True(IsCancelled);
        Assert.NotNull(eventArgs);
        Assert.True(eventArgs.Cancelled);
        Assert.Equal(typeof(TaskCanceledException), eventArgs.Error?.GetType());
    }

    [Fact(Timeout = 30_000)]
    public async Task CompletesWithErrorWhenBadUrlTest()
    {
        // arrange
        Exception onCompletionException = null;
        string address = "https://nofile";
        Filename = Path.GetTempFileName();
        Options = GetDefaultConfig();
        Options.RequestConfiguration.ConnectTimeout = 3_000; // if this timeout is not set, the test will fail
        Options.MaxTryAgainOnFailure = 0;
        DownloadStarted += (_, _) => { TestOutputHelper.WriteLine($"Download started"); };
        DownloadProgressChanged += (_, e) => {
            TestOutputHelper.WriteLine($"Download progress changed {e.ProgressPercentage}%");
        };
        DownloadFileCompleted += (_, e) => {
            TestOutputHelper.WriteLine(
                $"Download completed with Error: {e.Error?.Message ?? "false"}, Cancelled: {e.Cancelled}");
            onCompletionException = e.Error;
        };

        // act
        await DownloadFileTaskAsync(address, Filename);

        // assert
        Assert.False(IsBusy);
        Assert.NotNull(onCompletionException);
    }

    [Fact]
    public async Task ClearTest()
    {
        // arrange
        await CancelTaskAsync();

        // act
        await Clear();

        // assert
        Assert.False(IsCancelled);
    }

    [Fact]
    public async Task TestPackageSituationAfterDispose()
    {
        // arrange
        int sampleDataLength = 1024;
        byte[] sampleData = DummyData.GenerateRandomBytes(sampleDataLength);
        Package.TotalFileSize = sampleDataLength * 64;
        Options.ChunkCount = 1;
        new ChunkHub(Options).SetFileChunks(Package);
        Package.BuildStorage(1024 * 1024, Logger);
        await Package.Storage.WriteAsync(0, sampleData, sampleDataLength);
        await Package.Storage.FlushAsync();

        // act
        await base.DisposeAsync();

        // assert
        Assert.NotNull(Package.Chunks);
        Assert.Equal(sampleDataLength, Package.Storage.Length);
        Assert.Equal(sampleDataLength * 64, Package.TotalFileSize);
    }

    [Fact]
    public async Task TestPackageChunksDataAfterDispose()
    {
        // arrange
        int chunkSize = 1024;
        byte[] dummyData = DummyData.GenerateOrderedBytes(chunkSize);
        Options.ChunkCount = 64;
        Package.TotalFileSize = chunkSize * 64;
        Package.BuildStorage(1024 * 1024, Logger);
        new ChunkHub(Options).SetFileChunks(Package);
        foreach (Chunk chunk in Package.Chunks)
        {
            await Package.Storage.WriteAsync(chunk.Start, dummyData, chunkSize);
        }

        // act
        await Package.FlushAsync();
        await base.DisposeAsync();
        Stream stream = Package.Storage.OpenRead();

        // assert
        Assert.NotNull(Package.Chunks);
        for (int i = 0; i < Package.Chunks.Length; i++)
        {
            byte[] buffer = new byte[chunkSize];
            _ = await stream.ReadAsync(buffer.AsMemory(0, chunkSize));
            Assert.True(dummyData.SequenceEqual(buffer));
        }
    }

    [Fact]
    public async Task CancelPerformanceTest()
    {
        // arrange
        AsyncCompletedEventArgs eventArgs = null;
        Stopwatch watch = new();
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        DownloadProgressChanged += async (_, _) => {
            watch.Start();
            await CancelTaskAsync();
        };
        DownloadFileCompleted += (_, e) => eventArgs = e;

        // act
        await DownloadFileTaskAsync(address);
        watch.Stop();

        // assert
        Assert.True(eventArgs?.Cancelled);
        Assert.True(watch.ElapsedMilliseconds < 1000);
        Assert.Equal(4, Options.ParallelCount);
        Assert.Equal(8, Options.ChunkCount);
    }

    [Fact]
    public async Task ResumePerformanceTest()
    {
        // arrange
        AsyncCompletedEventArgs eventArgs = null;
        Stopwatch watch = new();
        bool isCancelled = false;
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        DownloadFileCompleted += (_, e) => eventArgs = e;
        DownloadProgressChanged += async (_, _) => {
            if (isCancelled == false)
            {
                await CancelTaskAsync();
                isCancelled = true;
            }
            else
            {
                watch.Stop();
            }
        };

        // act
        await DownloadFileTaskAsync(address);
        watch.Start();
        await DownloadFileTaskAsync(Package);
        watch.Stop();

        // assert
        Assert.False(eventArgs?.Cancelled);
        Assert.InRange(watch.ElapsedMilliseconds, 0, 2000);
        Assert.Equal(4, Options.ParallelCount);
        Assert.Equal(8, Options.ChunkCount);
    }

    [Fact]
    public async Task PauseResumeTest()
    {
        // arrange
        bool paused = false;
        bool cancelled = false;
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();

        // act
        DownloadProgressChanged += (_, _) => {
            Pause();
            cancelled = IsCancelled;
            paused = IsPaused;
            Resume();
        };
        await DownloadFileTaskAsync(address);

        // assert
        Assert.True(paused);
        Assert.False(cancelled);
        Assert.Equal(4, Options.ParallelCount);
        Assert.Equal(8, Options.ChunkCount);
    }

    [Fact(Timeout = 5000)]
    public async Task CancelAfterPauseTest()
    {
        // clear previous tests affect
        await Clear();
        await Task.Delay(100);

        // arrange
        bool cancelled = false;
        bool pauseStateBeforeCancel = false;
        bool cancelStateBeforeCancel = false;
        bool pauseStateAfterCancel = false;
        bool cancelStateAfterCancel = false;
        bool stateStored = false;
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        SemaphoreSlim semaphore = new(1, 1);

        DownloadFileCompleted += (_, e) => cancelled = e.Cancelled;

        // act
        DownloadProgressChanged += async (_, _) => {
            try
            {
                await semaphore.WaitAsync();

                if (stateStored)
                    return;

                Pause();
                // Add a small delay to ensure the pause state is fully applied
                await Task.Delay(50);
                cancelStateBeforeCancel = IsCancelled;
                pauseStateBeforeCancel = IsPaused;
                await CancelTaskAsync();
                pauseStateAfterCancel = IsPaused;
                cancelStateAfterCancel = IsCancelled;
                stateStored = true;
            }
            finally
            {
                semaphore.Release();
            }
        };
        await DownloadFileTaskAsync(address);

        // assert
        Assert.True(pauseStateBeforeCancel, "Failed to pause before canceling.");
        Assert.False(cancelStateBeforeCancel, "Was cancelled state before canceling.");
        Assert.False(pauseStateAfterCancel, "Can to pause after canceling!");
        Assert.True(cancelStateAfterCancel, "Failed to keep cancel state after canceling.");
        Assert.Equal(4, Options.ParallelCount);
        Assert.Equal(8, Options.ChunkCount);
        Assert.True(cancelled);
        Assert.False(Package.IsSaveComplete);
    }

    [Fact]
    public async Task DownloadParallelNotSupportedUrlTest()
    {
        // arrange
        int actualChunksCount = 0;
        AsyncCompletedEventArgs eventArgs = null;
        string address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        DownloadFileCompleted += (_, e) => eventArgs = e;
        DownloadStarted += (_, _) => {
            actualChunksCount = Package.Chunks.Length;
        };

        // act
        await DownloadFileTaskAsync(address);

        // assert
        Assert.False(Package.IsSupportDownloadInRange);
        Assert.Equal(1, Options.ParallelCount);
        Assert.Equal(1, Options.ChunkCount);
        Assert.False(eventArgs?.Cancelled);
        Assert.True(Package.IsSaveComplete);
        Assert.Null(eventArgs?.Error);
        Assert.Equal(1, actualChunksCount);
    }

    [Fact]
    public async Task ResumeNotSupportedUrlTest()
    {
        // arrange
        AsyncCompletedEventArgs eventArgs = null;
        bool isCancelled = false;
        int actualChunksCount = 0;
        int progressCount = 0;
        int cancelOnProgressNo = 6;
        double maxProgressPercentage = 0d;
        string address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        DownloadFileCompleted += (_, e) => eventArgs = e;
        DownloadProgressChanged += async (_, e) => {
            if (cancelOnProgressNo == progressCount++)
            {
                await CancelTaskAsync();
                isCancelled = true;
            }
            else if (isCancelled)
            {
                actualChunksCount = Package.Chunks.Length;
            }

            maxProgressPercentage = Math.Max(e.ProgressPercentage, maxProgressPercentage);
        };

        // act
        await DownloadFileTaskAsync(address); // start the download
        await DownloadFileTaskAsync(Package); // resume the downland after canceling

        // assert
        Assert.True(isCancelled);
        Assert.False(Package.IsSupportDownloadInRange);
        Assert.Equal(1, Options.ParallelCount);
        Assert.Equal(1, Options.ChunkCount);
        Assert.False(eventArgs?.Cancelled);
        Assert.True(Package.IsSaveComplete);
        Assert.Null(eventArgs?.Error);
        Assert.Equal(1, actualChunksCount);
        Assert.Equal(100, maxProgressPercentage);
    }

    [Fact]
    public async Task ActiveChunksTest()
    {
        // arrange
        List<int> allActiveChunksCount = new(20);
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();

        // act
        DownloadProgressChanged += (_, e) => {
            allActiveChunksCount.Add(e.ActiveChunks);
        };
        await DownloadFileTaskAsync(address);

        // assert
        Assert.Equal(4, Options.ParallelCount);
        Assert.Equal(8, Options.ChunkCount);
        Assert.True(Package.IsSupportDownloadInRange);
        Assert.True(Package.IsSaveComplete);
        foreach (int activeChunks in allActiveChunksCount)
            Assert.True(activeChunks is >= 1 and <= 4);
    }

    [Fact]
    public async Task ActiveChunksWithRangeNotSupportedUrlTest()
    {
        // arrange
        List<int> allActiveChunksCount = new(20);
        string address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();

        // act
        DownloadProgressChanged += (_, e) => {
            allActiveChunksCount.Add(e.ActiveChunks);
        };
        await DownloadFileTaskAsync(address);

        // assert
        Assert.Equal(1, Options.ParallelCount);
        Assert.Equal(1, Options.ChunkCount);
        Assert.False(Package.IsSupportDownloadInRange);
        Assert.True(Package.IsSaveComplete);
        foreach (int activeChunks in allActiveChunksCount)
            Assert.True(activeChunks is >= 1 and <= 4);
    }

    [Fact]
    public async Task ActiveChunksAfterCancelResumeWithNotSupportedUrlTest()
    {
        // arrange
        List<int> allActiveChunksCount = new(20);
        bool isCancelled = false;
        int actualChunksCount = 0;
        int progressCount = 0;
        int cancelOnProgressNo = 6;
        string address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        DownloadProgressChanged += async (_, e) => {
            allActiveChunksCount.Add(e.ActiveChunks);
            if (cancelOnProgressNo == progressCount++)
            {
                await CancelTaskAsync();
                isCancelled = true;
            }
            else if (isCancelled)
            {
                actualChunksCount = Package.Chunks.Length;
            }
        };

        // act
        await DownloadFileTaskAsync(address); // start the download
        await DownloadFileTaskAsync(Package); // resume the downland after canceling

        // assert
        Assert.True(isCancelled);
        Assert.False(Package.IsSupportDownloadInRange);
        Assert.True(Package.IsSaveComplete);
        Assert.Equal(1, actualChunksCount);
        Assert.Equal(1, Options.ParallelCount);
        Assert.Equal(1, Options.ChunkCount);
        foreach (int activeChunks in allActiveChunksCount)
            Assert.True(activeChunks is >= 1 and <= 4);
    }

    [Fact]
    public async Task TestPackageDataAfterCompletionWithSuccess()
    {
        // arrange
        Options.ClearPackageOnCompletionWithFailure = false;
        DownloadServiceEventsState states = new(this);
        string url =
            DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);

        // act
        await DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(url, Package.Urls.First());
        Assert.True(states.DownloadSuccessfulCompleted);
        Assert.True(states.DownloadProgressIsCorrect);
        Assert.Null(states.DownloadError);
        Assert.True(Package.IsSaveComplete);
        Assert.False(Package.IsSaving);
        Assert.Null(Package.Chunks);
    }

    [Fact]
    public async Task TestPackageStatusAfterCompletionWithSuccess()
    {
        // arrange
        string url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName,
            DummyFileHelper.FileSize16Kb);
        DownloadStatus createdStatus = DownloadStatus.None;
        DownloadStatus runningStatus = DownloadStatus.None;
        DownloadStatus pausedStatus = DownloadStatus.None;
        DownloadStatus resumeStatus = DownloadStatus.None;
        DownloadStatus completedStatus = DownloadStatus.None;

        DownloadStarted += (_, _) => createdStatus = Package.Status;
        DownloadProgressChanged += (_, e) => {
            runningStatus = Package.Status;
            if (e.ProgressPercentage is > 50 and < 70)
            {
                Pause();
                pausedStatus = Package.Status;
                Resume();
                resumeStatus = Package.Status;
            }
        };
        DownloadFileCompleted += (_, _) => completedStatus = Package.Status;

        // act
        await DownloadFileTaskAsync(url);

        // assert
        Assert.True(Package.IsSaveComplete);
        Assert.False(Package.IsSaving);
        Assert.Equal(DownloadStatus.Completed, Package.Status);
        Assert.Equal(DownloadStatus.Running, createdStatus);
        Assert.Equal(DownloadStatus.Running, runningStatus);
        Assert.Equal(DownloadStatus.Paused, pausedStatus);
        Assert.Equal(DownloadStatus.Running, resumeStatus);
        Assert.Equal(DownloadStatus.Completed, completedStatus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestSerializePackageAfterCancel(bool onMemory)
    {
        // arrange
        string path = Path.GetTempFileName();
        DownloadPackage package = null;
        string packageText = string.Empty;
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        ChunkDownloadProgressChanged += (_, _) => CancelAsync();
        DownloadFileCompleted += (_, e) => {
            package = e.UserState as DownloadPackage;
            if (package?.Status != DownloadStatus.Completed)
                packageText = System.Text.Json.JsonSerializer.Serialize(package);
        };

        // act
        if (onMemory)
        {
            await DownloadFileTaskAsync(url);
        }
        else
        {
            await DownloadFileTaskAsync(url, path);
        }

        // assert
        Assert.True(IsCancelled);
        Assert.NotNull(package);
        Assert.False(string.IsNullOrWhiteSpace(packageText));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestResumeFromSerializedPackage(bool onMemory)
    {
        // arrange
        bool isCancelOccurred = false;
        string path = Path.GetTempFileName();
        DownloadPackage package = null;
        string packageText = string.Empty;
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        ChunkDownloadProgressChanged += async (_, _) => {
            if (!isCancelOccurred)
            {
                isCancelOccurred = true;
                await CancelTaskAsync();
            }
        };
        DownloadFileCompleted += (_, e) => {
            package = e.UserState as DownloadPackage;
        };

        // act
        if (onMemory)
            await DownloadFileTaskAsync(url);
        else
            await DownloadFileTaskAsync(url, path);

        if (Package?.Status is DownloadStatus.Stopped)
            packageText = System.Text.Json.JsonSerializer.Serialize(Package);

        // resume act
        DownloadPackage reversedPackage = System.Text.Json.JsonSerializer.Deserialize<DownloadPackage>(packageText);
        await DownloadFileTaskAsync(reversedPackage);

        // assert
        Assert.False(IsCancelled);
        Assert.NotNull(package);
        Assert.NotNull(reversedPackage);
        Assert.True(reversedPackage.IsSaveComplete);
        Assert.False(string.IsNullOrWhiteSpace(packageText));
    }

    [Fact]
    public async Task TestPackageStatusAfterCancellation()
    {
        // arrange
        string url =
            DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        DownloadStatus createdStatus = DownloadStatus.None;
        DownloadStatus runningStatus = DownloadStatus.None;
        DownloadStatus cancelledStatus = DownloadStatus.None;
        DownloadStatus completedStatus = DownloadStatus.None;

        DownloadStarted += (_, _) => createdStatus = Package.Status;
        DownloadProgressChanged += async (_, e) => {
            runningStatus = Package.Status;
            if (e.ProgressPercentage is > 50 and < 70)
            {
                await CancelTaskAsync();
                cancelledStatus = Package.Status;
            }
        };
        DownloadFileCompleted += (_, _) => completedStatus = Package.Status;

        // act
        await DownloadFileTaskAsync(url);

        // assert
        Assert.False(Package.IsSaveComplete);
        Assert.False(Package.IsSaving);
        Assert.Equal(DownloadStatus.Stopped, Package.Status);
        Assert.Equal(DownloadStatus.Running, createdStatus);
        Assert.Equal(DownloadStatus.Running, runningStatus);
        Assert.Equal(DownloadStatus.Stopped, cancelledStatus);
        Assert.Equal(DownloadStatus.Stopped, completedStatus);
    }

    [Fact]
    public async Task TestResumeDownloadImmediatelyAfterCancellationAsync()
    {
        // arrange
        bool checkProgress = false;
        double secondStartProgressPercent = -1d;
        string url =
            DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        TaskCompletionSource<bool> tcs = new();
        DownloadFileCompleted += (_, _) => _ = Package.Status;

        // act
        DownloadProgressChanged += async (_, e) => {
            if (secondStartProgressPercent < 0)
            {
                if (checkProgress)
                {
                    checkProgress = false;
                    secondStartProgressPercent = e.ProgressPercentage;
                }
                else if (e.ProgressPercentage is > 50 and < 60)
                {
                    await CancelTaskAsync();
                    checkProgress = true;
                    await DownloadFileTaskAsync(Package);
                    tcs.SetResult(true);
                }
            }
        };
        await DownloadFileTaskAsync(url);
        await tcs.Task;

        // assert
        Assert.True(Package.IsSaveComplete);
        Assert.False(Package.IsSaving);
        Assert.Equal(DownloadStatus.Completed, Package.Status);
        Assert.True(secondStartProgressPercent > 50, $"progress percent is {secondStartProgressPercent}");
    }

    [Fact(Timeout = 5000)]
    public async Task TestStopDownloadOnClearWhenRunning()
    {
        // arrange
        DownloadStatus completedState = DownloadStatus.None;
        string url =
            DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        DownloadFileCompleted += (_, _) => completedState = Package.Status;

        // act
        DownloadProgressChanged += async (_, e) => {
            if (e.ProgressPercentage is > 50 and < 60)
                await Clear();
        };
        await DownloadFileTaskAsync(url);

        // assert
        Assert.False(Package.IsSaveComplete);
        Assert.False(Package.IsSaving);
        Assert.Equal(DownloadStatus.Stopped, completedState);
        Assert.Equal(DownloadStatus.Stopped, Package.Status);
    }

    [Fact(Timeout = 5000)]
    public async Task TestStopDownloadOnClearWhenPaused()
    {
        // arrange
        DownloadStatus completedState = DownloadStatus.None;
        string url =
            DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        DownloadFileCompleted += (_, _) => completedState = Package.Status;

        // act
        DownloadProgressChanged += async (_, e) => {
            if (e.ProgressPercentage is > 50 and < 60)
            {
                Pause();
                await Clear();
            }
        };
        await DownloadFileTaskAsync(url);

        // assert
        Assert.False(Package.IsSaveComplete);
        Assert.False(Package.IsSaving);
        Assert.Equal(DownloadStatus.Stopped, completedState);
        Assert.Equal(DownloadStatus.Stopped, Package.Status);
    }

    [Fact]
    public async Task TestMinimumSizeOfChunking()
    {
        // arrange
        Options = GetDefaultConfig();
        Options.ChunkCount = 16;
        Options.MinimumSizeOfChunking = DummyFileHelper.FileSize16Kb;
        string url =
            DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        int activeChunks = 0;
        int? chunkCounts = null;
        Dictionary<string, bool> progressIds = new();
        ChunkDownloadProgressChanged += (_, e) => {
            activeChunks = Math.Max(activeChunks, e.ActiveChunks);
            progressIds[e.ProgressId] = true;
            chunkCounts ??= Package.Chunks.Length;
        };

        // act
        await DownloadFileTaskAsync(url);

        // assert
        Assert.True(Package.IsSaveComplete);
        Assert.Equal(1, activeChunks);
        Assert.Single(progressIds);
        Assert.Equal(1, chunkCounts);
    }

    [Fact]
    public async Task TestMinimumChunkSize()
    {
        // arrange
        int fileSize = DummyFileHelper.FileSize16Kb;
        Options = GetDefaultConfig();
        Options.ChunkCount = 64;
        Options.MinimumChunkSize = DummyFileHelper.FileSize1Kb;
        int actualChunksCount = (int)Math.Min(Options.ChunkCount, fileSize / Options.MinimumChunkSize);
        string url =
            DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, fileSize);
        int activeChunks = 0;
        int? chunkCounts = null;
        ChunkDownloadProgressChanged += (_, e) => {
            activeChunks = Math.Max(activeChunks, e.ActiveChunks);
            chunkCounts ??= Package.Chunks.Length;
        };

        // act
        await DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(Options.ParallelCount, activeChunks);
        Assert.Equal(actualChunksCount, chunkCounts);
        Assert.True(Package.IsSaveComplete);
    }

    [Fact]
    public async Task TestCreatePathIfNotExist()
    {
        // arrange
        Options = GetDefaultConfig();
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, DummyFileHelper.FileSize1Kb);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        DirectoryInfo dir = new(path);

        // Ensure directory exists before starting download
        dir.Create();

        // Add error handling and logging
        Exception downloadError = null;
        DownloadFileCompleted += (_, e) => {
            if (e.Error != null)
            {
                downloadError = e.Error;
                TestOutputHelper.WriteLine($"Download completed with error: {e.Error}");
            }
        };

        // act
        try
        {
            await DownloadFileTaskAsync(url, dir);
        }
        catch (Exception ex)
        {
            TestOutputHelper.WriteLine($"Download failed with exception: {ex}");
            throw;
        }

        // assert
        Assert.True(Package.IsSaveComplete,
            $"Download did not complete successfully. Status: {Package.Status}, Error: {downloadError?.Message}");
        Assert.StartsWith(dir.FullName, Package.FileName);
        Assert.True(File.Exists(Package.FileName), $"File does not exist at path: {Package.FileName}");
    }

    [Fact]
    public void TestAddLogger()
    {
        // act
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => {
            builder.AddProvider(new TestOutputLoggerProvider(TestOutputHelper));
        });
        AddLogger(loggerFactory.CreateLogger("TestLogger"));

        // assert
        Assert.NotNull(Logger);
    }

    [Fact]
    public async Task DownloadOnCurrentDirectory()
    {
        // arrange
        Options = GetDefaultConfig();
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, DummyFileHelper.FileSize1Kb);

        // act
        await DownloadFileTaskAsync(url, Filename);

        // assert
        Assert.True(Package.IsSaveComplete);
        Assert.Equal(Filename, Package.FileName);
        Assert.True(File.Exists(Package.FileName), "FileName: " + Package.FileName);
    }

    [Fact]
    public async Task DownloadWithFileExistExceptionPolicy()
    {
        // arrange
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        Options.FileExistPolicy = FileExistPolicy.Exception;
        Exception downloadError = null;
        DownloadFileCompleted += (_, e) => {
            downloadError = e.Error;
        };

        // act
        await File.WriteAllTextAsync(Filename, "OK");
        await DownloadFileTaskAsync(address, Filename);

        // assert
        Assert.IsType<FileExistException>(downloadError);

        File.Delete(Filename);
    }

    [Fact]
    public async Task DownloadWithFileExistIgnorePolicy()
    {
        // arrange
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Filename = Path.GetTempFileName();
        Options = GetDefaultConfig();
        Options.FileExistPolicy = FileExistPolicy.IgnoreDownload;

        // act
        await DownloadFileTaskAsync(address, Filename);

        // assert
        Assert.False(Package.IsSaveComplete);
        Assert.Equal(DownloadStatus.Stopped, Package.Status);
        Assert.Equal(Filename, Package.FileName);
        Assert.True(File.Exists(Package.FileName), "FileName: " + Package.FileName);
    }

    [Fact]
    public async Task DownloadWithFileExistRenamePolicy()
    {
        // arrange
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        var filenameWithoutExt = "x" + Guid.NewGuid().ToString("N")[..10];
        var path = Path.Combine(Path.GetTempPath(), filenameWithoutExt);
        var ext = ".test";

        for (int i = 0; i < 10; i++)
        {
            var filename = path + (i > 0 ? $"({i})" + ext : ext);
            await File.WriteAllTextAsync(filename, "OK");
        }

        Options = GetDefaultConfig();
        Options.FileExistPolicy = FileExistPolicy.Rename;

        // act
        await DownloadFileTaskAsync(address, path + ext);

        // assert
        Assert.True(Package.IsSaveComplete);
        Assert.Equal(path + "(10)" + ext, Package.FileName);
        Assert.True(File.Exists(Package.FileName), "FileName: " + Package.FileName);
    }

    [Fact]
    public async Task SerializedPackageStateShouldHaveIncrementalSizeDuringDownload()
    {
        // arrange
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        string path = Path.GetTempFileName();
        int progressCounter = 0;
        int length = 0;
        JsonBinarySerializer serializer = new();
        Options = GetDefaultConfig();
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.EnableAutoResumeDownload = true;
        ChunkDownloadProgressChanged += (_, e) => {
            try
            {
                progressCounter++;
                byte[] pack = serializer.Serialize(Package);
                if (length > pack.Length)
                    Assert.Fail("Should grow the size during download");

                length = pack.Length;
            }
            catch (Exception exception)
            {
                Assert.Fail("Error occurred: " + exception.Message);
            }
        };

        // act
        await DownloadFileTaskAsync(address, path);

        // assert
        Assert.True(Package.IsSaveComplete);
        Assert.Equal(path, Package.FileName);
        Assert.True(progressCounter > 10, $"progressCounter: {progressCounter} > 10");
        Assert.True(File.Exists(Package.FileName), "FileName: " + Package.FileName);
        Assert.True(length > 0, "Pack Size is not growing!");
    }

    [Fact]
    public async Task ResumeDownloadFromExistingFileTest()
    {
        // arrange
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = "download";
        Options.EnableAutoResumeDownload = true;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 8;
        Options.ParallelCount = 8;

        int totalSize = DummyFileHelper.FileSize16Kb * 10;
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "resume_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";
        // Simulate a partially downloaded file with metadata
        // File is pre-allocated to full size (160KB) but only 80KB has been downloaded
        int stoppedDownloadedSize = 80 * 1024;
        bool stopped = false;
        long receivedBytesSizeOnStopping = 0;

        DownloadProgressChanged += (_, e) => {
            if (stopped)
            {
                Assert.True(e.ReceivedBytesSize > receivedBytesSizeOnStopping);
                Assert.True(e.ReceivedBytesSize <= totalSize);
            }
            else if (e.ProgressPercentage > 0 && e.ReceivedBytesSize >= stoppedDownloadedSize)
            {
                // Simulate stopping the download after reaching the stopped downloaded size
                stopped = true;
                CancelAsync();
            }
        };

        try
        {
            TestOutputHelper.WriteLine("Starting download to create a partial file...");

            // act
            await DownloadFileTaskAsync(address, testFile);

            Assert.True(File.Exists(downloadingFile), "Download file should exist during download");
            Assert.False(Package.IsSaveComplete, "Package should not be complete after cancellation");

            var file = new FileInfo(downloadingFile);
            await using (var stream = file.OpenRead())
            {
                var metadataSize = stream.Length - totalSize;
                stream.Seek(totalSize, SeekOrigin.Begin);
                var metadata = new byte[metadataSize];
                await stream.ReadExactlyAsync(metadata);
                var package = Serializer.Deserialize<DownloadPackage>(metadata);
                receivedBytesSizeOnStopping = package.Chunks.Sum(c => c.Position);

                Assert.True(file.Length > totalSize, "Downloading file should be pre-allocated to total size + metadata size");
                Assert.True(metadataSize > 0, "Metadata size should be greater than 0");
                Assert.NotNull(package?.Chunks);
                Assert.Equal(Options.ChunkCount, package.Chunks.Length);
                Assert.All(package.Chunks, c => Assert.True(c.Position >= 0, $"Invalid chunk: {c.Start}..{c.End}, Position={c.Position}"));
                Assert.True(package.Chunks.Any(c => c.Position < c.End - 1), "At least one chunk should be partially downloaded");
                Assert.True(receivedBytesSizeOnStopping > 0);
                Assert.True(receivedBytesSizeOnStopping < totalSize);
            }

            // resume act
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete);
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), "Download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            // Verify the file was downloaded correctly from the beginning
            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            Assert.Equal(expectedData.Length, downloadedData.Length);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine("Validate partial file handling test completed successfully");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFile))
                File.Delete(testFile);
            if (File.Exists(downloadingFile))
                File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task ShouldDownloadWithoutResumeableFeature()
    {
        // arrange
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = "download";
        Options.EnableAutoResumeDownload = false; // Doesn't Resumeable
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 8;
        Options.ParallelCount = 8;

        int totalSize = DummyFileHelper.FileSize16Kb * 10;
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "resume_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";
        // Simulate a partially downloaded file with metadata
        // File is pre-allocated to full size (160KB) but only 80KB has been downloaded
        int stoppedDownloadedSize = 80 * 1024;
        bool stopped = false;
        long receivedBytesSizeOnStopping = 0;

        DownloadProgressChanged += (_, e) => {
            if (stopped)
            {
                Assert.True(e.ReceivedBytesSize > receivedBytesSizeOnStopping);
                Assert.True(e.ReceivedBytesSize <= totalSize);
            }
            else if (!stopped && e.ProgressPercentage > 0 && e.ReceivedBytesSize >= stoppedDownloadedSize)
            {
                // Simulate stopping the download after reaching the stopped downloaded size
                stopped = true;
                CancelAsync();
            }
        };

        try
        {
            TestOutputHelper.WriteLine("Starting download to create a file...");

            // act
            await DownloadFileTaskAsync(address, testFile);

            Assert.True(File.Exists(downloadingFile), "Download file should exist during download");
            Assert.False(Package.IsSaveComplete, "Package should not be complete after cancellation");

            var file = new FileInfo(downloadingFile);
            await using (var stream = file.OpenRead())
            {
                var metadataSize = stream.Length - totalSize;
                receivedBytesSizeOnStopping = Package.Chunks.Sum(c => c.Position);

                Assert.Equal(totalSize, file.Length);
                Assert.Equal(0, metadataSize);
                Assert.NotNull(Package?.Chunks);
                Assert.All(Package.Chunks, c => Assert.True(c.Position >= 0, $"Invalid chunk: {c.Start}..{c.End}, Position={c.Position}"));
                Assert.True(Package.Chunks.Any(c => c.Position < c.End - 1), "At least one chunk should be partially downloaded");
                Assert.True(receivedBytesSizeOnStopping > 0);
                Assert.True(receivedBytesSizeOnStopping < totalSize);
            }

            // resume act
            await DownloadFileTaskAsync(Package);

            // assert
            Assert.True(Package.IsSaveComplete);
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), "Download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            // Verify the file was downloaded correctly from the beginning
            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            Assert.Equal(expectedData.Length, downloadedData.Length);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine("Validate partial file handling test completed successfully");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFile))
                File.Delete(testFile);
            if (File.Exists(downloadingFile))
                File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task ShouldIgnoreFakeMetadataAndDownloadFromBeginning()
    {
        // arrange
        // Create a .download file with the correct pre-allocated size + fake (corrupt) metadata appended.
        // The Downloader should fail to deserialize the fake metadata and start a fresh download.
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = true;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 4;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb;
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "fake_meta_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";

        try
        {
            // Create a fake .download file: pre-allocated data region + garbage metadata
            byte[] fakeData = new byte[totalSize];
            byte[] fakeMetadata = Encoding.UTF8.GetBytes("THIS_IS_NOT_VALID_JSON_METADATA_{corrupt}");

            await using (var fs = new FileStream(downloadingFile, FileMode.Create, FileAccess.Write))
            {
                await fs.WriteAsync(fakeData);
                await fs.WriteAsync(fakeMetadata);
            }

            Assert.True(File.Exists(downloadingFile), "Fake .download file should exist before resume attempt");

            // act - Downloader should detect invalid metadata, delete .download file, and start fresh
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully");
            Assert.True(File.Exists(testFile), "Final file should exist after completion");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            // Verify the file content is correct (downloaded from scratch)
            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine("Fake metadata was correctly ignored and file downloaded from beginning");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task ShouldResumeFromValidPreBuiltMetadata()
    {
        // arrange
        // Create a .download file with partially written data and valid metadata appended at end.
        // The Downloader should read the metadata, detect chunk positions, and resume from them.
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = true;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 4;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb;
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "valid_meta_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";
        long chunkSize = totalSize / 4;

        try
        {
            // Build a valid DownloadPackage with partially downloaded chunks
            var package = new DownloadPackage {
                TotalFileSize = totalSize,
                FileName = testFile,
                DownloadingFileExtension = ".download",
                Urls = [address],
                Status = DownloadStatus.Stopped,
                IsSupportDownloadInRange = true,
                Chunks = new Chunk[4],
            };

            // Simulate: first 2 chunks fully downloaded, last 2 chunks not started
            for (int i = 0; i < 4; i++)
            {
                long start = i * chunkSize;
                long end = (i == 3) ? totalSize - 1 : (i + 1) * chunkSize - 1;
                package.Chunks[i] = new Chunk(start, end) {
                    Position = (i < 2) ? (end - start + 1) : 0, // first 2 fully done, last 2 empty
                    MaxTryAgainOnFailure = 5,
                    Timeout = 3000,
                };
            }

            // Write the partial file data + metadata
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            byte[] metadata = Serializer.Serialize(package);

            await using (var fs = new FileStream(downloadingFile, FileMode.Create, FileAccess.Write))
            {
                // Write the pre-allocated file region (with partial real data for first 2 chunks)
                byte[] fileData = new byte[totalSize];
                Array.Copy(expectedData, 0, fileData, 0, (int)(chunkSize * 2)); // first 2 chunks have real data
                await fs.WriteAsync(fileData);

                // Append metadata at end
                await fs.WriteAsync(metadata);
            }

            Assert.True(File.Exists(downloadingFile), ".download file should exist with pre-built metadata");
            long expectedFileSize = totalSize + metadata.Length;
            Assert.Equal(expectedFileSize, new FileInfo(downloadingFile).Length);

            // Track progress to verify resume starts beyond 0%
            double firstProgressPercent = -1;
            DownloadProgressChanged += (_, e) => {
                if (firstProgressPercent < 0)
                    firstProgressPercent = e.ProgressPercentage;
            };

            // act - should resume from the valid metadata
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully after resume");
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            // Verify first progress > 0 (resumed, not started from scratch)
            Assert.True(firstProgressPercent > 0, $"First progress should be > 0% (was {firstProgressPercent}%), indicating resume occurred");

            // Verify the final file has correct content
            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine($"Resume from pre-built metadata succeeded, first progress was {firstProgressPercent}%");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task ShouldRestartWhenMetadataTotalFileSizeMismatch()
    {
        // arrange
        // Create a .download file with valid metadata but TotalFileSize differs from the actual server file size.
        // The Downloader should detect the mismatch and start a fresh download.
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = true;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 4;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb;
        int wrongTotalSize = totalSize * 2; // metadata says the file is twice as big
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "mismatch_meta_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";

        try
        {
            // Build metadata with wrong TotalFileSize
            var package = new DownloadPackage {
                TotalFileSize = wrongTotalSize, // mismatched!
                FileName = testFile,
                DownloadingFileExtension = ".download",
                Urls = [address],
                Status = DownloadStatus.Stopped,
                IsSupportDownloadInRange = true,
                Chunks = [
                    new Chunk(0, wrongTotalSize - 1) { Position = wrongTotalSize / 2, MaxTryAgainOnFailure = 5, Timeout = 3000, }
                ],
            };

            byte[] metadata = Serializer.Serialize(package);

            // Create .download file sized as if TotalFileSize = totalSize (actual server size)
            await using (var fs = new FileStream(downloadingFile, FileMode.Create, FileAccess.Write))
            {
                byte[] fileData = new byte[totalSize]; // actual size from server
                await fs.WriteAsync(fileData);
                await fs.WriteAsync(metadata); // metadata says wrongTotalSize
            }

            Assert.True(File.Exists(downloadingFile));

            // act - Downloader fetches TotalFileSize from server (= totalSize), reads metadata with wrongTotalSize → mismatch → fresh download
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully");
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine("Metadata with mismatched TotalFileSize was correctly rejected");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task ShouldStartFreshWhenDownloadFileHasNoMetadata()
    {
        // arrange
        // Create a .download file that is exactly TotalFileSize (no metadata appended).
        // The Downloader should detect metadataSize <= 0 and start a fresh download.
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = true;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 4;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb;
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "no_meta_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";

        try
        {
            // Create .download file with exactly TotalFileSize bytes (no metadata at end)
            await using (var fs = new FileStream(downloadingFile, FileMode.Create, FileAccess.Write))
            {
                byte[] fileData = new byte[totalSize]; // zeros, no metadata appended
                await fs.WriteAsync(fileData);
            }

            Assert.True(File.Exists(downloadingFile));
            Assert.Equal(totalSize, new FileInfo(downloadingFile).Length); // no metadata region

            // act
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully");
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine("File with no metadata correctly triggered a fresh download");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task ShouldDeleteDownloadFileAndStartFreshWhenAutoResumeDisabled()
    {
        // arrange
        // Create a .download file with valid metadata, but EnableAutoResumeDownload = false.
        // The Downloader should delete the existing .download file and start from scratch.
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = false; // auto-resume disabled
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 4;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb;
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "no_resume_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";
        long chunkSize = totalSize / 4;

        try
        {
            // Build valid metadata
            var package = new DownloadPackage {
                TotalFileSize = totalSize,
                FileName = testFile,
                DownloadingFileExtension = ".download",
                Urls = [address],
                Status = DownloadStatus.Stopped,
                IsSupportDownloadInRange = true,
                Chunks = new Chunk[4],
            };
            for (int i = 0; i < 4; i++)
            {
                long start = i * chunkSize;
                long end = (i == 3) ? totalSize - 1 : (i + 1) * chunkSize - 1;
                package.Chunks[i] = new Chunk(start, end) {
                    Position = (end - start + 1) / 2, // partially downloaded
                    MaxTryAgainOnFailure = 5,
                    Timeout = 3000,
                };
            }

            byte[] metadata = Serializer.Serialize(package);

            await using (var fs = new FileStream(downloadingFile, FileMode.Create, FileAccess.Write))
            {
                await fs.WriteAsync(new byte[totalSize]); // file data region
                await fs.WriteAsync(metadata); // valid metadata
            }

            Assert.True(File.Exists(downloadingFile));

            // act - should delete .download file, NOT resume
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully");
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine("Auto-resume disabled: ignored existing metadata and downloaded from scratch");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task MetadataShouldGrowDuringDownloadAndBeRemovedOnCompletion()
    {
        // arrange
        // Verifies that during download with EnableAutoResumeDownload = true:
        // 1. The .download file size exceeds TotalFileSize (metadata appended)
        // 2. Metadata size grows over time (never shrinks)
        // 3. After completion the final file is exactly TotalFileSize (metadata removed via SetLength)
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = true;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 8;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb * 10; // 160KB for enough progress events
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "meta_grow_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";
        List<long> observedFileSizes = new();
        long previousMetadataSize = 0;
        bool metadataShrunk = false;

        ChunkDownloadProgressChanged += (_, _) => {
            try
            {
                if (File.Exists(downloadingFile))
                {
                    var fi = new FileInfo(downloadingFile);
                    long currentSize = fi.Length;
                    observedFileSizes.Add(currentSize);

                    if (currentSize > totalSize)
                    {
                        long metadataSize = currentSize - totalSize;
                        if (metadataSize < previousMetadataSize)
                            metadataShrunk = true;
                        previousMetadataSize = metadataSize;
                    }
                }
            }
            catch
            {
                // File might be locked during write, ignore
            }
        };

        try
        {
            // act
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully");
            Assert.True(File.Exists(testFile), "Final file should exist after completion");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);
            Assert.False(metadataShrunk, "Metadata size should never shrink during download");

            // At least some observed file sizes should exceed TotalFileSize (metadata was appended)
            Assert.True(observedFileSizes.Any(s => s > totalSize),
                "At least one observed file size should be > TotalFileSize, proving metadata was appended");

            TestOutputHelper.WriteLine($"Observed {observedFileSizes.Count} file sizes, max metadata overhead: {previousMetadataSize} bytes");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task ShouldResumeAfterMultipleStopResumeCycles()
    {
        // arrange
        // Stop and resume the download 3 times, each time verifying progress advances.
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = true;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 8;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb * 10; // 160KB
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "multi_resume_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";

        try
        {
            int stopCount = 0;
            int maxStops = 3;
            double progressAtStop = 0;
            List<double> progressesAtStop = new();

            DownloadProgressChanged += (_, e) => {
                // Stop at progressively later points: 15%, 40%, 65%
                double stopThreshold = 15 + (stopCount * 25);
                if (stopCount < maxStops && e.ProgressPercentage >= stopThreshold)
                {
                    progressAtStop = e.ProgressPercentage;
                    stopCount++;    
                    CancelAsync();
                }
            };

            // First run: start fresh
            await DownloadFileTaskAsync(address, testFile);
            progressesAtStop.Add(progressAtStop);

            Assert.True(File.Exists(downloadingFile), ".download file should exist after first stop");
            Assert.False(Package.IsSaveComplete, "Package should not be complete after first stop");

            // Second run: resume from .download file
            await DownloadFileTaskAsync(address, testFile);
            progressesAtStop.Add(progressAtStop);

            Assert.True(File.Exists(downloadingFile), ".download file should exist after second stop");
            Assert.False(Package.IsSaveComplete, "Package should not be complete after second stop");

            // Third run: resume again
            await DownloadFileTaskAsync(address, testFile);
            progressesAtStop.Add(progressAtStop);

            Assert.True(File.Exists(downloadingFile), ".download file should exist after third stop");

            // Final run: let it complete
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully after final resume");
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed after completion");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            // Each stop should be at a progressively later point
            for (int i = 1; i < progressesAtStop.Count; i++)
            {
                Assert.True(progressesAtStop[i] >= progressesAtStop[i - 1],
                    $"Progress at stop {i + 1} ({progressesAtStop[i]}%) should be >= stop {i} ({progressesAtStop[i - 1]}%)");
            }

            // Verify final file content
            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine($"Multi stop-resume succeeded. Stops at: {string.Join(", ", progressesAtStop.Select(p => $"{p:F1}%"))}");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task ShouldStartFreshWhenDownloadFileSmallerThanTotalFileSize()
    {
        // arrange
        // Create a .download file that is smaller than TotalFileSize (truncated / corrupt).
        // metadataSize = fileLength - TotalFileSize would be negative → should NOT resume.
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = true;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 4;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb;
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "small_file_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";

        try
        {
            // Create a .download file smaller than TotalFileSize
            int smallSize = totalSize / 2;
            await using (var fs = new FileStream(downloadingFile, FileMode.Create, FileAccess.Write))
            {
                await fs.WriteAsync(new byte[smallSize]);
            }

            Assert.True(File.Exists(downloadingFile));
            Assert.Equal(smallSize, new FileInfo(downloadingFile).Length);

            // act
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully");
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);

            byte[] downloadedData = await File.ReadAllBytesAsync(testFile);
            byte[] expectedData = DummyData.GenerateOrderedBytes(totalSize);
            Assert.Equal(expectedData, downloadedData);

            TestOutputHelper.WriteLine("Truncated .download file correctly triggered fresh download");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }

    [Fact]
    public async Task NoMetadataWrittenToFileWhenAutoResumeDisabled()
    {
        // arrange
        // When EnableAutoResumeDownload = false, the .download file should never exceed TotalFileSize.
        // No metadata should be appended.
        Options = GetDefaultConfig();
        Options.DownloadFileExtension = ".download";
        Options.EnableAutoResumeDownload = false;
        Options.FileExistPolicy = FileExistPolicy.Delete;
        Options.ChunkCount = 8;
        Options.ParallelCount = 4;

        int totalSize = DummyFileHelper.FileSize16Kb * 10;
        string address = DummyFileHelper.GetFileUrl(totalSize);
        string testFile = Path.Combine(Path.GetTempPath(), "no_meta_written_test_" + Guid.NewGuid().ToString("N") + ".dat");
        string downloadingFile = testFile + ".download";
        bool anyFileSizeExceededTotal = false;

        ChunkDownloadProgressChanged += (_, _) => {
            try
            {
                if (File.Exists(downloadingFile))
                {
                    long size = new FileInfo(downloadingFile).Length;
                    if (size > totalSize)
                        anyFileSizeExceededTotal = true;
                }
            }
            catch { /* file might be locked */ }
        };

        try
        {
            // act
            await DownloadFileTaskAsync(address, testFile);

            // assert
            Assert.True(Package.IsSaveComplete, "Download should complete successfully");
            Assert.True(File.Exists(testFile), "Final file should exist");
            Assert.False(File.Exists(downloadingFile), ".download file should be removed");
            Assert.Equal(totalSize, new FileInfo(testFile).Length);
            Assert.False(anyFileSizeExceededTotal,
                "File size should never exceed TotalFileSize when auto-resume is disabled");

            TestOutputHelper.WriteLine("Confirmed no metadata was appended when auto-resume is disabled");
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
            if (File.Exists(downloadingFile)) File.Delete(downloadingFile);
        }
    }
}