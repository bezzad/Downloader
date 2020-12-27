using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

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
        public void GetTempFileSpecialPathTest()
        {
            // arrange
            var baseUrl = Path.Combine(Path.GetTempPath(), "downloader", "test");

            // act
            var tempFile = GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(tempFile.StartsWith(baseUrl));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNoPathTest()
        {
            // arrange
            var baseUrl = " ";
            var tempFolder = Path.GetTempPath();

            // act
            var tempFile = GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(tempFile.StartsWith(tempFolder));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNullPathTest()
        {
            // arrange
            var tempFolder = Path.GetTempPath();

            // act
            var tempFile = GetTempFile(null);

            // assert
            Assert.IsTrue(tempFile.StartsWith(tempFolder));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileSpecialPathNonDuplicationTest()
        {
            // arrange
            var baseUrl = Path.Combine(Path.GetTempPath(), "downloader", "test");

            // act
            var tempFile1 = GetTempFile(baseUrl);
            var tempFile2 = GetTempFile(baseUrl);

            // assert
            Assert.AreNotEqual(tempFile1, tempFile2);

            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }

        [TestMethod]
        public void GetTempFileNoPathNonDuplicationTest()
        {
            // arrange
            var baseUrl = "     ";

            // act
            var tempFile1 = GetTempFile(baseUrl);
            var tempFile2 = GetTempFile(baseUrl);

            // assert
            Assert.AreNotEqual(tempFile1, tempFile2);

            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }

        [TestMethod]
        public void GetTempFileNullPathNonDuplicationTest()
        {
            // act
            var tempFile1 = GetTempFile(null);
            var tempFile2 = GetTempFile(null);

            // assert
            Assert.AreNotEqual(tempFile1, tempFile2);

            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }

        [TestMethod]
        public void GetTempFileSpecialPathCreationTest()
        {
            // arrange
            var baseUrl = Path.Combine(Path.GetTempPath(), "downloader", "test");

            // act
            var tempFile = GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNullPathCreationTest()
        {
            // act
            var tempFile = GetTempFile(null);

            // assert
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNoPathCreationTest()
        {
            // arrange
            var baseUrl = " ";

            // act
            var tempFile = GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }
    }
}
