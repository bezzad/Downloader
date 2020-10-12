using System.Threading.Tasks;

namespace Downloader.Test
{
    internal static class DownloadTestHelper
    {
        public static string File1KbUrl { get; } = "https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/file_example_JSON_1kb.json";
        public static string File150KbUrl { get; } = "https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/file-sample_150kB.pdf";
        public static string File1MbUrl { get; } = "https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/excel_sample.xls";
        public static string File8MbUrl { get; } = "https://download.taaghche.com/download/kaaurZ7Rj7yQQrd4P7MWwUqtTInMypDA";
        public static string File10MbUrl { get; } = "https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/zip_10MB.zip";
        public static string File100MbUrl { get; } = "http://ipv4.download.thinkbroadband.com/100MB.zip";

        public static int FileSize1Kb { get; } = 20471;
        public static int FileSize150Kb { get; } = 142786;
        public static int FileSize1Mb { get; } = 672256;
        public static int FileSize8Mb { get; } = 8587760;
        public static int FileSize10Mb { get; } = 10679630;
        public static int FileSize100Mb { get; } = 100 * 1024 * 1024;


        public static async void CancelAfterDownloading(this IDownloadService ds, int delayMs)
        {
            while (ds.IsBusy == false)
                await Task.Delay(delayMs);

            ds.CancelAsync();
        }
    }
}
