using System.Collections.Concurrent;

namespace Downloader.Test.IntegrationTests;

public abstract class DownloadIntegrationTest : IDisposable
{
    protected static byte[] FileData { get; set; }
    protected readonly ITestOutputHelper Output;
    protected readonly ILoggerFactory LogFactory;
    protected string Url { get; set; }
    protected int FileSize { get; set; }
    protected string Filename { get; set; }
    protected string FilePath { get; set; }
    protected DownloadConfiguration Config { get; set; }
    protected DownloadService Downloader { get; set; }

    public DownloadIntegrationTest(ITestOutputHelper output)
    {
        Output = output;
        // Create an ILoggerFactory that logs to the ITestOutputHelper
        LogFactory = LoggerFactory.Create(builder => {
            builder.AddProvider(new TestOutputLoggerProvider(output));
        });
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

    protected void DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
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
        var downloadCompletedSuccessfully = false;
        var resultMessage = "";
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
        await using var memoryStream = await Downloader.DownloadFileTaskAsync(Url);

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
        var destFilename = FilePath;
        byte[] downloadedBytes = null;
        var downloadCompletedSuccessfully = false;
        Downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
            {
                // Execute the downloaded file within completed event
                // Note: Execute within this event caused to an IOException:
                // The process cannot access the file '...\Temp\tmp14D3.tmp'
                // because it is being used by another process.)

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
        var dir = new DirectoryInfo(Path.GetTempPath());

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
        var url1KbFile = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb);
        var file = new FileInfo(Path.GetTempFileName());

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
        var fileBytes = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.Equal(expected: FileSize, actual: Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, fileBytes.Length);
        Assert.True(FileData.AreEqual(fileBytes));
    }

    [Fact]
    public async Task DownloadProgressChangedTest()
    {
        // arrange
        var progressChangedCount = (int)Math.Ceiling((double)FileSize / Config.BufferBlockSize);
        var progressCounter = 0;
        Downloader.DownloadProgressChanged += (_, _) => Interlocked.Increment(ref progressCounter);

        // act
        await Downloader.DownloadFileTaskAsync(Url);

        // assert
        // Note: some times received bytes on read stream method was less than block size!
        Assert.True(progressChangedCount <= progressCounter);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.False(Downloader.Package.IsSaving);
    }

    [Fact]
    public async Task StopResumeDownloadTest()
    {
        // arrange
        var expectedStopCount = 2;
        var stopCount = 0;
        var cancellationsOccurrenceCount = 0;
        var downloadFileExecutionCounter = 0;
        var downloadCompletedSuccessfully = false;
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

        var stream = await File.ReadAllBytesAsync(Downloader.Package.FileName);

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
        var expectedPauseCount = 2;
        var pauseCount = 0;
        var downloadCompletedSuccessfully = false;
        Downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error is null)
                downloadCompletedSuccessfully = true;
        };
        Downloader.DownloadProgressChanged += delegate {
            if (expectedPauseCount > pauseCount)
            {
                // Stopping after start of downloading
                Downloader.Pause();
                pauseCount++;
                Downloader.Resume();
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url, Path.GetTempFileName());
        var stream = File.ReadAllBytes(Downloader.Package.FileName);

        // assert
        Assert.False(Downloader.IsPaused);
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(expectedPauseCount, pauseCount);
        Assert.True(downloadCompletedSuccessfully);
        Assert.True(FileData.SequenceEqual(stream.ToArray()));

        File.Delete(Downloader.Package.FileName);
    }

    [Fact]
    public async Task StopResumeDownloadFromLastPositionTest()
    {
        // arrange
        var expectedStopCount = 1;
        var stopCount = 0;
        var downloadFileExecutionCounter = 0;
        var totalProgressedByteSize = 0L;
        var totalReceivedBytes = 0L;
        Config.BufferBlockSize = 1024;
        Config.EnableLiveStreaming = true;

        Downloader.DownloadProgressChanged += (_, e) => {
            totalProgressedByteSize += e.ProgressedByteSize;
            totalReceivedBytes += e.ReceivedBytes.Length;
            if (expectedStopCount > stopCount)
            {
                // Stopping after start of downloading
                Downloader.CancelAsync();
                stopCount++;
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
        Assert.Equal(FileSize, totalProgressedByteSize);
        Assert.Equal(FileSize, totalReceivedBytes);
    }

    [Fact]
    public async Task StopResumeDownloadOverFirstPackagePositionTest()
    {
        // arrange
        var cancellationCount = 4;
        var isSavingStateOnCancel = false;
        var isSavingStateBeforCancel = false;
        Config.EnableLiveStreaming = true;

        Downloader.DownloadProgressChanged += async (_, _) => {
            isSavingStateBeforCancel |= Downloader.Package.IsSaving;
            if (--cancellationCount > 0)
            {
                // Stopping after start of downloading
                await Downloader.CancelTaskAsync();
            }
        };

        // act
        var result = await Downloader.DownloadFileTaskAsync(Url);
        // check point of package for once time
        var firstCheckPointPackage = JsonConvert.SerializeObject(Downloader.Package);

        while (Downloader.IsCancelled)
        {
            isSavingStateOnCancel |= Downloader.Package.IsSaving;
            var restoredPackage = JsonConvert.DeserializeObject<DownloadPackage>(firstCheckPointPackage);

            // resume download from first stopped point.
            result = await Downloader.DownloadFileTaskAsync(restoredPackage);
        }

        // assert
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.False(Downloader.Package.IsSaving);
        Assert.False(isSavingStateOnCancel);
        Assert.True(isSavingStateBeforCancel);
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, result.Length);
    }

    [Fact]
    public async Task TestTotalReceivedBytesWhenResumeDownload()
    {
        // arrange
        var canStopDownload = true;
        var totalDownloadSize = 0L;
        var lastProgressPercentage = 0.0;
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
        var canStopDownload = true;
        var totalDownloadSize = 0L;
        var lastProgressPercentage = 0.0;
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
        Downloader.Package.Storage.Dispose(); // set position to zero
        await Downloader.DownloadFileTaskAsync(Downloader.Package); // resume download from stopped point.

        // assert
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, totalDownloadSize);
        Assert.Equal(100.0, lastProgressPercentage);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
    }

    [Fact]
    //[Timeout(17_000)]
    public async Task SpeedLimitTest()
    {
        // arrange
        double averageSpeed = 0;
        var progressCounter = 0;
        Config.BufferBlockSize = 1024;
        Config.MaximumBytesPerSecond = 2048; // Byte/s

        Downloader.DownloadProgressChanged += (_, e) => {
            averageSpeed = ((averageSpeed * progressCounter) + e.BytesPerSecondSpeed) / (progressCounter + 1);
            progressCounter++;
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.True(averageSpeed <= Config.MaximumBytesPerSecond * 1.5,
            $"Average Speed: {averageSpeed} , Speed Limit: {Config.MaximumBytesPerSecond}");
    }

    [Fact]
    public async Task DynamicSpeedLimitTest()
    {
        // arrange
        double upperTolerance = 1.5; // 50% upper than expected avg speed
        long expectedAverageSpeed = FileSize / 32; // == (256*16 + 512*8 + 1024*4 + 2048*2) / 32
        double averageSpeed = 0;
        var progressCounter = 0;
        const int oneSpeedStepSize = 4096; // FileSize / 4

        Config.MaximumBytesPerSecond = 256; // Byte/s


        Downloader.DownloadProgressChanged += (_, e) => {
            // ReSharper disable once AccessToModifiedClosure
            averageSpeed += e.BytesPerSecondSpeed;
            progressCounter++;

            var pow = Math.Ceiling((double)e.ReceivedBytesSize / oneSpeedStepSize);
            Config.MaximumBytesPerSecond = 128 * (int)Math.Pow(2, pow); // 256, 512, 1024, 2048
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url);
        averageSpeed /= progressCounter;

        // assert
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.True(averageSpeed <= expectedAverageSpeed * upperTolerance,
            $"Avg Speed: {averageSpeed} , Expected Avg Speed Limit: {expectedAverageSpeed * upperTolerance}, " +
            $"Progress Count: {progressCounter}");
    }

    [Fact]
    public async Task TestSizeWhenDownloadOnMemoryStream()
    {
        // arrange


        // act
        await using var stream = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.Equal(FileSize, Downloader.Package.TotalFileSize);
        Assert.Equal(FileSize, stream.Length);
    }

    [Fact]
    public async Task TestTypeWhenDownloadOnMemoryStream()
    {
        // arrange


        // act
        await using var stream = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.True(stream is MemoryStream);
    }

    [Fact]
    public async Task TestContentWhenDownloadOnMemoryStream()
    {
        // act
        await using var stream = await Downloader.DownloadFileTaskAsync(Url);
        var data = (stream as MemoryStream)?.ToArray();

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
        var totalSize = Config.RangeHigh - Config.RangeLow + 1;


        // act
        await using var stream = await Downloader.DownloadFileTaskAsync(Url);

        // assert
        Assert.NotNull(stream);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        Assert.IsType<MemoryStream>(stream);

        var bytes = ((MemoryStream)stream).ToArray();
        for (int i = 0; i < totalSize; i++)
            Assert.Equal((byte)i, bytes[i]);
    }

    [Fact]
    public async Task DownloadNegetiveRangeOfFileTest()
    {
        // arrange
        Config.RangeDownload = true;
        Config.RangeLow = -256;
        Config.RangeHigh = 255;
        var totalSize = 256;


        // act
        await using var stream = await Downloader.DownloadFileTaskAsync(Url);
        var bytes = ((MemoryStream)stream).ToArray();

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
        var maxParallelCountTasks = Config.ChunkCount / 2;
        Config.ParallelCount = maxParallelCountTasks;

        var actualMaxParallelCountTasks = 0;
        Downloader.ChunkDownloadProgressChanged += (_, e) => {
            actualMaxParallelCountTasks = Math.Max(actualMaxParallelCountTasks, e.ActiveChunks);
        };

        // act
        await using var stream = await Downloader.DownloadFileTaskAsync(Url);
        var bytes = ((MemoryStream)stream).ToArray();

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
    public async Task TestResumeImmediatelyAfterCanceling()
    {
        // arrange
        var canStopDownload = true;
        var lastProgressPercentage = 0d;
        bool? stopped = null;

        Downloader.DownloadFileCompleted += (_, e) => stopped ??= e.Cancelled;
        Downloader.DownloadProgressChanged += (_, e) => {
            if (canStopDownload && e.ProgressPercentage > 50)
            {
                canStopDownload = false;
                Downloader.CancelAsync();
            }
            else if (!canStopDownload && lastProgressPercentage <= 0)
            {
                lastProgressPercentage = e.ProgressPercentage;
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(Url);
        await using var stream = await Downloader.DownloadFileTaskAsync(Downloader.Package); // resume

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
        Config.MaxTryAgainOnFailover = 0;
        Config.ClearPackageOnCompletionWithFailure = clearFileAfterFailure;
        var downloadService = new DownloadService(Config);
        var filename = Path.GetTempFileName();
        var url = DummyFileHelper.GetFileWithFailureAfterOffset(FileSize, FileSize / 2);

        // act
        await downloadService.DownloadFileTaskAsync(url, filename);

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
        var fileSize = FileSize;
        var failureOffset = fileSize / 2;
        Config.MaxTryAgainOnFailover = 5;
        Config.BufferBlockSize = 1024;
        Config.MinimumSizeOfChunking = 0;
        Config.Timeout = 100;
        Config.ClearPackageOnCompletionWithFailure = false;
        var downloadService = new DownloadService(Config);
        var url = timeout
            ? DummyFileHelper.GetFileWithTimeoutAfterOffset(fileSize, failureOffset)
            : DummyFileHelper.GetFileWithFailureAfterOffset(fileSize, failureOffset);
        downloadService.DownloadFileCompleted += (_, e) => error = e.Error;

        // act
        var stream = await downloadService.DownloadFileTaskAsync(url);
        var retryCount = downloadService.Package.Chunks.Sum(chunk => chunk.FailoverCount);

        // assert
        Assert.False(downloadService.Package.IsSaveComplete);
        Assert.False(downloadService.Package.IsSaving);
        Assert.Equal(DownloadStatus.Failed, downloadService.Package.Status);
        Assert.True(Config.MaxTryAgainOnFailover <= retryCount);
        Assert.NotNull(error);
        Assert.IsType<WebException>(error);
        Assert.Equal(failureOffset, stream.Length);

        await stream.DisposeAsync();
    }

    [Fact]
    public async Task DownloadMultipleFilesWithOneDownloaderInstanceTest()
    {
        // arrange
        var size1 = 1024 * 8;
        var size2 = 1024 * 16;
        var size3 = 1024 * 32;
        var url1 = DummyFileHelper.GetFileUrl(size1);
        var url2 = DummyFileHelper.GetFileUrl(size2);
        var url3 = DummyFileHelper.GetFileUrl(size3);


        // act
        var file1 = await Downloader.DownloadFileTaskAsync(url1);
        var file2 = await Downloader.DownloadFileTaskAsync(url2);
        var file3 = await Downloader.DownloadFileTaskAsync(url3);

        // assert
        Assert.Equal(size1, file1.Length);
        Assert.Equal(size2, file2.Length);
        Assert.Equal(size3, file3.Length);
    }

    [Fact]
    public async Task TestStopDownloadWithCancellationToken()
    {
        // arrange
        var downloadProgress = 0d;
        var downloadCancelled = false;
        var cts = new CancellationTokenSource();

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
        var url1 = DummyFileHelper.GetFileWithNameUrl("file1.dat", FileSize);
        var url2 = DummyFileHelper.GetFileWithNameUrl("file2.dat", FileSize);
        var canStopDownload = true;
        var totalDownloadSize = 0L;
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
        var totalSize = FileSize;
        var chunkSize = totalSize / Config.ChunkCount;

        var urls = Enumerable.Range(1, urlsCount)
            .Select(i => DummyFileHelper.GetFileWithNameUrl("testfile_" + i, totalSize, (byte)i))
            .ToArray();

        // act
        await using var stream = await Downloader.DownloadFileTaskAsync(urls);
        var bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.NotNull(stream);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        for (int i = 0; i < totalSize; i++)
        {
            var chunkIndex = (byte)(i / chunkSize);
            var expectedByte = (chunkIndex % urlsCount) + 1;
            Assert.Equal(expectedByte, bytes[i]);
        }
    }

    [Fact]
    public async Task DownloadBigFileOnDisk()
    {
        // arrange
        var totalSize = 1024 * 1024 * 100; // 100MB
        Config.ChunkCount = 8;
        Config.ParallelCount = 8;
        Config.MaximumBytesPerSecond = 0;
        Url = DummyFileHelper.GetFileWithNameUrl(Filename, totalSize);
        //Downloader.AddLogger(FileLogger.Factory("D:\\TestDownload"));
        var actualFile = DummyData.GenerateOrderedBytes(totalSize);

        // act
        await Downloader.DownloadFileTaskAsync(Url, FilePath);
        var file = await File.ReadAllBytesAsync(FilePath);

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
        var totalSize = 1024 * 1024 * 100; // 100MB
        Config.ChunkCount = 8;
        Config.ParallelCount = 8;
        Config.MaximumBytesPerSecond = 0;
        Url = DummyFileHelper.GetFileWithNameUrl(Filename, totalSize);
        var actualFile = DummyData.GenerateOrderedBytes(totalSize);

        // act
        await using var stream = await Downloader.DownloadFileTaskAsync(Url);

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
        var totalSize = 1024 * 1024 * 512; // 512MB
        byte fillByte = 123;
        Config.ChunkCount = 16;
        Config.ParallelCount = 16;
        Config.MaximumBytesPerSecond = 0;
        Config.MaximumMemoryBufferBytes = 1024 * 1024 * 50; // 50MB
        Url = DummyFileHelper.GetFileWithNameUrl(Filename, totalSize, fillByte);
        //Downloader.AddLogger(FileLogger.Factory("D:\\TestDownload"));

        // act
        await Downloader.DownloadFileTaskAsync(Url, FilePath);
        await using var fileStream = File.Open(FilePath, FileMode.Open, FileAccess.Read);

        // assert
        Assert.Equal(totalSize, Downloader.Package.TotalFileSize);
        Assert.Equal(totalSize, fileStream.Length);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        for (int i = 0; i < totalSize; i++)
        {
            Assert.Equal(fillByte, fileStream.ReadByte());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StorePackageFileWhenDownloadInProgress(bool storeInMemory)
    {
        // arrange
        const long totalSize = 1024 * 1024 * 256; // 256MB
        double snapshotPoint = 0.25; // 25%
        SemaphoreSlim semaphore = new(1, 1);
        ConcurrentBag<string> snapshots = new();
        Config.ChunkCount = 8;
        Config.ParallelCount = 8;
        Config.BufferBlockSize = 1024;
        Config.MaximumBytesPerSecond = 1024 * 1024 * 8; // 8MB/s
        Config.MaximumMemoryBufferBytes = totalSize / 2; // 128MB
        Url = DummyFileHelper.GetFileWithNameUrl(Filename, totalSize);
        Downloader.DownloadProgressChanged += async (_, e) => {
            if (snapshotPoint >= e.ProgressPercentage) return;

            try
            {
                await semaphore.WaitAsync();
                if (snapshotPoint < e.ProgressPercentage)
                {
                    snapshotPoint += 0.25;
                    snapshots.Add(JsonConvert.SerializeObject(Downloader.Package));
                }

                if (snapshotPoint > 0.75)
                    await Downloader.CancelTaskAsync();
            }
            finally
            {
                semaphore.Release();
            }
        };

        // act
        if (storeInMemory)
            await Downloader.DownloadFileTaskAsync(Url);
        else
            await Downloader.DownloadFileTaskAsync(Url, FilePath);

        // assert
        Assert.Equal(3, snapshots.Count);
        while (snapshots.TryTake(out string package))
        {
            Assert.False(string.IsNullOrWhiteSpace(package));
            await using DownloadPackage snapshot = JsonConvert.DeserializeObject<DownloadPackage>(package);
            Assert.Equal(totalSize, snapshot.TotalFileSize);
            Assert.True(snapshot.SaveProgress < 100);
            Assert.True(snapshot.SaveProgress > 0);

            if (storeInMemory)
            {
                Assert.True(snapshot.Storage.OpenRead() is MemoryStream);
            }
            else
            {
                Assert.True(snapshot.Storage.OpenRead() is FileStream);
                Assert.True(File.Exists(snapshot.FileName));
            }
        }
    }
}