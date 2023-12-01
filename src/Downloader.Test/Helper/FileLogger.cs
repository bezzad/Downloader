using Downloader.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.Helper;

internal class FileLogger : ILogger, IDisposable
{
    private volatile bool _disposed = false;
    private SemaphoreSlim _semaphore = new SemaphoreSlim(0);
    protected readonly ConcurrentQueue<string> LogQueue;
    protected string LogPath;
    protected StreamWriter LogStream;

    public FileLogger(string logPath)
    {
        LogQueue = new ConcurrentQueue<string>();
        LogPath = logPath;
        LogStream = new StreamWriter(FileHelper.CreateFile(logPath));

        Task<Task> task = Task.Factory.StartNew(
                function: Writer,
                cancellationToken: default,
                creationOptions: TaskCreationOptions.LongRunning,
                scheduler: TaskScheduler.Default);

        task.Unwrap();
    }

    public void Debug(string message)
    {
        Log(nameof(Debug), message);
    }

    public void Info(string message)
    {
        Log(nameof(Info), message);
    }

    public void Warning(string message)
    {
        Log(nameof(Warning), message);
    }

    public void Warning(string message, Exception exception)
    {
        Log(nameof(Warning), message, exception);
    }

    public void Error(string message)
    {
        Log(nameof(Error), message);
    }

    public void Error(string message, Exception exception)
    {
        Log(nameof(Error), message, exception);
    }

    public void Fatal(string message)
    {
        Log(nameof(Fatal), message);
    }

    public void Fatal(string message, Exception exception)
    {
        Log(nameof(Fatal), message, exception);
    }

    protected void Log(string logType, string message, Exception exception = null)
    {
        if (!_disposed)
        {
            LogQueue.Enqueue(Formatter(logType, message, exception));
            _semaphore.Release();
        }
    }

    public virtual string Formatter(string logType, string message, Exception exception)
    {
        var log = $"{DateTime.Now:s} | {logType} | {message}";
        if (exception is not null)
        {
            log += " | " + exception.Message + ": " + exception.StackTrace;
        }

        return log;
    }

    private async Task Writer()
    {
        while (!_disposed)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            if (LogQueue.TryDequeue(out var log))
            {
                await LogStream.WriteLineAsync(log).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        LogQueue.Clear();
        LogStream?.Dispose();
        LogStream = null;
    }

    public async Task FlushAsync()
    {
        while (!_disposed && _semaphore.CurrentCount > 0)
        {
            await Task.Delay(100);
        }

        await (LogStream?.FlushAsync() ?? Task.CompletedTask).ConfigureAwait(false);
    }
}
