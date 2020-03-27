using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace Downloader.Sample
{
    class Program
    {
        private static ProgressBar ConsoleProgress { get; set; }

        static async Task Main(string[] args)
        {
            ConsoleProgress = new ProgressBar { BlockCount = 60 };
            Console.WriteLine("Downloading...");

            var downloadOpt = new DownloadConfiguration()
            {
                ParallelDownload = true,
                BufferBlockSize = 10240, // max 8000
                ChunkCount = 16,
                MaxTryAgainOnFailover = int.MaxValue
            };
             var ds = new DownloadService(downloadOpt);
             ds.DownloadProgressChanged += OnDownloadProgressChanged;
             ds.DownloadFileCompleted += OnDownloadFileCompleted;
             var file = Path.Combine(Path.GetTempPath(), "zip_10MB8.zip");
            await ds.DownloadFileAsync("https://file-examples.com/wp-content/uploads/2017/02/zip_10MB.zip", file);
            Console.WriteLine();
            Console.WriteLine(file);

            // for (var i = 1; i <= 22; i++)
            // {
            //     var ds = new DownloadService(downloadOpt);
            //     ds.DownloadProgressChanged += OnDownloadProgressChanged;
            //     ds.DownloadFileCompleted += OnDownloadFileCompleted;
            //     await ds.DownloadFileAsync(
            //         $@"http://dl1.tvto.ga/Series/Person%20of%20Interest/S02/Person.of.Interest.S02E{i}.720p.BluRay.x265.TagName.mkv",
            //         $@"C:\Users\Behza\Videos\FILIM\Person of Interest\S02\PersonOfInterest.S02E{i}.mkv");
            // }

            Console.ReadKey();
        }

        private static async void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            await Task.Delay(1000);
            Console.WriteLine();

            if (e.Cancelled)
            {
                Console.WriteLine("Download canceled!");
            }
            else if (e.Error != null)
            {
                Console.Error.WriteLine(e.Error);
            }
            else
            {
                Console.WriteLine("Download completed successfully.");
            }
        }

        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            var nonZeroSpeed = e.BytesPerSecondSpeed == 0 ? 0.0001 : e.BytesPerSecondSpeed;
            var estimateTime = (int)((e.TotalBytesToReceive - e.BytesReceived) / nonZeroSpeed);
            var isMins = estimateTime >= 60;
            var timeLeftUnit = "seconds";
            if (isMins)
            {
                timeLeftUnit = "mins";
                estimateTime /= 60;
            }

            Console.Title = $"{e.ProgressPercentage:N3}%  -  {CalcMemoryMensurableUnit(e.BytesPerSecondSpeed)}/s  -  " +
                            $"[{CalcMemoryMensurableUnit(e.BytesReceived)} of {CalcMemoryMensurableUnit(e.TotalBytesToReceive)}], {estimateTime} {timeLeftUnit} left";
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
