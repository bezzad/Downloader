namespace Downloader.Test.UnitTests;

public class RequestConfigurationTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact]
    public void DefaultUserAgentIsValid()
    {
        // arrange
        RequestConfiguration requestConfiguration = new();

        // act
        string userAgent = requestConfiguration.UserAgent;

        // assert
        Assert.False(string.IsNullOrWhiteSpace(userAgent));
        Assert.StartsWith("Downloader/", userAgent);
        Assert.False(userAgent.EndsWith("/", StringComparison.Ordinal));
        Assert.NotEqual("Downloader/0.0.0", userAgent);
        Assert.NotEqual("Downloader/0.0.0.0", userAgent);
    }
}
