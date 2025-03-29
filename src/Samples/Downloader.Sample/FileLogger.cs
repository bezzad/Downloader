using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Sample;

[ExcludeFromCodeCoverage]
public class FileLogger : ILogger, IDisposable
{
    private volatile bool _disposed;
    private readonly SemaphoreSlim _semaphore;
    protected readonly ConcurrentQueue<string> LogQueue;
    protected string LogPath;
    protected StreamWriter LogStream;

    public static FileLogger Factory(string logPath, [CallerMemberName] string logName = default)
    {
        string filename = logName + "_" + DateTime.Now.ToString("yyyyMMdd.HHmmss") + ".log";
        return new FileLogger(Path.Combine(logPath, filename));
    }

    public FileLogger(string logPath)
    {
        _semaphore = new SemaphoreSlim(0);
        LogQueue = new ConcurrentQueue<string>();
        LogPath = logPath;
        LogStream = new StreamWriter(CreateFile(logPath));

        Task<Task> task = Task.Factory.StartNew(
                function: Writer,
                cancellationToken: default,
                creationOptions: TaskCreationOptions.LongRunning,
                scheduler: TaskScheduler.Default);

        task.Unwrap();
    }

    public void LogDebug(string message)
    {
        Log(LogLevel.Information, message);
    }

    public void LogInfo(string message)
    {
        Log(LogLevel.Information, message);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    public void LogError(string message)
    {
        Log(LogLevel.Error, message);
    }

    public void LogError(Exception exception, string message)
    {
        Log(LogLevel.Error, message, exception);
    }

    public void LogCritical(string message)
    {
        Log(LogLevel.Critical, message);
    }

    public void LogCritical(Exception exception, string message)
    {
        Log(LogLevel.Critical, message, exception);
    }

    protected void Log(LogLevel logLevel, string message, Exception exception = null)
    {
        if (!_disposed)
        {
            LogQueue.Enqueue(Formatter(logLevel, message, exception));
            _semaphore.Release();
        }
    }

    public virtual string Formatter(LogLevel logLevel, string message, Exception exception)
    {
        string log = $"{DateTime.Now:s} | {logLevel} | {message}";
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
            if (LogQueue.TryDequeue(out string log))
            {
                await LogStream.WriteLineAsync(log).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        LogStream?.Dispose();
        LogStream = null;
    }

    public async Task FlushAsync()
    {
        while (!_disposed && _semaphore.CurrentCount > 0)
        {
            await Task.Delay(100);
        }

        await (LogStream?.FlushAsync() ?? Task.FromResult(0)).ConfigureAwait(false);
    }

    private static Stream CreateFile(string filename)
    {
        string directory = Path.GetDirectoryName(filename);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Stream.Null;
        }

        if (Directory.Exists(directory) == false)
        {
            Directory.CreateDirectory(directory);
        }

        return new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string logMessage = formatter(state, exception);
        string logEntry = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} [{logLevel}] {logMessage}{Environment.NewLine}";

        Log(logLevel, logEntry, exception);
    }
}
