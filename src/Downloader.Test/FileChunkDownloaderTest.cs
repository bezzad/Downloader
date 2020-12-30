using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class FileChunkDownloaderTest : FileChunkDownloader
    {
        public FileChunkDownloaderTest()
            : base(new FileChunk(0, 10000), 1024, DownloadTestHelper.TempDirectory,
                DownloadTestHelper.TempFilesExtension)
        {
        }

        public FileChunkDownloaderTest(FileChunk chunk, int blockSize, string tempDirectory, string tempFileExtension)
            : base(chunk, blockSize, tempDirectory, tempFileExtension)
        {
        }

        [TestMethod]
        public void GetTempFileSpecialPathTest()
        {
            // arrange
            string baseUrl = Path.Combine(Path.GetTempPath(), "downloader", "test");

            // act
            string tempFile = GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(tempFile.StartsWith(baseUrl));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNoPathTest()
        {
            // arrange
            string baseUrl = " ";
            string tempFolder = Path.GetTempPath();

            // act
            string tempFile = GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(tempFile.StartsWith(tempFolder));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNullPathTest()
        {
            // arrange
            string tempFolder = Path.GetTempPath();

            // act
            string tempFile = GetTempFile(null);

            // assert
            Assert.IsTrue(tempFile.StartsWith(tempFolder));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileSpecialPathNonDuplicationTest()
        {
            // arrange
            string baseUrl = Path.Combine(Path.GetTempPath(), "downloader", "test");

            // act
            string tempFile1 = GetTempFile(baseUrl);
            string tempFile2 = GetTempFile(baseUrl);

            // assert
            Assert.AreNotEqual(tempFile1, tempFile2);

            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }

        [TestMethod]
        public void GetTempFileNoPathNonDuplicationTest()
        {
            // arrange
            string baseUrl = "     ";

            // act
            string tempFile1 = GetTempFile(baseUrl);
            string tempFile2 = GetTempFile(baseUrl);

            // assert
            Assert.AreNotEqual(tempFile1, tempFile2);

            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }

        [TestMethod]
        public void GetTempFileNullPathNonDuplicationTest()
        {
            // act
            string tempFile1 = GetTempFile(null);
            string tempFile2 = GetTempFile(null);

            // assert
            Assert.AreNotEqual(tempFile1, tempFile2);

            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }

        [TestMethod]
        public void GetTempFileSpecialPathCreationTest()
        {
            // arrange
            string baseUrl = Path.Combine(Path.GetTempPath(), "downloader", "test");

            // act
            string tempFile = GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNullPathCreationTest()
        {
            // act
            string tempFile = GetTempFile(null);

            // assert
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNoPathCreationTest()
        {
            // arrange
            string baseUrl = " ";

            // act
            string tempFile = GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void ReadStreamTest()
        {
            // arrange
            Chunk.Clear();
            Chunk.Timeout = 100;
            var streamSize = 2048;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            using var memoryStream = new MemoryStream(randomlyBytes);

            // act
            ReadStream(memoryStream, new CancellationToken()).Wait();
            
            // assert
            using var stream = new FileStream(((FileChunk)Chunk).FileName, FileMode.Open, FileAccess.Read, FileShare.Delete);
            var chunkData = new byte[streamSize];
            stream.Read(chunkData, 0, streamSize);
            for (int i = 0; i < streamSize; i++)
            {
                Assert.AreEqual(randomlyBytes[i], chunkData[i]);
            }

            Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamProgressEventsTest()
        {
            // arrange
            Chunk.Clear();
            Chunk.Timeout = 100;
            var streamSize = 9 * 1024;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            using var memoryStream = new MemoryStream(randomlyBytes);
            var eventCount = 0;
            DownloadProgressChanged += delegate {
                eventCount++;
            };

            // act
            ReadStream(memoryStream, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(streamSize/BufferBlockSize, eventCount);

            Chunk.Clear();
        }
    }
}