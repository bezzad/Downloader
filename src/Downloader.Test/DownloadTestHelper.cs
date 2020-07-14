using System.Threading.Tasks;

namespace Downloader.Test
{
    internal static class DownloadTestHelper
    {
        public static async void CancelAfterDownloading(this IDownloadService ds, int delayMs)
        {
            while (ds.IsBusy == false)
                await Task.Delay(delayMs);

            ds.CancelAsync();
        }
    }
}
