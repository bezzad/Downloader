using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadServiceMockHelperTest
    {
        [TestMethod]
        public void GetSuccessDownloadServiceTest()
        {
            // arrange
            var totalSize = 102400;
            var bytesCountPerProgress = 1024;
            var mockDownloadService = DownloadServiceMockHelper.GetSuccessDownloadService(totalSize, bytesCountPerProgress);
            var states = new DownloadServiceEventsState(mockDownloadService);

            // act
            mockDownloadService.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl, DownloadTestHelper.File1KbName).Wait();

            // assert
            Assert.AreEqual(DownloadTestHelper.File1KbUrl, mockDownloadService.Package.Address);
            Assert.AreEqual(DownloadTestHelper.File1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DownloadTestHelper.File1KbName, states.ActualFileName);
            Assert.AreEqual(totalSize/bytesCountPerProgress, states.DownloadProgressCount);
            Assert.IsTrue(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsNull(states.DownloadError);
            Assert.IsTrue(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }

        [TestMethod]
        public void GetCancelledDownloadServiceOn50PercentTest()
        {
            // arrange
            var totalSize = 102400;
            var bytesCountPerProgress = 1024;
            var mockDownloadService = DownloadServiceMockHelper.GetCancelledDownloadServiceOn50Percent(totalSize, bytesCountPerProgress);
            var states = new DownloadServiceEventsState(mockDownloadService);

            // act
            mockDownloadService.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl, DownloadTestHelper.File1KbName).Wait();

            // assert
            Assert.AreEqual(DownloadTestHelper.File1KbUrl, mockDownloadService.Package.Address);
            Assert.AreEqual(DownloadTestHelper.File1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DownloadTestHelper.File1KbName, states.ActualFileName);
            Assert.AreEqual(totalSize/bytesCountPerProgress*0.5, states.DownloadProgressCount);
            Assert.IsFalse(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsNull(states.DownloadError);
            Assert.IsFalse(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }

        [TestMethod]
        public void GetCorruptedDownloadServiceOn30PercentTest()
        {
            // arrange
            var totalSize = 102400;
            var bytesCountPerProgress = 1024;
            var mockDownloadService = DownloadServiceMockHelper.GetCorruptedDownloadServiceOn30Percent(totalSize, bytesCountPerProgress);
            var states = new DownloadServiceEventsState(mockDownloadService);

            // act
            mockDownloadService.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl, DownloadTestHelper.File1KbName).Wait();

            // assert
            Assert.AreEqual(DownloadTestHelper.File1KbUrl, mockDownloadService.Package.Address);
            Assert.AreEqual(DownloadTestHelper.File1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DownloadTestHelper.File1KbName, states.ActualFileName);
            Assert.AreEqual(totalSize/bytesCountPerProgress*0.3, states.DownloadProgressCount);
            Assert.IsFalse(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsNotNull(states.DownloadError);
            Assert.IsFalse(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }
    }
}
