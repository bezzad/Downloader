using System.IO;
using System.Linq;
using Downloader.Test.Properties;

namespace Downloader.Test
{
    internal static class DownloadTestHelper
    {
        public const string TempFilesExtension = ".temp";
        public const string File1KbName = "Sample1Kb.test";
        public const string File16KbName = "Sample16Kb.test";
        public const string File150KbName = "Sample150Kb.test";
        public const int FileSize1Kb = 1024;
        public const int FileSize16Kb = 16*1024;
        public const int FileSize150Kb = 150*1024;

        public static readonly string File1KbUrl = $"https://github.com/bezzad/Downloader/raw/develop/src/Downloader.Test/Assets/{File1KbName}";
        public static readonly string File16KbUrl = $"https://github.com/bezzad/Downloader/raw/develop/src/Downloader.Test/Assets/{File16KbName}";
        public static readonly string File150KbUrl = $"https://github.com/bezzad/Downloader/raw/develop/src/Downloader.Test/Assets/{File150KbName}";
        public static readonly string TempDirectory = Path.GetTempPath();

        public static byte[] File1Kb => Resources.Sample1Kb;
        public static byte[] File16Kb => Resources.Sample16Kb;
        public static byte[] File150Kb => Resources.Sample150Kb;

        public static bool AreEqual(byte[] expected, Stream actual)
        {
            using (actual)
            {
                return actual?.Length == expected.Length &&
                       expected.All(expectedByte => actual.ReadByte() == expectedByte);
            }
        }
    }
}