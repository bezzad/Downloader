namespace Downloader.Test.UnitTests
{
    //[Xunit.Collection("Sequential")] // run tests in order
    public abstract class BaseTestClass
    {
        protected readonly ILoggerFactory LoggerFactory;
        protected readonly ITestOutputHelper TestOutputHelper;
        
        protected BaseTestClass(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
            // Create an ILoggerFactory that logs to the ITestOutputHelper
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new TestOutputLoggerProvider(testOutputHelper));
            });
        }
    }
}