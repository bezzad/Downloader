using Downloader.Extensions.Logging;
using Newtonsoft.Json;
using ShellProgressBar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileLogger = Downloader.Extensions.Logging.FileLogger;

namespace Downloader.Sample;

public partial class Program
{
    private const string DownloadListFile = "download.json";
    private static List<DownloadItem> DownloadList;
    private static ProgressBar ConsoleProgress;
    private static ConcurrentDictionary<string, ChildProgressBar> ChildConsoleProgresses;
    private static ProgressBarOptions ChildOption;
    private static ProgressBarOptions ProcessBarOption;
    private static IDownloadService CurrentDownloadService;
    private static DownloadConfiguration CurrentDownloadConfiguration;
    private static CancellationTokenSource CancelAllTokenSource;
    private static ILogger Logger;

    private static async Task Main()
    {
        // Recover the standard output stream so that a
        // completion message can be displayed.
        var standardOutput = new StreamWriter(Console.OpenStandardOutput());
        standardOutput.AutoFlush = true;
        Console.SetOut(standardOutput);

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
            Console.Clear();
            await Console.Error.WriteLineAsync(e.Message);
            Debugger.Break();
        }
        finally
        {
            await DummyHttpServer.HttpServer.Stop();
        }

        await Console.Out.WriteLineAsync("END");
    }

    private static void Initial()
    {
        CancelAllTokenSource = new CancellationTokenSource();
        ChildConsoleProgresses = new ConcurrentDictionary<string, ChildProgressBar>();
        DownloadList = GetDownloadItems();

        ProcessBarOption = new ProgressBarOptions {
            ForegroundColor = ConsoleColor.Green,
            ForegroundColorDone = ConsoleColor.DarkGreen,
            BackgroundColor = ConsoleColor.DarkGray,
            BackgroundCharacter = '\u2593',
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
        Console.CancelKeyPress += (_, _) => CancelAll();

        while (true)
        {
            while (Console.KeyAvailable)
            {
                ConsoleKeyInfo cki = Console.ReadKey(true);
                switch (cki.Key)
                {
                    case ConsoleKey.C:
                        if (cki.Modifiers == ConsoleModifiers.Control)
                        {
                            CancelAll();
                            return;
                        }
                        break;
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
                        if (CurrentDownloadConfiguration != null)
                            CurrentDownloadConfiguration.MaximumBytesPerSecond *= 2;
                        break;
                    case ConsoleKey.DownArrow:
                        if (CurrentDownloadConfiguration != null)
                            CurrentDownloadConfiguration.MaximumBytesPerSecond /= 2;
                        break;
                }
            }
        }
    }

    private static void CancelAll()
    {
        CancelAllTokenSource.Cancel();
        CurrentDownloadService?.CancelAsync();
    }

    private static List<DownloadItem> GetDownloadItems()
    {
        List<DownloadItem> downloadList = File.Exists(DownloadListFile)
            ? JsonConvert.DeserializeObject<List<DownloadItem>>(File.ReadAllText(DownloadListFile))
            : new List<DownloadItem>();

        return downloadList;
    }

    private static async Task DownloadAll(IEnumerable<DownloadItem> downloadList, CancellationToken cancelToken)
    {
        foreach (DownloadItem downloadItem in downloadList)
        {
            if (cancelToken.IsCancellationRequested)
                return;

            // begin download from url
            await DownloadFile(downloadItem).ConfigureAwait(false);

            await Task.Yield();
        }
    }

    private static async Task<IDownloadService> DownloadFile(DownloadItem downloadItem)
    {
        CurrentDownloadConfiguration = GetDownloadConfiguration();
        CurrentDownloadService = CreateDownloadService(CurrentDownloadConfiguration);
        if (string.IsNullOrWhiteSpace(downloadItem.FileName))
        {
            Logger = FileLogger.Factory(downloadItem.FolderPath);
            CurrentDownloadService.AddLogger(Logger);
            await CurrentDownloadService
                .DownloadFileTaskAsync(downloadItem.Url, new DirectoryInfo(downloadItem.FolderPath))
                .ConfigureAwait(false);
        }
        else
        {
            Logger = FileLogger.Factory(downloadItem.FolderPath, Path.GetFileName(downloadItem.FileName));
            CurrentDownloadService.AddLogger(Logger);
            await CurrentDownloadService.DownloadFileTaskAsync(downloadItem.Url, downloadItem.FileName)
                .ConfigureAwait(false);
        }

        if (downloadItem.ValidateData)
        {
            var isValid =
                await ValidateDataAsync(CurrentDownloadService.Package.FileName,
                    CurrentDownloadService.Package.TotalFileSize).ConfigureAwait(false);
            if (!isValid)
            {
                var message = "Downloaded data is invalid: " + CurrentDownloadService.Package.FileName;
                Logger?.LogCritical(message);
                throw new InvalidDataException(message);
            }
        }

        return CurrentDownloadService;
    }

    private static async Task<bool> ValidateDataAsync(string filename, long size)
    {
        await using var stream = File.OpenRead(filename);
        for (var i = 0L; i < size; i++)
        {
            var next = stream.ReadByte();
            if (next != i % 256)
            {
                Logger?.LogWarning(
                    $"Sample.Program.ValidateDataAsync():  Data at index [{i}] of `{filename}` is `{next}`, expectation is `{i % 256}`");
                return false;
            }
        }

        return true;
    }

    private static async Task WriteKeyboardGuidLines()
    {
        Console.Clear();
        Console.Beep();
        Console.CursorVisible = false;
        await Console.Out.WriteLineAsync("Press Esc to Stop current file download");
        await Console.Out.WriteLineAsync("Press P to Pause and R to Resume downloading");
        await Console.Out.WriteLineAsync("Press Up Arrow to Increase download speed 2X");
        await Console.Out.WriteLineAsync("Press Down Arrow to Decrease download speed 2X \n");
        await Console.Out.FlushAsync();
        await Task.Yield();
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

    private static async void OnDownloadStarted(object sender, DownloadStartedEventArgs e)
    {
        await WriteKeyboardGuidLines();
        ConsoleProgress = new ProgressBar(10000, $"Downloading {Path.GetFileName(e.FileName)}   ", ProcessBarOption);
    }

    private static void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        ConsoleProgress?.Tick(10000);

        if (e.Cancelled)
        {
            ConsoleProgress.Message += " CANCELED";
        }
        else if (e.Error != null)
        {
            Console.Error.WriteLine(e.Error);
            Debugger.Break();
        }
        else
        {
            ConsoleProgress.Message += " DONE";
        }

        foreach (var child in ChildConsoleProgresses.Values)
            child.Dispose();

        ChildConsoleProgresses.Clear();
        ConsoleProgress?.Dispose();
    }

    private static void OnChunkDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        ChildProgressBar progress = ChildConsoleProgresses.GetOrAdd(e.ProgressId,
            id => ConsoleProgress?.Spawn(10000, $"chunk {id}", ChildOption));
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