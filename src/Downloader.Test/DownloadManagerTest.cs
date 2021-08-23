using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadManagerTest
    {
        private IDownloadService[] _successDownloadServices;
        private IDownloadService[] _cancelledDownloadServices;
        private IDownloadService[] _corruptedDownloadServices;

        [TestInitialize]
        public void Initial()
        {
            _successDownloadServices = new[] {
                DownloadServiceMockHelper.GetSuccessDownloadService(102400, 1024),
                DownloadServiceMockHelper.GetSuccessDownloadService(204800, 1024),
                DownloadServiceMockHelper.GetSuccessDownloadService(204800, 512),
                DownloadServiceMockHelper.GetSuccessDownloadService(102400, 512),
                DownloadServiceMockHelper.GetSuccessDownloadService(102400, 2048)
            };

            _cancelledDownloadServices = new[] {
                DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(102400, 1024),
                DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(204800, 1024),
                DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(204800, 512),
                DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(102400, 512),
                DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(102400, 2048)
            };

            _corruptedDownloadServices = new[] {
                DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(102400, 1024),
                DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(204800, 1024),
                DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(204800, 512),
                DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(102400, 512),
                DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(102400, 2048)
            };
        }

        [TestMethod]
        public void TestMaxNumberOfMultipleFileDownload()
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
            Assert.AreEqual(maxNumber1, downloadManager1.MaxNumberOfMultipleFileDownload);
            Assert.AreEqual(maxNumber2, downloadManager2.MaxNumberOfMultipleFileDownload);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DownloadManager(new DownloadConfiguration(), maxNumber3));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new DownloadManager(new DownloadConfiguration(), maxNumber4));
        }

        [TestMethod]
        public void TestNumberOfDownloadsBeforeAdding()
        {
            // arrange
            var maxNumber = 1;
            var downloadManager = new DownloadManager(new DownloadConfiguration(), maxNumber);

            // act

            // assert
           
        }
    }
}