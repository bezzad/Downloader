namespace Downloader.Test.IntegrationTests;

public class SerialDownloadIntegrationTest : DownloadIntegrationTest
{
    public SerialDownloadIntegrationTest()
    {
        Config = new DownloadConfiguration {
            ParallelDownload = false,
            BufferBlockSize = 1024,
            ParallelCount = 4,
            ChunkCount = 4,
            MaxTryAgainOnFailover = 100
        };
    }
}
