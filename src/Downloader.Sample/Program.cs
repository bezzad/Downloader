using Newtonsoft.Json;
using ShellProgressBar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Downloader.Sample
{
    class Program
    {
        private static ProgressBar ConsoleProgress { get; set; }
        private static ConcurrentDictionary<string, ChildProgressBar> ChildConsoleProgresses { get; set; }
        private static ProgressBarOptions ChildOption { get; set; }
        private static ProgressBarOptions ProcessBarOption { get; set; }
        private static List<DownloadItem> DownloadList { get; set; }
        private static string DownloadListFile { get; } = "DownloadList.json";



        static async Task Main(string[] args)
        {
            DownloadList = File.Exists(DownloadListFile)
                ? JsonConvert.DeserializeObject<List<DownloadItem>>(File.ReadAllText(DownloadListFile))
                : null;

            DownloadList ??= new List<DownloadItem>
            {
                new DownloadItem
                {
                    FileName = Path.Combine(Path.GetTempPath(), "100MB.zip"),
                    Url = "http://ipv4.download.thinkbroadband.com/100MB.zip"
                }
            };

            ProcessBarOption = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Green,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };
            ChildOption = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                BackgroundColor = ConsoleColor.DarkGray,
                ProgressCharacter = '─'
            };

            var downloadOpt = new DownloadConfiguration
            {
                ParallelDownload = true, // download parts of file as parallel or not
                BufferBlockSize = 10240, // usually, hosts support max to 8000 bytes
                ChunkCount = 8, // file parts to download
                MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
                OnTheFlyDownload = false, // caching in-memory or not?
                Timeout = 1000, // timeout (millisecond) per stream block reader
                RequestConfiguration = // config and customize request headers
                {
                    Accept = "*/*",
                    UserAgent = $"DownloaderSample/{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}",
                    ProtocolVersion = HttpVersion.Version11,
                    KeepAlive = false,
                    UseDefaultCredentials = false
                }
            };

            // foreach (var downloadItem in DownloadList)
            var downloadItem = DownloadList.First();
            {
                // begin download from url
                var ds = await Download(downloadItem, downloadOpt);
                // the delay for stopping completely the download
                await Task.Delay(1000);
                // after stopped download, resume that
                await ds.DownloadFileAsync(ds.Package);
                // clear download to order new of one
                ds.Clear();
            }
        }


        private static async Task<DownloadService> Download(DownloadItem downloadItem, DownloadConfiguration downloadOpt)
        {
            var ds = new DownloadService(downloadOpt);
            ds.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;
            ds.DownloadProgressChanged += OnDownloadProgressChanged;
            ds.DownloadFileCompleted += OnDownloadFileCompleted;


            Console.Clear();
            ConsoleProgress = new ProgressBar(10000, $"Downloading {Path.GetFileName(downloadItem.FileName)} file", ProcessBarOption);
            ChildConsoleProgresses = new ConcurrentDictionary<string, ChildProgressBar>();
            StopResumeDownload(ds); // Stopping after 2 second from the start of downloading.
            await ds.DownloadFileAsync(downloadItem.Url, downloadItem.FileName).ConfigureAwait(false);

            return ds;
        }

        private static async void StopResumeDownload(DownloadService ds)
        {
            while (ds.IsBusy == false)
                await Task.Delay(2000);

            ds.CancelAsync();
        }

        private static async void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            ConsoleProgress.Tick(10000);
            await Task.Delay(1000);
            Console.WriteLine();
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
                Console.Title = "100%";
            }
        }
        private static void OnChunkDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            var progress = ChildConsoleProgresses.GetOrAdd(e.ProgressId, id => ConsoleProgress.Spawn(10000, $"chunk {id}", ChildOption));
            progress.Tick((int)(e.ProgressPercentage * 100));
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
            ConsoleProgress.Tick((int)(e.ProgressPercentage * 100));
        }
        public static string CalcMemoryMensurableUnit(long bytes, bool isShortUnitName = true)
        {
            var kb = bytes / 1024; // · 1024 Bytes = 1 Kilobyte 
            var mb = kb / 1024; // · 1024 Kilobytes = 1 Megabyte 
            var gb = mb / 1024; // · 1024 Megabytes = 1 Gigabyte 
            var tb = gb / 1024; // · 1024 Gigabytes = 1 Terabyte 

            var b = isShortUnitName ? "B" : "Bytes";
            var k = isShortUnitName ? "KB" : "Kilobytes";
            var m = isShortUnitName ? "MB" : "Megabytes";
            var g = isShortUnitName ? "GB" : "Gigabytes";
            var t = isShortUnitName ? "TB" : "Terabytes";

            return tb > 1 ? $"{tb:N0}{t}" :
                   gb > 1 ? $"{gb:N0}{g}" :
                   mb > 1 ? $"{mb:N0}{m}" :
                   kb > 1 ? $"{kb:N0}{k}" :
                   $"{bytes:N0}{b}";
        }
    }
}
