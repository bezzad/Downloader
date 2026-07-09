using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public static class DummyFileHelper
{
    public const string TempFilesExtension = ".temp";
    public const string SampleFile1KbName = "Sample1Kb.test";
    public const string SampleFile16KbName = "Sample16Kb.test";
    public static readonly string TempDirectory = Path.GetTempPath();
    private static int Port => HttpServer.Port;
    public static int FileSize1Kb => 1024;
    public static int FileSize16Kb => 16 * 1024;
    public static readonly byte[] File1Kb = DummyData.GenerateOrderedBytes(FileSize1Kb);
    public static readonly byte[] File16Kb = DummyData.GenerateOrderedBytes(FileSize16Kb);

    static DummyFileHelper()
    {
        HttpServer.Run(0); // with dynamic port
    }

    public static string GetFileUrl(long size)
    {
        return $"http://localhost:{Port}/dummyfile/file/size/{size}";
    }

    public static string GetFileWithNameUrl(string filename, long size, byte? fillByte = null)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}?size={size}"
            + (fillByte == null ? "" : $"&fillByte={fillByte}");
    }

    public static string GetFileWithNameOnRedirectUrl(string filename, long size)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}/redirect?size={size}";
    }

    /// <summary>
    /// Returns a URL guarded by a CDN-style cookie wall: the first request is answered with a
    /// "307 to self" plus a Set-Cookie, and only the retry carrying that cookie is served the file.
    /// </summary>
    public static string GetFileBehindCookieChallengeUrl(string filename, long size)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}/cookie-challenge?size={size}";
    }

    public static string GetFileWithoutHeaderUrl(string filename, long size, byte? fillByte = null)
    {
        return $"http://localhost:{Port}/dummyfile/noheader/file/{filename}?size={size}"
            + (fillByte == null ? "" : $"&fillByte={fillByte}");
    }

    public static string GetFileWithContentDispositionUrl(string filename, long size, byte? fillByte = null)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}/size/{size}"
            + (fillByte == null ? "" : $"?fillByte={fillByte}");
    }

    public static string GetFileWithNoAcceptRangeUrl(string filename, long size, byte? fillByte = null)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}/size/{size}/norange"
            + (fillByte == null ? "" : $"?fillByte={fillByte}");
    }

    public static string GetFileWithFailureAfterOffset(long size, int failureOffset)
    {
        return $"http://localhost:{Port}/dummyfile/file/size/{size}/failure/{failureOffset}";
    }

    public static string GetFileWithTimeoutAfterOffset(long size, int timeoutOffset)
    {
        return $"http://localhost:{Port}/dummyfile/file/size/{size}/timeout/{timeoutOffset}";
    }

    /// <summary>
    /// Returns a URL whose server endpoint validates the User-Agent header and
    /// returns HTTP 428 for invalid/AOT-produced User-Agent values (issue #226).
    /// </summary>
    public static string GetFileRequiringValidUserAgentUrl(string filename, long size)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}/check-useragent?size={size}";
    }

    /// <summary>
    /// Returns a URL whose server advertises <paramref name="size"/> bytes on the range probe
    /// but delivers only <paramref name="actualSize"/> bytes on the body GET with a clean EOF,
    /// leaving the chunk incomplete without any transport error (issue #231).
    /// </summary>
    public static string GetTruncatedFileUrl(string filename, long size, long actualSize)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}/size/{size}/truncate/{actualSize}";
    }

    /// <summary>
    /// Returns a URL whose server fails every parallel/range chunk request with HTTP 503 but
    /// serves the full file to a single no-Range request — simulating an environment where
    /// concurrent connections are broken but a single connection works (issue #231).
    /// </summary>
    public static string GetFileFailingOnRangeRequestsUrl(string filename, long size)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}/size/{size}/failrange";
    }

    /// <summary>
    /// Returns a URL whose server serves a gzip-compressed representation of <paramref name="size"/>
    /// decompressed bytes, advertising <c>Content-Encoding: gzip</c> and a <c>Content-Length</c>
    /// equal to the compressed byte count, with no range support (issue #236).
    /// </summary>
    public static string GetGzipCompressedFileUrl(string filename, long size)
    {
        return $"http://localhost:{Port}/dummyfile/file/{filename}/size/{size}/gzip";
    }

    public static bool AreEqual(this byte[] expected, Stream actual)
    {
        using (actual)
        {
            actual.Seek(0, SeekOrigin.Begin);
            return actual?.Length == expected.Length &&
                   expected.All(expectedByte => actual.ReadByte() == expectedByte);
        }
    }
}
