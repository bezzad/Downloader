using Downloader.Extensions;

namespace Downloader.Test.UnitTests;

public class UrlHelperTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Theory]
    // Null / empty / whitespace passthrough
    [InlineData(null, null)]
    [InlineData("", "")]
    // No scheme: left alone (not an absolute URL we can safely decompose)
    [InlineData("relative/path", "relative/path")]
    [InlineData("/just/a/path[1].mkv", "/just/a/path[1].mkv")]
    // Plain URLs pass through unchanged
    [InlineData("http://example.com/", "http://example.com/")]
    [InlineData("http://example.com/path/to/file.mkv", "http://example.com/path/to/file.mkv")]
    [InlineData("https://example.com", "https://example.com")]
    // Bracketed release tags (the motivating bug, issue #223)
    [InlineData(
        "https://real-debrid.com/d/ABCDEF/[SubGroup] Series - 03 [1080p WEB-DL].mkv",
        "https://real-debrid.com/d/ABCDEF/%5BSubGroup%5D%20Series%20-%2003%20%5B1080p%20WEB-DL%5D.mkv")]
    // Curly braces
    [InlineData(
        "https://example.com/a/{token}/file.bin",
        "https://example.com/a/%7Btoken%7D/file.bin")]
    // Unencoded spaces in path
    [InlineData(
        "http://example.com/My Docs/report.pdf",
        "http://example.com/My%20Docs/report.pdf")]
    // Pipe, caret, backtick, double-quote, angle brackets — all illegal in path
    [InlineData(
        "http://example.com/a|b^c`d\"e<f>g.txt",
        "http://example.com/a%7Cb%5Ec%60d%22e%3Cf%3Eg.txt")]
    // Query string preserved verbatim (we only touch the path)
    [InlineData(
        "https://example.com/[x]/file.mkv?token=a[b]c&y=1",
        "https://example.com/%5Bx%5D/file.mkv?token=a[b]c&y=1")]
    // Fragment preserved verbatim
    [InlineData(
        "https://example.com/[x]/file.mkv#frag[ment]",
        "https://example.com/%5Bx%5D/file.mkv#frag[ment]")]
    // Query AND fragment together
    [InlineData(
        "https://example.com/[x]/file.mkv?q=1#frag",
        "https://example.com/%5Bx%5D/file.mkv?q=1#frag")]
    // IPv6 literal in host must NOT be touched
    [InlineData(
        "http://[::1]:8080/path[1].mkv",
        "http://[::1]:8080/path%5B1%5D.mkv")]
    [InlineData(
        "http://[2001:db8::1]/a b.txt",
        "http://[2001:db8::1]/a%20b.txt")]
    // Userinfo preserved
    [InlineData(
        "http://user:pass@example.com/dir[1]/file.mkv",
        "http://user:pass@example.com/dir%5B1%5D/file.mkv")]
    // pchar sub-delims and ':' '@' are preserved (legal in path)
    [InlineData(
        "http://example.com/a!$&'()*+,;=:@b.mkv",
        "http://example.com/a!$&'()*+,;=:@b.mkv")]
    // Non-http schemes work too (path is still path)
    [InlineData(
        "file:///home/user/[media]/show.mkv",
        "file:///home/user/%5Bmedia%5D/show.mkv")]
    // Already-encoded input is preserved (idempotency, single pass)
    [InlineData(
        "https://example.com/%5BSubGroup%5D/file.mkv",
        "https://example.com/%5BSubGroup%5D/file.mkv")]
    // Lowercase hex in existing triplets gets normalized to uppercase
    [InlineData(
        "https://example.com/%5bx%5d/file.mkv",
        "https://example.com/%5Bx%5D/file.mkv")]
    // Standalone '%' not followed by two hex digits must be encoded to %25
    [InlineData(
        "http://example.com/50%off.mkv",
        "http://example.com/50%25off.mkv")]
    [InlineData(
        "http://example.com/%",
        "http://example.com/%25")]
    [InlineData(
        "http://example.com/%Z1/file",
        "http://example.com/%25Z1/file")]
    // Authority-only URL with no path
    [InlineData("http://example.com?q=1", "http://example.com?q=1")]
    [InlineData("http://example.com#frag", "http://example.com#frag")]
    public void EnsurePathEncodedTest(string input, string expected)
    {
        string actual = UrlHelper.EnsurePathEncoded(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnsurePathEncodedIsIdempotent()
    {
        // Running twice must equal running once for any input we'd see in practice.
        string[] inputs =
        [
            "https://real-debrid.com/d/ABCDEF/[SubGroup] Series - 03 [1080p WEB-DL].mkv",
            "https://example.com/a/{token}/file.bin",
            "http://example.com/My Docs/report.pdf",
            "https://example.com/%5BSubGroup%5D/file.mkv",
            "http://[::1]:8080/path[1].mkv?q=[x]#frag[1]",
            "http://example.com/50%off.mkv",
        ];

        foreach (string input in inputs)
        {
            string once = UrlHelper.EnsurePathEncoded(input);
            string twice = UrlHelper.EnsurePathEncoded(once);
            Assert.Equal(once, twice);
        }
    }

    [Fact]
    public void EnsurePathEncodedProducesUriParsableOutput()
    {
        // The whole point is that after normalization, Uri.TryCreate succeeds
        // cleanly on every platform — including Linux.
        string input = "https://real-debrid.com/d/ABCDEF/[SubGroup] Series - 03 [1080p WEB-DL].mkv";
        string normalized = UrlHelper.EnsurePathEncoded(input);

        Assert.True(Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri));
        Assert.Equal("real-debrid.com", uri.Host);
        Assert.DoesNotContain("[", uri.AbsolutePath);
        Assert.DoesNotContain("]", uri.AbsolutePath);
        Assert.DoesNotContain(" ", uri.AbsolutePath);
    }

    [Fact]
    public void EnsurePathEncodedHandlesUnicodeCorrectly()
    {
        // Non-ASCII characters in the path should be UTF-8 percent-encoded.
        string input = "https://example.com/مستند.pdf";
        string normalized = UrlHelper.EnsurePathEncoded(input);

        Assert.True(Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri));
        // Round-trip: decoding the path must give back the original filename.
        string filename = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
        Assert.Equal("مستند.pdf", filename);
    }

    [Fact]
    public void RequestConstructorAcceptsBracketedUrl()
    {
        // Integration check: Request no longer falls back to the
        // http://localhost base when given a URL with illegal path chars.
        string input = "https://real-debrid.com/d/ABCDEF/[SubGroup] Series - 03 [1080p WEB-DL].mkv";

        Request request = new(input);

        Assert.Equal("real-debrid.com", request.Address.Host);
        Assert.Equal("https", request.Address.Scheme);
        Assert.Contains("%5B", request.Address.AbsoluteUri);
        Assert.Contains("%5D", request.Address.AbsoluteUri);
    }
}
