using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Downloader.Sample
{
    class Program
    {
        private static ProgressBar ConsoleProgress { get; set; }
        private static TaskCompletionSource<int> tcs { get; set; }

        static void Main(string[] args)
        {
            tcs = new TaskCompletionSource<int>();
            ConsoleProgress = new ProgressBar() { BlockCount = 60 };
            Console.WriteLine("Downloading...");

            var ds = new DownloadService();
            ds.DownloadProgressChanged += OnDownloadProgressChanged;
            ds.DownloadFileCompleted += OnDownloadFileCompleted;
            ds.DownloadFileAsync("https://download.taaghche.com/download/DBXP126H5eLD7avDHjMQp02IVVpnPnTO",
                "D:\\test.pdf");

            tcs.Task.Wait();
            Console.ReadKey();
        }

        private static void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Console.WriteLine();

            if (e.Cancelled)
            {
                Console.WriteLine("Download canceled!");
                tcs.SetResult(1); // exit with error
            }
            else if (e.Error != null)
            {
                Console.Error.WriteLine(e.Error);
                tcs.SetResult(1); // exit with error
            }
            else
            {
                Console.WriteLine("Download completed successfully.");
                tcs.SetResult(0); // exit with no error
            }
        }

        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Title = $"Downloading ({e.ProgressPercentage})";
            ConsoleProgress.Report(e.ProgressPercentage / 100);
        }
    }
}
