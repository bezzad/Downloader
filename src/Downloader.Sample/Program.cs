using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Downloader.Sample
{
    class Program
    {
        private static ProgressBar ConsoleProgress { get; set; }
        private static TaskCompletionSource<int> Tcs { get; set; }

        static void Main(string[] args)
        {
            Tcs = new TaskCompletionSource<int>();
            ConsoleProgress = new ProgressBar { BlockCount = 60 };
            Console.WriteLine("Downloading...");

            var downloadOpt = new DownloadConfiguration()
            {
                ParallelDownload = true,
                BufferBlockSize = 102400,
                ChunkCount = 4,
                MaxTryAgainOnFailover = int.MaxValue
            };
            var ds = new DownloadService(downloadOpt);
            ds.DownloadProgressChanged += OnDownloadProgressChanged;
            ds.DownloadFileCompleted += OnDownloadFileCompleted;
            //ds.DownloadFileAsync("https://download.taaghche.com/download/DBXP126H5eLD7avDHjMQp02IVVpnPnTO", "D:\\test.pdf");
            ds.DownloadFileAsync("http://dl1.tvto.ga/Series/Person%20of%20Interest/S01/Person.of.Interest.S01E06.720p.BluRay.x265.TagName.mkv",
                                     @"C:\Users\Behza\Videos\FILIM\Person of Interest\PersonOfInterest.S01E06.mkv");
            // ds.DownloadFileAsync("https://uk12.uploadboy.com/d/wjmzpm2p4up7/tvncwluzjdfx3pohcavxtrg46zz4yqldjomtvf2qf3ilzjrdvcwbayp5zr6jhy3w2tzjoie7/Th.e.%20.Ge..n.t.l.e.m.e.n%202020-720p-Hardsub.mkv",
            // @"C:\Users\Behza\Videos\FILIM\TheGentlemen.2020.mkv");

            Tcs.Task.Wait();
            Console.ReadKey();
        }

        private static async void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            await Task.Delay(1000);
            Console.WriteLine();

            if (e.Cancelled)
            {
                Console.WriteLine("Download canceled!");
                Tcs.TrySetCanceled(); // exit with error
            }
            else if (e.Error != null)
            {
                Console.Error.WriteLine(e.Error);
                Tcs.TrySetException(e.Error); // exit with error
            }
            else
            {
                Console.WriteLine("Download completed successfully.");
                Tcs.TrySetResult(0); // exit with no error
            }
        }

        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Title = $"Downloading ({e.ProgressPercentage:N3}%)    " +
                            $"[{CalcMemoryMensurableUnit(e.BytesReceived)} of {CalcMemoryMensurableUnit(e.TotalBytesToReceive)}]   " +
                            $"{CalcMemoryMensurableUnit(e.BytesPerSecondSpeed)}s";
            ConsoleProgress.Report(e.ProgressPercentage / 100);
        }

        public static string CalcMemoryMensurableUnit(long bigUnSignedNumber, bool isShort = true)
        {
            var kb = bigUnSignedNumber / 1024; // · 1024 Bytes = 1 Kilobyte 
            var mb = kb / 1024; // · 1024 Kilobytes = 1 Megabyte 
            var gb = mb / 1024; // · 1024 Megabytes = 1 Gigabyte 
            var tb = gb / 1024; // · 1024 Gigabytes = 1 Terabyte 

            var b = isShort ? "B" : "Bytes";
            var k = isShort ? "KB" : "Kilobytes";
            var m = isShort ? "MB" : "Megabytes";
            var g = isShort ? "GB" : "Gigabytes";
            var t = isShort ? "TB" : "Terabytes";

            return tb > 1 ? $"{tb:N0}{t}" :
                   gb > 1 ? $"{gb:N0}{g}" :
                   mb > 1 ? $"{mb:N0}{m}" :
                   kb > 1 ? $"{kb:N0}{k}" :
                   $"{bigUnSignedNumber:N0}{b}";
        }
    }
}
