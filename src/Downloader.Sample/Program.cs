using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ShellProgressBar;

namespace Downloader.Sample
{
    internal static class Program
    {
        private static ProgressBar ConsoleProgress { get; set; }
        private static ConcurrentDictionary<string, ChildProgressBar> ChildConsoleProgresses { get; set; }
        private static ProgressBarOptions ChildOption { get; set; }
        private static ProgressBarOptions ProcessBarOption { get; set; }
        private static string DownloadListFile { get; } = "DownloadList.json";
        private static ConcurrentBag<long> AverageSpeed { get; } = new ConcurrentBag<long>();
        private static long LastTick { get; set; }

        private static async Task Main()
        {
            try
            {
                Initial();
                List<DownloadItem> downloadList = await GetDownloadItems();
                DownloadConfiguration downloadOpt = GetDownloadConfiguration();
                await DownloadList(downloadList, downloadOpt);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Debugger.Break();
            }

            Console.WriteLine("END");
            Console.Read();
        }

        private static void Initial()
        {
            ProcessBarOption = new ProgressBarOptions {
                ForegroundColor = ConsoleColor.Green,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };
            ChildOption = new ProgressBarOptions {
                ForegroundColor = ConsoleColor.Yellow,
                BackgroundColor = ConsoleColor.DarkGray,
                ProgressCharacter = '─'
            };
        }

        private static DownloadConfiguration GetDownloadConfiguration()
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1";
            return new DownloadConfiguration {
                ParallelDownload = true, // download parts of file as parallel or not
                BufferBlockSize = 10240, // usually, hosts support max to 8000 bytes
                ChunkCount = 8, // file parts to download
                MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
                OnTheFlyDownload = false, // caching in-memory or not?
                Timeout = 100, // timeout (millisecond) per stream block reader
                MaximumBytesPerSecond = 1 * 1024 * 1024, // speed limited to 1MB/s
                TempDirectory =
                    "C:\\temp", // Set the temp path for buffering chunk files, the default path is Path.GetTempPath().
                RequestConfiguration = {
                    // config and customize request headers
                    Accept = "*/*",
                    UserAgent = $"DownloaderSample/{version}",
                    ProtocolVersion = HttpVersion.Version11,
                    KeepAlive = false,
                    UseDefaultCredentials = false
                }
            };
        }

        private static async Task<List<DownloadItem>> GetDownloadItems()
        {
            List<DownloadItem> downloadList = File.Exists(DownloadListFile)
                ? JsonConvert.DeserializeObject<List<DownloadItem>>(await File.ReadAllTextAsync(DownloadListFile))
                : null;

            downloadList ??= new List<DownloadItem> {
                new DownloadItem {
                    FolderPath = Path.GetTempPath(), Url = "http://ipv4.download.thinkbroadband.com/100MB.zip"
                }
            };

            return downloadList;
        }

        private static async Task DownloadList(IEnumerable<DownloadItem> downloadList, DownloadConfiguration config)
        {
            foreach (DownloadItem downloadItem in downloadList)
            {
                // begin download from url
                DownloadService ds = await DownloadFile(downloadItem, config);

                await Task.Delay(1000);

                // clear download to order new of one
                ds.Clear();
            }
        }

        private static async Task<DownloadService> DownloadFile(DownloadItem downloadItem,
            DownloadConfiguration downloadOpt)
        {
            DownloadService ds = new DownloadService(downloadOpt);
            ds.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;
            ds.DownloadProgressChanged += OnDownloadProgressChanged;
            ds.DownloadFileCompleted += OnDownloadFileCompleted;
            ds.DownloadStarted += OnDownloadStarted;

            if (string.IsNullOrWhiteSpace(downloadItem.FileName))
            {
                await ds.DownloadFileAsync(downloadItem.Url, new DirectoryInfo(downloadItem.FolderPath))
                    .ConfigureAwait(false);
            }
            else
            {
                await ds.DownloadFileAsync(downloadItem.Url, downloadItem.FileName).ConfigureAwait(false);
            }

            return ds;
        }

        private static void OnDownloadStarted(object sender, DownloadStartedEventArgs e)
        {
            ConsoleProgress =
                new ProgressBar(10000, $"Downloading {Path.GetFileName(e.FileName)} ...", ProcessBarOption);
            ChildConsoleProgresses = new ConcurrentDictionary<string, ChildProgressBar>();
        }

        private static void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            ConsoleProgress?.Tick(10000);
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
            ChildProgressBar progress = ChildConsoleProgresses.GetOrAdd(e.ProgressId, id =>
                ConsoleProgress?.Spawn(10000, $"chunk {id}", ChildOption));
            progress.Tick((int)(e.ProgressPercentage * 100));
        }

        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double nonZeroSpeed = e.BytesPerSecondSpeed + 0.0001;
            int estimateTime = (int)((e.TotalBytesToReceive - e.BytesReceived) / nonZeroSpeed);
            bool isMinutes = estimateTime >= 60;
            string timeLeftUnit = "seconds";
            bool isElapsedTimeMoreThanOneSecond = Environment.TickCount64 - LastTick >= 1000;
            ConsoleProgress.Tick((int)(e.ProgressPercentage * 100));

            if (isMinutes)
            {
                timeLeftUnit = "minutes";
                estimateTime /= 60;
            }

            if (isElapsedTimeMoreThanOneSecond)
            {
                AverageSpeed.Add(e.BytesPerSecondSpeed);
                LastTick = Environment.TickCount64;
            }

            string avgSpeed = CalcMemoryMensurableUnit((long)AverageSpeed.Average());
            string speed = CalcMemoryMensurableUnit(e.BytesPerSecondSpeed);
            string bytesReceived = CalcMemoryMensurableUnit(e.BytesReceived);
            string totalBytesToReceive = CalcMemoryMensurableUnit(e.TotalBytesToReceive);
            string progressPercentage = $"{e.ProgressPercentage:F3}".Replace("/", ".");

            Console.Title = $"{progressPercentage}%  -  " +
                            $"{speed}/s (avg: {avgSpeed}/s)  -  " +
                            $"[{bytesReceived} of {totalBytesToReceive}], " +
                            $"{estimateTime} {timeLeftUnit} left";
        }

        private static string CalcMemoryMensurableUnit(double bytes)
        {
            double kb = bytes / 1024; // · 1024 Bytes = 1 Kilobyte 
            double mb = kb / 1024; // · 1024 Kilobytes = 1 Megabyte 
            double gb = mb / 1024; // · 1024 Megabytes = 1 Gigabyte 
            double tb = gb / 1024; // · 1024 Gigabytes = 1 Terabyte 

            string result =
                tb > 1 ? $"{tb:0.##}TB" :
                gb > 1 ? $"{gb:0.##}GB" :
                mb > 1 ? $"{mb:0.##}MB" :
                kb > 1 ? $"{kb:0.##}KB" :
                $"{bytes:0.##}B";

            result = result.Replace("/", ".");
            return result;
        }
    }
}