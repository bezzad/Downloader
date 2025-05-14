namespace Downloader.Test.UnitTests;

public class ChunkDownloaderOnFileTest : ChunkDownloaderTest
{
    public ChunkDownloaderOnFileTest(ITestOutputHelper output) : base(output)
    {
        string path = Path.GetTempFileName();
        Configuration = new DownloadConfiguration {
            BufferBlockSize = 1024,
            ChunkCount = 16,
            ParallelDownload = true,
            ParallelCount = 8,
            MaxTryAgainOnFailure = 100,
            MinimumSizeOfChunking = 16,
            Timeout = 100,
        };
        Storage = new ConcurrentStream(path, 0);
    }
}