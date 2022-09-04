using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class DownloadBuilderTest
    {
        // arrange
        private string url;
        private string filename;
        private string folder;
        private string path;

        public DownloadBuilderTest()
        {
            // arrange
            url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            filename = "test.txt";
            folder = Path.GetTempPath().TrimEnd('\\', '/');
            path = Path.Combine(folder, filename);
        }

        [TestMethod]
        public void TestCorrect()
        {
            // act
            IDownload download = DownloadBuilder.New()
                .WithUrl(url)
                .WithFileLocation(path)
                .Configure(config => {
                    config.ParallelDownload = true;
                })
                .Build();

            // assert
            Assert.AreEqual(folder, download.Folder);
            Assert.AreEqual(filename, download.Filename);
        }

        [TestMethod]
        public void TestSetFolderAndName()
        {
            // act
            IDownload download = DownloadBuilder.New()
                .WithUrl(url)
                .WithDirectory(folder)
                .WithFileName(filename)
                .Build();

            // assert
            Assert.AreEqual(folder, download.Folder);
            Assert.AreEqual(filename, download.Filename);
        }

        [TestMethod]
        public void TestSetFolder()
        {
            // arrange
            var dir = Path.GetTempPath();

            // act
            IDownload download = DownloadBuilder.New()
                .WithUrl(url)
                .WithDirectory(dir)
                .Build();

            // assert
            Assert.AreEqual(dir, download.Folder);
            Assert.IsNull(download.Filename);
        }

        [TestMethod]
        public void TestSetName()
        {
            // act
            IDownload download = DownloadBuilder.New()
                .WithUrl(url)
                .WithFileLocation(path)
                .WithFileName(filename)
                .Build();

            // assert
            Assert.AreEqual(folder, download.Folder);
            Assert.AreEqual(filename, download.Filename);
        }

        [TestMethod]
        public void TestUrlless()
        {
            // act
            Action act = () => DownloadBuilder.New().WithFileLocation(path).Build();

            // assert
            Assert.ThrowsException<ArgumentNullException>(act);
        }

        [TestMethod]
        public void TestPathless()
        {
            // act
            Action act = () => DownloadBuilder.New().WithUrl(url).Build();

            // assert
            Assert.ThrowsException<ArgumentNullException>(act);
        }

        [TestMethod]
        public void TestPauseAndResume()
        {
            // arrange
            var pauseCount = 0;
            var downloader = DownloadBuilder.New()
                .WithUrl(url)
                .WithFileLocation(path)
                .Build();

            downloader.DownloadProgressChanged += (s, e) => {
                if (pauseCount < 10)
                {
                    downloader.Pause();
                    pauseCount++;
                    downloader.Resume();
                }
            };

            // act
            downloader.StartAsync().Wait();

            // assert
            Assert.AreEqual(10, pauseCount);
            Assert.IsTrue(File.Exists(path));

            // clean up
            File.Delete(path);
        }
    }
}
