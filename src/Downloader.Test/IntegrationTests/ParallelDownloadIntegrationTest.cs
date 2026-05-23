namespace Downloader.Test.IntegrationTests;

public class ParallelDownloadIntegrationTest : DownloadIntegrationTest
{
    public ParallelDownloadIntegrationTest(ITestOutputHelper output) : base(output)
    {
        Config = new DownloadConfiguration {
            ParallelDownload = true,
            BufferBlockSize = 512,
            ParallelCount = 2,
            ChunkCount = 4,
            MaxTryAgainOnFailure = 100,
            BlockTimeout = 3000,
            HttpClientTimeout = 10_000
        };

        Downloader = new DownloadService(Config, LogFactory);
        Downloader.DownloadFileCompleted += DownloadFileCompleted;
    }
}