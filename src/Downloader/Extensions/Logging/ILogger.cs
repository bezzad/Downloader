using System;
using System.Threading.Tasks;

namespace Downloader.Extensions.Logging;

public interface ILogger 
{
    void Debug(string message);
    void Info(string message);
    void Warning(string message);
    void Warning(string message, Exception exception);
    void Error(string message);
    void Error(string message, Exception exception);
    void Fatal(string message);
    void Fatal(string message, Exception exception);
    string Formatter(string logType, string message, Exception exception);
    Task FlushAsync();
}
