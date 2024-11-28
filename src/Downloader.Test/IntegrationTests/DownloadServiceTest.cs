namespace Downloader.Test.IntegrationTests;

[Collection("Sequential")]
public class DownloadServiceTest : DownloadService
{
    protected readonly ITestOutputHelper TestOutputHelper;
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
        Package?.Clear();
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
            MaxTryAgainOnFailover = 5,
            MinimumSizeOfChunking = 0,
            Timeout = 3000,
            RequestConfiguration = new RequestConfiguration {
                Timeout = 3000,
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
        Options.MaxTryAgainOnFailover = 0;
        DownloadFileCompleted += (_, e) => {
            onCompletionException = e.Error;
        };

        // act
        await DownloadFileTaskAsync(address, Filename);

        // assert
        Assert.False(IsBusy);
        Assert.NotNull(onCompletionException);
        Assert.Equal(typeof(WebException), onCompletionException.GetType());
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
        var sampleDataLength = 1024;
        var sampleData = DummyData.GenerateRandomBytes(sampleDataLength);
        Package.TotalFileSize = sampleDataLength * 64;
        Options.ChunkCount = 1;
        new ChunkHub(Options).SetFileChunks(Package);
        Package.BuildStorage(false, 1024 * 1024);
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
        var chunkSize = 1024;
        var dummyData = DummyData.GenerateOrderedBytes(chunkSize);
        Options.ChunkCount = 64;
        Package.TotalFileSize = chunkSize * 64;
        Package.BuildStorage(false, 1024 * 1024);
        new ChunkHub(Options).SetFileChunks(Package);
        foreach (Chunk chunk in Package.Chunks)
        {
            await Package.Storage.WriteAsync(chunk.Start, dummyData, chunkSize);
        }

        // act
        await Package.FlushAsync();
        await base.DisposeAsync();
        var stream = Package.Storage.OpenRead();

        // assert
        Assert.NotNull(Package.Chunks);
        for (int i = 0; i < Package.Chunks.Length; i++)
        {
            var buffer = new byte[chunkSize];
            _ = await stream.ReadAsync(buffer, 0, chunkSize);
            Assert.True(dummyData.SequenceEqual(buffer));
        }
    }

    [Fact]
    public async Task CancelPerformanceTest()
    {
        // arrange
        AsyncCompletedEventArgs eventArgs = null;
        var watch = new Stopwatch();
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
        var watch = new Stopwatch();
        var isCancelled = false;
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
        var paused = false;
        var cancelled = false;
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

    [Fact]
    public async Task CancelAfterPauseTest()
    {
        // arrange
        AsyncCompletedEventArgs eventArgs = null;
        var pauseStateBeforeCancel = false;
        var cancelStateBeforeCancel = false;
        var pauseStateAfterCancel = false;
        var cancelStateAfterCancel = false;
        string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        DownloadFileCompleted += (_, e) => eventArgs = e;

        // act
        DownloadProgressChanged += async (_, _) => {
            Pause();
            cancelStateBeforeCancel = IsCancelled;
            pauseStateBeforeCancel = IsPaused;
            await CancelTaskAsync();
            pauseStateAfterCancel = IsPaused;
            cancelStateAfterCancel = IsCancelled;
        };
        await DownloadFileTaskAsync(address);

        // assert
        Assert.True(pauseStateBeforeCancel);
        Assert.False(cancelStateBeforeCancel);
        Assert.False(pauseStateAfterCancel);
        Assert.True(cancelStateAfterCancel);
        Assert.Equal(4, Options.ParallelCount);
        Assert.Equal(8, Options.ChunkCount);
        Assert.Equal(8, Options.ChunkCount);
        Assert.False(Package.IsSaveComplete);
        Assert.True(eventArgs.Cancelled);
    }

    [Fact]
    public async Task DownloadParallelNotSupportedUrlTest()
    {
        // arrange
        var actualChunksCount = 0;
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
        var isCancelled = false;
        var actualChunksCount = 0;
        var progressCount = 0;
        var cancelOnProgressNo = 6;
        var maxProgressPercentage = 0d;
        var address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
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
        var allActiveChunksCount = new List<int>(20);
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
        foreach (var activeChunks in allActiveChunksCount)
            Assert.True(activeChunks is >= 1 and <= 4);
    }

    [Fact]
    public async Task ActiveChunksWithRangeNotSupportedUrlTest()
    {
        // arrange
        var allActiveChunksCount = new List<int>(20);
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
        foreach (var activeChunks in allActiveChunksCount)
            Assert.True(activeChunks is >= 1 and <= 4);
    }

    [Fact]
    public async Task ActiveChunksAfterCancelResumeWithNotSupportedUrlTest()
    {
        // arrange
        var allActiveChunksCount = new List<int>(20);
        var isCancelled = false;
        var actualChunksCount = 0;
        var progressCount = 0;
        var cancelOnProgressNo = 6;
        var address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
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
        foreach (var activeChunks in allActiveChunksCount)
            Assert.True(activeChunks is >= 1 and <= 4);
    }

    [Fact]
    public async Task TestPackageDataAfterCompletionWithSuccess()
    {
        // arrange
        Options.ClearPackageOnCompletionWithFailure = false;
        var states = new DownloadServiceEventsState(this);
        var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);

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
        var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName,
            DummyFileHelper.FileSize16Kb);
        var createdStatus = DownloadStatus.None;
        var runningStatus = DownloadStatus.None;
        var pausedStatus = DownloadStatus.None;
        var resumeStatus = DownloadStatus.None;
        var completedStatus = DownloadStatus.None;

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
        var path = Path.GetTempFileName();
        DownloadPackage package = null;
        var packageText = string.Empty;
        var url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
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
        var isCancelOccurred = false;
        var path = Path.GetTempFileName();
        DownloadPackage package = null;
        var packageText = string.Empty;
        var url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Options = GetDefaultConfig();
        ChunkDownloadProgressChanged += async (_, _) => {
            if (isCancelOccurred == false)
            {
                isCancelOccurred = true;
                await CancelTaskAsync();
            }
        };
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

        // resume act
        var reversedPackage = System.Text.Json.JsonSerializer.Deserialize<DownloadPackage>(packageText);
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
        var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        var createdStatus = DownloadStatus.None;
        var runningStatus = DownloadStatus.None;
        var cancelledStatus = DownloadStatus.None;
        var completedStatus = DownloadStatus.None;

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
        var checkProgress = false;
        var secondStartProgressPercent = -1d;
        var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        var tcs = new TaskCompletionSource<bool>();
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
        var completedState = DownloadStatus.None;
        var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
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
        var completedState = DownloadStatus.None;
        var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
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
        Options.MinimumSizeOfChunking = DummyFileHelper.FileSize16Kb;
        var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        var activeChunks = 0;
        int? chunkCounts = null;
        var progressIds = new Dictionary<string, bool>();
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
    public async Task TestCreatePathIfNotExist()
    {
        // arrange
        Options = GetDefaultConfig();
        var url = DummyFileHelper.GetFileWithNameUrl(Filename, DummyFileHelper.FileSize1Kb);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        var dir = new DirectoryInfo(path);

        // act
        await DownloadFileTaskAsync(url, dir);

        // assert
        Assert.True(Package.IsSaveComplete);
        Assert.StartsWith(dir.FullName, Package.FileName);
        Assert.True(File.Exists(Package.FileName), "FileName: " + Package.FileName);
    }

    [Fact]
    public void TestAddLogger()
    {
        // arrange
        var logFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

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
        var url = DummyFileHelper.GetFileWithNameUrl(Filename, DummyFileHelper.FileSize1Kb);
        var path = Filename;

        // act
        await DownloadFileTaskAsync(url, path);

        // assert
        Assert.True(Package.IsSaveComplete);
        Assert.Equal(Filename, Package.FileName);
        Assert.True(File.Exists(Package.FileName), "FileName: " + Package.FileName);
    }
}