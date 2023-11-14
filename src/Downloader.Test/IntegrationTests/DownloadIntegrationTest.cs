using Downloader.DummyHttpServer;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Downloader.Test.IntegrationTests;

public abstract class DownloadIntegrationTest
{
    protected DownloadConfiguration Config { get; set; }
    protected string URL { get; set; }
    protected string Filename { get; set; }
    protected string FilePath { get; set; }

    public DownloadIntegrationTest()
    {
        Filename = Path.GetRandomFileName();
        FilePath = Path.Combine(Path.GetTempPath(), Filename);
        URL = DummyFileHelper.GetFileWithNameUrl(Filename, DummyFileHelper.FileSize16Kb);
    }

    [Fact]
    public async Task DownloadUrlWithFilenameOnMemoryTest()
    {
        // arrange
        var downloadCompletedSuccessfully = false;
        var downloader = new DownloadService(Config);
        downloader.DownloadFileCompleted += (s, e) => {
            if (e.Cancelled == false && e.Error == null)
            {
                downloadCompletedSuccessfully = true;
            }
        };

        // act
        using var memoryStream = await downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.True(downloadCompletedSuccessfully);
        Assert.NotNull(memoryStream);
        Assert.True(downloader.Package.IsSaveComplete);
        Assert.Null(downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize16Kb, memoryStream.Length);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(memoryStream));
    }

    [Fact]
    public async Task DownloadAndReadFileOnDownloadFileCompletedEventTest()
    {
        // arrange
        var destFilename = FilePath;
        byte[] downloadedBytes = null;
        var downloadCompletedSuccessfully = false;
        var downloader = new DownloadService(Config);
        downloader.DownloadFileCompleted += (s, e) => {
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
        await downloader.DownloadFileTaskAsync(URL, destFilename);

        // assert
        Assert.True(downloadCompletedSuccessfully);
        Assert.NotNull(downloadedBytes);
        Assert.Equal(destFilename, downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloadedBytes.Length);
        Assert.True(DummyFileHelper.File16Kb.SequenceEqual(downloadedBytes));

        File.Delete(destFilename);
    }

    [Fact]
    public async Task Download16KbWithoutFilenameOnDirectoryTest()
    {
        // arrange
        var dir = new DirectoryInfo(Path.GetTempPath());
        var downloader = new DownloadService(Config);

        // act
        await downloader.DownloadFileTaskAsync(URL, dir);

        // assert
        Assert.True(downloader.Package.IsSaveComplete);
        Assert.True(File.Exists(downloader.Package.FileName));
        Assert.NotNull(downloader.Package.FileName);
        Assert.StartsWith(DummyFileHelper.TempDirectory, downloader.Package.FileName);
        Assert.Equal(FilePath, downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(File.OpenRead(downloader.Package.FileName)));

        File.Delete(FilePath);
    }

     
    [Fact]
    public async Task Download16KbWithFilenameTest()
    {
        // arrange
        var downloader = new DownloadService(Config);

        // act
        await downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName());

        // assert
        Assert.True(File.Exists(downloader.Package.FileName));
        Assert.NotNull(downloader.Package.FileName);
        Assert.StartsWith(DummyFileHelper.TempDirectory, downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(File.OpenRead(downloader.Package.FileName)));

        File.Delete(downloader.Package.FileName);
    }

    [Fact(Timeout = 20_000)]
    public async Task Download1KbWhenAnotherBiggerFileExistTest()
    {
        // arrange
        var url1KbFile = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb);
        var file = new FileInfo(Path.GetTempFileName());
        var downloader = new DownloadService(Config);

        // act
        // write file bigger than download file
        await File.WriteAllBytesAsync(file.FullName, DummyData.GenerateSingleBytes(2048, 250));
        // override file with downloader
        await downloader.DownloadFileTaskAsync(url1KbFile, file.FullName);

        // assert
        Assert.True(File.Exists(file.FullName));
        Assert.Equal(file.FullName, downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize1Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize1Kb, file.Length);
        Assert.True(DummyFileHelper.File1Kb.AreEqual(file.OpenRead()));

        file.Delete();
    }

    [Fact]
    public async Task Download16KbOnMemoryTest()
    {
        // arrange
        var downloader = new DownloadService(Config);

        // act
        var fileBytes = await downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, fileBytes.Length);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(fileBytes));
    }

    [Fact]
    public async Task DownloadProgressChangedTest()
    {
        // arrange
        var downloader = new DownloadService(Config);
        var progressChangedCount = (int)Math.Ceiling((double)DummyFileHelper.FileSize16Kb / Config.BufferBlockSize);
        var progressCounter = 0;
        downloader.DownloadProgressChanged += (s, e) => Interlocked.Increment(ref progressCounter);

        // act
        await downloader.DownloadFileTaskAsync(URL);

        // assert
        // Note: some times received bytes on read stream method was less than block size!
        Assert.True(progressChangedCount <= progressCounter);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
        Assert.True(downloader.Package.IsSaveComplete);
        Assert.False(downloader.Package.IsSaving);
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
        var downloader = new DownloadService(Config);
        downloader.DownloadFileCompleted += (s, e) => {
            if (e.Cancelled && e.Error != null)
            {
                cancellationsOccurrenceCount++;
            }
            else
            {
                downloadCompletedSuccessfully = true;
            }
        };
        downloader.DownloadStarted += async delegate {
            if (expectedStopCount > stopCount)
            {
                // Stopping after start of downloading
                await downloader.CancelTaskAsync();
                stopCount++;
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName());
        while (expectedStopCount > downloadFileExecutionCounter++)
        {
            // resume download from stopped point.
            await downloader.DownloadFileTaskAsync(downloader.Package);
        }
        var stream = File.ReadAllBytes(downloader.Package.FileName);

        // assert
        Assert.True(File.Exists(downloader.Package.FileName));
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(expectedStopCount, stopCount);
        Assert.Equal(expectedStopCount, cancellationsOccurrenceCount);
        Assert.True(downloadCompletedSuccessfully);
        Assert.True(DummyFileHelper.File16Kb.SequenceEqual(stream.ToArray()));

        File.Delete(downloader.Package.FileName);
    }

    [Fact]
    public async Task PauseResumeDownloadTest()
    {
        // arrange
        var expectedPauseCount = 2;
        var pauseCount = 0;
        var downloadCompletedSuccessfully = false;
        var downloader = new DownloadService(Config);
        downloader.DownloadFileCompleted += (s, e) => {
            if (e.Cancelled == false && e.Error is null)
                downloadCompletedSuccessfully = true;
        };
        downloader.DownloadProgressChanged += delegate {
            if (expectedPauseCount > pauseCount)
            {
                // Stopping after start of downloading
                downloader.Pause();
                pauseCount++;
                downloader.Resume();
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName());
        var stream = File.ReadAllBytes(downloader.Package.FileName);

        // assert
        Assert.False(downloader.IsPaused);
        Assert.True(File.Exists(downloader.Package.FileName));
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(expectedPauseCount, pauseCount);
        Assert.True(downloadCompletedSuccessfully);
        Assert.True(DummyFileHelper.File16Kb.SequenceEqual(stream.ToArray()));

        File.Delete(downloader.Package.FileName);
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

        var config = (DownloadConfiguration)Config.Clone();
        config.BufferBlockSize = 1024;
        var downloader = new DownloadService(config);
        downloader.DownloadProgressChanged += (s, e) => {
            totalProgressedByteSize += e.ProgressedByteSize;
            totalReceivedBytes += e.ReceivedBytes.Length;
            if (expectedStopCount > stopCount)
            {
                // Stopping after start of downloading
                downloader.CancelAsync();
                stopCount++;
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(URL);
        while (expectedStopCount > downloadFileExecutionCounter++)
        {
            // resume download from stopped point.
            await downloader.DownloadFileTaskAsync(downloader.Package);
        }

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalProgressedByteSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalReceivedBytes);
    }

    [Fact]
    public async Task StopResumeDownloadOverFirstPackagePositionTest()
    {
        // arrange
        var cancellationCount = 4;
        var downloader = new DownloadService(Config);
        var isSavingStateOnCancel = false;
        var isSavingStateBeforCancel = false;

        downloader.DownloadProgressChanged += async (s, e) => {
            isSavingStateBeforCancel |= downloader.Package.IsSaving;
            if (--cancellationCount > 0)
            {
                // Stopping after start of downloading
                await downloader.CancelTaskAsync();
            }
        };

        // act
        var result = await downloader.DownloadFileTaskAsync(URL);
        // check point of package for once time
        var firstCheckPointPackage = JsonConvert.SerializeObject(downloader.Package);

        while (downloader.IsCancelled)
        {
            isSavingStateOnCancel |= downloader.Package.IsSaving;
            var restoredPackage = JsonConvert.DeserializeObject<DownloadPackage>(firstCheckPointPackage);

            // resume download from first stopped point.
            result = await downloader.DownloadFileTaskAsync(restoredPackage);
        }

        // assert
        Assert.True(downloader.Package.IsSaveComplete);
        Assert.False(downloader.Package.IsSaving);
        Assert.False(isSavingStateOnCancel);
        Assert.True(isSavingStateBeforCancel);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, result.Length);
    }

    [Fact]
    public async Task TestTotalReceivedBytesWhenResumeDownload()
    {
        // arrange
        var canStopDownload = true;
        var totalDownloadSize = 0L;
        var lastProgressPercentage = 0.0;

        var config = (DownloadConfiguration)Config.Clone();
        config.BufferBlockSize = 1024;
        config.ChunkCount = 1;
        var downloader = new DownloadService(config);
        downloader.DownloadProgressChanged += async (s, e) => {
            totalDownloadSize += e.ReceivedBytes.Length;
            lastProgressPercentage = e.ProgressPercentage;
            if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
            {
                // Stopping after start of downloading
                await downloader.CancelTaskAsync();
                canStopDownload = false;
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(URL);
        await downloader.DownloadFileTaskAsync(downloader.Package); // resume download from stopped point.

        // assert
        Assert.True(downloader.Package.IsSaveComplete);
        Assert.False(downloader.IsCancelled);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalDownloadSize);
        Assert.Equal(100.0, lastProgressPercentage);
    }

    [Fact]
    public async Task TestTotalReceivedBytesOnResumeDownloadWhenLostDownloadedData()
    {
        // arrange
        var canStopDownload = true;
        var totalDownloadSize = 0L;
        var lastProgressPercentage = 0.0;

        var config = (DownloadConfiguration)Config.Clone();
        config.BufferBlockSize = 1024;
        config.ChunkCount = 1;
        var downloader = new DownloadService(config);
        downloader.DownloadProgressChanged += (s, e) => {
            totalDownloadSize = e.ReceivedBytesSize;
            lastProgressPercentage = e.ProgressPercentage;
            if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
            {
                // Stopping after start of downloading
                downloader.CancelAsync();
                canStopDownload = false;
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(URL);
        downloader.Package.Storage.Dispose(); // set position to zero
        await downloader.DownloadFileTaskAsync(downloader.Package); // resume download from stopped point.

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalDownloadSize);
        Assert.Equal(100.0, lastProgressPercentage);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
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
        var downloader = new DownloadService(Config);
        downloader.DownloadProgressChanged += (s, e) => {
            averageSpeed = ((averageSpeed * progressCounter) + e.BytesPerSecondSpeed) / (progressCounter + 1);
            progressCounter++;
        };

        // act
        await downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.True(averageSpeed <= Config.MaximumBytesPerSecond * 1.5, $"Average Speed: {averageSpeed} , Speed Limit: {Config.MaximumBytesPerSecond}");
    }

    [Fact]
    public async Task DynamicSpeedLimitTest()
    {
        // arrange
        double upperTolerance = 1.5; // 50% upper than expected avg speed
        double expectedAverageSpeed = DummyFileHelper.FileSize16Kb / 32; // == (256*16 + 512*8 + 1024*4 + 2048*2) / 32
        double averageSpeed = 0;
        var progressCounter = 0;
        const int oneSpeedStepSize = 4096; // DummyFileHelper.FileSize16Kb / 4

        Config.MaximumBytesPerSecond = 256; // Byte/s
        var downloader = new DownloadService(Config);

        downloader.DownloadProgressChanged += (s, e) => {
            averageSpeed += e.BytesPerSecondSpeed;
            progressCounter++;

            var pow = Math.Ceiling((double)e.ReceivedBytesSize / oneSpeedStepSize);
            Config.MaximumBytesPerSecond = 128 * (int)Math.Pow(2, pow); // 256, 512, 1024, 2048
        };

        // act
        await downloader.DownloadFileTaskAsync(URL);
        averageSpeed /= progressCounter;

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.True(averageSpeed <= expectedAverageSpeed * upperTolerance,
            $"Avg Speed: {averageSpeed} , Expected Avg Speed Limit: {expectedAverageSpeed * upperTolerance}, " +
            $"Progress Count: {progressCounter}");
    }

    [Fact]
    public async Task TestSizeWhenDownloadOnMemoryStream()
    {
        // arrange
        var downloader = new DownloadService(Config);

        // act
        using var stream = await downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, stream.Length);
    }

    [Fact]
    public async Task TestTypeWhenDownloadOnMemoryStream()
    {
        // arrange
        var downloader = new DownloadService(Config);

        // act
        using var stream = await downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.True(stream is MemoryStream);
    }

    [Fact]
    public async Task TestContentWhenDownloadOnMemoryStream()
    {
        // arrange
        var downloader = new DownloadService(Config);

        // act
        using var stream = await downloader.DownloadFileTaskAsync(URL);
        var memStream = stream as MemoryStream;

        // assert
        Assert.True(DummyFileHelper.File16Kb.SequenceEqual(memStream.ToArray()));
    }

    [Fact(Timeout = 60_000)]
    public async Task Download256BytesRangeOfFileTest()
    {
        // arrange
        Config.RangeDownload = true;
        Config.RangeLow = 256;
        Config.RangeHigh = 511;
        var totalSize = Config.RangeHigh - Config.RangeLow + 1;
        var downloader = new DownloadService(Config);

        // act
        using var stream = await downloader.DownloadFileTaskAsync(URL);
        var bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.NotNull(stream);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(totalSize, downloader.Package.TotalFileSize);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
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
        var downloader = new DownloadService(Config);

        // act
        using var stream = await downloader.DownloadFileTaskAsync(URL);
        var bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.NotNull(stream);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(totalSize, downloader.Package.TotalFileSize);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
        for (int i = 0; i < totalSize; i++)
            Assert.Equal((byte)i, bytes[i]);
    }

    [Fact]
    public async Task TestDownloadParallelVsHalfOfChunks()
    {
        // arrange
        var maxParallelCountTasks = Config.ChunkCount / 2;
        Config.ParallelCount = maxParallelCountTasks;
        var downloader = new DownloadService(Config);
        var actualMaxParallelCountTasks = 0;
        downloader.ChunkDownloadProgressChanged += (s, e) => {
            actualMaxParallelCountTasks = Math.Max(actualMaxParallelCountTasks, e.ActiveChunks);
        };

        // act
        using var stream = await downloader.DownloadFileTaskAsync(URL);
        var bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.True(maxParallelCountTasks >= actualMaxParallelCountTasks);
        Assert.NotNull(stream);
        Assert.Equal(DummyFileHelper.FileSize16Kb, stream.Length);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
        for (int i = 0; i < DummyFileHelper.FileSize16Kb; i++)
            Assert.Equal((byte)i, bytes[i]);
    }

    [Fact(Timeout = 10_000)]
    public async Task TestResumeImmediatelyAfterCanceling()
    {
        // arrange
        var canStopDownload = true;
        var lastProgressPercentage = 0d;
        bool? stopped = null;
        var downloader = new DownloadService(Config);
        downloader.DownloadFileCompleted += (s, e) => stopped ??= e.Cancelled;
        downloader.DownloadProgressChanged += (s, e) => {
            if (canStopDownload && e.ProgressPercentage > 50)
            {
                canStopDownload = false;
                downloader.CancelAsync();
            }
            else if (!canStopDownload && lastProgressPercentage <= 0)
            {
                lastProgressPercentage = e.ProgressPercentage;
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(URL);
        using var stream = await downloader.DownloadFileTaskAsync(downloader.Package); // resume

        // assert
        Assert.True(stopped);
        Assert.True(lastProgressPercentage > 50);
        Assert.True(downloader.Package.IsSaveComplete);
        Assert.False(downloader.IsCancelled);
    }

    [Fact]
    public async Task KeepFileWhenDownloadFailedTest()
    {
        await KeepOrRemoveFileWhenDownloadFailedTest(false);
    }

    [Fact]
    public async Task RemoveFileWhenDownloadFailedTest()
    {
        await KeepOrRemoveFileWhenDownloadFailedTest(true);
    }

    private async Task KeepOrRemoveFileWhenDownloadFailedTest(bool clearFileAfterFailure)
    {
        // arrange
        Config.MaxTryAgainOnFailover = 0;
        Config.ClearPackageOnCompletionWithFailure = clearFileAfterFailure;
        var downloadService = new DownloadService(Config);
        var filename = Path.GetTempFileName();
        var url = DummyFileHelper.GetFileWithFailureAfterOffset(DummyFileHelper.FileSize16Kb, DummyFileHelper.FileSize16Kb / 2);

        // act
        await downloadService.DownloadFileTaskAsync(url, filename);

        // assert
        Assert.Equal(filename, downloadService.Package.FileName);
        Assert.False(downloadService.Package.IsSaveComplete);
        Assert.False(downloadService.Package.IsSaving);
        Assert.NotEqual(clearFileAfterFailure, File.Exists(filename));
    }

    [Fact]
    public async Task TestRetryDownloadAfterTimeout()
    {
        await testRetryDownloadAfterFailure(true);
    }

    [Fact]
    public async Task TestRetryDownloadAfterFailure()
    {
        await testRetryDownloadAfterFailure(false);
    }

    private async Task testRetryDownloadAfterFailure(bool timeout)
    {
        // arrange
        Exception error = null;
        var fileSize = DummyFileHelper.FileSize16Kb;
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
        downloadService.DownloadFileCompleted += (s, e) => error = e.Error;

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
        var downloader = new DownloadService(Config);

        // act
        var file1 = await downloader.DownloadFileTaskAsync(url1);
        var file2 = await downloader.DownloadFileTaskAsync(url2);
        var file3 = await downloader.DownloadFileTaskAsync(url3);

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
        var cancelltionTokenSource = new CancellationTokenSource();
        var downloader = new DownloadService(Config);
        downloader.DownloadFileCompleted += (s, e) => downloadCancelled = e.Cancelled;
        downloader.DownloadProgressChanged += (s, e) => {
            downloadProgress = e.ProgressPercentage;
            if (e.ProgressPercentage > 10)
            {
                // Stopping after 10% progress of downloading
                cancelltionTokenSource.Cancel();
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(URL, cancelltionTokenSource.Token);

        // assert
        Assert.True(downloadCancelled);
        Assert.True(downloader.IsCancelled);
        Assert.True(downloader.Status == DownloadStatus.Stopped);
        Assert.True(downloadProgress > 10);
    }

    [Fact]
    public async Task TestResumeDownloadWithAnotherUrl()
    {
        // arrange
        var url1 = DummyFileHelper.GetFileWithNameUrl("file1.dat", DummyFileHelper.FileSize16Kb);
        var url2 = DummyFileHelper.GetFileWithNameUrl("file2.dat", DummyFileHelper.FileSize16Kb);
        var canStopDownload = true;
        var totalDownloadSize = 0L;
        var config = (DownloadConfiguration)Config.Clone();
        config.BufferBlockSize = 1024;
        config.ChunkCount = 4;
        var downloader = new DownloadService(config);
        downloader.DownloadProgressChanged += (s, e) => {
            totalDownloadSize = e.ReceivedBytesSize;
            if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
            {
                // Stopping after start of downloading
                downloader.CancelAsync();
                canStopDownload = false;
            }
        };

        // act
        await downloader.DownloadFileTaskAsync(url1);
        await downloader.DownloadFileTaskAsync(downloader.Package, url2); // resume download with new url2.

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalDownloadSize);
        Assert.Equal(downloader.Package.Storage.Length, DummyFileHelper.FileSize16Kb);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
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
        var totalSize = DummyFileHelper.FileSize16Kb;
        var chunkSize = totalSize / Config.ChunkCount;
        var downloader = new DownloadService(Config);
        var urls = Enumerable.Range(1, urlsCount)
            .Select(i => DummyFileHelper.GetFileWithNameUrl("testfile_" + i, totalSize, (byte)i))
            .ToArray();

        // act
        using var stream = await downloader.DownloadFileTaskAsync(urls);
        var bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.NotNull(stream);
        Assert.Equal(totalSize, stream.Length);
        Assert.Equal(totalSize, downloader.Package.TotalFileSize);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
        for (int i = 0; i < totalSize; i++)
        {
            var chunkIndex = (byte)(i / chunkSize);
            var expectedByte = (chunkIndex % urlsCount) + 1;
            Assert.Equal(expectedByte, bytes[i]);
        }
    }
}