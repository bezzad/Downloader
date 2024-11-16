namespace Downloader.Test.Helper;

public class TestOutputLoggerProvider(ITestOutputHelper outputHelper) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(outputHelper, categoryName);

    public void Dispose() { }
}