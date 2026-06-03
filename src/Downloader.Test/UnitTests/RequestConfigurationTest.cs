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

    [Fact]
    public void ObsoletePropertiesAreSettableAndGettable()
    {
        // arrange
        RequestConfiguration config = new();

#pragma warning disable CS0618 // obsolete members are retained for API compatibility
        // act
        config.CachePolicy = new System.Net.Cache.RequestCachePolicy();
        config.ConnectionGroupName = "group-1";
        config.SendChunked = true;
        config.UseDefaultCredentials = true;

        // assert
        Assert.NotNull(config.CachePolicy);
        Assert.Equal("group-1", config.ConnectionGroupName);
        Assert.True(config.SendChunked);
        Assert.True(config.UseDefaultCredentials);
#pragma warning restore CS0618
    }

    private static string NormalizeVersion(string versionText)
    {
        MethodInfo method = typeof(RequestConfiguration)
            .GetMethod("NormalizeVersion", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string)method.Invoke(null, [versionText]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0.0.0.0")]        // ZeroVersion → treated as no version
    [InlineData("not-a-version")] // unparsable
    public void NormalizeVersionReturnsNullForInvalidInput(string input)
    {
        // act
        string result = NormalizeVersion(input);

        // assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3.4", "1.2.3")]            // 4-part normalized to 3 parts
    [InlineData("1.2.3+build.99", "1.2.3")]    // strips build metadata after '+'
    [InlineData("5.8.0-beta.1", "5.8.0")]      // strips pre-release after '-'
    [InlineData("2.5", "2.5")]                  // 2-part version kept as-is
    public void NormalizeVersionStripsMetadataAndNormalizesParts(string input, string expected)
    {
        // act
        string result = NormalizeVersion(input);

        // assert
        Assert.Equal(expected, result);
    }
}
