namespace Downloader.Test.UnitTests;

public class ChunkDownloaderOnMemoryTest : ChunkDownloaderTest
{
    public ChunkDownloaderOnMemoryTest(ITestOutputHelper output) : base(output)
    {
        Configuration = new DownloadConfiguration {
            BufferBlockSize = 1024,
            ChunkCount = 16,
            ParallelDownload = true,
            MaxTryAgainOnFailure = 100,
            MinimumSizeOfChunking = 16,
            Timeout = 100
        };
        Storage = new ConcurrentStream(null);
    }
}