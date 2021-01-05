using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class FileHelperTest
    {
        [TestMethod]
        public void CreateFileSpecialPathTest()
        {
            // arrange
            string baseUrl = Path.Combine(Path.GetTempPath(), "downloader", "test");
            string filename = Path.Combine(baseUrl, Guid.NewGuid().ToString("N") + ".test");

            // act
            FileHelper.CreateFile(filename).Dispose();

            // assert
            Assert.IsTrue(File.Exists(filename));

            File.Delete(filename);
        }

        [TestMethod]
        public void CreateFileNoPathTest()
        {
            // arrange
            string baseUrl = "  ";
            string filename = Path.Combine(baseUrl, Guid.NewGuid().ToString("N") + ".test");

            // act
            var fileStream = FileHelper.CreateFile(filename);

            // assert
            Assert.AreEqual(Stream.Null, fileStream);
        }

        [TestMethod]
        public void GetTempFileSpecialPathTest()
        {
            // arrange
            string baseUrl = Path.Combine(Path.GetTempPath(), "downloader", "test");

            // act
            string tempFile = FileHelper.GetTempFile(baseUrl);

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
            string tempFile = FileHelper.GetTempFile(baseUrl);

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
            string tempFile = FileHelper.GetTempFile(null);

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
            string tempFile1 = FileHelper.GetTempFile(baseUrl);
            string tempFile2 = FileHelper.GetTempFile(baseUrl);

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
            string tempFile1 = FileHelper.GetTempFile(baseUrl);
            string tempFile2 = FileHelper.GetTempFile(baseUrl);

            // assert
            Assert.AreNotEqual(tempFile1, tempFile2);

            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }

        [TestMethod]
        public void GetTempFileNullPathNonDuplicationTest()
        {
            // act
            string tempFile1 = FileHelper.GetTempFile(null);
            string tempFile2 = FileHelper.GetTempFile(null);

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
            string tempFile = FileHelper.GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void GetTempFileNullPathCreationTest()
        {
            // act
            string tempFile = FileHelper.GetTempFile(null);

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
            string tempFile = FileHelper.GetTempFile(baseUrl);

            // assert
            Assert.IsTrue(File.Exists(tempFile));

            File.Delete(tempFile);
        }

        [TestMethod]
        public void CheckDiskSizeIOExceptionTest()
        {
            // arrange
            var mainDriveRoot = Directory.GetDirectoryRoot(Environment.SystemDirectory);
            var mainDrive = new DriveInfo(mainDriveRoot);
            var mainDriveAvailableFreeSpace = mainDrive.AvailableFreeSpace;

            // act
            void CheckDiskSize() => FileHelper.CheckDiskSize(Environment.SystemDirectory, mainDriveAvailableFreeSpace);

            // assert
            Assert.ThrowsException<IOException>(CheckDiskSize);
        }
    }
}
