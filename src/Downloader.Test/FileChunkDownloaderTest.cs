using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class FileChunkDownloaderTest : FileChunkDownloader
    {
        public FileChunkDownloaderTest()
            : base(new FileChunk(0, 10000), 1024, DownloadTestHelper.TempDirectory, DownloadTestHelper.TempFilesExtension)
        { }

        public FileChunkDownloaderTest(FileChunk chunk, int blockSize, string tempDirectory, string tempFileExtension)
            : base(chunk, blockSize, tempDirectory, tempFileExtension)
        { }

        [TestMethod]
        public void GetTempFileTest()
        {
            var baseUrl = "C:\\temp";
            var tempFile = GetTempFile(baseUrl);
            Assert.IsTrue(tempFile.StartsWith(baseUrl));
            Assert.AreNotEqual(GetTempFile(baseUrl), GetTempFile(baseUrl));
            Assert.AreNotEqual(GetTempFile(null), GetTempFile(null));
            Assert.IsTrue(File.Exists(GetTempFile(baseUrl)));
            Assert.IsTrue(GetTempFile("").StartsWith(Path.GetTempPath()));
            Assert.IsTrue(GetTempFile("     ").StartsWith(Path.GetTempPath()));
            Assert.IsTrue(GetTempFile(null).StartsWith(Path.GetTempPath()));

            Directory.Delete(baseUrl, true);
        }
    }
}
