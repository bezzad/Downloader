using System.IO;
using System.Threading.Tasks;
using Downloader.Test.Properties;

namespace Downloader.Test
{
    internal static class DownloadTestHelper
    {
        public const string TempFilesExtension = ".temp";
        public const string File1KbName = "file_example_JSON_1kb.json";
        public const string File150KbName = "file-sample_150kB.pdf";
        public const string File10MbName = "zip_10MB.zip";
        public const int FileSize1Kb = 15547;
        public const int FileSize150Kb = 142786;
        public const int FileSize10Mb = 10679630;

        public static readonly string File1KbUrl = $"https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/{File1KbName}";
        public static readonly string File150KbUrl = $"https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/{File150KbName}";
        public static readonly string File10MbUrl = $"https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/{File10MbName}";
        public static readonly string TempDirectory = Path.GetTempPath();

        public static byte[] File1Kb => Resources.file_example_JSON_1kb;
        public static byte[] File150Kb => Resources.file_sample_150kB;
        public static byte[] File10Mb => Resources.zip_10MB;

        public static async void CancelAfterDownloading(this IDownloadService ds, int delayMs)
        {
            while (ds.IsBusy == false)
            {
                await Task.Delay(delayMs);
            }

            ds.CancelAsync();
        }
    }
}