using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class MockHelperTest
    {
        [TestMethod]
        public void GetCorrectDownloadServiceTest()
        {
            // arrange
            var mockDownloadService = MockHelper.GetCorrectDownloadService(102400);
            var ActualFileName = ""; // DownloadTestHelper.File1KbName;
            var downloadWasSuccessfull = false;
            var downloadProgressIsCorrect = true;
            mockDownloadService.DownloadStarted += (s, e) => ActualFileName = e.FileName;
            mockDownloadService.DownloadFileCompleted += (s, e) => downloadWasSuccessfull = e.Error == null && !e.Cancelled;
            mockDownloadService.DownloadProgressChanged += (s, e) =>
                downloadProgressIsCorrect &= (e.ProgressPercentage == mockDownloadService.Package.SaveProgress);

            // act
            mockDownloadService.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl, DownloadTestHelper.File1KbName).Wait();

            // assert
            Assert.IsTrue(mockDownloadService.IsBusy);
            Assert.AreEqual(DownloadTestHelper.File1KbUrl, mockDownloadService.Package.Address);
            Assert.AreEqual(DownloadTestHelper.File1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DownloadTestHelper.File1KbName, ActualFileName);
            Assert.IsTrue(downloadWasSuccessfull);
            Assert.IsTrue(downloadProgressIsCorrect);
            Assert.IsTrue(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }
    }
}
