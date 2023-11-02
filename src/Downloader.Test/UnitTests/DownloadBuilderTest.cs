using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

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
            IDownload result = DownloadBuilder.New()
                .WithUrl(url)
                .Build();

            // assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void TestPackageWhenNewUrl()
        {
            // arrange
            DownloadPackage beforePackage = null;
            IDownload download = DownloadBuilder.New()
                .WithUrl(url)
                .Build();

            // act
            beforePackage = download.Package;
            download.StartAsync().Wait();

            // assert
            Assert.IsNotNull(beforePackage);
            Assert.IsNotNull(download.Package);
            Assert.AreEqual(beforePackage, download.Package);
            Assert.IsTrue(beforePackage.IsSaveComplete);
        }

        [TestMethod]
        public void TestPackageWhenResume()
        {
            // arrange
            DownloadPackage package = new DownloadPackage() {
                Urls = new[] { url },
                IsSupportDownloadInRange = true
            };
            IDownload download = DownloadBuilder.New().Build(package);
            DownloadPackage beforeStartPackage = download.Package;

            // act
            download.StartAsync().Wait();

            // assert
            Assert.IsNotNull(beforeStartPackage);
            Assert.IsNotNull(download.Package);
            Assert.AreEqual(beforeStartPackage, download.Package);
            Assert.AreEqual(beforeStartPackage, package);
            Assert.IsTrue(package.IsSaveComplete);
        }

        [TestMethod]
        public async Task TestPauseAndResume()
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
            await downloader.StartAsync();

            // assert
            Assert.IsTrue(downloader.Package?.IsSaveComplete);
            Assert.AreEqual(10, pauseCount);
            Assert.IsTrue(File.Exists(path));

            // clean up
            File.Delete(path);
        }

        [TestMethod]
        public async Task TestOverwriteFileWithDownloadSameLocation()
        {
            // arrange
            var content = "THIS IS TEST CONTENT WHICH MUST BE OVERWRITE WITH THE DOWNLOADER";
            var downloader = DownloadBuilder.New()
                            .WithUrl(url)
                            .WithFileLocation(path)
                            .Build();

            // act
            await File.WriteAllTextAsync(path, content); // create file
            await downloader.StartAsync(); // overwrite file
            var file = await File.ReadAllTextAsync(path); 

            // assert
            Assert.IsTrue(downloader.Package?.IsSaveComplete);
            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(file.StartsWith(content));
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, Encoding.ASCII.GetByteCount(file));

            // clean up
            File.Delete(path);
        }

        [TestMethod]
        public async Task TestOverwriteFileWithDownloadSameFileName()
        {
            // arrange
            var content = "THIS IS TEST CONTENT WHICH MUST BE OVERWRITE WITH THE DOWNLOADER";
            var downloader = DownloadBuilder.New()
                            .WithUrl(url)
                            .WithDirectory(folder)
                            .WithFileName(filename)
                            .Build();

            // act
            await File.WriteAllTextAsync(path, content); // create file
            await downloader.StartAsync(); // overwrite file
            var file = await File.ReadAllTextAsync(path);

            // assert
            Assert.IsTrue(downloader.Package?.IsSaveComplete);
            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(file.StartsWith(content));
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, Encoding.ASCII.GetByteCount(file));

            // clean up
            File.Delete(path);
        }
    }
}
