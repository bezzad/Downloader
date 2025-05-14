using Microsoft.AspNetCore.Http.Timeouts;

namespace Downloader.Test.IntegrationTests;

[Collection("Sequential")]
public abstract class DownloadIntegrationTest : BaseTestClass, IDisposable
{
    protected static byte[] FileData { get; set; }
    protected string Url { get; init; }
    protected int FileSize { get; set; }
    protected string Filename { get; set; }
    protected string FilePath { get; set; }
    protected DownloadConfiguration Config { get; set; }
    protected DownloadService Downloader { get; set; }

    protected DownloadIntegrationTest(ITestOutputHelper output) : base(output)
    {
        Filename = Path.GetRandomFileName();
        FilePath = Path.Combine(Path.GetTempPath(), Filename);
        FileSize = DummyFileHelper.FileSize16Kb;
        FileData ??= DummyFileHelper.File16Kb;
        Url = DummyFileHelper.GetFileWithNameUrl(Filename, FileSize);
    }

    public void Dispose()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    protected void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        if (e.Error is not null)
        {
            Output.WriteLine("Error when completed: " + e.Error.Message);
        }
    }

    [Fact]
    public async Task DownloadUrlWithFilenameOnMemoryTest()
    {
        // arrange
        bool downloadCompletedSuccessfully = false;
        string resultMessage = "";
        Downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
            {
                downloadCompletedSuccessfully = true;
            }
            else
            {
                resultMessage = e.Error?.Message;
            }
        };

        // act
        await using Stream memoryStream = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.True(downloadCompletedSuccessfully, resultMessage);
        Assert.NotNull(memoryStream);
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.Null(Downloader.Package.FileName);
        Assert.Equal(FileSize, memoryStream.Length);
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        Assert.True(FileData.AreEqual(memoryStream));
    }

    [Fact]
    public async Task DownloadAndReadFileOnDownloadFileCompletedEventTest()
    {
        // arrange
        string destFilename = FilePath;
        byte[] downloadedBytes = null;
        bool downloadCompletedSuccessfully = false;
        Downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
            {
                // Execute the downloaded file within completed event
                // Note: Execute within this event caused to an IOException:
                // The process cannot access the file '...\Temp\tmp14D3.tmp'
                // because it is being used by another process.

                downloadCompletedSuccessfully = true;
                downloadedBytes = File.ReadAllBytes(destFilename);
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url, destFilename);

        // assert
        Assert.True(downloadCompletedSuccessfully);
        Assert.NotNull(downloadedBytes);
        Assert.Equal(destFilename, Downloader.Package.FileName);
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, downloadedBytes.Length);
        Assert.True(FileData.SequenceEqual(downloadedBytes));

        File.Delete(destFilename);
    }

    [Fact]
    public async Task Download16KbWithoutFilenameOnDirectoryTest()
    {
        // arrange
        DirectoryInfo dir = new(Path.GetTempPath());

        // act
        await Downloader.DownloadFileTaskAsync(Url, dir);

        // assert
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.NotNull(Downloader.Package.FileName);
        Assert.StartsWith(DummyFileHelper.TempDirectory, Downloader.Package.FileName);
        Assert.Equal(FilePath, Downloader.Package.FileName);
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.True(FileData.AreEqual(File.OpenRead(Downloader.Package.FileName)));

        File.Delete(FilePath);
    }

    [Fact]
    public async Task Download16KbWithFilenameTest()
    {
        // act
        await Downloader.DownloadFileTaskAsync(Url, Path.GetTempFileName());

        // assert
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.NotNull(Downloader.Package.FileName);
        Assert.StartsWith(DummyFileHelper.TempDirectory, Downloader.Package.FileName);
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.True(FileData.AreEqual(File.OpenRead(Downloader.Package.FileName)));

        File.Delete(Downloader.Package.FileName);
    }

    [Fact(Timeout = 20_000)]
    public async Task Download1KbWhenAnotherBiggerFileExistTest()
    {
        // arrange
        string url1KbFile = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb);
        FileInfo file = new(Path.GetTempFileName());

        // act
        // write file bigger than download file
        await File.WriteAllBytesAsync(file.FullName, DummyData.GenerateSingleBytes(2048, 250));
        if (File.Exists(file.FullName))
        {
            // override file with downloader
            await Downloader.DownloadFileTaskAsync(url1KbFile, file.FullName);
        }

        // assert
        Assert.True(File.Exists(file.FullName));
        Assert.Equal(file.FullName, Downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize1Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize1Kb, file.Length);
        Assert.True(DummyFileHelper.File1Kb.AreEqual(file.OpenRead()));

        file.Delete();
    }

    [Fact]
    public async Task Download16KbOnMemoryTest()
    {
        // act
        Stream fileBytes = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.Equal(expected: FileSize, actual: Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, fileBytes.Length);
        Assert.True(FileData.AreEqual(fileBytes));
    }

    [Fact]
    public async Task DownloadProgressChangedTest()
    {
        // arrange
        int progressChangedCount = (int)Math.Ceiling((double)FileSize / Config.BufferBlockSize);
        int progressCounter = 0;
        Downloader.DownloadProgressChanged += (_, _) => Interlocked.Increment(ref progressCounter);

        // act
        await Downloader.DownloadFileTaskAsync(Url);

        // assert
        // Note: sometimes received bytes on read stream method was less than block size!
        Assert.True(progressChangedCount <= progressCounter);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.False(Downloader.Package.IsSaving);
    }

    [Fact]
    public async Task StopResumeDownloadTest()
    {
        // arrange
        int expectedStopCount = 2;
        int stopCount = 0;
        int cancellationsOccurrenceCount = 0;
        int downloadFileExecutionCounter = 0;
        bool downloadCompletedSuccessfully = false;
        Downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled && e.Error != null)
            {
                cancellationsOccurrenceCount++;
            }
            else
            {
                downloadCompletedSuccessfully = true;
            }
        };
        Downloader.DownloadStarted += async delegate {
            if (expectedStopCount > stopCount)
            {
                // Stopping after start of downloading
                await Downloader.CancelTaskAsync();
                stopCount++;
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url, Path.GetTempFileName());
        while (expectedStopCount > downloadFileExecutionCounter++)
        {
            // resume download from stopped point.
            await Downloader.DownloadFileTaskAsync(Downloader.Package);
        }

        byte[] stream = await File.ReadAllBytesAsync(Downloader.Package.FileName);

        // assert
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(expectedStopCount, stopCount);
        Assert.Equal(expectedStopCount, cancellationsOccurrenceCount);
        Assert.True(downloadCompletedSuccessfully);
        Assert.True(FileData.SequenceEqual(stream.ToArray()));

        File.Delete(Downloader.Package.FileName);
    }

    [Fact]
    public async Task PauseResumeDownloadTest()
    {
        // arrange
        int expectedPauseCount = 2;
        int pauseCount = 0;
        bool downloadCompletedSuccessfully = false;
        Downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error is null)
                downloadCompletedSuccessfully = true;
        };
        Downloader.DownloadProgressChanged += delegate {
            if (expectedPauseCount > pauseCount)
            {
                // Stopping after start of downloading
                Downloader.Pause();
                pauseCount++; // Because of concurrency may be pauseCount > expectedPauseCount
                Downloader.Resume();
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url, Path.GetTempFileName());
        byte[] stream = await File.ReadAllBytesAsync(Downloader.Package.FileName);

        // assert
        Assert.False(Downloader.IsPaused);
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.True(downloadCompletedSuccessfully);
        Assert.True(FileData.SequenceEqual(stream.ToArray()));

        File.Delete(Downloader.Package.FileName);
    }

    [Fact]
    public async Task StopResumeDownloadFromLastPositionTest()
    {
        // arrange
        int expectedStopCount = 1;
        int stopCount = 0;
        int downloadFileExecutionCounter = 0;
        long totalProgressedByteSize = 0L;
        long totalReceivedBytes = 0L;
        Config.BufferBlockSize = 1024;
        Config.EnableLiveStreaming = true;

        Downloader.DownloadProgressChanged += (_, e) => {
            Interlocked.Add(ref totalProgressedByteSize, e.ProgressedByteSize);
            Interlocked.Add(ref totalReceivedBytes, e.ReceivedBytes.Length);
            if (expectedStopCount > stopCount)
            {
                // Stopping after start of downloading
                Downloader.CancelAsync();
                Interlocked.Increment(ref stopCount);
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url);
        while (expectedStopCount > downloadFileExecutionCounter++)
        {
            // resume download from stopped point.
            await Downloader.DownloadFileTaskAsync(Downloader.Package);
        }

        // assert
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, totalReceivedBytes);
        Assert.Equal(FileSize, totalProgressedByteSize);
    }

    [Fact]
    public async Task StopResumeDownloadOverFirstPackagePositionTest()
    {
        // arrange
        int cancellationCount = 4;
        bool isSavingStateOnCancel = false;
        bool isSavingStateBeforeCancel = false;
        Config.EnableLiveStreaming = true;

        Downloader.DownloadProgressChanged += async (_, _) => {
            isSavingStateBeforeCancel |= Downloader.Package.IsSaving;
            if (--cancellationCount > 0)
            {
                // Stopping after start of downloading
                await Downloader.CancelTaskAsync();
            }
        };

        // act
        Stream result = await Downloader.DownloadFileTaskAsync(Url);
        // check point of package for once time
        string firstCheckPointPackage = JsonConvert.SerializeObject(Downloader.Package);

        while (Downloader.IsCancelled)
        {
            isSavingStateOnCancel |= Downloader.Package.IsSaving;
            DownloadPackage restoredPackage = JsonConvert.DeserializeObject<DownloadPackage>(firstCheckPointPackage);

            // resume download from first stopped point.
            result = await Downloader.DownloadFileTaskAsync(restoredPackage);
        }

        // assert
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.False(Downloader.Package.IsSaving);
        Assert.False(isSavingStateOnCancel);
        Assert.True(isSavingStateBeforeCancel);
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, result.Length);
    }

    [Fact]
    public async Task TestTotalReceivedBytesWhenResumeDownload()
    {
        // arrange
        bool canStopDownload = true;
        long totalDownloadSize = 0L;
        double lastProgressPercentage = 0.0;
        Config.BufferBlockSize = 1024;
        Config.ChunkCount = 1;
        Config.EnableLiveStreaming = true;
        Downloader.DownloadProgressChanged += async (_, e) => {
            totalDownloadSize += e.ReceivedBytes.Length;
            lastProgressPercentage = e.ProgressPercentage;
            if (canStopDownload && totalDownloadSize > FileSize / 2)
            {
                // Stopping after start of downloading
                await Downloader.CancelTaskAsync();
                canStopDownload = false;
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url);
        await Downloader.DownloadFileTaskAsync(Downloader.Package); // resume download from stopped point.

        // assert
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.False(Downloader.IsCancelled);
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, totalDownloadSize);
        Assert.Equal(100.0, lastProgressPercentage);
    }

    [Fact]
    public async Task TestTotalReceivedBytesOnResumeDownloadWhenLostDownloadedData()
    {
        // arrange
        bool canStopDownload = true;
        long totalDownloadSize = 0L;
        double lastProgressPercentage = 0.0;
        Config.BufferBlockSize = 1024;
        Config.ChunkCount = 1;
        Downloader.DownloadProgressChanged += (_, e) => {
            totalDownloadSize = e.ReceivedBytesSize;
            lastProgressPercentage = e.ProgressPercentage;
            if (canStopDownload && totalDownloadSize > FileSize / 2)
            {
                // Stopping after start of downloading
                Downloader.CancelAsync();
                canStopDownload = false;
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url);
        await Downloader.Package.Storage.DisposeAsync(); // set position to zero
        await Downloader.DownloadFileTaskAsync(Downloader.Package); // resume download from stopped point.

        // assert
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, totalDownloadSize);
        Assert.Equal(100.0, lastProgressPercentage);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
    }

    [Fact]
    public async Task SpeedLimitTest()
    {
        // arrange
        double averageSpeed = 0;
        const double tolerance = 2;
        const int speed = 1024 * 1024; // 1MB/s
        const int fileSize = 1024 * 1024 * 10; // 10MB
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, fileSize);
        Config.BufferBlockSize = 1024; // 1KB
        Config.MaximumBytesPerSecond = speed;

        Downloader.DownloadProgressChanged += (_, e) => {
            averageSpeed = e.AverageBytesPerSecondSpeed;
        };

        // act
        await Downloader.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(fileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(speed, Config.MaximumBytesPerSecond);
        Assert.True(averageSpeed <= Config.MaximumBytesPerSecond * tolerance,
            $"Average Speed: {averageSpeed} , Speed Limit: {speed}");
    }

    [Fact]
    public async Task DynamicSpeedLimitTest()
    {
        // arrange
        const long size = 1024 * 256; // 256KB
        const int speedUpLevels = 4; // 4 levels
        const int levelPercent = 100 / speedUpLevels; // 20% each level
        const double tolerance = 2; // 200% of expected avg speed
        const int speedPerStep = 8192; // 8KB/s
        const double sumLevelsSectors = speedUpLevels * (speedUpLevels + 1) / 2d; // n*(n+1)/2
        const double expectedAverageSpeed = speedPerStep * sumLevelsSectors / speedUpLevels;
        int currentLevel = 1;
        long averageSpeed = 0;
        object lockObj = new();
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, size);

        Config.BufferBlockSize = 1024;
        Config.MaximumBytesPerSecond = speedPerStep;

        Downloader.DownloadProgressChanged += (_, e) => {
            averageSpeed = (long)e.AverageBytesPerSecondSpeed;
            lock (lockObj)
            {
                if (currentLevel <= speedUpLevels &&
                    (int)e.ProgressPercentage / (levelPercent * currentLevel) > 1)
                {
                    currentLevel++;
                    // increase speed each 25% of progress:  25%, 50%, 75%, 100%
                    Config.MaximumBytesPerSecond += speedPerStep;
                }
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(size, Downloader.Package.TotalFileSize);
        Assert.True(averageSpeed <= expectedAverageSpeed * tolerance,
            $"Avg Speed: {averageSpeed} , Expected Avg Speed Limit: {expectedAverageSpeed} * {tolerance}, ");
    }

    [Fact]
    public async Task TestSizeWhenDownloadOnMemoryStream()
    {
        // arrange


        // act
        await using Stream stream = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, stream.Length);
    }

    [Fact]
    public async Task TestTypeWhenDownloadOnMemoryStream()
    {
        // arrange


        // act
        await using Stream stream = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.True(stream is MemoryStream);
    }

    [Fact]
    public async Task TestContentWhenDownloadOnMemoryStream()
    {
        // act
        await using Stream stream = await Downloader.DownloadFileTaskAsync(Url);
        byte[] data = (stream as MemoryStream)?.ToArray();

        // assert
        Assert.True(data != null && FileData.SequenceEqual(data));
    }

    [Fact(Timeout = 60_000)]
    public async Task Download256BytesRangeOfFileTest()
    {
        // arrange
        Config.RangeDownload = true;
        Config.RangeLow = 256;
        Config.RangeHigh = 511;
        long totalSize = Config.RangeHigh - Config.RangeLow + 1;


        // act
        await using Stream stream = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.NotNull(stream);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        Assert.IsType<MemoryStream>(stream);

        byte[] bytes = ((MemoryStream)stream).ToArray();
        for (int i = 0; i < totalSize; i++)
            Assert.Equal((byte)i, bytes[i]);
    }

    [Fact]
    public async Task DownloadNegativeRangeOfFileTest()
    {
        // arrange
        Config.RangeDownload = true;
        Config.RangeLow = -256;
        Config.RangeHigh = 255;
        int totalSize = 256;


        // act
        await using Stream stream = await Downloader.DownloadFileTaskAsync(Url);
        byte[] bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.NotNull(stream);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        for (int i = 0; i < totalSize; i++)
            Assert.Equal((byte)i, bytes[i]);
    }

    [Fact]
    public async Task TestDownloadParallelVsHalfOfChunks()
    {
        // arrange
        int maxParallelCountTasks = Config.ChunkCount / 2;
        Config.ParallelCount = maxParallelCountTasks;

        int actualMaxParallelCountTasks = 0;
        Downloader.ChunkDownloadProgressChanged += (_, e) => {
            actualMaxParallelCountTasks = Math.Max(actualMaxParallelCountTasks, e.ActiveChunks);
        };

        // act
        await using Stream stream = await Downloader.DownloadFileTaskAsync(Url);
        byte[] bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.True(maxParallelCountTasks >= actualMaxParallelCountTasks);
        Assert.NotNull(stream);
        Assert.Equal(FileSize, stream.Length);
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        for (int i = 0; i < FileSize; i++)
            Assert.Equal((byte)i, bytes[i]);
    }

    [Fact]
    [RequestTimeout(10000)]
    public async Task TestResumeImmediatelyAfterCanceling()
    {
        // arrange
        bool canStopDownload = true;
        double lastProgressPercentage = 0d;
        bool? stopped = null;
        SemaphoreSlim semaphoreSlim = new(1, 1);
        TaskCompletionSource<bool> cancellationCompleted = new();

        Downloader.DownloadFileCompleted += (_, e) => {
            stopped ??= e.Cancelled;
            if (stopped == true)
                cancellationCompleted.TrySetResult(true);
        };
        Downloader.DownloadProgressChanged += async (_, e) => {
            try
            {
                await semaphoreSlim.WaitAsync();
                if (canStopDownload && e.ProgressPercentage > 50)
                {
                    canStopDownload = false;
                    lastProgressPercentage = e.ProgressPercentage;
                    await Downloader.CancelTaskAsync();
                }
            }
            finally
            {
                semaphoreSlim.Release();
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url);
        await cancellationCompleted.Task; // Wait for cancellation to complete
        await using Stream stream = await Downloader.DownloadFileTaskAsync(Downloader.Package); // resume

        // assert
        Assert.True(stopped);
        Assert.True(lastProgressPercentage > 50);
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.False(Downloader.IsCancelled);
    }

    [Theory]
    [InlineData(true)] // Remove File When Download Failed Test
    [InlineData(false)] // Keep File When Download Failed Test
    public async Task KeepOrRemoveFileWhenDownloadFailedTest(bool clearFileAfterFailure)
    {
        // arrange
        Config.MaxTryAgainOnFailure = 0;
        Config.ClearPackageOnCompletionWithFailure = clearFileAfterFailure;
        DownloadService downloadService = new(Config);
        string filename = Path.GetTempFileName();
        string url = DummyFileHelper.GetFileWithFailureAfterOffset(FileSize, FileSize / 2);

        // act
        await downloadService.DownloadFileTaskAsync(url, filename);
        await Task.Delay(1000);

        // assert
        Assert.Equal(filename, downloadService.Package.FileName);
        Assert.False(downloadService.Package.IsSaveComplete);
        Assert.False(downloadService.Package.IsSaving);
        Assert.NotEqual(clearFileAfterFailure, File.Exists(filename));
    }

    [Theory]
    [InlineData(true)] // Test Retry Download After Timeout
    [InlineData(false)] // Test Retry Download After Failure
    public async Task TestRetryDownloadAfterFailure(bool timeout)
    {
        // arrange
        Exception error = null;
        int fileSize = FileSize;
        int failureOffset = fileSize / 2;
        Config.MaxTryAgainOnFailure = 5;
        Config.BufferBlockSize = 1024;
        Config.MinimumSizeOfChunking = 0;
        Config.Timeout = 100;
        Config.ClearPackageOnCompletionWithFailure = false;
        DownloadService downloadService = new(Config);
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        string url = timeout
            ? DummyFileHelper.GetFileWithTimeoutAfterOffset(fileSize, failureOffset)
            : DummyFileHelper.GetFileWithFailureAfterOffset(fileSize, failureOffset);
        downloadService.DownloadFileCompleted += (_, e) => {
            error ??= e.Error;
            tcs.TrySetResult(true);
        };

        // act
        Stream stream = await downloadService.DownloadFileTaskAsync(url);
        await tcs.Task; // Wait for completion event
        int retryCount = downloadService.Package.Chunks.Sum(chunk => chunk.FailureCount);

        // assert
        Assert.False(downloadService.Package.IsSaveComplete);
        Assert.False(downloadService.Package.IsSaving);
        Assert.Equal(DownloadStatus.Failed, downloadService.Package.Status);
        Assert.True(Config.MaxTryAgainOnFailure <= retryCount,
            $"Retry download count: {retryCount} > {Config.MaxTryAgainOnFailure}");
        Assert.NotNull(error);
        Assert.IsType<HttpRequestException>(error);
        Assert.Equal(failureOffset, stream.Length);

        await stream.DisposeAsync();
    }

    [Fact]
    public async Task DownloadMultipleFilesWithOneDownloaderInstanceTest()
    {
        // arrange
        int size1 = 1024 * 8;
        int size2 = 1024 * 16;
        int size3 = 1024 * 32;
        string url1 = DummyFileHelper.GetFileUrl(size1);
        string url2 = DummyFileHelper.GetFileUrl(size2);
        string url3 = DummyFileHelper.GetFileUrl(size3);


        // act
        Stream file1 = await Downloader.DownloadFileTaskAsync(url1);
        Stream file2 = await Downloader.DownloadFileTaskAsync(url2);
        Stream file3 = await Downloader.DownloadFileTaskAsync(url3);

        // assert
        Assert.Equal(size1, file1.Length);
        Assert.Equal(size2, file2.Length);
        Assert.Equal(size3, file3.Length);
    }

    [Fact]
    public async Task TestStopDownloadWithCancellationToken()
    {
        // arrange
        double downloadProgress = 0d;
        bool downloadCancelled = false;
        CancellationTokenSource cts = new();

        Downloader.DownloadFileCompleted += (_, e) => downloadCancelled = e.Cancelled;
        Downloader.DownloadProgressChanged += (_, e) => {
            downloadProgress = e.ProgressPercentage;
            if (e.ProgressPercentage > 10)
            {
                // Stopping after 10% progress of downloading
                cts.Cancel();
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url, cts.Token);
        // Introduce a delay to allow for some progress
        await Task.Delay(500); // Adjust the delay time as needed
        
        // assert
        Assert.True(downloadCancelled);
        Assert.True(Downloader.IsCancelled);
        Assert.True(Downloader.Status == DownloadStatus.Stopped);
        Assert.True(downloadProgress > 10);
    }

    [Fact]
    public async Task TestResumeDownloadWithAnotherUrl()
    {
        // arrange
        string url1 = DummyFileHelper.GetFileWithNameUrl("file1.dat", FileSize);
        string url2 = DummyFileHelper.GetFileWithNameUrl("file2.dat", FileSize);
        bool canStopDownload = true;
        long totalDownloadSize = 0L;
        Config.BufferBlockSize = 1024;
        Config.ChunkCount = 4;
        Downloader.DownloadProgressChanged += (_, e) => {
            totalDownloadSize = e.ReceivedBytesSize;
            if (canStopDownload && totalDownloadSize > FileSize / 2)
            {
                // Stopping after start of downloading
                Downloader.CancelAsync();
                canStopDownload = false;
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(url1);
        await Downloader.DownloadFileTaskAsync(Downloader.Package, url2); // resume download with new url2.

        // assert
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, totalDownloadSize);
        Assert.Equal(Downloader.Package.Storage.Length, FileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
    }

    [Theory]
    [InlineData(8, 2)] // Download A File From 8 Urls With 2 Chunks Test
    [InlineData(2, 8)] // Download A File From 2 Urls With 8 Chunks Test
    [InlineData(8, 8)] // Download A File From 8 Urls With 8 Chunks Test
    public async Task DownloadAFileFromMultipleUrlsWithMultipleChunksTest(int urlsCount, int chunksCount)
    {
        // arrange
        Config.ChunkCount = chunksCount;
        Config.ParallelCount = chunksCount;
        int totalSize = FileSize;
        int chunkSize = totalSize / Config.ChunkCount;

        string[] urls = Enumerable.Range(1, urlsCount)
            .Select(i => DummyFileHelper.GetFileWithNameUrl("test-file_" + i, totalSize, (byte)i))
            .ToArray();

        // act
        await using Stream stream = await Downloader.DownloadFileTaskAsync(urls);
        byte[] bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.NotNull(stream);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        for (int i = 0; i < totalSize; i++)
        {
            byte chunkIndex = (byte)(i / chunkSize);
            int expectedByte = (chunkIndex % urlsCount) + 1;
            Assert.Equal(expectedByte, bytes[i]);
        }
    }

    [Fact]
    public async Task DownloadBigFileOnDisk()
    {
        // arrange
        const int totalSize = 1024 * 1024 * 128; // 128MB
        Config.ChunkCount = 8;
        Config.ParallelCount = 8;
        Config.MaximumBytesPerSecond = 0;
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, totalSize);
        byte[] actualFile = DummyData.GenerateOrderedBytes(totalSize);

        // act
        await Downloader.DownloadFileTaskAsync(url, FilePath);
        byte[] file = await File.ReadAllBytesAsync(FilePath);

        // assert
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(totalSize, file.Length);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        Assert.True(file.SequenceEqual(actualFile));

        File.Delete(FilePath);
    }

    [Fact]
    public async Task DownloadBigFileOnMemory()
    {
        // arrange
        int totalSize = 1024 * 1024 * 100; // 100MB
        Config.ChunkCount = 8;
        Config.ParallelCount = 8;
        Config.MaximumBytesPerSecond = 0;
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, totalSize);
        byte[] actualFile = DummyData.GenerateOrderedBytes(totalSize);

        // act
        await using Stream stream = await Downloader.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        Assert.True(actualFile.AreEqual(stream));
    }

    [Fact]
    public async Task DownloadBigFileWithMemoryLimitationOnDisk()
    {
        // arrange
        int totalSize = 1024 * 1024 * 512; // 512MB
        byte fillByte = 123;
        Config.ChunkCount = 16;
        Config.ParallelCount = 16;
        Config.MaximumBytesPerSecond = 0;
        Config.MaximumMemoryBufferBytes = 1024 * 1024 * 50; // 50MB
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, totalSize, fillByte);
        //Downloader.AddLogger(FileLogger.Factory("D:\\TestDownload"));

        // act
        await Downloader.DownloadFileTaskAsync(url, FilePath);
        await using FileStream fileStream = File.Open(FilePath, FileMode.Open, FileAccess.Read);

        // assert
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(totalSize, fileStream.Length);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        for (int i = 0; i < totalSize; i++)
        {
            Assert.Equal(fillByte, fileStream.ReadByte());
        }
    }

    [Fact]
    public async Task StorePackageFileWhenDownloadInProgress()
    {
        // arrange
        const int totalSizeMegabyte = 128;
        const int totalSize = 1024 * 1024 * totalSizeMegabyte;
        double snapshotPoint = 0.25; // 25%
        SemaphoreSlim semaphore = new(1, 1);
        string snapshot = "";
        Exception error = null;
        Config.ChunkCount = 8;
        Config.ParallelCount = 8;
        Config.BufferBlockSize = 1024;
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, totalSize);
        byte[] data = DummyData.GenerateOrderedBytes(totalSize);
        byte[] buffer = new byte[totalSize];
        DownloadService downloader = new(Config, LogFactory);
        DownloadService resumeDownloader = new(Config, LogFactory);
        resumeDownloader.DownloadFileCompleted += (_, args) => error = args.Error;
        downloader.DownloadProgressChanged += async (_, e) => {
            if (snapshotPoint >= e.ProgressPercentage) return;

            try
            {
                await semaphore.WaitAsync();
                if (snapshotPoint >= e.ProgressPercentage) return;
                if (string.IsNullOrWhiteSpace(snapshot))
                {
                    // First snapshot point
                    snapshotPoint += 0.25; // +25%
                    snapshot = JsonConvert.SerializeObject(downloader.Package);
                }
                else
                {
                    // Second snapshot point
                    await downloader.CancelTaskAsync(); // stop download
                }
            }
            finally
            {
                semaphore.Release();
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(url, FilePath);

        // assert
        Assert.False(string.IsNullOrWhiteSpace(snapshot));
        await using DownloadPackage package = JsonConvert.DeserializeObject<DownloadPackage>(snapshot);
        Assert.Equal(totalSize, package.TotalFileSize);
        Assert.True(package.SaveProgress < 100);
        Assert.True(package.SaveProgress > 0);
        await resumeDownloader.DownloadFileTaskAsync(package);
        Assert.Null(package.Storage);
        Assert.Null(error);
        Assert.True(package.IsSaveComplete);
        await using FileStream stream = File.OpenRead(FilePath);
        Assert.Equal(totalSize, actual: stream.Length);
        int readBytes = await stream.ReadAsync(buffer.AsMemory(0, totalSize));
        Assert.Equal(totalSize, readBytes);
        Assert.True(data.SequenceEqual(buffer));
    }
}