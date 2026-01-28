namespace Downloader.Test.Helper
{
    //[Xunit.Collection("Sequential")] // run tests in order
    public abstract class BaseTestClass
    {
        protected readonly ILoggerFactory LogFactory;
        protected readonly ITestOutputHelper Output;

        /// <summary>
        /// Get filename without creating it
        /// </summary>
        /// <returns></returns>
        protected static string GetTempNoFilename() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        protected BaseTestClass(ITestOutputHelper testOutputHelper)
        {
            Output = testOutputHelper;
            // Create an ILoggerFactory that logs to the ITestOutputHelper
            LogFactory = LoggerFactory.Create(builder => {
                builder.AddProvider(new TestOutputLoggerProvider(testOutputHelper));
            });
        }
    }
}