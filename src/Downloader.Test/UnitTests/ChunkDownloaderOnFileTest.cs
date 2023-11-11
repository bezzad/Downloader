using Downloader.DummyHttpServer;
using System.IO;

namespace Downloader.Test.UnitTests;

public class ChunkDownloaderOnFileTest : ChunkDownloaderTest
{
    public ChunkDownloaderOnFileTest()
    {
        var path = Path.GetTempFileName();
        Configuration = new DownloadConfiguration {
            BufferBlockSize = 1024,
            ChunkCount = 16,
            ParallelDownload = true,
            ParallelCount = 8,
            MaxTryAgainOnFailover = 100,
            MinimumSizeOfChunking = 16,
            Timeout = 100,
        };
        Storage = new ConcurrentStream(path, DummyFileHelper.FileSize16Kb);
    }
}
