using System.IO;
using System.Linq;

namespace Downloader.DummyHttpServer;

public static class DummyFileHelper
{
    public const string TempFilesExtension = ".temp";
    public const string SampleFile1KbName = "Sample1Kb.test";
    public const string SampleFile16KbName = "Sample16Kb.test";
    public static readonly string TempDirectory = Path.GetTempPath();
    public static int Port => HttpServer.Port;
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
