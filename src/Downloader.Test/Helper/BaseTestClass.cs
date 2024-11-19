namespace Downloader.Test.Helper
{
    //[Xunit.Collection("Sequential")] // run tests in order
    public abstract class BaseTestClass
    {
        protected readonly ILoggerFactory LogFactory;
        protected readonly ITestOutputHelper Output;
        
        protected BaseTestClass(ITestOutputHelper testOutputHelper)
        {
            Output = testOutputHelper;
            // Create an ILoggerFactory that logs to the ITestOutputHelper
            LogFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new TestOutputLoggerProvider(testOutputHelper));
            });
        }
    }
}