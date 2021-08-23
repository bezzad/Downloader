using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadManagerTest
    {
        private IDownloadRequest[] _successDownloadRequest;
        private IDownloadRequest[] _cancelledDownloadRequest;
        private IDownloadRequest[] _corruptedDownloadRequest;

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
            var downloadManager1 = new DownloadManager(new DownloadConfiguration(), maxNumber1);
            var downloadManager2 = new DownloadManager(new DownloadConfiguration(), maxNumber2);

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
            var downloadManager = new DownloadManager(new DownloadConfiguration(), maxNumber);

            // act
            downloadManager.DownloadAsync(_successDownloadRequest);

            // assert
            Assert.AreEqual(maxNumber, downloadManager.NumberOfDownloadsInProgress);
        }
    }
}