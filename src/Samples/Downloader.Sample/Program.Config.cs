using System.Net;
using System.Reflection;

namespace Downloader.Sample;

public static partial class Program
{
    private static DownloadConfiguration GetDownloadConfiguration()
    {
        CookieContainer cookies = new();
        cookies.Add(new Cookie("download-type", "test") { Domain = "domain.com" });

        return new DownloadConfiguration {
            // usually, hosts support max to 8000 bytes, default values is 8000
            BufferBlockSize = 8000,
            // file parts to download, default value is 1
            ChunkCount = 8,
            // number of parallel downloads. The default value is the same as the chunk count
            ParallelCount = 4,
            // download speed limited to 20MB/s, default values is zero or unlimited
            MaximumBytesPerSecond = 1024 * 1024 * 20,
            // the maximum number of times to fail
            MaxTryAgainOnFailure = 50_000,
            // release memory buffer after each 500MB
            MaximumMemoryBufferBytes = 1024 * 1024 * 500,
            // download parts of file as parallel or not. Default value is false
            ParallelDownload = true,
            // timeout (millisecond) per stream block reader, default value is 1000
            BlockTimeout = 3000,
            // Timeout of the http client
            HttpClientTimeout = 3000,
            // set true if you want to download just a specific range of bytes of a large file
            RangeDownload = false,
            // floor offset of download range of a large file
            RangeLow = 0,
            // ceiling offset of download range of a large file
            RangeHigh = 0,
            // Clear package and downloaded data when download completed with failure, default value is false
            ClearPackageOnCompletionWithFailure = true,
            // minimum size of file to enable chunking or download a file in multiple parts, default value is 512
            MinimumSizeOfChunking = 1024,
            // minimum size of a single chunk, 0 disables this, default is 0
            MinimumChunkSize = 0,
            // Get on demand downloaded data with ReceivedBytes on downloadProgressChanged event 
            EnableLiveStreaming = false,
            // The download metadata stored in filename.ext.download file and if you want you can to continue from last position automatically
            ResumeDownloadIfCan = true,
            // config and customize request headers
            RequestConfiguration = {
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