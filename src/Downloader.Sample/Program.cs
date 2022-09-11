using Newtonsoft.Json;
using ShellProgressBar;
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

namespace Downloader.Sample
{
    internal static class Program
    {
        private const string DownloadListFile = "DownloadList.json";
        private static List<DownloadItem> DownloadList;
        private static ProgressBar ConsoleProgress;
        private static ConcurrentDictionary<string, ChildProgressBar> ChildConsoleProgresses;
        private static ProgressBarOptions ChildOption;
        private static ProgressBarOptions ProcessBarOption;
        private static DownloadService CurrentDownloadService;
        private static DownloadConfiguration CurrentDownloadConfiguration;
        private static CancellationTokenSource CancelAllTokenSource;

        private static async Task Main()
        {
            try
            {
                DummyHttpServer.HttpServer.Run(3333);
                await Task.Delay(1000);
                Console.Clear();
                Initial();
                new Task(KeyboardHandler).Start();
                await DownloadAll(DownloadList, CancelAllTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Debugger.Break();
            }

            Console.WriteLine("\n\n END \n\n");
            Console.Read();
        }
        private static void Initial()
        {
            CancelAllTokenSource = new CancellationTokenSource();
            DownloadList = GetDownloadItems();

            ProcessBarOption = new ProgressBarOptions {
                ForegroundColor = ConsoleColor.Green,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593',
                EnableTaskBarProgress = true,
                ProgressBarOnBottom = false,
                ProgressCharacter = '#'
            };
            ChildOption = new ProgressBarOptions {
                ForegroundColor = ConsoleColor.Yellow,
                BackgroundColor = ConsoleColor.DarkGray,
                ProgressCharacter = '-',
                ProgressBarOnBottom = true
            };
        }
        private static void KeyboardHandler()
        {
            ConsoleKeyInfo cki;
            Console.CancelKeyPress += CancelAll;
            Console.WriteLine("\nPress Esc to Stop current file download");
            Console.WriteLine("\nPress P to Pause and R to Resume downloading");
            Console.WriteLine("\nPress Up Arrow to Increase download speed 2X");
            Console.WriteLine("\nPress Down Arrow to Decrease download speed 2X");
            Console.WriteLine();

            while (true)
            {
                cki = Console.ReadKey(true);
                if (CurrentDownloadConfiguration != null)
                {
                    switch (cki.Key)
                    {
                        case ConsoleKey.P:
                            CurrentDownloadService?.Pause();
                            Console.Beep();
                            break;
                        case ConsoleKey.R:
                            CurrentDownloadService?.Resume();
                            break;
                        case ConsoleKey.Escape:
                            CurrentDownloadService?.CancelAsync();
                            break;

                        case ConsoleKey.UpArrow:
                            CurrentDownloadConfiguration.MaximumBytesPerSecond *= 2;
                            break;

                        case ConsoleKey.DownArrow:
                            CurrentDownloadConfiguration.MaximumBytesPerSecond /= 2;
                            break;
                    }
                }
            }
        }
        private static void CancelAll(object sender, ConsoleCancelEventArgs e)
        {
            CancelAllTokenSource.Cancel();
            CurrentDownloadService?.CancelAsync();
        }

        private static DownloadConfiguration GetDownloadConfiguration()
        {
            var cookies = new CookieContainer();
            cookies.Add(new Cookie("download-type", "test") { Domain = "domain.com" });

            return new DownloadConfiguration {
                BufferBlockSize = 10240,    // usually, hosts support max to 8000 bytes, default values is 8000
                ChunkCount = 8,             // file parts to download, default value is 1
                MaximumBytesPerSecond = 1024 * 1024 * 8, // download speed limited to 8MB/s, default values is zero or unlimited
                MaxTryAgainOnFailover = 5,  // the maximum number of times to fail
                OnTheFlyDownload = true,   // caching in-memory or not? default values is true
                ParallelDownload = true,    // download parts of file as parallel or not. Default value is false
                ParallelCount = 4,          // number of parallel downloads. The default value is the same as the chunk count
                TempDirectory = @"C:\temp", // Set the temp path for buffering chunk files, the default path is Path.GetTempPath()
                Timeout = 1000,             // timeout (millisecond) per stream block reader, default values is 1000
                RangeDownload = false,      // set true if you want to download just a specific range of bytes of a large file
                RangeLow = 0,               // floor offset of download range of a large file
                RangeHigh = 0,              // ceiling offset of download range of a large file
                ClearPackageOnCompletionWithFailure = false, // clear package temp files when download completed with failure, default value is true
                RequestConfiguration =
                {
                    // config and customize request headers
                    Accept = "*/*",
                    CookieContainer = cookies,
                    Headers = new WebHeaderCollection(),     // { your custom headers }
                    KeepAlive = true,                        // default value is false
                    ProtocolVersion = HttpVersion.Version11, // default value is HTTP 1.1
                    UseDefaultCredentials = false,
                    // your custom user agent or your_app_name/app_version.
                    UserAgent = $"DownloaderSample/{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}"
                    // Proxy = new WebProxy() {
                    //    Address = new Uri("http://YourProxyServer/proxy.pac"),
                    //    UseDefaultCredentials = false,
                    //    Credentials = System.Net.CredentialCache.DefaultNetworkCredentials,
                    //    BypassProxyOnLocal = true
                    // }
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
        private static async Task DownloadAll(IEnumerable<DownloadItem> downloadList, CancellationToken cancelToken)
        {
            foreach (DownloadItem downloadItem in downloadList)
            {
                if (cancelToken.IsCancellationRequested)
                    return;

                // begin download from url
                DownloadService ds = await DownloadFile(downloadItem).ConfigureAwait(false);
            }
        }
        private static async Task<DownloadService> DownloadFile(DownloadItem downloadItem)
        {
            CurrentDownloadConfiguration = GetDownloadConfiguration();
            CurrentDownloadService = CreateDownloadService(CurrentDownloadConfiguration);

            if (string.IsNullOrWhiteSpace(downloadItem.FileName))
            {
                await CurrentDownloadService.DownloadFileTaskAsync(downloadItem.Url, new DirectoryInfo(downloadItem.FolderPath)).ConfigureAwait(false);
            }
            else
            {
                await CurrentDownloadService.DownloadFileTaskAsync(downloadItem.Url, downloadItem.FileName).ConfigureAwait(false);
            }

            return CurrentDownloadService;
        }
        private static DownloadService CreateDownloadService(DownloadConfiguration config)
        {
            var downloadService = new DownloadService(config);

            // Provide `FileName` and `TotalBytesToReceive` at the start of each downloads
            downloadService.DownloadStarted += OnDownloadStarted;

            // Provide any information about chunker downloads, 
            // like progress percentage per chunk, speed, 
            // total received bytes and received bytes array to live streaming.
            downloadService.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;

            // Provide any information about download progress, 
            // like progress percentage of sum of chunks, total speed, 
            // average speed, total received bytes and received bytes array 
            // to live streaming.
            downloadService.DownloadProgressChanged += OnDownloadProgressChanged;

            // Download completed event that can include occurred errors or 
            // cancelled or download completed successfully.
            downloadService.DownloadFileCompleted += OnDownloadFileCompleted;

            return downloadService;
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
            var activeChunksCount = e.ActiveChunks; // Running chunks count
        }
        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            ConsoleProgress.Tick((int)(e.ProgressPercentage * 100));
            if (sender is DownloadService ds)
                e.UpdateTitleInfo(ds.IsPaused);
        }
    }
}