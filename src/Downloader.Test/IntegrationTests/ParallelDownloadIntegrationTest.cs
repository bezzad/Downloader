namespace Downloader.Test.IntegrationTests;

public class ParallelDownloadIntegrationTest : DownloadIntegrationTest
{
    public ParallelDownloadIntegrationTest(ITestOutputHelper output) : base(output)
    {
        Config = new DownloadConfiguration {
            ParallelDownload = true,
            BufferBlockSize = 1024,
            ParallelCount = 4,
            ChunkCount = 8,
            MaxTryAgainOnFailure = 100
        };

        Downloader = new DownloadService(Config, LogFactory);
        Downloader.DownloadFileCompleted += DownloadFileCompleted;
    }
}