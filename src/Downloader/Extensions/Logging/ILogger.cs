using System;
using System.Threading.Tasks;

namespace Downloader.Extensions.Logging;

public interface ILogger 
{
    void LogDebug(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(Exception exception, string message);
    void LogCritical(string message);
    void LogCritical(Exception exception, string message);
    string Formatter(string logType, string message, Exception exception);
    Task FlushAsync();
}
