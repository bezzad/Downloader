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
    [InlineData("https://real-debrid.com/d/ABCDEF/[SubGroup] Series - 03 [1080p WEB-DL].mkv",
        "https://real-debrid.com/d/ABCDEF/%5BSubGroup%5D%20Series%20-%2003%20%5B1080p%20WEB-DL%5D.mkv")]
    // Curly braces
    [InlineData("https://example.com/a/{token}/file.bin", "https://example.com/a/%7Btoken%7D/file.bin")]
    // Unencoded spaces in path
    [InlineData("http://example.com/My Docs/report.pdf", "http://example.com/My%20Docs/report.pdf")]
    // Pipe, caret, backtick, double-quote, angle brackets — all illegal in path
    [InlineData("http://example.com/a|b^c`d\"e<f>g.txt", "http://example.com/a%7Cb%5Ec%60d%22e%3Cf%3Eg.txt")]
    // Query string preserved verbatim (we only touch the path)
    [InlineData("https://example.com/[x]/file.mkv?token=a[b]c&y=1", "https://example.com/%5Bx%5D/file.mkv?token=a[b]c&y=1")]
    // Fragment preserved verbatim
    [InlineData("https://example.com/[x]/file.mkv#frag[ment]", "https://example.com/%5Bx%5D/file.mkv#frag[ment]")]
    // Query AND fragment together
    [InlineData("https://example.com/[x]/file.mkv?q=1#frag", "https://example.com/%5Bx%5D/file.mkv?q=1#frag")]
    // IPv6 literal in host must NOT be touched
    [InlineData("http://[::1]:8080/path[1].mkv", "http://[::1]:8080/path%5B1%5D.mkv")]
    [InlineData("http://[2001:db8::1]/a b.txt", "http://[2001:db8::1]/a%20b.txt")]
    // Userinfo preserved
    [InlineData("http://user:pass@example.com/dir[1]/file.mkv", "http://user:pass@example.com/dir%5B1%5D/file.mkv")]
    // pchar sub-delims and ':' '@' are preserved (legal in path)
    [InlineData("http://example.com/a!$&'()*+,;=:@b.mkv", "http://example.com/a!$&'()*+,;=:@b.mkv")]
    // Non-http schemes work too (path is still path)
    [InlineData("file:///home/user/[media]/show.mkv", "file:///home/user/%5Bmedia%5D/show.mkv")]
    // Already-encoded input is preserved (idempotency, single pass)
    [InlineData("https://example.com/%5BSubGroup%5D/file.mkv", "https://example.com/%5BSubGroup%5D/file.mkv")]
    // Lowercase hex in existing triplets gets normalized to uppercase
    [InlineData("https://example.com/%5bx%5d/file.mkv", "https://example.com/%5Bx%5D/file.mkv")]
    // Standalone '%' not followed by two hex digits must be encoded to %25
    [InlineData("http://example.com/50%off.mkv", "http://example.com/50%25off.mkv")]
    [InlineData("http://example.com/%", "http://example.com/%25")]
    [InlineData("http://example.com/%Z1/file", "http://example.com/%25Z1/file")]
    // Authority-only URL with no path
    [InlineData("http://example.com?q=1", "http://example.com?q=1")]
    [InlineData("http://example.com#frag", "http://example.com#frag")]
    // Empty path segments (double slash) preserved as-is
    [InlineData("http://example.com//double//slash/", "http://example.com//double//slash/")]
    // Very short scheme-only input — no authority, no path
    [InlineData("http://", "http://")]
    public void EnsurePathEncodedTest(string input, string expected)
    {
        string actual = UrlHelper.EnsurePathEncoded(input);
        Assert.Equal(expected, actual);
    }

    // -------- Security tests -----------------------------------------------
    //
    // These verify that characters with HTTP-level semantic impact (CR/LF for
    // request-line smuggling, NUL for string-termination bugs, tab for header
    // parsing, other C0 controls) are always percent-encoded. Encoding, not
    // stripping, preserves byte identity so the server can apply its own
    // rejection policies on the original input.

    [Theory]
    // NOTE: use \uXXXX-style escapes (exact 4 hex digits) not \x — C#'s
    // \x is a greedy 1-to-4-digit escape, so "\x01b" parses as U+01B (ESC),
    // not U+0001 followed by literal 'b'.
    [InlineData("http://example.com/a\rb.txt", "http://example.com/a%0Db.txt")]              // CR
    [InlineData("http://example.com/a\nb.txt", "http://example.com/a%0Ab.txt")]              // LF
    [InlineData("http://example.com/a\r\nb.txt", "http://example.com/a%0D%0Ab.txt")]         // CRLF
    [InlineData("http://example.com/a\tb.txt", "http://example.com/a%09b.txt")]              // Tab
    [InlineData("http://example.com/a\u0000b.txt", "http://example.com/a%00b.txt")]          // NUL
    [InlineData("http://example.com/a\u0001b.txt", "http://example.com/a%01b.txt")]          // SOH
    [InlineData("http://example.com/a\u000Cb.txt", "http://example.com/a%0Cb.txt")]          // FF
    [InlineData("http://example.com/a\u007Fb.txt", "http://example.com/a%7Fb.txt")]          // DEL
    // CRLF injection via URL path (HTTP request smuggling attempt) must be
    // defused — the injected Host header and malicious request tail become
    // percent-escaped and harmless.
    [InlineData("http://example.com/file\r\nHost: evil.com\r\nGET /admin HTTP/1.1",
        "http://example.com/file%0D%0AHost:%20evil.com%0D%0AGET%20/admin%20HTTP/1.1")]
    public void EnsurePathEncodedEscapesControlCharacters(string input, string expected)
    {
        string actual = UrlHelper.EnsurePathEncoded(input);
        Assert.Equal(expected, actual);
        // Sanity: output contains no raw control characters.
        foreach (char c in actual)
            Assert.False(c < 0x20 || c == 0x7F, $"control char U+{(int)c:X4} leaked into output");
    }

    [Fact]
    public void EnsurePathEncodedDoesNotDecodeExistingEscapes()
    {
        // Defense against double-decode smuggling: an attacker-supplied
        // %2e%2e%2f must reach the server exactly as %2e%2e%2f so that
        // server-side path-traversal defenses see the true input. The helper
        // must not unescape on its own initiative.
        string input = "https://example.com/files/%2e%2e%2fetc%2fpasswd";
        string actual = UrlHelper.EnsurePathEncoded(input);

        // Lowercase hex is normalized to uppercase, but bytes stay encoded.
        Assert.Equal("https://example.com/files/%2E%2E%2Fetc%2Fpasswd", actual);
        Assert.DoesNotContain("..", actual);
        Assert.DoesNotContain("/etc/", actual);
    }

    [Fact]
    public void EnsurePathEncodedDoesNotAlterAuthority()
    {
        // Path encoding must never reach into the authority — an attacker
        // must not be able to influence host resolution via path content.
        const string maliciousHost = "attacker.example.com";
        string input = $"http://{maliciousHost}/legit/[path]";
        string actual = UrlHelper.EnsurePathEncoded(input);

        Assert.StartsWith($"http://{maliciousHost}/", actual);
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
        Assert.NotEqual("localhost", request.Address.Host);
        Assert.Equal("https", request.Address.Scheme);
        Assert.Contains("%5B", request.Address.AbsoluteUri);
        Assert.Contains("%5D", request.Address.AbsoluteUri);
        Assert.DoesNotContain("[", request.Address.AbsolutePath);
        Assert.DoesNotContain(" ", request.Address.AbsolutePath);
    }

    [Fact]
    public void RequestConstructorHandlesControlCharUrlWithoutInjection()
    {
        // Even if a caller somehow supplies a URL containing CRLF (e.g. from
        // a compromised upstream API), the normalized Address must not carry
        // raw control chars into the HTTP layer.
        string input = "http://example.com/a\r\nHost: evil.com/b.txt";

        Request request = new(input);

        Assert.Equal("example.com", request.Address.Host);
        foreach (char c in request.Address.AbsoluteUri)
            Assert.False(c < 0x20 || c == 0x7F, "control character reached Address.AbsoluteUri");
    }
}
