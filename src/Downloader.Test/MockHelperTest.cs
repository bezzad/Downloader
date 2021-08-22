using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class MockHelperTest
    {
        private class DownloadTestStates
        {
            public string ActualFileName { get; set; }
            public bool DownloadSuccessfullCompleted { get; set; }
            public bool DownloadProgressIsCorrect { get; set; } = true;
            public int DownloadProgressCount { get; set; } = 0;

            public DownloadTestStates(IDownloadService mockDownloadService)
            {
                mockDownloadService.DownloadStarted += (s, e) => ActualFileName = e.FileName;
                mockDownloadService.DownloadFileCompleted += (s, e) => DownloadSuccessfullCompleted = e.Error == null && !e.Cancelled;
                mockDownloadService.DownloadProgressChanged += (s, e) => {
                    DownloadProgressCount++;
                    DownloadProgressIsCorrect &= (e.ProgressPercentage == mockDownloadService.Package.SaveProgress);
                };
            }
        }

        [TestMethod]
        public void GetSuccessDownloadServiceTest()
        {
            // arrange
            var totalSize = 102400;
            var bytesCountPerProgress = 1024;
            var mockDownloadService = MockHelper.GetSuccessDownloadService(totalSize, bytesCountPerProgress);
            var states = new DownloadTestStates(mockDownloadService);

            // act
            mockDownloadService.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl, DownloadTestHelper.File1KbName).Wait();

            // assert
            Assert.AreEqual(DownloadTestHelper.File1KbUrl, mockDownloadService.Package.Address);
            Assert.AreEqual(DownloadTestHelper.File1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DownloadTestHelper.File1KbName, states.ActualFileName);
            Assert.AreEqual(totalSize/bytesCountPerProgress, states.DownloadProgressCount);
            Assert.IsTrue(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsTrue(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }

        [TestMethod]
        public void GetCancelledDownloadServiceTest()
        {
            // arrange
            var totalSize = 102400;
            var bytesCountPerProgress = 1024;
            var mockDownloadService = MockHelper.GetCancelledDownloadService(totalSize, bytesCountPerProgress);
            var states = new DownloadTestStates(mockDownloadService);

            // act
            mockDownloadService.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl, DownloadTestHelper.File1KbName).Wait();

            // assert
            Assert.AreEqual(DownloadTestHelper.File1KbUrl, mockDownloadService.Package.Address);
            Assert.AreEqual(DownloadTestHelper.File1KbName, mockDownloadService.Package.FileName);
            Assert.AreEqual(DownloadTestHelper.File1KbName, states.ActualFileName);
            Assert.AreEqual(totalSize/(2*bytesCountPerProgress), states.DownloadProgressCount);
            Assert.IsFalse(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsFalse(mockDownloadService.Package.IsSaveComplete);
            Assert.IsFalse(mockDownloadService.Package.IsSaving);
        }
    }
}
