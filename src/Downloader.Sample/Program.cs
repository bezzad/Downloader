using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
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
        private static DownloadService _currentDownloadService;
        private static DownloadConfiguration _currentDownloadConfiguration;

        private static async Task Main()
        {
            try
            {
                DummyHttpServer.HttpServer.Run(3333);
                Console.Clear();
                new Thread(AddKeyboardHandler) { IsBackground = true }.Start();
                Initial();
                List<DownloadItem> downloadList = GetDownloadItems();
                await DownloadAll(downloadList).ConfigureAwait(false);
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

        private static void AddKeyboardHandler()
        {
            Console.WriteLine("\nPress Esc to Stop current file download");
            Console.WriteLine("\nPress Up Arrow to Increase download speed 2X");
            Console.WriteLine("\nPress Down Arrow to Decrease download speed 2X");
            Console.WriteLine();

            while (true) // continue download other files of the list
            {
                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(50);
                }

                if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                    _currentDownloadService?.CancelAsync();

                if (Console.ReadKey(true).Key == ConsoleKey.UpArrow)
                    _currentDownloadConfiguration.MaximumBytesPerSecond *= 2;

                if (Console.ReadKey(true).Key == ConsoleKey.DownArrow)
                    _currentDownloadConfiguration.MaximumBytesPerSecond /= 2;
            }
        }

        private static DownloadConfiguration GetDownloadConfiguration()
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1";
            var cookies = new CookieContainer();
            cookies.Add(new Cookie("download-type", "test") { Domain = "domain.com" });

            return new DownloadConfiguration {
                BufferBlockSize = 10240, // usually, hosts support max to 8000 bytes, default values is 8000
                ChunkCount = 8, // file parts to download, default value is 1
                MaximumBytesPerSecond = 1024 * 1024 * 2, // download speed limited to 2MB/s, default values is zero or unlimited
                MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail
                OnTheFlyDownload = false, // caching in-memory or not? default values is true
                ParallelDownload = true, // download parts of file as parallel or not. Default value is false
                TempDirectory = "C:\\temp", // Set the temp path for buffering chunk files, the default path is Path.GetTempPath()
                Timeout = 1000, // timeout (millisecond) per stream block reader, default values is 1000
                RangeDownload = false,
                RangeLow = 0,
                RangeHigh = 0,
                RequestConfiguration = {
                    // config and customize request headers
                    Accept = "*/*",
                    CookieContainer = cookies,
                    Headers = new WebHeaderCollection(), // { Add your custom headers }
                    KeepAlive = true, // default value is false
                    ProtocolVersion = HttpVersion.Version11, // Default value is HTTP 1.1
                    UseDefaultCredentials = false,
                    UserAgent = $"DownloaderSample/{version}"
                    //Proxy = new WebProxy() {
                    //    Address = new Uri("http://YourProxyServer/proxy.pac"),
                    //    UseDefaultCredentials = false,
                    //    Credentials = System.Net.CredentialCache.DefaultNetworkCredentials,
                    //    BypassProxyOnLocal = true
                    //}
                }
            };
        }
        private static List<DownloadItem> GetDownloadItems()
        {
            List<DownloadItem> downloadList = File.Exists(DownloadListFile)
                ? JsonConvert.DeserializeObject<List<DownloadItem>>(File.ReadAllText(DownloadListFile))
                : null;

            downloadList ??= new List<DownloadItem> {
                new DownloadItem {
                    FolderPath = Path.GetTempPath(), Url = "http://ipv4.download.thinkbroadband.com/100MB.zip"
                }
            };

            return downloadList;
        }
        private static async Task DownloadAll(IEnumerable<DownloadItem> downloadList)
        {
            foreach (DownloadItem downloadItem in downloadList)
            {
                // begin download from url
                DownloadService ds = await DownloadFile(downloadItem).ConfigureAwait(false);

                // clear download to order new of one
                ds.Clear();
            }
        }
        private static async Task<DownloadService> DownloadFile(DownloadItem downloadItem)
        {
            _currentDownloadConfiguration = GetDownloadConfiguration();
            _currentDownloadService = new DownloadService(_currentDownloadConfiguration);
            _currentDownloadService.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;
            _currentDownloadService.DownloadProgressChanged += OnDownloadProgressChanged;
            _currentDownloadService.DownloadFileCompleted += OnDownloadFileCompleted;
            _currentDownloadService.DownloadStarted += OnDownloadStarted;

            if (string.IsNullOrWhiteSpace(downloadItem.FileName))
            {
                await _currentDownloadService.DownloadFileTaskAsync(downloadItem.Url, new DirectoryInfo(downloadItem.FolderPath)).ConfigureAwait(false);
            }
            else
            {
                await _currentDownloadService.DownloadFileTaskAsync(downloadItem.Url, downloadItem.FileName).ConfigureAwait(false);
            }

            return _currentDownloadService;
        }

        private static void OnDownloadStarted(object sender, DownloadStartedEventArgs e)
        {
            ConsoleProgress = new ProgressBar(10000,
                $"Downloading {Path.GetFileName(e.FileName)} ...", ProcessBarOption);
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
            ConsoleProgress.Tick((int)(e.ProgressPercentage * 100));
            e.UpdateTitleInfo();
        }
    }
}