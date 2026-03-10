namespace Downloader.Test.Helper;

public class TestOutputLogger(ITestOutputHelper outputHelper, string categoryName) : ILogger
{
    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        try
        {
            outputHelper.WriteLine($"{categoryName} [{logLevel}]: {formatter(state, exception)}");
        }
        catch
        {
            // ignore any exceptions that may occur during logging to prevent test failures due to logging issues
        }
    }
}