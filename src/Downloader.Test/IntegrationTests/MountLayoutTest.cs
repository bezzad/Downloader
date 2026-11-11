namespace Downloader.Test.IntegrationTests;

/// <summary>
/// Real file system layouts (Linux only). They run only when the folders are given:
/// <c>DOWNLOADER_SMALL_MOUNT</c> = a small mount (for example a 64 MB tmpfs), and
/// <c>DOWNLOADER_BIG_MOUNT</c> = a mount with more than 100 MB free. Example:
/// <c>docker run --tmpfs /data:size=64m -e DOWNLOADER_SMALL_MOUNT=/data ...</c>
/// The small mount case fails on the old check, which measured "/" instead of the target mount.
/// </summary>
[Trait("Category", "MountLayout")]
public class MountLayoutTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const long Size = 100L * 1024 * 1024; // 100 MB

    private static async Task<(DownloadService Service, Exception Error, bool Started)> Download(string folder)
    {
        DownloadService service = new(new DownloadConfiguration { ChunkCount = 4, ParallelDownload = true });
        Exception error = null;
        bool started = false;
        service.DownloadStarted += (_, _) => started = true;
        service.DownloadFileCompleted += (_, e) => error = e.Error;
        // A unique name: CI runs the net10.0 and net11.0 tests at the same time in one folder. With one
        // fixed name the two runs moved and deleted each other's file (now reported as Failed).
        string file = Path.Combine(folder, $"f-{Guid.NewGuid():N}.bin");
        try
        {
            await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(Size), file);
        }
        finally
        {
            File.Delete(file);
            File.Delete(file + ".download");
        }

        return (service, error, started);
    }

    [Fact(Timeout = 300_000)]
    public async Task SmallTargetMountFailsBeforeTransfer()
    {
        string folder = Environment.GetEnvironmentVariable("DOWNLOADER_SMALL_MOUNT");
        if (!OperatingSystem.IsLinux() || string.IsNullOrEmpty(folder))
            return;

        // act
        (DownloadService service, Exception error, bool started) = await Download(folder);
        await using DownloadService disposable = service;

        // assert
        Assert.Equal(DownloadStatus.Failed, service.Status);
        Assert.False(started);
        IOException io = Assert.IsAssignableFrom<IOException>(error);
        Assert.StartsWith("Not enough disk space", io.Message);
        Assert.Contains($"'{folder}'", io.Message);
    }

    [Fact(Timeout = 300_000)]
    public async Task BigTargetMountDownloads()
    {
        string folder = Environment.GetEnvironmentVariable("DOWNLOADER_BIG_MOUNT");
        if (!OperatingSystem.IsLinux() || string.IsNullOrEmpty(folder))
            return;

        // act
        (DownloadService service, Exception error, bool _) = await Download(folder);
        await using DownloadService disposable = service;

        // assert
        Assert.Null(error);
        Assert.Equal(DownloadStatus.Completed, service.Status);
    }
}
