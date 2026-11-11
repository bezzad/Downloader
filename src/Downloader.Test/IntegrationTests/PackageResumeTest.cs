namespace Downloader.Test.IntegrationTests;

/// <summary>
/// H2 of the 7.0.1 review: a resume from a <see cref="DownloadPackage"/> must check the temp file and
/// the server file, or it gives a broken file that is reported as Completed.
/// </summary>
public class PackageResumeTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Size = 256 * 1024;

    private static DownloadConfiguration SlowConfig() => new() {
        ChunkCount = 4,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0,
        MaximumBytesPerSecond = 64 * 1024,
        BufferBlockSize = 1024
    };

    private static DownloadConfiguration FastConfig() => new() {
        ChunkCount = 4,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0
    };

    /// <summary>Downloads until about a third is received, then stops, and returns the package.</summary>
    private static async Task<DownloadPackage> StopInTheMiddle(string url, string file)
    {
        DownloadService service = new(SlowConfig());
        service.DownloadProgressChanged += (_, e) => {
            if (e.ProgressPercentage > 30)
                service.CancelAsync();
        };
        await service.DownloadFileTaskAsync(url, file);
        Assert.Equal(DownloadStatus.Stopped, service.Status);
        Assert.True(service.Package.ReceivedBytesSize > 0);
        return service.Package;
    }

    private static async Task<DownloadStatus> Resume(DownloadPackage package, string url = null)
    {
        await using DownloadService service = new(FastConfig());
        if (url is null)
            await service.DownloadFileTaskAsync(package);
        else
            await service.DownloadFileTaskAsync(package, url);
        return service.Status;
    }

    [Fact(Timeout = 60_000)]
    public async Task ResumeAfterTempFileWasDeletedGivesTheRightFile()
    {
        // arrange
        string file = GetTempNoFilename();
        DownloadPackage package = await StopInTheMiddle(DummyFileHelper.GetFileUrl(Size), file);
        File.Delete(package.DownloadingFileName);

        try
        {
            // act
            DownloadStatus status = await Resume(package);

            // assert
            Assert.Equal(DownloadStatus.Completed, status);
            Assert.Equal(DummyData.GenerateOrderedBytes(Size), await File.ReadAllBytesAsync(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task ResumeAfterServerFileChangedGivesOnlyTheNewFile()
    {
        // arrange: the first file is all 1s with ETag v1; the new one is all 2s with ETag v2
        string file = GetTempNoFilename();
        DownloadPackage package = await StopInTheMiddle(DummyFileHelper.GetFileWithETagUrl(Size, "v1", 1), file);

        try
        {
            // act
            DownloadStatus status = await Resume(package, DummyFileHelper.GetFileWithETagUrl(Size, "v2", 2));

            // assert
            Assert.Equal(DownloadStatus.Completed, status);
            byte[] bytes = await File.ReadAllBytesAsync(file);
            Assert.Equal(Size, bytes.Length);
            Assert.All(bytes, b => Assert.Equal(2, b));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
