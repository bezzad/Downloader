using System.Threading.Tasks;

namespace Downloader
{
    public interface IDownloadService
    {
        /// <summary>
        /// Is in downloading time
        /// </summary>
        bool IsBusy { get; }
        string MainProgressName { get; }
        long DownloadSpeed { get; set; }
        DownloadPackage Package { get; set; }

        Task DownloadFileAsync(DownloadPackage package);
        Task DownloadFileAsync(string address, string fileName);
        void CancelAsync();
        void Clear();
    }
}