using Downloader.DummyHttpServer;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Downloader.Test.IntegrationTests;

public abstract class DownloadIntegrationTest
{
    private readonly ITestOutputHelper _output;
    protected string URL { get; set; }
    protected string Filename { get; set; }
    protected string FilePath { get; set; }
    protected DownloadConfiguration Config { get; set; }
    protected DownloadService Downloader { get; set; }

    public DownloadIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        Filename = Path.GetRandomFileName();
        FilePath = Path.Combine(Path.GetTempPath(), Filename);
        URL = DummyFileHelper.GetFileWithNameUrl(Filename, DummyFileHelper.FileSize16Kb);
    }

    protected void DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
    {
        if (e.Error is not null)
        {
            _output.WriteLine("Error when completed: " + e.Error.Message.ToString());
        }
    }

    [Fact]
    public async Task DownloadUrlWithFilenameOnMemoryTest()
    {
        // arrange
        var downloadCompletedSuccessfully = false;
        var resultMessage = "";
        Downloader.DownloadFileCompleted += (s, e) => {
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
        using var memoryStream = await Downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.True(downloadCompletedSuccessfully, resultMessage);
        Assert.NotNull(memoryStream);
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.Null(Downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize16Kb, memoryStream.Length);
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(memoryStream));
    }

    [Fact]
    public async Task DownloadAndReadFileOnDownloadFileCompletedEventTest()
    {
        // arrange
        var destFilename = FilePath;
        byte[] downloadedBytes = null;
        var downloadCompletedSuccessfully = false;
        Downloader.DownloadFileCompleted += (s, e) => {
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
        await Downloader.DownloadFileTaskAsync(URL, destFilename);

        // assert
        Assert.True(downloadCompletedSuccessfully);
        Assert.NotNull(downloadedBytes);
        Assert.Equal(destFilename, Downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, downloadedBytes.Length);
        Assert.True(DummyFileHelper.File16Kb.SequenceEqual(downloadedBytes));

        File.Delete(destFilename);
    }

    [Fact]
    public async Task Download16KbWithoutFilenameOnDirectoryTest()
    {
        // arrange
        var dir = new DirectoryInfo(Path.GetTempPath());

        // act
        await Downloader.DownloadFileTaskAsync(URL, dir);

        // assert
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.NotNull(Downloader.Package.FileName);
        Assert.StartsWith(DummyFileHelper.TempDirectory, Downloader.Package.FileName);
        Assert.Equal(FilePath, Downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(File.OpenRead(Downloader.Package.FileName)));

        File.Delete(FilePath);
    }

    [Fact]
    public async Task Download16KbWithFilenameTest()
    {
        // act
        await Downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName());

        // assert
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.NotNull(Downloader.Package.FileName);
        Assert.StartsWith(DummyFileHelper.TempDirectory, Downloader.Package.FileName);
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(File.OpenRead(Downloader.Package.FileName)));

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
        var fileBytes = await Downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.Equal(expected: DummyFileHelper.FileSize16Kb, actual: Downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, fileBytes.Length);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(fileBytes));
    }

    [Fact]
    public async Task DownloadProgressChangedTest()
    {
        // arrange
        var progressChangedCount = (int)Math.Ceiling((double)DummyFileHelper.FileSize16Kb / Config.BufferBlockSize);
        var progressCounter = 0;
        Downloader.DownloadProgressChanged += (s, e) => Interlocked.Increment(ref progressCounter);

        // act
        await Downloader.DownloadFileTaskAsync(URL);

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
        Downloader.DownloadFileCompleted += (s, e) => {
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
        await Downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName());
        while (expectedStopCount > downloadFileExecutionCounter++)
        {
            // resume download from stopped point.
            await Downloader.DownloadFileTaskAsync(Downloader.Package);
        }
        var stream = File.ReadAllBytes(Downloader.Package.FileName);

        // assert
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(expectedStopCount, stopCount);
        Assert.Equal(expectedStopCount, cancellationsOccurrenceCount);
        Assert.True(downloadCompletedSuccessfully);
        Assert.True(DummyFileHelper.File16Kb.SequenceEqual(stream.ToArray()));

        File.Delete(Downloader.Package.FileName);
    }

    [Fact]
    public async Task PauseResumeDownloadTest()
    {
        // arrange
        var expectedPauseCount = 2;
        var pauseCount = 0;
        var downloadCompletedSuccessfully = false;
        Downloader.DownloadFileCompleted += (s, e) => {
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
        await Downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName());
        var stream = File.ReadAllBytes(Downloader.Package.FileName);

        // assert
        Assert.False(Downloader.IsPaused);
        Assert.True(File.Exists(Downloader.Package.FileName));
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(expectedPauseCount, pauseCount);
        Assert.True(downloadCompletedSuccessfully);
        Assert.True(DummyFileHelper.File16Kb.SequenceEqual(stream.ToArray()));

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
        Downloader.DownloadProgressChanged += (s, e) => {
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
        await Downloader.DownloadFileTaskAsync(URL);
        while (expectedStopCount > downloadFileExecutionCounter++)
        {
            // resume download from stopped point.
            await Downloader.DownloadFileTaskAsync(Downloader.Package);
        }

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalProgressedByteSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalReceivedBytes);
    }

    [Fact]
    public async Task StopResumeDownloadOverFirstPackagePositionTest()
    {
        // arrange
        var cancellationCount = 4;
        var isSavingStateOnCancel = false;
        var isSavingStateBeforCancel = false;

        Downloader.DownloadProgressChanged += async (s, e) => {
            isSavingStateBeforCancel |= Downloader.Package.IsSaving;
            if (--cancellationCount > 0)
            {
                // Stopping after start of downloading
                await Downloader.CancelTaskAsync();
            }
        };

        // act
        var result = await Downloader.DownloadFileTaskAsync(URL);
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
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, result.Length);
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
        Downloader.DownloadProgressChanged += async (s, e) => {
            totalDownloadSize += e.ReceivedBytes.Length;
            lastProgressPercentage = e.ProgressPercentage;
            if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
            {
                // Stopping after start of downloading
                await Downloader.CancelTaskAsync();
                canStopDownload = false;
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(URL);
        await Downloader.DownloadFileTaskAsync(Downloader.Package); // resume download from stopped point.

        // assert
        Assert.True(Downloader.Package.IsSaveComplete);
        Assert.False(Downloader.IsCancelled);
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
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
        Config.BufferBlockSize = 1024;
        Config.ChunkCount = 1;
        Downloader.DownloadProgressChanged += (s, e) => {
            totalDownloadSize = e.ReceivedBytesSize;
            lastProgressPercentage = e.ProgressPercentage;
            if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
            {
                // Stopping after start of downloading
                Downloader.CancelAsync();
                canStopDownload = false;
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(URL);
        Downloader.Package.Storage.Dispose(); // set position to zero
        await Downloader.DownloadFileTaskAsync(Downloader.Package); // resume download from stopped point.

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalDownloadSize);
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

        Downloader.DownloadProgressChanged += (s, e) => {
            averageSpeed = ((averageSpeed * progressCounter) + e.BytesPerSecondSpeed) / (progressCounter + 1);
            progressCounter++;
        };

        // act
        await Downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
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


        Downloader.DownloadProgressChanged += (s, e) => {
            averageSpeed += e.BytesPerSecondSpeed;
            progressCounter++;

            var pow = Math.Ceiling((double)e.ReceivedBytesSize / oneSpeedStepSize);
            Config.MaximumBytesPerSecond = 128 * (int)Math.Pow(2, pow); // 256, 512, 1024, 2048
        };

        // act
        await Downloader.DownloadFileTaskAsync(URL);
        averageSpeed /= progressCounter;

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.True(averageSpeed <= expectedAverageSpeed * upperTolerance,
            $"Avg Speed: {averageSpeed} , Expected Avg Speed Limit: {expectedAverageSpeed * upperTolerance}, " +
            $"Progress Count: {progressCounter}");
    }

    [Fact]
    public async Task TestSizeWhenDownloadOnMemoryStream()
    {
        // arrange


        // act
        using var stream = await Downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, stream.Length);
    }

    [Fact]
    public async Task TestTypeWhenDownloadOnMemoryStream()
    {
        // arrange


        // act
        using var stream = await Downloader.DownloadFileTaskAsync(URL);

        // assert
        Assert.True(stream is MemoryStream);
    }

    [Fact]
    public async Task TestContentWhenDownloadOnMemoryStream()
    {
        // arrange


        // act
        using var stream = await Downloader.DownloadFileTaskAsync(URL);
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


        // act
        using var stream = await Downloader.DownloadFileTaskAsync(URL);

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
        using var stream = await Downloader.DownloadFileTaskAsync(URL);
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
        Downloader.ChunkDownloadProgressChanged += (s, e) => {
            actualMaxParallelCountTasks = Math.Max(actualMaxParallelCountTasks, e.ActiveChunks);
        };

        // act
        using var stream = await Downloader.DownloadFileTaskAsync(URL);
        var bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.True(maxParallelCountTasks >= actualMaxParallelCountTasks);
        Assert.NotNull(stream);
        Assert.Equal(DummyFileHelper.FileSize16Kb, stream.Length);
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(100.0, Downloader.Package.SaveProgress);
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

        Downloader.DownloadFileCompleted += (s, e) => stopped ??= e.Cancelled;
        Downloader.DownloadProgressChanged += (s, e) => {
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
        await Downloader.DownloadFileTaskAsync(URL);
        using var stream = await Downloader.DownloadFileTaskAsync(Downloader.Package); // resume

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
        var url = DummyFileHelper.GetFileWithFailureAfterOffset(DummyFileHelper.FileSize16Kb, DummyFileHelper.FileSize16Kb / 2);

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
    public async Task testRetryDownloadAfterFailure(bool timeout)
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
        var cancelltionTokenSource = new CancellationTokenSource();

        Downloader.DownloadFileCompleted += (s, e) => downloadCancelled = e.Cancelled;
        Downloader.DownloadProgressChanged += (s, e) => {
            downloadProgress = e.ProgressPercentage;
            if (e.ProgressPercentage > 10)
            {
                // Stopping after 10% progress of downloading
                cancelltionTokenSource.Cancel();
            }
        };

        // act
        await Downloader.DownloadFileTaskAsync(URL, cancelltionTokenSource.Token);

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
        var url1 = DummyFileHelper.GetFileWithNameUrl("file1.dat", DummyFileHelper.FileSize16Kb);
        var url2 = DummyFileHelper.GetFileWithNameUrl("file2.dat", DummyFileHelper.FileSize16Kb);
        var canStopDownload = true;
        var totalDownloadSize = 0L;
        Config.BufferBlockSize = 1024;
        Config.ChunkCount = 4;
        Downloader.DownloadProgressChanged += (s, e) => {
            totalDownloadSize = e.ReceivedBytesSize;
            if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
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
        Assert.Equal(DummyFileHelper.FileSize16Kb, Downloader.Package.TotalFileSize);
        Assert.Equal(DummyFileHelper.FileSize16Kb, totalDownloadSize);
        Assert.Equal(Downloader.Package.Storage.Length, DummyFileHelper.FileSize16Kb);
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
        var totalSize = DummyFileHelper.FileSize16Kb;
        var chunkSize = totalSize / Config.ChunkCount;

        var urls = Enumerable.Range(1, urlsCount)
            .Select(i => DummyFileHelper.GetFileWithNameUrl("testfile_" + i, totalSize, (byte)i))
            .ToArray();

        // act
        using var stream = await Downloader.DownloadFileTaskAsync(urls);
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
}