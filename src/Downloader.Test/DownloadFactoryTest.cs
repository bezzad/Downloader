using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadFactoryTest
    {
        [TestMethod]
        public void TestCorrect()
        {
            var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(profilePath, "file.txt");
            IDownload download = DownloadBuilder.New()
                .WithUrl("http://google.com")
                .WithFileLocation(path)
                .Configure(config => {
                    config.ParallelDownload = true;
                })
                .Build();
            Assert.AreEqual(path, download.FilePath);
        }

        [TestMethod]
        public void TestSetFolderAndName()
        {
            var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(profilePath, "file.txt");
            IDownload download = DownloadBuilder.New()
                .WithUrl("http://google.com")
                .WithDirectory(profilePath)
                .WithFileName("file.txt")
                .Build();
            Assert.AreEqual(path, download.FilePath);
        }

        [TestMethod]
        public void TestSetFolder()
        {
            var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(profilePath, "file.txt");
            IDownload download = DownloadBuilder.New()
                .WithUrl("http://host.com/file.txt")
                .WithDirectory(profilePath)
                .Build();
            Assert.AreEqual(path, download.FilePath);
        }

        [TestMethod]
        public void TestSetName()
        {
            var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(profilePath, "file.txt");
            IDownload download = DownloadBuilder.New()
                .WithUrl("http://host.com/file2.txt")
                .WithFileLocation(path)
                .WithFileName("file.txt")
                .Build();
            Assert.AreEqual(path, download.FilePath);
        }

        [TestMethod]
        public void TestUrlless()
        {
            var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(profilePath, "file.txt");
            Assert.ThrowsException<ArgumentNullException>(() => {
                DownloadBuilder.New()
                    .WithFileLocation(path)
                    .Build();
            });
        }

        [TestMethod]
        public void TestPathless()
        {
            var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(profilePath, "file.txt");
            Assert.ThrowsException<ArgumentNullException>(() => {
                DownloadBuilder.New()
                    .WithUrl("http://host.com/link.txt")
                    .Build();
            });
        }
    }
}
