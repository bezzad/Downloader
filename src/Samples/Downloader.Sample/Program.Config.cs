using System.Net;
using System.Reflection;

namespace Downloader.Sample;

public partial class Program
{
    private static DownloadConfiguration GetDownloadConfiguration()
    {
        CookieContainer cookies = new();
        cookies.Add(new Cookie("download-type", "test") { Domain = "domain.com" });

        return new DownloadConfiguration {
            BufferBlockSize = 10240,    // usually, hosts support max to 8000 bytes, default values is 8000
            ChunkCount = 8,             // file parts to download, default value is 1
            ParallelCount = 4,          // number of parallel downloads. The default value is the same as the chunk count
            MaximumBytesPerSecond = 1024 * 1024 * 20,  // download speed limited to 20MB/s, default values is zero or unlimited
            MaxTryAgainOnFailure = 50_000,  // the maximum number of times to fail
            MaximumMemoryBufferBytes = 1024 * 1024 * 500, // release memory buffer after each 500MB
            ParallelDownload = true,    // download parts of file as parallel or not. Default value is false
            Timeout = 3000,             // timeout (millisecond) per stream block reader, default value is 1000
            RangeDownload = false,      // set true if you want to download just a specific range of bytes of a large file
            RangeLow = 0,               // floor offset of download range of a large file
            RangeHigh = 0,              // ceiling offset of download range of a large file
            ClearPackageOnCompletionWithFailure = true, // Clear package and downloaded data when download completed with failure, default value is false
            MinimumSizeOfChunking = 1024, // minimum size of chunking to download a file in multiple parts, default value is 512                                              
            ReserveStorageSpaceBeforeStartingDownload = false, // Before starting the download, reserve the storage space of the file as file size, default value is false
            EnableLiveStreaming = false, // Get on demand downloaded data with ReceivedBytes on downloadProgressChanged event 
            RequestConfiguration =
            {
                // config and customize request headers
                Accept = "*/*",
                CookieContainer = cookies,
                Headers = ["Accept-Encoding: gzip, deflate, br"], // { your custom headers }
                KeepAlive = true, // default value is false
                ProtocolVersion = HttpVersion.Version11, // default value is HTTP 1.1
                UseDefaultCredentials = false,
                // your custom user agent or your_app_name/app_version.
                UserAgent = $"DownloaderSample/{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}",
                // Proxy = new WebProxy("socks5://127.0.0.1:12000")
            }
        };
    }
}