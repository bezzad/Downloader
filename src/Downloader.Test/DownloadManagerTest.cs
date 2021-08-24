using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadManagerTest
    {
        private IDownloadRequest[] _successDownloadRequest;
        private IDownloadRequest[] _cancelledDownloadRequest;
        private IDownloadRequest[] _corruptedDownloadRequest;
        private IDownloadRequest[] _emptyDownloadRequest;

        [TestInitialize]
        public void Initial()
        {
            _successDownloadRequest = new[] {
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(102400, 1024) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(204800, 1024) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(102400, 512) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(204800, 512) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(102400, 2048) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(204800, 2048) }
            };

            _cancelledDownloadRequest = new[] {
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(102400, 1024) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(204800, 1024) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(102400, 512) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(204800, 512) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(102400, 2048) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(204800, 2048) }
            };

            _corruptedDownloadRequest = new[] {
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(102400, 1024) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(204800, 1024) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(102400, 512) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(204800, 512) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(102400, 2048) },
                new DownloadRequest() { DownloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(204800, 2048) }
            };

            _emptyDownloadRequest = new[] {
                new DownloadRequest(),
                new DownloadRequest(),
                new DownloadRequest(),
                new DownloadRequest(),
                new DownloadRequest(),
                new DownloadRequest()
            };
        }

        [TestMethod]
        public void TestMaxConcurrentDownloadsDegree()
        {
            // arrange
            var maxNumber1 = 1;
            var maxNumber2 = 100;
            var maxNumber3 = 0;
            var maxNumber4 = -5;

            // act
            using var downloadManager1 = new DownloadManager(new DownloadConfiguration(), maxNumber1);
            using var downloadManager2 = new DownloadManager(new DownloadConfiguration(), maxNumber2);

            // assert
            Assert.AreEqual(maxNumber1, downloadManager1.MaxConcurrentDownloadsDegree);
            Assert.AreEqual(maxNumber2, downloadManager2.MaxConcurrentDownloadsDegree);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DownloadManager(new DownloadConfiguration(), maxNumber3));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DownloadManager(new DownloadConfiguration(), maxNumber4));
        }

        [TestMethod]
        public void TestNumberOfDownloadsInProgress()
        {
            // arrange
            var maxNumber = 1;
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), maxNumber);

            // act
            downloadManager.DownloadAsync(_successDownloadRequest);

            // assert
            Assert.AreEqual(maxNumber, downloadManager.NumberOfDownloadsInProgress);
        }

        [TestMethod]
        public void TestNullPathException()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            Action act = () => downloadManager.DownloadAsync(DownloadTestHelper.File16KbUrl, null);

            // assert
            Assert.ThrowsException<ArgumentNullException>(act);
        }

        [TestMethod]
        public void TestNullUrlException()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            Action act = () => downloadManager.DownloadAsync(null, Path.GetTempPath());

            // assert
            Assert.ThrowsException<ArgumentNullException>(act);
        }

        [TestMethod]
        public void TestGetDownloadRequests()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);

            // act
            downloadManager.DownloadAsync(_emptyDownloadRequest);
            var requests = downloadManager.GetDownloadRequests();

            // assert
            Assert.AreEqual(_successDownloadRequest.Length, requests.Count);
            for (var i = 0; i<requests.Count; i++)
            {
                Assert.AreEqual(_successDownloadRequest[i].Url, requests[i].Url);
                Assert.AreEqual(_successDownloadRequest[i].Path, requests[i].Path);
                Assert.IsNotNull(requests[i].DownloadService);
            }
        }

        [TestMethod]
        public void TestCancelAsync()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 1);
            var slowlyDownloadService = DownloadServiceMockHelper.GetSpecialDownloadService(102400, 1024, 100, 100, false, null);
            var request = new DownloadRequest() {
                Url = DownloadTestHelper.File1KbUrl,
                Path = Path.GetTempPath(),
                DownloadService = slowlyDownloadService
            };

            // act
            downloadManager.DownloadAsync(request);
            downloadManager.CancelAsync(request);

            // assert
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
            Assert.IsFalse(request.IsSaving);
            Assert.IsFalse(request.IsSaveComplete);
        }

        [TestMethod]
        public void TestCancelAllAsync()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 3);            

            // act
            downloadManager.DownloadAsync(_successDownloadRequest);
            downloadManager.CancelAllAsync();

            // assert
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
            foreach(var request in _successDownloadRequest)
            {
                Assert.IsFalse(request.IsSaving);
                Assert.IsFalse(request.IsSaveComplete);
            }            
        }

        [TestMethod]
        public void TestClear()
        {
            // arrange
            using var downloadManager = new DownloadManager(new DownloadConfiguration(), 2);

            // act
            downloadManager.DownloadAsync(_successDownloadRequest);
            downloadManager.Clear();
            var requests = downloadManager.GetDownloadRequests();

            // assert
            Assert.AreEqual(0, downloadManager.NumberOfDownloadsInProgress);
            Assert.AreEqual(0, requests.Count);
        }
    }
}