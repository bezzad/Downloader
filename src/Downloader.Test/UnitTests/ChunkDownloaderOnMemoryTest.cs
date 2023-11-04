namespace Downloader.Test.UnitTests;

public class ChunkDownloaderOnMemoryTest : ChunkDownloaderTest
{
    public ChunkDownloaderOnMemoryTest()
    {
        Configuration = new DownloadConfiguration {
            BufferBlockSize = 1024,
            ChunkCount = 16,
            ParallelDownload = true,
            MaxTryAgainOnFailover = 100,
            MinimumSizeOfChunking = 16,
            Timeout = 100,
        };
        Storage = new ConcurrentStream();
    }
}
