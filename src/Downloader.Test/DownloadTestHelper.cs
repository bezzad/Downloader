using System.IO;
using System.Linq;
using Downloader.Test.Properties;

namespace Downloader.Test
{
    internal static class DownloadTestHelper
    {
        public const string TempFilesExtension = ".temp";
        public const string File1KbName = "Sample1Kb.json";
        public const string File16KbName = "Sample16Kb.json";
        public const string File150KbName = "Sample150Kb.pdf";
        public const int FileSize1Kb = 1074;
        public const int FileSize16Kb = 15547;
        public const int FileSize150Kb = 142786;

        public static readonly string File1KbUrl = $"https://raw.githubusercontent.com/bezzad/Downloader/develop/src/Downloader.Test/Assets/{File1KbName}";
        public static readonly string File16KbUrl = $"https://raw.githubusercontent.com/bezzad/Downloader/develop/src/Downloader.Test/Assets/{File16KbName}";
        public static readonly string File150KbUrl = $"https://raw.githubusercontent.com/bezzad/Downloader/develop/src/Downloader.Test/Assets/{File150KbName}";
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