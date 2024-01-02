using System.Net;
using System.Reflection;

namespace Downloader.Sample;

public partial class Program
{
    private static DownloadConfiguration GetDownloadConfiguration()
    {
        var cookies = new CookieContainer();
        cookies.Add(new Cookie("download-type", "test") { Domain = "domain.com" });

        return new DownloadConfiguration {
            BufferBlockSize = 10240,    // usually, hosts support max to 8000 bytes, default values is 8000
            ChunkCount = 8,             // file parts to download, default value is 1
            MaximumBytesPerSecond = 1024 * 1024 * 10,  // download speed limited to 10MB/s, default values is zero or unlimited
            MaxTryAgainOnFailover = 5,  // the maximum number of times to fail
            MaximumMemoryBufferBytes = 1024 * 1024 * 200, // release memory buffer after each 200MB
            ParallelDownload = true,    // download parts of file as parallel or not. Default value is false
            ParallelCount = 8,          // number of parallel downloads. The default value is the same as the chunk count
            Timeout = 3000,             // timeout (millisecond) per stream block reader, default value is 1000
            RangeDownload = false,      // set true if you want to download just a specific range of bytes of a large file
            RangeLow = 0,               // floor offset of download range of a large file
            RangeHigh = 0,              // ceiling offset of download range of a large file
            ClearPackageOnCompletionWithFailure = true, // Clear package and downloaded data when download completed with failure, default value is false
            MinimumSizeOfChunking = 1024, // minimum size of chunking to download a file in multiple parts, default value is 512                                              
            ReserveStorageSpaceBeforeStartingDownload = false, // Before starting the download, reserve the storage space of the file as file size, default value is false
            RequestConfiguration =
            {
                // config and customize request headers
                Accept = "*/*",
                CookieContainer = cookies,
                Headers = new WebHeaderCollection(),     // { your custom headers }
                KeepAlive = true,                        // default value is false
                ProtocolVersion = HttpVersion.Version11, // default value is HTTP 1.1
                UseDefaultCredentials = false,
                // your custom user agent or your_app_name/app_version.
                UserAgent = $"DownloaderSample/{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}"
                // Proxy = new WebProxy(new Uri($"socks5://127.0.0.1:9050"))
                // Proxy = new WebProxy() {
                //    Address = new Uri("http://YourProxyServer/proxy.pac"),
                //    UseDefaultCredentials = false,
                //    Credentials = System.Net.CredentialCache.DefaultNetworkCredentials,
                //    BypassProxyOnLocal = true
                // }
            }
        };
    }
}