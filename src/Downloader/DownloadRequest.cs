using System.Runtime.CompilerServices;

// The Downloader.Test will have access to the internal types and members
[assembly: InternalsVisibleTo("Downloader.Test,PublicKey="+
    "0024000004800000940000000602000000240000525341310004000001000100e5f0f44edd82a5" +
    "799d2665e8027542feb70dda8213d3243ee9c6719cf0d6188d675105337ab3f4d299851fb1c578" +
    "d09a9c4e03b168849190b58b2ffee64282db518717319ed4e187953341184b4e91b6b7599a7b0e" +
    "eb7e17247b9728e23e100285183fdfcf5a4b9adf21c5c61834159df6c28b322270e0558cfb7e46" +
    "b28658db")]
namespace Downloader
{
    internal class DownloadRequest
    {
        public IDownloadService Downloader;
        public IDownloadInfo DownloadInfo { get; }
        public string Id => DownloadInfo?.Url;
        public bool IsBusy => Downloader?.IsBusy == true;

        public DownloadRequest(IDownloadInfo downloadInfo, IDownloadService downloader)
        {
            DownloadInfo = downloadInfo;
            Downloader = downloader;
        }
    }
}
