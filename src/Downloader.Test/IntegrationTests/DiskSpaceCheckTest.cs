using Downloader.Extensions;

namespace Downloader.Test.IntegrationTests;

// FileHelper.FreeSpaceOf is global, so these tests must not run beside other downloads.
[CollectionDefinition(nameof(DiskSpaceCheckTest), DisableParallelization = true)]
public class DiskSpaceCheckCollection;

[Collection(nameof(DiskSpaceCheckTest))]
public class DiskSpaceCheckTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Size = 10 * 1024;

    private static async Task<DownloadService> DownloadWithFreeSpace(long free, bool check)
    {
        Func<string, long> old = FileHelper.FreeSpaceOf;
        FileHelper.FreeSpaceOf = _ => free;
        try
        {
            DownloadService service = new(new DownloadConfiguration { CheckDiskSizeBeforeDownload = check });
            string file = Path.Combine(DummyFileHelper.TempDirectory, Guid.NewGuid().ToString("N") + ".bin");
            await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(Size), file);
            if (File.Exists(file))
                File.Delete(file);
            return service;
        }
        finally
        {
            FileHelper.FreeSpaceOf = old;
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task TooLittleFreeSpaceFailsBeforeTransfer()
    {
        // act
        await using DownloadService service = await DownloadWithFreeSpace(100, check: true);

        // assert
        Assert.Equal(DownloadStatus.Failed, service.Status);
        Assert.Equal(0, service.Package.ReceivedBytesSize);
    }

    [Fact(Timeout = 60_000)]
    public async Task DisabledCheckNeverThrows()
    {
        // act
        await using DownloadService service = await DownloadWithFreeSpace(100, check: false);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
    }

    [Fact(Timeout = 60_000)]
    public async Task EnoughFreeSpaceDownloads()
    {
        // act
        await using DownloadService service = await DownloadWithFreeSpace(Size, check: true);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
    }
}
