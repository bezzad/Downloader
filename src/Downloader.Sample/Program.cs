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
            ConsoleProgress = new ProgressBar { BlockCount = 60 };
            Console.WriteLine("Downloading...");

            var ds = new DownloadService();
            ds.DownloadProgressChanged += OnDownloadProgressChanged;
            ds.DownloadFileCompleted += OnDownloadFileCompleted;
            // ds.DownloadFileAsync("https://download.taaghche.com/download/DBXP126H5eLD7avDHjMQp02IVVpnPnTO", "D:\\test.pdf", 10);
            //ds.DownloadFileAsync("https://dl2.1-movies.ir/1/serial/Westworld/s3/1080p/Westworld.S03E02.The.Winter.Line.1080p.mkv.T8d7d0ac4bd25b43.mkv?md5=16afa444fd192be03374ffe0ef7a9d68&expires=1587796472", "D:\\Westworld.S03E02.The.Winter.Line.1080p.mkv", 10);
            ds.DownloadFileAsync("http://dl3.mojdl.com/upload/Movies/2019/1917/1917.2019.1080p.GalaxyRG.%5BMojoo%5D.mkv", @"C:\Users\Behza\Videos\FILIM\1917.mkv", 10);

            tcs.Task.Wait();
            Console.ReadKey();
        }

        private static async void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            await Task.Delay(1000);
            Console.WriteLine();

            if (e.Cancelled)
            {
                Console.WriteLine("Download canceled!");
                tcs.TrySetCanceled(); // exit with error
            }
            else if (e.Error != null)
            {
                Console.Error.WriteLine(e.Error);
                tcs.TrySetException(e.Error); // exit with error
            }
            else
            {
                Console.WriteLine("Download completed successfully.");
                tcs.TrySetResult(0); // exit with no error
            }
        }

        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Title = $"Downloading ({e.ProgressPercentage})";
            ConsoleProgress.Report(e.ProgressPercentage / 100);
        }
    }
}
