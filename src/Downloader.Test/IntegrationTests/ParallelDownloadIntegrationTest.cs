namespace Downloader.Test.IntegrationTests;

public class ParallelDownloadIntegrationTest : DownloadIntegrationTest
{
    public ParallelDownloadIntegrationTest()
    {
        Config = new DownloadConfiguration {
            ParallelDownload = true,
            BufferBlockSize = 1024,
            ParallelCount = 4,
            ChunkCount = 8,
            MaxTryAgainOnFailover = 100
        };
    }
}
